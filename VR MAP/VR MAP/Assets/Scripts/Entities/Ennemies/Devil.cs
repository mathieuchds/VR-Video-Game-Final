using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Petit démon volant :   se déplace rapidement vers le joueur.   
/// Reste à distance (outerCircle), puis dash à travers le joueur quand prêt.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Devil : Enemy
{
    [Header("Références")]
    [SerializeField] private GameObject targetObject;
    private Transform target;
    [SerializeField] private Animator animator;

    [Header("Distances & Zones")]
    [SerializeField] private float outerCircleRadius = 15f; // Distance de maintien autour du joueur
    [SerializeField] private float innerCircleRadius = 3f;  // Distance minimale du dash (traversée)

    [Header("Déplacement")]
    [SerializeField] private float normalSpeed = 8f;        // Vitesse normale d'approche
    [SerializeField] private float dashSpeed = 30f;         // Vitesse du dash
    [SerializeField] private float flyHeight = 3f;          // Hauteur de vol au-dessus du sol

    [Header("Combat")]
    [SerializeField] private float dashDamage = 15f;        // Dégâts du dash
    [SerializeField] private float chillDuration = 1.5f;    // Temps de repos après dash
    [SerializeField] private float dashHitRadius = 2f;      // Rayon de détection pendant le dash
    [SerializeField] private float dashHeightOffset = 0.5f; // Hauteur au-dessus du joueur pendant le dash

    [Header("Cooldowns")]
    [SerializeField] private float dashCooldown = 3f;       // Cooldown entre deux dashs
    [SerializeField] private float dashPreparationTime = 0.5f; // Temps avant de lancer le dash

    // États pour l'Animator
    private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int ParamIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int ParamIsChilling = Animator.StringToHash("IsChilling");
    private static readonly int TriggerHit = Animator.StringToHash("Hit");

    // État interne
    private enum DevilState
    {
        Idle,           // Repos
        Approaching,    // Se déplace vers le cercle extérieur (IsRunning)
        Orbiting,       // Tourne autour du joueur à distance
        Charging,       // En train de dasher (IsCharging)
        Chilling        // Repos après dash (IsChilling)
    }

    private DevilState currentState = DevilState.Idle;
    private bool canDash = true;
    private bool hasHitPlayerThisDash = false;
    private Vector3 dashDirection;
    private Vector3 dashEndPosition;
    private Vector3 dashStartPosition;
    private Vector3 dashMidPosition; // Point au niveau du joueur
    private float dashProgress = 0f;
    private Vector3 orbitPosition;
    private float orbitAngle = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private Collider devilCollider;
    private int originalLayer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (agent != null)
        {
            agent.speed = normalSpeed;
            agent.updatePosition = false; // ✅ On gère la position manuellement pendant le dash
            agent.updateRotation = false;

            // ✅ Permettre au Devil de voler (ignorer le NavMesh en hauteur)
            agent.baseOffset = flyHeight;

            if (rb != null)
                rb.isKinematic = true;
        }

        health = maxHealth;

        if (healthBar != null)
            healthBar.SetHealth(1f);

        // Trouver le joueur
        if (targetObject != null)
            target = targetObject.transform;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        if (target == null)
            Debug.LogWarning($"[Devil:{name}] Aucun joueur trouvé !");
        else
            Debug.Log($"[Devil:{name}] Target trouvé : {target.name}");

        currentState = DevilState.Approaching;

        devilCollider = GetComponent<Collider>();
        if (devilCollider == null)
            devilCollider = GetComponentInChildren<Collider>();

        originalLayer = gameObject.layer;
    }

    void Update()
    {
        if (target == null || isStunned) return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        switch (currentState)
        {
            case DevilState.Approaching:
                HandleApproaching(distanceToPlayer);
                break;

            case DevilState.Orbiting:
                HandleOrbiting(distanceToPlayer);
                break;

            case DevilState.Charging:
                HandleCharging();
                break;

            case DevilState.Chilling:
                // État géré par coroutine
                break;
        }

        // ✅ Synchroniser le NavMeshAgent quand on ne dash pas
        if (currentState != DevilState.Charging && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }

        // Rotation vers la direction de mouvement (sauf en chill)
        if (currentState != DevilState.Chilling)
        {
            Vector3 lookDirection = (currentState == DevilState.Charging) ? dashDirection : (target.position - transform.position);
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }

    private void HandleApproaching(float distanceToPlayer)
    {
        // ✅ Se déplacer vers le bord du cercle extérieur (pas vers le joueur directement)
        if (distanceToPlayer > outerCircleRadius + 2f)
        {
            // Trop loin :  se rapprocher
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = normalSpeed;

                // Destination : un point à outerCircleRadius du joueur
                Vector3 directionToPlayer = (target.position - transform.position).normalized;
                Vector3 targetPos = target.position - directionToPlayer * outerCircleRadius;
                targetPos.y += flyHeight;

                agent.SetDestination(targetPos);
                transform.position = agent.nextPosition;
            }

            UpdateAnimatorState(isRunning: true, isCharging: false, isChilling: false);
        }
        else
        {
            // ✅ Arrivé à distance : passer en mode orbite
            currentState = DevilState.Orbiting;
            Debug.Log($"[Devil:{name}] 🔄 Passage en mode Orbiting");
        }
    }

    private void HandleOrbiting(float distanceToPlayer)
    {
        // ✅ Tourner autour du joueur à distance outerCircleRadius
        orbitAngle += Time.deltaTime * 50f; // Vitesse de rotation (degrés/sec)

        float radian = orbitAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(radian), 0f, Mathf.Sin(radian)) * outerCircleRadius;
        orbitPosition = target.position + offset;
        orbitPosition.y = target.position.y + flyHeight;

        // Se déplacer vers la position d'orbite
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = normalSpeed;
            agent.SetDestination(orbitPosition);
            transform.position = agent.nextPosition;
        }

        UpdateAnimatorState(isRunning: true, isCharging: false, isChilling: false);

        // ✅ Si on peut dasher, préparer l'attaque
        if (canDash)
        {
            StartCoroutine(PrepareDash());
        }
    }

    private IEnumerator PrepareDash()
    {
        canDash = false; // Empêcher de relancer pendant la préparation

        // Attendre un peu (le Devil se positionne)
        yield return new WaitForSeconds(dashPreparationTime);

        StartDash();
    }

    private void HandleCharging()
    {
        // ✅ Vérifier si on touche le joueur pendant le dash
        if (!hasHitPlayerThisDash)
        {
            float distToPlayer = Vector3.Distance(transform.position, target.position);
            if (distToPlayer <= dashHitRadius)
            {
                HitPlayer();
            }
        }
    }

    private void StartDash()
    {
        Debug.Log($"[Devil:{name}] 🔥 DASH DÉMARRÉ !");

        currentState = DevilState.Charging;
        hasHitPlayerThisDash = false;
        dashProgress = 0f;

        // Arrêter le NavMeshAgent pendant le dash
        if (agent != null && agent.enabled)
            agent.isStopped = true;

        // ✅ Changer le layer pour "Ignore Raycast" (pas de collisions)
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        // Aussi changer les enfants
        foreach (Transform child in transform)
        {
            child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // ✅ Point de départ :  position actuelle (en hauteur)
        dashStartPosition = transform.position;

        // ✅ Point du milieu :  DIRECTEMENT sur le joueur (à dashHeightOffset au-dessus)
        dashMidPosition = target.position + Vector3.up * dashHeightOffset;

        // ✅ Calculer la direction horizontale du dash
        Vector3 horizontalDirection = new Vector3(
            target.position.x - dashStartPosition.x,
            0f,
            target.position.z - dashStartPosition.z
        ).normalized;

        dashDirection = horizontalDirection;

        // ✅ Point d'arrivée : de l'AUTRE CÔTÉ du joueur, en hauteur
        dashEndPosition = target.position + horizontalDirection * (outerCircleRadius - 2f);
        dashEndPosition.y = target.position.y + flyHeight;

        UpdateAnimatorState(isRunning: false, isCharging: true, isChilling: false);

        StartCoroutine(DashCoroutine());
    }

    private IEnumerator DashCoroutine()
    {
        float totalDistance = Vector3.Distance(dashStartPosition, dashMidPosition) + Vector3.Distance(dashMidPosition, dashEndPosition);
        float dashDuration = totalDistance / dashSpeed;
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            dashProgress = elapsed / dashDuration;

            // ✅ Trajectoire en 2 phases :  descente puis remontée
            Vector3 newPosition;

            if (dashProgress < 0.5f)
            {
                // Phase 1 :  Descente vers le joueur (0 → 0.5)
                float phase1Progress = dashProgress * 2f; // 0 → 1
                newPosition = Vector3.Lerp(dashStartPosition, dashMidPosition, phase1Progress);
            }
            else
            {
                // Phase 2 : Remontée de l'autre côté (0.5 → 1)
                float phase2Progress = (dashProgress - 0.5f) * 2f; // 0 → 1
                newPosition = Vector3.Lerp(dashMidPosition, dashEndPosition, phase2Progress);
            }

            // ✅ Appliquer la position (ignore collisions)
            transform.position = newPosition;

            // Vérifier si on touche le joueur pendant le dash
            if (!hasHitPlayerThisDash)
            {
                float distToPlayer = Vector3.Distance(transform.position, target.position);
                if (distToPlayer <= dashHitRadius)
                {
                    HitPlayer();
                }
            }

            yield return null;
        }

        // Fin du dash
        transform.position = dashEndPosition;

        Debug.Log($"[Devil:{name}] ✅ Dash terminé, entrée en chill.");
        StartChill();
    }

    private void HitPlayer()
    {
        hasHitPlayerThisDash = true;

        Debug.Log($"[Devil:{name}] 💥 HIT JOUEUR pendant le dash !");

        // Trigger l'animation Hit
        if (animator != null)
            animator.SetTrigger(TriggerHit);

        // Infliger des dégâts
        PlayerStats ps = target.GetComponent<PlayerStats>();
        if (ps != null)
        {
            ps.TakeDamage(dashDamage);
            Debug.Log($"[Devil:{name}] ✅ {dashDamage} dégâts infligés !");
        }
    }

    private void StartChill()
    {
        currentState = DevilState.Chilling;

        // ✅ Restaurer le layer original
        gameObject.layer = originalLayer;

        foreach (Transform child in transform)
        {
            child.gameObject.layer = originalLayer;
        }

        UpdateAnimatorState(isRunning: false, isCharging: false, isChilling: true);

        StartCoroutine(ChillCoroutine());
    }

    private IEnumerator ChillCoroutine()
    {
        // Repos pendant chillDuration secondes
        yield return new WaitForSeconds(chillDuration);

        Debug.Log($"[Devil:{name}] 😎 Chill terminé, retour en orbite.");

        // Retour en mode orbite
        currentState = DevilState.Orbiting;
        UpdateAnimatorState(isRunning: true, isCharging: false, isChilling: false);

        // Réactiver le dash après le cooldown
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        Debug.Log($"[Devil:{name}] ⚡ Dash à nouveau disponible.");
    }

    private void UpdateAnimatorState(bool isRunning, bool isCharging, bool isChilling)
    {
        if (animator == null) return;

        animator.SetBool(ParamIsRunning, isRunning);
        animator.SetBool(ParamIsCharging, isCharging);
        animator.SetBool(ParamIsChilling, isChilling);
    }

    // Gizmos pour visualiser les zones
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || target == null) return;

        // Cercle extérieur (zone d'orbite)
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(target.position, outerCircleRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position, outerCircleRadius);

        // Cercle intérieur (zone de passage)
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawSphere(target.position, innerCircleRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, innerCircleRadius);

        // Rayon de hit pendant le dash
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, dashHitRadius);

        // Position d'orbite
        if (currentState == DevilState.Orbiting)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(orbitPosition, 0.5f);
            Gizmos.DrawLine(transform.position, orbitPosition);
        }

        // Ligne du dash (si en charge)
        if (currentState == DevilState.Charging)
        {
            Gizmos.color = Color.magenta;
            // Trajectoire en V :  descente puis remontée
            Gizmos.DrawLine(dashStartPosition, dashMidPosition);
            Gizmos.DrawLine(dashMidPosition, dashEndPosition);

            // Point sur le joueur
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(dashMidPosition, 0.5f);
        }
    }
}
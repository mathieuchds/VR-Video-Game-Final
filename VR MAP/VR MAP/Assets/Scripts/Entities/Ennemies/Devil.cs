using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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
        base.Start();

        agent = GetComponent<NavMeshAgent>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (agent != null)
        {
            agent.speed = normalSpeed;
            agent.updatePosition = false; 
            agent.updateRotation = false;

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

        if (currentState != DevilState.Charging && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }

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
        if (distanceToPlayer > outerCircleRadius + 2f)
        {
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
            currentState = DevilState.Orbiting;
            Debug.Log($"[Devil:{name}] 🔄 Passage en mode Orbiting");
        }
    }

    private void HandleOrbiting(float distanceToPlayer)
    {
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

        if (canDash)
        {
            StartCoroutine(PrepareDash());
        }
    }

    private IEnumerator PrepareDash()
    {
        canDash = false;

        yield return new WaitForSeconds(dashPreparationTime);

        StartDash();
    }

    private void HandleCharging()
    {
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

        currentState = DevilState.Charging;
        hasHitPlayerThisDash = false;
        dashProgress = 0f;

        if (agent != null && agent.enabled)
            agent.isStopped = true;

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        foreach (Transform child in transform)
        {
            child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        dashStartPosition = transform.position;

        dashMidPosition = target.position + Vector3.up * dashHeightOffset;

        Vector3 horizontalDirection = new Vector3(
            target.position.x - dashStartPosition.x,
            0f,
            target.position.z - dashStartPosition.z
        ).normalized;

        dashDirection = horizontalDirection;

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

            Vector3 newPosition;

            if (dashProgress < 0.5f)
            {
                float phase1Progress = dashProgress * 2f; // 0 → 1
                newPosition = Vector3.Lerp(dashStartPosition, dashMidPosition, phase1Progress);
            }
            else
            {
                float phase2Progress = (dashProgress - 0.5f) * 2f; // 0 → 1
                newPosition = Vector3.Lerp(dashMidPosition, dashEndPosition, phase2Progress);
            }

            transform.position = newPosition;

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

        transform.position = dashEndPosition;

        StartChill();
    }

    private void HitPlayer()
    {
        hasHitPlayerThisDash = true;

        if (animator != null)
            animator.SetTrigger(TriggerHit);

        PlayerStats ps = target.GetComponent<PlayerStats>();
        if (ps != null)
        {
            ps.TakeDamage(dashDamage);
        }
    }

    private void StartChill()
    {
        currentState = DevilState.Chilling;

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
        yield return new WaitForSeconds(chillDuration);

        currentState = DevilState.Orbiting;
        UpdateAnimatorState(isRunning: true, isCharging: false, isChilling: false);

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
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
            Gizmos.DrawLine(dashStartPosition, dashMidPosition);
            Gizmos.DrawLine(dashMidPosition, dashEndPosition);

            // Point sur le joueur
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(dashMidPosition, 0.5f);
        }
    }
}
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ennemi "Sorcier" : se déplace vers le joueur mais s'arrête à une distance minimale.
/// Tire des boules de feu en direction du joueur quand il est à portée (<= stopDistance).
/// Le spawn du projectile DOIT être effectué depuis l'Animation Event qui appelle ShootAtTarget().
/// </summary>
[RequireComponent(typeof(NavMeshAgent))
]
public class Wizard : Enemy
{
    [Header("Références")]
    [SerializeField] private GameObject targetObject;
    private Transform target;

    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Animator animator;

    [Header("Distances")]
    [SerializeField] private float stopDistance = 12f; // distance à garder (tir quand <=)

    [Header("Attaque")]
    [SerializeField] private float attackCooldown = 2f;
    [Tooltip("Cooldown entre attaques (le spawn est déclenché par l'Animation Event).")]
    [SerializeField] private float attackDelayAfterTrigger = 0.15f;

    [Header("Projectile par défaut")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileDamage = 15f;
    [SerializeField] private float projectileLifetime = 6f;

    private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int ParamShoot = Animator.StringToHash("Shoot"); // trigger

    private Coroutine attackCoroutine;
    private bool isAttacking = false;

    // logs / petite marge anti-oscillation
    private const float epsilon = 0.05f;
    [Header("Debug")]
    [SerializeField] private float debugLogInterval = 2f;
    private float debugLogTimer = 0f;

    void Start()
    {
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (agent != null)
        {
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (rb != null)
                rb.isKinematic = true;
        }
        health = maxHealth;

        if (healthBar != null)
            healthBar.SetHealth(1f);

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
            Debug.LogWarning($"[Wizard:{name}] Aucun target trouvé (tag Player ou targetObject).");
        else
            Debug.Log($"[Wizard:{name}] Target trouvé : {target.name}");

        Debug.Log($"[Wizard:{name}] stopDistance={stopDistance}, attackCooldown={attackCooldown}");

        if (firePoint == null)
            firePoint = transform;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        debugLogTimer = debugLogInterval;
    }

    void Update()
    {

        if (target == null)
        {
            Debug.LogWarning($"[Wizard:{name}] ❌ Target null, pas de mouvement");
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        bool shouldRun = dist > (stopDistance + epsilon);

        // ✅ Debug détaillé
        if (Time.frameCount % 60 == 0) // Log toutes les 60 frames
        {
            Debug.Log($"[Wizard:{name}] dist={dist:F2}, shouldRun={shouldRun}, isStunned={isStunned}, agent. enabled={agent?.enabled}, isOnNavMesh={agent?.isOnNavMesh}");
        }

        if (!isStunned)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (shouldRun)
                {
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }
                else
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
            }
            else
            {
                Debug.LogWarning($"[Wizard:{name}] ❌ Agent problème:  enabled={agent?.enabled}, onNavMesh={agent?.isOnNavMesh}");
            }

            // Mettre à jour l'Animator
            if (animator != null)
                animator.SetBool(ParamIsRunning, shouldRun);

            // Attaque
            bool canAttack = dist <= (stopDistance + epsilon);

            if (canAttack && !isAttacking)
            {
                Debug.Log($"[Wizard:{name}] Entrée zone d'attaque (dist={dist:F2})");
                attackCoroutine = StartCoroutine(AttackLoop());
            }
            else if (!canAttack && isAttacking)
            {
                Debug.Log($"[Wizard:{name}] Sortie zone d'attaque (dist={dist:F2})");
                if (attackCoroutine != null)
                    StopCoroutine(attackCoroutine);
                attackCoroutine = null;
                isAttacking = false;
            }

            // Rotation
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
        }
    }

    private System.Collections.IEnumerator AttackLoop()
    {
        isAttacking = true;
        while (true)
        {
            if (isStunned)
            {
                yield return null;
            }else if (animator != null)
            {
                Debug.Log($"[Wizard:{name}] Déclenche trigger Shoot sur Animator.");
                animator.SetTrigger(ParamShoot);

                // Ne pas spawner depuis le script : le projectile DOIT être instancié par l'Animation Event
                // On attend un petit délai pour laisser l'animation démarrer (optionnel)
                yield return new WaitForSeconds(attackDelayAfterTrigger);
            }else{
                // Si pas d'Animator (fallback) on déclenche directement le spawn
                ShootAtTarget();
            }

            // cooldown entre attaques
            yield return new WaitForSeconds(attackCooldown);
        }
    }

    public void ShootAtTarget()
    {
        Debug.Log($"[Wizard:{name}] ShootAtTarget() appelé (via Animation Event).");
        if (target == null || fireballPrefab == null)
        {
            Debug.LogWarning($"[Wizard:{name}] Impossible de tirer :  target={(target == null)}, fireballPrefab={(fireballPrefab == null)}");
            return;
        }

        // Position de spawn
        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position + transform.forward * 1f + Vector3.up * 1.5f;

        // Point visé
        Vector3 aimPoint = target.position + Vector3.up * 1f;

        // Direction vers la cible
        Vector3 dir = (aimPoint - spawnPos).normalized;

        // ✅ Instancier le projectile
        GameObject b = Instantiate(fireballPrefab, spawnPos, Quaternion.LookRotation(dir));

        // ✅ APPELER SetDirection pour que le projectile bouge ! 
        FireBall fb = b.GetComponent<FireBall>();
        if (fb != null)
        {
            fb.SetDirection(dir);
            fb.damage = projectileDamage;
            fb.speed = projectileSpeed;
            Debug.Log($"[Wizard:{name}] ✅ FireBall configuré:  dir={dir}, speed={projectileSpeed}");
        }
        else
        {
            Debug.LogError($"[Wizard:{name}] ❌ Pas de script FireBall sur le prefab !");
        }

        // ✅ Configurer le Rigidbody (kinematic, PAS de velocity)
        Rigidbody rb = b.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            // ⚠️ NE PAS utiliser linearVelocity sur un kinematic !
            // Le mouvement est géré par FireBall. Update() avec transform.position
        }

        // Détruire après projectileLifetime secondes
        Destroy(b, projectileLifetime);

        Debug.Log($"[Wizard:{name}] Projectile tiré vers {target.name}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, stopDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
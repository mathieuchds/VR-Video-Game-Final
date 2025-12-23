using UnityEngine;
using UnityEngine.AI;

public class Bear : Enemy
{
    [Header("Bear Settings")]
    public float moveSpeed = 3.5f;
    public float attackRange = 2f;
    public float attackCooldown = 2f;
    [Tooltip("Distance maximale pour infliger des dégâts lors de l'attaque")]
    [SerializeField] private float damageRange = 5f; 
    [Tooltip("Délai après le déclenchement de l'attaque avant d'appliquer les dégâts (pour sync avec l'animation)")]
    [SerializeField] private float damageDelay = 0.5f;

    [Header("Animation")]
    public Animator animator;

    [Header("NavMesh Safety")]
    [Tooltip("Temps max hors NavMesh avant destruction automatique")]
    [SerializeField] private float maxTimeOffNavMesh = 2f;

    private Transform player;
    private float lastAttackTime = -999f; // Permet d'attaquer dès le début
    private bool isInAttackRange = false;
    
    private float timeOffNavMesh = 0f;
    private bool wasOnNavMesh = true;

    void Start()
    {
        base.Start();
        Debug.Log("[Bear] Start() appelé");

        agent = GetComponent<NavMeshAgent>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Si l'ennemi est piloté par NavMeshAgent, on préfère que le Rigidbody soit kinematic
        // (évite que l'agent "passe à travers" des obstacles gérés par la NavMesh).
        if (agent != null)
        {
            Debug.Log("[Bear] NavMeshAgent trouvé et configuré");
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (rb != null)
                rb.isKinematic = true;
        }
        else
        {
            Debug.LogError("[Bear] NavMeshAgent NON trouvé sur " + gameObject.name);
        }

        health = maxHealth;
        healthBar.SetHealth(1f);

        if (agent != null)
        {
            agent.speed = moveSpeed;
            Debug.Log("[Bear] Vitesse de l'agent définie à " + moveSpeed);
        }

        // Trouver le joueur
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log("[Bear] Joueur trouvé : " + playerObj.name);
        }
        else
        {
            Debug.LogError("[Bear] JOUEUR NON TROUVÉ avec le tag 'Player'");
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            Debug.Log("[Bear] Animator trouvé et configuré");
        }
        else
        {
            Debug.LogError("[Bear] ANIMATOR NON TROUVÉ sur " + gameObject.name);
        }
    }

    void Update()
    {
        if (player == null)
        {
            Debug.LogWarning("[Bear] Player est null dans Update()");
            return;
        }

        if (agent == null)
        {
            Debug.LogWarning("[Bear] Agent est null dans Update()");
            return;
        }

        if (animator == null)
        {
            Debug.LogWarning("[Bear] Animator est null dans Update()");
            return;
        }

        if (!IsAgentOnNavMesh())
        {
            // Agent hors NavMesh
            if (wasOnNavMesh)
            {
                // Vient juste de sortir de la NavMesh
                Debug.LogWarning($"[Bear] {gameObject.name} a quitté la NavMesh !");
                wasOnNavMesh = false;
                timeOffNavMesh = 0f;
            }

            timeOffNavMesh += Time.deltaTime;

            if (timeOffNavMesh >= maxTimeOffNavMesh)
            {
                // Trop longtemps hors NavMesh, supprimer l'ennemi
                Debug.LogError($"[Bear] {gameObject.name} hors NavMesh depuis {timeOffNavMesh:F2}s - Suppression automatique");
                KillAndCountForPlayer();
                return;
            }

            Debug.LogWarning($"[Bear] Hors NavMesh depuis {timeOffNavMesh:F2}s / {maxTimeOffNavMesh}s");
            return; // Ne pas essayer de bouger si hors NavMesh
        }
        else
        {
            // Agent sur la NavMesh
            if (!wasOnNavMesh)
            {
                // Vient de revenir sur la NavMesh
                Debug.Log($"[Bear] {gameObject.name} est revenu sur la NavMesh !");
                wasOnNavMesh = true;
                timeOffNavMesh = 0f;
            }
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Si l'ours est assez proche pour attaquer
        if (distanceToPlayer <= attackRange)
        {
            // Arrêter le mouvement et passer en mode attaque
            if (!isInAttackRange)
            {
                isInAttackRange = true;
                
                if (agent.isOnNavMesh && agent.enabled)
                {
                    agent.ResetPath();
                }
                
                animator.SetBool("IsWalking", false);
                Debug.Log("[Bear] Entrée en zone d'attaque - IsWalking = false");
            }

            // Déclencher le trigger Attack toutes les attackCooldown secondes
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Debug.Log("[Bear] 🔥 TRIGGER ATTACK déclenché !");
                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
                
                StartCoroutine(DealDamageAfterDelay());
            }
        }
        else
        {
            // Sortie de la zone d'attaque, recommencer à courir
            if (isInAttackRange)
            {
                isInAttackRange = false;
                Debug.Log("[Bear] Sortie de la zone d'attaque - IsWalking = true");
            }

            if (agent.isOnNavMesh && agent.enabled)
            {
                agent.SetDestination(player.position);
                animator.SetBool("IsWalking", true);
            }
            else
            {
                Debug.LogWarning("[Bear] Agent non prêt (isOnNavMesh=" + agent.isOnNavMesh + ", enabled=" + agent.enabled + ")");
            }
        }
    }


    private System.Collections.IEnumerator DealDamageAfterDelay()
    {
        // Attendre que l'animation arrive au moment du coup
        yield return new WaitForSeconds(damageDelay);
        
        // Appliquer les dégâts
        DealDamageToPlayer();
    }


    private bool IsAgentOnNavMesh()
    {
        if (agent == null || !agent.enabled)
            return false;

        if (!agent.isOnNavMesh)
            return false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 1.0f, NavMesh.AllAreas))
        {
            // Il y a une NavMesh dans un rayon de 1m
            return true;
        }

        return false;
    }


    private void KillAndCountForPlayer()
    {
        // Ajouter au compteur de quête
        QuestManager questManager = QuestManager.Instance;
        if (questManager != null)
        {
            questManager.AddProgress(QuestObjectiveType.KillEnemy, 1);
        }

        // Ajouter du score
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            LevelData levelData = FindObjectOfType<LevelData>();
            int currentLevel = levelData != null ? levelData.level : 0;
            float scoreToAdd = 10f + currentLevel;
            
            playerStats.AddScore(scoreToAdd);
        }

        // Notifier le spawner si présent
        if (spawner != null)
        {
            spawner.EnemyDied();
        }

        // Détruire l'ennemi
        Destroy(gameObject);
    }

    public void DealDamageToPlayer()
    {
        Debug.Log("[Bear] 💥 DealDamageToPlayer() appelé");

        if (player == null)
        {
            Debug.LogWarning("[Bear] Player est null, impossible d'infliger des dégâts");
            return;
        }


        Vector3 bearPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 playerPosXZ = new Vector3(player.position.x, 0, player.position.z);
        float distanceXZ = Vector3.Distance(bearPosXZ, playerPosXZ);
        
        Debug.Log($"[Bear] Distance XZ au joueur: {distanceXZ:F2} / Damage Range: {damageRange:F2}");

        if (distanceXZ <= damageRange)
        {
            PlayerStats ps = player.GetComponent<PlayerStats>();
            
            if (ps == null)
                ps = player.GetComponentInParent<PlayerStats>();
            
            if (ps == null)
                ps = player.GetComponentInChildren<PlayerStats>();
            
            if (ps == null)
            {
                GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null)
                    ps = playerGO.GetComponent<PlayerStats>();
            }

            if (ps != null)
            {
                ps.ApplyRawDamage(contactDamage);
            }
           
        }
        
    }

    public new void Knockback(Vector3 direction, float force, float duration)
    {
        StartCoroutine(SafeKnockbackRoutine(direction, force, duration));
    }


    private System.Collections.IEnumerator SafeKnockbackRoutine(Vector3 dir, float force, float duration)
    {
        dir.y = 0f;
        dir.Normalize();

        if (rb == null)
            yield break;

        bool hadAgent = agent != null && agent.enabled;
        if (hadAgent) agent.enabled = false;

        rb.isKinematic = false;
        rb.AddForce(dir * force, ForceMode.Impulse);

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        if (hadAgent)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
            {
                // Replacer sur la NavMesh la plus proche
                transform.position = hit.position;
                Debug.Log($"[Bear] Replacé sur NavMesh à {hit.position}");
            }
            else
            {
                Debug.LogWarning($"[Bear] Impossible de trouver une NavMesh proche après knockback");
            }

            agent.enabled = true;
        }
    }
}
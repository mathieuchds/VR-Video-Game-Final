using UnityEngine;

public class Bear : Enemy
{
    [Header("Bear Settings")]
    public float moveSpeed = 3.5f;
    public float attackRange = 2f;
    public float attackCooldown = 2f;

    [Header("Animation")]
    public Animator animator;

    private Transform player;
    private float lastAttackTime = -999f; // Permet d'attaquer dès le début
    private bool isInAttackRange = false;

    void Start()
    {
        base.Start();
        Debug.Log("[Bear] Start() appelé");

        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
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
            Debug.Log("[Bear] Joueur trouvé :   " + playerObj.name);
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

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Si l'ours est assez proche pour attaquer
        if (distanceToPlayer <= attackRange)
        {
            Debug.Log("[Bear] Dans la portée d'attaque !  Distance:  " + distanceToPlayer + " / Range:  " + attackRange);

            // Arrêter le mouvement et passer en mode attaque
            if (!isInAttackRange)
            {
                isInAttackRange = true;
                agent.ResetPath();
                animator.SetBool("IsWalking", false);
                Debug.Log("[Bear] Entrée en zone d'attaque - IsWalking = false");
            }

            // Déclencher le trigger Attack toutes les attackCooldown secondes
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Debug.Log("[Bear] 🔥 TRIGGER ATTACK déclenché !");
                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
            }
            else
            {
                float cooldownRemaining = (lastAttackTime + attackCooldown) - Time.time;
                Debug.Log("[Bear] Cooldown en cours : " + cooldownRemaining.ToString("F2") + "s restantes");
            }
        }
        else
        {
            Debug.Log("[Bear] Trop loin pour attaquer. Distance: " + distanceToPlayer + " / Range:  " + attackRange);

            // Sortie de la zone d'attaque - recommencer à courir
            if (isInAttackRange)
            {
                isInAttackRange = false;
                Debug.Log("[Bear] Sortie de la zone d'attaque - IsWalking = true");
            }

            // Courir vers le joueur
            agent.SetDestination(player.position);
            animator.SetBool("IsWalking", true);
            Debug.Log("[Bear] Course vers le joueur activée");
        }
    }

    // Cette méthode peut être appelée par un Animation Event dans l'animation d'attaque
    public void DealDamageToPlayer()
    {
        Debug.Log("[Bear] 💥 DealDamageToPlayer() appelé via Animation Event");

        if (player == null)
        {
            Debug.LogWarning("[Bear] Player est null, impossible d'infliger des dégâts");
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        Debug.Log("[Bear] Distance au joueur lors de l'attaque:  " + distanceToPlayer);

        if (distanceToPlayer <= attackRange)
        {
            PlayerStats ps = player.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(contactDamage);
                Debug.Log("[Bear] ✅ Dégâts infligés au joueur : " + contactDamage);
            }
            else
            {
                Debug.LogError("[Bear] PlayerStats non trouvé sur le joueur");
            }
        }
        else
        {
            Debug.LogWarning("[Bear] Joueur hors de portée lors de l'attaque !");
        }
    }
}
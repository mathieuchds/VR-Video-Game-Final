using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] public EnemyHealthBar healthBar;
    public float maxHealth = 100f;
    public float health;
    public float contactDamage = 10f;
    public float speed = 3f;

    public float flashTime = 0.1f;
    public bool isStunned = false;
    public bool isSlowed = false; 

    private Renderer rend;
    private Color baseColor;
    private float flashTimer = 0f;
    public Rigidbody rb;

    public EnemySpawner spawner;

    protected UnityEngine.AI.NavMeshAgent agent;
    protected float flameDamagePerSecond = 1f;
    protected float flameDuration = 3f;

    protected float poisonDamagePerSecond = 1f;
    protected float poisonDuration = 3f;

    [Header("VFX")]
    [SerializeField] protected GameObject flameEffectPrefab;
    [Tooltip("Multiplicateur de taille pour l'effet de flamme")]
    [SerializeField] protected float flameScale = 2f;

    [SerializeField] protected GameObject poisonEffectPrefab;
    [Tooltip("Multiplicateur de taille pour l'effet de poison")]
    [SerializeField] protected float poisonScale = 1.5f;

    [SerializeField] protected GameObject slowEffectPrefab; 
    [Tooltip("Multiplicateur de taille pour l'effet de slow")]
    [SerializeField] protected float slowScale = 1.5f; 

    protected GameObject currentFlame;
    protected GameObject currentPoison;
    protected GameObject currentSlow; 

    protected void Start()
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
        healthBar.SetHealth(1f);

        PlayerStats ps = GameObject.FindObjectOfType<PlayerStats>(true);
        flameDamagePerSecond = ps.flameDamagePerSecond;
        flameDuration = ps.flameDuration;

        poisonDamagePerSecond = ps.poisonDamage;
        poisonDuration = ps.poisonDuration;

        rend = GetComponent<Renderer>();
        if (rend != null) baseColor = rend.material.color;

        if (GetComponent<EnemyOutOfBoundsChecker>() == null)
        {
            gameObject.AddComponent<EnemyOutOfBoundsChecker>();
        }
    }

    void Update()
    {
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0 && rend != null)
                rend.material.color = baseColor;
        }
    }

    public void ApplyBurn()
    {
        if (currentFlame != null)
        {
            return;
        }

        currentFlame = Instantiate(
            flameEffectPrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        if (currentFlame != null)
        {
            currentFlame.transform.localScale = Vector3.one * flameScale;
        }

        StartCoroutine(Burn());
    }

    protected IEnumerator Burn()
    {
        float elapsed = 0f;

        while (elapsed < flameDuration)
        {
            TakeDamage(flameDamagePerSecond * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentFlame != null)
        {
            Destroy(currentFlame);
            currentFlame = null;
        }
    }

    public void ApplyPoison()
    {
        if (currentPoison != null)
        {
            return;
        }

        if (poisonEffectPrefab != null)
        {
            currentPoison = Instantiate(
                poisonEffectPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );

            if (currentPoison != null)
            {
                currentPoison.transform.localScale = Vector3.one * poisonScale;
            }
        }


        StartCoroutine(Poison());
    }

    protected IEnumerator Poison()
    {
        float elapsed = 0f;

        while (elapsed < poisonDuration)
        {
            TakeDamage(poisonDamagePerSecond * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentPoison != null)
        {
            Destroy(currentPoison);
            currentPoison = null;
        }
    }

    public void ApplySlow(float slowFactor, float duration)
    {
        if (isSlowed)
        {
            Debug.Log("[Enemy] Déjà ralenti");
            return;
        }

        if (slowEffectPrefab != null)
        {
            currentSlow = Instantiate(
                slowEffectPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );

            if (currentSlow != null)
            {
                currentSlow.transform.localScale = Vector3.one * slowScale;
                Debug.Log($"[Enemy] Effet de slow appliqué (scale: {slowScale}, factor: {slowFactor}, durée: {duration}s)");
            }
        }
        else
        {
            Debug.LogWarning("[Enemy] Aucun prefab d'effet de slow assigné !");
        }

        StartCoroutine(SlowRoutine(slowFactor, duration));
    }


    protected IEnumerator SlowRoutine(float slowFactor, float duration)
    {
        isSlowed = true;
        float originalSpeed = speed;

        speed= originalSpeed * slowFactor;
        if (agent != null)
        {
            agent.speed = originalSpeed * slowFactor;
            Debug.Log($"[Enemy] Vitesse réduite de {originalSpeed} à {agent.speed}");
        }

        yield return new WaitForSeconds(duration);
        speed = originalSpeed;
        if (agent != null && !isStunned)
        {
            agent.speed = originalSpeed;
            Debug.Log($"[Enemy] Vitesse restaurée à {originalSpeed}");
        }

        isSlowed = false;

        if (currentSlow != null)
        {
            Destroy(currentSlow);
            currentSlow = null;
        }
    }

    public void Knockback(Vector3 direction, float force, float duration)
    {
        StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    protected IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration)
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

        if (hadAgent) agent.enabled = true;
    }

    public void Stun(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(StunRoutine(speed, duration));
    }

    protected IEnumerator StunRoutine(float baseSpeed, float duration)
    {
        isStunned = true;
        speed = 0f;
        if (agent != null)
            agent.speed = 0f;

        yield return new WaitForSeconds(duration);
        speed= baseSpeed;
        if (agent != null)
        {
            if (!isSlowed) {
                agent.speed = baseSpeed;
            }
        }
        
        isStunned = false;
    }

    public virtual void TakeDamage(float dmg)
    {
        health -= dmg;

        // flash
        if (rend != null) rend.material.color = Color.black;
        flashTimer = flashTime;

        if (healthBar != null) healthBar.SetHealth(health / maxHealth);

        if (health <= 0f)
        {
            if (spawner != null) spawner.EnemyDied();
            QuestManager.Instance?.AddProgress(QuestObjectiveType.KillEnemy, 1);
            Die();
        }
    }

    protected virtual void Die()
    {
        PlayerStats ps = GameObject.FindObjectOfType<PlayerStats>(true);
        LevelData levelData = FindObjectOfType<LevelData>(true);
        int currentLevel;

        if (levelData == null)
        {
            currentLevel = 0;
        }
        else
        {
            currentLevel = levelData.level;
        }

        ps.AddScore(10f + currentLevel);

        if (currentFlame != null)
            Destroy(currentFlame);
        if (currentPoison != null)
            Destroy(currentPoison);
        if (currentSlow != null) 
            Destroy(currentSlow);

        if (CompareTag("Boss"))
        {

            GameStateManager gsm = FindObjectOfType<GameStateManager>(true);
            if (gsm != null)
                gsm.TriggerGameOver(true);
        }

        Destroy(gameObject);
    }
}


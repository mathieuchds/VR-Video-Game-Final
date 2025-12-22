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

    [SerializeField] protected GameObject flameEffectPrefab;
    protected GameObject currentFlame;


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
            Debug.Log(" Déjà en feu ");
            return;
        }
            

        currentFlame = Instantiate(
            flameEffectPrefab,
            transform.position,
            Quaternion.identity,
            transform 
        );

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

        Destroy(currentFlame);
        currentFlame = null;
    }


    public void ApplyPoison(){ 
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
        StartCoroutine(StunRoutine(speed, duration));
    }

    protected IEnumerator StunRoutine(float baseSpeed, float duration)
    {
        isStunned = true;

        if (agent != null)
            agent.speed = 0f;

        yield return new WaitForSeconds(duration);

        if (agent != null)
            agent.speed = baseSpeed;
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

        if (levelData == null) { 
            currentLevel = 0; 
        }else{
            currentLevel = levelData.level;
        }

        ps.AddScore(10f + currentLevel);
        // Si c'est le boss victoire
        if (CompareTag("Boss"))
        {
            Debug.Log(" BOSS MORT = FIN DU JEU ");

            GameStateManager gsm = FindObjectOfType<GameStateManager>(true);
            if (gsm != null)
                gsm.TriggerGameOver(true);
        }

        Destroy(gameObject);
    }

}


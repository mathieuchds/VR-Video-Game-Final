using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public class PlayerStats : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] public float maxHealth = 100f;
    [SerializeField] public float currentHealth = 100f;

    [SerializeField] public int attackDamage = 50;
    [SerializeField] public float attackSpeed = 1.5f;

    [SerializeField] public int defense = 5;
    [SerializeField] public float moveSpeed = 1f;

    //[SerializeField] public int mana = 50;
    //[SerializeField] public int maxMana = 50;


    [Header("Power Up")]

    [Header("Shockwave")]
    [SerializeField] public float shockwaveRadius = 5f;
    [SerializeField] public float shockwaveDamage = 20f;

    [Header("Stun")]
    [SerializeField] public float stunDuration = 2f;


    [Header("SpeedBoost")]
    [SerializeField] public float speedBoostMultiplier = 3f;
    [SerializeField] public float speedBoostDuration = 3f;

    [Header("Bomba")]
    [SerializeField] public float explosionRadius = 3f;
    [SerializeField] public float explosionDamage = 20f;

    [Header("FlameThrower")]
    [SerializeField] public float flameDamagePerSecond = 4f;
    [SerializeField] public float flameDuration = 2f;

    [Header("PoisonBullets")]
    [SerializeField] public float poisonDamage = 10f;
    [SerializeField] public float poisonDuration = 3f;

    [Header("IceRay")]
    [SerializeField] public float iceDuration = 2f;

    [Header("Score")]
    [SerializeField] private float score = 0f;


    public event Action HealthUpdate;

    private GameStateManager gameStateManager;
    private bool isDead = false; 

    // --- Slow state (non stacking) ---
    private bool isSlowed = false;
    private Coroutine slowCoroutine = null;
    private float originalMoveSpeed = -1f;

    private void Awake()
    {
        gameStateManager = FindObjectOfType<GameStateManager>();
        
        if (gameStateManager == null)
        {
            Debug.LogError("[PlayerStats] GameStateManager introuvable ! Le Game Over ne pourra pas se déclencher.");
        }

        // store original base move speed for safe restore
        originalMoveSpeed = moveSpeed;

        ResetStats();
    }

    // ✅ NOUVEAU : Méthode pour réinitialiser les stats
    public void ResetStats()
    {
        currentHealth = maxHealth;
        isDead = false;
        HealthUpdate?.Invoke();

        // restore base move speed and cancel slow if any
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }
        isSlowed = false;
        moveSpeed = originalMoveSpeed;

        Debug.Log($"[PlayerStats] ✅ Stats réinitialisées : {currentHealth}/{maxHealth} HP");
    }

    public void TakeDamage(float amount)
    {
        // ✅ Ne pas prendre de dégâts si déjà mort
        if (isDead)
            return;

        // calcule damage effectif en tenant compte de la défense
        float finalDamage = Mathf.Max(amount - defense, 0f);

        // debug log pour vérifier les appels (utile pour ton problème)
        Debug.Log($"[PlayerStats] TakeDamage called: amount={amount:F2}, defense={defense}, finalDamage={finalDamage:F2}, healthBefore={currentHealth:F2}");

        currentHealth -= finalDamage;
        
        currentHealth = Mathf.Max(currentHealth, 0f);
        
        HealthUpdate?.Invoke();

        Debug.Log($"[PlayerStats] Health after damage: {currentHealth:F2}/{maxHealth:F2}");

        if (currentHealth <= 0f && !isDead)
        {
            OnPlayerDeath();
        }
    }

    // NOUVEAU : Appliquer des dégâts "bruts" sans soustraire la défense
    public void ApplyRawDamage(float amount)
    {
        if (isDead) return;

        Debug.Log($"[PlayerStats] ApplyRawDamage called: amount={amount:F2}, healthBefore={currentHealth:F2}");

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        HealthUpdate?.Invoke();

        Debug.Log($"[PlayerStats] Health after raw damage: {currentHealth:F2}/{maxHealth:F2}");

        if (currentHealth <= 0f && !isDead)
        {
            OnPlayerDeath();
        }
    }

    public void Heal(float amount)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        HealthUpdate?.Invoke();
    }

    private void OnPlayerDeath()
    {
        isDead = true; 
        
        Debug.Log("[PlayerStats] 💀 JOUEUR MORT ! Déclenchement du Game Over...");

        if (gameStateManager != null)
        {
            gameStateManager.TriggerGameOver(false);
        }
        else
        {
            Debug.LogError("[PlayerStats] Impossible de déclencher le Game Over : GameStateManager introuvable !");
        }
    }

    // Public API: apply a slow that does NOT stack. If already slowed, the call is ignored.
    public void ApplySlow(float factor, float duration)
    {
        if (isSlowed)
            return;

        // ensure we have a sensible base value to restore later
        if (originalMoveSpeed <= 0f)
            originalMoveSpeed = moveSpeed;

        // apply slow immediately
        moveSpeed = originalMoveSpeed * factor;
        isSlowed = true;

        // start restore coroutine on this component (safe because this GameObject is the player)
        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);
        slowCoroutine = StartCoroutine(RestoreSlowRoutine(originalMoveSpeed, duration));
    }

    private IEnumerator RestoreSlowRoutine(float baseSpeed, float duration)
    {
        yield return new WaitForSeconds(duration);

        // restore only if not dead (but restore even if dead is harmless)
        moveSpeed = baseSpeed;
        isSlowed = false;
        slowCoroutine = null;
    }

    public void AddScore(float amount)
    {
        score += amount;
    }

    public float GetScore()
    {
        return score;
    }
}

using UnityEngine;

public class IceLaser : MonoBehaviour
{
    [Header("Slow Settings")]
    [Tooltip("Facteur de réduction de vitesse (0.3 = 30% de vitesse, soit 70% de réduction)")]
    [SerializeField] private float slowFactor = 0.1f;
    [Tooltip("Durée du slow (sera overridée par PlayerStats.iceDuration)")]
    [SerializeField] private float slowDuration = 2f;

    private PlayerStats playerStats;

    private void Start()
    {
        // Récupérer les stats du joueur pour utiliser iceDuration
        playerStats = FindObjectOfType<PlayerStats>();

        if (playerStats != null)
        {
            slowDuration = playerStats.iceDuration;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            // ✅ MODIFIÉ : Appliquer le slow au lieu du stun
            // Utiliser la durée du PlayerStats si disponible
            float duration = playerStats != null ? playerStats.iceDuration : slowDuration;
            enemy.ApplySlow(slowFactor, duration);
        }
    }
}

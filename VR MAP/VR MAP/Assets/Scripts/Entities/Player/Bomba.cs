using UnityEngine;

public class Bomba : MonoBehaviour
{
    public PlayerStats stats;
    public float explosionRadius = 3f;
    public float explosionDamage = 20f;

    [Header("VFX")]
    [Tooltip("Prefab de l'effet de particules d'explosion")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [Tooltip("Durée avant destruction automatique du VFX (si pas de Particle System)")]
    [SerializeField] private float vfxLifetime = 2f;
    [Tooltip("Taille de base du VFX prefab (pour calculer le scale)")]
    [SerializeField] private float baseVFXRadius = 3f;

    void Start()
    {
        stats = GameObject.FindObjectOfType<PlayerStats>(true);
        explosionDamage = stats.explosionDamage;
        explosionRadius = stats.explosionRadius;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player"))
        {
            Explode();
        }
    }

    void Explode()
    {
        // ✅ Spawn l'effet de particules AVANT de détruire la bombe
        if (explosionVFXPrefab != null)
        {
            // Instancier le VFX à la position de la bombe
            GameObject vfxInstance = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);

            // ✅ NOUVEAU : Ajuster la taille du VFX en fonction du explosionRadius
            float scaleFactor = explosionRadius / baseVFXRadius;
            vfxInstance.transform.localScale = Vector3.one * scaleFactor;

            // ✅ NOUVEAU : Ajuster aussi le Shape Radius du Particle System si présent
            ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                // Ajuster le shape radius pour correspondre à explosionRadius
                var shape = ps.shape;
                shape.radius = explosionRadius;

                // Ajuster la vitesse des particules proportionnellement
                var main = ps.main;
                main.startSpeedMultiplier *= scaleFactor;

                // Détruire automatiquement après la durée du Particle System
                float duration = main.duration + main.startLifetime.constantMax;
                Destroy(vfxInstance, duration);

                Debug.Log($"[Bomba] 💥 Explosion VFX spawné à {transform.position} avec radius {explosionRadius}");
            }
            else
            {
                // Fallback : détruire après vfxLifetime secondes
                Destroy(vfxInstance, vfxLifetime);
                Debug.Log($"[Bomba] 💥 Explosion VFX spawné (sans Particle System) avec scale {scaleFactor}");
            }
        }
        else
        {
            Debug.LogWarning("[Bomba] ⚠️ Aucun prefab VFX assigné pour l'explosion !");
        }

        // Appliquer les dégâts et knockback aux ennemis
        Collider[] enemies = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in enemies)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            enemy.TakeDamage(explosionDamage);

            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            enemy.Knockback(dir, 10f, 1f);
        }

        // Détruit la bombe
        Destroy(gameObject);
    }
}

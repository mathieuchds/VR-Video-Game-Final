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
        if (explosionVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);

            float scaleFactor = explosionRadius / baseVFXRadius;
            vfxInstance.transform.localScale = Vector3.one * scaleFactor;

            ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var shape = ps.shape;
                shape.radius = explosionRadius;

                var main = ps.main;
                main.startSpeedMultiplier *= scaleFactor;

                float duration = main.duration + main.startLifetime.constantMax;
                Destroy(vfxInstance, duration);

            }
            else
            {
                Destroy(vfxInstance, vfxLifetime);
            }
        }
       

        Collider[] enemies = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in enemies)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            enemy.TakeDamage(explosionDamage);

            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            enemy.Knockback(dir, 10f, 1f);
        }

        Destroy(gameObject);
    }
}

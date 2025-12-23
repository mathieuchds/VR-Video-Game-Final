using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float damage = 10f;   // dégâts infligés
    public float lifeTime = 3f;  // durée avant auto-destruction

    public bool isPoisonous = false;

    [Header("VFX")]
    [Tooltip("Prefab de l'effet de particules lors de l'impact")]
    [SerializeField] private GameObject impactVFXPrefab;
    [Tooltip("Multiplicateur de taille des particules (1 = taille normale)")]
    [SerializeField] private float particleScale = 1f;
    [Tooltip("Durée avant destruction automatique du VFX")]
    [SerializeField] private float vfxLifetime = 2f;

    private bool hasExploded = false; 

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded)
            return;

        if (other.CompareTag("Player"))
            return;


        if (other.isTrigger && other.GetComponent<Enemy>() == null)
        {
            return;
        }

        if (other.CompareTag("EnemyProjectile") || other.GetComponent<FireBall>() != null)
        {
            return;
        }

        Enemy enemy = other.GetComponent<Enemy>();

        if (enemy != null)
        {
            hasExploded = true; 
            
            enemy.TakeDamage(damage);
            if (isPoisonous)
            {
                enemy.ApplyPoison();
            }

            SpawnImpactVFX(other.ClosestPoint(transform.position));

            Destroy(gameObject); 
        }
        else
        {
            if (!other.isTrigger)
            {
                hasExploded = true; 
                
                SpawnImpactVFX(other.ClosestPoint(transform.position));
                Destroy(gameObject);
            }
        }
    }


    private void SpawnImpactVFX(Vector3 impactPoint)
    {
        if (impactVFXPrefab == null)
        {
            Debug.LogWarning("[Projectile] Aucun prefab VFX assigné pour l'impact !");
            return;
        }

        GameObject vfxInstance = Instantiate(impactVFXPrefab, impactPoint, Quaternion.identity);

        vfxInstance.transform.localScale = Vector3.one * particleScale;

        ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var shape = ps.shape;
            shape.radius *= particleScale;

            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(vfxInstance, duration);
        }
        else
        {
            Destroy(vfxInstance, vfxLifetime);
        }
    }

    private void OnDestroy()
    {
        // ✅ MODIFIÉ : Spawner les particules seulement si pas déjà explosé
        if (!hasExploded && gameObject.scene.isLoaded && impactVFXPrefab != null)
        {
            SpawnImpactVFX(transform.position);
        }
    }
}

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

    private bool hasExploded = false; // ✅ NOUVEAU : Empêcher les explosions multiples

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // ✅ NOUVEAU : Ignorer si déjà explosé
        if (hasExploded)
            return;

        // ✅ Ignorer le joueur
        if (other.CompareTag("Player"))
            return;

        // ✅ NOUVEAU : Ignorer tous les colliders qui sont des triggers (zones de détection, etc.)
        // SAUF les ennemis qui ont un trigger pour la détection
        if (other.isTrigger && other.GetComponent<Enemy>() == null)
        {
            // C'est un trigger qui n'est pas un ennemi (ex: ReachZone, zones de quête, etc.)
            return;
        }

        // ✅ Ignorer les projectiles ennemis (fireballs)
        if (other.CompareTag("EnemyProjectile") || other.GetComponent<FireBall>() != null)
        {
            return; // Traverser sans détruire le projectile
        }

        // Vérifier si c'est un ennemi
        Enemy enemy = other.GetComponent<Enemy>();

        if (enemy != null)
        {
            hasExploded = true; // ✅ Marquer comme explosé
            
            enemy.TakeDamage(damage);
            if (isPoisonous)
            {
                enemy.ApplyPoison();
            }

            // Spawner les particules d'impact sur l'ennemi
            SpawnImpactVFX(other.ClosestPoint(transform.position));

            Destroy(gameObject); // Détruire uniquement si on touche un ennemi
        }
        else
        {
            // ✅ MODIFIÉ : Ne détruire que si c'est un collider solide (pas un trigger)
            if (!other.isTrigger)
            {
                hasExploded = true; // ✅ Marquer comme explosé
                
                // Si ce n'est pas un ennemi mais un collider solide (sol, mur, etc.), spawner les particules et détruire
                SpawnImpactVFX(other.ClosestPoint(transform.position));
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Spawner l'effet de particules à la position d'impact
    /// </summary>
    /// <param name="impactPoint">Position de l'impact</param>
    private void SpawnImpactVFX(Vector3 impactPoint)
    {
        if (impactVFXPrefab == null)
        {
            Debug.LogWarning("[Projectile] Aucun prefab VFX assigné pour l'impact !");
            return;
        }

        // Instancier le VFX à la position d'impact
        GameObject vfxInstance = Instantiate(impactVFXPrefab, impactPoint, Quaternion.identity);

        // Ajuster la taille des particules
        vfxInstance.transform.localScale = Vector3.one * particleScale;

        // Vérifier si le prefab a un Particle System pour gérer la durée automatiquement
        ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // Ajuster le shape radius si présent
            var shape = ps.shape;
            shape.radius *= particleScale;

            // Détruire automatiquement après la durée du Particle System
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(vfxInstance, duration);
        }
        else
        {
            // Fallback : détruire après vfxLifetime secondes
            Destroy(vfxInstance, vfxLifetime);
        }
    }

    /// <summary>
    /// Appelé automatiquement quand l'objet est détruit (timeout)
    /// </summary>
    private void OnDestroy()
    {
        // ✅ MODIFIÉ : Spawner les particules seulement si pas déjà explosé
        if (!hasExploded && gameObject.scene.isLoaded && impactVFXPrefab != null)
        {
            SpawnImpactVFX(transform.position);
        }
    }
}

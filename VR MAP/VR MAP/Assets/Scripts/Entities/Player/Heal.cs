using UnityEngine;

public class Heal : MonoBehaviour
{
    public float heal = 50f;   // pv soignés
    private bool isActive = false;

    [Header("VFX")]
    [Tooltip("Prefab de l'effet de particules lors de la collecte")]
    [SerializeField] private GameObject healVFXPrefab;
    [Tooltip("Multiplicateur de taille des particules (1 = taille normale)")]
    [SerializeField] private float particleScale = 1f;
    [Tooltip("Durée avant destruction automatique du VFX")]
    [SerializeField] private float vfxLifetime = 2f;

    [Header("Optional - For old single-heal system")]
    [Tooltip("Positions possibles (utilisé seulement si ActivateRandom est appelé)")]
    public Transform[] possiblePositions;

    private HealSpawnManager spawnManager;

    private void Awake()
    {
        // Trouver le HealSpawnManager (si présent)
        spawnManager = FindObjectOfType<HealSpawnManager>();
    }

    /// <summary>
    /// ✅ NOUVEAU : Active le heal directement sans téléportation
    /// Utilisé par le HealSpawnManager
    /// </summary>
    public void ActivateDirectly()
    {
        isActive = true;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Active le heal à une position aléatoire
    /// ⚠️ LEGACY : Utilisé pour l'ancien système (un seul heal)
    /// </summary>
    public void ActivateRandom()
    {
        if (possiblePositions != null && possiblePositions.Length > 0)
        {
            int index = Random.Range(0, possiblePositions.Length);
            transform.position = possiblePositions[index].position;
        }

        isActive = true;
        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        if (other.CompareTag("Player"))
        {
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                // ✅ NOUVEAU : Spawner l'effet de particules avant de détruire
                SpawnHealVFX(transform.position);

                ps.Heal(heal);

                // Notifier le HealSpawnManager (si présent)
                if (spawnManager != null)
                {
                    spawnManager.OnHealCollected(gameObject);
                }

                // Détruire l'instance (le manager en créera de nouvelles à la prochaine vague)
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// ✅ NOUVEAU : Spawner l'effet de particules à la position de collecte
    /// </summary>
    /// <param name="position">Position où spawner l'effet</param>
    private void SpawnHealVFX(Vector3 position)
    {
        if (healVFXPrefab == null)
        {
            Debug.LogWarning("[Heal] Aucun prefab VFX assigné pour la collecte !");
            return;
        }

        // Instancier le VFX à la position du heal
        GameObject vfxInstance = Instantiate(healVFXPrefab, position, Quaternion.identity);

        // Ajuster la taille des particules
        vfxInstance.transform.localScale = Vector3.one * particleScale;

        // Vérifier si le prefab a un Particle System pour gérer la durée automatiquement
        ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // S'assurer que le Particle System joue
            if (!ps.isPlaying)
                ps.Play();

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
}
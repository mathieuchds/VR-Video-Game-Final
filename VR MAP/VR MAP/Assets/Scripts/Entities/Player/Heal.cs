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
        spawnManager = FindObjectOfType<HealSpawnManager>();
    }


    public void ActivateDirectly()
    {
        isActive = true;
        gameObject.SetActive(true);
    }


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
                SpawnHealVFX(transform.position);

                ps.Heal(heal);

                if (spawnManager != null)
                {
                    spawnManager.OnHealCollected(gameObject);
                }

                Destroy(gameObject);
            }
        }
    }


    private void SpawnHealVFX(Vector3 position)
    {
        if (healVFXPrefab == null)
        {
            Debug.LogWarning("[Heal] Aucun prefab VFX assigné pour la collecte !");
            return;
        }

        GameObject vfxInstance = Instantiate(healVFXPrefab, position, Quaternion.identity);

        vfxInstance.transform.localScale = Vector3.one * particleScale;

        ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            if (!ps.isPlaying)
                ps.Play();

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
}
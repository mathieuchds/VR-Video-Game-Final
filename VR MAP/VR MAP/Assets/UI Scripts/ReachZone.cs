using UnityEngine;

public class ReachZone : MonoBehaviour
{
    [Header("Random Spawn")]
    [SerializeField] private Transform[] possiblePositions;

    [Header("Visual Highlight")]
    [Tooltip("Couleur de surbrillance")]
    [SerializeField] private Color highlightColor = new Color(0f, 1f, 1f, 0.5f); // Cyan semi-transparent
    [Tooltip("Intensité de la surbrillance")]
    [SerializeField] private float glowIntensity = 2f;
    [Tooltip("Pulse l'effet de surbrillance")]
    [SerializeField] private bool pulseEffect = true;
    [Tooltip("Vitesse du pulse")]
    [SerializeField] private float pulseSpeed = 2f;

    private bool isActive = false;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private float pulseTimer = 0f;

    private void Awake()
    {
        // Récupérer tous les renderers de la zone
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        // Configuration initiale des matériaux
        SetupHighlightMaterials();
    }

    private void SetupHighlightMaterials()
    {
        foreach (var renderer in renderers)
        {
            // Pour chaque material, activer le mode émissif
            foreach (var mat in renderer.materials)
            {
                // Activer l'émission
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", highlightColor * glowIntensity);

                // Rendre le matériau semi-transparent
                mat.SetFloat("_Mode", 3); // Mode Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;

                // Couleur de base avec transparence
                Color baseColor = highlightColor;
                baseColor.a = 0.3f;
                mat.SetColor("_Color", baseColor);
            }
        }
    }

    private void Update()
    {
        if (!isActive || !pulseEffect) return;

        // Effet de pulse
        pulseTimer += Time.deltaTime * pulseSpeed;
        float pulseFactor = (Mathf.Sin(pulseTimer) + 1f) / 2f; // Oscille entre 0 et 1

        Color emissionColor = highlightColor * Mathf.Lerp(glowIntensity * 0.5f, glowIntensity * 1.5f, pulseFactor);

        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                mat.SetColor("_EmissionColor", emissionColor);
            }
        }
    }

    public void ActivateRandom()
    {
        if (possiblePositions.Length > 0)
        {
            int index = Random.Range(0, possiblePositions.Length);
            transform.position = possiblePositions[index].position;
        }

        isActive = true;
        gameObject.SetActive(true);
        pulseTimer = 0f; // Réinitialiser le timer du pulse
    }

    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;

        QuestManager.Instance?.AddProgress(QuestObjectiveType.ReachZone, 1);

        Deactivate();
    }
}

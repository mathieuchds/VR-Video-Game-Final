using System.Collections.Generic;
using UnityEngine;

public class WeatherManagement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelData levelData;
    [SerializeField] private Transform player; // Pour suivre le joueur

    [Header("Weather Prefabs")]
    [Tooltip("Prefab de l'effet de pluie (Particle System)")]
    [SerializeField] private GameObject rainPrefab;
    [Tooltip("Prefab de l'effet de neige (Particle System)")]
    [SerializeField] private GameObject snowPrefab;

    [Header("Weather Settings")]
    [Tooltip("Vague à partir de laquelle la pluie commence")]
    [SerializeField] private int rainStartWave = 2;
    [Tooltip("Vague à partir de laquelle la neige commence")]
    [SerializeField] private int snowStartWave = 6;
    [Tooltip("Vague où la pluie atteint son maximum")]
    [SerializeField] private int rainMaxWave = 5;
    [Tooltip("Vague où la neige atteint son maximum")]
    [SerializeField] private int snowMaxWave = 10;

    [Header("Rain Settings")]
    [Tooltip("Densité minimale de pluie (vague de départ)")]
    [SerializeField] private float rainMinDensity = 50f;
    [Tooltip("Densité maximale de pluie (vague max)")]
    [SerializeField] private float rainMaxDensity = 300f;
    [Tooltip("Hauteur au-dessus du joueur pour spawner la pluie")]
    [SerializeField] private float rainHeightAbovePlayer = 20f;
    [Tooltip("Rayon de chaque instance de pluie")]
    [SerializeField] private float rainEffectRadius = 30f;
    [Tooltip("Nombre d'instances de pluie à spawner en grille")]
    [SerializeField] private int rainGridSize = 5;
    [Tooltip("Espacement entre les instances de pluie")]
    [SerializeField] private float rainGridSpacing = 50f;

    [Header("Snow Settings")]
    [Tooltip("Densité minimale de neige (vague de départ)")]
    [SerializeField] private float snowMinDensity = 100f;
    [Tooltip("Densité maximale de neige (vague max)")]
    [SerializeField] private float snowMaxDensity = 500f;
    [Tooltip("Multiplicateur de taille des particules de neige (0.5 = 50% plus petit)")]
    [SerializeField] private float snowParticleSizeMultiplier = 0.5f;
    [Tooltip("Hauteur au-dessus du joueur pour spawner la neige")]
    [SerializeField] private float snowHeightAbovePlayer = 25f;
    [Tooltip("Rayon de chaque instance de neige")]
    [SerializeField] private float snowEffectRadius = 35f;
    [Tooltip("Nombre d'instances de neige à spawner en grille")]
    [SerializeField] private int snowGridSize = 7;
    [Tooltip("Espacement entre les instances de neige")]
    [SerializeField] private float snowGridSpacing = 40f;

    [Header("General Settings")]
    [Tooltip("L'effet suit le joueur en temps réel")]
    [SerializeField] private bool followPlayer = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Instances actuelles des effets (plusieurs pour couvrir la map)
    private List<GameObject> currentRainInstances = new List<GameObject>();
    private List<GameObject> currentSnowInstances = new List<GameObject>();

    private int lastLevel = -1;

    private void Awake()
    {
        // Trouver les références si non assignées
        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (levelData == null)
            Debug.LogError("[WeatherManagement] LevelData introuvable — système météo désactivé.");

        if (player == null)
            Debug.LogWarning("[WeatherManagement] Player introuvable — l'effet météo sera statique.");
    }

    private void Start()
    {
        if (levelData != null)
        {
            lastLevel = levelData.level;
            UpdateWeather(lastLevel);
        }
    }

    private void Update()
    {
        if (levelData == null) return;

        // Détecter le changement de niveau
        if (levelData.level != lastLevel)
        {
            lastLevel = levelData.level;
            UpdateWeather(lastLevel);
        }

        // Faire suivre l'effet au joueur si activé
        if (followPlayer && player != null)
        {
            UpdateWeatherPosition();
        }
    }

    /// <summary>
    /// Met à jour les effets météo en fonction de la vague actuelle
    /// </summary>
    private void UpdateWeather(int wave)
    {
        if (debugMode)
            Debug.Log($"[WeatherManagement] === UPDATE MÉTÉO VAGUE {wave} ===");

        // Gérer la pluie (vagues 2-5)
        if (wave >= rainStartWave && wave < snowStartWave)
        {
            float density = CalculateRainDensity(wave);
            ActivateRain(density);
            DeactivateSnow();

            if (debugMode)
                Debug.Log($"[WeatherManagement] 🌧️ Pluie activée - Densité: {density}");
        }
        // Gérer la neige (vagues 6+)
        else if (wave >= snowStartWave)
        {
            float density = CalculateSnowDensity(wave);
            DeactivateRain();
            ActivateSnow(density);

            if (debugMode)
                Debug.Log($"[WeatherManagement] ❄️ Neige activée - Densité: {density}");
        }
        // Pas de météo (vague 1)
        else
        {
            DeactivateRain();
            DeactivateSnow();

            if (debugMode)
                Debug.Log($"[WeatherManagement] ☀️ Pas de météo");
        }
    }

    /// <summary>
    /// Calcule la densité de pluie en fonction de la progression des vagues
    /// </summary>
    private float CalculateRainDensity(int currentWave)
    {
        float progress = Mathf.Clamp01((float)(currentWave - rainStartWave) / (rainMaxWave - rainStartWave));
        return Mathf.Lerp(rainMinDensity, rainMaxDensity, progress);
    }

    /// <summary>
    /// Calcule la densité de neige en fonction de la progression des vagues
    /// </summary>
    private float CalculateSnowDensity(int currentWave)
    {
        float progress = Mathf.Clamp01((float)(currentWave - snowStartWave) / (snowMaxWave - snowStartWave));
        return Mathf.Lerp(snowMinDensity, snowMaxDensity, progress);
    }

    /// <summary>
    /// Active l'effet de pluie avec une densité donnée (plusieurs instances en grille)
    /// </summary>
    private void ActivateRain(float density)
    {
        // Si pas encore créé, instancier plusieurs instances en grille
        if (currentRainInstances.Count == 0 && rainPrefab != null)
        {
            Vector3 centerPos = GetRainSpawnPosition(); // ✅ MODIFIÉ : Position spécifique à la pluie
            
            // Créer une grille d'instances avec la taille spécifique à la pluie
            int halfGrid = rainGridSize / 2; // ✅ MODIFIÉ
            
            for (int x = -halfGrid; x <= halfGrid; x++)
            {
                for (int z = -halfGrid; z <= halfGrid; z++)
                {
                    Vector3 offset = new Vector3(x * rainGridSpacing, 0, z * rainGridSpacing); // ✅ MODIFIÉ
                    Vector3 spawnPos = centerPos + offset;
                    
                    // Rotation de 90° sur l'axe X pour orienter la pluie vers le bas
                    Quaternion downwardRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameObject instance = Instantiate(rainPrefab, spawnPos, downwardRotation, transform);
                    instance.name = $"RainEffect_{x}_{z}";
                    
                    ParticleSystem ps = instance.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        Debug.LogError("[WeatherManagement] Le prefab de pluie ne contient pas de ParticleSystem !");
                        Destroy(instance);
                        continue;
                    }

                    // Changer la densité et le radius
                    var emission = ps.emission;
                    emission.rateOverTime = density;

                    var shape = ps.shape;
                    shape.radius = rainEffectRadius; // ✅ MODIFIÉ

                    currentRainInstances.Add(instance);
                }
            }

            if (debugMode)
                Debug.Log($"[WeatherManagement] 🌧️ {currentRainInstances.Count} instances de pluie créées en grille {rainGridSize}x{rainGridSize}");
        }

        // Mettre à jour la densité de toutes les instances
        foreach (var instance in currentRainInstances)
        {
            if (instance == null) continue;
            
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = density;

                if (!ps.isPlaying)
                    ps.Play();
            }
        }

        if (debugMode && currentRainInstances.Count > 0)
            Debug.Log($"[WeatherManagement] 🌧️ Densité pluie mise à jour: {density} sur {currentRainInstances.Count} instances");
    }

    /// <summary>
    /// Active l'effet de neige avec une densité donnée (plusieurs instances en grille)
    /// </summary>
    private void ActivateSnow(float density)
    {
        // Si pas encore créé, instancier plusieurs instances en grille
        if (currentSnowInstances.Count == 0 && snowPrefab != null)
        {
            Vector3 centerPos = GetSnowSpawnPosition(); // ✅ MODIFIÉ : Position spécifique à la neige
            
            // Créer une grille d'instances avec la taille spécifique à la neige
            int halfGrid = snowGridSize / 2; // ✅ MODIFIÉ
            
            for (int x = -halfGrid; x <= halfGrid; x++)
            {
                for (int z = -halfGrid; z <= halfGrid; z++)
                {
                    Vector3 offset = new Vector3(x * snowGridSpacing, 0, z * snowGridSpacing); // ✅ MODIFIÉ
                    Vector3 spawnPos = centerPos + offset;
                    
                    // Rotation de 180° sur l'axe X pour la neige
                    Quaternion snowRotation = Quaternion.Euler(180f, 0f, 0f);
                    GameObject instance = Instantiate(snowPrefab, spawnPos, snowRotation, transform);
                    instance.name = $"SnowEffect_{x}_{z}";
                    
                    ParticleSystem ps = instance.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        Debug.LogError("[WeatherManagement] Le prefab de neige ne contient pas de ParticleSystem !");
                        Destroy(instance);
                        continue;
                    }

                    // Changer la densité et le radius
                    var emission = ps.emission;
                    emission.rateOverTime = density;

                    var shape = ps.shape;
                    shape.radius = snowEffectRadius; // ✅ MODIFIÉ

                    // Réduire la taille des particules de neige
                    var main = ps.main;
                    main.startSizeMultiplier *= snowParticleSizeMultiplier;

                    currentSnowInstances.Add(instance);
                }
            }

            if (debugMode)
                Debug.Log($"[WeatherManagement] ❄️ {currentSnowInstances.Count} instances de neige créées en grille {snowGridSize}x{snowGridSize} (taille: {snowParticleSizeMultiplier}x)");
        }

        // Mettre à jour la densité de toutes les instances
        foreach (var instance in currentSnowInstances)
        {
            if (instance == null) continue;
            
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = density;

                if (!ps.isPlaying)
                    ps.Play();
            }
        }

        if (debugMode && currentSnowInstances.Count > 0)
            Debug.Log($"[WeatherManagement] ❄️ Densité neige mise à jour: {density} sur {currentSnowInstances.Count} instances");
    }

    /// <summary>
    /// Désactive l'effet de pluie
    /// </summary>
    private void DeactivateRain()
    {
        foreach (var instance in currentRainInstances)
        {
            if (instance != null)
            {
                ParticleSystem ps = instance.GetComponent<ParticleSystem>();
                if (ps != null && ps.isPlaying)
                    ps.Stop();
                
                Destroy(instance);
            }
        }
        
        currentRainInstances.Clear();

        if (debugMode)
            Debug.Log("[WeatherManagement] 🌧️ Toutes les instances de pluie arrêtées");
    }

    /// <summary>
    /// Désactive l'effet de neige
    /// </summary>
    private void DeactivateSnow()
    {
        foreach (var instance in currentSnowInstances)
        {
            if (instance != null)
            {
                ParticleSystem ps = instance.GetComponent<ParticleSystem>();
                if (ps != null && ps.isPlaying)
                    ps.Stop();
                
                Destroy(instance);
            }
        }
        
        currentSnowInstances.Clear();

        if (debugMode)
            Debug.Log("[WeatherManagement] ❄️ Toutes les instances de neige arrêtées");
    }

    /// <summary>
    /// ✅ NOUVEAU : Retourne la position de spawn spécifique à la pluie
    /// </summary>
    private Vector3 GetRainSpawnPosition()
    {
        if (player != null)
        {
            return player.position + Vector3.up * rainHeightAbovePlayer;
        }
        else
        {
            return new Vector3(0, rainHeightAbovePlayer, 0);
        }
    }

    /// <summary>
    /// ✅ NOUVEAU : Retourne la position de spawn spécifique à la neige
    /// </summary>
    private Vector3 GetSnowSpawnPosition()
    {
        if (player != null)
        {
            return player.position + Vector3.up * snowHeightAbovePlayer;
        }
        else
        {
            return new Vector3(0, snowHeightAbovePlayer, 0);
        }
    }

    /// <summary>
    /// Met à jour la position de l'effet météo pour suivre le joueur
    /// </summary>
    private void UpdateWeatherPosition()
    {
        // Mettre à jour toutes les instances de pluie
        if (currentRainInstances.Count > 0)
        {
            Vector3 rainCenterPos = GetRainSpawnPosition(); // ✅ MODIFIÉ
            int rainHalfGrid = rainGridSize / 2; // ✅ MODIFIÉ

            int rainIndex = 0;
            for (int x = -rainHalfGrid; x <= rainHalfGrid; x++)
            {
                for (int z = -rainHalfGrid; z <= rainHalfGrid; z++)
                {
                    if (rainIndex < currentRainInstances.Count && currentRainInstances[rainIndex] != null)
                    {
                        Vector3 offset = new Vector3(x * rainGridSpacing, 0, z * rainGridSpacing); // ✅ MODIFIÉ
                        currentRainInstances[rainIndex].transform.position = rainCenterPos + offset;
                    }
                    rainIndex++;
                }
            }
        }

        // Mettre à jour toutes les instances de neige
        if (currentSnowInstances.Count > 0)
        {
            Vector3 snowCenterPos = GetSnowSpawnPosition(); // ✅ MODIFIÉ
            int snowHalfGrid = snowGridSize / 2; // ✅ MODIFIÉ

            int snowIndex = 0;
            for (int x = -snowHalfGrid; x <= snowHalfGrid; x++)
            {
                for (int z = -snowHalfGrid; z <= snowHalfGrid; z++)
                {
                    if (snowIndex < currentSnowInstances.Count && currentSnowInstances[snowIndex] != null)
                    {
                        Vector3 offset = new Vector3(x * snowGridSpacing, 0, z * snowGridSpacing); // ✅ MODIFIÉ
                        currentSnowInstances[snowIndex].transform.position = snowCenterPos + offset;
                    }
                    snowIndex++;
                }
            }
        }
    }

    /// <summary>
    /// Nettoie les effets à la destruction du script
    /// </summary>
    private void OnDestroy()
    {
        DeactivateRain();
        DeactivateSnow();
    }

    /// <summary>
    /// Méthode publique pour forcer la mise à jour de la météo
    /// </summary>
    public void ForceUpdateWeather()
    {
        if (levelData != null)
        {
            UpdateWeather(levelData.level);
        }
    }

    #region Debug Helpers

    [ContextMenu("Debug: Show Weather Status")]
    private void DebugShowWeatherStatus()
    {
        Debug.Log("=== WEATHER STATUS ===");
        Debug.Log($"Wave: {levelData?.level}");
        Debug.Log($"Rain Instances: {currentRainInstances.Count} (Grid: {rainGridSize}x{rainGridSize}, Height: {rainHeightAbovePlayer}m)");
        Debug.Log($"Snow Instances: {currentSnowInstances.Count} (Grid: {snowGridSize}x{snowGridSize}, Height: {snowHeightAbovePlayer}m)");
        
        if (currentRainInstances.Count > 0)
        {
            ParticleSystem ps = currentRainInstances[0]?.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                Debug.Log($"Rain Density: {emission.rateOverTime.constant}, Radius: {rainEffectRadius}m");
            }
        }
        
        if (currentSnowInstances.Count > 0)
        {
            ParticleSystem ps = currentSnowInstances[0]?.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                var main = ps.main;
                Debug.Log($"Snow Density: {emission.rateOverTime.constant}, Radius: {snowEffectRadius}m");
                Debug.Log($"Snow Particle Size: {main.startSizeMultiplier}");
            }
        }
    }

    [ContextMenu("Debug: Force Rain")]
    private void DebugForceRain()
    {
        ActivateRain(150f);
        DeactivateSnow();
    }

    [ContextMenu("Debug: Force Snow")]
    private void DebugForceSnow()
    {
        DeactivateRain();
        ActivateSnow(250f);
    }
     
    #endregion
}
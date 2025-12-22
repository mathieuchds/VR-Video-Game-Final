using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public enum SpawnerSeason
{
    Halloween,
    Winter
}

public enum SpawnerType
{
    Normal,
    Miniboss
}

[Serializable]
public struct WaveSetting
{
    [Tooltip("Nombre d'ennemis à spawn pour le slot 1 (catégorie 0) pour cette vague.")]
    public int count0;

    [Tooltip("Nombre d'ennemis à spawn pour le slot 2 (catégorie 1) pour cette vague.")]
    public int count1;

    [Tooltip("Nombre d'ennemis à spawn pour le slot 3 (catégorie 2) pour cette vague.")]
    public int count2;

    [Tooltip("Intervalle en secondes entre deux spawns pour cette vague.")]
    public float interval;
}

[Serializable]
public struct MinibossStats
{
    [Tooltip("Multiplicateur de HP (ex: 2.0 = 2x plus de HP)")]
    public float healthMultiplier;

    [Tooltip("Multiplicateur de dégâts (ex: 1.5 = 1.5x plus de dégâts)")]
    public float damageMultiplier;

    [Tooltip("Multiplicateur de taille (ex: 1.5 = 50% plus gros)")]
    public float sizeMultiplier;
}

public class SpawnerContent : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private SpawnerSeason seasonType = SpawnerSeason.Halloween;
    public SpawnerSeason SeasonType => seasonType;

    [SerializeField] private SpawnerType spawnerType = SpawnerType.Normal;
    public SpawnerType Type => spawnerType;

    [Header("Mob prefabs (exactement 3 emplacements)")]
    [SerializeField] private GameObject[] mobPrefabs = new GameObject[3];

    [Header("Wave settings (index 0 = vague 1)")]
    [SerializeField] private WaveSetting[] waves;

    [Header("Miniboss Settings (si spawnerType = Miniboss)")]
    [SerializeField] private MinibossStats minibossStats = new MinibossStats
    {
        healthMultiplier = 3.0f,
        damageMultiplier = 2.0f,
        sizeMultiplier = 1.5f
    };

    [Header("Scaling")]
    [SerializeField, Tooltip("Intervalle minimal entre deux spawns")]
    private float minInterval = 0.05f;

    [SerializeField, Tooltip("Facteur de croissance du nombre d'ennemis par vague au-delà des réglages explicites")]
    private float countGrowthPerExtraWave = 1.15f;

    [SerializeField, Tooltip("Facteur de réduction d'intervalle par vague (plus la vague est haute, plus c'est rapide)")]
    private float intervalReductionPerWave = 0.05f;

    [Header("Events")]
    [SerializeField] private UnityEvent onWaveCompleted;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private Coroutine spawnCoroutine;

    // Optionnel : callback C#
    public event Action<SpawnerContent> WaveCompleted;

    // Level listening
    private LevelData levelData;
    private int observedLevel = -1;

    private void Awake()
    {
        // Rendre ce GameObject invisible (désactiver renderers et colliders)
        HideSpawnerVisuals();

        if (mobPrefabs == null || mobPrefabs.Length != 3)
            mobPrefabs = ResizeArray(mobPrefabs, 3);

        if (CountNonNull(mobPrefabs) == 0)
            Debug.LogWarning($"[SpawnerContent] '{name}' : aucun prefab assigné dans les 3 slots.");

        if (waves == null || waves.Length == 0)
            Debug.LogWarning($"[SpawnerContent] '{name}' : aucun WaveSetting configuré (waves).");
    }

    private void Start()
    {
        // ✅ Rechercher LevelData
        levelData = FindObjectOfType<LevelData>();
        
        if (levelData != null)
        {
            observedLevel = levelData.level;
            StartSpawning(observedLevel);
            
            if (spawnerType == SpawnerType.Miniboss && debugMode)
                Debug.Log($"[SpawnerContent] Miniboss spawner '{name}' démarré pour vague {observedLevel}");
        }
        else
        {
            Debug.LogWarning($"[SpawnerContent] '{name}' : LevelData introuvable — spawn par niveau inactif.");
        }
    }

    private void OnEnable()
    {
        // ✅ MODIFIÉ : Toujours chercher LevelData et redémarrer
        if (spawnerType == SpawnerType.Miniboss && debugMode)
            Debug.Log($"[SpawnerContent] Miniboss spawner '{name}' réactivé (OnEnable)");

        // Chercher LevelData si on ne l'a pas encore
        if (levelData == null)
        {
            levelData = FindObjectOfType<LevelData>();
            
            if (spawnerType == SpawnerType.Miniboss && debugMode)
                Debug.Log($"[SpawnerContent] LevelData {(levelData != null ? "trouvé" : "introuvable")}");
        }

        // Si on a LevelData, forcer le restart
        if (levelData != null)
        {
            observedLevel = -1; // Forcer un nouveau démarrage
            StartCoroutine(DelayedRestart());
        }
    }

    private void Update()
    {
        // ✅ Chercher LevelData si on ne l'a pas
        if (levelData == null)
        {
            levelData = FindObjectOfType<LevelData>();
            if (levelData == null) return;
            
            observedLevel = levelData.level;
            StartSpawning(observedLevel);
            
            if (spawnerType == SpawnerType.Miniboss && debugMode)
                Debug.Log($"[SpawnerContent] LevelData trouvé dans Update, spawn démarré pour vague {observedLevel}");
            
            return;
        }

        // Détecter changement de niveau
        if (levelData.level != observedLevel)
        {
            observedLevel = levelData.level;
            StartSpawning(observedLevel);
            
            if (spawnerType == SpawnerType.Miniboss && debugMode)
                Debug.Log($"[SpawnerContent] Miniboss spawner '{name}' redémarré pour nouvelle vague {observedLevel}");
        }
    }

    // Rendre le spawner invisible
    private void HideSpawnerVisuals()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (!c.isTrigger)
                c.enabled = false;
        }
    }

    // Initialize called by SeasonalSpawnManager (optional)
    public void Initialize(SpawnerSeason season, GameObject[] prefabs)
    {
        seasonType = season;
        mobPrefabs = ResizeArray(prefabs, 3);
    }

    public void StartSpawning(int waveNumber)
    {
        StopSpawning();
        spawnCoroutine = StartCoroutine(SpawnWaveCoroutine(waveNumber));
        
        if (spawnerType == SpawnerType.Miniboss && debugMode)
            Debug.Log($"[SpawnerContent] 🎯 Miniboss spawner '{name}' commence spawn pour vague {waveNumber}");
    }

    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    /// <summary>
    /// ✅ Reset complet du spawner (force le restart du spawn)
    /// </summary>
    public void ResetSpawner()
    {
        if (spawnerType == SpawnerType.Miniboss && debugMode)
            Debug.Log($"[SpawnerContent] 🔄 ResetSpawner() appelé sur '{name}'");

        // 1. Arrêter tout spawn en cours
        StopSpawning();

        // 2. Réinitialiser le niveau observé pour forcer un nouveau spawn
        observedLevel = -1;

        // 3. Chercher LevelData si nécessaire
        if (levelData == null)
        {
            levelData = FindObjectOfType<LevelData>();
            
            if (spawnerType == SpawnerType.Miniboss && debugMode)
                Debug.Log($"[SpawnerContent] LevelData {(levelData != null ? "trouvé" : "INTROUVABLE")} lors du reset");
        }

        // 4. Redémarrer avec délai
        StartCoroutine(DelayedRestart());
    }

    /// <summary>
    /// ✅ Redémarre le spawner après une frame
    /// </summary>
    private IEnumerator DelayedRestart()
    {
        yield return null; // Attendre une frame
        
        // ✅ Chercher LevelData si on ne l'a toujours pas
        if (levelData == null)
        {
            levelData = FindObjectOfType<LevelData>();
        }
        
        if (levelData != null)
        {
            observedLevel = levelData.level;
            StartSpawning(observedLevel);
            
            if (spawnerType == SpawnerType.Miniboss)
            {
                Debug.Log($"[SpawnerContent] ✅ Miniboss spawner '{name}' redémarré pour vague {observedLevel}");
            }
        }
        else
        {
            if (spawnerType == SpawnerType.Miniboss)
                Debug.LogError($"[SpawnerContent] ❌ Miniboss spawner '{name}' ne peut pas redémarrer : LevelData introuvable !");
        }
    }

    private IEnumerator SpawnWaveCoroutine(int waveNumber)
    {
        if (CountNonNull(mobPrefabs) == 0)
        {
            if (spawnerType == SpawnerType.Miniboss)
                Debug.LogWarning($"[SpawnerContent] Miniboss spawner '{name}' n'a aucun prefab !");
            yield break;
        }

        int index = Mathf.Max(0, waveNumber - 1);

        int[] baseCounts = new int[3];
        float baseInterval;

        if (index < waves.Length)
        {
            baseCounts[0] = Mathf.Max(0, waves[index].count0);
            baseCounts[1] = Mathf.Max(0, waves[index].count1);
            baseCounts[2] = Mathf.Max(0, waves[index].count2);
            baseInterval = Mathf.Max(minInterval, waves[index].interval);
        }
        else if (waves.Length > 0)
        {
            var last = waves[waves.Length - 1];
            int extra = index - (waves.Length - 1);
            baseCounts[0] = Mathf.Max(0, Mathf.CeilToInt(last.count0 * Mathf.Pow(countGrowthPerExtraWave, extra)));
            baseCounts[1] = Mathf.Max(0, Mathf.CeilToInt(last.count1 * Mathf.Pow(countGrowthPerExtraWave, extra)));
            baseCounts[2] = Mathf.Max(0, Mathf.CeilToInt(last.count2 * Mathf.Pow(countGrowthPerExtraWave, extra)));
            baseInterval = Mathf.Max(minInterval, last.interval / (1f + extra * intervalReductionPerWave));
        }
        else
        {
            baseCounts[0] = Mathf.Max(0, 2 + (waveNumber - 1) * 1);
            baseCounts[1] = Mathf.Max(0, 1 + (waveNumber - 1) * 1);
            baseCounts[2] = Mathf.Max(0, 0 + (waveNumber - 1) * 1);
            baseInterval = Mathf.Max(minInterval, 1f / (1f + (waveNumber - 1) * intervalReductionPerWave));
        }

        for (int i = 0; i < 3; i++)
        {
            if (baseCounts[i] > 0 && (mobPrefabs == null || mobPrefabs.Length <= i || mobPrefabs[i] == null))
            {
                Debug.LogWarning($"[SpawnerContent] '{name}': wave {waveNumber} demande {baseCounts[i]} spawns pour slot {i} mais prefab null");
                baseCounts[i] = 0;
            }
        }

        int remainingTotal = baseCounts[0] + baseCounts[1] + baseCounts[2];
        if (remainingTotal <= 0)
        {
            if (spawnerType == SpawnerType.Miniboss)
                Debug.LogWarning($"[SpawnerContent] Miniboss spawner '{name}' n'a aucun ennemi à spawner pour vague {waveNumber}!");
            yield break;
        }

        if (spawnerType == SpawnerType.Miniboss)
            Debug.Log($"[SpawnerContent] 👹 Miniboss spawner '{name}' va spawner {remainingTotal} ennemi(s)");

        float interval = Mathf.Max(minInterval, baseInterval);

        System.Random rnd = new System.Random();
        while (remainingTotal > 0)
        {
            var avail = new System.Collections.Generic.List<int>(3);
            for (int i = 0; i < 3; i++) if (baseCounts[i] > 0) avail.Add(i);

            if (avail.Count == 0) break;

            int choice = avail[rnd.Next(avail.Count)];
            var prefab = mobPrefabs[choice];
            if (prefab == null)
            {
                baseCounts[choice] = 0;
                remainingTotal = baseCounts[0] + baseCounts[1] + baseCounts[2];
                continue;
            }

            Vector3 spawnPos = transform.position + Vector3.up * 1f;
            Quaternion spawnRot = transform.rotation;

            GameObject spawnedEnemy = Instantiate(prefab, spawnPos, spawnRot, null);

            // Appliquer les stats de miniboss si nécessaire
            if (spawnerType == SpawnerType.Miniboss)
            {
                //ApplyMinibossModifiers(spawnedEnemy);
                Debug.Log($"[SpawnerContent] 👹 Miniboss '{spawnedEnemy.name}' spawné !");
            }

            baseCounts[choice]--;
            remainingTotal--;

            yield return new WaitForSeconds(interval);
        }

        spawnCoroutine = null;
        onWaveCompleted?.Invoke();
        WaveCompleted?.Invoke(this);
        
        if (spawnerType == SpawnerType.Miniboss)
            Debug.Log($"[SpawnerContent] ✅ Miniboss spawner '{name}' terminé pour vague {waveNumber}");
    }

    private void ApplyMinibossModifiers(GameObject enemy)
    {
        if (enemy == null) return;

        // Appliquer le multiplicateur de taille
        enemy.transform.localScale *= minibossStats.sizeMultiplier;

        // Chercher et modifier le composant Enemy
        var enemyComponent = enemy.GetComponent<Enemy>();
        if (enemyComponent != null)
        {
            enemyComponent.maxHealth *= minibossStats.healthMultiplier;
            enemyComponent.contactDamage *= minibossStats.damageMultiplier;
            
            Debug.Log($"[SpawnerContent] Miniboss modifié: HP={enemyComponent.maxHealth}, Damage={enemyComponent.contactDamage}, Size={minibossStats.sizeMultiplier}x");
        }
        else
        {
            Debug.LogWarning($"[SpawnerContent] GameObject '{enemy.name}' n'a pas de composant Enemy!");
        }

        ApplyMinibossVisualEffect(enemy);
    }

    private void ApplyMinibossVisualEffect(GameObject enemy)
    {
        var renderers = enemy.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                mat.color = Color.Lerp(mat.color, new Color(1f, 0.3f, 0f), 0.4f);
            }
        }
    }

    // Helpers
    private static int CountNonNull(GameObject[] arr)
    {
        if (arr == null) return 0;
        int c = 0;
        foreach (var g in arr) if (g != null) c++;
        return c;
    }

    private static GameObject[] ResizeArray(GameObject[] src, int size)
    {
        if (src != null && src.Length == size) return src;
        var dst = new GameObject[size];
        if (src == null) return dst;
        for (int i = 0; i < Math.Min(src.Length, size); i++) dst[i] = src[i];
        return dst;
    }
}
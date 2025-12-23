using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro; // Pour TextMeshPro

public class LevelManagement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelData levelData;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private SeasonalSpawnManager seasonalSpawnManager;
    [SerializeField] private PowerSelectionManager powerSelectionManager; // ✅ NOUVEAU

    [Header("Spawner Prefabs References")]
    [Tooltip("Prefab spawner Halloween (pour lire les WaveSetting)")]
    [SerializeField] private GameObject spawnerPrefabHalloween;
    [Tooltip("Prefab spawner Winter (pour lire les WaveSetting)")]
    [SerializeField] private GameObject spawnerPrefabWinter;
    [Tooltip("Prefab spawner Halloween Miniboss")]
    [SerializeField] private GameObject spawnerPrefabHalloweenMiniboss;
    [Tooltip("Prefab spawner Winter Miniboss")]
    [SerializeField] private GameObject spawnerPrefabWinterMiniboss;

    [Header("Marker Detection (doit correspondre à SeasonalSpawnManager)")]
    [Tooltip("Si vrai, on recherche les marqueurs par tag plutôt que par nom.")]
    [SerializeField] private bool useMarkerTag = false;
    [Tooltip("Tag des marqueurs Halloween (si useMarkerTag = true).")]
    [SerializeField] private string markerTagHalloween = "SpawnHalloween";
    [Tooltip("Tag des marqueurs Winter (si useMarkerTag = true).")]
    [SerializeField] private string markerTagWinter = "SpawnWinter";

    [Header("Enemy Detection")]
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Wave Settings")]
    [SerializeField] private int maxWave = 10;
    [Tooltip("Délai avant de vérifier si tous les ennemis sont morts (secondes)")]
    [SerializeField] private float checkDelay = 2f;
    [Tooltip("Durée de l'écran de transition entre les vagues (secondes)")]
    [SerializeField] private float waveTransitionDuration = 3f;

    [Header("Wave Transition UI")]
    [Tooltip("Canvas affichant la transition de vague (fond noir + texte)")]
    [SerializeField] private GameObject waveTransitionCanvas;
    [Tooltip("Texte affichant le numéro de la vague (TextMeshPro recommandé)")]
    [SerializeField] private TextMeshProUGUI waveNumberText;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    [SerializeField] private Heal healthObject;


    private bool bossSpawned = false;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private bool isActive = false;
    private bool isInWaveTransition = false;
    private bool waitingForPowerSelection = false; // ✅ NOUVEAU : Flag pour la sélection de pouvoir
    private Coroutine checkCoroutine;
    
    // Compteurs pour la vague actuelle
    private int expectedEnemiesThisWave = 0;
    private int enemiesKilledThisWave = 0;
    private int currentAliveEnemies = 0;

    // Pour tracker les ennemis spawned
    private HashSet<GameObject> trackedEnemies = new HashSet<GameObject>();

    private void Awake()
    {
        // Trouver les références si non assignées
        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();

        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (seasonalSpawnManager == null)
            seasonalSpawnManager = FindObjectOfType<SeasonalSpawnManager>();

        // ✅ NOUVEAU : Trouver le PowerSelectionManager
        if (powerSelectionManager == null)
            powerSelectionManager = FindObjectOfType<PowerSelectionManager>();

        if (levelData == null)
            Debug.LogError("[LevelManagement] LevelData introuvable — le système de vagues ne fonctionnera pas.");

        if (gameStateManager == null)
            Debug.LogError("[LevelManagement] GameStateManager introuvable — impossible de détecter l'état du jeu.");

        if (seasonalSpawnManager == null)
            Debug.LogWarning("[LevelManagement] SeasonalSpawnManager introuvable — les totaux de marqueurs ne seront pas synchronisés.");

        if (powerSelectionManager == null)
            Debug.LogWarning("[LevelManagement] PowerSelectionManager introuvable — pas de sélection de pouvoir.");

        // ✅ Masquer le canvas de transition au démarrage
        if (waveTransitionCanvas != null)
            waveTransitionCanvas.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameStateManager != null)
        {
            gameStateManager.OnStateChanged += HandleStateChange;
        }
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
        {
            gameStateManager.OnStateChanged -= HandleStateChange;
        }

        StopChecking();
    }

    private void HandleStateChange(GameStateManager.GameState previousState, GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Game)
        {
            StartLevelManagement();
        }
        else
        {
            StopLevelManagement();
        }
    }

    /// <summary>
    /// Démarre la gestion des vagues
    /// </summary>
    private void StartLevelManagement()
    {
        if (levelData == null)
        {
            Debug.LogWarning("[LevelManagement] Impossible de démarrer — LevelData manquant.");
            return;
        }

        isActive = true;
        isInWaveTransition = false;
        waitingForPowerSelection = false;

        // Réinitialiser le niveau à 1 au démarrage d'une nouvelle partie
        levelData.level = 1;
        enemiesKilledThisWave = 0;
        trackedEnemies.Clear();

        // ✅ NOUVEAU : S'assurer que le canvas de jeu est visible avant de commencer
        if (gameStateManager != null)
        {
            gameStateManager.SetWaveTransitionMode(false);
            if (debugMode)
                Debug.Log("[LevelManagement] Canvas de jeu forcé visible au démarrage");
        }

        // Afficher la transition de la vague 1 avant de commencer
        StartCoroutine(ShowWaveTransitionAndStart(levelData.level));
    }

    /// <summary>
    /// ✅ MODIFIÉ : Affiche l'écran de transition puis la sélection de pouvoir APRÈS
    /// </summary>
    private IEnumerator ShowWaveTransitionAndStart(int waveNumber)
    {
        isInWaveTransition = true;

        // Calculer le nombre d'ennemis attendus AVANT d'afficher la transition
        CalculateExpectedEnemies(waveNumber);

        // 1. Afficher l'écran de transition (3 secondes)
        yield return StartCoroutine(ShowWaveTransition(waveNumber));

        isInWaveTransition = false;

        // 2. ✅ MODIFIÉ : Afficher la sélection de pouvoir APRÈS la transition (sauf vague 1)
        if (waveNumber > 1 && powerSelectionManager != null)
        {
            if (debugMode)
                Debug.Log($"[LevelManagement] Affichage de la sélection de pouvoir pour vague {waveNumber}");

            waitingForPowerSelection = true;
            powerSelectionManager.ShowPowerSelection();

            // Attendre que le joueur choisisse un pouvoir
            while (powerSelectionManager.IsSelectionActive())
            {
                yield return null;
            }

            waitingForPowerSelection = false;

            if (debugMode)
                Debug.Log($"[LevelManagement] Pouvoir sélectionné");
        }

        if (debugMode)
            Debug.Log($"[LevelManagement] === DÉMARRAGE VAGUE {waveNumber} ===\n  Ennemis attendus: {expectedEnemiesThisWave}");

        // 3. Démarrer la vérification continue
        StartChecking();
    }

    /// <summary>
    /// ✅ MODIFIÉ : Affiche l'écran de transition de vague pendant X secondes
    /// APPLIQUE UN STUN à tous les ennemis pendant la transition
    /// </summary>
    private IEnumerator ShowWaveTransition(int waveNumber)
    {
        if (debugMode)
            Debug.Log($"[LevelManagement] Affichage transition vague {waveNumber}");

        // Activer le canvas de transition
        if (waveTransitionCanvas != null)
        {
            waveTransitionCanvas.SetActive(true);

            // Mettre à jour le texte
            if (waveNumberText != null)
            {
                waveNumberText.text = $"WAVE {waveNumber}";
            }
        }

        // Masquer le canvas du joueur via GameStateManager
        if (gameStateManager != null)
        {
            gameStateManager.SetWaveTransitionMode(true);
        }

        // ✅ Appliquer un stun à tous les ennemis existants + nouveaux spawns
        StartCoroutine(StunAllEnemiesDuringTransition(waveTransitionDuration));

        // Attendre la durée de la transition
        yield return new WaitForSeconds(waveTransitionDuration);

        // Désactiver le canvas de transition
        if (waveTransitionCanvas != null)
        {
            waveTransitionCanvas.SetActive(false);
        }

        // Réafficher le canvas du joueur
        if (gameStateManager != null)
        {
            gameStateManager.SetWaveTransitionMode(false);
        }

        if (debugMode)
            Debug.Log($"[LevelManagement] ✅ Transition terminée");
    }

    /// <summary>
    /// ✅ Applique un stun à tous les ennemis pendant la durée spécifiée
    /// Continue de surveiller les nouveaux spawns pendant cette période
    /// </summary>
    private IEnumerator StunAllEnemiesDuringTransition(float duration)
    {
        float elapsed = 0f;
        float checkInterval = 0.1f; // Vérifier toutes les 0.1 secondes pour les nouveaux spawns

        HashSet<GameObject> stunnedEnemies = new HashSet<GameObject>();

        if (debugMode)
            Debug.Log($"[LevelManagement] Début du stun de masse (durée: {duration}s)");

        while (elapsed < duration)
        {
            // Trouver tous les ennemis actuels
            GameObject[] currentEnemies = GameObject.FindGameObjectsWithTag(enemyTag);

            int newlyStunned = 0;

            foreach (var enemyObj in currentEnemies)
            {
                if (enemyObj == null) continue;

                // Si cet ennemi n'a pas encore été stunné
                if (!stunnedEnemies.Contains(enemyObj))
                {
                    Enemy enemy = enemyObj.GetComponent<Enemy>();
                    if (enemy != null)
                    {
                        // Appliquer le stun pour la durée restante de la transition
                        float remainingDuration = duration - elapsed;
                        enemy.Stun(remainingDuration);
                        stunnedEnemies.Add(enemyObj);
                        newlyStunned++;

                        if (debugMode)
                            Debug.Log($"[LevelManagement] Stun appliqué à '{enemyObj.name}' pour {remainingDuration:F1}s");
                    }
                }
            }

            if (debugMode && newlyStunned > 0)
                Debug.Log($"[LevelManagement] ✓ {newlyStunned} nouveau(x) ennemi(s) stunné(s)");

            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;
        }

        if (debugMode)
            Debug.Log($"[LevelManagement] ✅ Fin du stun de masse - {stunnedEnemies.Count} ennemis total stunnés");
    }

    /// <summary>
    /// Arrête la gestion des vagues
    /// </summary>
    private void StopLevelManagement()
    {
        isActive = false;
        isInWaveTransition = false;
        waitingForPowerSelection = false;
        StopChecking();
        trackedEnemies.Clear();

        // Masquer le canvas de transition
        if (waveTransitionCanvas != null)
            waveTransitionCanvas.SetActive(false);

        if (debugMode)
            Debug.Log("[LevelManagement] Arrêt de la gestion des vagues");
    }

    /// <summary>
    /// ✅ GETTER PUBLIC : Indique si on est en transition de vague (pour bloquer les spawns)
    /// </summary>
    public bool IsInWaveTransition()
    {
        return isInWaveTransition;
    }

    /// <summary>
    /// Calcule le nombre total d'ennemis attendus pour une vague donnée
    /// UTILISE LA MÊME LOGIQUE QUE SeasonalSpawnManager
    /// </summary>
    private void CalculateExpectedEnemies(int wave)
    {
        // Compter les marqueurs dans la scène (COPIE de SeasonalSpawnManager.UpdateSpawnsByLevel)
        var allTransforms = FindObjectsOfType<Transform>();

        List<Transform> halloweenMarkers;
        List<Transform> winterMarkers;

        if (useMarkerTag)
        {
            halloweenMarkers = allTransforms.Where(t => t != null && t.CompareTagSafe(markerTagHalloween)).ToList();
            winterMarkers = allTransforms.Where(t => t != null && t.CompareTagSafe(markerTagWinter)).ToList();
        }
        else
        {
            halloweenMarkers = allTransforms.Where(t => t != null && IsSpawnMarker(t, "spawnhalloween")).ToList();
            winterMarkers = allTransforms.Where(t => t != null && IsSpawnMarker(t, "spawnwinter")).ToList();
        }

        int totalHalloween = halloweenMarkers.Count;
        int totalWinter = winterMarkers.Count;

        // FIXE : 1 spawner de miniboss de chaque type
        int totalHalloweenMiniboss = 1;
        int totalWinterMiniboss = 1;

        // Déterminer combien de spawners de chaque type seront activés (LOGIQUE EXACTE de SeasonalSpawnManager)
        int targetHalloween = GetTargetHalloweenSpawners(wave, totalHalloween);
        int targetWinter = GetTargetWinterSpawners(wave, totalWinter);
        int targetHalloweenMiniboss = 1;
        int targetWinterMiniboss = 1;

        // Récupérer le nombre d'ennemis par spawner pour cette vague
        int enemiesPerHalloween = GetEnemiesPerSpawner(spawnerPrefabHalloween, wave);
        int enemiesPerWinter = GetEnemiesPerSpawner(spawnerPrefabWinter, wave);
        int enemiesPerHalloweenMiniboss = GetEnemiesPerSpawner(spawnerPrefabHalloweenMiniboss, wave);
        int enemiesPerWinterMiniboss = GetEnemiesPerSpawner(spawnerPrefabWinterMiniboss, wave);

        // Calculer le total
        int halloweenTotal = targetHalloween * enemiesPerHalloween;
        int winterTotal = targetWinter * enemiesPerWinter;
        int halloweenMinibossTotal = targetHalloweenMiniboss * enemiesPerHalloweenMiniboss;
        int winterMinibossTotal = targetWinterMiniboss * enemiesPerWinterMiniboss;

        expectedEnemiesThisWave = halloweenTotal + winterTotal + halloweenMinibossTotal + winterMinibossTotal;

        if (debugMode)
        {
            Debug.Log($"[LevelManagement] === CALCUL VAGUE {wave} ===");
            Debug.Log($"  Marqueurs totaux:");
            Debug.Log($"    - Halloween: {totalHalloween}, Winter: {totalWinter}");
            Debug.Log($"    - Halloween Miniboss: {totalHalloweenMiniboss}, Winter Miniboss: {totalWinterMiniboss}");
            Debug.Log($"  Spawners actifs pour vague {wave}:");
            Debug.Log($"    - Halloween: {targetHalloween} × {enemiesPerHalloween} = {halloweenTotal}");
            Debug.Log($"    - Winter: {targetWinter} × {enemiesPerWinter} = {winterTotal}");
            Debug.Log($"    - Halloween Miniboss: {targetHalloweenMiniboss} × {enemiesPerHalloweenMiniboss} = {halloweenMinibossTotal}");
            Debug.Log($"    - Winter Miniboss: {targetWinterMiniboss} × {enemiesPerWinterMiniboss} = {winterMinibossTotal}");
            Debug.Log($"  ★ TOTAL ATTENDU: {expectedEnemiesThisWave} ennemis");
        }
    }

    /// <summary>           
    /// Vérifie si un Transform est un marqueur spawn (par nom)
    /// COPIE de SeasonalSpawnManager.IsSpawnMarker
    /// </summary>
    private bool IsSpawnMarker(Transform t, string markerNameLower)
    {
        if (t == null) return false;
        if (useMarkerTag) return false; // géré par CompareTag
        return t.name.IndexOf(markerNameLower, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Retourne le nombre de spawners Halloween ACTIFS pour une vague
    /// LOGIQUE IDENTIQUE À SeasonalSpawnManager
    /// </summary>
    private int GetTargetHalloweenSpawners(int level, int totalHalloween)
    {
        int targetHalloween;
        
        if (level <= 1) targetHalloween = 2;
        else if (level == 2) targetHalloween = 5;
        else if (level == 3) targetHalloween = 8;
        else if (level == 4) targetHalloween = 11;
        else if (level == 5) targetHalloween = 14;
        else targetHalloween = totalHalloween; // level 6+ -> all halloween

        return Mathf.Min(targetHalloween, totalHalloween);
    }

    /// <summary>
    /// Retourne le nombre de spawners Winter ACTIFS pour une vague
    /// LOGIQUE IDENTIQUE À SeasonalSpawnManager
    /// </summary>
    private int GetTargetWinterSpawners(int level, int totalWinter)
    {
        int targetWinter = 0;
        
        if (level >= 6 && level <= 10)
        {
            switch (level)
            {
                case 6: targetWinter = 2; break;
                case 7: targetWinter = 4; break;
                case 8: targetWinter = 7; break;
                case 9: targetWinter = 10; break;
                case 10: targetWinter = 12; break;
            }
        }
        else if (level > 10)
        {
            targetWinter = totalWinter;
        }

        return Mathf.Min(targetWinter, totalWinter);
    }

    /// <summary>
    /// Retourne le nombre de spawners Miniboss ACTIFS pour une vague
    /// </summary>
    private int GetTargetMinibossSpawners(int level, int totalMiniboss, bool isHalloween)
    {
        // Les miniboss apparaissent à partir du niveau 3
        if (level < 3)
            return 0;

        // Toujours 1 miniboss de chaque type dès le niveau 3
        return 1;
    }

    /// <summary>
    /// Lit le nombre total d'ennemis qu'un spawner va générer pour une vague donnée
    /// </summary>
    private int GetEnemiesPerSpawner(GameObject spawnerPrefab, int wave)
    {
        if (spawnerPrefab == null)
            return 0;

        var spawnerContent = spawnerPrefab.GetComponent<SpawnerContent>();
        if (spawnerContent == null)
        {
            if (debugMode)
                Debug.LogWarning($"[LevelManagement] Pas de SpawnerContent sur {spawnerPrefab.name}");
            return 0;
        }

        var field = typeof(SpawnerContent).GetField("waves", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field == null)
        {
            if (debugMode)
                Debug.LogWarning($"[LevelManagement] Impossible d'accéder au champ 'waves' de SpawnerContent");
            return 0;
        }

        var waves = field.GetValue(spawnerContent) as WaveSetting[];
        
        if (waves == null || waves.Length == 0)
        {
            if (debugMode)
                Debug.LogWarning($"[LevelManagement] Aucune WaveSetting définie dans {spawnerPrefab.name}");
            return 0;
        }

        int waveIndex = wave - 1;

        if (waveIndex >= 0 && waveIndex < waves.Length)
        {
            var waveSetting = waves[waveIndex];
            return waveSetting.count0 + waveSetting.count1 + waveSetting.count2;
        }
        else if (waves.Length > 0)
        {
            var lastWave = waves[waves.Length - 1];
            int baseCount = lastWave.count0 + lastWave.count1 + lastWave.count2;
            int extraWaves = waveIndex - (waves.Length - 1);
            float growthFactor = 1.15f;
            
            return Mathf.CeilToInt(baseCount * Mathf.Pow(growthFactor, extraWaves));
        }

        return 0;
    }

    /// <summary>
    /// Commence à vérifier régulièrement le nombre d'ennemis
    /// </summary>
    private void StartChecking()
    {
        StopChecking();
        checkCoroutine = StartCoroutine(CheckEnemiesRoutine());
    }

    /// <summary>
    /// Arrête la vérification
    /// </summary>
    private void StopChecking()
    {
        if (checkCoroutine != null)
        {
            StopCoroutine(checkCoroutine);
            checkCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine qui vérifie périodiquement le nombre d'ennemis vivants ET détecte les morts
    /// </summary>
    private IEnumerator CheckEnemiesRoutine()
    {
        yield return new WaitForSeconds(checkDelay);

        while (isActive && gameStateManager != null && gameStateManager.IsPlaying())
        {
            // Récupérer tous les ennemis actuels
            GameObject[] currentEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
            currentAliveEnemies = currentEnemies != null ? currentEnemies.Length : 0;

            // Détecter les nouveaux ennemis spawnés
            int newEnemiesCount = 0;
            foreach (var enemy in currentEnemies)
            {
                if (enemy != null && !trackedEnemies.Contains(enemy))
                {
                    trackedEnemies.Add(enemy);
                    newEnemiesCount++;
                }
            }

            if (debugMode && newEnemiesCount > 0)
                Debug.Log($"[LevelManagement] ✓ {newEnemiesCount} nouveau(x) ennemi(s) détecté(s) (Total spawned: {trackedEnemies.Count})");

            // Détecter les ennemis morts
            var deadEnemies = new List<GameObject>();
            foreach (var tracked in trackedEnemies)
            {
                if (tracked == null)
                {
                    deadEnemies.Add(tracked);
                }
            }

            // Traiter les morts
            if (deadEnemies.Count > 0)
            {
                foreach (var dead in deadEnemies)
                {
                    trackedEnemies.Remove(dead);
                    enemiesKilledThisWave++;
                }

                if (debugMode)
                {
                    Debug.Log($"[LevelManagement] ☠ {deadEnemies.Count} ennemi(s) tué(s) !");
                    Debug.Log($"[LevelManagement] === STATUS VAGUE {levelData.level} ===");
                    Debug.Log($"  Ennemis tués: {enemiesKilledThisWave}/{expectedEnemiesThisWave}");
                    Debug.Log($"  Ennemis vivants: {currentAliveEnemies}");
                    Debug.Log($"  Trackés: {trackedEnemies.Count}");
                }
            }

            // Vérifier si tous les ennemis sont morts
            if (enemiesKilledThisWave >= expectedEnemiesThisWave && currentAliveEnemies == 0 && expectedEnemiesThisWave > 0)
            {
                if (debugMode)
                    Debug.Log($"[LevelManagement] ★★★ VAGUE {levelData.level} TERMINÉE ! ★★★");
                
                OnWaveCompleted();
                yield break;
            }

            yield return new WaitForSeconds(checkDelay);
        }

        checkCoroutine = null;
    }

    /// <summary>
    /// Compte le nombre d'ennemis vivants dans la scène
    /// </summary>
    private int CountAliveEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        return enemies != null ? enemies.Length : 0;
    }

    /// <summary>
    /// Appelé quand une vague est terminée
    /// </summary>
    private void OnWaveCompleted()
    {
        if (levelData == null || gameStateManager == null) return;

        int currentWave = levelData.level;

        if (debugMode)
            Debug.Log($"[LevelManagement] === VAGUE {currentWave} COMPLÉTÉE ===\n  Ennemis tués: {enemiesKilledThisWave}");
        healthObject.ActivateRandom();

        if (currentWave >= maxWave)
        {
            if (debugMode)
                Debug.Log("[LevelManagement] ★★★ VICTOIRE ! TOUTES LES VAGUES TERMINÉES ★★★");

            SpawnBoss();
            StopLevelManagement();
        }
        else
        {
            levelData.level++;
            enemiesKilledThisWave = 0;
            trackedEnemies.Clear();

            if (debugMode)
                Debug.Log($"[LevelManagement] → Passage à la vague {levelData.level}");

            // ✅ Afficher la sélection de pouvoir puis la transition AVANT de commencer la nouvelle vague
            StartCoroutine(ShowWaveTransitionAndStart(levelData.level));
        }
    }

    private void SpawnBoss()
    {
        if (bossPrefab == null || bossSpawnPoint == null)
        {
            return;
        }

        bossSpawned = true;

        Instantiate(
            bossPrefab,
            bossSpawnPoint.position,
            bossSpawnPoint.rotation
        );
    }

    /// <summary>
    /// Redémarre la vérification après un délai
    /// </summary>
    private IEnumerator RestartCheckingAfterDelay()
    {
        yield return new WaitForSeconds(checkDelay);
        StartChecking();
    }

    /// <summary>
    /// Méthode publique pour forcer la vérification immédiate
    /// </summary>
    public void ForceCheck()
    {
        if (!isActive) return;

        int enemyCount = CountAliveEnemies();
        Debug.Log($"[LevelManagement] FORCE CHECK — Vivants: {enemyCount}, Tués: {enemiesKilledThisWave}/{expectedEnemiesThisWave}, Vague: {levelData?.level}");

        if (enemyCount == 0 && enemiesKilledThisWave >= expectedEnemiesThisWave && expectedEnemiesThisWave > 0)
        {
            OnWaveCompleted();
        }
    }

    #region Debug Helper

    [ContextMenu("Debug: Count Enemies")]
    private void DebugCountEnemies()
    {
        int count = CountAliveEnemies();
        Debug.Log($"[LevelManagement] === DEBUG COUNT ===");
        Debug.Log($"  Vivants actuellement: {count}");
        Debug.Log($"  Tués cette vague: {enemiesKilledThisWave}/{expectedEnemiesThisWave}");
        Debug.Log($"  Trackés: {trackedEnemies.Count}");
    }

    [ContextMenu("Debug: Force Next Wave")]
    private void DebugForceNextWave()
    {
        if (levelData != null && levelData.level < maxWave)
        {
            levelData.level++;
            enemiesKilledThisWave = 0;
            trackedEnemies.Clear();
            CalculateExpectedEnemies(levelData.level);
            Debug.Log($"[LevelManagement] DEBUG — Forcé vague {levelData.level}, {expectedEnemiesThisWave} ennemis attendus");
        }
    }

    [ContextMenu("Debug: Show Full Status")]
    private void DebugShowFullStatus()
    {
        CalculateExpectedEnemies(levelData != null ? levelData.level : 1);
        
        Debug.Log($"[LevelManagement] === STATUS COMPLET ===");
        Debug.Log($"  Vague actuelle: {levelData?.level}");
        Debug.Log($"  Attendus: {expectedEnemiesThisWave}");
        Debug.Log($"  Tués: {enemiesKilledThisWave}");
        Debug.Log($"  Vivants: {CountAliveEnemies()}");
        Debug.Log($"  Trackés: {trackedEnemies.Count}");
    }

    #endregion
}

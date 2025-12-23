using System.Collections.Generic;
using UnityEngine;

public class HealSpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelData levelData;
    [SerializeField] private GameObject healPrefab; // Prefab du point de heal

    [Header("Spawn Positions")]
    [Tooltip("Positions possibles pour spawner les heals")]
    [SerializeField] private Transform[] possiblePositions;

    [Header("Wave-based Heal Count")]
    [Tooltip("Nombre de heals par tranche de vagues")]
    [SerializeField] private int healsForWaves1to3 = 1;
    [SerializeField] private int healsForWaves4to6 = 2;
    [SerializeField] private int healsForWaves7to9 = 3;
    [SerializeField] private int healsForWave10Plus = 4;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Liste des heals actuellement actifs
    private List<GameObject> activeHeals = new List<GameObject>();
    private int lastWave = -1;

    private void Awake()
    {
        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();

        if (levelData == null)
            Debug.LogError("[HealSpawnManager] LevelData introuvable !");

        if (healPrefab == null)
            Debug.LogError("[HealSpawnManager] HealPrefab non assigné !");

        if (possiblePositions == null || possiblePositions.Length == 0)
            Debug.LogWarning("[HealSpawnManager] Aucune position de spawn configurée !");
    }

    private void Start()
    {
        if (levelData != null)
        {
            lastWave = levelData.level;
            SpawnHealsForWave(lastWave);
        }
    }

    private void Update()
    {
        if (levelData == null) return;

        // Détecter changement de vague
        if (levelData.level != lastWave)
        {
            lastWave = levelData.level;
            SpawnHealsForWave(lastWave);
        }
    }

    /// <summary>
    /// Spawne les heals en fonction de la vague actuelle
    /// </summary>
    private void SpawnHealsForWave(int wave)
    {
        // Détruire tous les heals actifs
        ClearActiveHeals();

        // Calculer le nombre de heals à spawner
        int healCount = GetHealCountForWave(wave);

        if (debugMode)
            Debug.Log($"[HealSpawnManager] Vague {wave} : Spawn de {healCount} heal(s)");

        // Vérifier qu'on a assez de positions
        if (possiblePositions.Length == 0)
        {
            Debug.LogWarning($"[HealSpawnManager] Impossible de spawner {healCount} heals : aucune position disponible");
            return;
        }

        if (healCount > possiblePositions.Length)
        {
            Debug.LogWarning($"[HealSpawnManager] Demande de {healCount} heals mais seulement {possiblePositions.Length} positions disponibles");
            healCount = possiblePositions.Length;
        }

        // Choisir des positions aléatoires sans répétition
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < possiblePositions.Length; i++)
        {
            availableIndices.Add(i);
        }

        // Spawner les heals
        for (int i = 0; i < healCount; i++)
        {
            // Choisir une position aléatoire parmi celles disponibles
            int randomIndex = Random.Range(0, availableIndices.Count);
            int positionIndex = availableIndices[randomIndex];
            availableIndices.RemoveAt(randomIndex);

            Transform spawnPosition = possiblePositions[positionIndex];

            // Instancier le heal
            GameObject healInstance = Instantiate(healPrefab, spawnPosition.position, Quaternion.identity, transform);
            healInstance.name = $"Heal_{wave}_{i + 1}";

            // Activer le heal
            Heal healScript = healInstance.GetComponent<Heal>();
            if (healScript != null)
            {
                healScript.ActivateDirectly(); // Nouvelle méthode à ajouter dans Heal.cs
            }

            activeHeals.Add(healInstance);

            if (debugMode)
                Debug.Log($"[HealSpawnManager] Heal {i + 1}/{healCount} spawné à la position '{spawnPosition.name}'");
        }
    }

    /// <summary>
    /// Retourne le nombre de heals à spawner selon la vague
    /// </summary>
    private int GetHealCountForWave(int wave)
    {
        if (wave >= 1 && wave <= 3)
            return healsForWaves1to3;
        else if (wave >= 4 && wave <= 6)
            return healsForWaves4to6;
        else if (wave >= 7 && wave <= 9)
            return healsForWaves7to9;
        else if (wave >= 10)
            return healsForWave10Plus;

        return 1; // Fallback
    }

    /// <summary>
    /// Détruit tous les heals actifs
    /// </summary>
    private void ClearActiveHeals()
    {
        foreach (var heal in activeHeals)
        {
            if (heal != null)
                Destroy(heal);
        }

        activeHeals.Clear();

        if (debugMode)
            Debug.Log("[HealSpawnManager] Tous les heals actifs ont été détruits");
    }

    /// <summary>
    /// Appelé quand un heal est ramassé (pour le retirer de la liste)
    /// </summary>
    public void OnHealCollected(GameObject heal)
    {
        activeHeals.Remove(heal);

        if (debugMode)
            Debug.Log($"[HealSpawnManager] Heal collecté : {activeHeals.Count} restant(s)");
    }

    private void OnDestroy()
    {
        ClearActiveHeals();
    }

    #region Debug Helpers

    [ContextMenu("Debug: Show Current Heals")]
    private void DebugShowCurrentHeals()
    {
        Debug.Log($"[HealSpawnManager] === STATUS ===");
        Debug.Log($"Vague actuelle: {levelData?.level}");
        Debug.Log($"Heals actifs: {activeHeals.Count}");
        Debug.Log($"Positions disponibles: {possiblePositions.Length}");
    }

    [ContextMenu("Debug: Force Respawn Heals")]
    private void DebugForceRespawn()
    {
        if (levelData != null)
        {
            SpawnHealsForWave(levelData.level);
        }
    }

    #endregion
}
using System;
using System.Collections; // ✅ AJOUTÉ : Nécessaire pour IEnumerator
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SeasonalSpawnManager : MonoBehaviour
{
    [Header("Prefabs (glisser-déposer)")]
    public List<GameObject> halloweenPrefabs = new List<GameObject>();
    public List<GameObject> winterPrefabs = new List<GameObject>();

    [Header("Spawner prefabs (invisibles, contiennent SpawnerContent)")]
    [Tooltip("Prefab contenant SpawnerContent pour marqueurs Halloween (sera instancié invisible).")]
    [SerializeField] private GameObject spawnerPrefabHalloween = null;
    [Tooltip("Prefab contenant SpawnerContent pour marqueurs Winter (sera instancié invisible).")]
    [SerializeField] private GameObject spawnerPrefabWinter = null;

    [Header("Marqueurs")]
    [Tooltip("Si vrai, on recherche les marqueurs par tag plutôt que par nom.")]
    [SerializeField] private bool useMarkerTag = false;
    [Tooltip("Tag des marqueurs Halloween (si useMarkerTag = true).")]
    [SerializeField] private string markerTagHalloween = "SpawnHalloween";
    [Tooltip("Tag des marqueurs Winter (si useMarkerTag = true).")]
    [SerializeField] private string markerTagWinter = "SpawnWinter";

    [Header("Placement au sol (utilisé pour le snap)")]
    [Tooltip("Objet parent contenant les colliders du sol (optionnel).")]
    [SerializeField] private GameObject groundRoot = null;
    [Tooltip("LayerMask utilisé en fallback pour détecter le sol.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Hauteur maximale de départ pour les raycasts vers le bas (m).")]
    [SerializeField] private float maxRayHeight = 50f;
    [Tooltip("Offset vertical final appliqué après snap (m).")]
    [SerializeField] private float groundSnapOffset = 0f;

    [Header("Debug")]
    [Tooltip("Active logs détaillés pour diagnostiquer les spawns.")]
    [SerializeField] private bool debugMode = false;

    // clé PlayerPrefs par scène
    private string prefsKey => $"SeasonalSpawn_Activated_{SceneManager.GetActiveScene().name}";

    // saved entries format: prefix|hierPath|x,y,z|prefabName
    private HashSet<string> activatedKeys = new HashSet<string>(StringComparer.Ordinal);

    private LevelData levelData;
    private int lastLevel = -1;

    private void Awake()
    {
        LoadActivatedKeys();

        // rendre invisibles tous les marqueurs présents (ne pas les détruire)
        var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>();
        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            if (IsSpawnMarker(t, "spawnhalloween") || IsSpawnMarker(t, "spawnwinter"))
            {
                HideMarkerVisuals(t.gameObject);
            }
        }

        // restaurer les spawns déjà activés (persistés)
        RestoreActivatedMarkers();

        // Debug: lister les marqueurs présents dans la scène
        DebugListMarkers();
    }

    private void Start()
    {
        levelData = FindObjectOfType<LevelData>();
        if (levelData == null)
        {
            Debug.LogWarning("[SeasonalSpawnManager] LevelData introuvable — aucun spawn progressif.");
            return;
        }

        lastLevel = levelData.level;
        UpdateSpawnsByLevel(lastLevel);
    }

    private void Update()
    {
        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();
        if (levelData == null) return;

        if (levelData.level != lastLevel)
        {
            lastLevel = levelData.level;
            UpdateSpawnsByLevel(lastLevel);
            if (debugMode) Debug.Log($"[SeasonalSpawnManager] Level -> {lastLevel}");
        }
    }

    private void UpdateSpawnsByLevel(int level)
    {
        var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>();

        // collect markers: either by tag (configurable) or by name contains
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

        if (debugMode) Debug.Log($"[SeasonalSpawnManager] Markers found - Halloween:{totalHalloween}, Winter:{totalWinter}");

        // Targets according to requested mapping
        int targetHalloween;
        if (level <= 1) targetHalloween = 2;
        else if (level == 2) targetHalloween = 5;
        else if (level == 3) targetHalloween = 8;
        else if (level == 4) targetHalloween = 11;
        else if (level == 5) targetHalloween = 14;
        else targetHalloween = totalHalloween; // level 6+ -> all halloween

        targetHalloween = Mathf.Min(targetHalloween, totalHalloween);

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

        targetWinter = Mathf.Min(targetWinter, totalWinter);

        if (debugMode) Debug.Log($"[SeasonalSpawnManager] Targets - Halloween:{targetHalloween}, Winter:{targetWinter} (level {level})");

        var spawned = UnityEngine.Object.FindObjectsOfType<SpawnedSeasonal>();
        int currentHalloween = spawned.Count(s => s != null && s.activated && s.category == SpawnedSeasonal.Category.Halloween);
        int currentWinter = spawned.Count(s => s != null && s.activated && s.category == SpawnedSeasonal.Category.Winter);

        if (debugMode)
        {
            int persistedH = activatedKeys.Count(k => k.StartsWith("H|"));
            int persistedW = activatedKeys.Count(k => k.StartsWith("W|"));
            Debug.Log($"[SeasonalSpawnManager] Persisted keys H:{persistedH}, W:{persistedW} (scene spawned H:{currentHalloween}, W:{currentWinter})");
        }

        if (currentHalloween < targetHalloween && totalHalloween > 0)
        {
            var inactive = halloweenMarkers.Where(t => !activatedKeys.Contains(MakeSavedKeyPrefix("H", t))).ToList();
            int need = Mathf.Min(targetHalloween - currentHalloween, inactive.Count);
            ActivateRandomMarkers(inactive, need, halloweenPrefabs, "H");
        }

        if (currentWinter < targetWinter && totalWinter > 0)
        {
            var inactive = winterMarkers.Where(t => !activatedKeys.Contains(MakeSavedKeyPrefix("W", t))).ToList();
            int need = Mathf.Min(targetWinter - currentWinter, inactive.Count);
            ActivateRandomMarkers(inactive, need, winterPrefabs, "W");
        }

        SaveActivatedKeys();
    }

    // activation : replace marker by instantiated object (inspired by RockSpawn)
    // -> instancie le visuel + un spawner invisible (selon saison) contenant SpawnerContent initialisé
    private void ActivateRandomMarkers(List<Transform> candidates, int numberToActivate, List<GameObject> prefabPool, string categoryPrefix)
    {
        if (numberToActivate <= 0) return;
        if (candidates == null || candidates.Count == 0) return;
        if (prefabPool == null || prefabPool.Count == 0)
        {
            Debug.LogWarning($"[SeasonalSpawnManager] Aucun prefab dans le pool pour la catégorie {categoryPrefix}. Activation ignorée.");
            return;
        }

        var rnd = new System.Random();
        candidates = candidates.OrderBy(_ => rnd.Next()).ToList();

        if (debugMode) Debug.Log($"[SeasonalSpawnManager] Activating {numberToActivate} of {candidates.Count} candidates for {categoryPrefix}");

        for (int i = 0; i < Math.Min(numberToActivate, candidates.Count); i++)
        {
            var t = candidates[i];

            var prefab = prefabPool[UnityEngine.Random.Range(0, prefabPool.Count)];
            if (prefab == null) continue;

            // skip if marker already converted (we check nearby spawned objects)
            var existingMarkerComp = t.GetComponent<SpawnedSeasonal>();
            if (existingMarkerComp != null && existingMarkerComp.activated) continue;

            if (debugMode) Debug.Log($"[SeasonalSpawnManager] Instantiating prefab '{prefab.name}' at marker '{t.name}'");

            Vector3 spawnPos = t.position + Vector3.up * 1f;

            // compute rotation: try to align to ground normal + apply random yaw
            Quaternion finalRot;
            if (TryGetGroundHitUnderPosition(t.position, out RaycastHit groundHit))
            {
                Quaternion alignToNormal = Quaternion.FromToRotation(Vector3.up, groundHit.normal);
                Quaternion randomYaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                finalRot = alignToNormal * randomYaw * prefab.transform.rotation;
            }
            else
            {
                // fallback: keep marker yaw but add random yaw
                Quaternion yaw = Quaternion.Euler(0f, t.rotation.eulerAngles.y, 0f);
                Quaternion randomYaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                finalRot = yaw * randomYaw * prefab.transform.rotation;
            }

            // instantiate visible prefab at marker's parent (we will remove the marker like RockSpawn)
            var newObj = Instantiate(prefab, spawnPos, finalRot, t.parent);
            newObj.transform.localScale = prefab.transform.localScale;
            newObj.name = $"seasonal.{categoryPrefix}.{prefab.name}";

            // Ensure active and visuals enabled
            if (!newObj.activeSelf) newObj.SetActive(true);
            ShowObjectVisuals(newObj);

            // snap bottom of prefab to real ground under marker
            if (TryGetGroundYUnderPosition(t.position, out float groundY))
            {
                float lowestWorldY = GetLowestWorldY(newObj);
                if (!float.IsPositiveInfinity(lowestWorldY))
                {
                    float diff = lowestWorldY - groundY;
                    newObj.transform.position += Vector3.down * diff;
                    newObj.transform.position += Vector3.up * groundSnapOffset;
                }
                else
                {
                    newObj.transform.position = new Vector3(newObj.transform.position.x, groundY + groundSnapOffset, newObj.transform.position.z);
                }
            }
            else
            {
                Vector3 rayStart = t.position + Vector3.up * maxRayHeight;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxRayHeight * 2f, groundMask))
                {
                    float lowestWorldY = GetLowestWorldY(newObj);
                    if (!float.IsPositiveInfinity(lowestWorldY))
                    {
                        float diff = lowestWorldY - hit.point.y;
                        newObj.transform.position += Vector3.down * diff;
                        newObj.transform.position += Vector3.up * groundSnapOffset;
                    }
                    else
                    {
                        newObj.transform.position = new Vector3(newObj.transform.position.x, hit.point.y + groundSnapOffset, newObj.transform.position.z);
                    }
                }
            }

            ShowObjectVisuals(newObj);
             
            // --- Instantiate the invisible spawner prefab (child of visible) ---
            // Le spawner prefab contient déjà SpawnerContent avec mobPrefabs[] configurés dans l'inspector
            GameObject spawnerPrefab = categoryPrefix == "H" ? spawnerPrefabHalloween : spawnerPrefabWinter;
            if (spawnerPrefab != null)
            {
                try
                {
                    var spawnerInstance = Instantiate(spawnerPrefab, newObj.transform);
                    spawnerInstance.name = spawnerPrefab.name;
                    spawnerInstance.transform.localPosition = Vector3.zero;
                    spawnerInstance.transform.localRotation = Quaternion.identity;

                    // ensure invisible visuals only (renderers/colliders disabled)
                    HideMarkerVisuals(spawnerInstance);

                    // Le SpawnerContent est déjà sur le prefab avec ses mobPrefabs[] configurés
                    // -> PAS besoin d'appeler Initialize() — on vérifie juste qu'il est présent
                    var spComp = spawnerInstance.GetComponent<SpawnerContent>();
                    if (spComp == null)
                    {
                        Debug.LogWarning($"[SeasonalSpawnManager] Prefab spawner '{spawnerPrefab.name}' ne contient pas de SpawnerContent — ajout automatique (mais mobPrefabs vides).");
                        spComp = spawnerInstance.AddComponent<SpawnerContent>();
                    }

                    if (debugMode) 
                        Debug.Log($"[SeasonalSpawnManager] Spawner '{spawnerInstance.name}' instancié — SpawnerContent utilise ses propres mobPrefabs (configurés dans prefab).");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SeasonalSpawnManager] Erreur en créant spawner inner pour '{newObj.name}': {ex.Message}");
                }
            }
            else
            {
                if (debugMode) Debug.Log($"[SeasonalSpawnManager] Aucun spawnerPrefab défini pour la saison {categoryPrefix}.");
            }

            // add marker component on the new object (same approach as RockSpawn)
            var spawnedComp = newObj.AddComponent<SpawnedSeasonal>();
            spawnedComp.activated = true;
            spawnedComp.prefabName = prefab.name;
            spawnedComp.category = categoryPrefix == "H" ? SpawnedSeasonal.Category.Halloween : SpawnedSeasonal.Category.Winter;

            // persist key using the new object's hierarchy path (so Restore can find it later)
            string key = MakeSavedKey(categoryPrefix, newObj.transform, prefab.name);
            activatedKeys.Add(key);

            // destroy the original marker cube
            UnityEngine.Object.Destroy(t.gameObject);
        }
    }

    // Choisit jusqu'à 3 prefabs distincts dans la pool (retourne array length 3, null si manque)
    private GameObject[] ChooseThreePrefabs(List<GameObject> pool)
    {
        var result = new GameObject[3];
        if (pool == null || pool.Count == 0) return result;
        var indices = Enumerable.Range(0, pool.Count).ToList();
        var rnd = new System.Random();
        for (int i = 0; i < 3; i++)
        {
            if (indices.Count == 0) break;
            int pick = indices[rnd.Next(indices.Count)];
            result[i] = pool[pick];
            indices.Remove(pick);
        }
        return result;
    }

    // create saved key including prefab name
    private string MakeSavedKey(string prefix, Transform t, string prefabName)
    {
        string path = GetHierarchyPath(t);
        Vector3 p = t.position;
        return $"{prefix}|{path}|{p.x:F3},{p.y:F3},{p.z:F3}|{prefabName}";
    }

    // quick prefix-only key for lookup before deciding prefab (used to check if marker already activated)
    private string MakeSavedKeyPrefix(string prefix, Transform t)
    {
        string path = GetHierarchyPath(t);
        Vector3 p = t.position;
        return $"{prefix}|{path}|{p.x:F3},{p.y:F3},{p.z:F3}";
    }

    private void SaveActivatedKeys()
    {
        try
        {
            var arr = activatedKeys.ToArray();
            string joined = string.Join(";", arr);
            PlayerPrefs.SetString(prefsKey, joined);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SeasonalSpawnManager] Erreur SaveActivatedKeys: {e}");
        }
    }

    private void LoadActivatedKeys()
    {
        activatedKeys.Clear();
        if (!PlayerPrefs.HasKey(prefsKey)) return;
        string raw = PlayerPrefs.GetString(prefsKey);
        if (string.IsNullOrEmpty(raw)) return;
        var arr = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in arr) activatedKeys.Add(s);
    }

    // Hide renderer/collider of marker so it's not visible in scene (but keep GameObject alive)
    private void HideMarkerVisuals(GameObject go)
    {
        if (go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    // Ensure instantiated object visuals are enabled
    private void ShowObjectVisuals(GameObject go)
    {
        if (go == null) return;
        if (!go.activeSelf) go.SetActive(true);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = true;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = true;
    }

    private bool IsSpawnMarker(Transform t, string markerNameLower)
    {
        if (t == null) return false;
        if (useMarkerTag)
        {
            // handled ailleurs; garder le fallback
            return false;
        }
        return t.name.IndexOf(markerNameLower, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private float GetLowestWorldY(GameObject obj)
    {
        if (obj == null) return float.PositiveInfinity;
        float lowest = float.PositiveInfinity;
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            lowest = Mathf.Min(lowest, r.bounds.min.y);
        }
        if (lowest != float.PositiveInfinity) return lowest;

        var colliders = obj.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c == null) continue;
            lowest = Mathf.Min(lowest, c.bounds.min.y);
        }
        return lowest;
    }

    private bool TryGetGroundYUnderPosition(Vector3 position, out float groundY)
    {
        groundY = 0f;
        Vector3 rayStart = position + Vector3.up * maxRayHeight;
        float maxDist = maxRayHeight * 2f;

        if (groundRoot != null)
        {
            var hits = Physics.RaycastAll(rayStart, Vector3.down, maxDist);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform == groundRoot.transform || h.collider.transform.IsChildOf(groundRoot.transform))
                {
                    groundY = h.point.y;
                    return true;
                }
            }
        }

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hitMask, maxDist, groundMask))
        {
            groundY = hitMask.point.y;
            return true;
        }

        if (Terrain.activeTerrain != null)
        {
            groundY = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.GetPosition().y;
            return true;
        }

        return false;
    }

    // nouvelle méthode : obtient RaycastHit (point + normale) sous une position, priorité groundRoot -> groundMask -> Terrain
    private bool TryGetGroundHitUnderPosition(Vector3 position, out RaycastHit hit)
    {
        hit = default;
        Vector3 rayStart = position + Vector3.up * maxRayHeight;
        float maxDist = maxRayHeight * 2f;

        if (groundRoot != null)
        {
            var hits = Physics.RaycastAll(rayStart, Vector3.down, maxDist);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform == groundRoot.transform || h.collider.transform.IsChildOf(groundRoot.transform))
                {
                    hit = h;
                    return true;
                }
            }
        }

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit maskHit, maxDist, groundMask))
        {
            hit = maskHit;
            return true;
        }

        if (Terrain.activeTerrain != null)
        {
            RaycastHit h = new RaycastHit();
            h.point = new Vector3(position.x, Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.GetPosition().y, position.z);
            h.normal = Vector3.up;
            hit = h;
            return true;
        }

        return false;
    }

    // restore activated markers from PlayerPrefs: instantiate saved prefab as child of marker
    private void RestoreActivatedMarkers()
    {
        if (activatedKeys.Count == 0) return;

        if (debugMode) Debug.Log($"[SeasonalSpawnManager] Restoring {activatedKeys.Count} activated entries.");

        bool removedAny = false;

        foreach (var saved in activatedKeys.ToList())
        {
            try
            {
                // parse saved key: prefix|path|x,y,z|prefabName
                var parts = saved.Split(new[] { '|' }, StringSplitOptions.None);
                if (parts.Length < 3) continue;
                string prefix = parts[0];
                string path = parts[1];
                string prefabName = parts.Length >= 4 ? parts[3] : null;

                var marker = FindTransformByHierarchyPath(path);
                if (marker == null)
                {
                    if (debugMode) Debug.LogWarning($"[SeasonalSpawnManager] Marker not found for saved path: {path} — removing saved entry");
                    activatedKeys.Remove(saved); // nettoie l'entrée obsolète
                    removedAny = true;
                    continue;
                }

                // skip if already has SpawnedSeasonal activated
                var existing = marker.GetComponent<SpawnedSeasonal>();
                if (existing != null && existing.activated) continue;

                // find prefab by name in corresponding pool
                GameObject prefab = null;
                if (!string.IsNullOrEmpty(prefabName))
                {
                    prefab = (prefix == "H") ? halloweenPrefabs.FirstOrDefault(p => p != null && p.name == prefabName)
                                             : winterPrefabs.FirstOrDefault(p => p != null && p.name == prefabName);
                }
                // fallback choose random from pool
                if (prefab == null)
                {
                    prefab = (prefix == "H") ? (halloweenPrefabs.Count > 0 ? halloweenPrefabs[UnityEngine.Random.Range(0, halloweenPrefabs.Count)] : null)
                                              : (winterPrefabs.Count > 0 ? winterPrefabs[UnityEngine.Random.Range(0, winterPrefabs.Count)] : null);
                }
                if (prefab == null) continue;

                Vector3 spawnPos = marker.position + Vector3.up * 1f;

                // compute rotation with ground normal + random yaw when possible
                Quaternion finalRot;
                if (TryGetGroundHitUnderPosition(marker.position, out RaycastHit groundHit))
                {
                    Quaternion alignToNormal = Quaternion.FromToRotation(Vector3.up, groundHit.normal);
                    Quaternion randomYaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    finalRot = alignToNormal * randomYaw * prefab.transform.rotation;
                }
                else
                {
                    Quaternion yaw = Quaternion.Euler(0f, marker.rotation.eulerAngles.y, 0f);
                    Quaternion randomYaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    finalRot = yaw * randomYaw * prefab.transform.rotation;
                }

                var newObj = Instantiate(prefab, spawnPos, finalRot, marker);
                newObj.transform.localScale = prefab.transform.localScale;
                newObj.name = $"seasonal.{prefix}.{prefab.name}";

                // snap
                if (TryGetGroundYUnderPosition(marker.position, out float groundY))
                {
                    float lowestWorldY = GetLowestWorldY(newObj);
                    if (!float.IsPositiveInfinity(lowestWorldY))
                    {
                        float diff = lowestWorldY - groundY;
                        newObj.transform.position += Vector3.down * diff;
                        newObj.transform.position += Vector3.up * groundSnapOffset;
                    }
                    else
                    {
                        newObj.transform.position = new Vector3(newObj.transform.position.x, groundY + groundSnapOffset, newObj.transform.position.z);
                    }
                }

                ShowObjectVisuals(newObj);

                var markerComp = marker.GetComponent<SpawnedSeasonal>();
                if (markerComp == null)
                    markerComp = marker.gameObject.AddComponent<SpawnedSeasonal>();
                markerComp.activated = true;
                markerComp.prefabName = prefab.name;
                markerComp.category = prefix == "H" ? SpawnedSeasonal.Category.Halloween : SpawnedSeasonal.Category.Winter;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SeasonalSpawnManager] Erreur RestoreActivatedMarkers pour '{saved}': {ex}");
            }
        }

        if (removedAny)
        {
            SaveActivatedKeys();
        }
    }

    // recherche d'un Transform par chemin hiérarchique (stable)
    private Transform FindTransformByHierarchyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        Transform cur = null;
        // find root(s) in scene
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        // find matching root
        foreach (var root in roots)
        {
            if (root.name == parts[0])
            {
                cur = root.transform;
                break;
            }
        }
        if (cur == null) return null;
        for (int i = 1; i < parts.Length; i++)
        {
            var child = cur.Find(parts[i]);
            if (child == null) return null;
            cur = child;
        }
        return cur;
    }

    private string GetHierarchyPath(Transform t)
    {
        if (t == null) return string.Empty;
        var parts = new List<string>();
        var cur = t;
        while (cur != null)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    public void ClearSavedActivated()
    {
        activatedKeys.Clear();
        PlayerPrefs.DeleteKey(prefsKey);
        Debug.Log("[SeasonalSpawnManager] Données sauvegardées effacées.");
    }

    /// <summary>
    /// ✅ Reset complet du système de spawn (spawners saisonniers + miniboss)
    /// </summary>
    public void ResetSpawns()
    {
        if (debugMode) Debug.Log("[SeasonalSpawnManager] === RESET COMPLET DES SPAWNS ===");

        // 1. Effacer les données sauvegardées dans PlayerPrefs
        activatedKeys.Clear();
        PlayerPrefs.DeleteKey(prefsKey);
        PlayerPrefs.Save();

        // 2. Détruire tous les objets spawnés saisonniers (visuels + spawners)
        var spawnedObjects = FindObjectsOfType<SpawnedSeasonal>();
        int seasonalCount = 0;
        foreach (var spawned in spawnedObjects)
        {
            if (spawned != null && spawned.gameObject != null)
            {
                Destroy(spawned.gameObject);
                seasonalCount++;
            }
        }

        // 3. ✅ MODIFIÉ : Reset des miniboss via coroutine (attendre que LevelData soit à jour)
        StartCoroutine(ResetMinibossSpawnersDelayed());

        // 4. Réinitialiser le tracker de niveau
        lastLevel = -1; // Forcer UpdateSpawnsByLevel à se relancer

        if (debugMode) 
            Debug.Log($"[SeasonalSpawnManager] ✅ Reset terminé : {seasonalCount} objets détruits, miniboss en cours de reset...");
    }

    /// <summary>
    /// ✅ NOUVEAU : Reset les spawners de miniboss après un délai (pour que LevelData soit prêt)
    /// </summary>
    private IEnumerator ResetMinibossSpawnersDelayed()
    {
        // Attendre une frame pour que LevelData soit à jour
        yield return null;

        int resetCount = 0;

        // Trouver tous les SpawnerContent de type Miniboss dans la scène
        SpawnerContent[] allSpawners = FindObjectsOfType<SpawnerContent>();
        
        foreach (var spawner in allSpawners)
        {
            if (spawner == null)
                continue;

            // Vérifier si c'est un spawner de miniboss
            if (spawner.Type == SpawnerType.Miniboss)
            {
                spawner.ResetSpawner();
                resetCount++;
                
                if (debugMode)
                    Debug.Log($"[SeasonalSpawnManager] 🔄 Miniboss spawner réinitialisé : {spawner.gameObject.name}");
            }
        }

        if (debugMode)
            Debug.Log($"[SeasonalSpawnManager] ✅ {resetCount} spawner(s) de miniboss réinitialisés");
    }

    // appelez ceci depuis Awake() pour tracer ce qui existe dans la scène
    private void DebugListMarkers()
    {
        var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>();
        Debug.Log($"[SeasonalSpawnManager] total transforms: {allTransforms.Length}");

        var halloween = new List<string>();
        var winter = new List<string>();

        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            bool isH = useMarkerTag ? t.CompareTagSafe(markerTagHalloween) : IsSpawnMarker(t, "spawnhalloween");
            bool isW = useMarkerTag ? t.CompareTagSafe(markerTagWinter) : IsSpawnMarker(t, "spawnwinter");
            if (isH) halloween.Add($"{GetHierarchyPath(t)} @ {t.position:F3}");
            if (isW) winter.Add($"{GetHierarchyPath(t)} @ {t.position:F3}");
        }

        Debug.Log($"[SeasonalSpawnManager] Halloween markers ({halloween.Count}): {string.Join("; ", halloween.Take(20))}");
        Debug.Log($"[SeasonalSpawnManager] Winter markers ({winter.Count}): {string.Join("; ", winter.Take(20))}");
    }
}

// Extension helper pour CompareTag sans exception
public static class TransformExtensions
{
    public static bool CompareTagSafe(this Transform t, string tag)
    {
        if (t == null) return false;
        if (string.IsNullOrEmpty(tag)) return false;
        try
        {
            return t.CompareTag(tag);
        }
        catch
        {
            return false;
        }
    }
}
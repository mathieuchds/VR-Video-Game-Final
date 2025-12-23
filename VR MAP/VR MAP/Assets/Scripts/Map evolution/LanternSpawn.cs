using System;
using System.Collections.Generic;
using UnityEngine;

public class LanternSpawn : MonoBehaviour
{
    [Header("Objet parent contenant les lanternes")]
    [Tooltip("Tous les enfants de cet objet seront remplacés selon le niveau")]
    [SerializeField] private GameObject lanternsParent = null;

    [Header("Prefabs (glisser-déposer)")]
    public List<GameObject> normalPrefabs = new List<GameObject>();
    public List<GameObject> halloweenPrefabs = new List<GameObject>();
    public List<GameObject> christmasPrefabs = new List<GameObject>();

    [Header("Placement au sol")]
    [Tooltip("Objet parent contenant les colliders du sol (optionnel)")]
    [SerializeField] private GameObject groundRoot = null;
    [Tooltip("Couches considérées comme sol pour le raycast (fallback)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Hauteur maximale depuis laquelle on cast vers le bas pour trouver le sol (m)")]
    [SerializeField] private float maxRayHeight = 50f;
    [Tooltip("Offset vertical après snap (ajustement visuel)")]
    [SerializeField] private float groundSnapOffset = 0f;
    [Tooltip("Enfoncement vertical pour réduire l'effet 'flottant' (mètres)")]
    [SerializeField] private float groundPenetration = 0.05f;

    [Header("Ajustement spécifique lanternes")]
    [Tooltip("Décalage supplémentaire vers le bas appliqué après le snap au sol (en mètres).")]
    [SerializeField] private float additionalDownOffset = 0.0f;

    private LevelData levelData;
    private int lastLevel = -1;

    private void Start()
    {
        if (lanternsParent == null)
        {
            Debug.LogWarning("[LanternSpawn] Aucun objet parent assigné. Assignez 'lanternsParent' dans l'inspecteur.");
            return;
        }

        levelData = FindObjectOfType<LevelData>();
        if (levelData == null)
        {
            Debug.LogWarning("[LanternSpawn] LevelData introuvable dans la scène. Remplacement des lanternes ignoré.");
            return;
        }

        lastLevel = levelData.level;
        ReplaceLanternsByLevel(lastLevel);
    }

    private void Update()
    {
        if (lanternsParent == null) return;

        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();
        if (levelData == null) return;

        if (levelData.level != lastLevel)
        {
            lastLevel = levelData.level;
            ReplaceLanternsByLevel(lastLevel);
            Debug.Log($"[LanternSpawn] Niveau changé -> {lastLevel}, lanternes mises à jour.");
        }
    }

    private void ReplaceLanternsByLevel(int level)
    {
        if (lanternsParent == null)
        {
            Debug.LogWarning("[LanternSpawn] lanternsParent est null, impossible de remplacer les lanternes.");
            return;
        }

        // Collecter tous les enfants directs du parent
        List<Transform> childrenToReplace = new List<Transform>();
        for (int i = 0; i < lanternsParent.transform.childCount; i++)
        {
            Transform child = lanternsParent.transform.GetChild(i);
            if (child != null)
            {
                childrenToReplace.Add(child);
            }
        }

        if (childrenToReplace.Count == 0)
        {
            Debug.Log($"[LanternSpawn] Aucun enfant trouvé dans '{lanternsParent.name}'.");
            return;
        }

        Debug.Log($"[LanternSpawn] Remplacement de {childrenToReplace.Count} lanternes dans '{lanternsParent.name}' pour le niveau {level}.");

        foreach (var child in childrenToReplace)
        {
            if (child == null) continue;

            SpawnedLantern existingMarker = child.GetComponent<SpawnedLantern>();
            var selection = SelectReplacementPrefab(level);
            GameObject replacementPrefab = selection.prefab;
            SpawnedLantern.Category desiredCategory = selection.category;

            if (replacementPrefab == null)
            {
                Debug.LogWarning($"[LanternSpawn] Aucun prefab disponible pour la catégorie {desiredCategory} au niveau {level}.");
                continue;
            }

            if (existingMarker != null && existingMarker.category == desiredCategory)
            {
                continue;
            }

            Vector3 originalPos = child.position;
            Quaternion originalRot = child.rotation;
            int siblingIndex = child.GetSiblingIndex();

            bool groundFound = TryGetGroundYUnderPosition(originalPos, out float groundY);

            Vector3 spawnPos = originalPos + Vector3.up * 1f;
            Quaternion yaw = Quaternion.Euler(0f, originalRot.eulerAngles.y, 0f);
            Quaternion prefabRot = replacementPrefab.transform.rotation;
            Quaternion finalRot = yaw * prefabRot;

            var newObj = Instantiate(replacementPrefab, spawnPos, finalRot, lanternsParent.transform);
            newObj.transform.localScale = replacementPrefab.transform.localScale;
            newObj.name = $"spawned.{desiredCategory}.{replacementPrefab.name}";

            newObj.transform.SetSiblingIndex(siblingIndex);

            float lowestWorldY = GetLowestWorldY(newObj);

            bool snapped = false;
            if (groundFound)
            {
                float difference = lowestWorldY - groundY;
                newObj.transform.position += Vector3.down * difference;
                newObj.transform.position += Vector3.up * groundSnapOffset;
                snapped = true;
            }
            else
            {
                Vector3 rayStart = originalPos + Vector3.up * maxRayHeight;
                float maxDist = maxRayHeight * 2f;
                RaycastHit chosenHit = new RaycastHit();

                if (groundRoot != null)
                {
                    var hits = Physics.RaycastAll(rayStart, Vector3.down, maxDist);
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits)
                    {
                        if (h.collider == null) continue;
                        if (h.collider.transform == groundRoot.transform || h.collider.transform.IsChildOf(groundRoot.transform))
                        {
                            chosenHit = h;
                            snapped = true;
                            break;
                        }
                    }
                }

                if (!snapped)
                {
                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hitMask, maxDist, groundMask))
                    {
                        chosenHit = hitMask;
                        snapped = true;
                    }
                    else if (Terrain.activeTerrain != null)
                    {
                        float terrainY = Terrain.activeTerrain.SampleHeight(originalPos) + Terrain.activeTerrain.GetPosition().y;
                        float diff = lowestWorldY - terrainY;
                        newObj.transform.position += Vector3.down * diff;
                        newObj.transform.position += Vector3.up * groundSnapOffset;
                        snapped = true;
                    }
                }

                if (snapped && chosenHit.collider != null)
                {
                    float diff = lowestWorldY - chosenHit.point.y;
                    newObj.transform.position += Vector3.down * diff;
                    newObj.transform.position += Vector3.up * groundSnapOffset;
                }
            }

            // Appliquer les décalages supplémentaires
            if (additionalDownOffset > 0f)
            {
                newObj.transform.position += Vector3.down * additionalDownOffset;
            }

            if (groundPenetration > 0f)
            {
                newObj.transform.position += Vector3.down * groundPenetration;
            }

            if (!snapped)
            {
                Debug.LogWarning($"[LanternSpawn] Aucun sol détecté sous '{replacementPrefab.name}'. Assigne 'groundRoot' ou règle 'groundMask'.");
            }

            // Marquer comme spawned
            var marker = newObj.AddComponent<SpawnedLantern>();
            marker.category = desiredCategory;

            Destroy(child.gameObject);
        }
    }

    private float GetLowestWorldY(GameObject obj)
    {
        if (obj == null) return float.MaxValue;

        float lowestWorldY = float.MaxValue;
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            lowestWorldY = Mathf.Min(lowestWorldY, r.bounds.min.y);
        }

        if (lowestWorldY == float.MaxValue)
        {
            var colliders = obj.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                if (c == null) continue;
                lowestWorldY = Mathf.Min(lowestWorldY, c.bounds.min.y);
            }
        }

        if (lowestWorldY == float.MaxValue)
        {
            lowestWorldY = obj.transform.position.y;
        }

        return lowestWorldY;
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

    private (GameObject prefab, SpawnedLantern.Category category) SelectReplacementPrefab(int level)
    {
        float rand = UnityEngine.Random.value;

        if (level >= 1 && level <= 5)
        {
            float halloweenProb = Mathf.Clamp01((level - 1f) / (5f - 1f));
            if (rand < halloweenProb)
                return (PickRandom(halloweenPrefabs), SpawnedLantern.Category.Halloween);
            else
                return (PickRandom(normalPrefabs), SpawnedLantern.Category.Normal);
        }

        if (level >= 6 && level <= 10)
        {
            float winterProb = Mathf.Clamp01((level - 5f) / (10f - 5f));
            if (rand < winterProb)
                return (PickRandom(christmasPrefabs), SpawnedLantern.Category.Christmas);
            else
                return (PickRandom(halloweenPrefabs), SpawnedLantern.Category.Halloween);
        }

        return (PickRandom(normalPrefabs), SpawnedLantern.Category.Normal);
    }

    private GameObject PickRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        int idx = UnityEngine.Random.Range(0, list.Count);
        return list[idx];
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class RockSpawn : MonoBehaviour
{
    [Header("Objet parent contenant les rochers")]
    [Tooltip("Tous les enfants de cet objet seront remplacés selon le niveau")]
    [SerializeField] private GameObject rocksParent = null;

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

    [Header("Ajustement spécifique rochers")]
    [Tooltip("Décalage supplémentaire vers le bas appliqué après le snap au sol (en mètres).")]
    [SerializeField] private float additionalDownOffset = 0.15f;

    private LevelData levelData;
    private int lastLevel = -1;

    private void Start()
    {
        if (rocksParent == null)
        {
            Debug.LogWarning("[RockSpawn] Aucun objet parent assigné. Assignez 'rocksParent' dans l'inspecteur.");
            return;
        }

        levelData = FindObjectOfType<LevelData>();
        if (levelData == null)
        {
            Debug.LogWarning("[RockSpawn] LevelData introuvable dans la scène. Remplacement des rochers ignoré.");
            return;
        }

        lastLevel = levelData.level;
        ReplaceRocksByLevel(lastLevel);
    }

    private void Update()
    {
        if (rocksParent == null) return;

        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();
        if (levelData == null) return;

        if (levelData.level != lastLevel)
        {
            lastLevel = levelData.level;
            ReplaceRocksByLevel(lastLevel);
            Debug.Log($"[RockSpawn] Niveau changé -> {lastLevel}, rochers mis à jour.");
        }
    }

    private void ReplaceRocksByLevel(int level)
    {
        if (rocksParent == null)
        {
            Debug.LogWarning("[RockSpawn] rocksParent est null, impossible de remplacer les rochers.");
            return;
        }

        // Collecter tous les enfants directs du parent
        List<Transform> childrenToReplace = new List<Transform>();
        for (int i = 0; i < rocksParent.transform.childCount; i++)
        {
            Transform child = rocksParent.transform.GetChild(i);
            if (child != null)
            {
                childrenToReplace.Add(child);
            }
        }

        if (childrenToReplace.Count == 0)
        {
            Debug.Log($"[RockSpawn] Aucun enfant trouvé dans '{rocksParent.name}'.");
            return;
        }

        Debug.Log($"[RockSpawn] Remplacement de {childrenToReplace.Count} rochers dans '{rocksParent.name}' pour le niveau {level}.");

        foreach (var child in childrenToReplace)
        {
            if (child == null) continue;

            // Vérifier si déjà remplacé avec la bonne catégorie
            SpawnedRock existingMarker = child.GetComponent<SpawnedRock>();
            var selection = SelectReplacementPrefab(level);
            GameObject replacementPrefab = selection.prefab;
            SpawnedRock.Category desiredCategory = selection.category;

            if (replacementPrefab == null)
            {
                Debug.LogWarning($"[RockSpawn] Aucun prefab disponible pour la catégorie {desiredCategory} au niveau {level}.");
                continue;
            }

            // Si déjà la bonne catégorie, on ne remplace pas
            if (existingMarker != null && existingMarker.category == desiredCategory)
            {
                continue;
            }

            // Sauvegarder les infos de position/rotation
            Vector3 originalPos = child.position;
            Quaternion originalRot = child.rotation;
            int siblingIndex = child.GetSiblingIndex();

            // Recherche du sol sous la position actuelle
            bool groundFound = TryGetGroundYUnderPosition(originalPos, out float groundY);

            // Instancier le nouveau prefab
            Vector3 spawnPos = originalPos + Vector3.up * 1f;
            Quaternion yaw = Quaternion.Euler(0f, originalRot.eulerAngles.y, 0f);
            Quaternion prefabRot = replacementPrefab.transform.rotation;
            Quaternion finalRot = yaw * prefabRot;

            var newObj = Instantiate(replacementPrefab, spawnPos, finalRot, rocksParent.transform);
            newObj.transform.localScale = replacementPrefab.transform.localScale;
            newObj.name = $"spawned.{desiredCategory}.{replacementPrefab.name}";

            // Remettre à la même position dans la hiérarchie
            newObj.transform.SetSiblingIndex(siblingIndex);

            // Calculer le point le plus bas du prefab instancié
            float lowestWorldY = GetLowestWorldY(newObj);

            // Alignement au sol
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
                // Fallback : raycast depuis hauteur au-dessus
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
                Debug.LogWarning($"[RockSpawn] Aucun sol détecté sous '{replacementPrefab.name}'. Assigne 'groundRoot' ou règle 'groundMask'.");
            }

            // Marquer comme spawned
            var marker = newObj.AddComponent<SpawnedRock>();
            marker.category = desiredCategory;

            // Détruire l'ancien enfant
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

    private (GameObject prefab, SpawnedRock.Category category) SelectReplacementPrefab(int level)
    {
        float rand = UnityEngine.Random.value;

        if (level >= 1 && level <= 5)
        {
            float halloweenProb = Mathf.Clamp01((level - 1f) / (5f - 1f));
            if (rand < halloweenProb)
                return (PickRandom(halloweenPrefabs), SpawnedRock.Category.Halloween);
            else
                return (PickRandom(normalPrefabs), SpawnedRock.Category.Normal);
        }

        if (level >= 6 && level <= 10)
        {
            float winterProb = Mathf.Clamp01((level - 5f) / (10f - 5f));
            if (rand < winterProb)
                return (PickRandom(christmasPrefabs), SpawnedRock.Category.Christmas);
            else
                return (PickRandom(halloweenPrefabs), SpawnedRock.Category.Halloween);
        }

        return (PickRandom(normalPrefabs), SpawnedRock.Category.Normal);
    }

    private GameObject PickRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        int idx = UnityEngine.Random.Range(0, list.Count);
        return list[idx];
    }
}

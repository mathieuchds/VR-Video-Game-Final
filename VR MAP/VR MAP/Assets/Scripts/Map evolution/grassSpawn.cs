using System;
using System.Collections.Generic;
using UnityEngine;

public class grassSpawn : MonoBehaviour
{
    [Header("Objet parent contenant l'herbe")]
    [Tooltip("Tous les enfants de cet objet seront remplacés selon le niveau")]
    [SerializeField] private GameObject grassParent = null;

    [Header("Prefabs (glisser-déposer)")]
    public List<GameObject> flowerPrefabs = new List<GameObject>();
    public List<GameObject> halloweenPrefabs = new List<GameObject>();
    public List<GameObject> winterPrefabs = new List<GameObject>();

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

    [Header("Ajustement spécifique herbe")]
    [Tooltip("Décalage supplémentaire vers le bas appliqué après le snap au sol (en mètres).")]
    [SerializeField] private float additionalDownOffset = 0.0f;

    private LevelData levelData;
    private int lastLevel = -1;

    private void Start()
    {
        if (grassParent == null)
        {
            Debug.LogWarning("[grassSpawn] Aucun objet parent assigné. Assignez 'grassParent' dans l'inspecteur.");
            return;
        }

        levelData = FindObjectOfType<LevelData>();
        if (levelData == null)
        {
            Debug.LogWarning("[grassSpawn] LevelData introuvable dans la scène. Remplacement de l'herbe ignoré.");
            return;
        }

        lastLevel = levelData.level;
        ReplaceGrassByLevel(lastLevel);
    }

    private void Update()
    {
        if (grassParent == null) return;

        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();
        if (levelData == null) return;

        if (levelData.level != lastLevel)
        {
            lastLevel = levelData.level;
            ReplaceGrassByLevel(lastLevel);
            Debug.Log($"[grassSpawn] Niveau changé -> {lastLevel}, herbe mise à jour.");
        }
    }

    private void ReplaceGrassByLevel(int level)
    {
        if (grassParent == null)
        {
            Debug.LogWarning("[grassSpawn] grassParent est null, impossible de remplacer l'herbe.");
            return;
        }

        // Collecter tous les enfants directs du parent
        List<Transform> childrenToReplace = new List<Transform>();
        for (int i = 0; i < grassParent.transform.childCount; i++)
        {
            Transform child = grassParent.transform.GetChild(i);
            if (child != null)
            {
                childrenToReplace.Add(child);
            }
        }

        if (childrenToReplace.Count == 0)
        {
            Debug.Log($"[grassSpawn] Aucun enfant trouvé dans '{grassParent.name}'.");
            return;
        }

        Debug.Log($"[grassSpawn] Remplacement de {childrenToReplace.Count} herbes dans '{grassParent.name}' pour le niveau {level}.");

        foreach (var child in childrenToReplace)
        {
            if (child == null) continue;

            // Vérifier si déjà remplacé avec la bonne catégorie
            SpawnedGrass existingMarker = child.GetComponent<SpawnedGrass>();
            var selection = SelectReplacementPrefab(level);
            GameObject replacementPrefab = selection.prefab;
            SpawnedGrass.Category desiredCategory = selection.category;

            if (replacementPrefab == null)
            {
                Debug.LogWarning($"[grassSpawn] Aucun prefab disponible pour la catégorie {desiredCategory} au niveau {level}.");
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

            var newObj = Instantiate(replacementPrefab, spawnPos, finalRot, grassParent.transform);
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
                Debug.LogWarning($"[grassSpawn] Aucun sol détecté sous '{replacementPrefab.name}'. Assigne 'groundRoot' ou règle 'groundMask'.");
            }

            // Marquer comme spawned
            var marker = newObj.AddComponent<SpawnedGrass>();
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

    private (GameObject prefab, SpawnedGrass.Category category) SelectReplacementPrefab(int level)
    {
        float rand = UnityEngine.Random.value;

        if (level >= 1 && level <= 5)
        {
            float halloweenProb = Mathf.Clamp01((level - 1f) / (5f - 1f));
            if (rand < halloweenProb)
                return (PickRandom(halloweenPrefabs), SpawnedGrass.Category.Halloween);
            else
                return (PickRandom(flowerPrefabs), SpawnedGrass.Category.Flower);
        }

        if (level >= 6 && level <= 10)
        {
            float winterProb = Mathf.Clamp01((level - 5f) / (10f - 5f));
            if (rand < winterProb)
                return (PickRandom(winterPrefabs), SpawnedGrass.Category.Winter);
            else
                return (PickRandom(halloweenPrefabs), SpawnedGrass.Category.Halloween);
        }

        return (PickRandom(flowerPrefabs), SpawnedGrass.Category.Flower);
    }

    private GameObject PickRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        int idx = UnityEngine.Random.Range(0, list.Count);
        return list[idx];
    }
}

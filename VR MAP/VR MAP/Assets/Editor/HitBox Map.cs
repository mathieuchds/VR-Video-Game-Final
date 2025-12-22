using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapCollisionSetup : EditorWindow
{
    [SerializeField] private GameObject parentObject = null;
    [SerializeField] private bool applyToAllChildren = true;
    [SerializeField] private bool includeNestedChildren = true;

    [Header("Collider Settings")]
    [SerializeField] private bool addCollider = true;
    [SerializeField] private bool setAsTrigger = false;
    [SerializeField] private PhysicsMaterial physicsMaterial = null;

    [Header("Layer Settings")]
    [SerializeField] private bool setLayer = false;
    [SerializeField] private int targetLayer = 0;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Map Collision Setup")]
    public static void ShowWindow()
    {
        GetWindow<MapCollisionSetup>("Map Collision Setup");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Configuration des Collisions de Map", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        parentObject = (GameObject)EditorGUILayout.ObjectField(
            "Objet parent:", 
            parentObject, 
            typeof(GameObject), 
            true
        );

        EditorGUILayout.Space();
        
        applyToAllChildren = EditorGUILayout.Toggle("Appliquer à tous les enfants", applyToAllChildren);
        includeNestedChildren = EditorGUILayout.Toggle("Inclure enfants imbriqués", includeNestedChildren);

        EditorGUILayout.Space();
        GUILayout.Label("Paramètres Collider", EditorStyles.boldLabel);
        
        addCollider = EditorGUILayout.Toggle("Ajouter Collider", addCollider);
        
        if (addCollider)
        {
            EditorGUI.indentLevel++;
            setAsTrigger = EditorGUILayout.Toggle("Is Trigger", setAsTrigger);
            physicsMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField(
                "Physics Material", 
                physicsMaterial, 
                typeof(PhysicsMaterial), 
                false
            );
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        GUILayout.Label("Paramètres Layer", EditorStyles.boldLabel);
        
        setLayer = EditorGUILayout.Toggle("Définir Layer", setLayer);
        
        if (setLayer)
        {
            EditorGUI.indentLevel++;
            targetLayer = EditorGUILayout.LayerField("Layer cible", targetLayer);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Prévisualiser (compte)", GUILayout.Height(30)))
        {
            PreviewChanges();
        }

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("APPLIQUER LES MODIFICATIONS", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog(
                "Confirmation",
                $"Êtes-vous sûr de vouloir modifier tous les enfants de '{parentObject?.name}' ?\n\nCette action peut être annulée avec Ctrl+Z.",
                "OUI, APPLIQUER",
                "Annuler"))
            {
                ApplyChanges();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("CONFIGURER POUR OBSTACLES SOLIDES", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Preset Obstacles",
                "Appliquer la configuration pour obstacles solides (rochers, murs, décorations) ?\n\nCollider non-trigger sera ajouté.",
                "Oui",
                "Non"))
            {
                ApplyObstaclePreset();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void PreviewChanges()
    {
        if (parentObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez assigner un objet parent.", "OK");
            return;
        }

        List<GameObject> targets = GetTargetObjects();

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Aucun objet", "Aucun enfant trouvé.", "OK");
            return;
        }

        string message = $"Modifications qui seront appliquées à {targets.Count} objet(s) :\n\n";

        if (addCollider)
        {
            message += "✓ Ajout/Vérification de Collider\n";
            message += $"  - Is Trigger: {setAsTrigger}\n";
            if (physicsMaterial != null)
                message += $"  - Physics Material: {physicsMaterial.name}\n";
        }

        if (setLayer)
        {
            message += $"✓ Définir Layer: {LayerMask.LayerToName(targetLayer)}\n";
        }

        message += $"\nPremiers objets concernés:\n";
        for (int i = 0; i < Mathf.Min(targets.Count, 5); i++)
        {
            message += $"- {targets[i].name}\n";
        }
        if (targets.Count > 5)
        {
            message += $"... et {targets.Count - 5} autres.\n";
        }

        EditorUtility.DisplayDialog("Prévisualisation", message, "OK");
    }

    private void ApplyChanges()
    {
        if (parentObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez assigner un objet parent.", "OK");
            return;
        }

        List<GameObject> targets = GetTargetObjects();

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Aucun objet", "Aucun enfant trouvé à modifier.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(targets.ToArray(), "Map Collision Setup");

        int modifiedCount = 0;

        foreach (GameObject obj in targets)
        {
            bool modified = false;

            // Ajouter/configurer Collider
            if (addCollider)
            {
                Collider collider = obj.GetComponent<Collider>();
                
                if (collider == null)
                {
                    // Essayer de détecter le meilleur type de collider selon la forme
                    MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                    
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        // Utiliser MeshCollider pour objets complexes
                        MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(obj);
                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                        meshCollider.convex = false; // Non-convex pour meilleures collisions statiques
                        collider = meshCollider;
                    }
                    else
                    {
                        // Fallback: BoxCollider
                        collider = Undo.AddComponent<BoxCollider>(obj);
                    }
                    
                    modified = true;
                }

                if (collider != null)
                {
                    Undo.RecordObject(collider, "Modify Collider");
                    collider.isTrigger = setAsTrigger;
                    
                    if (physicsMaterial != null)
                    {
                        collider.material = physicsMaterial;
                    }
                }
            }

            // Définir Layer
            if (setLayer)
            {
                if (obj.layer != targetLayer)
                {
                    Undo.RecordObject(obj, "Change Layer");
                    obj.layer = targetLayer;
                    modified = true;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(obj);
                modifiedCount++;
            }
        }

        EditorUtility.DisplayDialog(
            "Succès",
            $"✅ {modifiedCount} objet(s) modifié(s) avec succès !\n\nLes colliders empêcheront joueurs et ennemis de passer à travers.",
            "OK"
        );
    }

    private void ApplyObstaclePreset()
    {
        addCollider = true;
        setAsTrigger = false;
        physicsMaterial = null;
        setLayer = false;

        ApplyChanges();
    }

    private List<GameObject> GetTargetObjects()
    {
        List<GameObject> targets = new List<GameObject>();

        if (parentObject == null) return targets;

        if (applyToAllChildren)
        {
            if (includeNestedChildren)
            {
                // Récursif : tous les enfants et sous-enfants
                GetAllChildren(parentObject.transform, targets);
            }
            else
            {
                // Seulement les enfants directs
                for (int i = 0; i < parentObject.transform.childCount; i++)
                {
                    targets.Add(parentObject.transform.GetChild(i).gameObject);
                }
            }
        }
        else
        {
            // Seulement l'objet parent lui-même
            targets.Add(parentObject);
        }

        return targets;
    }

    private void GetAllChildren(Transform parent, List<GameObject> list)
    {
        foreach (Transform child in parent)
        {
            list.Add(child.gameObject);
            GetAllChildren(child, list);
        }
    }
}

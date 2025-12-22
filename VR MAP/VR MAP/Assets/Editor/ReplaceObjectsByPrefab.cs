using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ReplaceObjectsByPrefab : EditorWindow
{
    [SerializeField] private GameObject parentObject = null;
    [SerializeField] private GameObject replacementPrefab = null;
    [SerializeField] private bool preservePosition = true;
    [SerializeField] private bool preserveRotation = true;
    [SerializeField] private bool preserveScale = false;

    [MenuItem("Tools/Replace Children By Prefab")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceObjectsByPrefab>("Replace Children");
    }

    private void OnGUI()
    {
        GUILayout.Label("Remplacer tous les enfants par un prefab", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        parentObject = (GameObject)EditorGUILayout.ObjectField("Objet parent:", parentObject, typeof(GameObject), true);
        replacementPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab de remplacement:", replacementPrefab, typeof(GameObject), false);
        
        EditorGUILayout.Space();
        GUILayout.Label("Options:", EditorStyles.boldLabel);
        
        preservePosition = EditorGUILayout.Toggle("Conserver la position", preservePosition);
        preserveRotation = EditorGUILayout.Toggle("Conserver la rotation", preserveRotation);
        preserveScale = EditorGUILayout.Toggle("Conserver l'échelle", preserveScale);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Prévisualiser (compte)"))
        {
            PreviewReplacement();
        }
        
        EditorGUILayout.Space();
        
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("REMPLACER TOUS LES ENFANTS", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirmation", 
                $"Êtes-vous sûr de vouloir remplacer tous les {GetChildCount()} enfants de '{parentObject?.name}' ?\n\nCette action ne peut pas être annulée facilement!", 
                "OUI, REMPLACER", 
                "Annuler"))
            {
                ReplaceChildren();
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private int GetChildCount()
    {
        if (parentObject == null) return 0;
        return parentObject.transform.childCount;
    }

    private void PreviewReplacement()
    {
        if (parentObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez glisser un objet parent.", "OK");
            return;
        }

        int childCount = parentObject.transform.childCount;

        if (childCount == 0)
        {
            EditorUtility.DisplayDialog("Résultat", $"L'objet '{parentObject.name}' n'a aucun enfant.", "OK");
        }
        else
        {
            string message = $"Trouvé {childCount} enfant(s) dans '{parentObject.name}' :\n\n";
            for (int i = 0; i < Mathf.Min(childCount, 10); i++)
            {
                Transform child = parentObject.transform.GetChild(i);
                message += $"- {child.name} (position: {child.position})\n";
            }
            if (childCount > 10)
            {
                message += $"\n... et {childCount - 10} autres.";
            }
            
            EditorUtility.DisplayDialog("Enfants trouvés", message, "OK");
        }
    }

    private void ReplaceChildren()
    {
        if (parentObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez glisser un objet parent.", "OK");
            return;
        }

        if (replacementPrefab == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez assigner un prefab de remplacement.", "OK");
            return;
        }

        int childCount = parentObject.transform.childCount;

        if (childCount == 0)
        {
            EditorUtility.DisplayDialog("Aucun enfant", "L'objet parent n'a aucun enfant à remplacer.", "OK");
            return;
        }

        // Collecter tous les enfants dans une liste (important car on va les détruire)
        List<Transform> childrenToReplace = new List<Transform>();
        for (int i = 0; i < childCount; i++)
        {
            childrenToReplace.Add(parentObject.transform.GetChild(i));
        }

        // Enregistrer pour Undo (Ctrl+Z)
        Undo.RegisterFullObjectHierarchyUndo(parentObject, "Replace Children");

        int replacedCount = 0;

        foreach (Transform oldChild in childrenToReplace)
        {
            // Sauvegarder les infos avant destruction
            Vector3 position = oldChild.position;
            Quaternion rotation = oldChild.rotation;
            Vector3 scale = oldChild.localScale;
            int siblingIndex = oldChild.GetSiblingIndex();

            // Instancier le nouveau prefab
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(replacementPrefab);

            if (newObj != null)
            {
                // Appliquer les transformations
                if (preservePosition)
                    newObj.transform.position = position;
                
                if (preserveRotation)
                    newObj.transform.rotation = rotation;
                
                if (preserveScale)
                    newObj.transform.localScale = scale;

                // Toujours reparenter au même parent et conserver l'ordre
                newObj.transform.SetParent(parentObject.transform);
                newObj.transform.SetSiblingIndex(siblingIndex);

                Undo.RegisterCreatedObjectUndo(newObj, "Create Replacement");

                replacedCount++;
            }

            // Détruire l'ancien enfant
            Undo.DestroyObjectImmediate(oldChild.gameObject);
        }

        EditorUtility.DisplayDialog("Succès", 
            $"✅ {replacedCount} enfant(s) remplacé(s) avec succès dans '{parentObject.name}' !\n\nN'oubliez pas de sauvegarder la scène (Ctrl+S).", 
            "OK");

        // Marquer la scène comme modifiée
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}

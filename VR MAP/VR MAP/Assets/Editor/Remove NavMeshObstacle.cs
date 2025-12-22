using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class RemoveObstacle : EditorWindow
{
    [MenuItem("Tools/NavMesh/Remove NavMesh Obstacles from Children")]
    public static void RemoveObstaclesFromChildren()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un GameObject parent dans la hiérarchie.", "OK");
            return;
        }

        int count = 0;
        NavMeshObstacle[] obstacles = selectedObject.GetComponentsInChildren<NavMeshObstacle>(true);
        
        foreach (NavMeshObstacle obstacle in obstacles)
        {
            if (obstacle != null)
            {
                Undo.DestroyObjectImmediate(obstacle);
                count++;
            }
        }

        EditorUtility.DisplayDialog("Succès", 
            $"{count} NavMeshObstacle(s) supprimé(s) des enfants de '{selectedObject.name}'.", "OK");
    }

    [MenuItem("Tools/NavMesh/Remove NavMesh Obstacles from Children", true)]
    public static bool ValidateRemoveObstaclesFromChildren()
    {
        return Selection.activeGameObject != null;
    }
}

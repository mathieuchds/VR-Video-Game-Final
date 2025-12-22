using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class Obstacle : EditorWindow
{
    [MenuItem("Tools/NavMesh/Convert Children to NavMesh Obstacles")]
    public static void ConvertChildrenToObstacles()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un GameObject parent dans la hiérarchie.", "OK");
            return;
        }

        int count = 0;
        Transform[] children = selectedObject.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform child in children)
        {
            // Skip le parent lui-même
            if (child == selectedObject.transform) continue;

            // Ajouter NavMeshObstacle si pas déjà présent
            NavMeshObstacle obstacle = child.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = child.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true; // Active le carving par défaut
                obstacle.shape = NavMeshObstacleShape.Box;
                
                // Essayer d'ajuster la taille selon le collider ou renderer
                Collider col = child.GetComponent<Collider>();
                if (col != null)
                {
                    obstacle.center = col.bounds.center - child.position;
                    obstacle.size = col.bounds.size;
                }
                else
                {
                    Renderer rend = child.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        obstacle.center = rend.bounds.center - child.position;
                        obstacle.size = rend.bounds.size;
                    }
                }
                
                count++;
            }
        }

        EditorUtility.DisplayDialog("Succès", 
            $"{count} NavMeshObstacle(s) ajouté(s) aux enfants de '{selectedObject.name}'.", "OK");
    }

    [MenuItem("Tools/NavMesh/Convert Children to NavMesh Obstacles", true)]
    public static bool ValidateConvertChildrenToObstacles()
    {
        return Selection.activeGameObject != null;
    }
}

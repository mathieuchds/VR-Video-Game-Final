using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class WalkableSurface : EditorWindow
{
    [MenuItem("Tools/NavMesh/Convert Children to Walkable NavMesh Surfaces")]
    public static void ConvertChildrenToWalkableSurfaces()
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

            // Marquer comme Navigation Static
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, 
                GameObjectUtility.GetStaticEditorFlags(child.gameObject) | StaticEditorFlags.NavigationStatic);

            // Ajouter NavMeshSurface si pas déjà présent
            NavMeshSurface surface = child.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = child.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                
                // Configuration pour surface marchable
                surface.defaultArea = 0; // Walkable area
                
                count++;
            }
        }

        EditorUtility.DisplayDialog("Succès", 
            $"{count} NavMeshSurface(s) ajouté(s) aux enfants de '{selectedObject.name}'.\n" +
            "N'oubliez pas de cliquer sur 'Bake' sur chaque NavMeshSurface pour générer la navigation.", "OK");
    }

    [MenuItem("Tools/NavMesh/Convert Children to Walkable NavMesh Surfaces", true)]
    public static bool ValidateConvertChildrenToWalkableSurfaces()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/NavMesh/Bake All NavMesh Surfaces in Selection")]
    public static void BakeAllNavMeshSurfaces()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un GameObject parent dans la hiérarchie.", "OK");
            return;
        }

        NavMeshSurface[] surfaces = selectedObject.GetComponentsInChildren<NavMeshSurface>(true);
        int baked = 0;

        foreach (NavMeshSurface surface in surfaces)
        {
            surface.BuildNavMesh();
            baked++;
        }

        EditorUtility.DisplayDialog("Succès", 
            $"{baked} NavMeshSurface(s) baked dans '{selectedObject.name}'.", "OK");
    }

    [MenuItem("Tools/NavMesh/Bake All NavMesh Surfaces in Selection", true)]
    public static bool ValidateBakeAllNavMeshSurfaces()
    {
        if (Selection.activeGameObject == null) return false;
        return Selection.activeGameObject.GetComponentsInChildren<NavMeshSurface>(true).Length > 0;
    }
}
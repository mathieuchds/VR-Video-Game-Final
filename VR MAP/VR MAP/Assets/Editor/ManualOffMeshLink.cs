using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;

public class ManualOffMeshLink : EditorWindow
{
    [MenuItem("Tools/NavMesh/Create Manual Link (Select 2 Empty GameObjects)")]
    public static void CreateManualLink()
    {
        if (Selection.gameObjects.Length != 2)
        {
            EditorUtility.DisplayDialog("Info", 
                "Comment créer un NavMesh Link manuel :\n\n" +
                "1. Crée 2 GameObjects vides (Ctrl+Shift+N)\n" +
                "2. Place-les aux points de départ et d'arrivée\n" +
                "3. Sélectionne les 2 objets (Ctrl+clic)\n" +
                "4. Lance ce menu à nouveau\n\n" +
                "Un NavMesh Link sera créé entre eux.",
                "OK");
            return;
        }

        GameObject obj1 = Selection.gameObjects[0];
        GameObject obj2 = Selection.gameObjects[1];

        // Créer le container du link
        GameObject linkContainer = new GameObject($"NavMeshLink_{obj1.name}_to_{obj2.name}");
        linkContainer.transform.position = (obj1.transform.position + obj2.transform.position) / 2f;

        // Ajouter le composant NavMeshLink
        NavMeshLink link = Undo.AddComponent<NavMeshLink>(linkContainer);
        
        // Calculer les positions relatives
        Vector3 startPoint = obj1.transform.position - linkContainer.transform.position;
        Vector3 endPoint = obj2.transform.position - linkContainer.transform.position;
        
        link.startPoint = startPoint;
        link.endPoint = endPoint;
        link.bidirectional = true;
        link.autoUpdate = true;
        link.width = 2.0f; // Largeur généreuse pour les ennemis
        
        // Activer le link
        link.enabled = true;

        Selection.activeGameObject = linkContainer;

        EditorUtility.DisplayDialog("Succès", 
            $"✅ NavMesh Link créé entre :\n• {obj1.name}\n• {obj2.name}\n\n" +
            "Les ennemis utiliseront ce lien automatiquement pour traverser.",
            "OK");
    }
}

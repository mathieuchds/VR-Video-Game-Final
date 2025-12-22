using UnityEngine;

public class RiverLimit : MonoBehaviour
{
    [SerializeField] private GameObject spawnPointsParent; // GameObject parent contenant les points de spawn
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string bossTag = "Boss";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float respawnOffset = 1f; // Offset au-dessus du point de spawn

    private void Start()
    {
        // Vérifier que le collider est bien configuré en trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            ////Debug.LogWarning($"Le Collider sur {gameObject.name} n'est pas configuré en 'Is Trigger'. Activation automatique.");
            col.isTrigger = true;
        }
        else if (col == null)
        {
            ////Debug.LogError($"Aucun Collider trouvé sur {gameObject.name}. Ajoutez un BoxCollider ou autre.");
        }

        // Vérifier que le GameObject parent est défini et qu'il a des enfants
        if (spawnPointsParent == null)
        {
            ////Debug.LogError($"Aucun GameObject parent défini sur {gameObject.name} ! Assignez un GameObject contenant des points de spawn enfants.");
        }
        else if (spawnPointsParent.transform.childCount == 0)
        {
            ////Debug.LogError($"Le GameObject '{spawnPointsParent.name}' n'a aucun enfant ! Ajoutez des GameObjects enfants comme points de spawn.");
        }
        else
        {
            ////Debug.Log($"{spawnPointsParent.transform.childCount} point(s) de spawn trouvé(s) dans '{spawnPointsParent.name}'."); 
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ////Debug.Log($"OnTriggerEnter avec {other.gameObject.name}, tag: {other.tag}");

        // Si c'est un ennemi, le détruire
        if (other.CompareTag(enemyTag))
        {
            ////Debug.Log($"Ennemi détecté ({other.gameObject.name}), destruction...");
            Destroy(other.gameObject);
            return;
        }

        // Si c'est le joueur ou le boss, le téléporter
        if (other.CompareTag(playerTag) || other.CompareTag(bossTag))
        {
            ////Debug.Log($"Joueur ou Boss détecté ({other.gameObject.name}), recherche du point de spawn le plus proche...");
            TeleportToClosestSpawn(other);
        }
    }

    private void TeleportToClosestSpawn(Collider target)
    {
        if (spawnPointsParent == null || spawnPointsParent.transform.childCount == 0)
        {
            ////Debug.LogError("Impossible de téléporter : aucun point de spawn défini !");
            return;
        }

        Transform closestSpawn = FindClosestSpawnPoint(target.transform.position);

        if (closestSpawn != null)
        {
            // Téléporter au point de spawn le plus proche avec l'offset
            Vector3 respawnPosition = closestSpawn.position + Vector3.up * respawnOffset;
            
            // Trouver le GameObject avec le CharacterController
            CharacterController controller = target.GetComponentInParent<CharacterController>();
            
            if (controller != null)
            {
                ////Debug.Log($"CharacterController trouvé sur {controller.gameObject.name}");
                
                // IMPORTANT : Désactiver le CharacterController avant la téléportation
                controller.enabled = false;
                
                // Téléporter le GameObject qui a le CharacterController
                controller.transform.position = respawnPosition;
                
                // Réactiver le CharacterController
                controller.enabled = true;
                
                ////Debug.Log($"Téléportation effectuée. Nouvelle position : {controller.transform.position}");
            }
            else
            {
                // Fallback : téléporter directement le transform si pas de CharacterController
                ////Debug.LogWarning($"Aucun CharacterController trouvé, téléportation de {target.transform.root.name}");
                target.transform.root.position = respawnPosition;
            }
            
            // Réinitialiser la vélocité du Rigidbody s'il existe
            Rigidbody rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                ////Debug.Log("Vitesse du Rigidbody réinitialisée.");
            }
        }
        else
        {
            ////Debug.LogError("Aucun point de spawn valide trouvé !");
        }
    }

    private Transform FindClosestSpawnPoint(Vector3 position)
    {
        Transform closest = null;
        float minDistance = Mathf.Infinity;

        // Parcourir tous les enfants du GameObject parent
        foreach (Transform spawnPoint in spawnPointsParent.transform)
        {
            float distance = Vector3.Distance(position, spawnPoint.position);
            //Debug.Log($"Point de spawn '{spawnPoint.name}' : distance = {distance:F2}m");

            if (distance < minDistance)
            {
                minDistance = distance;
                closest = spawnPoint;
            }
        }

        if (closest != null)
        {
            //Debug.Log($"Point de spawn le plus proche : '{closest.name}' à {minDistance:F2}m");
        }

        return closest;
    }

    // Méthode helper pour visualiser les points de spawn dans l'éditeur
    private void OnDrawGizmos()
    {
        if (spawnPointsParent == null || spawnPointsParent.transform.childCount == 0)
            return;

        Gizmos.color = Color.green;
        foreach (Transform spawnPoint in spawnPointsParent.transform)
        {
            Gizmos.DrawWireSphere(spawnPoint.position, 1f);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + Vector3.up * 2f);
        }
    }
}

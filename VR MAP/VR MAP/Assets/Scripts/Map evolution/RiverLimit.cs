using UnityEngine;

public class RiverLimit : MonoBehaviour
{
    [SerializeField] private GameObject spawnPointsParent; 
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string bossTag = "Boss";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float respawnOffset = 1f; 

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag(enemyTag))
        {
            Destroy(other.gameObject);
            return;
        }

        if (other.CompareTag(playerTag) || other.CompareTag(bossTag))
        {
            TeleportToClosestSpawn(other);
        }
    }

    private void TeleportToClosestSpawn(Collider target)
    {
        if (spawnPointsParent == null || spawnPointsParent.transform.childCount == 0)
        {
            return;
        }

        Transform closestSpawn = FindClosestSpawnPoint(target.transform.position);

        if (closestSpawn != null)
        {
            Vector3 respawnPosition = closestSpawn.position + Vector3.up * respawnOffset;
            
            CharacterController controller = target.GetComponentInParent<CharacterController>();
            
            if (controller != null)
            {
                
                controller.enabled = false;
                
                controller.transform.position = respawnPosition;
                
                controller.enabled = true;
                
            }
            else
            {
                target.transform.root.position = respawnPosition;
            }
            
            Rigidbody rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

    }

    private Transform FindClosestSpawnPoint(Vector3 position)
    {
        Transform closest = null;
        float minDistance = Mathf.Infinity;

        foreach (Transform spawnPoint in spawnPointsParent.transform)
        {
            float distance = Vector3.Distance(position, spawnPoint.position);

            if (distance < minDistance)
            {
                minDistance = distance;
                closest = spawnPoint;
            }
        }



        return closest;
    }

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

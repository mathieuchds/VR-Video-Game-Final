using UnityEngine;
using UnityEngine.AI;

public class FollowPlayer : MonoBehaviour
{
    [Header("Assignation (drag depuis Hierarchy ou Project)")]
    [Tooltip("Glisser soit le GameObject 'Player' (depuis la Hierarchy) soit le prefab (depuis Project).")]
    [SerializeField] private GameObject targetObject;

    // runtime Transform utilisé par l'agent
    public Transform target;
    private NavMeshAgent agent;

    public EnemySpawner spawner;

    void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
        agent = GetComponent<NavMeshAgent>();

        // Si une référence GameObject est fournie dans l'inspector, on en récupère le Transform
        if (targetObject != null)
            target = targetObject.transform;

        // fallback : recherche par tag si rien d'assigné
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        // Vérifier que l'agent est bien sur le NavMesh au démarrage
        if (agent != null && !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[FollowPlayer] Agent '{name}' n'est pas sur le NavMesh ! Essai de placement...");
            TryPlaceOnNavMesh();
        }
    }


    void Update()
    {
        if (target == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                target = playerGO.transform;
            else
                return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.SetDestination(target.position);
    }

    /// <summary>
    /// Tente de placer l'agent sur le NavMesh le plus proche
    /// </summary>
    private void TryPlaceOnNavMesh()
    {
        if (agent == null) return;

        NavMeshHit hit;
        // Chercher le point NavMesh le plus proche dans un rayon de 10m
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Debug.Log($"[FollowPlayer] Agent '{name}' replacé sur NavMesh à {hit.position}");
        }
        else
        {
            Debug.LogError($"[FollowPlayer] Impossible de trouver un NavMesh proche pour '{name}' ! Vérifiez que le NavMesh est bien baked.");
            // Désactiver l'agent pour éviter les erreurs
            agent.enabled = false;
        }
    }

    public void Die()
    {
        if (spawner != null) spawner.EnemyDied();
        Destroy(gameObject);
    }
}

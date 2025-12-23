using UnityEngine;
using UnityEngine.AI;

public class FollowPlayer : MonoBehaviour
{
    [Header("Assignation (drag depuis Hierarchy ou Project)")]
    [Tooltip("Glisser soit le GameObject 'Player' (depuis la Hierarchy) soit le prefab (depuis Project).")]
    [SerializeField] private GameObject targetObject;

    public Transform target;
    private NavMeshAgent agent;

    public EnemySpawner spawner;

    void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
        agent = GetComponent<NavMeshAgent>();

        if (targetObject != null)
            target = targetObject.transform;

        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (agent != null && !agent.isOnNavMesh)
        {
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

    private void TryPlaceOnNavMesh()
    {
        if (agent == null) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            agent.enabled = false;
        }
    }

    public void Die()
    {
        if (spawner != null) spawner.EnemyDied();
        Destroy(gameObject);
    }
}

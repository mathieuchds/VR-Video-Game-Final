using UnityEngine;
using UnityEngine.AI;

public class WeepingAngel : MonoBehaviour
{
    public Transform player;
    public Camera playerCam;

    public float attackDistance = 2f;
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 8f;

    private NavMeshAgent agent;
    private bool isSeen;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        playerCam = Camera.main;
    }

    void Update()
    {
        if (player == null || playerCam == null) return;

        CheckIfSeen();

        if (!isSeen)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);

            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * rotationSpeed
            );
        }
        else
        {
            agent.isStopped = true;
        }

    }

    void CheckIfSeen()
    {
        Vector3 dirToEnemy = transform.position - playerCam.transform.position;
        float dot = Vector3.Dot(playerCam.transform.forward, dirToEnemy.normalized);

        // Si l’ennemi est dans le champ de vision de la caméra
        if (dot > 0.5f)
        {
            // Vérifier si rien ne bloque la vue
            if (Physics.Raycast(playerCam.transform.position, dirToEnemy.normalized, out RaycastHit hit))
            {
                if (hit.transform == transform)
                {
                    isSeen = true;
                    return;
                }
            }
        }

        isSeen = false;
    }

 



}

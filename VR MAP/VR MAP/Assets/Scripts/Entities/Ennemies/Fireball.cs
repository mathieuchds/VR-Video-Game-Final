using UnityEngine;

public class FireBall : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 15f;
    private Vector3 direction;

    [Header("Raycast Settings")]
    [SerializeField] private float sphereRadius = 0.8f;
    [Tooltip("Layers que la fireball peut toucher")]
    [SerializeField] private LayerMask hitLayers = ~0; 

    private bool hasHit = false;
    private Vector3 lastPosition;

    public void SetDirection(Vector3 dir)
    {
        direction = dir.normalized;
    }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (direction == Vector3.zero || hasHit) return;

        Vector3 currentPosition = transform.position;
        float distanceThisFrame = speed * Time.fixedDeltaTime;
        Vector3 nextPosition = currentPosition + direction * distanceThisFrame;

        float totalDistance = Vector3.Distance(lastPosition, nextPosition);

        RaycastHit hit;
        if (Physics.SphereCast(lastPosition, sphereRadius, direction, out hit, totalDistance, hitLayers))
        {
            Debug.DrawLine(lastPosition, hit.point, Color.red, 1f);

            if (hit.collider.CompareTag("PlayerProjectile"))
            {
                return;
            }

            if (hit.collider.gameObject != gameObject && hit.collider.CompareTag("Player"))
            {
                hasHit = true;

                PlayerStats ps = hit.collider.GetComponent<PlayerStats>();
                if (ps != null)
                {
                    ps.TakeDamage(damage);
                }

                Destroy(gameObject);
                return;
            }
        }
        transform.position = nextPosition;
        lastPosition = nextPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || other.gameObject == gameObject) return;

        if (other.CompareTag("PlayerProjectile"))
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            hasHit = true;
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
    }
}
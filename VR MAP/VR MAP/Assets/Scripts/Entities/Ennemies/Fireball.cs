using UnityEngine;

public class FireBall : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 15f;
    private Vector3 direction;

    [Header("Raycast Settings")]
    [SerializeField] private float sphereRadius = 0.8f; // Plus gros rayon

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

        // ✅ RAYCAST entre la position précédente et la prochaine position
        // Cela garantit qu'on ne rate JAMAIS une collision
        float totalDistance = Vector3.Distance(lastPosition, nextPosition);

        RaycastHit hit;
        if (Physics.SphereCast(lastPosition, sphereRadius, direction, out hit, totalDistance))
        {
            Debug.DrawLine(lastPosition, hit.point, Color.red, 1f);

            if (hit.collider.gameObject != gameObject && hit.collider.CompareTag("Player"))
            {
                hasHit = true;

                PlayerStats ps = hit.collider.GetComponent<PlayerStats>();
                if (ps != null)
                {
                    ps.TakeDamage(damage);
                    Debug.Log($"[FireBall] ✅ {damage} dégâts infligés via RAYCAST!");
                }

                Destroy(gameObject);
                return;
            }
        }

        // Déplacer
        transform.position = nextPosition;
        lastPosition = nextPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || other.gameObject == gameObject) return;

        if (other.CompareTag("Player"))
        {
            hasHit = true;
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(damage);
                Debug.Log($"[FireBall] ✅ {damage} dégâts infligés via TRIGGER!");
            }
            Destroy(gameObject);
        }
    }
}
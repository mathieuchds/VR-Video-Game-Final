using UnityEngine;

public class Bomba : MonoBehaviour
{

    public PlayerStats stats;
    public float explosionRadius = 3f;
    public float explosionDamage = 20f;

    void Start()
    {
        stats=GameObject.FindObjectOfType<PlayerStats>(true);
        explosionDamage = stats.explosionDamage;
        explosionRadius = stats.explosionRadius;

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player"))
        {
            Explode();
        }
    }

    void Explode()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in enemies)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (enemy != null)
            {
                enemy.TakeDamage(explosionDamage);

                Vector3 dir = (enemy.transform.position - transform.position).normalized;

                enemy.Knockback(dir, 10f, 1f);
            }
        }

        // Détruit la bombe
        Destroy(gameObject);
    }

}

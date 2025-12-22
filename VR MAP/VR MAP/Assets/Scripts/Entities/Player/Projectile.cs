using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float damage = 10f;   // dégâts infligés
    public float lifeTime = 3f;  // durée avant auto-destruction

    public bool isPoisonous = false;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            return;

        Enemy enemy = other.GetComponent<Enemy>();

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if (isPoisonous)
            {
                enemy.ApplyPoison();
            }

        }

        Destroy(gameObject); // disparaît après impact


    }
}

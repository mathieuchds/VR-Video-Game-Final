using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float damage = 10f;   // dégâts infligés

    private void Start()
    {
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {

                ps.TakeDamage(damage);
            }
            Destroy(gameObject); // disparaît après impact
        }

       
    }

}

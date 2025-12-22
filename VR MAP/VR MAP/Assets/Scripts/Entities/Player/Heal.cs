using UnityEngine;

public class Heal : MonoBehaviour
{
    public float heal = 100f;   // dégâts infligés

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
                ps.Heal(heal);
                Destroy(gameObject); // disparaît après contact
            }
            
        }


    }

}
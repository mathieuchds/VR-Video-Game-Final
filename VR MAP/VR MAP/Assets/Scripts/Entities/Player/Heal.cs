using UnityEngine;

public class Heal : MonoBehaviour
{
    public float heal = 50f;   // pv soignés
    private bool isActive = false;
    public Transform[] possiblePositions;

    private void Start()
    {
    }

    public void ActivateRandom()
    {
        if (possiblePositions.Length > 0)
        {
            int index = Random.Range(0, possiblePositions.Length);
            transform.position = possiblePositions[index].position;
        }

        isActive = true;
        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
    }
    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            PlayerStats ps = other.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.Heal(heal);
                Deactivate(); // disparaît après contact
            }
            
        }


    }

}
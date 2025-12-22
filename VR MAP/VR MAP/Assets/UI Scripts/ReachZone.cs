using UnityEngine;

public class ReachZone : MonoBehaviour
{
    [Header("Random Spawn")]
    [SerializeField] private Transform[] possiblePositions;

    private bool isActive = false;

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
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;

        QuestManager.Instance?.AddProgress(QuestObjectiveType.ReachZone, 1);

        Deactivate();
    }
}

using UnityEngine;

public class FlameHitbox : MonoBehaviour
{


    private void OnTriggerStay(Collider other)
    {

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        enemy.ApplyBurn();
    }
}

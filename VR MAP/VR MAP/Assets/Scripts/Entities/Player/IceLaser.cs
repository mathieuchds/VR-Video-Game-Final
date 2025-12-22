using UnityEngine;

public class IceLaser : MonoBehaviour
{
    public float stunDuration = 0.2f;

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.Stun(stunDuration);
        }
    }
}

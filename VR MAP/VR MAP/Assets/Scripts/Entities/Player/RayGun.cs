using UnityEngine;
using UnityEngine.InputSystem;

public class Gun : MonoBehaviour
{
    public float range = 50f;
    public float damage = 20f;

    void Update()
    {
       
        // Tir quand tu cliques avec le bouton gauche
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        Vector3 origin = Camera.main.transform.position;
        Vector3 direction = Camera.main.transform.forward;

        // Afficher le ray en rouge
        Debug.DrawRay(origin, direction * range, Color.red, 5f);

        // Raycast physique
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range))
        {
            //Debug.Log("Touché : " + hit.transform.name);

            Enemy enemy = hit.transform.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }
    }
}

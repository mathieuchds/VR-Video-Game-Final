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
        // ✅ Ignorer le joueur
        if (other.CompareTag("Player"))
            return;

        // ✅ NOUVEAU : Ignorer les projectiles ennemis (fireballs)
        if (other.CompareTag("EnemyProjectile") || other.GetComponent<FireBall>() != null)
        {
            return; // Traverser sans détruire le projectile
        }

        // Vérifier si c'est un ennemi
        Enemy enemy = other.GetComponent<Enemy>();

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if (isPoisonous)
            {
                enemy.ApplyPoison();
            }

            Destroy(gameObject); // Détruire uniquement si on touche un ennemi
        }
    }
}

using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;   // Préfab de ton ennemi
    public float spawnInterval = 3f; // Temps entre chaque spawn
    public int maxEnemies = 10;      // Nombre max d'ennemis actifs

    private int currentEnemies = 0;

    void Start()
    {
        InvokeRepeating("SpawnEnemy", 0f, spawnInterval);
    }

    void SpawnEnemy()
    {
        if (currentEnemies >= maxEnemies) return;

        GameObject enemy = Instantiate(enemyPrefab, transform.position, Quaternion.identity);

        enemy.GetComponent<Enemy>().spawner = this;

        currentEnemies++;
    }

    public void EnemyDied()
    {
        currentEnemies--;
    }
}

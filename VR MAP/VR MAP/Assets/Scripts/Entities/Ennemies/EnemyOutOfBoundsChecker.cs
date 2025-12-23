using System.Collections;
using UnityEngine;


public class EnemyOutOfBoundsChecker : MonoBehaviour
{
    [Header("Out of Bounds Settings")]
    [Tooltip("Y minimum - en dessous de cette valeur, l'ennemi est considéré hors map")]
    [SerializeField] private float minY = -2f; 

    [Tooltip("Y maximum pour les ennemis volants (wizards, ours) - au dessus de cette valeur, ils sont considérés bloqués")]
    [SerializeField] private float maxYForFlyingEnemies = 2.5f;

    [Tooltip("Durée en secondes avant de supprimer l'ennemi hors limites")]
    [SerializeField] private float timeBeforeRemoval = 2f;

    [Header("Enemy Type Detection")]
    [Tooltip("Noms des types d'ennemis concernés par les limites haute ET basse")]
    [SerializeField] private string[] flyingEnemyNames = { "Wizard", "wizard", "Ice Wizard", "ice wizard", "IceWizard", "icewizard", "Bear", "bear" }; 

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private Enemy enemyComponent;
    private Coroutine outOfBoundsCoroutine;
    private bool isOutOfBounds = false;
    private bool isFlyingType = false;

    private void Awake()
    {
        enemyComponent = GetComponent<Enemy>();

        if (enemyComponent == null)
        {
            enabled = false;
            return;
        }

        isFlyingType = IsEnemyFlyingType();

        if (debugMode)
            Debug.Log($"[EnemyOutOfBoundsChecker] Initialisé sur {gameObject.name} - Type volant: {isFlyingType}");
    }

    private void Update()
    {
        if (enemyComponent == null) return;

        float currentY = transform.position.y;
        bool wasOutOfBounds = isOutOfBounds;

        if (isFlyingType)
        {
            if (currentY < minY)
            {
                isOutOfBounds = true;
                if (debugMode && !wasOutOfBounds)
                    Debug.Log($"[EnemyOutOfBoundsChecker] {gameObject.name} (volant) est sous la limite basse (Y={currentY:F2} < {minY})");
            }
            else if (currentY > maxYForFlyingEnemies)
            {
                isOutOfBounds = true;
                if (debugMode && !wasOutOfBounds)
                    Debug.Log($"[EnemyOutOfBoundsChecker] {gameObject.name} (volant) est au-dessus de la limite haute (Y={currentY:F2} > {maxYForFlyingEnemies})");
            }
            else
            {
                isOutOfBounds = false;
            }
        }
        else
        {
            if (currentY < minY)
            {
                isOutOfBounds = true;
                if (debugMode && !wasOutOfBounds)
                    Debug.Log($"[EnemyOutOfBoundsChecker] {gameObject.name} est sous la limite basse (Y={currentY:F2} < {minY})");
            }
            else
            {
                isOutOfBounds = false;
            }
        }

        if (isOutOfBounds && outOfBoundsCoroutine == null)
        {
            outOfBoundsCoroutine = StartCoroutine(RemoveAfterDelay());
        }
        else if (!isOutOfBounds && outOfBoundsCoroutine != null)
        {
            StopCoroutine(outOfBoundsCoroutine);
            outOfBoundsCoroutine = null;

            if (debugMode)
                Debug.Log($"[EnemyOutOfBoundsChecker] {gameObject.name} est revenu dans les limites");
        }
    }


    private bool IsEnemyFlyingType()
    {
        string objName = gameObject.name.ToLower();

        foreach (string flyingName in flyingEnemyNames)
        {
            if (objName.Contains(flyingName.ToLower()))
            {
                if (debugMode)
                    Debug.Log($"[EnemyOutOfBoundsChecker] {gameObject.name} détecté comme type volant (contient '{flyingName}')");
                return true;
            }
        }

        return false;
    }

    private IEnumerator RemoveAfterDelay()
    {
        if (debugMode)
            Debug.Log($"[EnemyOutOfBoundsChecker] Démarrage du compte à rebours ({timeBeforeRemoval}s) pour {gameObject.name}");

        yield return new WaitForSeconds(timeBeforeRemoval);

        if (debugMode)
            Debug.Log($"[EnemyOutOfBoundsChecker] Suppression de {gameObject.name} (hors limites depuis {timeBeforeRemoval}s)");

        CountAsKill();

        if (enemyComponent.spawner != null)
        {
            enemyComponent.spawner.EnemyDied();
        }

        Destroy(gameObject);
    }


    private void CountAsKill()
    {
        QuestManager questManager = QuestManager.Instance;
        if (questManager != null)
        {
            questManager.AddProgress(QuestObjectiveType.KillEnemy, 1);

            if (debugMode)
                Debug.Log($"[EnemyOutOfBoundsChecker] +1 kill ajouté pour {gameObject.name}");
        }

        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            LevelData levelData = FindObjectOfType<LevelData>();
            int currentLevel = levelData != null ? levelData.level : 0;
            float scoreToAdd = 10f + currentLevel;

            playerStats.AddScore(scoreToAdd);

            if (debugMode)
                Debug.Log($"[EnemyOutOfBoundsChecker]  +{scoreToAdd} score ajouté");
        }
    }

    private void OnDestroy()
    {
        if (outOfBoundsCoroutine != null)
        {
            StopCoroutine(outOfBoundsCoroutine);
            outOfBoundsCoroutine = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        Vector3 pos = transform.position;

        Gizmos.color = Color.red;
        Vector3 minYPos = new Vector3(pos.x, minY, pos.z);
        Gizmos.DrawLine(pos, minYPos);
        Gizmos.DrawWireSphere(minYPos, 0.5f);

        if (isFlyingType)
        {
            Gizmos.color = Color.yellow;
            Vector3 maxYPos = new Vector3(pos.x, maxYForFlyingEnemies, pos.z);
            Gizmos.DrawLine(pos, maxYPos);
            Gizmos.DrawWireSphere(maxYPos, 0.5f);
        }
    }

    private void DebugShowStatus()
    {
        Debug.Log($"=== STATUS {gameObject.name} ===");
        Debug.Log($"Position Y: {transform.position.y:F2}");
        Debug.Log($"Min Y: {minY}");
        Debug.Log($"Max Y (flying): {maxYForFlyingEnemies}");
        Debug.Log($"Is Flying Type: {isFlyingType}");
        Debug.Log($"Is Out Of Bounds: {isOutOfBounds}");
        Debug.Log($"Coroutine Active: {outOfBoundsCoroutine != null}");
    }

    private void DebugForceRemove()
    {
        Debug.Log($"[EnemyOutOfBoundsChecker] Force Remove appelé sur {gameObject.name}");
        CountAsKill();
        Destroy(gameObject);
    }

}

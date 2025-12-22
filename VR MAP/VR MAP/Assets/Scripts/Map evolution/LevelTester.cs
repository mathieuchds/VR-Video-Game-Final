using UnityEngine;
using System.Collections;

public class LevelTester : MonoBehaviour
{
    [Header("Mode test")]
    [Tooltip("Cochez pour incrémenter le level automatiquement toutes les X secondes (Play Mode uniquement).")]
    [SerializeField] private bool testMode = false;

    [Tooltip("Intervalle en secondes entre chaque incrément.")]
    [SerializeField] private float intervalSeconds = 5f;

    private LevelData levelData;
    private Coroutine runningCoroutine;

    void Awake()
    {
        levelData = FindObjectOfType<LevelData>();
        if (levelData == null)
            Debug.LogWarning("LevelData introuvable dans la scène. Attachez un objet avec LevelData.");
    }

    void Update()
    {
        // Démarrer/arrêter la coroutine selon l'état de la case dans l'inspecteur (seulement en Play Mode)
        if (testMode && runningCoroutine == null && Application.isPlaying)
        {
            runningCoroutine = StartCoroutine(IncrementLevelRoutine());
        }
        else if (!testMode && runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
    }

    private IEnumerator IncrementLevelRoutine()
    {
        if (levelData == null) yield break;

        while (levelData.level < 10 && testMode)
        {
            yield return new WaitForSeconds(intervalSeconds);

            // Sécurité : re-vérifier l'existence et la limite
            if (levelData == null) yield break;

            levelData.level = Mathf.Min(levelData.level + 1, 10);
            Debug.Log($"[LevelTester] Level incrémenté -> {levelData.level}");

            // Si atteint 10, on arrête automatiquement et décoche la case pour éviter boucle inutile
            if (levelData.level >= 10)
            {
                testMode = false;
                runningCoroutine = null;
                yield break;
            }
        }

        runningCoroutine = null;
    }

    // Optionnel : expose une méthode publique pour démarrer/stopper depuis d'autres scripts
    public void SetTestMode(bool enabled)
    {
        testMode = enabled;
        if (!enabled && runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
    }
}
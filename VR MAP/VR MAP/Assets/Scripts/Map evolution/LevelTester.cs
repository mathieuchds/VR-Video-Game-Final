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

            if (levelData == null) yield break;

            levelData.level = Mathf.Min(levelData.level + 1, 10);
            Debug.Log($"[LevelTester] Level incrémenté -> {levelData.level}");

            if (levelData.level >= 10)
            {
                testMode = false;
                runningCoroutine = null;
                yield break;
            }
        }

        runningCoroutine = null;
    }

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
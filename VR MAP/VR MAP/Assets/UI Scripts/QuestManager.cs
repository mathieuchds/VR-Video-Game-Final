using UnityEngine;

public class QuestManager : MonoBehaviour
{

    public static QuestManager Instance;
    public QuestUIManager questUI;

    private Quest currentQuest;
    private ReachZone reachZone;

    private int questIndex = 0;


    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        reachZone = FindObjectOfType<ReachZone>();
        questIndex = -1;
        StartNextQuest();

    }

    public void StartQuest(string title, string description, QuestObjectiveType type,float target)
    {
        currentQuest = new Quest(title, description, type, target);
        questUI.ShowQuest();
        UpdateUI();
    }

    public void AddProgress(QuestObjectiveType type, float amount = 1f)
    {
        if (currentQuest == null) return;
        if (currentQuest == null) return;
        if (currentQuest.IsCompleted) return;

        if (currentQuest.objectiveType != type)
            return;

        currentQuest.AddProgress(amount);
        UpdateUI();

        if (currentQuest.IsCompleted)
        {
            CompleteQuest();
        }
    }

    void UpdateUI()
    {
        questUI.UpdateQuest(
            currentQuest.title,
            currentQuest.description,
            currentQuest.current,
            currentQuest.target
        );
    }

    void CompleteQuest()
    {
        questUI.HideQuest();
        PowerSelectionManager psm = FindObjectOfType<PowerSelectionManager>();
        if (psm != null)
        {
            psm.ShowPowerSelection();
        }
        Invoke(nameof(StartNextQuest), 1f);
    }



    void StartNextQuest()
    {
        questIndex++;

        switch (questIndex % 3)
        {
            case 0:
                StartKillQuest();
                break;

            case 1:
                StartReachZoneQuest();
                break;

            case 2:
                StartShootQuest();
                break;
        }
    }

    void StartKillQuest()
    {
        StartQuest(
            "Cleanup",
            "Kill 25 enemies",
            QuestObjectiveType.KillEnemy,
            25
        );
    }

    void StartReachZoneQuest()
    {
        StartQuest(
            "Reach Zone",
            "Reach 1 golden zone",
            QuestObjectiveType.ReachZone,
            1
        );

        reachZone.ActivateRandom();
    }

    void StartShootQuest()
    {
        StartQuest(
            "Training",
            "Shoot 50 bullets",
            QuestObjectiveType.ShootBullets,
            50
        );
    }





}

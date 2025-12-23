using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    public QuestUIManager questUI;

    private Quest currentQuest;
    
    [Header("References")]
    [SerializeField] private ReachZone reachZone;

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
        if (reachZone == null)
        {
            reachZone = FindObjectOfType<ReachZone>(true); 
            
            if (reachZone == null)
            {
                Debug.LogError("[QuestManager] ReachZone introuvable dans la scène ! Créez un GameObject avec le script ReachZone.");
            }
            else
            {
                Debug.Log($"[QuestManager] ReachZone trouvée: {reachZone.gameObject.name}");
            }
        }
        
        if (reachZone != null)
        {
            reachZone.Deactivate();
        }
        
        questIndex = -1;
        StartNextQuest();
    }

    public void StartQuest(string title, string description, QuestObjectiveType type, float target)
    {
        currentQuest = new Quest(title, description, type, target);
        questUI.ShowQuest();
        UpdateUI();
    }

    public void AddProgress(QuestObjectiveType type, float amount = 1f)
    {
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
        if (reachZone != null && currentQuest != null && currentQuest.objectiveType == QuestObjectiveType.ReachZone)
        {
            reachZone.Deactivate();
            Debug.Log("[QuestManager] ReachZone désactivée après complétion");
        }
        
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
        if (reachZone != null)
        {
            reachZone.Deactivate();
        }
        
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
        
        if (reachZone != null)
        {
            reachZone.Deactivate();
        }
        
        Debug.Log("[QuestManager] Quête Kill démarrée");
    }

    void StartReachZoneQuest()
    {
        StartQuest(
            "Reach Zone",
            "Reach 1 golden zone",
            QuestObjectiveType.ReachZone,
            1
        );

        if (reachZone != null)
        {
            reachZone.ActivateRandom();
            Debug.Log($"[QuestManager] Quête ReachZone démarrée - Zone activée à {reachZone.transform.position}");
        }
        else
        {
            Debug.LogError("[QuestManager] ReachZone introuvable ! Impossible de démarrer la quête. Vérifiez que le GameObject ReachZone existe dans la scène.");
        }
    }

    void StartShootQuest()
    {
        StartQuest(
            "Training",
            "Shoot 50 bullets",
            QuestObjectiveType.ShootBullets,
            50
        );
        
        // S'assurer que la zone est désactivée pour cette quête
        if (reachZone != null)
        {
            reachZone.Deactivate();
        }
        
        Debug.Log("[QuestManager] 🎯 Quête Shoot démarrée");
    }
}

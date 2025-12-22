using UnityEngine;
using System.Collections;

public enum QuestObjectiveType
{
    KillEnemy,
    ShootBullets,
    ReachZone,
}



public class Quest
{
    public string title;
    public string description;

    public QuestObjectiveType objectiveType;

    public float current;
    public float target;

    public bool IsCompleted => current >= target;

    public Quest(string title, string description, QuestObjectiveType type, float target)
    {
        this.title = title;
        this.description = description;
        this.objectiveType = type;
        this.target = target;
        current = 0;
    }

    public void AddProgress(float amount)
    {
        if (IsCompleted) return;
        current += amount;
        current = Mathf.Clamp(current, 0, target);
    }
}

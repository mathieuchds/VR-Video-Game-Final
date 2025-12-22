using UnityEngine;
using TMPro;

public class QuestUIManager : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text progressText;

    public void UpdateQuest(string title, string description, float current, float target)
    {
        titleText.text = title;
        descriptionText.text = description;
        progressText.text = $"Progress : {current} / {target}";
    }

    public void HideQuest()
    {
        gameObject.SetActive(false);
    }

    public void ShowQuest()
    {
        gameObject.SetActive(true);
    }
}

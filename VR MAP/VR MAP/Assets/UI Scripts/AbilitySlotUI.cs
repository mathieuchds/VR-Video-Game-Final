using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilitySlotUI : MonoBehaviour
{
    [Header("UI (optionnel)")]
    public Image icon;              
    public TMP_Text cooldownText;
    public Image powerLock;


    [Header("State")]
    public bool isUnlocked = false;

    public void Awake()
    {
        Lock();
    }

    public void Unlock()
    {
        isUnlocked = true;
        cooldownText.text = "";
        if (icon != null)
        {
            icon.color = Color.white;
            icon.fillAmount = 1f;
        }
        powerLock.enabled = false;

    }

    public void Lock()
    {
        isUnlocked = false;

        if (icon != null)
        {
            icon.color = Color.gray;
            icon.fillAmount = 1f;
        }
        powerLock.enabled = true;
    }

    public void UpdateCooldown(float remaining, float max)
    {
        if (!isUnlocked)
        {
            return;
        }

        if (remaining > 0f)
        {
            cooldownText.text = remaining.ToString("F1") + "s";

            if (icon != null)
                icon.fillAmount = 1f-(remaining / max);
        }
        else
        {
            cooldownText.text = "";
            if (icon != null)
            {
                icon.fillAmount = 1f;
                icon.color = Color.white;
            }
                
            
        }
    }
}

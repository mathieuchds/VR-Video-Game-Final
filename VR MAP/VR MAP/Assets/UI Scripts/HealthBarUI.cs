using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    public PlayerStats stats;
    public Image fillImage;
    public TMP_Text textAmount;

    private void Start()
    {
        /*GameObject player = GameObject.FindGameObjectWithTag("Player");
        stats = player.GetComponent<PlayerStats>();*/

        stats.currentHealth = stats.maxHealth;
        fillImage = GameObject.Find("HealthBar_Fill").GetComponent<Image>();
        textAmount = GameObject.Find("HealthAmount").GetComponent<TMP_Text>();


        stats.HealthUpdate += UpdateHealthUI;
        UpdateHealthUI();
    }

    void UpdateHealthUI()
    {
        if (stats != null)
        {
            fillImage.fillAmount = stats.currentHealth / stats.maxHealth;
            textAmount.text = $"{stats.currentHealth} / {stats.maxHealth}";
        }
    }


}

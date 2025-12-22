using TMPro;
using UnityEngine;

public class GameOverScore : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI scoreText;

    public void SetScore(float score)
    {
        scoreText.text = score.ToString();
    }
}

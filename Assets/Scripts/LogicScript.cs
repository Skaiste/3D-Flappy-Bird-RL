using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.MLAgents;


public class LogicScript : MonoBehaviour
{
    public int playerScore;
    public Text scoreText;
    public GameObject gameOverScreen;
    public BirdPlayerScript bird;

    bool IsTraining() =>
        Academy.IsInitialized && (Academy.Instance.IsCommunicatorOn || Application.isBatchMode);


    [ContextMenu("Increase Score")]
    public void AddScore(int amount)
    {
        if (gameOverScreen.activeSelf) return;
        playerScore += amount;
        scoreText.text = playerScore.ToString();
        // Debug.Log("Score: " + playerScore);
    }

    public void restartGame()
    {
        if (!IsTraining())
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void gameOver(bool isTraining)
    {
        if (!gameOverScreen) return;
        if (IsTraining())
        {
            // During training we skip the UI overlay entirely
            gameOverScreen.SetActive(false);
            return;
        }
        gameOverScreen.SetActive(true);
    }
}

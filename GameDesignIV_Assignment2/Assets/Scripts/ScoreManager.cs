using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private int scorePlayer1 = 0;
    private int scorePlayer2 = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddScore(int player, int amount)
    {
        if (player == 1)
            scorePlayer1 += amount;
        else if (player == 2)
            scorePlayer2 += amount;

        Debug.Log($"Player 1: {scorePlayer1} | Player 2: {scorePlayer2}");
    }

    public int GetScore(int player) => player == 1 ? scorePlayer1 : scorePlayer2;
}
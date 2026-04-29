using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI scorePlayer1Text;
    [SerializeField] private TextMeshProUGUI scorePlayer2Text;

    [Header("End Screens")]
    [SerializeField] private Image winScreen;
    [SerializeField] private Image loseScreen;

    [Header("Menu")]
    [SerializeField] private string mainMenuSceneName = "NGO_FPS_Menu";

    // ✅ Networked scores
    private NetworkVariable<int> player1Score = new NetworkVariable<int>(0);
    private NetworkVariable<int> player2Score = new NetworkVariable<int>(0);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to score changes
        player1Score.OnValueChanged += OnScoreChanged;
        player2Score.OnValueChanged += OnScoreChanged;

        // Initialize UI
        UpdateScoreUI(player1Score.Value, player2Score.Value);

        if (winScreen != null)
            winScreen.gameObject.SetActive(false);

        if (loseScreen != null)
            loseScreen.gameObject.SetActive(false);
    }

    private void OnScoreChanged(int oldValue, int newValue)
    {
        UpdateScoreUI(player1Score.Value, player2Score.Value);
    }

    // ---------------- SERVER ONLY ----------------

    public void AddScoreServer(int playerNumber, int amount)
    {
        if (!IsServer) return;

        if (playerNumber == 1)
            player1Score.Value += amount;
        else if (playerNumber == 2)
            player2Score.Value += amount;

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        int winScore = 5; // example

        if (player1Score.Value >= winScore)
        {
            ShowEndScreenClientRpc(1);
        }
        else if (player2Score.Value >= winScore)
        {
            ShowEndScreenClientRpc(2);
        }
    }

    // ---------------- CLIENT ----------------

    public void UpdateScoreUI(int p1Score, int p2Score)
    {
        if (scorePlayer1Text != null)
            scorePlayer1Text.text = $"P1: {p1Score}";

        if (scorePlayer2Text != null)
            scorePlayer2Text.text = $"P2: {p2Score}";
    }

    [ClientRpc]
    public void ShowEndScreenClientRpc(int winningPlayer)
    {
        bool isWinner = false;

        // Each client checks if they are the winner
        if (NetworkManager.Singleton.LocalClientId == (ulong)(winningPlayer - 1))
            isWinner = true;

        if (winScreen != null)
            winScreen.gameObject.SetActive(isWinner);

        if (loseScreen != null)
            loseScreen.gameObject.SetActive(!isWinner);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // ---------------- MENU ----------------

    public void OnReturnToMenuPressed()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
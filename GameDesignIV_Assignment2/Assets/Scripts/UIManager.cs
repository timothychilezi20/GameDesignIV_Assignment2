using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Score UI")]
    [SerializeField] private GameObject scorePlayer1Object;
    [SerializeField] private GameObject scorePlayer2Object;

    private Text scorePlayer1Text;
    private Text scorePlayer2Text;

    [Header("End Screens")]
    [SerializeField] private Image winScreen;
    [SerializeField] private Image loseScreen;

    [Header("Menu")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Disable images via their GameObjects
        if (winScreen != null)
            winScreen.gameObject.SetActive(false);

        if (loseScreen != null)
            loseScreen.gameObject.SetActive(false);

        // Get Text components from GameObjects
        if (scorePlayer1Object != null)
            scorePlayer1Text = scorePlayer1Object.GetComponent<Text>();

        if (scorePlayer2Object != null)
            scorePlayer2Text = scorePlayer2Object.GetComponent<Text>();
    }

    public void UpdateScoreUI(int p1Score, int p2Score)
    {
        if (scorePlayer1Text != null)
            scorePlayer1Text.text = "P1: " + p1Score;

        if (scorePlayer2Text != null)
            scorePlayer2Text.text = "P2: " + p2Score;
    }

    public void ShowEndScreen(bool isWinner)
    {
        if (winScreen != null)
            winScreen.gameObject.SetActive(isWinner);

        if (loseScreen != null)
            loseScreen.gameObject.SetActive(!isWinner);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnReturnToMenuPressed()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
            Unity.Netcode.NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
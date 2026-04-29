using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField ipInput;
    [SerializeField] private TMP_InputField portInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Defaults")]
    [SerializeField] private string defaultIP = "10.196.184.50";
    [SerializeField] private ushort defaultPort = 7777;

    [Header("Networking")]
    [SerializeField] private UnityTransport transport;
    [SerializeField] private NetworkManager networkManager;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "apayin";

    [Header("Audio")]
    [SerializeField] private AudioSource buttonSfx;

    private void Awake()
    {
        // Set default values
        if (ipInput) ipInput.text = defaultIP;
        if (portInput) portInput.text = defaultPort.ToString();

        // Persist audio across scenes
        if (buttonSfx != null)
            DontDestroyOnLoad(buttonSfx.gameObject);
    }

    // ================= HOST =================
    public void StartHost()
    {
        PlayButtonSound();

        Debug.Log("Attempting to start HOST...");

        if (networkManager.IsListening)
        {
            Debug.LogWarning("Network already running. Shutting down first...");
            networkManager.Shutdown();
        }

        ushort port = GetPort();
        transport.SetConnectionData("0.0.0.0", port);

        bool success = networkManager.StartHost();

        if (success)
        {
            Debug.Log("Host started successfully");

            // Load scene for all clients (requires Scene Management enabled)
            networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);

            DisableButtons();
        }
        else
        {
            Debug.LogError("Failed to start host");
        }
    }

    // ================= CLIENT =================
    public void JoinGame()
    {
        PlayButtonSound();

        Debug.Log("Attempting to start CLIENT...");

        Debug.Log(
            $"State Before Start → IsClient: {networkManager.IsClient}, " +
            $"IsServer: {networkManager.IsServer}, " +
            $"IsListening: {networkManager.IsListening}"
        );

        if (networkManager.IsListening)
        {
            Debug.LogWarning("Already running. Shutting down before reconnect...");
            networkManager.Shutdown();
        }

        string ip = GetIP();
        ushort port = GetPort();

        transport.SetConnectionData(ip, port);

        bool success = networkManager.StartClient();

        if (success)
        {
            Debug.Log("Client started. Connecting to " + ip);
            DisableButtons();
        }
        else
        {
            Debug.LogError("Failed to start client");
        }
    }

    // ================= SERVER ONLY =================
    public void StartServerOnly()
    {
        PlayButtonSound();

        if (networkManager.IsListening)
        {
            Debug.LogWarning("Network already running. Shutting down first...");
            networkManager.Shutdown();
        }

        ushort port = GetPort();
        transport.SetConnectionData("0.0.0.0", port);

        if (networkManager.StartServer())
        {
            Debug.Log("Server started");
            DisableButtons();
        }
        else
        {
            Debug.LogError("Failed to start server");
        }
    }

    // ================= HELPERS =================
    private void PlayButtonSound()
    {
        if (buttonSfx != null)
            buttonSfx.Play();
    }

    private string GetIP()
    {
        if (!ipInput || string.IsNullOrWhiteSpace(ipInput.text))
            return defaultIP;

        return ipInput.text.Trim();
    }

    private ushort GetPort()
    {
        if (!portInput || !ushort.TryParse(portInput.text, out ushort port))
            return defaultPort;

        return port;
    }

    private void DisableButtons()
    {
        if (hostButton) hostButton.interactable = false;
        if (joinButton) joinButton.interactable = false;
    }
}
using UnityEngine;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private NetworkVariable<int> scorePlayer1 = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> scorePlayer2 = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        scorePlayer1.OnValueChanged += (oldValue, newValue) => OnScoreChanged();
        scorePlayer2.OnValueChanged += (oldValue, newValue) => OnScoreChanged();
    }

    public override void OnNetworkDespawn()
    {
        scorePlayer1.OnValueChanged -= (oldValue, newValue) => OnScoreChanged();
        scorePlayer2.OnValueChanged -= (oldValue, newValue) => OnScoreChanged();
    }

    private void OnScoreChanged()
    {
        Debug.Log($"Player 1: {scorePlayer1.Value} | Player 2: {scorePlayer2.Value}");
        // Hook your UI update here e.g. UpdateScoreUI()
    }

    public void AddScore(int player, int amount)
    {
        if (!IsServer) return;

        if (player == 1) scorePlayer1.Value += amount;
        else if (player == 2) scorePlayer2.Value += amount;
    }

    public int GetScore(int player) => player == 1 ? scorePlayer1.Value : scorePlayer2.Value;
}
using UnityEngine;
using Unity.Netcode;
using Unity.Multiplayer.Center.NetcodeForGameObjectsExample.DistributedAuthority;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    private bool _gameEnded = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Called by ScoreManager or PlayerController when a win condition is met
    public void EndGame(int winningPlayer)
    {
        if (!IsServer || _gameEnded) return;
        _gameEnded = true;

        Debug.Log($"[GameManager] Player {winningPlayer} wins!");
        ShowResultClientRpc(winningPlayer);
    }

    [ClientRpc]
    private void ShowResultClientRpc(int winningPlayer)
    {
        // Each client figures out if they are the winner or loser
        // based on their own player number
        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer == null) return;

        bool isWinner = localPlayer.playerNumber == winningPlayer;
        UIManager.Instance?.ShowEndScreenClientRpc(localPlayer.playerNumber);
    }

    private PlayerController GetLocalPlayer()
    {
        foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.IsOwner) return pc;
        return null;
    }
}
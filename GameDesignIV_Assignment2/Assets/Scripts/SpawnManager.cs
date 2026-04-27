using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class SpawnManager : NetworkBehaviour
{
    [SerializeField] private MapManager mapManager;

    public void RespawnPlayer(ulong clientId)
    {
        StartCoroutine(RespawnCoroutine(clientId));
    }

    private IEnumerator RespawnCoroutine(ulong clientId)
    {
        while (!mapManager.CanSpawn())
            yield return null;

        Transform spawnPoint = mapManager.GetActiveLaunchPoint();
        var player = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;

        if (player == null) yield break;

        // Use the RPC on the player, not direct transform manipulation
        var networkPlayer = player.GetComponent<NetworkFPSPlayer>();
        if (networkPlayer != null)
        {
            networkPlayer.TeleportToSpawn(); // calls the RPC internally
        }

        var launcher = player.GetComponent<PlayerLauncher>();
        if (launcher != null)
        {
            launcher.ResetVelocity();
        }
    }
}
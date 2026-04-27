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

        if (player == null)
            yield break;

        var controller = player.GetComponent<CharacterController>();
        if (controller) controller.enabled = false;

        player.transform.SetPositionAndRotation(
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (controller) controller.enabled = true;

        var launcher = player.GetComponent<PlayerLauncher>();
        if (launcher != null)
        {
            launcher.ResetVelocity(); // only movement reset
        }

        Debug.Log("[SERVER] Player respawned (no countdown)");
    }
}
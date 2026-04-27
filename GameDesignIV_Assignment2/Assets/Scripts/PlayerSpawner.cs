using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class PlayerSpawnManager : NetworkBehaviour
{
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Handle host immediately
        OnClientConnected(NetworkManager.Singleton.LocalClientId);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        StartCoroutine(SpawnAfterDelay(clientId));
    }

    private IEnumerator SpawnAfterDelay(ulong clientId)
    {
        // Wait until player object exists
        NetworkClient client = null;
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out client)
                && client.PlayerObject != null)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (client == null || client.PlayerObject == null)
        {
            Debug.LogError($"Timed out waiting for player object for client {clientId}");
            yield break;
        }

        // Determine which spawn point to use
        Transform spawnPoint = clientId == 0 ? spawnPoint1 : spawnPoint2;

        if (spawnPoint == null)
        {
            Debug.LogError($"Spawn point is null for client {clientId}");
            yield break;
        }

        Debug.Log($"Spawning client {clientId} at {spawnPoint.position}");

        // Tell the owning client to set their own position
        TeleportPlayerClientRpc(clientId, spawnPoint.position, spawnPoint.rotation);
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController player in players)
        {
            if (player.OwnerClientId == clientId)
            {
                player.transform.position = position;
                player.transform.rotation = rotation;
                Debug.Log($"Client {clientId} placed at {position}");
                return;
            }
        }

        Debug.LogError($"Could not find PlayerController for client {clientId}");
    }
}
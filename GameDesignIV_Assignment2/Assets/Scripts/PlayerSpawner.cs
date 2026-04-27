using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class PlayerSpawnManager : NetworkBehaviour
{
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
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
        yield return null;
        yield return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Debug.LogError($"Could not find client {clientId}");
            yield break;
        }

        if (client.PlayerObject == null)
        {
            Debug.LogError($"Player object is null for client {clientId}");
            yield break;
        }

        int spawnIndex = Mathf.Clamp(
            NetworkManager.Singleton.ConnectedClients.Count - 1,
            0,
            spawnPoints.Length - 1);

        client.PlayerObject.transform.position = spawnPoints[spawnIndex].position;
        client.PlayerObject.transform.rotation = spawnPoints[spawnIndex].rotation;

        Debug.Log($"Spawned client {clientId} at spawn point {spawnIndex}");
    }
}
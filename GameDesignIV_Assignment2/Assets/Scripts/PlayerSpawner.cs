using UnityEngine;
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
        // Get the player object for this client
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            int spawnIndex = NetworkManager.Singleton.ConnectedClients.Count - 1;
            spawnIndex = Mathf.Clamp(spawnIndex, 0, spawnPoints.Length - 1);

            client.PlayerObject.transform.position = spawnPoints[spawnIndex].position;
            client.PlayerObject.transform.rotation = spawnPoints[spawnIndex].rotation;
        }
    }
}
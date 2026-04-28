using UnityEngine;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerSpawnManager : NetworkBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 15f;
    [SerializeField] private float launchDelay = 1f; // pause before launch after both ready

    // Track which clients are ready
    private bool _player1Ready = false;
    private bool _player2Ready = false;
    private ulong _client1Id;
    private ulong _client2Id;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        _client1Id = NetworkManager.Singleton.LocalClientId;
        StartCoroutine(TeleportWhenReady(_client1Id, spawnPoint1, isPlayer1: true));
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        _client2Id = clientId;
        StartCoroutine(TeleportWhenReady(clientId, spawnPoint2, isPlayer1: false));
    }

    private IEnumerator TeleportWhenReady(ulong clientId, Transform spawnPoint, bool isPlayer1)
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

        if (client?.PlayerObject == null)
        {
            Debug.LogError($"Timed out waiting for player {clientId}");
            yield break;
        }

        if (spawnPoint == null)
        {
            Debug.LogError($"Spawn point null for client {clientId}");
            yield break;
        }

        // Teleport into position and lock them there until launch
        TeleportClientRpc(clientId, spawnPoint.position, spawnPoint.rotation);
        client.PlayerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Mark this player as ready
        if (isPlayer1) _player1Ready = true;
        else _player2Ready = true;

        // Once both are in position, launch both
        if (_player1Ready && _player2Ready)
            StartCoroutine(LaunchBothPlayers());
    }

    private IEnumerator LaunchBothPlayers()
    {
        // Brief hold so players can see each other before launching
        yield return new WaitForSeconds(launchDelay);

        LaunchPlayerClientRpc(_client1Id, spawnPoint1.forward);
        LaunchPlayerClientRpc(_client2Id, spawnPoint2.forward);
    }

    // ── ClientRpcs ────────────────────────────────────────────────────────────

    [ClientRpc]
    private void TeleportClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.transform.SetPositionAndRotation(position, rotation);

        NetworkTransform nt = player.GetComponent<NetworkTransform>();
        if (nt != null)
            nt.Teleport(position, rotation, player.transform.localScale);
    }

    [ClientRpc]
    private void LaunchPlayerClientRpc(ulong clientId, Vector3 launchDirection)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        player.Launch(launchDirection, launchForce);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private PlayerController GetLocalPlayer(ulong clientId)
    {
        foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (pc.OwnerClientId == clientId)
                return pc;
        }
        Debug.LogWarning($"Could not find PlayerController for client {clientId}");
        return null;
    }
}
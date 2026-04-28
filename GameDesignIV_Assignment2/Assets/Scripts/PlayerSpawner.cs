using UnityEngine;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerSpawnManager : NetworkBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    [Header("Countdown")]
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float launchDelay = 1f; // pause before countdown starts

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
        NetworkClient client = null;
        float elapsed = 0f;

        while (elapsed < 5f)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out client)
                && client.PlayerObject != null)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (client?.PlayerObject == null)
        {
            Debug.LogError($"[SpawnManager] Timed out waiting for player {clientId}");
            yield break;
        }

        if (spawnPoint == null)
        {
            Debug.LogError($"[SpawnManager] Spawn point null for client {clientId}");
            yield break;
        }

        // Register with MapManager so player rides map switches
        if (MapManager.Instance != null)
            MapManager.Instance.RegisterPlayer(client.PlayerObject.transform);

        // Place on server
        client.PlayerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Place and lock on owning client
        TeleportClientRpc(clientId, spawnPoint.position, spawnPoint.rotation);

        if (isPlayer1) _player1Ready = true;
        else _player2Ready = true;

        // Once both are placed, start countdown on both
        if (_player1Ready && _player2Ready)
            StartCoroutine(BeginCountdowns());
    }

    private IEnumerator BeginCountdowns()
    {
        // Brief pause so both players are settled before the countdown appears
        yield return new WaitForSeconds(launchDelay);

        StartCountdownClientRpc(_client1Id, countdownDuration);
        StartCountdownClientRpc(_client2Id, countdownDuration);
    }

    // ── ClientRpcs ────────────────────────────────────────────────────────────

    [ClientRpc]
    private void TeleportClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        // Lock movement and zero physics before placing
        player.SetMovementLocked(true);

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
    private void StartCountdownClientRpc(ulong clientId, float duration)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        // PlayerLauncher drives the countdown and calls ExecuteLaunch when done
        PlayerLauncher launcher = player.GetComponent<PlayerLauncher>();
        if (launcher != null)
            launcher.StartCountdown(duration);
        else
            Debug.LogWarning($"[SpawnManager] No PlayerLauncher on player {clientId} — launching directly.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private PlayerController GetLocalPlayer(ulong clientId)
    {
        foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.OwnerClientId == clientId)
                return pc;

        Debug.LogWarning($"[SpawnManager] Could not find PlayerController for client {clientId}");
        return null;
    }
}
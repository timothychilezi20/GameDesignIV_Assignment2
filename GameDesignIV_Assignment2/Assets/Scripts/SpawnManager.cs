using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

public class SpawnManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MapManager mapManager;

    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    [Header("Countdown")]
    [SerializeField] private float countdownDuration = 3f;

    private bool _player1Ready = false;
    private bool _player2Ready = false;
    private ulong _client1Id;
    private ulong _client2Id;

    public static SpawnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        _client1Id = NetworkManager.Singleton.LocalClientId;
        StartCoroutine(InitialSpawnCoroutine(_client1Id, spawnPoint1, isPlayer1: true));
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
        StartCoroutine(InitialSpawnCoroutine(clientId, spawnPoint2, isPlayer1: false));
    }

    // ── Initial spawn ─────────────────────────────────────────────────────────

    private IEnumerator InitialSpawnCoroutine(ulong clientId, Transform spawnPoint, bool isPlayer1)
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

        if (MapManager.Instance != null)
            MapManager.Instance.RegisterPlayer(client.PlayerObject.transform);

        // Pass position, rotation AND the spawn point's forward so the player
        // knows exactly which direction to launch in
        PlacePlayerClientRpc(clientId, spawnPoint.position,
                             spawnPoint.rotation, spawnPoint.forward);

        if (isPlayer1) _player1Ready = true;
        else _player2Ready = true;

        if (_player1Ready && _player2Ready)
            StartCoroutine(BeginCountdowns());
    }

    private IEnumerator BeginCountdowns()
    {
        yield return new WaitForSeconds(0.5f);
        StartCountdownClientRpc(_client1Id, countdownDuration);
        StartCountdownClientRpc(_client2Id, countdownDuration);
    }

    // ── Respawn ───────────────────────────────────────────────────────────────

    public void RespawnPlayer(ulong clientId)
    {
        if (!IsServer) return;
        StartCoroutine(RespawnCoroutine(clientId));
    }

    private IEnumerator RespawnCoroutine(ulong clientId)
    {
        while (!mapManager.CanSpawn())
            yield return null;

        Transform spawnPoint = mapManager.GetSpawnPoint();

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            || client.PlayerObject == null)
        {
            Debug.LogError($"[SpawnManager] Could not find player object for client {clientId}");
            yield break;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("[SpawnManager] Spawn point from MapManager is null");
            yield break;
        }

        PlacePlayerClientRpc(clientId, spawnPoint.position,
                             spawnPoint.rotation, spawnPoint.forward);

        Debug.Log($"[SpawnManager] Respawned client {clientId} at {spawnPoint.position}");
    }

    // ── ClientRpcs ────────────────────────────────────────────────────────────

    [ClientRpc]
    private void PlacePlayerClientRpc(ulong clientId, Vector3 position,
                                      Quaternion rotation, Vector3 launchDirection)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        // Bake the spawn point's forward into the player before placement
        // so PlayerLauncher reads the correct direction at countdown end
        player.SetLaunchDirection(launchDirection);
        player.PlaceAtSpawnPoint(position, rotation);

        NetworkTransform nt = player.GetComponent<NetworkTransform>();
        if (nt != null)
            nt.Teleport(player.transform.position, rotation, player.transform.localScale);

        PlayerLauncher launcher = player.GetComponent<PlayerLauncher>();
        if (launcher != null)
            launcher.ResetMatchState();

        Debug.Log($"[SpawnManager] Client {clientId} placed at {player.transform.position} " +
                  $"launch dir: {launchDirection}");
    }

    [ClientRpc]
    private void StartCountdownClientRpc(ulong clientId, float duration)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        PlayerLauncher launcher = player.GetComponent<PlayerLauncher>();
        if (launcher != null)
            launcher.StartCountdown(duration);
        else
            Debug.LogWarning($"[SpawnManager] No PlayerLauncher on player {clientId}");
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
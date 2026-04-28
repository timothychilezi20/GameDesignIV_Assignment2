using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

/// <summary>
/// SpawnManager — places players at their spawn points and triggers the
/// PlayerLauncher countdown. Works with the Rigidbody-based PlayerController.
///
/// FLOW:
///   1. Server waits for both players' NetworkObjects to be ready.
///   2. PlacePlayerClientRpc fires on the owning client — bakes launch direction,
///      calls PlaceAtSpawnPoint, resets launcher state.
///   3. StartCountdownClientRpc fires — countdown begins, auto-fires at zero.
///   4. On respawn, waits for MapManager.CanSpawn(), then repeats steps 2–3.
/// </summary>
public class SpawnManager : NetworkBehaviour
{
    // =========================================================================
    // Inspector
    // =========================================================================

    [Header("References")]
    [SerializeField] private MapManager mapManager;

    [Header("Initial Spawn Points")]
    [Tooltip("Spawn point for the host (player 1). Forward = launch direction.")]
    [SerializeField] private Transform spawnPoint1;

    [Tooltip("Spawn point for the client (player 2). Forward = launch direction.")]
    [SerializeField] private Transform spawnPoint2;

    [Header("Countdown")]
    [SerializeField] private float countdownDuration = 3f;

    // =========================================================================
    // Private state
    // =========================================================================

    private bool _player1Ready = false;
    private bool _player2Ready = false;
    private ulong _client1Id;
    private ulong _client2Id;

    // =========================================================================
    // Singleton
    // =========================================================================

    public static SpawnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // =========================================================================
    // Network spawn
    // =========================================================================

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
        // Ignore the host reconnecting to itself
        if (clientId == NetworkManager.Singleton.LocalClientId) return;
        _client2Id = clientId;
        StartCoroutine(InitialSpawnCoroutine(clientId, spawnPoint2, isPlayer1: false));
    }

    // =========================================================================
    // Initial spawn
    // =========================================================================

    private IEnumerator InitialSpawnCoroutine(ulong clientId, Transform spawnPoint, bool isPlayer1)
    {
        // Wait until the player's NetworkObject is fully ready
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
            Debug.LogError($"[SpawnManager] Timed out waiting for player object — client {clientId}");
            yield break;
        }

        if (spawnPoint == null)
        {
            Debug.LogError($"[SpawnManager] Spawn point null for client {clientId}");
            yield break;
        }

        // Register with MapManager so player rides map transitions
        if (MapManager.Instance != null)
            MapManager.Instance.RegisterPlayer(client.PlayerObject.transform);

        // Place player and bake launch direction from the spawn point's forward
        PlacePlayerClientRpc(clientId,
                             spawnPoint.position,
                             spawnPoint.rotation,
                             spawnPoint.forward);

        if (isPlayer1) _player1Ready = true;
        else _player2Ready = true;

        // Start both countdowns once both players are placed
        if (_player1Ready && _player2Ready)
            StartCoroutine(BeginCountdowns());
    }

    private IEnumerator BeginCountdowns()
    {
        // Brief pause so PlacePlayerClientRpc has propagated to clients
        yield return new WaitForSeconds(0.5f);
        StartCountdownClientRpc(_client1Id, countdownDuration);
        StartCountdownClientRpc(_client2Id, countdownDuration);
    }

    // =========================================================================
    // Respawn  (call from game logic — e.g. fall-off detection)
    // =========================================================================

    public void RespawnPlayer(ulong clientId)
    {
        if (!IsServer) return;
        StartCoroutine(RespawnCoroutine(clientId));
    }

    private IEnumerator RespawnCoroutine(ulong clientId)
    {
        // Block until map is not mid-transition
        while (!mapManager.CanSpawn())
            yield return null;

        Transform spawnPoint = mapManager.GetSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.LogError("[SpawnManager] MapManager returned null spawn point.");
            yield break;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId, out NetworkClient client) || client.PlayerObject == null)
        {
            Debug.LogError($"[SpawnManager] Cannot find player object for client {clientId}");
            yield break;
        }

        PlacePlayerClientRpc(clientId,
                             spawnPoint.position,
                             spawnPoint.rotation,
                             spawnPoint.forward);

        // Small delay so placement propagates, then start countdown
        yield return new WaitForSeconds(0.3f);
        StartCountdownClientRpc(clientId, countdownDuration);

        Debug.Log($"[SpawnManager] Respawned client {clientId} at {spawnPoint.position}");
    }

    // =========================================================================
    // ClientRPCs
    // =========================================================================

    [ClientRpc]
    private void PlacePlayerClientRpc(ulong clientId, Vector3 position,
                                      Quaternion rotation, Vector3 launchDirection)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        PlayerController player = GetLocalPlayer(clientId);
        if (player == null) return;

        // 1. Bake the launch direction BEFORE placement so the launcher reads
        //    the correct forward when the countdown fires
        player.SetLaunchDirection(launchDirection);

        // 2. Place the Rigidbody at the corrected surface position
        player.PlaceAtSpawnPoint(position, rotation);

        // 3. Sync NetworkTransform so remote clients see the correct position
        NetworkTransform nt = player.GetComponent<NetworkTransform>();
        if (nt != null)
            nt.Teleport(player.transform.position, rotation, player.transform.localScale);

        // 4. Reset launcher state (clears countdown, locks movement)
        PlayerLauncher launcher = player.GetComponent<PlayerLauncher>();
        if (launcher != null)
            launcher.ResetMatchState();
        else
            Debug.LogWarning($"[SpawnManager] No PlayerLauncher on player {clientId}");

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

    // =========================================================================
    // Helpers
    // =========================================================================

    private PlayerController GetLocalPlayer(ulong clientId)
    {
        foreach (PlayerController pc in
                 FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (pc.OwnerClientId == clientId)
                return pc;
        }

        Debug.LogWarning($"[SpawnManager] Could not find PlayerController for client {clientId}");
        return null;
    }
}
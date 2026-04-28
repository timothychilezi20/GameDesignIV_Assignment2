using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MapManager : NetworkBehaviour
{
    [Header("Map Transforms")]
    public Transform map1;
    public Transform map2;

    [Header("Positions")]
    public Vector3 map1UpPos;
    public Vector3 map1DownPos;
    public Vector3 map2UpPos;
    public Vector3 map2DownPos;

    [Header("Slide Settings")]
    public float moveSpeed = 5f;

    [Header("Flat State")]
    public float flatHoldDuration = 3f;

    [Header("Switch Settings")]
    public int flatPhasesBeforeSwap = 2;

    [Header("Tilt Settings")]
    public float minTiltAngle = 35f;
    public float maxTiltAngle = 40f;
    public float tiltSpeed = 8f;
    public float tiltHoldDuration = 3f;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] map1SpawnPoints;
    [SerializeField] private Transform[] map2SpawnPoints;

    // Drag your LaunchPoint GameObject here in the Inspector
    [Header("Launch Gate")]
    public LaunchPoint launchPoint;

    // =========================================================================
    // Network state
    // =========================================================================

    private enum MapState : byte { WaitingForLaunch, Flat, Switching, Tilting }

    private NetworkVariable<MapState> netState = new NetworkVariable<MapState>(
        MapState.WaitingForLaunch,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> netMap1Active = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<float> netTilt = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // =========================================================================
    // Server-only state
    // =========================================================================

    private float stateTimer;
    private float targetTilt;
    private int flatPhaseCount;
    private bool tiltingIn;
    private int spawnIndex;
    private List<Transform> trackedPlayers = new List<Transform>();

    public static MapManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer) { flatPhaseCount = 0; spawnIndex = 0; }
    }

    // =========================================================================
    // Update
    // =========================================================================

    private void Update()
    {
        if (IsServer) ServerTick();
        ApplyVisuals();
    }

    private void ServerTick()
    {
        switch (netState.Value)
        {
            case MapState.WaitingForLaunch:
                // Poll LaunchPoint — the moment both players are out, start the map
                if (launchPoint != null && launchPoint.BothLaunched)
                    EnterFlat();
                break;

            case MapState.Flat: TickFlat(); break;
            case MapState.Switching: TickSwitching(); break;
            case MapState.Tilting: TickTilting(); break;
        }
    }

    // ── Flat ──────────────────────────────────────────────────────────────────

    private void EnterFlat()
    {
        netState.Value = MapState.Flat;
        netTilt.Value = 0f;
        stateTimer = 0f;
    }

    private void TickFlat()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer < flatHoldDuration) return;

        flatPhaseCount++;
        if (flatPhaseCount >= flatPhasesBeforeSwap)
        {
            flatPhaseCount = 0;
            EnterSwitching();
        }
        else
        {
            EnterTilting();
        }
    }

    // ── Switching ─────────────────────────────────────────────────────────────

    private void EnterSwitching()
    {
        bool incomingMap1 = !netMap1Active.Value;
        Transform incomingMap = incomingMap1 ? map1 : map2;

        foreach (Transform player in trackedPlayers)
        {
            if (player == null) continue;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.interpolation = RigidbodyInterpolation.None;
            player.SetParent(incomingMap, worldPositionStays: true);
        }

        netMap1Active.Value = !netMap1Active.Value;
        netState.Value = MapState.Switching;
        stateTimer = 0f;
    }

    private void TickSwitching()
    {
        bool active = netMap1Active.Value;
        Vector3 t1 = active ? map1UpPos : map1DownPos;
        Vector3 t2 = active ? map2DownPos : map2UpPos;

        bool done = Vector3.Distance(map1.position, t1) < 0.01f &&
                    Vector3.Distance(map2.position, t2) < 0.01f;

        if (!done) return;

        foreach (Transform player in trackedPlayers)
        {
            if (player == null) continue;
            player.SetParent(null, worldPositionStays: true);
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        EnterFlat();
    }

    // ── Tilting ───────────────────────────────────────────────────────────────

    private void EnterTilting()
    {
        targetTilt = Random.Range(minTiltAngle, maxTiltAngle);
        tiltingIn = true;
        stateTimer = 0f;
        netState.Value = MapState.Tilting;
    }

    private void TickTilting()
    {
        float current = netTilt.Value;

        if (tiltingIn)
        {
            current = Mathf.MoveTowards(current, targetTilt, tiltSpeed * Time.deltaTime);
            netTilt.Value = current;

            if (Mathf.Approximately(current, targetTilt))
            { tiltingIn = false; stateTimer = 0f; }
        }
        else
        {
            if (stateTimer < tiltHoldDuration) { stateTimer += Time.deltaTime; return; }

            current = Mathf.MoveTowards(current, 0f, tiltSpeed * Time.deltaTime);
            netTilt.Value = current;

            if (current <= 0f) { netTilt.Value = 0f; EnterFlat(); }
        }
    }

    // ── Visuals — all clients ─────────────────────────────────────────────────

    private void ApplyVisuals()
    {
        bool active = netMap1Active.Value;
        Vector3 target1 = active ? map1UpPos : map1DownPos;
        Vector3 target2 = active ? map2DownPos : map2UpPos;

        map1.position = Vector3.MoveTowards(map1.position, target1, moveSpeed * Time.deltaTime);
        map2.position = Vector3.MoveTowards(map2.position, target2, moveSpeed * Time.deltaTime);

        // Only the active (top) map tilts — bottom stays flat
        if (active)
        {
            map1.localRotation = Quaternion.Euler(netTilt.Value, 0f, 0f);
            map2.localRotation = Quaternion.identity;
        }
        else
        {
            map1.localRotation = Quaternion.identity;
            map2.localRotation = Quaternion.Euler(netTilt.Value, 0f, 0f);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public Transform GetActiveMap() => netMap1Active.Value ? map1 : map2;
    public bool CanSpawn() => netState.Value != MapState.Switching;
    public bool IsLive() => netState.Value != MapState.WaitingForLaunch;

    public Transform GetSpawnPoint()
    {
        Transform[] points = netMap1Active.Value ? map1SpawnPoints : map2SpawnPoints;
        if (points == null || points.Length == 0)
        {
            Debug.LogError("[MapManager] No spawn points assigned.");
            return null;
        }
        Transform chosen = points[spawnIndex % points.Length];
        spawnIndex = (spawnIndex + 1) % points.Length;
        return chosen;
    }

    public void RegisterPlayer(Transform playerTransform)
    {
        if (!IsServer || trackedPlayers.Contains(playerTransform)) return;
        trackedPlayers.Add(playerTransform);
    }

    public void UnregisterPlayer(Transform playerTransform)
    {
        if (!IsServer) return;
        trackedPlayers.Remove(playerTransform);
    }
}
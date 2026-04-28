using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// MapManager — Server-authoritative map switcher with spawn point management.
///
/// STATE MACHINE:
///   FLAT      — Maps rest at 0°. Spawning allowed. After flatHoldDuration, decides swap or tilt.
///   SWITCHING — Maps slide. Spawning BLOCKED. Players stay parented to active map transform.
///   TILTING   — Active map tilts and returns. Spawning allowed.
///
/// SPAWNING:
///   • Each map has an array of spawn points assigned in the Inspector.
///   • GetSpawnPoint() returns the next spawn point on the ACTIVE map (round-robin).
///   • CanSpawn() returns false during SWITCHING — callers must respect this.
///   • Players are kept on the active map during transitions by re-parenting them
///     to the active map's Transform on the server when a switch begins.
/// </summary>
public class MapManager : NetworkBehaviour
{
    // =========================================================================
    // Inspector
    // =========================================================================

    [Header("Map Transforms")]
    public Transform map1;
    public Transform map2;

    [Header("Positions")]
    public Vector3 map1UpPos;
    public Vector3 map1DownPos;
    public Vector3 map2UpPos;
    public Vector3 map2DownPos;

    [Header("Slide Settings")]
    [Tooltip("Speed (units/sec) at which maps slide between up and down positions.")]
    public float moveSpeed = 5f;

    [Header("Flat State")]
    [Tooltip("How long (seconds) both maps rest fully flat before the next action.")]
    public float flatHoldDuration = 3f;

    [Header("Switch Settings")]
    [Tooltip("How many FLAT phases must complete before a swap is triggered.")]
    public int flatPhasesBeforeSwap = 2;

    [Header("Tilt Settings")]
    [Tooltip("Minimum tilt angle on the POSITIVE X axis (degrees).")]
    public float minTiltAngle = 35f;

    [Tooltip("Maximum tilt angle on the POSITIVE X axis (degrees).")]
    public float maxTiltAngle = 40f;

    [Tooltip("Degrees per second when tilting in or out.")]
    public float tiltSpeed = 8f;

    [Tooltip("How long (seconds) the map holds at peak tilt before returning to flat.")]
    public float tiltHoldDuration = 3f;

    [Header("Spawn Points")]
    [Tooltip("Spawn points on Map 1. Assign as children of the map1 Transform.")]
    [SerializeField] private Transform[] map1SpawnPoints;

    [Tooltip("Spawn points on Map 2. Assign as children of the map2 Transform.")]
    [SerializeField] private Transform[] map2SpawnPoints;

    // =========================================================================
    // Network state  (server writes → all clients read)
    // =========================================================================

    private enum MapState : byte { Flat, Switching, Tilting }

    private NetworkVariable<MapState> netState = new NetworkVariable<MapState>(
        MapState.Flat,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// True = map1 is the active (upper) map.
    private NetworkVariable<bool> netMap1Active = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// Current tilt angle in degrees applied on the positive X axis.
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
    private int spawnIndex;          // round-robin index across active map's spawn points

    // Tracks which players are currently parented to which map (server only)
    private List<Transform> trackedPlayers = new List<Transform>();

    // =========================================================================
    // Singleton access (optional — lets other scripts call MapManager.Instance)
    // =========================================================================

    public static MapManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // =========================================================================
    // Unity / NGO lifecycle
    // =========================================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            flatPhaseCount = 0;
            spawnIndex = 0;
            EnterFlat();
        }
    }

    private void Update()
    {
        if (IsServer)
            ServerTick();

        ApplyVisuals();
    }

    // =========================================================================
    // Server tick
    // =========================================================================

    private void ServerTick()
    {
        switch (netState.Value)
        {
            case MapState.Flat: TickFlat(); break;
            case MapState.Switching: TickSwitching(); break;
            case MapState.Tilting: TickTilting(); break;
        }
    }

    // =========================================================================
    // FLAT
    // =========================================================================

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

    // =========================================================================
    // SWITCHING
    // =========================================================================

    private void EnterSwitching()
    {
        // Parent all tracked players to the INCOMING active map so they
        // ride up with it and don't fall with the outgoing map.
        bool incomingMap1Active = !netMap1Active.Value; // what it's about to become
        Transform incomingMap = incomingMap1Active ? map1 : map2;

        foreach (Transform player in trackedPlayers)
        {
            if (player != null)
                player.SetParent(incomingMap, worldPositionStays: true);
        }

        // Flip the active flag — ApplyVisuals will begin sliding both maps
        netMap1Active.Value = !netMap1Active.Value;
        netState.Value = MapState.Switching;
        stateTimer = 0f;
    }

    private void TickSwitching()
    {
        bool active = netMap1Active.Value;
        Vector3 t1 = active ? map1UpPos : map1DownPos;
        Vector3 t2 = active ? map2DownPos : map2UpPos;

        bool map1Done = Vector3.Distance(map1.position, t1) < 0.01f;
        bool map2Done = Vector3.Distance(map2.position, t2) < 0.01f;

        if (map1Done && map2Done)
        {
            // Unparent players — they now stand freely on the new active map
            foreach (Transform player in trackedPlayers)
            {
                if (player != null)
                    player.SetParent(null, worldPositionStays: true);
            }

            EnterFlat();
        }
    }

    // =========================================================================
    // TILTING
    // =========================================================================

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
            {
                tiltingIn = false;
                stateTimer = 0f;
            }
        }
        else
        {
            // Hold at peak
            if (stateTimer < tiltHoldDuration)
            {
                stateTimer += Time.deltaTime;
                return;
            }

            // Return to flat
            current = Mathf.MoveTowards(current, 0f, tiltSpeed * Time.deltaTime);
            netTilt.Value = current;

            if (current <= 0f)
            {
                netTilt.Value = 0f;
                EnterFlat();
            }
        }
    }

    // =========================================================================
    // Visual application — ALL clients
    // =========================================================================

    private void ApplyVisuals()
    {
        bool active = netMap1Active.Value;
        float tilt = netTilt.Value;

        Vector3 target1 = active ? map1UpPos : map1DownPos;
        Vector3 target2 = active ? map2DownPos : map2UpPos;

        map1.position = Vector3.MoveTowards(map1.position, target1, moveSpeed * Time.deltaTime);
        map2.position = Vector3.MoveTowards(map2.position, target2, moveSpeed * Time.deltaTime);

        Quaternion tiltRot = Quaternion.Euler(tilt, 0f, 0f);
        map1.localRotation = tiltRot;
        map2.localRotation = tiltRot;
    }

    // =========================================================================
    // PUBLIC SPAWN API  (call from your spawner / game manager on the SERVER)
    // =========================================================================

    /// <summary>
    /// Returns false during a SWITCHING phase. Always check before spawning.
    /// </summary>
    public bool CanSpawn()
    {
        return netState.Value != MapState.Switching;
    }

    /// <summary>
    /// Returns the next spawn point on the currently active map (round-robin).
    /// Returns null if no spawn points are assigned.
    /// Call only after CanSpawn() == true.
    /// </summary>
    public Transform GetSpawnPoint()
    {
        Transform[] points = netMap1Active.Value ? map1SpawnPoints : map2SpawnPoints;

        if (points == null || points.Length == 0)
        {
            Debug.LogError($"[MapManager] No spawn points assigned for the active map " +
                           $"(map{(netMap1Active.Value ? "1" : "2")}).");
            return null;
        }

        Transform chosen = points[spawnIndex % points.Length];
        spawnIndex = (spawnIndex + 1) % points.Length;
        return chosen;
    }

    /// <summary>
    /// Register a player Transform so MapManager can re-parent it during map switches.
    /// Call this on the server after the player spawns (e.g. from your spawner script).
    /// </summary>
    public void RegisterPlayer(Transform playerTransform)
    {
        if (!IsServer) return;

        if (!trackedPlayers.Contains(playerTransform))
            trackedPlayers.Add(playerTransform);
    }

    /// <summary>
    /// Unregister a player (call on disconnect / death).
    /// </summary>
    public void UnregisterPlayer(Transform playerTransform)
    {
        if (!IsServer) return;
        trackedPlayers.Remove(playerTransform);
    }
}
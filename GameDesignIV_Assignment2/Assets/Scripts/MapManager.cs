using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// MapManager — Server-authoritative map switcher.
///
/// STATE MACHINE:
///   Flat      — Both maps at rest. Active map is enabled, inactive is disabled.
///               After flatHoldDuration the machine enters Switching.
///   Switching — Inactive map is re-enabled just before sliding begins.
///               Active map slides down, incoming map slides up.
///               Once both reach their targets, inactive map is disabled and
///               the machine returns to Flat.
///
/// PERFORMANCE:
///   The inactive map's renderers and colliders are disabled while not in use
///   and re-enabled just before the switch begins. The GameObject itself stays
///   active so NGO NetworkBehaviour components are never interrupted.
///
/// SPAWN POINTS:
///   GetSpawnPoint() always returns a point from the currently ACTIVE (up) map.
///   CanSpawn() returns false during Switching.
///   IsFlat() returns true only when state is Flat AND maps have physically settled.
/// </summary>
public class MapManager : NetworkBehaviour
{
    // =========================================================================
    // Inspector
    // =========================================================================

    [Header("Map GameObjects")]
    [Tooltip("The full GameObject of map 1.")]
    public GameObject map1Object;

    [Tooltip("The full GameObject of map 2.")]
    public GameObject map2Object;

    [Header("Positions")]
    public Vector3 map1UpPos;
    public Vector3 map1DownPos;
    public Vector3 map2UpPos;
    public Vector3 map2DownPos;

    [Header("Slide Settings")]
    [Tooltip("Units per second at which maps slide between positions.")]
    public float moveSpeed = 5f;

    [Header("Flat State")]
    [Tooltip("How long (seconds) the active map stays flat before the next swap.")]
    public float flatHoldDuration = 10f;

    [Header("Spawn Points")]
    [Tooltip("Spawn points that belong to map 1. Assign as children of map1Object.")]
    [SerializeField] private Transform[] map1SpawnPoints;

    [Tooltip("Spawn points that belong to map 2. Assign as children of map2Object.")]
    [SerializeField] private Transform[] map2SpawnPoints;

    // =========================================================================
    // Network state  (server writes → all clients read)
    // =========================================================================

    private enum MapState : byte { Flat, Switching }

    private NetworkVariable<MapState> netState = new NetworkVariable<MapState>(
        MapState.Flat,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// True = map1 is the active (up) map.
    private NetworkVariable<bool> netMap1Active = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // =========================================================================
    // Cached Transform references
    // =========================================================================

    private Transform map1;
    private Transform map2;

    // =========================================================================
    // Server-only state
    // =========================================================================

    private float stateTimer;
    private int spawnIndex;
    private bool incomingEnabled = false;
    private int _playersLaunching = 0;

    private List<Transform> trackedPlayers = new List<Transform>();

    private bool AnyPlayerLaunching => _playersLaunching > 0;

    // =========================================================================
    // Singleton
    // =========================================================================

    public static MapManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        map1 = map1Object.transform;
        map2 = map2Object.transform;
    }

    // =========================================================================
    // Network spawn
    // =========================================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            spawnIndex = 0;
            EnterFlat();
        }

        ApplyActiveState();
    }

    // =========================================================================
    // Update
    // =========================================================================

    private void Update()
    {
        if (IsServer) ServerTick();
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
        }
    }

    // =========================================================================
    // FLAT
    // =========================================================================

    private void EnterFlat()
    {
        netState.Value = MapState.Flat;
        stateTimer = 0f;
        incomingEnabled = false;

        SetInactiveMapEnabled(false);
    }

    private void TickFlat()
    {
        // Pause the swap timer while any player is still mid-launch
        if (AnyPlayerLaunching) return;

        stateTimer += Time.deltaTime;
        if (stateTimer >= flatHoldDuration)
            EnterSwitching();
    }

    // =========================================================================
    // SWITCHING
    // =========================================================================

    private void EnterSwitching()
    {
        if (AnyPlayerLaunching)
        {
            Debug.Log("[MapManager] Switching deferred — player still launching.");
            return;
        }

        if (!incomingEnabled)
        {
            SetInactiveMapEnabled(true);
            incomingEnabled = true;
        }

        // Flip active flag — ApplyVisuals will start sliding both maps
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

        OnMapSwitched?.Invoke();
        EnterFlat();
    }

    // =========================================================================
    // Visuals — all clients
    // =========================================================================

    private void ApplyVisuals()
    {
        bool active = netMap1Active.Value;
        Vector3 target1 = active ? map1UpPos : map1DownPos;
        Vector3 target2 = active ? map2DownPos : map2UpPos;

        map1.position = Vector3.MoveTowards(map1.position, target1, moveSpeed * Time.deltaTime);
        map2.position = Vector3.MoveTowards(map2.position, target2, moveSpeed * Time.deltaTime);

        map1.localRotation = Quaternion.identity;
        map2.localRotation = Quaternion.identity;
    }

    // =========================================================================
    // Enable / disable inactive map
    // =========================================================================

    /// <summary>
    /// Toggles renderers and colliders on the INACTIVE map only.
    /// The GameObject itself is never disabled so NGO components stay alive.
    /// </summary>
    private void SetInactiveMapEnabled(bool enabled)
    {
        bool map1Active = netMap1Active.Value;

        if (map1Active)
            SetMap2VisibilityClientRpc(enabled);
        else
            SetMap1VisibilityClientRpc(enabled);
    }

    [ClientRpc]
    private void SetMap1VisibilityClientRpc(bool enabled)
        => SetMapVisibility(map1Object, enabled);

    [ClientRpc]
    private void SetMap2VisibilityClientRpc(bool enabled)
        => SetMapVisibility(map2Object, enabled);

    private void SetMapVisibility(GameObject mapObject, bool enabled)
    {
        if (mapObject == null) return;

        foreach (Renderer r in mapObject.GetComponentsInChildren<Renderer>(true))
            r.enabled = enabled;

        foreach (Collider c in mapObject.GetComponentsInChildren<Collider>(true))
            c.enabled = enabled;
    }

    /// <summary>
    /// Applies the correct visible/hidden state to both maps on spawn.
    /// </summary>
    private void ApplyActiveState()
    {
        bool map1Active = netMap1Active.Value;
        SetMapVisibility(map1Object, map1Active);   // active map   — visible + collidable
        SetMapVisibility(map2Object, !map1Active);   // inactive map — hidden  + no collision
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Fired on the server when a map switch completes and the new map is flat.
    /// Subscribe to react to map changes (e.g. clearing and restocking targets).
    /// </summary>
    public event System.Action OnMapSwitched;

    /// <summary>
    /// Returns false during Switching — callers must not spawn then.
    /// </summary>
    public bool CanSpawn() => netState.Value != MapState.Switching;

    /// <summary>
    /// Returns true only when the state machine is in Flat AND both map
    /// Transforms have physically reached their target positions.
    /// Use this before placing players so they are never spawned onto a
    /// still-moving map surface.
    /// </summary>
    public bool IsFlat()
    {
        if (netState.Value != MapState.Flat) return false;

        bool active = netMap1Active.Value;
        Vector3 target1 = active ? map1UpPos : map1DownPos;
        Vector3 target2 = active ? map2DownPos : map2UpPos;

        return Vector3.Distance(map1.position, target1) < 0.01f &&
               Vector3.Distance(map2.position, target2) < 0.01f;
    }

    /// <summary>
    /// Called by PlayerLauncher (via ServerRpc) when a player fires their launch.
    /// Prevents map switching until all mid-launch players have landed.
    /// </summary>
    public void NotifyPlayerLaunching()
    {
        if (!IsServer) return;
        _playersLaunching++;
        Debug.Log($"[MapManager] Player launching. Active launches: {_playersLaunching}");
    }

    /// <summary>
    /// Called by PlayerLauncher (via ServerRpc) once the player has slowed to a stop.
    /// </summary>
    public void NotifyPlayerLandedAfterLaunch()
    {
        if (!IsServer) return;
        _playersLaunching = Mathf.Max(0, _playersLaunching - 1);
        Debug.Log($"[MapManager] Player landed. Active launches: {_playersLaunching}");
    }

    /// <summary>
    /// Returns the next spawn point on the ACTIVE (up) map, round-robin.
    /// Always check CanSpawn() and IsFlat() before calling this.
    /// </summary>
    public Transform GetSpawnPoint()
    {
        Transform[] points = netMap1Active.Value ? map1SpawnPoints : map2SpawnPoints;

        if (points == null || points.Length == 0)
        {
            Debug.LogError($"[MapManager] No spawn points on active map " +
                           $"(map{(netMap1Active.Value ? "1" : "2")}).");
            return null;
        }

        Transform chosen = points[spawnIndex % points.Length];
        spawnIndex = (spawnIndex + 1) % points.Length;
        return chosen;
    }

    /// <summary>Returns the Transform of whichever map is currently active (up).</summary>
    public Transform GetActiveMap() => netMap1Active.Value ? map1 : map2;

    /// <summary>
    /// Register a player Transform so MapManager can track it.
    /// Call on the server after the player spawns.
    /// </summary>
    public void RegisterPlayer(Transform playerTransform)
    {
        if (!IsServer || trackedPlayers.Contains(playerTransform)) return;
        trackedPlayers.Add(playerTransform);
    }

    /// <summary>Unregister on disconnect or death.</summary>
    public void UnregisterPlayer(Transform playerTransform)
    {
        if (!IsServer) return;
        trackedPlayers.Remove(playerTransform);
    }
}
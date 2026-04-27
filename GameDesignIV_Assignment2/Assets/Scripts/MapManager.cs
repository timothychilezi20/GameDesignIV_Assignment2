using UnityEngine;
using Unity.Netcode;

/// <summary>
/// MapManager — Server-authoritative map switcher with a clean 3-state cycle.
///
/// STATE MACHINE (server-driven):
///
///   FLAT
///     • Both maps are at rest (0° tilt).
///     • After flatHoldDuration seconds the machine decides: swap or tilt?
///     • A swap is triggered every [flatPhasesBeforeSwap] flat phases.
///     • Otherwise, a TILTING phase runs first.
///
///   SWITCHING
///     • Can only be entered from FLAT.
///     • Active map slides down; new active map slides up. Both stay at 0° tilt.
///     • Returns to FLAT once both maps reach their destination.
///
///   TILTING
///     • Can only be entered from FLAT (swap not yet due).
///     • Active map tilts to a random angle in [minTiltAngle, maxTiltAngle] on the
///       POSITIVE X axis, holds for tiltHoldDuration, then returns to 0°.
///     • Inactive map mirrors the tilt for visual cohesion.
///     • Returns to FLAT once tilt is back to 0°.
///
/// Networking:
///     • NetworkVariables replicate all driving state to every client.
///     • Server runs the state machine; clients read and apply visuals each frame.
/// </summary>
public class MapManager : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Network state  (server writes → all clients read)
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Server-only state
    // -------------------------------------------------------------------------

    private float stateTimer;       // general timer for the current state
    private float targetTilt;       // tilt angle chosen when entering TILTING
    private int flatPhaseCount;   // flat phases completed since last swap
    private bool tiltingIn;        // true = moving toward targetTilt, false = holding/returning

    // -------------------------------------------------------------------------
    // Unity / NGO lifecycle
    // -------------------------------------------------------------------------

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            flatPhaseCount = 0;
            EnterFlat();
        }
    }

    private void Update()
    {
        if (IsServer)
            ServerTick();

        // All clients (including host) apply visuals every frame
        ApplyVisuals();
    }

    // -------------------------------------------------------------------------
    // Server tick — drives the state machine
    // -------------------------------------------------------------------------

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

        // Flat hold complete — decide what comes next
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
        // Flip active map immediately; ApplyVisuals will start sliding them
        netMap1Active.Value = !netMap1Active.Value;
        netState.Value = MapState.Switching;
        stateTimer = 0f;
    }

    private void TickSwitching()
    {
        // Wait until both maps have arrived at their new positions
        bool active = netMap1Active.Value;
        Vector3 t1 = active ? map1UpPos : map1DownPos;
        Vector3 t2 = active ? map2DownPos : map2UpPos;

        bool map1Arrived = Vector3.Distance(map1.position, t1) < 0.01f;
        bool map2Arrived = Vector3.Distance(map2.position, t2) < 0.01f;

        if (map1Arrived && map2Arrived)
            EnterFlat();
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
            // --- Phase 1: tilt toward target ---
            current = Mathf.MoveTowards(current, targetTilt, tiltSpeed * Time.deltaTime);
            netTilt.Value = current;

            if (Mathf.Approximately(current, targetTilt))
            {
                tiltingIn = false;   // switch to hold + return phase
                stateTimer = 0f;
            }
        }
        else
        {
            // --- Phase 2: hold at peak ---
            if (stateTimer < tiltHoldDuration)
            {
                stateTimer += Time.deltaTime;
                return;
            }

            // --- Phase 3: return to flat ---
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
    // Visual application — runs on ALL clients every frame
    // =========================================================================

    private void ApplyVisuals()
    {
        bool active = netMap1Active.Value;
        float tilt = netTilt.Value;

        // --- Slide positions ---
        Vector3 target1 = active ? map1UpPos : map1DownPos;
        Vector3 target2 = active ? map2DownPos : map2UpPos;

        map1.position = Vector3.MoveTowards(map1.position, target1, moveSpeed * Time.deltaTime);
        map2.position = Vector3.MoveTowards(map2.position, target2, moveSpeed * Time.deltaTime);

        // --- Tilt rotation (positive X axis) ---
        // Both maps share the same tilt value so the inactive map mirrors the active one,
        // preventing it from visually clashing or tilting into the active map.
        Quaternion tiltRot = Quaternion.Euler(tilt, 0f, 0f);
        map1.localRotation = tiltRot;
        map2.localRotation = tiltRot;
    }
}
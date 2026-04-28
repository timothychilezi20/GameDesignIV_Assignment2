using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// PlayerController — all movement and rotation driven exclusively through Rigidbody.
///
/// Movement:  rb.linearVelocity  (grounded = surface-projected, airborne = reduced)
/// Rotation:  rb.MoveRotation    (faces mouse cursor each FixedUpdate)
/// Launch:    rb.AddForce        (impulse, then physics takes over until speed drops)
///
/// Transform.position and Transform.rotation are NEVER written directly.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    // =========================================================================
    // Inspector
    // =========================================================================

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask mapLayerMask = ~0;

    [Header("Laser")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private LineRenderer laserLineRenderer;

    [Header("Launch")]
    [SerializeField] private float launchSpeedThreshold = 3f;

    // =========================================================================
    // Private state
    // =========================================================================

    private Rigidbody rb;
    private Camera cam;

    private Vector3 moveInput;
    private Vector2 lookInput;

    private bool _isLaunching = false;
    private bool _launched = false;
    private bool _movementLocked = true;
    private bool isStunned = false;

    private Vector3 _surfaceNormal = Vector3.up;
    private bool _isGrounded = false;
    private Vector3 _launchDirection = Vector3.forward;

    private float speedMultiplier = 1f;
    public int playerNumber;

    private NetworkVariable<bool> isInvincibleNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool IsInvincible => isInvincibleNet.Value;

    // =========================================================================
    // Init
    // =========================================================================

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;   // we control rotation via rb.MoveRotation
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    public override void OnNetworkSpawn()
    {
        playerNumber = OwnerClientId == 0 ? 1 : 2;

        if (IsOwner)
            StartCoroutine(InitOwner());
        else
        {
            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;
        }
    }

    private IEnumerator InitOwner()
    {
        yield return null; // let PlayerCamera.OnNetworkSpawn run first

        PlayerCamera playerCam = GetComponent<PlayerCamera>();
        if (playerCam != null)
            cam = playerCam.GetCamera();

        if (cam == null)
        {
            cam = Camera.main;
            Debug.LogWarning("[PlayerController] Falling back to Camera.main.");
        }
        if (cam == null)
            Debug.LogError("[PlayerController] No camera found.");

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        yield return null; // second frame — LaserManager is ready

        if (LaserManager.Instance == null) { Debug.LogError("LaserManager null"); yield break; }
        RegisterWithLaserManagerServerRpc(playerNumber);
    }

    [ServerRpc]
    private void RegisterWithLaserManagerServerRpc(int pNumber)
    {
        if (LaserManager.Instance == null) { Debug.LogError("LaserManager null on server"); return; }
        LaserManager.Instance.RegisterPlayer(pNumber, laserOrigin, laserLineRenderer, this);
    }

    // =========================================================================
    // Update — input reads only, no physics writes here
    // =========================================================================

    void Update()
    {
        // Nothing physics-related in Update — all rb writes happen in FixedUpdate
    }

    // =========================================================================
    // FixedUpdate — single place where ALL rb writes happen
    // =========================================================================

    void FixedUpdate()
    {
        if (!IsOwner || _movementLocked || !_launched) return;

        CheckGround();
        RotateTowardMouse();

        if (_isLaunching)
        {
            float hSpeed = new Vector3(
                rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

            if (hSpeed <= launchSpeedThreshold)
            {
                _isLaunching = false;
                Debug.Log("[PlayerController] Launch complete — resuming normal movement.");
            }
            return; // physics controls movement during launch
        }

        ApplyMovement();
    }

    // =========================================================================
    // Ground check
    // =========================================================================

    private void CheckGround()
    {
        Vector3 origin = rb.position + Vector3.up * 0.1f;

        if (Physics.SphereCast(origin, 0.25f, Vector3.down, out RaycastHit hit,
                               groundCheckDistance + 0.1f, mapLayerMask))
        {
            _isGrounded = true;
            _surfaceNormal = hit.normal;
        }
        else
        {
            _isGrounded = false;
            _surfaceNormal = Vector3.up;
        }
    }

    // =========================================================================
    // Movement — rb.linearVelocity only
    // =========================================================================

    private void ApplyMovement()
    {
        if (!_isGrounded)
        {
            // Reduced air control
            rb.linearVelocity = new Vector3(
                moveInput.x * moveSpeed * speedMultiplier * 0.3f,
                rb.linearVelocity.y,
                moveInput.z * moveSpeed * speedMultiplier * 0.3f
            );
            return;
        }

        // Project movement onto the surface so the player walks flush with tilted maps
        Vector3 right = Vector3.ProjectOnPlane(Vector3.right, _surfaceNormal).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, _surfaceNormal).normalized;
        Vector3 move = (right * moveInput.x + forward * moveInput.z)
                          * moveSpeed * speedMultiplier;

        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);
    }

    // =========================================================================
    // Rotation — rb.MoveRotation only
    // =========================================================================

    private void RotateTowardMouse()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(lookInput);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, rb.position.y, 0f));

        if (!groundPlane.Raycast(ray, out float distance)) return;

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector3 direction = worldPoint - rb.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(direction);

        Quaternion newRot = rotationSpeed <= 0f
            ? targetRot
            : Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * rotationSpeed);

        rb.MoveRotation(newRot);
    }

    // =========================================================================
    // Input callbacks
    // =========================================================================

    void OnMove(InputValue value)
    {
        if (!IsOwner) return;
        Vector2 input = value.Get<Vector2>();
        moveInput = new Vector3(input.x, 0f, input.y).normalized;
    }

    void OnLook(InputValue value)
    {
        if (!IsOwner) return;
        lookInput = value.Get<Vector2>();
    }

    // =========================================================================
    // Launch
    // =========================================================================

    public void ExecuteLaunch(Vector3 direction, float force)
    {
        _launched = true;
        _isLaunching = true;
        _movementLocked = false;
        rb.useGravity = true;

        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY
                       | RigidbodyConstraints.FreezeRotationZ;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        Debug.Log($"[PlayerController] Launched — direction:{direction} force:{force}");
    }

    public void SetLaunchDirection(Vector3 direction) => _launchDirection = direction.normalized;
    public Vector3 GetLaunchDirection() => _launchDirection;

    public void SetMovementLocked(bool locked)
    {
        _movementLocked = locked;

        if (locked)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        else
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    public void LaunchFromServer(Vector3 direction, float force)
        => LaunchClientRpc(direction, force);

    [ClientRpc]
    private void LaunchClientRpc(Vector3 direction, float force)
    {
        if (!IsOwner) return;
        ExecuteLaunch(direction, force);
    }

    // =========================================================================
    // Spawn placement — uses rb.position + rb.rotation, never Transform
    // =========================================================================

    public void PlaceAtSpawnPoint(Vector3 position, Quaternion rotation)
    {
        _launchDirection = rotation * Vector3.forward;
        _launched = false;
        _isLaunching = false;
        _movementLocked = true;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        // Raycast down from above the spawn point to land cleanly on the surface
        Vector3 correctedPos = position;
        Vector3 rayOrigin = position + Vector3.up * 2f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4f, mapLayerMask))
        {
            correctedPos = hit.point + Vector3.up * 0.6f;
            Debug.Log($"[PlayerController] Surface at {hit.point} — placing at {correctedPos}");
        }
        else
        {
            correctedPos = new Vector3(position.x, position.y + 0.6f, position.z);
            Debug.LogWarning("[PlayerController] No surface under spawn point — using offset.");
        }

        // Use rb.position and rb.rotation — no Transform writes
        rb.position = correctedPos;
        rb.rotation = rotation;
    }

    // =========================================================================
    // Trap / stun API
    // =========================================================================

    public void ApplySpeedMultiplier(float multiplier) => speedMultiplier = multiplier;
    public void ResetSpeedMultiplier() => speedMultiplier = 1f;

    public void ApplyStun(float stunDuration, float invincibleDuration)
        => ApplyStunClientRpc(stunDuration, invincibleDuration);

    [ClientRpc]
    private void ApplyStunClientRpc(float stunDuration, float invincibleDuration)
    {
        if (!IsOwner || isStunned || isInvincibleNet.Value) return;
        StartCoroutine(StunRoutine(stunDuration, invincibleDuration));
    }

    private IEnumerator StunRoutine(float stunDuration, float invincibleDuration)
    {
        isStunned = true;
        ApplySpeedMultiplier(0f);
        yield return new WaitForSeconds(stunDuration);

        isStunned = false;
        isInvincibleNet.Value = true;
        ResetSpeedMultiplier();
        yield return new WaitForSeconds(invincibleDuration);

        isInvincibleNet.Value = false;
    }

    // =========================================================================
    // Accessors
    // =========================================================================

    public LineRenderer GetLaserLineRenderer() => laserLineRenderer;
    public Transform GetLaserOrigin() => laserOrigin;

    // =========================================================================
    // Editor gizmos
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;
        Vector3 end = origin + _launchDirection * 5f;

        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawLine(origin, end, 2f);
        UnityEditor.Handles.ArrowHandleCap(
            0, origin,
            Quaternion.LookRotation(_launchDirection),
            5f, EventType.Repaint);
        UnityEditor.Handles.Label(end, $"Launch: {_launchDirection:F2}");
    }
#endif
}
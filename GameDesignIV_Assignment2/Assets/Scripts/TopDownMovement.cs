using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask mapLayerMask = ~0;

    [Header("Laser")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private LineRenderer laserLineRenderer;

    private Rigidbody rb;
    private Camera cam;
    private Vector3 moveInput;
    private Vector2 lookInput;

    [Header("Launch")]
    [SerializeField] private float launchSpeedThreshold = 3f; // resume normal movement below this speed

    private bool _isLaunching = false;
    private bool isStunned = false;
    private bool _launched = false;
    private bool _movementLocked = true;

    private NetworkVariable<bool> isInvincibleNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool IsInvincible => isInvincibleNet.Value;

    private float speedMultiplier = 1f;
    public int playerNumber;

    private Vector3 _surfaceNormal = Vector3.up;
    private bool _isGrounded = false;

    private Vector3 _launchDirection = Vector3.forward;

    // =========================================================================
    // Init
    // =========================================================================

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    public override void OnNetworkSpawn()
    {
        playerNumber = OwnerClientId == 0 ? 1 : 2;

        if (IsOwner)
        {
            // Wait a frame for PlayerCamera to activate the top-down camera
            // before we try to find it
            StartCoroutine(InitOwner());
        }
        else
        {
            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;
        }
    }

    private IEnumerator InitOwner()
    {
        // Frame 1: let PlayerCamera.OnNetworkSpawn run and activate the camera
        yield return null;

        // Grab the camera from the sibling PlayerCamera component so we always
        // get the camera that belongs to THIS player, not Camera.main which
        // could be ambiguous in a two-player scene
        PlayerCamera playerCam = GetComponent<PlayerCamera>();
        if (playerCam != null)
        {
            // Access the camera through the component using the serialized field
            // via a public getter we'll add to PlayerCamera (see below)
            cam = playerCam.GetCamera();
        }

        // Fallback to Camera.main if no PlayerCamera found
        if (cam == null)
        {
            cam = Camera.main;
            Debug.LogWarning("[PlayerController] Could not get camera from PlayerCamera — " +
                             "falling back to Camera.main.");
        }

        if (cam == null)
            Debug.LogError("[PlayerController] No camera found.");

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        // Frame 2: register with LaserManager
        yield return null;

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
    // Update / FixedUpdate
    // =========================================================================

    void Update()
    {
        if (!IsOwner) return;
        FaceMouseCursor();
    }

    void FixedUpdate()
    {
        if (!IsOwner || _movementLocked || !_launched) return;

        CheckGround();

        // While launching, let physics handle movement naturally
        // Only resume player control once velocity slows below threshold
        if (_isLaunching)
        {
            float horizontalSpeed = new Vector3(
                rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

            if (horizontalSpeed <= launchSpeedThreshold)
            {
                _isLaunching = false;
                Debug.Log("[PlayerController] Launch complete, resuming normal movement");
            }
            return; // don't apply movement input during launch
        }

        ApplyMovement();
    }

    // =========================================================================
    // Ground check
    // =========================================================================

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;

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
    // Movement
    // =========================================================================

    private void ApplyMovement()
    {
        if (!_isGrounded)
        {
            rb.linearVelocity = new Vector3(
                moveInput.x * moveSpeed * speedMultiplier * 0.3f,
                rb.linearVelocity.y,
                moveInput.z * moveSpeed * speedMultiplier * 0.3f
            );
            return;
        }

        Vector3 right = Vector3.ProjectOnPlane(Vector3.right, _surfaceNormal).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, _surfaceNormal).normalized;
        Vector3 move = (right * moveInput.x + forward * moveInput.z) * moveSpeed * speedMultiplier;

        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);
    }

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
    // Face mouse — updated for top-down camera
    // =========================================================================

    void FaceMouseCursor()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(lookInput);

        // For a top-down camera the ground plane sits at the player's Y position
        // so the rotation stays flat regardless of map tilt
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 direction = worldPoint - transform.position;

            // Only rotate on the horizontal plane — ignore any Y component so
            // the player never tilts to look up/down at the camera
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                transform.rotation = rotationSpeed <= 0f
                    ? targetRotation
                    : Quaternion.Slerp(transform.rotation, targetRotation,
                                       Time.deltaTime * rotationSpeed);
            }
        }
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

        Debug.Log($"[PlayerController] Launched in direction {direction} with force {force}");
    }

    public void SetLaunchDirection(Vector3 direction)
    {
        _launchDirection = direction.normalized;
    }

    public Vector3 GetLaunchDirection() => _launchDirection;

    public void SetMovementLocked(bool locked)
    {
        _movementLocked = locked;

        if (locked)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;

            // Freeze position while locked so player stays put
            rb.constraints = RigidbodyConstraints.FreezePosition
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            // Restore normal constraints when unlocked
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

    public LineRenderer GetLaserLineRenderer() => laserLineRenderer;
    public Transform GetLaserOrigin() => laserOrigin;

    public void PlaceAtSpawnPoint(Vector3 position, Quaternion rotation)
    {

        _launchDirection = rotation * Vector3.forward;
        // Reset all launch state
        _launched = false;
        _isLaunching = false;
        _movementLocked = true;

        // Fully stop physics
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        // Raycast up from the spawn point to find the surface
        // This corrects for spawn points placed inside or below geometry
        Vector3 correctedPosition = position;
        Vector3 rayOrigin = position + Vector3.up * 2f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4f, mapLayerMask))
        {
            // Place the player on top of the surface with a small offset
            correctedPosition = hit.point + Vector3.up * 0.6f;
            Debug.Log($"[PlayerController] Surface found at {hit.point}, placing at {correctedPosition}");
        }
        else
        {
            // No surface found — use spawn point Y but add capsule half height
            correctedPosition = new Vector3(position.x, position.y + 0.6f, position.z);
            Debug.LogWarning("[PlayerController] No surface found under spawn point — using offset position");
        }

        transform.SetPositionAndRotation(correctedPosition, rotation);
        rb.position = correctedPosition;
        rb.rotation = rotation;
    }
}
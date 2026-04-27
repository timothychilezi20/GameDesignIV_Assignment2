using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Laser")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private LineRenderer laserLineRenderer;

    private Rigidbody rb;
    private Camera cam;
    private Vector3 moveInput;
    private Vector2 lookInput;

    private bool isStunned = false;
    private bool isInvincible = false;
    private NetworkVariable<bool> isInvincibleNet = new NetworkVariable<bool>(
      false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool IsInvincible => isInvincibleNet.Value;

    private float speedMultiplier = 1f;
    public int playerNumber;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnNetworkSpawn()
    {
        playerNumber = OwnerClientId == 0 ? 1 : 2;

        if (IsOwner)
        {
            cam = Camera.main;

            if (cam == null)
                Debug.LogError("Camera.main is null");

            // Wait a frame before registering so NetworkManager is fully ready
            StartCoroutine(RegisterAfterDelay());

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }

        if (!IsOwner)
        {
            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null)
                input.enabled = false;
        }
    }


    private IEnumerator RegisterAfterDelay()
    {
        yield return null; // wait one frame
        RegisterWithLaserManagerServerRpc(playerNumber);
    }

    [ServerRpc]
    private void RegisterWithLaserManagerServerRpc(int pNumber)
    {
        if (LaserManager.Instance == null)
        {
            Debug.LogError("LaserManager Instance is null on server");
            return;
        }

        LaserManager.Instance.RegisterPlayer(pNumber, laserOrigin, laserLineRenderer, this);
    }

    void Update()
    {
        // Only the owning client rotates toward their own mouse
        if (!IsOwner) return;
        FaceMouseCursor();
    }

    void FixedUpdate()
    {
        // Only the owning client moves their own player
        if (!IsOwner) return;

        rb.linearVelocity = new Vector3(
            moveInput.x * moveSpeed * speedMultiplier,
            rb.linearVelocity.y,
            moveInput.z * moveSpeed * speedMultiplier
        );
    }

    // Input callbacks — only fire on the owning client
    // because PlayerInput is disabled on non-owners above
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

    void FaceMouseCursor()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(lookInput);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 direction = worldPoint - transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                if (rotationSpeed <= 0f)
                    transform.rotation = targetRotation;
                else
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        Time.deltaTime * rotationSpeed
                    );
            }
        }
    }

    // Trap functions — called by server via LaserManager and trap scripts
    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public void ResetSpeedMultiplier()
    {
        speedMultiplier = 1f;
    }

    public void ApplyStun(float stunDuration, float invincibleDuration)
    {
        // Don't check local bools here — they're only accurate on the owning client
        // The ClientRpc guard handles double-stun prevention
        ApplyStunClientRpc(stunDuration, invincibleDuration);
    }

    [ClientRpc]
    private void ApplyStunClientRpc(float stunDuration, float invincibleDuration)
    {
        if (!IsOwner) return;
        // Guard runs here where the bools are actually accurate
        if (isStunned || isInvincibleNet.Value) return;
        StartCoroutine(StunRoutine(stunDuration, invincibleDuration));
    }

    private IEnumerator StunRoutine(float stunDuration, float invincibleDuration)
    {
        isStunned = true;
        ApplySpeedMultiplier(0f);
        yield return new WaitForSeconds(stunDuration);

        isStunned = false;
        isInvincibleNet.Value = true; // synced to server so laser checks work
        ResetSpeedMultiplier();
        yield return new WaitForSeconds(invincibleDuration);

        isInvincibleNet.Value = false;
    }

    public LineRenderer GetLaserLineRenderer()
    {
        return laserLineRenderer;
    }

    public Transform GetLaserOrigin()
    {
        return laserOrigin;
    }
    //[ServerRpc]

}



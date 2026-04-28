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

    private NetworkVariable<bool> isInvincibleNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool IsInvincible => isInvincibleNet.Value;

    private float speedMultiplier = 1f;
    public int playerNumber;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false; // gravity off until launched
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnNetworkSpawn()
    {
        playerNumber = OwnerClientId == 0 ? 1 : 2;
        Debug.Log($"OnNetworkSpawn — OwnerClientId: {OwnerClientId} playerNumber: {playerNumber} IsOwner: {IsOwner}");
        if (IsOwner)
        {
            cam = Camera.main;

            if (cam == null)
                Debug.LogError("Camera.main is null — ensure camera is tagged MainCamera");

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;

            // Delay registration by one frame so NetworkManager is fully ready
            StartCoroutine(RegisterAfterDelay());
        }
        else
        {
            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null)
                input.enabled = false;
        }
    }

    private IEnumerator RegisterAfterDelay()
    {
        yield return null;
        yield return null; // two frames to be safe

        if (LaserManager.Instance == null)
        {
            Debug.LogError("LaserManager Instance is null");
            yield break;
        }

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
        
       
        if (!IsOwner) return;
        FaceMouseCursor();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        rb.linearVelocity = new Vector3(
            moveInput.x * moveSpeed * speedMultiplier,
            rb.linearVelocity.y,
            moveInput.z * moveSpeed * speedMultiplier
        );
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

    public void ApplySpeedMultiplier(float multiplier) => speedMultiplier = multiplier;
    public void ResetSpeedMultiplier() => speedMultiplier = 1f;

    public void ApplyStun(float stunDuration, float invincibleDuration)
    {
        ApplyStunClientRpc(stunDuration, invincibleDuration);
    }

    public void Launch(Vector3 direction, float force)
    {
        // Unlock Y so players can fly and bounce freely
        ///rb.constraints = RigidbodyConstraints.FreezeRotationX
                       //| RigidbodyConstraints.FreezeRotationZ;

        rb.useGravity = true;

        // Zero out any residual velocity then apply the launch impulse
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    [ClientRpc]
    private void ApplyStunClientRpc(float stunDuration, float invincibleDuration)
    {
        if (!IsOwner) return;
        if (isStunned || isInvincibleNet.Value) return;
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
}


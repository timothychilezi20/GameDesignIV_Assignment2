using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class NetworkThirdPersonCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float cameraDistance = 4f;
    [SerializeField] private float collisionBuffer = 0.2f;
    [SerializeField] private LayerMask collisionMask;

    private InputAction lookAction;

    private float yaw;
    private float pitch;

    public override void OnNetworkSpawn()
    {
        lookAction = InputSystem.actions.FindAction("Look");

        if (!IsOwner)
        {
            playerCamera.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        playerCamera.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleLook();
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        FollowPlayer();
    }

    private void HandleLook()
    {
        // 🚨 PAUSE CHECK (THIS IS THE FIX)
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused)
            return;

        Vector2 look = lookAction.ReadValue<Vector2>() * sensitivity;

        yaw += look.x;
        pitch -= look.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        SubmitRotationServerRpc(yaw, pitch);
    }

    private void FollowPlayer()
    {
        Vector3 target = transform.position + Vector3.up * 1.5f;

        Vector3 desiredDir = (cameraPivot.position - target).normalized;

        if (desiredDir == Vector3.zero)
            desiredDir = -transform.forward;

        float desiredDistance = cameraDistance;

        Vector3 finalPosition = target + desiredDir * desiredDistance;

        if (Physics.SphereCast(
            target,
            0.2f,
            desiredDir,
            out RaycastHit hit,
            cameraDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            finalPosition = target + desiredDir * (hit.distance - collisionBuffer);
        }

        cameraPivot.position = finalPosition;
    }

    [ServerRpc]
    private void SubmitRotationServerRpc(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = newPitch;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        UpdateRotationClientRpc(newYaw, newPitch);
    }

    [ClientRpc]
    private void UpdateRotationClientRpc(float newYaw, float newPitch)
    {
        if (IsOwner) return;

        yaw = newYaw;
        pitch = newPitch;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class NetworkFPSPlayer : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;

    [Header("Player Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private float maxPitch = 80f;

    private PlayerInput playerInput; 
    private InputAction moveAction;
    private InputAction lookAction;
    private CharacterController characterController;

    private float pitch; 

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (!IsOwner)
        {
            if (playerCamera) playerCamera.enabled = false;
            if (playerInput) playerInput.enabled = false;
            enabled = false;
            return;
        }

        if (characterController == null)
        {
            Debug.LogError("CharacterController component is missing.", this);
            enabled = false;
            return;
        }

        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component is missing.", this);
            enabled = false;
            return;
        }

        if (playerInput.actions == null)
        {
            Debug.LogError("PlayerInput actions are not set up.", this);
            enabled = false;
            return;
        }

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];

        if (moveAction == null)
        {
            Debug.LogError("Move action is not defined in PlayerInput actions.", this);
            enabled = false;
            return;
        }

        if (lookAction == null)
        {
            Debug.LogError("Look action is not defined in PlayerInput actions.", this);
            enabled = false;
            return;
        }

        if (cameraPivot == null)
        {
            Debug.LogError("Camera Pivot is not assigned.", this);
            enabled = false;
            return;
        }

        moveAction.Enable();
        lookAction.Enable();

        if (playerCamera != null)
        {
            playerCamera.enabled = true;
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
        {
            return; 
        }

        if (moveAction == null || lookAction == null || characterController == null || cameraPivot == null)
        {
            return; 
        }

        Vector2 m = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * m.x + transform.forward * m.y;
        characterController.Move(move * moveSpeed * Time.deltaTime);

        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, look.x, 0f);

        pitch -= look.y;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);

        Debug.Log(moveAction.ReadValue<Vector2>());
    }
}

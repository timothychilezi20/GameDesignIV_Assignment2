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

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.18f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    private PlayerInput playerInput;
    private CharacterController characterController;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction dashAction; 
   
    private float pitch;
    private float verticalVelocity;

    private bool isDashing;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private Vector3 dashDirection; 

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
        jumpAction = playerInput.actions["Jump"];
        dashAction = playerInput.actions["Dash"];

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

        if (jumpAction == null)
        {
            Debug.LogError("Jump action is not defined in PlayerInput actions.", this);
            enabled = false;
            return; 
        }

        if (cameraPivot == null)
        {
            Debug.LogError("Camera Pivot is not assigned.", this);
            enabled = false;
            return;
        }

        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        dashAction?.Enable();

        if (playerCamera != null)
        {
            playerCamera.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        dashAction?.Disable();
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
        {
            return; 
        }

        if (moveAction == null || lookAction == null || characterController == null || cameraPivot == null || jumpAction == null || dashAction == null)
        {
            return; 
        }

        HandleLook();
        HandleMovement(); 

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

    private void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch -= look.y;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.magnitude > 1f)
        {
            move.Normalize();
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; 
        }

        if (jumpAction.WasPressedThisFrame() && characterController.isGrounded && !isDashing)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); 
        }

        if (dashCooldownRemaining > 0f)
        {
            dashCooldownRemaining -= Time.deltaTime;
        }

        if (dashAction.WasPressedThisFrame() && dashCooldownRemaining <= 0f && !isDashing)
        {
            isDashing = true;
            dashTimeRemaining = dashDuration;
            dashCooldownRemaining = dashCooldown;

            dashDirection = move.sqrMagnitude > 0.01f ? move : transform.forward;
            dashDirection.y = 0f;
            dashDirection.Normalize();
        }

        Vector3 horizontalVelocity; 

        if (isDashing)
        {
            horizontalVelocity = dashDirection * dashSpeed;
            dashTimeRemaining -= Time.deltaTime;

            if (dashTimeRemaining <= 0f)
            {
                isDashing = false;
            }
        }

        else
        {
            horizontalVelocity = move * moveSpeed;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = horizontalVelocity;
        finalMove.y = verticalVelocity;

        characterController.Move(finalMove * Time.deltaTime);
    }
}

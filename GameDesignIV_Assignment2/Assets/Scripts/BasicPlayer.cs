using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem; 

public class BasicPlayer : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.18f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    private CharacterController characterController;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private Vector3 velocity;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection; 

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return; 
        }

        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        dashAction = playerInput.actions["Dash"];

        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return; 
        }

        moveAction?.Disable();
        jumpAction?.Disable();
        dashAction?.Disable();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
        {
            return; 
        }

        HandleMovement();
        HandleJump();
        HandleDash();
        ApplyGravity(); 
    }

    private void HandleMovement()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        if (move.magnitude > 1f)
        {
            move.Normalize();
        }

        if (isDashing)
        {
            characterController.Move(dashDirection * dashSpeed * Time.deltaTime);
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }
        else
        {
            characterController.Move(move * speed * Time.deltaTime);

            if (move != Vector3.zero)
            {
                transform.forward = move;
            }

            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= Time.deltaTime;
            }

            if (dashAction.WasPressedThisFrame() && dashCooldownTimer <= 0f)
            {
                dashDirection = move != Vector3.zero ? move : transform.forward;
                isDashing = true;
                dashTimer = dashDuration;
                dashCooldownTimer = dashCooldown;
            }
        }
    }

    private void HandleJump()
    {
        if (characterController.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (jumpAction.WasPressedThisFrame() && characterController.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleDash()
    {
        // Dash logic is handled in HandleMovement for better integration with movement
    }
}

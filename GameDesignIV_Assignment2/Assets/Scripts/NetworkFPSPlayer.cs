using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class NetworkFPSPlayer : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("UI")]
    [SerializeField] private Slider healthBarUI;

    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerInput playerInput;
    private CharacterController characterController;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private float verticalVelocity;

    private bool isDashing;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private Vector3 dashDirection;

    [SerializeField] private MapManager mapManager;

    public bool IsAlive { get; private set; } = true;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        currentHealth.OnValueChanged += OnHealthChanged;

        if (!IsOwner)
        {
            if (playerInput != null) playerInput.enabled = false;
            if (healthBarUI != null) healthBarUI.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        dashAction = playerInput.actions["Dash"];

        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();

        if (healthBarUI != null)
        {
            healthBarUI.gameObject.SetActive(true);
            healthBarUI.maxValue = maxHealth;
            healthBarUI.value = currentHealth.Value;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (IsServer && mapManager != null)
        {
            Transform spawn = mapManager.GetActiveLaunchPoint();

            characterController.enabled = false;
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            characterController.enabled = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;

        if (!IsOwner) return;

        moveAction?.Disable();
        jumpAction?.Disable();
        dashAction?.Disable();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * input.y + right * input.x;

        if (move.magnitude > 1f)
            move.Normalize();

        // Gravity
        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        // Jump
        if (jumpAction.WasPressedThisFrame() && characterController.isGrounded && !isDashing)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Dash cooldown
        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining -= Time.deltaTime;

        // Dash start
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
                isDashing = false;
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

    private void OnHealthChanged(float previousValue, float newValue)
    {
        if (healthBarUI != null && IsOwner)
        {
            healthBarUI.maxValue = maxHealth;
            healthBarUI.value = newValue;
        }

        IsAlive = newValue > 0f;
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        currentHealth.Value -= damage;
        currentHealth.Value = Mathf.Clamp(currentHealth.Value, 0f, maxHealth);

        if (currentHealth.Value <= 0f)
        {
            currentHealth.Value = 0f;
            IsAlive = false;
            Die();
        }
    }

    public void AddHealth(float addedHealth)
    {
        if (!IsServer) return;

        currentHealth.Value += addedHealth;
        currentHealth.Value = Mathf.Clamp(currentHealth.Value, 0f, maxHealth);

        if (currentHealth.Value > 0f)
            IsAlive = true;
    }

    private void Die()
    {
        Debug.Log("Player died!");

        currentHealth.Value = maxHealth;
        IsAlive = true;

        transform.position = Vector3.zero;

        verticalVelocity = 0f;
        isDashing = false;
        dashTimeRemaining = 0f;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
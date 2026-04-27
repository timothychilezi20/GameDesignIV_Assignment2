using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

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
    private InputAction pauseAction;
    private InputAction lookAction; 

    private float verticalVelocity;

    private bool isDashing;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private Vector3 dashDirection;

    private MapManager mapManager;

    public bool IsAlive { get; private set; } = true;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        mapManager = MapManager.Instance;

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
        pauseAction = playerInput.actions["Pause"];
        lookAction = playerInput.actions["Look"];

        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
        pauseAction.Enable();
        lookAction.Enable();

        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.Initialize(playerInput);
        }

        if (healthBarUI != null)
        {
            healthBarUI.gameObject.SetActive(true);
            healthBarUI.maxValue = maxHealth;
            healthBarUI.value = currentHealth.Value;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (IsServer)
        {
            StartCoroutine(DelayedSpawn());
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;

        if (!IsOwner) return;

        moveAction?.Disable();
        jumpAction?.Disable();
        dashAction?.Disable();
        pauseAction?.Disable();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        if (pauseAction != null && pauseAction.WasPressedThisFrame())
        {
            if (PauseMenu.Instance != null)
            {
                PauseMenu.Instance.TogglePause();

                if (PauseMenu.Instance.IsPaused)
                {
                    lookAction.Disable();
                }
                else
                {
                    lookAction.Enable();
                }
            }
                
        }

        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused)
            return;

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

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpAction.WasPressedThisFrame() && characterController.isGrounded && !isDashing)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining -= Time.deltaTime;

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

        verticalVelocity = 0f;
        isDashing = false;
        dashTimeRemaining = 0f;

        SetSpawnPosition();
    }

    private IEnumerator DelayedSpawn()
    {
        while (MapManager.Instance == null)
            yield return null;

        mapManager = MapManager.Instance;

        while (!mapManager.CanSpawn())
            yield return null;

        SetSpawnPosition();
    }

    private void SetSpawnPosition()
    {
        if (mapManager == null)
        {
            Debug.LogError("MapManager not found!");
            return;
        }

        Transform spawn = mapManager.GetActiveLaunchPoint();

        if (spawn == null)
        {
            Debug.LogError("Spawn point is null");
            return;
        }

        characterController.enabled = false;
        transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        characterController.enabled = true;
    }
}
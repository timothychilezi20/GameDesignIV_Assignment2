using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerLauncher : NetworkBehaviour
{
    [Header("Launch Settings")]
    public float minForce = 10f;
    public float maxForce = 40f;
    public float chargeSpeed = 20f;
    public float gravity = -20f;

    private float currentForce;
    private bool isCharging;
    private Vector3 velocity;

    private CharacterController controller;
    private PlayerInput playerInput;
    private InputAction launchAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>(); 
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        launchAction = playerInput.actions["Launch"];

        launchAction.started += OnLaunchStarted;
        launchAction.canceled += OnLaunchReleased;

        launchAction.Enable();
    }

    public override void OnDestroy()
    {
        if (launchAction != null)
        {
            launchAction.started -= OnLaunchStarted;
            launchAction.canceled -= OnLaunchReleased;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (isCharging)
        {
            currentForce += chargeSpeed * Time.deltaTime;
            currentForce = Mathf.Clamp(currentForce, minForce, maxForce);
        }

        ApplyMovement();
    }

    private void OnLaunchStarted(InputAction.CallbackContext context)
    {
        isCharging = true;
        currentForce = minForce;
    }

    private void OnLaunchReleased(InputAction.CallbackContext context)
    {
        isCharging = false;

        ReleaseLaunchServerRpc(currentForce);
    }

    private void ApplyMovement()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    [ServerRpc]
    private void ReleaseLaunchServerRpc(float force)
    {
        LaunchClientRpc(force);
    }

    [ClientRpc]
    private void LaunchClientRpc(float force)
    {
        velocity = transform.forward * force;
    }

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }
}
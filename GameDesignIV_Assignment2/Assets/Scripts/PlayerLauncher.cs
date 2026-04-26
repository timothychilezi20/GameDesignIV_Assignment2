using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerLauncher : NetworkBehaviour
{
    [Header("Physics")]
    public float gravity = -25f;

    [Header("Launch Settings")]
    public float launchForce = 120f;
    public float upwardBoost = 20f;
    public float launchDamping = 3f;

    private Vector3 velocity;
    private Vector3 launchVelocity;

    private CharacterController controller;
    private PlayerInput playerInput;

    private InputAction launchAction;

    [SerializeField] private TMP_Text countdownText;

    private float countdownValue;
    private bool isCountingDown;

    private bool hasStartedMatch = false;
    private bool canMove = false;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        launchAction = playerInput.actions["LaunchCountdown"];
        launchAction.Enable();
        launchAction.performed += OnLaunchPressed;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        if (launchAction != null)
        {
            launchAction.performed -= OnLaunchPressed;
            launchAction.Disable();
        }
    }

    // ---------------- INPUT ----------------
    private void OnLaunchPressed(InputAction.CallbackContext context)
    {
        if (hasStartedMatch) return;

        StartCountdown(3f);
    }

    // ---------------- UPDATE ----------------
    private void Update()
    {
        if (!IsOwner) return;

        HandleCountdown();
        ApplyMovement();
    }

    // ---------------- MOVEMENT ----------------
    private void ApplyMovement()
    {
        if (!canMove) return;

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        if (launchVelocity.magnitude > 0.1f)
        {
            controller.Move(launchVelocity * Time.deltaTime);

            launchVelocity = Vector3.Lerp(
                launchVelocity,
                Vector3.zero,
                launchDamping * Time.deltaTime
            );
        }

        controller.Move(velocity * Time.deltaTime);
    }

    // ---------------- COUNTDOWN ----------------
    public void StartCountdown(float time)
    {
        if (hasStartedMatch) return;

        hasStartedMatch = true;
        canMove = false;

        countdownValue = time;
        isCountingDown = true;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.Ceil(time).ToString();
        }
    }

    private void HandleCountdown()
    {
        if (!isCountingDown) return;

        countdownValue -= Time.deltaTime;

        if (countdownText != null)
            countdownText.text = Mathf.Ceil(countdownValue).ToString();

        if (countdownValue <= 0f)
        {
            isCountingDown = false;

            if (countdownText != null)
                countdownText.gameObject.SetActive(false);

            canMove = true;

            TriggerLaunch();
        }
    }

    // ---------------- LAUNCH ----------------
    private void TriggerLaunch()
    {
        launchVelocity = transform.forward * launchForce;
        launchVelocity.y += upwardBoost;
    }

    // ---------------- RESET ----------------
    public void ResetVelocity()
    {
        velocity = Vector3.zero;
        launchVelocity = Vector3.zero;
    }

    public void ResetMatchState()
    {
        hasStartedMatch = false;
        canMove = false;

        countdownValue = 0f;
        isCountingDown = false;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        ResetVelocity();
    }
}
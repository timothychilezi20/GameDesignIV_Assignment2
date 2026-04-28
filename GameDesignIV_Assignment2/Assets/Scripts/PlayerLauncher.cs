using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// PlayerLauncher — fires the player horizontally from their spawn point when
/// the countdown hits zero. Movement during the launch phase is handled entirely
/// by the Rigidbody (set in PlayerController). This script owns the countdown,
/// the initial impulse, and the transition back to normal player control once
/// the player has slowed to a stop naturally via physics drag + bouncing.
///
/// SETUP NOTES:
///   • Attach a PhysicsMaterial to the player's Collider with:
///       Bounciness        : 0.6  (adjust to taste)
///       Bounce Combine    : Maximum
///       Dynamic Friction  : 0.1
///       Static Friction   : 0.1
///       Friction Combine  : Minimum
///   • Set Rigidbody.drag to ~0.5 so the player naturally decelerates after bouncing.
///   • Wall/bumper colliders on the map should also have a PhysicsMaterial with
///     Bounciness 0.6–1.0 and Bounce Combine set to Maximum.
///   • The spawn point's forward vector is the exact horizontal fire direction —
///     make sure spawn point Transforms face into the map.
/// </summary>
public class PlayerLauncher : NetworkBehaviour
{
    // =========================================================================
    // Inspector
    // =========================================================================

    [Header("Launch Settings")]
    [Tooltip("Horizontal impulse force applied at launch.")]
    public float launchForce = 200f;

    [Tooltip("Rigidbody speed below which the player is considered stopped " +
             "and normal movement control is restored.")]
    public float controlRestoreSpeed = 1.5f;

    [Tooltip("How long (seconds) after launch before we start checking if the " +
             "player has slowed enough to restore control. Prevents instant restore " +
             "if launch force is low.")]
    public float controlRestoreDelay = 0.5f;

    [Header("Countdown")]
    [SerializeField] private TMP_Text countdownText;

    // =========================================================================
    // Private state
    // =========================================================================

    private PlayerController _playerController;
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private PauseMenu _pauseMenu;
    private InputAction _pauseAction;

    private float _countdownValue;
    private bool _isCountingDown = false;
    private bool _hasLaunched = false;
    private float _timeSinceLaunch = 0f;

    // =========================================================================
    // Init
    // =========================================================================

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Pause input only — launch is triggered by countdown, not player input
        _pauseAction = _playerInput.actions["Pause"];
        _pauseAction.Enable();
        _pauseAction.performed += OnPausePressed;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        _pauseMenu = FindAnyObjectByType<PauseMenu>();
    }

    public override void OnNetworkDespawn()
    {
        if (_pauseAction != null)
        {
            _pauseAction.performed -= OnPausePressed;
            _pauseAction.Disable();
        }
    }

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        _pauseMenu?.TogglePause();
    }

    // =========================================================================
    // Update
    // =========================================================================

    private void Update()
    {
        if (!IsOwner) return;

        HandleCountdown();

        if (_hasLaunched)
            CheckRestoreControl();
    }

    // =========================================================================
    // Countdown
    // =========================================================================

    /// <summary>
    /// Called by SpawnManager on the owning client after placement.
    /// </summary>
    public void StartCountdown(float duration)
    {
        if (_isCountingDown || _hasLaunched) return;

        _countdownValue = duration;
        _isCountingDown = true;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.Ceil(_countdownValue).ToString();
        }

        // Keep player frozen while countdown runs
        _playerController.SetMovementLocked(true);
    }

    private void HandleCountdown()
    {
        if (!_isCountingDown) return;

        _countdownValue -= Time.deltaTime;

        if (countdownText != null)
            countdownText.text = Mathf.Ceil(Mathf.Max(_countdownValue, 0f)).ToString();

        if (_countdownValue <= 0f)
        {
            _isCountingDown = false;

            if (countdownText != null)
                countdownText.gameObject.SetActive(false);

            FireLaunch();
        }
    }

    // =========================================================================
    // Launch — pure horizontal impulse along spawn-point forward
    // =========================================================================

    private void FireLaunch()
    {
        // Launch direction is baked into PlayerController by SpawnManager
        // before placement, so it always matches the spawn point's forward.
        Vector3 direction = _playerController.GetLaunchDirection();

        // Flatten to horizontal — no vertical component at all
        direction.y = 0f;
        direction = direction.normalized;

        _hasLaunched = true;
        _timeSinceLaunch = 0f;

        // ExecuteLaunch unlocks movement, enables gravity, and fires the impulse
        _playerController.ExecuteLaunch(direction, launchForce);

        Debug.Log($"[PlayerLauncher] Fired — direction:{direction} force:{launchForce}");
    }

    // =========================================================================
    // Control restore — waits for the Rigidbody to slow naturally via drag
    // and PhysicsMaterial friction after bouncing around the map
    // =========================================================================

    private void CheckRestoreControl()
    {
        _timeSinceLaunch += Time.deltaTime;

        // Don't check too early — player may not have even left the corridor yet
        if (_timeSinceLaunch < controlRestoreDelay) return;

        float speed = _rb.linearVelocity.magnitude;

        if (speed <= controlRestoreSpeed)
        {
            _hasLaunched = false;

            // Hand control back to PlayerController's normal movement loop
            _playerController.SetMovementLocked(false);

            Debug.Log("[PlayerLauncher] Player settled — control restored.");
        }
    }

    // =========================================================================
    // Reset — called by SpawnManager on each respawn
    // =========================================================================

    public void ResetMatchState()
    {
        _isCountingDown = false;
        _hasLaunched = false;
        _timeSinceLaunch = 0f;
        _countdownValue = 0f;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        _playerController.SetMovementLocked(true);
    }
}
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class PlayerLauncher : NetworkBehaviour
{
    // ─────────────────────────────────────────────
    // LAUNCH SETTINGS
    // ─────────────────────────────────────────────

    [Header("Launch Settings")]
    public float launchForce = 200f;
    public float controlRestoreSpeed = 1.5f;
    public float controlRestoreDelay = 0.5f;

    // ─────────────────────────────────────────────
    // UI + AUDIO
    // ─────────────────────────────────────────────

    [Header("Countdown UI")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Countdown Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countdownClip;

    // ─────────────────────────────────────────────
    // STATE
    // ─────────────────────────────────────────────

    private PlayerController _playerController;
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private PauseMenu _pauseMenu;

    private InputAction _pauseAction;

    private float _countdownValue;
    private bool _isCountingDown;
    private bool _hasLaunched;
    private float _timeSinceLaunch;

    // ─────────────────────────────────────────────
    // INIT
    // ─────────────────────────────────────────────

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        _pauseAction = _playerInput.actions["Pause"];
        _pauseAction.Enable();
        _pauseAction.performed += OnPausePressed;

        _pauseMenu = FindAnyObjectByType<PauseMenu>();

        ResetCountdownUI();
    }

    public override void OnNetworkDespawn()
    {
        if (_pauseAction != null)
        {
            _pauseAction.performed -= OnPausePressed;
            _pauseAction.Disable();
        }
    }

    private void Start()
    {
        ResetCountdownUI();
    }

    // ─────────────────────────────────────────────
    // UI RESET + AUDIO STOP
    // ─────────────────────────────────────────────

    private void ResetCountdownUI()
    {
        if (countdownText != null)
        {
            countdownText.text = "";
            countdownText.gameObject.SetActive(false);
        }

        StopCountdownAudio();
    }

    private void StopCountdownAudio()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }
    }

    // ─────────────────────────────────────────────
    // PAUSE
    // ─────────────────────────────────────────────

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        _pauseMenu?.TogglePause();
    }

    // ─────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (!IsOwner) return;

        HandleCountdown();

        if (_hasLaunched)
            CheckRestoreControl();
    }

    // ─────────────────────────────────────────────
    // START COUNTDOWN
    // ─────────────────────────────────────────────

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

        _playerController.SetMovementLocked(true);

        // ▶ Start looping countdown audio
        if (audioSource != null && countdownClip != null)
        {
            audioSource.clip = countdownClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    // ─────────────────────────────────────────────
    // COUNTDOWN LOGIC
    // ─────────────────────────────────────────────

    private void HandleCountdown()
    {
        if (!_isCountingDown) return;

        _countdownValue -= Time.deltaTime;

        if (countdownText != null)
            countdownText.text = Mathf.Ceil(Mathf.Max(_countdownValue, 0f)).ToString();

        if (_countdownValue <= 0f)
        {
            _isCountingDown = false;

            StartCoroutine(ShowGoThenLaunch());
        }
    }

    private IEnumerator ShowGoThenLaunch()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "GO!";
        }

        // optional small delay so player sees it
        yield return new WaitForSeconds(0.3f);

        ResetCountdownUI();
        FireLaunch();
    }

    // ─────────────────────────────────────────────
    // LAUNCH
    // ─────────────────────────────────────────────

    private void FireLaunch()
    {
        Vector3 direction = _playerController.GetLaunchDirection();

        direction.y = 0f;
        direction.Normalize();

        _hasLaunched = true;
        _timeSinceLaunch = 0f;

        _playerController.ExecuteLaunch(direction, launchForce);

        Debug.Log($"[PlayerLauncher] Launch fired: {direction}");
    }

    // ─────────────────────────────────────────────
    // CONTROL RESTORE
    // ─────────────────────────────────────────────

    private void CheckRestoreControl()
    {
        _timeSinceLaunch += Time.deltaTime;

        if (_timeSinceLaunch < controlRestoreDelay) return;

        if (_rb.linearVelocity.magnitude <= controlRestoreSpeed)
        {
            _hasLaunched = false;
            _playerController.SetMovementLocked(false);

            Debug.Log("[PlayerLauncher] Control restored");
        }
    }

    // ─────────────────────────────────────────────
    // RESET MATCH STATE
    // ─────────────────────────────────────────────

    public void ResetMatchState()
    {
        _isCountingDown = false;
        _hasLaunched = false;
        _timeSinceLaunch = 0f;
        _countdownValue = 0f;

        ResetCountdownUI();

        _playerController.SetMovementLocked(true);
    }
}
using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] private GameObject pauseUI;
    [SerializeField] private GameObject controlsUI;
    public bool IsPaused { get; private set; }

    private PlayerInput playerInput;

    private void Awake()
    {
        Instance = this;
        
        if (pauseUI != null)
        {
            pauseUI.SetActive(false);
        }

        if (controlsUI != null)
            controlsUI.SetActive(false);
    }

    public void Initialize(PlayerInput input)
    {
        playerInput = input;
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        IsPaused = true;

        if (pauseUI != null)
        {
            pauseUI.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerInput != null)
            playerInput.enabled = false;
    }

    public void Resume()
    {
        IsPaused = false;

        if (pauseUI != null)
        {
            pauseUI.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerInput != null)
            playerInput.enabled = true;
    }

    public void ShowControls()
    {
        if (controlsUI != null)
            controlsUI.SetActive(true);

        if (pauseUI != null)
            pauseUI.SetActive(false);
    }

    public void HideControls()
    {
        if (controlsUI != null)
            controlsUI.SetActive(false);

        if (pauseUI != null)
            pauseUI.SetActive(true);
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}
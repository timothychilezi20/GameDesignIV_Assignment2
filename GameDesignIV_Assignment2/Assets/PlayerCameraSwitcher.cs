using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerCameraSwitcher : NetworkBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera thirdPersonCamera;
    [SerializeField] private Camera topDownCamera;

    private PlayerInput playerInput;
    private InputAction switchCameraAction;

    private bool isTopDown = false;

    public override void OnNetworkSpawn()
    {
        playerInput = GetComponent<PlayerInput>();

        if (!IsOwner)
        {
            thirdPersonCamera.gameObject.SetActive(false);
            topDownCamera.gameObject.SetActive(false);
            enabled = false;
            return; 
        }

        switchCameraAction = playerInput.actions["SwitchCamera"];
        switchCameraAction.Enable();

        SetCamera(false);
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (switchCameraAction.WasPressedThisFrame())
        {
            isTopDown = !isTopDown;
            SetCamera(isTopDown);
        }
    }

    private void SetCamera(bool topDown)
    {
        thirdPersonCamera.gameObject.SetActive(!topDown);
        topDownCamera.gameObject.SetActive(topDown);
    }
}

using UnityEngine;
using Unity.Netcode;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Camera topDownCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Disable camera for non-owners so only the local player sees their own camera
            if (topDownCamera != null)
                topDownCamera.gameObject.SetActive(false);

            enabled = false;
            return;
        }

        // Enable top-down camera for the owning player only
        if (topDownCamera != null)
            topDownCamera.gameObject.SetActive(true);
    }

    public Camera GetCamera() => topDownCamera;
}
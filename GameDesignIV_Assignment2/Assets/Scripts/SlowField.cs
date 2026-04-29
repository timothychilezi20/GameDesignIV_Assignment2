using UnityEngine;
using Unity.Netcode;

public class SlowField : NetworkBehaviour
{
    [SerializeField] private float slowMultiplier = 0.5f;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.ApplySpeedMultiplier(slowMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.ResetSpeedMultiplier();
    }
}

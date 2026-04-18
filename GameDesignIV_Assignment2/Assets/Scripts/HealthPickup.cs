using UnityEngine;
using Unity.Netcode;

public class HealthPickup : InteractableItems
{
    [SerializeField] private float health = 50f;

    public override void Interact(GameObject player)
    {
        if (!IsServer) return;

        NetworkFPSPlayer fpsPlayer = player.GetComponent<NetworkFPSPlayer>();

        if (fpsPlayer == null)
        {
            Debug.LogWarning("HealthPickup: NetworkFPSPlayer component not found on interacting object.", player);
            return;
        }

        if (!fpsPlayer.IsAlive)
        {
            return;
        }

        fpsPlayer.AddHealth(health);
        base.Interact(player);
    }
}
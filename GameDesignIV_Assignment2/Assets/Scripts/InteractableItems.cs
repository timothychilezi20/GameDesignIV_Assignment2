using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class InteractableItems : NetworkBehaviour
{
    [Header("General")]
    [SerializeField] private bool destroyOnInteract = true;

    public virtual void Interact(GameObject player)
    {
        if (!IsServer) return; // Only server can destroy network objects

        if (destroyOnInteract)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}
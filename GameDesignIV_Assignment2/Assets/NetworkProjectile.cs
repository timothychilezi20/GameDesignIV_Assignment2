using Unity.Netcode; 
using UnityEngine;

public class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(Despawn), lifeTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsServer)
        {
            Despawn();
        }
    }

    private void Despawn()
    {
        if (NetworkObject && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}

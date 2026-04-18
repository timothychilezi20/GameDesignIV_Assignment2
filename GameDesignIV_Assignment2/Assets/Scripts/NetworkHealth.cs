using UnityEngine;
using Unity.Netcode;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public NetworkVariable<int> Health = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]

    public void TakeDamageServerRpc(int amount)
    {
        Health.Value -= amount;

        if (Health.Value <= 0)
        {
            Health.Value = maxHealth;
        }

    }

    public float Health01
    {
        get
        {
            if (maxHealth == 0)
            {
                return 0f; 
            }
            else
            {
                return (float)Health.Value / maxHealth;
            }
        }
        
    }
}

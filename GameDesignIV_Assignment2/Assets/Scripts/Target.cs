using UnityEngine;
using Unity.Netcode;

public class Target : NetworkBehaviour
{
    [SerializeField] private int pointValue = 1;

    public void GetHit(int playerNumber)
    {
        if (!IsServer) return;

        ScoreManager.Instance.AddScore(playerNumber, pointValue);
        GetComponent<NetworkObject>().Despawn(destroy: true);
    }
}
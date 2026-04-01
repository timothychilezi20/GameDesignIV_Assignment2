using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem; 

public class BasicPlayer : NetworkBehaviour
{
    private float speed = 5f;
    private PlayerInput playerInput;
    private InputAction moveAction; 

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return; 
        }

        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];

        playerInput.enabled = true;
        moveAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return; 
        }
    }

    void Update()
    {
        if (!IsOwner || !IsSpawned)
        {
            return; 
        }
        Vector2 move = moveAction.ReadValue<Vector2>();
        Vector3 move3 = new Vector3(move.x, 0, move.y) * speed * Time.deltaTime;
        transform.position += move3; 
    }
}

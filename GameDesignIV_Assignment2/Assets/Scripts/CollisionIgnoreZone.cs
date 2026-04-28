using UnityEngine;
using Unity.Netcode;

public class CollisionIgnoreZone : NetworkBehaviour
{
    // While inside this zone players ignore each other's colliders
    // Once a player exits, collisions are re-enabled

    private Collider player1Collider;
    private Collider player2Collider;
    private bool collisionsIgnored = false;

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.playerNumber == 1)
            player1Collider = other;
        else if (player.playerNumber == 2)
            player2Collider = other;

        // Ignore collisions once we have both players
        if (player1Collider != null && player2Collider != null && !collisionsIgnored)
        {
            Physics.IgnoreCollision(player1Collider, player2Collider, true);
            collisionsIgnored = true;
            Debug.Log("[CollisionIgnoreZone] Player collisions disabled");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // Re-enable collisions once either player exits the zone
        if (collisionsIgnored && player1Collider != null && player2Collider != null)
        {
            Physics.IgnoreCollision(player1Collider, player2Collider, false);
            collisionsIgnored = false;
            Debug.Log("[CollisionIgnoreZone] Player collisions re-enabled");
        }
    }
}
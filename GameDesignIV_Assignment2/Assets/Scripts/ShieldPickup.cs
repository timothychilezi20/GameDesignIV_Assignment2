using UnityEngine;

public class ShieldPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerShield shield = other.GetComponent<PlayerShield>();
        if (shield != null)
        {
            shield.GrantShield();
            Destroy(gameObject); // remove pickup from world
        }
    }
}
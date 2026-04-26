using UnityEngine;

public class SlowField : MonoBehaviour
{
    [SerializeField] private float slowMultiplier = 0.5f; // 0.5 = 50% speed

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.ApplySpeedMultiplier(slowMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.ResetSpeedMultiplier();
    }
}

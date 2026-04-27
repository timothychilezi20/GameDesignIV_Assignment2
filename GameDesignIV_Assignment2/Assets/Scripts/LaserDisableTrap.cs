using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class LaserDisableTrap : NetworkBehaviour
{
    [SerializeField] private float disableDuration = 5f;
    [SerializeField] private float flashStartTime = 2f;
    [SerializeField] private float flashSpeed = 8f;

    private bool isActive = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !isActive) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // Disable the laser of the player who stepped on the trap
        StartCoroutine(DisableLaser(player.playerNumber));
    }

    private IEnumerator DisableLaser(int playerNumber)
    {
        isActive = false;

        LaserManager.Instance.SetLaserActive(playerNumber, false);

        yield return new WaitForSeconds(disableDuration - flashStartTime);

        // Flash for last 2 seconds
        StartCoroutine(FlashLaser(playerNumber));
        yield return new WaitForSeconds(flashStartTime);

        LaserManager.Instance.SetLaserActive(playerNumber, true);
        isActive = true;
    }

    private IEnumerator FlashLaser(int playerNumber)
    {
        float elapsed = 0f;
        while (elapsed < flashStartTime)
        {
            LaserManager.Instance.FlashLaserClientRpc(playerNumber);
            float interval = 1f / flashSpeed;
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }
}
using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class LaserDisableTrap : NetworkBehaviour
{
    [SerializeField] private float disableDuration = 5f;
    [SerializeField] private float flashStartTime = 2f;
    [SerializeField] private float flashSpeed = 8f;

    private bool isTriggerLocked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isTriggerLocked) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        StartCoroutine(DisableLaserRoutine(player.playerNumber));
    }

    private IEnumerator DisableLaserRoutine(int playerNumber)
    {
        isTriggerLocked = true;

        LaserManager laser = LaserManager.Instance;
        if (laser == null) yield break;

        laser.SetLaserActive(playerNumber, false);

        float safeDisableTime = Mathf.Max(0f, disableDuration - flashStartTime);
        yield return new WaitForSeconds(safeDisableTime);

        yield return StartCoroutine(FlashLaserRoutine(playerNumber, laser));

        laser.SetLaserActive(playerNumber, true);

        isTriggerLocked = false;
    }

    private IEnumerator FlashLaserRoutine(int playerNumber, LaserManager laser)
    {
        float elapsed = 0f;
        float interval = 1f / Mathf.Max(1f, flashSpeed);

        while (elapsed < flashStartTime)
        {
            laser.FlashLaserClientRpc(playerNumber);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }
}
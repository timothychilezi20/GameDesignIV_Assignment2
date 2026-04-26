using UnityEngine;
using System.Collections;

public class LaserDisableTrap : MonoBehaviour
{
    [SerializeField] private float disableDuration = 5f;
    [SerializeField] private float flashStartTime = 2f;
    [SerializeField] private float flashSpeed = 8f;

    private bool isActive = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        // The player that stepped on the trap
        LaserBeam laser = other.GetComponentInChildren<LaserBeam>();
        if (laser != null)
        {
            // Disable the OTHER players laser
            LaserBeam targetLaser = laser.otherLaser;
            if (targetLaser != null)
                StartCoroutine(DisableLaser(targetLaser));
        }
    }

    private IEnumerator DisableLaser(LaserBeam laser)
    {
        isActive = false;
        laser.SetActive(false);

        // Wait until flash period
        yield return new WaitForSeconds(disableDuration - flashStartTime);

        // Flash the laser for the last 2 seconds
        StartCoroutine(FlashLaser(laser));
        yield return new WaitForSeconds(flashStartTime);

        // Re-enable
        laser.SetActive(true);
        isActive = true;
    }

    private IEnumerator FlashLaser(LaserBeam laser)
    {
        float elapsed = 0f;
        while (elapsed < flashStartTime)
        {
            laser.lineRenderer.enabled = !laser.lineRenderer.enabled;
            float interval = 1f / flashSpeed;
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }
}

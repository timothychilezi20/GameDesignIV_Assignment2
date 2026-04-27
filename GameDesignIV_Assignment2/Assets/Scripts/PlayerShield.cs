using UnityEngine;

public class PlayerShield : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Physics Settings")]
    [SerializeField] private LayerMask shieldLayer;
    [SerializeField] private float checkRadius = 0.3f;

    public void GrantShield()
    {
        Transform freePoint = GetFreeSpawnPoint();

        if (freePoint != null)
        {
            SpawnAt(freePoint);
        }
        else
        {
            Debug.Log("No free spawn points available.");
        }
    }

    private Transform GetFreeSpawnPoint()
    {
        foreach (Transform point in spawnPoints)
        {
            // Check only relevant shield objects using layer mask
            Collider[] hits = Physics.OverlapSphere(point.position, checkRadius, shieldLayer);

            bool occupied = false;

            foreach (Collider hit in hits)
            {
                if (hit.isTrigger) continue;

                ShieldOrb orb = hit.GetComponent<ShieldOrb>();
                if (orb != null)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
                return point;
        }

        return null;
    }

    private void SpawnAt(Transform point)
    {
        GameObject orb = Instantiate(shieldPrefab, point.position, Quaternion.identity);

        ShieldOrb shieldOrb = orb.GetComponent<ShieldOrb>();
        if (shieldOrb != null)
        {
            shieldOrb.Initialize(point);
        }
        else
        {
            Debug.LogWarning("ShieldPrefab is missing ShieldOrb component.");
        }
    }
}
using UnityEngine;

public class PlayerShield : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform[] spawnPoints;

    public void GrantShield()
    {
        Transform freePoint = GetFreeSpawnPoint();

        if (freePoint != null)
            SpawnAt(freePoint);
        else
            Debug.Log("No free spawn points available.");
    }

    private Transform GetFreeSpawnPoint()
    {
        foreach (Transform point in spawnPoints)
        {
            Collider[] hits = Physics.OverlapSphere(point.position, 0.3f);
            bool occupied = false;

            foreach (Collider hit in hits)
            {
                if (hit.GetComponent<ShieldOrb>() != null)
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
        orb.GetComponent<ShieldOrb>().Initialize(point);
    }
}
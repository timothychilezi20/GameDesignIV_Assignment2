using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TargetSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private GameObject wallTargetPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Settings")]
    [SerializeField] private int maxTargets = 5;
    [SerializeField] private float spawnInterval = 3f;

    private List<GameObject> activeTargets = new List<GameObject>();

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Clean up destroyed targets
            activeTargets.RemoveAll(t => t == null);

            if (activeTargets.Count < maxTargets)
                SpawnTarget();
        }
    }

    private void SpawnTarget()
    {
        // Collect unoccupied spawn points
        List<Transform> freePoints = new List<Transform>();
        foreach (Transform point in spawnPoints)
        {
            bool occupied = false;
            foreach (GameObject activetarget in activeTargets)
            {
                if (activetarget != null && Vector3.Distance(activetarget.transform.position, point.position) < 0.5f)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
                freePoints.Add(point);
        }

        if (freePoints.Count == 0) return;

        Transform spawnPoint = freePoints[Random.Range(0, freePoints.Count)];

        // Pick prefab based on spawn point tag
        GameObject prefab = spawnPoint.CompareTag("WallSpawn") ? wallTargetPrefab : orbPrefab;

        GameObject target = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        activeTargets.Add(target);
    }
}
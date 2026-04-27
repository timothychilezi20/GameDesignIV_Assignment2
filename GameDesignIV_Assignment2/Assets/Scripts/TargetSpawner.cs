using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class TargetSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private GameObject wallTargetPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Settings")]
    [SerializeField] private int maxTargets = 5;
    [SerializeField] private float spawnInterval = 3f;

    private List<GameObject> activeTargets = new List<GameObject>();
    private bool spawningStarted = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Listen for clients connecting
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (spawningStarted) return;

        // Wait until exactly 2 players are connected
        if (NetworkManager.Singleton.ConnectedClients.Count >= 2)
        {
            spawningStarted = true;
            Debug.Log("Both players connected — starting target spawner");
            StartCoroutine(SpawnRoutine());
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            activeTargets.RemoveAll(t => t == null);
            if (activeTargets.Count < maxTargets)
                SpawnTarget();
        }
    }

    private void SpawnTarget()
    {
        List<Transform> freePoints = new List<Transform>();
        foreach (Transform point in spawnPoints)
        {
            bool occupied = false;
            foreach (GameObject activeTarget in activeTargets)
            {
                if (activeTarget != null &&
                    Vector3.Distance(activeTarget.transform.position, point.position) < 0.5f)
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
        GameObject prefab = spawnPoint.CompareTag("WallSpawn") ? wallTargetPrefab : orbPrefab;

        GameObject target = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        target.GetComponent<NetworkObject>().Spawn();
        activeTargets.Add(target);
    }
}
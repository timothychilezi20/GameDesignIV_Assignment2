using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class TargetSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private GameObject wallTargetPrefab;

    [Header("Spawn Points — Map 1 Active")]
    [SerializeField] private Transform[] map1SpawnPoints;

    [Header("Spawn Points — Map 2 Active")]
    [SerializeField] private Transform[] map2SpawnPoints;

    [Header("Settings")]
    [SerializeField] private int maxTargets = 5;
    [SerializeField] private float spawnInterval = 3f;

    // =========================================================================
    // Private state
    // =========================================================================

    private List<GameObject> activeTargets = new List<GameObject>();
    private bool spawningStarted = false;

    // =========================================================================
    // Network lifecycle
    // =========================================================================

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Subscribe to map switches so we can clear and re-spawn on the new map
        if (MapManager.Instance != null)
            MapManager.Instance.OnMapSwitched += OnMapSwitched;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        if (MapManager.Instance != null)
            MapManager.Instance.OnMapSwitched -= OnMapSwitched;
    }

    // =========================================================================
    // Start condition — both players connected
    // =========================================================================

    private void OnClientConnected(ulong clientId)
    {
        if (spawningStarted) return;

        if (NetworkManager.Singleton.ConnectedClients.Count >= 2)
        {
            spawningStarted = true;
            Debug.Log("[TargetSpawner] Both players connected — starting spawner.");
            StartCoroutine(SpawnRoutine());
        }
    }

    // =========================================================================
    // Map switch — despawn all targets then let SpawnRoutine restock on new map
    // =========================================================================

    private void OnMapSwitched()
    {
        DespawnAllTargets();
        // SpawnRoutine is still running — it will restock using the new active
        // map's spawn points on its next tick
    }

    // =========================================================================
    // Spawn routine
    // =========================================================================

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Don't spawn while the map is transitioning
            if (MapManager.Instance != null && !MapManager.Instance.CanSpawn())
                continue;

            activeTargets.RemoveAll(t => t == null);

            if (activeTargets.Count < maxTargets)
                SpawnTarget();
        }
    }

    // =========================================================================
    // Spawn a single target on the currently active map's spawn points
    // =========================================================================

    private void SpawnTarget()
    {
        Transform[] spawnPoints = GetActiveSpawnPoints();

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[TargetSpawner] No spawn points for the active map.");
            return;
        }

        // Build a list of unoccupied points
        List<Transform> freePoints = new List<Transform>();
        foreach (Transform point in spawnPoints)
        {
            bool occupied = false;
            foreach (GameObject active in activeTargets)
            {
                if (active != null &&
                    Vector3.Distance(active.transform.position, point.position) < 0.5f)
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

    // =========================================================================
    // Despawn all active targets (called on map switch)
    // =========================================================================

    private void DespawnAllTargets()
    {
        foreach (GameObject target in activeTargets)
        {
            if (target == null) continue;

            NetworkObject no = target.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
                no.Despawn(destroy: true);
            else
                Destroy(target);
        }

        activeTargets.Clear();
        Debug.Log("[TargetSpawner] All targets despawned for map switch.");
    }

    // =========================================================================
    // Helper — returns the correct spawn point array for the active map
    // =========================================================================

    private Transform[] GetActiveSpawnPoints()
    {
        if (MapManager.Instance == null)
            return map1SpawnPoints; // fallback

        // GetActiveMap() returns the Transform of whichever map is currently up
        Transform activeMap = MapManager.Instance.GetActiveMap();

        bool isMap1 = activeMap == map1SpawnPoints[0]?.root ||
                      (map1SpawnPoints.Length > 0 &&
                       map1SpawnPoints[0] != null &&
                       map1SpawnPoints[0].IsChildOf(activeMap));

        return isMap1 ? map1SpawnPoints : map2SpawnPoints;
    }
}
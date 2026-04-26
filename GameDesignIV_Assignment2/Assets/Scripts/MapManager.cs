using UnityEngine;
using Unity.Netcode;

public class MapManager : NetworkBehaviour
{
    [Header("Maps")]
    public Transform map1;
    public Transform map2;

    [Header("Movement Settings")]
    public float swapInterval = 10f;
    public float moveSpeed = 5f;

    [Header("Map Positions")]
    public Vector3 map1UpPos;
    public Vector3 map1DownPos;

    public Vector3 map2UpPos;
    public Vector3 map2DownPos;

    [Header("Spawn Points")]
    [SerializeField] private Transform map1LaunchPoint;
    [SerializeField] private Transform map2LaunchPoint;

    private NetworkVariable<bool> isMap1Active = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isTransitioning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float timer;

    public static MapManager Instance;

    private void Awake()
    {
        // ✅ Safe singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;

        if (timer >= swapInterval)
        {
            timer = 0f;

            // Toggle active map
            isMap1Active.Value = !isMap1Active.Value;

            // Begin transition
            isTransitioning.Value = true;

            Debug.Log("Map swap triggered: " + (isMap1Active.Value ? "Map1 ACTIVE" : "Map2 ACTIVE"));
        }

        MoveMaps();
    }

    public Transform GetActiveLaunchPoint()
    {
        Transform spawn = isMap1Active.Value ? map1LaunchPoint : map2LaunchPoint;

        if (spawn == null)
        {
            Debug.LogError("Launch point is NULL!");
            return null;
        }

        return spawn;
    }

    public bool CanSpawn()
    {
        return !isTransitioning.Value;
    }

    public bool IsMap1Active()
    {
        return isMap1Active.Value;
    }

    private void MoveMaps()
    {
        Vector3 target1 = isMap1Active.Value ? map1UpPos : map1DownPos;
        Vector3 target2 = isMap1Active.Value ? map2DownPos : map2UpPos;

        map1.position = Vector3.MoveTowards(map1.position, target1, moveSpeed * Time.deltaTime);
        map2.position = Vector3.MoveTowards(map2.position, target2, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(map1.position, target1) < 0.01f &&
            Vector3.Distance(map2.position, target2) < 0.01f)
        {
            if (isTransitioning.Value)
            {
                Debug.Log("Map transition complete.");
            }

            isTransitioning.Value = false;
        }
    }
}
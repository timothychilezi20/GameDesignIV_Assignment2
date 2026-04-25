using UnityEngine;
using Unity.Netcode;

public class MapManager : NetworkBehaviour
{
    public Transform map1;
    public Transform map2;

    public float swapInterval = 10f;
    public float moveSpeed = 5f;

    public Vector3 map1UpPos;
    public Vector3 map1DownPos;

    public Vector3 map2UpPos;
    public Vector3 map2DownPos;

    [SerializeField] private Transform map1LaunchPoint;
    [SerializeField] private Transform map2LaunchPoint;

    private NetworkVariable<bool> isMap1Active = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isTransitioning = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private float timer;

    private void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;

        if (timer >= swapInterval)
        {
            timer = 0f;
            isMap1Active.Value = !isMap1Active.Value;
            isTransitioning.Value = true;
        }

        MoveMaps();
    }

    public Transform GetActiveLaunchPoint()
    {
        return isMap1Active.Value ? map1LaunchPoint : map2LaunchPoint;
    }

    public bool CanSpawn()
    {
        return !isTransitioning.Value;
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
            isTransitioning.Value = false;
        }
    }
}
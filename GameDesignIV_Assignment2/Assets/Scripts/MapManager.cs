using UnityEngine;
using Unity.Netcode;

public class MapManager : MonoBehaviour
{
    public Transform map1;
    public Transform map2;

    public float swapInterval = 10f;
    public float moveSpeed = 5f;

    public Vector3 map1UpPos;
    public Vector3 map1DownPos;

    public Vector3 map2UpPos;
    public Vector3 map2DownPos;

    private float timer;
    private bool isMap1Active = true;

    private void Update()
    {
        //if (!IsServer) return;

        timer += Time.deltaTime;

        if (timer >= swapInterval)
        {
            timer = 0f;
            isMap1Active = !isMap1Active;
        }

        MoveMaps();
    }

    private void MoveMaps()
    {
        Vector3 target1 = isMap1Active ? map1UpPos : map1DownPos;
        Vector3 target2 = isMap1Active ? map2DownPos : map2UpPos;

        map1.position = Vector3.MoveTowards(map1.position, target1, moveSpeed * Time.deltaTime);
        map2.position = Vector3.MoveTowards(map2.position, target2, moveSpeed * Time.deltaTime);
    }
}

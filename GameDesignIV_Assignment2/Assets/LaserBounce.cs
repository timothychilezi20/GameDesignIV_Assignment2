using System.Security.Cryptography;
using UnityEngine;

public class LaserBounce : MonoBehaviour
{
    int maxBounces = 5;
    private LineRenderer lineRenderer;
    [SerializeField] private Transform startPoint;
    [SerializeField] private bool reflectOnlyMirror; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.SetPosition(0, startPoint.position);

    }

    // Update is called once per frame
    void Update()
    {
        CastLaser(transform.position, -transform.forward);
    }

    void CastLaser(Vector3 position, Vector3 direction)
    {
        lineRenderer.SetPosition(0, startPoint.position);

        for (int i = 0; i < maxBounces; i++)
        {
            Ray ray = new Ray(position, direction);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit, 300, 1))
            {
                position = hit.point;
                direction = Vector3.Reflect(direction, hit.normal);
                lineRenderer.SetPosition(i + 1, hit.point);

                if (hit.transform.name != "Mirror" && reflectOnlyMirror)
                {
                    for (int j = (i+i); j <= 5; j++)
                    {
                        lineRenderer.SetPosition(j, hit.point);
                    }
                    break;
                }
            }
        }
    }
}

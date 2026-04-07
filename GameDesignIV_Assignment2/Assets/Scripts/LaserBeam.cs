using UnityEngine;

[RequireComponent(typeof(LineRenderer))]    //scripts needs a linerenderer before it runs 

public class LaserBeam : MonoBehaviour
{
    public int reflections;
    public float maxLength;

    private LineRenderer lineRenderer;
    private Ray ray;
    private RaycastHit hit;
    private Vector3 direction;

    public float damagePerSecond = 2f;


    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        ray = new Ray(transform.position, transform.forward);

        lineRenderer.positionCount = 1; //number of verticies for line renderer, this means it has 2 cause the positions count starts at zero
        lineRenderer.SetPosition(0, transform.position); //the first linerenderer position is the transform of the object with the script, this is saying the first vertex for the line render is the objects transform  

        float remainingLength = maxLength;

        for (int i = 0; i < reflections; i++)
        {
            if (Physics.Raycast(ray.origin, ray.direction, out hit, remainingLength))
            {
                lineRenderer.positionCount += 1;
                lineRenderer.SetPosition(lineRenderer.positionCount - 1, hit.point);
                remainingLength -= Vector3.Distance(ray.origin, hit.point);

                BreakableWall wall = hit.collider.GetComponent<BreakableWall>();
                if (wall != null)
                {
                    wall.TakeDamage(damagePerSecond * Time.deltaTime);
                }


                ray = new Ray(hit.point - ray.direction * 0.02f, Vector3.Reflect(ray.direction, hit.normal));
                if (hit.collider.tag != "Reflective")
                    break;
            }
            else
            {
                lineRenderer.positionCount += 1;
                lineRenderer.SetPosition(lineRenderer.positionCount - 1, ray.origin + ray.direction * remainingLength);
            }
        }
    }
}


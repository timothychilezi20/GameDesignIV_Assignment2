using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]    //scripts needs a linerenderer before it runs 

public class LaserBeam : MonoBehaviour
{
    public int reflections;
    public float maxLength;

    [HideInInspector] public LineRenderer lineRenderer;
    private Ray ray;
    private RaycastHit hit;
    private Vector3 direction;

    public float damagePerSecond = 2f;

    //laser points 
    public List<Vector3> points = new List<Vector3>();
    public LaserBeam otherLaser;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        points.Clear();
        points.Add(transform.position);

        ray = new Ray(transform.position, transform.forward);

       

        float remainingLength = maxLength;

        for (int i = 0; i < reflections; i++)
        {
            if (Physics.Raycast(ray.origin, ray.direction, out hit, remainingLength))
            {
               
                points.Add(hit.point);
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
                points.Add(ray.origin + ray.direction * remainingLength);
                break;
            }
        }

        bool cut = false;

        if (otherLaser != null)
        {
            for (int i = 0; i < points.Count - 1 && !cut; i++)
            {
                for (int j = 0; j < otherLaser.points.Count - 1; j++)
                {
                    if (LineIntersect(
                        points[i], points[i + 1],
                        otherLaser.points[j], otherLaser.points[j + 1],
                        out Vector3 hitPoint))
                    {
                        points[i + 1] = hitPoint;
                        points.RemoveRange(i + 2, points.Count - (i + 2));

                        cut = true;
                        break; // exit inner loop
                    }
                }
            }
        }






        lineRenderer.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            lineRenderer.SetPosition(i, points[i]);
        }
    }

    bool LineIntersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, out Vector3 hitPoint) // a1 and a2 are the first segment of the laser, b1 and b2 the second
    {
        hitPoint = Vector3.zero;

        Vector2 p = new Vector2(a1.x, a1.z); // ignore the y of this second due to the top down camera
        Vector2 r = new Vector2(a2.x - a1.x, a2.z - a1.z);

        Vector2 q = new Vector2(b1.x, b1.z);
        Vector2 s = new Vector2(b2.x - b1.x, b2.z - b1.z);

        float rxs = r.x * s.y - r.y * s.x; //cross product of the lasers, tells me if theyve intersected 
        float qpxr = (q.x - p.x) * r.y - (q.y - p.y) * r.x;

        if (Mathf.Approximately(rxs, 0f)) //if the rxs is 0 then they are not touching
            return false; // parallel

        float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / rxs;
        float u = ((q.x - p.x) * r.y - (q.y - p.y) * r.x) / rxs;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            Vector2 intersection = p + t * r;
            hitPoint = new Vector3(intersection.x, a1.y, intersection.y);
            Debug.DrawLine(hitPoint, hitPoint);
            Debug.Log("intersectingLines");
            return true;
        }

        return false;
    }


    public void SetActive(bool active)
    {
        enabled = active;
        lineRenderer.enabled = active;
    }
}


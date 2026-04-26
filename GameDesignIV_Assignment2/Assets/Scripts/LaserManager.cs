using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class LaserManager : NetworkBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private int reflections = 5;
    [SerializeField] private float maxLength = 20f;
    [SerializeField] private float damagePerSecond = 2f;

    // Assign both laser origins (the cube child of each player) in the Inspector
    [SerializeField] private Transform laserOrigin1;
    [SerializeField] private Transform laserOrigin2;

    [SerializeField] private LineRenderer lineRenderer1;
    [SerializeField] private LineRenderer lineRenderer2;

    private List<Vector3> points1 = new List<Vector3>();
    private List<Vector3> points2 = new List<Vector3>();

    private void Update()
    {
        // Only server calculates laser logic
        if (!IsServer) return;

        points1 = CalculateLaser(laserOrigin1);
        points2 = CalculateLaser(laserOrigin2);

        // Check intersection and cut lasers
        CutAtIntersection(points1, points2);

        // Send resulting points to all clients to render
        UpdateLaserClientRpc(points1.ToArray(), points2.ToArray());
    }

    private List<Vector3> CalculateLaser(Transform origin)
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(origin.position);

        Ray ray = new Ray(origin.position, origin.forward);
        float remainingLength = maxLength;

        for (int i = 0; i < reflections; i++)
        {
            if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, remainingLength))
            {
                points.Add(hit.point);
                remainingLength -= Vector3.Distance(ray.origin, hit.point);

                BreakableWall wall = hit.collider.GetComponent<BreakableWall>();
                if (wall != null)
                    wall.TakeDamage(damagePerSecond * Time.deltaTime);

                Target target = hit.collider.GetComponent<Target>();
                if (target != null)
                {
                    int playerNumber = origin == laserOrigin1 ? 1 : 2;
                    target.GetHit(playerNumber);
                    break;
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

        return points;
    }

    private void CutAtIntersection(List<Vector3> a, List<Vector3> b)
    {
        for (int i = 0; i < a.Count - 1; i++)
        {
            for (int j = 0; j < b.Count - 1; j++)
            {
                if (LineIntersect(a[i], a[i + 1], b[j], b[j + 1], out Vector3 hitPoint))
                {
                    a[i + 1] = hitPoint;
                    if (a.Count > i + 2)
                        a.RemoveRange(i + 2, a.Count - (i + 2));
                    return;
                }
            }
        }
    }

    [ClientRpc]
    private void UpdateLaserClientRpc(Vector3[] laser1Points, Vector3[] laser2Points)
    {
        lineRenderer1.positionCount = laser1Points.Length;
        lineRenderer1.SetPositions(laser1Points);

        lineRenderer2.positionCount = laser2Points.Length;
        lineRenderer2.SetPositions(laser2Points);
    }

    // Called by LaserDisableTrap
    public void SetLaserActive(int playerNumber, bool active)
    {
        if (!IsServer) return;
        SetLaserActiveClientRpc(playerNumber, active);
    }

    [ClientRpc]
    private void SetLaserActiveClientRpc(int playerNumber, bool active)
    {
        LineRenderer lr = playerNumber == 1 ? lineRenderer1 : lineRenderer2;
        lr.enabled = active;

        // Also disable the origin so server skips raycasting it
        if (playerNumber == 1)
            laserOrigin1.gameObject.SetActive(active);
        else
            laserOrigin2.gameObject.SetActive(active);
    }

    [ClientRpc]
    public void FlashLaserClientRpc(int playerNumber)
    {
        LineRenderer lr = playerNumber == 1 ? lineRenderer1 : lineRenderer2;
        lr.enabled = !lr.enabled;
    }

    bool LineIntersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        Vector2 p = new Vector2(a1.x, a1.z);
        Vector2 r = new Vector2(a2.x - a1.x, a2.z - a1.z);
        Vector2 q = new Vector2(b1.x, b1.z);
        Vector2 s = new Vector2(b2.x - b1.x, b2.z - b1.z);

        float rxs = r.x * s.y - r.y * s.x;
        if (Mathf.Approximately(rxs, 0f)) return false;

        float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / rxs;
        float u = ((q.x - p.x) * r.y - (q.y - p.y) * r.x) / rxs;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            Vector2 intersection = p + t * r;
            hitPoint = new Vector3(intersection.x, a1.y, intersection.y);
            return true;
        }
        return false;
    }
}
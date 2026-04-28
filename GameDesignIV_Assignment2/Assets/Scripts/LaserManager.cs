using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class LaserManager : NetworkBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private int reflections = 5;
    [SerializeField] private float maxLength = 20f;
    [SerializeField] private float damagePerSecond = 2f;

    [Header("Stun Settings")]
    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private float invincibleDuration = 3f;

    [Header("Laser Materials")]
    [SerializeField] private Material laser1Material;
    [SerializeField] private Material laser2Material;

    public static LaserManager Instance { get; private set; }

    private Transform laserOrigin1;
    private Transform laserOrigin2;
    private LineRenderer lineRenderer1;
    private LineRenderer lineRenderer2;
    private PlayerController player1;
    private PlayerController player2;

    private bool laser1Active = true;
    private bool laser2Active = true;

    private List<Vector3> points1 = new List<Vector3>();
    private List<Vector3> points2 = new List<Vector3>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterPlayer(int playerNumber, Transform laserOrigin,
        LineRenderer lineRenderer, PlayerController controller)
    {
        if (playerNumber == 1)
        {
            laserOrigin1 = laserOrigin;
            lineRenderer1 = lineRenderer;
            player1 = controller;
        }
        else if (playerNumber == 2)
        {
            laserOrigin2 = laserOrigin;
            lineRenderer2 = lineRenderer;
            player2 = controller;
        }

        Debug.Log($"Player {playerNumber} registered. P1 ready: {laserOrigin1 != null} P2 ready: {laserOrigin2 != null}");

        // Broadcast all currently known players to all clients
        if (player1 != null)
        {
            RegisterPlayerLineRendererClientRpc(1);
            ApplyLaserMaterialClientRpc(1);
        }
        if (player2 != null)
        {
            RegisterPlayerLineRendererClientRpc(2);
            ApplyLaserMaterialClientRpc(2);
        }
    }

    [ClientRpc]
    private void RegisterPlayerLineRendererClientRpc(int playerNumber)
    {
        if (IsServer) return;
        StartCoroutine(WaitAndRegister(playerNumber));
    }

    private IEnumerator WaitAndRegister(int playerNumber)
    {
        yield return null;
        yield return null;

        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController target = null;

        foreach (PlayerController pc in allPlayers)
        {
            if (pc.playerNumber == playerNumber)
            {
                target = pc;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogError($"Client could not find PlayerController for Player {playerNumber}");
            yield break;
        }

        LineRenderer lr = target.GetLaserLineRenderer();
        if (lr == null)
        {
            Debug.LogError($"LaserLineRenderer is null on Player {playerNumber} prefab");
            yield break;
        }

        if (playerNumber == 1)
        {
            lineRenderer1 = lr;
            laserOrigin1 = target.GetLaserOrigin();
        }
        else
        {
            lineRenderer2 = lr;
            laserOrigin2 = target.GetLaserOrigin();
        }

        ApplyMaterialLocally(playerNumber, lr);
        Debug.Log($"Client stored LineRenderer and Origin for Player {playerNumber}");
    }

    [ClientRpc]
    private void ApplyLaserMaterialClientRpc(int playerNumber)
    {
        if (!IsServer) return;

        if (playerNumber == 1 && lineRenderer1 != null && laser1Material != null)
            lineRenderer1.material = laser1Material;
        else if (playerNumber == 2 && lineRenderer2 != null && laser2Material != null)
            lineRenderer2.material = laser2Material;
    }

    private void ApplyMaterialLocally(int playerNumber, LineRenderer lr)
    {
        if (playerNumber == 1 && laser1Material != null)
            lr.material = laser1Material;
        else if (playerNumber == 2 && laser2Material != null)
            lr.material = laser2Material;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        points1 = (laser1Active && laserOrigin1 != null)
            ? CalculateLaser(laserOrigin1)
            : new List<Vector3>();

        points2 = (laser2Active && laserOrigin2 != null)
            ? CalculateLaser(laserOrigin2)
            : new List<Vector3>();

        if (points1.Count > 0 && points2.Count > 0)
            CutAtIntersection(points1, points2);

        CheckHits(points1, player1, player2, 1);
        CheckHits(points2, player2, player1, 2);

        UpdateLaserClientRpc(points1.ToArray(), points2.ToArray());
    }

    private List<Vector3> CalculateLaser(Transform origin)
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(origin.position);

        Ray ray = new Ray(origin.position, origin.forward);
        float remaining = maxLength;

        for (int i = 0; i < reflections; i++)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, remaining))
            {
                points.Add(hit.point);
                remaining -= Vector3.Distance(ray.origin, hit.point);

                ray = new Ray(hit.point - ray.direction * 0.02f,
                    Vector3.Reflect(ray.direction, hit.normal));

                if (hit.collider.tag != "Reflective")
                    break;
            }
            else
            {
                points.Add(ray.origin + ray.direction * remaining);
                break;
            }
        }

        return points;
    }

    private void CheckHits(List<Vector3> points, PlayerController owner,
        PlayerController opponent, int playerNumber)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 dir = points[i + 1] - points[i];
            float dist = dir.magnitude;
            if (dist < 0.001f) continue;

            Ray ray = new Ray(points[i], dir.normalized);

            if (Physics.Raycast(ray, out RaycastHit hit, dist))
            {
                BreakableWall wall = hit.collider.GetComponent<BreakableWall>();
                if (wall != null)
                    wall.TakeDamage(damagePerSecond * Time.deltaTime);

                Target target = hit.collider.GetComponent<Target>();
                if (target != null)
                {
                    target.GetHit(playerNumber);
                    break;
                }

                PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
                if (hitPlayer != null && hitPlayer != owner && !hitPlayer.IsInvincible)
                {
                    hitPlayer.ApplyStun(stunDuration, invincibleDuration);
                    break;
                }
            }
        }
    }

    private void CutAtIntersection(List<Vector3> a, List<Vector3> b)
    {
        for (int i = 0; i < a.Count - 1; i++)
        {
            for (int j = 0; j < b.Count - 1; j++)
            {
                if (LineIntersect(a[i], a[i + 1], b[j], b[j + 1], out Vector3 hit))
                {
                    a[i + 1] = hit;
                    if (a.Count > i + 2) a.RemoveRange(i + 2, a.Count - (i + 2));
                    b[j + 1] = hit;
                    if (b.Count > j + 2) b.RemoveRange(j + 2, b.Count - (j + 2));
                    return;
                }
            }
        }
    }

    [ClientRpc]
    private void UpdateLaserClientRpc(Vector3[] a, Vector3[] b)
    {
        if (lineRenderer1 != null)
        {
            lineRenderer1.positionCount = a.Length;
            lineRenderer1.SetPositions(a);
        }
        if (lineRenderer2 != null)
        {
            lineRenderer2.positionCount = b.Length;
            lineRenderer2.SetPositions(b);
        }
    }

    public void SetLaserActive(int playerNumber, bool active)
    {
        if (!IsServer) return;
        if (playerNumber == 1) laser1Active = active;
        else laser2Active = active;
        SetLaserActiveClientRpc(playerNumber, active);
    }

    [ClientRpc]
    private void SetLaserActiveClientRpc(int playerNumber, bool active)
    {
        LineRenderer lr = playerNumber == 1 ? lineRenderer1 : lineRenderer2;
        if (lr != null) lr.enabled = active;
    }

    [ClientRpc]
    public void FlashLaserClientRpc(int playerNumber)
    {
        LineRenderer lr = playerNumber == 1 ? lineRenderer1 : lineRenderer2;
        if (lr != null) lr.enabled = !lr.enabled;
    }

    private bool LineIntersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, out Vector3 hit)
    {
        hit = Vector3.zero;
        Vector2 p = new Vector2(a1.x, a1.z);
        Vector2 r = new Vector2(a2.x - a1.x, a2.z - a1.z);
        Vector2 q = new Vector2(b1.x, b1.z);
        Vector2 s = new Vector2(b2.x - b1.x, b2.z - b1.z);

        float rxs = r.x * s.y - r.y * s.x;
        if (Mathf.Approximately(rxs, 0)) return false;

        float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / rxs;
        float u = ((q.x - p.x) * r.y - (q.y - p.y) * r.x) / rxs;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            Vector2 intersection = p + t * r;
            hit = new Vector3(intersection.x, a1.y, intersection.y);
            return true;
        }
        return false;
    }
}
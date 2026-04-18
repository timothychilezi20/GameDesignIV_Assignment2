using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(LineRenderer))]
public class NewLaserScript : NetworkBehaviour
{
    private LineRenderer lineRenderer;

    [SerializeField] private Transform startPoint;
    [SerializeField] private float damagePerSecond = 25f;
    [SerializeField] private float maxDistance = 100f;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            Debug.LogError("LineRenderer component is missing.", this);
            enabled = false;
            return;
        }
    }
    void Update()
    {
        lineRenderer.SetPosition(0, startPoint.position);

        RaycastHit hit;

        if (Physics.Raycast(startPoint.position, startPoint.forward, out hit, maxDistance))
        {
            lineRenderer.SetPosition(1, hit.point);

            if (hit.collider.CompareTag("Player"))
            {
                NetworkFPSPlayer player = hit.collider.GetComponent<NetworkFPSPlayer>();

                if (player != null && IsServer)
                {
                    float damage = damagePerSecond * Time.deltaTime;
                    player.TakeDamage(damage);
                }
            }
        }
        else
        {
            lineRenderer.SetPosition(1, startPoint.position + startPoint.forward * maxDistance);
        }
    }
}

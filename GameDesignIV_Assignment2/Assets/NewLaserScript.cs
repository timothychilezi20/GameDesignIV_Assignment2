using UnityEngine;

public class NewLaserScript : MonoBehaviour
{
    private LineRenderer lineRenderer;
    [SerializeField] private Transform startPoint;

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
        if (Physics.Raycast(transform.position, -transform.right, out hit))
        {
            if (hit.collider)
            {
                lineRenderer.SetPosition(1, hit.point);
            }

            if (hit.transform.tag == "Player")
            {
                Destroy(hit.transform.gameObject);
                Debug.Log("Player hit!");
            }
        }

        else lineRenderer.SetPosition(1, -transform.forward * 5000f);
    }
}

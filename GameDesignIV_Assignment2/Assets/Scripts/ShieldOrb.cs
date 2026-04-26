using UnityEngine;
using System.Collections;

public class ShieldOrb : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float flashStartTime = 2f;
    [SerializeField] private float flashSpeed = 8f;

    private Transform followTarget; // the spawn point empty
    private Renderer orbRenderer;
    private bool isFlashing = false;

    public void Initialize(Transform spawnPoint)
    {
        followTarget = spawnPoint;
        orbRenderer = GetComponent<Renderer>();
        StartCoroutine(LifetimeRoutine());
    }

    void Update()
    {
        if (followTarget != null)
            transform.position = followTarget.position;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime - flashStartTime);

        isFlashing = true;
        StartCoroutine(Flash());

        yield return new WaitForSeconds(flashStartTime);
        Destroy(gameObject);
    }

    private IEnumerator Flash()
    {
        while (isFlashing)
        {
            orbRenderer.enabled = !orbRenderer.enabled;
            yield return new WaitForSeconds(1f / flashSpeed);
        }
    }

    void OnDestroy()
    {
        isFlashing = false;
        // Make sure renderer is visible when destroyed mid-flash
        if (orbRenderer != null)
            orbRenderer.enabled = true;
    }
}
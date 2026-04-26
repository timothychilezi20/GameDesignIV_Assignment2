using UnityEngine;

public class TopDownFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float height = 15f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 newPos = target.position;
        newPos.y = height;

        transform.position = newPos;
        transform.rotation = Quaternion.Euler(90f, 0f, 360f);
    }
}

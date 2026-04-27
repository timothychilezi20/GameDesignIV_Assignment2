using UnityEngine;
using UnityEngine.InputSystem;

public class MouseIndicator : MonoBehaviour
{
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
        Cursor.visible = false; // hides the default OS cursor
    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            transform.position = worldPoint;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f; // set to 0 for instant snap

    private Rigidbody rb;
    private Camera cam;
    private Vector3 moveInput;
    private Vector2 lookInput;


    private float speedMultiplier = 1f;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;

        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        
        FaceMouseCursor();
    }


    // Called by PlayerInput component (Send Messages or Broadcast Messages mode)
    void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        moveInput = new Vector3(input.x, 0f, input.y).normalized;
    }

    void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    void FixedUpdate()
    {
        // Movement
        rb.linearVelocity = new Vector3(
      moveInput.x * moveSpeed * speedMultiplier,
      rb.linearVelocity.y,
      moveInput.z * moveSpeed * speedMultiplier
  );

        // Rotation
        FaceMouseCursor();
    }

    void FaceMouseCursor()
    {
        Ray ray = cam.ScreenPointToRay(lookInput);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 direction = worldPoint - transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                if (rotationSpeed <= 0f)
                    transform.rotation = targetRotation;
                else
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        Time.fixedDeltaTime * rotationSpeed
                    );
            }
        }
    }


    //Trap functions 
    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public void SpeedBoost(float SpeedMultiplier)
    {
        speedMultiplier = SpeedMultiplier;
    }

    public void ResetSpeedMultiplier()
    {
        speedMultiplier = 1f;
    }
}

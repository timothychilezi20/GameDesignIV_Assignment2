using UnityEngine;

public class PinballBounce : MonoBehaviour
{
    [SerializeField] private float bounceSpeed = 15f;
    [SerializeField] private LayerMask bounceableLayers;

    private Rigidbody rb;
    private PlayerController _playerController;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _playerController = GetComponent<PlayerController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if ((bounceableLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        Vector3 normal = collision.contacts[0].normal;
        Vector3 reflected = Vector3.Reflect(rb.linearVelocity.normalized, normal);
        rb.linearVelocity = reflected * bounceSpeed;

        // Tell PlayerController to stay out of the way
        if (_playerController != null)
            _playerController.SetPinballActive(true);

        Debug.Log($"[PinballBounce] Bounced off {collision.gameObject.name}");
    }

    void OnCollisionExit(Collision collision)
    {
        if ((bounceableLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        // Allow PlayerController to resume once player leaves the surface
        if (_playerController != null)
            _playerController.SetPinballActive(false);
    }
}
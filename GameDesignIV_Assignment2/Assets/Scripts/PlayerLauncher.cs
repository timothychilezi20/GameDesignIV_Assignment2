using UnityEngine;
using Unity.Netcode;


[RequireComponent(typeof(Rigidbody))]
public class PlayerLauncher : NetworkBehaviour
{
    [Header("Launch Settings")]
    public float minForce = 10f;
    public float maxForce = 40f;
    public float chargeSpeed = 20f;

    private float currentForce;
    private bool isCharging;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            StartChargingServerRpc();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (isCharging)
        {
            currentForce += chargeSpeed * Time.deltaTime;
            currentForce = Mathf.Clamp(currentForce, minForce, maxForce);

            if (Input.GetKeyUp(KeyCode.Space))
            {
                ReleaseLaunchServerRpc(currentForce);
                isCharging = false;
            }
        }
    }

    [ServerRpc]
    private void StartChargingServerRpc()
    {
        currentForce = minForce;
        isCharging = true;
    }

    [ServerRpc]
    private void ReleaseLaunchServerRpc(float force)
    {
        Launch(force);
    }

    private void Launch(float force)
    {
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(transform.forward * force, ForceMode.Impulse);
    }
}

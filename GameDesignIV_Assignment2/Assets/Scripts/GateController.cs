using UnityEngine;
using Unity.Netcode;

public class GateController : NetworkBehaviour
{
    [Header("Gate Settings")]
    [SerializeField] private Transform gateVisual;
    [SerializeField] private Vector3 openOffset = new Vector3(0, 5f, 0);
    [SerializeField] private float openSpeed = 2f;

    private Vector3 closedPos;
    private Vector3 openPos;

    private bool isOpening = false;

    private void Awake()
    {
        if (gateVisual == null)
            gateVisual = transform;

        closedPos = gateVisual.localPosition;
        openPos = closedPos + openOffset;
    }

    private void Update()
    {
        if (!isOpening) return;

        gateVisual.localPosition = Vector3.Lerp(
            gateVisual.localPosition,
            openPos,
            Time.deltaTime * openSpeed
        );
    }

    public void OpenGate()
    {
        isOpening = true;
        Debug.Log("[GATE] Opening...");
    }

    public void CloseGate()
    {
        isOpening = false;
        gateVisual.localPosition = closedPos;
    }
}
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkFPSPlayer))]
[RequireComponent(typeof(PlayerInput))]
public class Interactor : NetworkBehaviour
{
    private NetworkFPSPlayer stats;
    private InteractableItems interactableItem;

    [SerializeField] private GameObject interactText;

    private PlayerInput playerInput;
    private InputAction interactAction;

    private void Start()
    {
        stats = GetComponent<NetworkFPSPlayer>();
        playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            interactAction = playerInput.actions["Interact"];
            interactAction?.Enable();
        }
    }

    private void Update()
    {
        // Only the local player should interact
        if (!IsOwner) return;

        if (interactText != null)
        {
            interactText.SetActive(interactableItem != null);
        }

        if (interactableItem != null && interactAction != null && interactAction.WasPressedThisFrame())
        {
            if (stats != null && stats.IsAlive)
            {
                Interact();
            }
        }
    }

    private void Interact()
    {
        if (interactableItem == null) return;

        interactableItem.Interact(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        InteractableItems found = other.GetComponent<InteractableItems>();

        if (found != null)
        {
            interactableItem = found;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        InteractableItems found = other.GetComponent<InteractableItems>();

        if (found == interactableItem)
        {
            interactableItem = null;
        }
    }
}
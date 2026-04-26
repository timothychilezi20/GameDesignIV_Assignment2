using UnityEngine;
using System.Collections;

public class FreezeTrap : MonoBehaviour
{
    [SerializeField] private float freezeDuration = 4f;

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            StartCoroutine(FreezePlayer(player));
    }

    private IEnumerator FreezePlayer(PlayerController player)
    {
        player.ApplySpeedMultiplier(0f);
        yield return new WaitForSeconds(freezeDuration);
        player.ResetSpeedMultiplier();
    }
}
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class LaunchPoint : NetworkBehaviour
{
    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 20f;
    [SerializeField] private int playersRequired = 2;

    public bool BothLaunched => _launchedCount >= playersRequired;

    private int _launchedCount = 0;
    private HashSet<ulong> _launchedIds = new HashSet<ulong>();

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        ulong id = pc.OwnerClientId;
        if (_launchedIds.Contains(id)) return;

        _launchedIds.Add(id);
        _launchedCount++;

        pc.LaunchFromServer(transform.forward, launchForce);

        Debug.Log($"[LaunchPoint] Launched player {id}. Total: {_launchedCount}/{playersRequired}");
    }
}
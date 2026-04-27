using UnityEngine;
using Unity.Netcode;

public class NetworkTestLauncher : MonoBehaviour
{
    private string statusMessage = "Not Connected";

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        // Always show status
        GUI.Label(new Rect(10, 160, 300, 30), statusMessage);

        // Show join buttons if not yet connected
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(10, 10, 150, 40), "Start Host"))
            {
                NetworkManager.Singleton.StartHost();
                statusMessage = "Running as Host (Player 1)";
            }

            if (GUI.Button(new Rect(10, 60, 150, 40), "Start Client"))
            {
                NetworkManager.Singleton.StartClient();
                statusMessage = "Running as Client (Player 2)";
            }

            if (GUI.Button(new Rect(10, 110, 150, 40), "Start Server"))
            {
                NetworkManager.Singleton.StartServer();
                statusMessage = "Running as Server";
            }
        }
        else
        {
            // Show who you are once connected
            GUI.Label(new Rect(10, 10, 300, 30), $"IsHost: {NetworkManager.Singleton.IsHost}");
            GUI.Label(new Rect(10, 40, 300, 30), $"IsClient: {NetworkManager.Singleton.IsClient}");
            GUI.Label(new Rect(10, 70, 300, 30), $"ClientId: {NetworkManager.Singleton.LocalClientId}");
            GUI.Label(new Rect(10, 100, 300, 30), $"Connected Clients: {NetworkManager.Singleton.ConnectedClients.Count}");
        }
    }
}
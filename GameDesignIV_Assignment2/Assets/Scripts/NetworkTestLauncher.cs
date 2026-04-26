using UnityEngine;
using Unity.Netcode;

public class NetworkTestLauncher : MonoBehaviour
{
    private void OnGUI()
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(10, 10, 150, 40), "Start Host"))
                NetworkManager.Singleton.StartHost();

            if (GUI.Button(new Rect(10, 60, 150, 40), "Start Client"))
                NetworkManager.Singleton.StartClient();

            if (GUI.Button(new Rect(10, 110, 150, 40), "Start Server"))
                NetworkManager.Singleton.StartServer();
        }
    }
}
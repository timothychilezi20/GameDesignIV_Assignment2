using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{

    [SerializeField] private TMP_InputField ipInput;
    [SerializeField] private TMP_InputField portInput;

    [SerializeField] private string defaultIP = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7777;

    [SerializeField] private UnityTransport transport;
    [SerializeField] private NetworkManager networkManager;

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}

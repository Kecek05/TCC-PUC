using Unity.Netcode;
using UnityEngine;

public class NetworkHostStarterDebug : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Singleton.StartHost();
    }
}

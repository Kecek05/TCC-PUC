using Sirenix.OdinInspector;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [Title("Singletons")]
    [SerializeField] private ClientManager clientPrefab;
    [SerializeField] private HostManager hostPrefab;

    private void Start()
    {
        Instantiate(clientPrefab);
        Instantiate(hostPrefab);
    }
}

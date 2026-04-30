using Sirenix.OdinInspector;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [Title("Singletons")]
    [SerializeField] private BaseClientManager clientPrefab;
    [SerializeField] private BaseHostManager hostPrefab;

    private void Awake()
    {
        if (IsDedicatedServer())
        {
            Application.targetFrameRate = 60;
            return;
        }
        
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 180;
    }

    private void Start()
    {
        if (IsDedicatedServer())
        {
            Debug.Log("TODO: Dedicated server code");
            return;
        }
        
        Instantiate(clientPrefab);
        Instantiate(hostPrefab);
    }

    private bool IsDedicatedServer()
    {
        return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}

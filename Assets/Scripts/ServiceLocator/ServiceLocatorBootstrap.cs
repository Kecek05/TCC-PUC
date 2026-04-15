using System;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class ServiceLocatorBootstrap : MonoBehaviour
{
    public static event Action OnServiceLocatorInitialized;

    private void Awake()
    {
        RegisterGameFlowManager();
    }

    private void RegisterGameFlowManager()
    {
        BaseGameFlowManager gameFlowManager = gameObject.AddComponent<GameFlowManager>();
        
        ServiceLocator.Register(gameFlowManager);
    }
}

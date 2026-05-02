using System;
using UnityEngine;

public class EventsDebugManager : MonoBehaviour, IOnPlayerLoaded
{
    public event Action<string> OnPlayerLoaded;

    private bool firstUpdate = false;
    
    private void Awake()
    {
        ServiceLocator.Register<IOnPlayerLoaded>(this);
    }
    
    public void Update()
    {
        if (firstUpdate) return;
        
        firstUpdate = true;
        
        OnPlayerLoaded?.Invoke("ID");
        
        OnPlayerLoaded?.Invoke("ID2");
    }
}

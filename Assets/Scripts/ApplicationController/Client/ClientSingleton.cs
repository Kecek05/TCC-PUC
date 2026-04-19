using System;
using UnityEngine;

public class ClientSingleton : MonoBehaviour
{
    private static ClientSingleton instance;

    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindFirstObjectByType<ClientSingleton>();

            if (instance == null)
            {
                Debug.LogError("No ClientSingleton found in the scene.");
                return null;
            }

            return instance;
        }
    }
    
    private ClientAuth clientAuth;
    public ClientAuth ClientAuth => clientAuth;

    private async void Awake()
    {
        DontDestroyOnLoad(gameObject);
        
        clientAuth = new ClientAuth();

        print(await clientAuth.TryInitAsync());
    }

    private void OnDestroy()
    {
        clientAuth?.Dispose();
    }
}

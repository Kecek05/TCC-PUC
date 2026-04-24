using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class AuthServiceManager : MonoBehaviour
{
    [SerializeField] private GameObject backgroundObject;
    [SerializeField] private GameObject relayObject;
    
    [SerializeField] private DebugSettingsSO  debugSettings;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 180;
        relayObject.SetActive(false);
    }

    private async void Start()
    {
        if (!debugSettings.isDebug)
        {
            Destroy(backgroundObject);
            relayObject.SetActive(true);
            return;
        }
        
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += () => GameLog.Info($"Signed in as {AuthenticationService.Instance.PlayerId}");
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Destroy(backgroundObject);
        relayObject.SetActive(true);
    }
}
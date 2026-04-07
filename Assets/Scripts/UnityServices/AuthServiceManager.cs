using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class AuthServiceManager : MonoBehaviour
{
    [SerializeField] private GameObject backgroundObject;
    [SerializeField] private GameObject relayObject;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 180;
        relayObject.SetActive(false);
    }

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += () => Debug.Log($"Signed in as {AuthenticationService.Instance.PlayerId}");
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Destroy(backgroundObject);
        relayObject.SetActive(true);
    }
}
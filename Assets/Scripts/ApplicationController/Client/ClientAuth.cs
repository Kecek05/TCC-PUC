using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ClientAuth : IDisposable
{
    private AuthState authState = AuthState.NotAuthenticated;
    
    public async Task<bool> TryInitAsync()
    {
        //Debugging code FOR DEDICATED SERVER
        // InitializationOptions initializationOptions = new InitializationOptions();
        // initializationOptions.SetProfile(UnityEngine.Random.Range(0, 10000).ToString());
        // await UnityServices.InitializeAsync(initializationOptions);
        //
        
        await UnityServices.InitializeAsync();
        
        await DoAuthAnonymously();
        
        if (authState == AuthState.Authenticated)
        {
            GameLog.Info($"Player - {authState} - {AuthenticationService.Instance.PlayerId}");
            return true;
        }

        GameLog.Info($"Player - {authState} - {AuthenticationService.Instance.PlayerId}");
        return false;
    }

    private async Task<AuthState> DoAuthAnonymously(int maxTries = 5)
    {
        if (authState == AuthState.Authenticated) return authState;

        if (authState == AuthState.Authenticating)
        {
            GameLog.Warn("Already authenticating.");
            await Authenticating();
            return authState;
        }

        PlayerPrefs.DeleteKey("AccessToken");

        await SignInAnonymouslyAsync(maxTries);

        return authState;
    }
    
    private async Task SignInAnonymouslyAsync(int maxTries = 5)
    {
        authState = AuthState.Authenticating;

        int tries = 0;

        while (authState == AuthState.Authenticating && tries < maxTries)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                {
                    authState = AuthState.Authenticated;

                    break;
                }

            }
            catch (AuthenticationException authEx)
            {
                GameLog.Error(authEx);
                authState = AuthState.Error;
            }
            catch (RequestFailedException requestEx)
            {
                GameLog.Error(requestEx);
                authState = AuthState.Error;
            }


            tries++;

            await Task.Delay(1000);
        }

        if (authState != AuthState.Authenticated)
        {
            GameLog.Warn($"Player could not authenticate after {tries} tries.");
            authState = AuthState.TimeOut;
            // OnSignInFail?.Invoke();
        }
    }
    
    private async Task<AuthState> Authenticating()
    {
        while (authState == AuthState.Authenticating || authState == AuthState.NotAuthenticated)
        {
            await Task.Delay(200);
        }

        return authState;
    }
    
    public void Dispose()
    {
        
    }
}

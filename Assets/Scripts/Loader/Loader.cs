using Unity.Netcode;
using UnityEngine.SceneManagement;

public static class Loader
{
    private static Scene targetScene;

    private static Scene currentScene;

    private static LoadType loadType;
    public static Scene CurrentScene => currentScene;

    /// <summary>
    /// Whether a scene name is one a match actually runs in. Anything gating on "are we in the match yet"
    /// must ask this rather than compare against GameScene alone — TutorialScene is a second gameplay
    /// scene, and a check that names only one of them silently does nothing in the other.
    /// </summary>
    public static bool IsGameplayScene(string sceneName) =>
        sceneName == Scene.GameScene.ToString() || sceneName == Scene.TutorialScene.ToString();
    
    public enum Scene
    {
        None,
        Loading,
        AuthBootstrap,
        GameScene,
        MainMenu,
        StartScene,
        NoNetwork,

        /// <summary>The scripted first-time-experience match. A copy of GameScene, hosted locally.</summary>
        TutorialScene
    }
    
    public enum LoadType
    {
        None,
        Client,
        Host,
        DS,
    }
    
    /// <summary>
    /// Called to load a scene.
    /// </summary>
    /// <param name="scene"> Scene to go to</param>
    public static void Load(Scene scene)
    {
        targetScene = scene;
        currentScene = Scene.Loading;
        loadType = LoadType.None;

        // Every switch is a circle wipe: the screen is covered first, then the scene changes under it.
        SceneTransition.Cover(() => SceneManager.LoadScene(Scene.Loading.ToString()));
    }
    
    /// <summary>
    /// Called from host to load the scene.
    /// </summary>
    /// <param name="scene"> Scene to go to</param>
    public static void LoadHostNetwork(Scene scene)
    {
        loadType = LoadType.Host;
        targetScene = scene;
        currentScene = Scene.Loading;

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null)
        {
            GameLog.Error("Loader.LoadHostNetwork called before NetworkManager.SceneManager was ready. " +
                          "Make sure NetworkManager.StartHost() succeeded before calling this.");
            return;
        }

        SceneTransition.Cover(() =>
        {
            // The host can have gone in the moment the wipe took.
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) return;
            NetworkManager.Singleton.SceneManager.LoadScene(Scene.Loading.ToString(), LoadSceneMode.Single);
        });
    }
    
    /// <summary>
    /// Called to load the client in server.
    /// </summary>
    /// <param name="scene"> Scene to go to</param>
    public static void LoadClient()
    {
        loadType = LoadType.Client;
        currentScene = Scene.Loading;

        SceneTransition.Cover(() => SceneManager.LoadScene(Scene.Loading.ToString()));
    }
    
    public static void LoadCallback()
    {
        // The loading screen wipes out before what it was loading arrives, like every other switch.
        Scene target = targetScene;
        switch(loadType)
        {
            case LoadType.None:
                currentScene = target;
                SceneTransition.Cover(() => SceneManager.LoadScene(target.ToString()));
                break;
            case LoadType.Client:
                // Debug.Log($"Load Callback Client Connect");
                currentScene = Scene.GameScene;
                SceneTransition.Cover(() => ServiceLocator.Get<BaseClientManager>().ConnectClient());
                break;
            case LoadType.Host:
                currentScene = target;
                SceneTransition.Cover(() =>
                {
                    if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) return;
                    NetworkManager.Singleton.SceneManager.LoadScene(target.ToString(), LoadSceneMode.Single);
                });
                break;
        }
    }
}

using Unity.Netcode;
using UnityEngine.SceneManagement;

public static class Loader
{
    private static Scene targetScene;

    private static Scene currentScene;

    private static LoadType loadType;
    public static Scene CurrentScene => currentScene;
    
    public enum Scene
    {
        None,
        Loading,
        AuthBootstrap,
        GameScene,
        MainMenu,
        StartScene,
        NoNetwork
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

        SceneManager.LoadScene(Scene.Loading.ToString());
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

        NetworkManager.Singleton.SceneManager.LoadScene(Scene.Loading.ToString(), LoadSceneMode.Single);
    }
    
    /// <summary>
    /// Called to load the client in server.
    /// </summary>
    /// <param name="scene"> Scene to go to</param>
    public static void LoadClient()
    {
        loadType = LoadType.Client;
        currentScene = Scene.Loading;

        SceneManager.LoadScene(Scene.Loading.ToString());
    }
    
    public static void LoadCallback()
    {
        switch(loadType)
        {
            case LoadType.None:
                SceneManager.LoadScene(targetScene.ToString());
                currentScene = targetScene;
                break;
            case LoadType.Client:
                // Debug.Log($"Load Callback Client Connect");
                ServiceLocator.Get<BaseClientManager>().ConnectClient();
                currentScene = Scene.GameScene;
                break;
            case LoadType.Host:
                NetworkManager.Singleton.SceneManager.LoadScene(targetScene.ToString(), LoadSceneMode.Single);
                currentScene = targetScene;
                break;
        }
    }
}

using UnityEngine.SceneManagement;

public static class Loader
{
    private static Scene targetScene;

    private static Scene currentScene;

    public static Scene CurrentScene => currentScene;
    
    public enum Scene
    {
        None,
        Loading,
        AuthBootstrap,
        GameScene,
        MainMenu,
        StartScene,
    }
    
    /// <summary>
    /// Called to load a scene.
    /// </summary>
    /// <param name="scene"> Scene to go to</param>
    public static void Load(Scene scene)
    {
        targetScene = scene;
        currentScene = Scene.Loading;

        SceneManager.LoadScene(Scene.Loading.ToString());
    }
    
    public static void LoadCallback()
    {
        SceneManager.LoadScene(targetScene.ToString());
        currentScene = targetScene;
    }
}

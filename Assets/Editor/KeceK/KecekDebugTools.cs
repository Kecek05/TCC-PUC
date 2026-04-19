#if UNITY_EDITOR
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor.KeceK
{
    public static class KecekDebugTools
    {
        private const string DEBUG_SETTINGS_ASSET_PATH = "Assets/ScriptableObjects/Debug/DebugSettingsSO.asset";
        private const string NETWORK_MANAGER_PREFAB_PATH = "Assets/Prefabs/NetworkManager.prefab";
        private const string GAME_SCENE_ASSET_PATH = "Assets/Scenes/GameScene.unity";
        private const string GAME_SCENE_NAME = "GameScene";
        private const string START_SCENE_NAME = "StartScene";
        private const string DEBUG_MODE_MENU_PATH = "Kecek/Debug Tools/Debug Mode";
        private const string AUTO_ENABLED_SESSION_KEY = "KecekDebugTools.AutoEnabledForGameScene";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    HandleExitingEditMode();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    HandleEnteredEditMode();
                    break;
            }
        }

        private static void HandleExitingEditMode()
        {
            DebugSettingsSO settings = AssetDatabase.LoadAssetAtPath<DebugSettingsSO>(DEBUG_SETTINGS_ASSET_PATH);
            if (settings == null) return;

            string activeSceneName = SceneManager.GetActiveScene().name;

            if (activeSceneName == START_SCENE_NAME && settings.isDebug)
            {
                SetDebugMode(settings, false);
                Debug.Log("[Kecek] Debug Mode auto-disabled: starting from StartScene.");
                return;
            }

            if (activeSceneName == GAME_SCENE_NAME && !settings.isDebug)
            {
                SetDebugMode(settings, true);
                SessionState.SetBool(AUTO_ENABLED_SESSION_KEY, true);
                Debug.Log("[Kecek] Debug Mode auto-enabled: starting from GameScene.");
            }
        }

        private static void HandleEnteredEditMode()
        {
            if (!SessionState.GetBool(AUTO_ENABLED_SESSION_KEY, false)) return;
            SessionState.EraseBool(AUTO_ENABLED_SESSION_KEY);

            DebugSettingsSO settings = AssetDatabase.LoadAssetAtPath<DebugSettingsSO>(DEBUG_SETTINGS_ASSET_PATH);
            if (settings == null) return;

            SetDebugMode(settings, false);
            Debug.Log("[Kecek] Debug Mode auto-disabled: exited GameScene play session.");
        }

        [MenuItem("Kecek/Debug Tools/Clear PlayerPrefs")]
        public static void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("PlayerPrefs cleared.");
        }

        [MenuItem(DEBUG_MODE_MENU_PATH)]
        public static void ToggleDebugMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            DebugSettingsSO settings = AssetDatabase.LoadAssetAtPath<DebugSettingsSO>(DEBUG_SETTINGS_ASSET_PATH);
            if (settings == null)
            {
                Debug.LogError($"DebugSettingsSO not found at {DEBUG_SETTINGS_ASSET_PATH}");
                return;
            }

            SetDebugMode(settings, !settings.isDebug);
        }

        private static void SetDebugMode(DebugSettingsSO settings, bool enable)
        {
            if (settings.isDebug == enable) return;

            Undo.RecordObject(settings, "Set Debug Mode");
            settings.isDebug = enable;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            Menu.SetChecked(DEBUG_MODE_MENU_PATH, enable);

            (Scene gameScene, bool openedHere) = GetOrLoadGameScene();
            if (!gameScene.IsValid()) return;

            if (enable) EnsureNetworkManager(gameScene);
            else        RemoveNetworkManager(gameScene);

            if (openedHere)
            {
                EditorSceneManager.SaveScene(gameScene);
                EditorSceneManager.CloseScene(gameScene, removeScene: true);
            }
        }

        private static (Scene scene, bool openedHere) GetOrLoadGameScene()
        {
            Scene existing = SceneManager.GetSceneByName(GAME_SCENE_NAME);
            if (existing.IsValid() && existing.isLoaded)
                return (existing, false);

            if (!System.IO.File.Exists(GAME_SCENE_ASSET_PATH))
            {
                Debug.LogError($"GameScene not found at {GAME_SCENE_ASSET_PATH}");
                return (default, false);
            }

            Scene loaded = EditorSceneManager.OpenScene(GAME_SCENE_ASSET_PATH, OpenSceneMode.Additive);
            return (loaded, true);
        }

        [MenuItem(DEBUG_MODE_MENU_PATH, true)]
        public static bool ToggleDebugModeValidate()
        {
            DebugSettingsSO settings = AssetDatabase.LoadAssetAtPath<DebugSettingsSO>(DEBUG_SETTINGS_ASSET_PATH);
            Menu.SetChecked(DEBUG_MODE_MENU_PATH, settings != null && settings.isDebug);
            return settings != null && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void EnsureNetworkManager(Scene scene)
        {
            if (FindNetworkManager(scene) != null) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NETWORK_MANAGER_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"NetworkManager prefab not found at {NETWORK_MANAGER_PREFAB_PATH}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate NetworkManager");
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"NetworkManager spawned in {scene.name}");
        }

        private static void RemoveNetworkManager(Scene scene)
        {
            GameObject existing = FindNetworkManager(scene);
            if (existing == null) return;

            Undo.DestroyObjectImmediate(existing);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"NetworkManager removed from {scene.name}");
        }

        private static GameObject FindNetworkManager(Scene scene)
        {
            if (!scene.IsValid()) return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                NetworkManager nm = root.GetComponentInChildren<NetworkManager>(includeInactive: true);
                if (nm != null) return nm.gameObject;
            }
            return null;
        }
    }
}
#endif
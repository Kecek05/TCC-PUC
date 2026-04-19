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
        private const string GAME_SCENE_NAME = "GameScene";
        private const string DEBUG_MODE_MENU_PATH = "Kecek/Debug Tools/Debug Mode";

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

            bool newValue = !settings.isDebug;

            Undo.RecordObject(settings, "Toggle Debug Mode");
            settings.isDebug = newValue;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            Menu.SetChecked(DEBUG_MODE_MENU_PATH, newValue);

            Scene active = SceneManager.GetActiveScene();
            if (active.name != GAME_SCENE_NAME) return;

            if (newValue) EnsureNetworkManager(active);
            else          RemoveNetworkManager(active);
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
            NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            return nm != null && nm.gameObject.scene == scene ? nm.gameObject : null;
        }
    }
}
#endif
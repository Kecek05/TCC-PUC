using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kecek.Editor
{
    /// <summary>
    /// Puts a <see cref="JuicyButton"/> on every <see cref="Button"/> the player can press, in the UI prefabs first
    /// and then in the menu and match scenes, and configures the few that are special (the one call to action
    /// breathes; buttons that commit something click with Confirm). Safe to run again: a button that already has
    /// one is left as it is, so hand-tuned settings survive.
    /// </summary>
    /// <remarks>
    /// Skipped on purpose: <see cref="MenuNavButton"/>s (they pop their own icon on the same transform, and the
    /// nav controller plays their sound), full-screen backdrops that close a modal (a tap outside is not a
    /// button press), and the Quantum Console (a developer tool).
    /// </remarks>
    public static class JuicyButtonInstaller
    {
        /// <summary>Base prefabs before the variants that inherit from them, so a variant never gets its own copy.</summary>
        private static readonly string[] Prefabs =
        {
            "Assets/Prefabs/UI/Menu/DeckPage/TabButtons/TabButton.prefab",
            "Assets/Prefabs/UI/Menu/DeckPage/Card_V2.prefab",
            "Assets/Prefabs/UI/Menu/DeckPage/Card.prefab",
            "Assets/Prefabs/UI/Menu/DeckPage/ActionButtons.prefab",
            "Assets/Prefabs/UI/Tutorial/TutorialOverlayCanvas.prefab",
        };

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/TutorialScene.unity",
        };

        [MenuItem("Kecek/Juice/Add Juicy Buttons (Prefabs And Scenes)")]
        public static void AddEverywhere()
        {
            // Never a dialog (this runs from automation too) and never someone else's unsaved edits.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.LogError("[Juice] Save or discard the open scene's changes before adding juicy buttons.");
                return;
            }

            string returnTo = SceneManager.GetActiveScene().path;
            List<string> report = new();

            foreach (string path in Prefabs) report.Add(AddToPrefab(path));
            foreach (string path in Scenes) report.Add(AddToScene(path));

            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log("[Juice] Juicy buttons:\n" + string.Join("\n", report));
        }

        private static string AddToPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return $"{path}: missing";

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int added = 0;
            try
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    if (TryAdd(button, inScene: false)) added++;

                if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return $"{path}: +{added}";
        }

        private static string AddToScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int added = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (TryAdd(button, inScene: true)) added++;

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return $"{path}: +{added}";
        }

        private static bool TryAdd(Button button, bool inScene)
        {
            GameObject go = button.gameObject;
            if (go.GetComponent<JuicyButton>() != null || go.GetComponent<MenuNavButton>() != null) return false;
            if (IsUnder(go.transform, "Quantum Console")) return false;

            // A prefab instance's button is the prefab's business: it was handled above, at the source.
            if (inScene && PrefabUtility.IsPartOfPrefabInstance(go)) return false;

            // A full-screen dim that closes a modal: no transition, and as big as the screen.
            RectTransform rect = (RectTransform)go.transform;
            if (button.transition == Selectable.Transition.None && rect.rect.width >= 1000f && rect.rect.height >= 1000f) return false;

            JuicyButton juicy = go.AddComponent<JuicyButton>();
            SerializedObject so = new SerializedObject(juicy);

            // An invisible hit area laid over a card (Card_V2's Button): the card body around it is what squashes.
            Image image = go.GetComponent<Image>();
            bool invisible = image == null || image.color.a < 0.05f;
            if (invisible && go.transform.parent != null)
            {
                so.FindProperty("target").objectReferenceValue = go.transform.parent;

                // The card answers its tap with the action popup's own pop, so a click sound would double it.
                so.FindProperty("clickSfx").enumValueIndex = IndexOf(SfxId.None);
            }

            switch (go.name)
            {
                case "BattleButton":
                    so.FindProperty("clickSfx").enumValueIndex = IndexOf(SfxId.Confirm);
                    so.FindProperty("breatheWhenIdle").boolValue = true;
                    break;
                case "UpgradeButton":
                case "PlayAgainButton":
                case "JoinButton":
                    so.FindProperty("clickSfx").enumValueIndex = IndexOf(SfxId.Confirm);
                    break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>Enum fields serialize by index; SfxId is append-only and starts at 0, so index == value.</summary>
        private static int IndexOf(SfxId id) => System.Array.IndexOf(System.Enum.GetValues(typeof(SfxId)), id);

        private static bool IsUnder(Transform t, string name)
        {
            for (; t != null; t = t.parent)
                if (t.name.Contains(name)) return true;
            return false;
        }
    }
}

using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kecek.Editor
{
    /// <summary>
    /// One-shot scene and prefab setup for the match half of the Juice &amp; Feedback System, kept as a menu item
    /// so it can be re-run (it updates in place) rather than being a pile of hand edits nobody can repeat.
    /// </summary>
    public static class JuiceMatchSetup
    {
        private const string FloatingTextPrefabPath = "Assets/Prefabs/Presentation/JuiceFloatingText.prefab";
        private const string FontPath = "Assets/Font/Fredoka/SDF/Fredoka-Bold SDF.asset";
        private const string SpawnerName = "FloatingNumbersSpawner";

        private static readonly string[] MatchScenes =
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/TutorialScene.unity",
        };

        /// <summary>
        /// Damage numbers and payouts were raised (<see cref="FloatingNumbers"/>) but nothing listened in the match
        /// scenes, so none ever showed. Builds the pooled number prefab and puts a spawner in both match scenes.
        /// </summary>
        [MenuItem("Kecek/Juice/Build Floating Numbers (Prefab + Match Scenes)")]
        public static void BuildFloatingNumbers()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.LogError("[Juice] Save or discard the open scene's changes before building floating numbers.");
                return;
            }

            JuiceFloatingText prefab = BuildPrefab();
            string returnTo = SceneManager.GetActiveScene().path;

            foreach (string path in MatchScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                AddSpawner(scene, prefab);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log("[Juice] Floating numbers ready: " + FloatingTextPrefabPath + " + a spawner in each match scene.");
        }

        private static JuiceFloatingText BuildPrefab()
        {
            GameObject root = new GameObject("JuiceFloatingText");
            root.AddComponent<MMPoolableObject>();
            JuiceFloatingText floating = root.AddComponent<JuiceFloatingText>();

            Transform moving = new GameObject("Moving").transform;
            moving.SetParent(root.transform, false);
            Transform billboard = new GameObject("Billboard").transform;
            billboard.SetParent(moving, false);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(billboard, false);
            TextMeshPro text = textGo.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.fontSize = 2.7f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = "0";
            text.rectTransform.sizeDelta = new Vector2(3f, 1f);
            text.sortingLayerID = SortingLayer.NameToID("UI");
            text.sortingOrder = 100;

            floating.MovingPart = moving;
            floating.Billboard = billboard;
            floating.Bind(text);

            JuiceFloatingText saved = PrefabUtility.SaveAsPrefabAsset(root, FloatingTextPrefabPath).GetComponent<JuiceFloatingText>();
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void AddSpawner(Scene scene, JuiceFloatingText prefab)
        {
            MMFloatingTextSpawner spawner = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != SpawnerName) continue;
                spawner = root.GetComponent<MMFloatingTextSpawner>();
            }

            if (spawner == null)
            {
                GameObject go = new GameObject(SpawnerName);
                SceneManager.MoveGameObjectToScene(go, scene);
                spawner = go.AddComponent<MMFloatingTextSpawner>();
            }

            spawner.ChannelMode = MMChannelModes.Int;
            spawner.Channel = 0;
            spawner.UseUnscaledTime = false;

            // budget-numbers: never more than 16 alive. A full pool skips a number rather than growing.
            spawner.PoolerMode = MMFloatingTextSpawner.PoolerModes.Simple;
            spawner.PooledSimpleMMFloatingText = prefab;
            spawner.PoolSize = 16;
            spawner.PoolCanExpand = false;

            spawner.Lifetime = new Vector2(0.7f, 0.82f);
            spawner.SpawnOffsetMin = new Vector3(-0.12f, 0f, 0f);
            spawner.SpawnOffsetMax = new Vector3(0.12f, 0.08f, 0f);

            // A quick rise that slows (ease-out), never a drift.
            spawner.AnimateMovement = true;
            spawner.AnimateX = false;
            spawner.AnimateY = true;
            spawner.RemapYZero = Vector2.zero;
            spawner.RemapYOne = new Vector2(0.5f, 0.68f);
            spawner.AnimateYCurve = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
            spawner.AnimateZ = false;

            spawner.AlignmentMode = MMFloatingTextSpawner.AlignmentModes.Fixed;
            spawner.FixedAlignment = Vector3.up;
            spawner.AlwaysFaceCamera = false;

            // Pop in past full size, settle, shrink a little as it fades.
            spawner.AnimateScale = true;
            spawner.RemapScaleZero = Vector2.zero;
            spawner.RemapScaleOne = Vector2.one;
            spawner.AnimateScaleCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.12f, 1.3f), new Keyframe(0.3f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0.75f));

            spawner.AnimateOpacity = true;
            spawner.RemapOpacityZero = Vector2.zero;
            spawner.RemapOpacityOne = Vector2.one;
            spawner.AnimateOpacityCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));

            // A heavy hit's number is bigger (FloatingNumbers passes its intensity).
            spawner.IntensityImpactsScale = true;
            spawner.IntensityScaleMultiplier = 1f;
            spawner.IntensityImpactsMovement = false;
            spawner.IntensityImpactsLifetime = false;

            EditorUtility.SetDirty(spawner);
        }
    }
}

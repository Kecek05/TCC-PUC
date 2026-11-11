using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KeceK.Capture.EditorTools
{
    /// <summary>
    /// Editor entry points for the capture tools, as menu items and as static methods the Unity CLI can call
    /// (<c>KeceK.Capture.EditorTools.CaptureMenu.PlayAsset("...")</c>). Everything here only starts programs;
    /// what a program does is its own asset.
    /// </summary>
    public static class CaptureMenu
    {
        public const string Root = "Assets/_Capture";
        public const string SettingsPath = Root + "/Settings/CaptureSettings.asset";

        [MenuItem("Kecek/Capture/Play Selected Shot Or Script")]
        public static void PlaySelected()
        {
            if (Selection.activeObject is ICaptureProgram program) Play(program);
            else Debug.LogWarning("[Capture] Select a ShotDefinition or MatchScript asset.");
        }

        [MenuItem("Kecek/Capture/Play Selected Shot Or Script", true)]
        private static bool PlaySelectedValidate() => EditorApplication.isPlaying && Selection.activeObject is ICaptureProgram;

        [MenuItem("Kecek/Capture/Abort")]
        public static void Abort() => CaptureDirector.Instance?.Abort("aborted from menu");

        /// <summary>CLI: runs the shot or match script at <paramref name="assetPath"/>.</summary>
        public static string PlayAsset(string assetPath)
        {
            if (!EditorApplication.isPlaying) return "Not in play mode";

            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset is not ICaptureProgram program) return $"No capture program at {assetPath}";
            return Play(program) ? $"Started {program.ProgramName}" : "Refused: " + Status();
        }

        /// <summary>CLI: runs several shots back to back, in the order given.</summary>
        public static string PlayAssets(params string[] assetPaths)
        {
            if (!EditorApplication.isPlaying) return "Not in play mode";

            List<ICaptureAction> sequence = new();
            CaptureSettingsSO settings = null;
            foreach (string path in assetPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is not ICaptureProgram program) return $"No capture program at {path}";
                settings ??= program.Settings;
                sequence.AddRange(program.BuildSequence());
            }

            return CaptureRunner.Play($"batch x{assetPaths.Length}", settings, "Trailer/raw", sequence) ? "Started batch" : "Refused: " + Status();
        }

        public static bool Play(ICaptureProgram program) => CaptureRunner.Play(program);

        public static string Status() => CaptureDirector.Instance != null ? CaptureDirector.Instance.Status : "no director";

        public static string LogTail(int lines = 40)
        {
            if (CaptureDirector.Instance == null) return "";
            string[] all = CaptureDirector.Instance.Context.LogText.Split('\n');
            int start = Mathf.Max(0, all.Length - lines);
            return string.Join("\n", all, start, all.Length - start);
        }

        /// <summary>Writes the edit's view of every shot (file, cut-in, length, caption) for the ffmpeg montage.</summary>
        [MenuItem("Kecek/Capture/Export Trailer Edit List")]
        public static string ExportEditList()
        {
            List<ShotDefinition> shots = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ShotDefinition", new[] { Root }))
            {
                ShotDefinition shot = AssetDatabase.LoadAssetAtPath<ShotDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (shot != null) shots.Add(shot);
            }

            shots.Sort((a, b) => a.Number != b.Number ? a.Number.CompareTo(b.Number) : string.CompareOrdinal(a.Part, b.Part));

            StringBuilder json = new();
            json.AppendLine("[");
            for (int i = 0; i < shots.Count; i++)
            {
                ShotDefinition s = shots[i];
                json.Append("  {")
                    .Append($"\"number\": {s.Number}, ")
                    .Append($"\"part\": \"{s.Part}\", ")
                    .Append($"\"file\": \"{s.EditSource}.mp4\", ")
                    .Append($"\"cutIn\": {s.CutIn.ToString(System.Globalization.CultureInfo.InvariantCulture)}, ")
                    .Append($"\"seconds\": {s.CutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}, ")
                    .Append($"\"caption\": \"{Escape(s.Caption)}\"")
                    .Append(i < shots.Count - 1 ? "},\n" : "}\n");
            }
            json.AppendLine("]");

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", "Trailer", "edit-list.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
            Debug.Log($"[Capture] Edit list: {path}");
            return path;
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

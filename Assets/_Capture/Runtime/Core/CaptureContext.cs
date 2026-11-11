#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// What an action may reach: the match's services (resolved lazily, so one context survives a scene load),
    /// the virtual finger, the recorder and the capture log. Holds no rules of its own.
    /// </summary>
    public class CaptureContext
    {
        public CaptureDirector Director { get; }
        public VirtualFinger Finger { get; }
        public ICaptureRecorder Recorder => CaptureRecorderHub.Get();

        /// <summary>The project's Recordings folder (beside Assets), created on demand.</summary>
        public string RecordingsRoot { get; }

        /// <summary>Game data the actions read. Set by whoever starts a shot or a match script.</summary>
        public CaptureSettingsSO Settings { get; set; }

        /// <summary>Sub-folder a recording action writes into, e.g. "Trailer/raw".</summary>
        public string OutputFolder { get; set; } = "Trailer/raw";

        private readonly StringBuilder _log = new();
        private readonly List<(string label, float videoTime)> _marks = new();
        private float _recordingStartedAt = -1f;

        public CaptureContext(CaptureDirector director)
        {
            Director = director;
            Finger = new VirtualFinger(director);
            RecordingsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
        }

        // ---------------------------------------------------------------- services (null outside a match)

        public BaseTeamManager Teams => Lookup<BaseTeamManager>();
        public BaseServerManaManager ServerMana => Lookup<BaseServerManaManager>();
        public BaseServerWaveManager Waves => Lookup<BaseServerWaveManager>();
        public BaseServerPlayerHealthManager Health => Lookup<BaseServerPlayerHealthManager>();
        public BaseGameFlowManager Flow => Lookup<BaseGameFlowManager>();
        public BaseCardContainer Hand => Lookup<BaseCardContainer>();
        public BaseCardTowerDeployer TowerDeployer => Lookup<BaseCardTowerDeployer>();
        public BaseCardSpellDeployer SpellDeployer => Lookup<BaseCardSpellDeployer>();
        public BaseCardSpawnEnemyDeployer TroopDeployer => Lookup<BaseCardSpawnEnemyDeployer>();
        public BaseClientManager Client => Lookup<BaseClientManager>();
        public BasePlayerSaveManager Save => Lookup<BasePlayerSaveManager>();
        public BaseHostManager Host => Lookup<BaseHostManager>();

        public TeamType LocalTeam => Teams != null && Teams.HasLocalTeamBeenAssigned() ? Teams.GetLocalTeam() : TeamType.Red;
        public TeamType RivalTeam => LocalTeam == TeamType.Red ? TeamType.Blue : TeamType.Red;

        public GameState State => Flow != null ? Flow.CurrentGameState.Value : GameState.None;

        public Camera MainCamera => Camera.main;

        public CameraSlide Slide => Object.FindFirstObjectByType<CameraSlide>();

        public TeamType Side(BoardSide side) => side == BoardSide.Local ? LocalTeam : RivalTeam;

        private static T Lookup<T>() where T : class => ServiceLocator.TryGet(out T service) ? service : null;

        // ---------------------------------------------------------------- recording bookkeeping

        public void NotifyRecordingStarted(string name)
        {
            _recordingStartedAt = Director.Clock;
            _marks.Clear();
            Log($"REC start {name}");
        }

        public void NotifyRecordingStopped()
        {
            Log($"REC stop at {VideoTime:0.00}s");
            _recordingStartedAt = -1f;
        }

        /// <summary>Seconds into the current recording (video time), or -1 when not recording.</summary>
        public float VideoTime => _recordingStartedAt < 0f ? -1f : Director.Clock - _recordingStartedAt;

        public IReadOnlyList<(string label, float videoTime)> Marks => _marks;

        public void Mark(string label)
        {
            _marks.Add((label, VideoTime));
            Log($"MARK {VideoTime:0.00}s {label}");
        }

        public void Log(string line)
        {
            string stamped = $"[{Director.Clock,8:0.00}] {line}";
            _log.AppendLine(stamped);
            Debug.Log($"[Capture] {stamped}");
        }

        public string LogText => _log.ToString();

        public string ResolveOutput(string fileName)
        {
            string folder = Path.Combine(RecordingsRoot, OutputFolder);
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName);
        }
    }

    public enum BoardSide { Local, Rival }
}
#endif

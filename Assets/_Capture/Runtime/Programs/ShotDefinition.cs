#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>Something the director can run: a trailer shot or a whole match script.</summary>
    public interface ICaptureProgram
    {
        string ProgramName { get; }
        CaptureSettingsSO Settings { get; }
        string OutputFolder { get; }
        List<ICaptureAction> BuildSequence();
    }

    /// <summary>
    /// One shot of the trailer: the deck it needs, the staging that sets it up off tape, the beats that are
    /// filmed, and how the edit uses it (where its cut starts, how long it runs, the caption over it).
    /// </summary>
    [CreateAssetMenu(fileName = "Shot", menuName = "Kecek/Capture/Shot Definition")]
    public class ShotDefinition : ScriptableObject, ICaptureProgram
    {
        public CaptureSettingsSO Settings;

        [Header("Identity")]
        public int Number = 1;
        public string Slug = "shot";
        [TextArea] public string Notes = "";

        [Header("Match")]
        [Tooltip("Start a fresh local match with Deck before this shot (leaving any match first). Off: reuse the match the previous shot left running.")]
        public bool NewMatch = true;
        public List<CardType> Deck = new();
        [Tooltip("Leave the match after the shot (off when the next shot continues in it).")]
        public bool LeaveAfter = true;

        [Header("Off tape")]
        [SerializeReference] public List<ICaptureAction> Setup = new();

        [Header("On tape")]
        public float LeadInSeconds = 1.5f;
        [SerializeReference] public List<ICaptureAction> Beats = new();
        public float TailSeconds = 1.5f;

        [Header("Edit")]
        [Tooltip("Where the shot's cut starts in its raw file, in seconds (after the lead-in).")]
        public float CutIn = 1.5f;
        [Tooltip("How long the shot runs in the trailer.")]
        public float CutSeconds = 2f;
        [Tooltip("On-screen caption (PT-BR). Empty: none.")]
        public string Caption = "";

        public string FileName => $"plano_{Number:00}_{Slug}";

        string ICaptureProgram.ProgramName => FileName;
        CaptureSettingsSO ICaptureProgram.Settings => Settings;
        public string OutputFolder => "Trailer/raw";

        public List<ICaptureAction> BuildSequence()
        {
            List<ICaptureAction> sequence = new();

            if (NewMatch)
            {
                sequence.Add(new LeaveMatch());
                sequence.Add(new StartLocalMatch(Deck.ToArray()));
            }

            sequence.Add(new HideDebugUi());
            sequence.AddRange(Setup);
            sequence.Add(new BeginRecording(FileName));
            sequence.Add(new Wait(LeadInSeconds));
            sequence.AddRange(Beats);
            sequence.Add(new Wait(TailSeconds));
            sequence.Add(new EndRecording());
            sequence.Add(new TimeScaleRamp(1f, 0f));
            sequence.Add(new CameraReset { Seconds = 0f });

            if (LeaveAfter) sequence.Add(new LeaveMatch());
            return sequence;
        }
    }
}
#endif

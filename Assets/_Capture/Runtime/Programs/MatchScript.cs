#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// A whole take: menu, match and end screen, as one list of steps with the recording switched on and off
    /// inside it (the dry cuts). The rival is the real bot; only the player's side is scripted, and every play
    /// waits for the game to offer it (the card in hand, the mana, the wave) rather than forcing it.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchScript", menuName = "Kecek/Capture/Match Script")]
    public class MatchScript : ScriptableObject, ICaptureProgram
    {
        public CaptureSettingsSO Settings;
        public string TakeName = "take";
        public string Folder = "Gameplay/raw";
        public List<CardType> Deck = new();
        [TextArea] public string Notes = "";

        [SerializeReference] public List<ICaptureAction> Steps = new();

        public string ProgramName => TakeName;
        CaptureSettingsSO ICaptureProgram.Settings => Settings;
        public string OutputFolder => Folder;

        public List<ICaptureAction> BuildSequence() => new(Steps);
    }

    /// <summary>The one entry point that hands a program to the director.</summary>
    public static class CaptureRunner
    {
        public static bool Play(ICaptureProgram program)
        {
            CaptureDirector director = CaptureDirector.Ensure();
            director.Context.Settings = program.Settings;
            director.Context.OutputFolder = program.OutputFolder;
            return director.Play(program.ProgramName, program.BuildSequence());
        }

        public static bool Play(string name, CaptureSettingsSO settings, string folder, List<ICaptureAction> sequence)
        {
            CaptureDirector director = CaptureDirector.Ensure();
            director.Context.Settings = settings;
            director.Context.OutputFolder = folder;
            return director.Play(name, sequence);
        }
    }
}
#endif

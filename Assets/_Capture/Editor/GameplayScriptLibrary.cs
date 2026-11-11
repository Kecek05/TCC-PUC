using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static KeceK.Capture.BoardTarget;

namespace KeceK.Capture.EditorTools
{
    /// <summary>
    /// Writes the gameplay video's <see cref="MatchScript"/>: one real match against the real bot, filmed in
    /// segments (each segment is a file; the gaps between them are the dry cuts). Only the player's side is
    /// scripted, and every play waits for the game to offer it: the card in hand, the mana, the wave. Nothing
    /// is staged: no raised ceiling, no health floor, no lane hold, no spawned enemy.
    /// </summary>
    public static class GameplayScriptLibrary
    {
        public const string Folder = CaptureMenu.Root + "/Gameplay";
        public const string AssetPath = Folder + "/MatchScript_Gameplay.asset";

        private const BoardSide L = BoardSide.Local;
        private const BoardSide R = BoardSide.Rival;

        public static readonly CardType[] Deck =
        {
            CardType.TowerDart, CardType.TowerMortar, CardType.TowerBeacon, CardType.TowerChain,
            CardType.SpellLance, CardType.SpellIce, CardType.SpawnEnemyArmy, CardType.SpawnEnemyMiniBoss,
        };

        private static readonly CardType[] OtherDeck =
        {
            CardType.TowerSquare, CardType.TowerSlam, CardType.TowerCircle, CardType.TowerNeedle,
            CardType.SpellFireball, CardType.SpellRift, CardType.SpawnEnemyRam, CardType.SpellHaste,
        };

        [MenuItem("Kecek/Capture/Build Gameplay Script")]
        public static string Build()
        {
            MatchScript script = ScriptableObject.CreateInstance<MatchScript>();
            script.Settings = AssetDatabase.LoadAssetAtPath<CaptureSettingsSO>(CaptureMenu.SettingsPath);
            script.TakeName = "gameplay";
            script.Folder = "Gameplay/raw";
            script.Deck = new List<CardType>(Deck);
            script.Notes = "Menu -> deck page -> real match vs the bot -> rewards. Segments are files; the gaps are dry cuts.";
            script.Steps = Steps();

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(CaptureMenu.Root, "Gameplay");
            MatchScript existing = AssetDatabase.LoadAssetAtPath<MatchScript>(AssetPath);
            if (existing == null) AssetDatabase.CreateAsset(script, AssetPath);
            else
            {
                EditorUtility.CopySerialized(script, existing);
                existing.name = "MatchScript_Gameplay";
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(script);
            }

            AssetDatabase.SaveAssets();
            return $"Wrote {AssetPath}";
        }

        /// <summary>Cards the policy leaves alone: the scripted beats need them (and a troop it sent on its own
        /// could end the match before the beats are filmed).</summary>
        private static readonly CardType[] Held = { CardType.SpellIce, CardType.SpawnEnemyArmy, CardType.SpawnEnemyMiniBoss };

        /// <summary>From wave 5 the Lance is a scripted beat too.</summary>
        private static readonly CardType[] HeldAll = { CardType.SpellIce, CardType.SpawnEnemyArmy, CardType.SpawnEnemyMiniBoss, CardType.SpellLance };

        private static AutoPlay Auto(WaitUntil until, bool finger, bool iceHop, float keepMana, params CardType[] reserve) =>
            new(until, reserve) { ShowFinger = finger, AllowIceHop = iceHop, KeepMana = keepMana };

        private static List<ICaptureAction> Steps()
        {
            WaitUntil forever = WaitUntil.InState(GameState.EndMatch, 900f);

            return new List<ICaptureAction>
            {
                // ── off tape: the menu as a player would find it, two saved decks
                new LeaveMatch(),
                new PrepareSave
                {
                    Decks = new List<DeckSlotContent> { new(0, Deck), new(1, OtherDeck) },
                    ActiveSlot = 0,
                },
                new HideDebugUi(),
                new Wait(1.0f),

                // ── 1. menu and deck (file 01)
                new BeginRecording("gameplay_01_menu"),
                new Wait(1.0f),
                new Mark("1 menu e deck"),
                new TapUi("Deck", "Buttons"),
                new Wait(2.2f),
                new TapUi("Button", "DeckEntry2"),
                new Wait(1.6f),
                new TapUi("Button", "DeckEntry1"),
                new Wait(2.0f),
                new TapUi("Battle", "Buttons"),
                new Wait(1.6f),
                new EndRecording(),

                // ── dry cut 1: the real Battle button opens a public Relay lobby; the take hosts offline instead
                new StartLocalMatch(Deck) { ReturnAtSceneLoad = true },
                new HideDebugUi(),

                // ── 2-7. one continuous take of a real match: this bot's base falls around our wave 5-6, so the
                // whole match is about the length of the video. Beats come when the game allows them.
                new MatchGuard(new Sequence(
                    new BeginRecording("gameplay_02_partida"),
                    WaitUntil.InState(GameState.InMatch, 90f),
                    new HideDebugUi(),
                    new Wait(4.2f), // the VS introduction
                    new Mark("2 vertice na curva"),
                    new PlayCard(CardType.TowerDart, Slot(0.3f), 0.6f) { WaitForCardSeconds = 20f },
                    new Wait(0.7f, 1.1f),
                    new Mark("3 evolucao nivel 2"),
                    new PlayCard(CardType.TowerDart, UpgradableTowerOf(CardType.TowerDart), 0.6f) { WaitForCardSeconds = 15f },
                    new RunUntil(WaitUntil.Wave(2, L, 60f), Auto(forever, true, false, 0f, Held)),
                    new Mark("~corte-inicio"),
                    new RunUntil(WaitUntil.WaveDone(4, L, 0.6f, 240f), Auto(forever, true, false, 0f, Held)),
                    new Mark("~corte-fim"),

                    // 4. the ceiling rises as wave 5 starts: Beacon and Swarm (6) join the cycle. Hold on it.
                    new RunUntil(WaitUntil.Wave(5, L, 90f), Auto(forever, true, false, 0f, Held)),
                    new Mark("4 teto sobe"),
                    new Wait(1.2f),

                    // 5-6. The mana decides the order: at a ceiling of 6, Ice (4), Lance (5) and Swarm (6) cannot
                    // follow each other, and this bot's base falls to the first Swarm. So: over to their side for
                    // a look and Ice on their towers; home for a Lance down a line; then the Swarm to finish.
                    new RunUntil(WaitUntil.Mana(4, 30f), Auto(forever, true, false, 99f, HeldAll)),
                    new Mark("5 swipe para o rival"),
                    new Swipe(R),
                    new Wait(2.2f),
                    new Mark("5 ice nas torres"),
                    new PlayCard(CardType.SpellIce, TowerBunch(R, 1.0f), 0.5f) { WaitForCardSeconds = 5f },
                    new Wait(2.0f),
                    new Swipe(L),
                    new RunUntil(WaitUntil.Enemies(3, L, 0.3f, 30f), Auto(forever, true, false, 5f, HeldAll)),
                    new RunUntil(WaitUntil.Mana(5, 20f), Auto(forever, true, false, 99f, HeldAll)),
                    new Mark("6 restia"),
                    new PlayCard(CardType.SpellLance, Bunch(L, 1.8f), 0.55f) { WaitForCardSeconds = 10f },
                    new RunUntil(WaitUntil.InHand(CardType.SpawnEnemyArmy, 30f), Auto(forever, true, false, 0f, HeldAll)),
                    new RunUntil(WaitUntil.Mana(6, 30f), Auto(forever, true, false, 99f, HeldAll)),
                    new Swipe(R),
                    new Wait(1.0f),
                    new Mark("5 enxame"),
                    new PlayCard(CardType.SpawnEnemyArmy, Path(0.5f, R), 0.55f) { WaitForCardSeconds = 5f },
                    new Wait(2.0f),

                    // 7. the last waves until the end screen.
                    new Mark("7 ultimas waves"),
                    new RunUntil(WaitUntil.InState(GameState.EndMatch, 400f), Auto(forever, true, false, 0f, CardType.SpawnEnemyMiniBoss)),
                    new HideDebugUi(),
                    new Mark("7 vitoria e recompensas"),
                    new Wait(7.5f),
                    new EndRecording()
                ), "gameplay_03_fim"),
                new LeaveMatch(),
            };
        }
    }
}

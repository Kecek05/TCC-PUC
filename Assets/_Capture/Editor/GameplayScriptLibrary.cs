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

                // ── 2-3. the opening: a Dart on a bend, a second Dart on it (file 02)
                new BeginRecording("gameplay_02_inicio"),
                WaitUntil.InState(GameState.InMatch, 90f),
                new HideDebugUi(),
                new Wait(4.2f), // the VS introduction
                new Mark("2 vertice na curva"),
                new PlayCard(CardType.TowerDart, Slot(0.3f), 0.6f) { WaitForCardSeconds = 20f },
                new Wait(0.7f, 1.1f),
                new Mark("3 evolucao nivel 2"),
                new PlayCard(CardType.TowerDart, UpgradableTowerOf(CardType.TowerDart), 0.6f) { WaitForCardSeconds = 15f },
                new RunUntil(WaitUntil.Wave(2, L, 60f),
                    Auto(forever, true, false, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce)),
                new Wait(1.0f),
                new EndRecording(),

                // ── dry cut 2: waves 2-4 played off tape
                new RunUntil(WaitUntil.WaveDone(4, L, 0.55f, 400f),
                    Auto(forever, false, true, 0f, CardType.SpawnEnemyMiniBoss), 3f),

                // ── 4. the ceiling rises at the wave 4 -> 5 change, a costlier card joins the cycle (file 03)
                new BeginRecording("gameplay_03_teto"),
                new RunUntil(WaitUntil.Wave(5, L, 60f),
                    Auto(forever, true, false, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce)),
                new Mark("4 teto sobe"),
                new Wait(1.2f), // hold on it
                new RunUntil(WaitUntil.Standing(CardType.TowerBeacon, L, 14f),
                    Auto(forever, true, false, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce)),
                new Wait(1.5f),
                new EndRecording(),

                // ── dry cut 3: waves 5-6 off tape, banking for the Swarm
                new RunUntil(WaitUntil.WaveDone(6, L, 0.5f, 400f),
                    Auto(forever, false, true, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpawnEnemyArmy), 3f),
                new RunUntil(WaitUntil.InHand(CardType.SpawnEnemyArmy, 120f),
                    Auto(forever, false, true, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpawnEnemyArmy), 3f),
                new RunUntil(WaitUntil.Mana(6, 60f),
                    Auto(forever, false, false, 99f, CardType.SpawnEnemyMiniBoss, CardType.SpawnEnemyArmy), 2f),

                // ── 5a. over to their side: a look, the Swarm (file 04)
                new BeginRecording("gameplay_04_rival"),
                new Wait(0.6f),
                new Mark("5 swipe para o rival"),
                new Swipe(R),
                new Wait(2.4f),
                new Mark("5 enxame"),
                new PlayCard(CardType.SpawnEnemyArmy, Path(0.5f, R), 0.55f),
                new Wait(2.2f),
                new Swipe(L),
                new Wait(1.0f),
                new EndRecording(),

                // ── dry cut 4: waves 7-8 off tape; then a full bar for the Mini Boss with Ice in hand
                new RunUntil(WaitUntil.WaveDone(8, L, 0.5f, 500f),
                    Auto(forever, false, false, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce), 3f),
                new RunUntil(WaitUntil.InHand(CardType.SpellIce, 120f),
                    Auto(forever, false, false, 0f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce), 3f),
                new RunUntil(WaitUntil.MaxMana(10, 200f),
                    Auto(forever, false, false, 4f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce), 3f),
                new RunUntil(WaitUntil.Mana(10, 60f),
                    Auto(forever, false, false, 99f, CardType.SpawnEnemyMiniBoss, CardType.SpellIce), 2f),

                // ── 5b-7. the Mini Boss, Ice on their line as it walks it, a Lance at home, the end (file 05)
                new BeginRecording("gameplay_05_final"),
                new Wait(0.6f),
                new Swipe(R),
                new Wait(0.9f),
                new Mark("5 casco"),
                new PlayCard(CardType.SpawnEnemyMiniBoss, Path(0.6f, R), 0.55f),
                new Wait(1.0f),
                new Swipe(L),
                new RunUntil(WaitUntil.Mana(4, 30f),
                    Auto(forever, true, false, 4f, CardType.SpellIce, CardType.SpellLance)),
                new Swipe(R),
                new Wait(0.8f),
                new Mark("5 ice nas torres"),
                new PlayCard(CardType.SpellIce, TowerBunch(R, 1.0f), 0.5f) { WaitForCardSeconds = 6f },
                new Wait(2.0f),
                new Swipe(L),
                new RunUntil(WaitUntil.Enemies(4, L, 0.3f, 40f),
                    Auto(forever, true, false, 5f, CardType.SpellLance)),
                new Mark("6 restia"),
                new PlayCard(CardType.SpellLance, Bunch(L, 1.8f), 0.55f) { WaitForCardSeconds = 25f },
                new Mark("7 ultimas waves"),
                new RunUntil(WaitUntil.InState(GameState.EndMatch, 400f),
                    Auto(forever, true, false, 0f)),
                new HideDebugUi(),
                new Mark("7 vitoria e recompensas"),
                new Wait(7.5f),
                new EndRecording(),
                new LeaveMatch(),
            };
        }
    }
}

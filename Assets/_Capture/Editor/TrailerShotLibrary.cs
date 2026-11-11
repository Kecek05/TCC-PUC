using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static KeceK.Capture.BoardTarget;

namespace KeceK.Capture.EditorTools
{
    /// <summary>
    /// Writes the trailer's <see cref="ShotDefinition"/> assets from one readable list, so the whole script can
    /// be read top to bottom and re-tuned in one place. Re-running it updates the assets in place (same
    /// GUIDs), so tuning a shot is: edit here, run <c>Kecek/Capture/Build Trailer Shots</c>, re-record that shot.
    /// </summary>
    public static class TrailerShotLibrary
    {
        public const string Folder = CaptureMenu.Root + "/Trailer/Shots";

        private const BoardSide L = BoardSide.Local;
        private const BoardSide R = BoardSide.Rival;

        [MenuItem("Kecek/Capture/Build Trailer Shots")]
        public static string Build()
        {
            CaptureSettingsSO settings = AssetDatabase.LoadAssetAtPath<CaptureSettingsSO>(CaptureMenu.SettingsPath);
            EnsureFolder(Folder);

            List<ShotDefinition> shots = Define();
            foreach (ShotDefinition shot in shots)
            {
                shot.Settings = settings;
                if (shot.NewMatch && string.IsNullOrEmpty(shot.SourceFile) && shot.Setup.Exists(a => a is HoldWaves))
                {
                    // Whatever the bot sent before it was paused may have reached a base: full lives on tape.
                    shot.Setup.Add(new SetLives(L, 100f));
                    shot.Setup.Add(new SetLives(R, 100f));
                }
                Save(shot);
            }

            AssetDatabase.SaveAssets();
            return $"Wrote {shots.Count} shots to {Folder}";
        }

        public static string PathOf(int number, string part = "")
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ShotDefinition", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ShotDefinition shot = AssetDatabase.LoadAssetAtPath<ShotDefinition>(path);
                if (shot != null && shot.Number == number && shot.Part == part) return path;
            }

            return null;
        }

        // =============================================================================== the script

        private static List<ShotDefinition> Define()
        {
            List<ShotDefinition> s = new();

            // 1-2 ─ one take: the Mortar dropped in front of a packed wave, then its salvo clearing it.
            ShotDefinition one = Shot(1, "", "uma-carta",
                new[] { CardType.TowerMortar, CardType.TowerDart, CardType.SpellIce, CardType.SpellLance });
            one.Setup = Stage(pauseBot: true);
            one.Setup.Add(new SpawnCrowd(L, EnemyType.Triangle1, 9, 0.62, 0.68));
            one.Setup.Add(new Wait(1.1f)); // past their spawn invincibility
            one.LeadInSeconds = 0.8f;
            one.Beats = new List<ICaptureAction>
            {
                new PlayCard(CardType.TowerMortar, Slot(0.83f), 0.6f) { TopUpMana = true },
                new Wait(5f),
            };
            Edit(one, 0.0f, 2.0f, "UMA CARTA.");
            s.Add(one);

            ShotDefinition two = Shot(2, "", "uma-wave");
            two.SourceFile = one.FileName;
            Edit(two, 2.0f, 2.0f, "UMA WAVE INTEIRA.");
            s.Add(two);

            // 3-4 ─ one take: swipe to the rival, Swarm then Rage on it.
            ShotDefinition three = Shot(3, "", "lado-dele",
                new[] { CardType.SpawnEnemyArmy, CardType.SpellRage, CardType.TowerDart, CardType.SpellIce });
            three.Setup = Stage(pauseBot: true);
            three.Setup.AddRange(new ICaptureAction[]
            {
                new RivalPlaceTower(CardType.TowerSquare, 0.35f),
                new RivalPlaceTower(CardType.TowerSlam, 0.6f),
                new RivalPlaceTower(CardType.TowerCircle, 0.82f),
                new PlayCard(CardType.TowerDart, Slot(0.4f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new SpawnCrowd(L, EnemyType.Triangle1, 4, 0.18, 0.3),
                new Wait(1.5f),
            });
            three.LeadInSeconds = 0.8f;
            three.Beats = new List<ICaptureAction>
            {
                new Swipe(R),
                new Wait(0.35f),
                new PlayCard(CardType.SpawnEnemyArmy, Path(0.45f, R), 0.5f) { TopUpMana = true },
                new Wait(2.6f), // the troops stand invincible for a second, then walk in off the edge
                new PlayCard(CardType.SpellRage, Bunch(R, 1.6f), 0.45f) { TopUpMana = true },
                new Wait(3.0f),
            };
            Edit(three, 0.0f, 2.0f, "AGORA, O LADO DELE.");
            s.Add(three);

            ShotDefinition four = Shot(4, "", "caos");
            four.SourceFile = three.FileName;
            Edit(four, 2.0f, 2.0f, "MANDE O CAOS.");
            s.Add(four);

            // 5 ─ the HUD in a live match: both corners (a swipe across), the hand of four and the next card.
            ShotDefinition five = Shot(5, "", "hud",
                new[] { CardType.TowerDart, CardType.TowerMortar, CardType.TowerBeacon, CardType.SpellIce,
                        CardType.SpellLance, CardType.SpellRift, CardType.SpawnEnemyArmy, CardType.TowerChain });
            five.Setup = new List<ICaptureAction>
            {
                WaitUntil.Wave(1, L, 30f),
                new ProtectMatch { HealthFloor = 30f },
                new RaiseManaCap(10),
                new Wait(1.5f),
                new PlayCard(CardType.TowerDart, Slot(0.35f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new PlayCard(CardType.TowerMortar, Slot(0.7f), 0.3f) { TopUpMana = true, ShowFinger = false, WaitForCardSeconds = 6f },
                WaitUntil.Enemies(3, L, 0f, 20f),
                new Wait(1.5f),
            };
            five.Beats = new List<ICaptureAction>
            {
                new Wait(1.0f),
                new Swipe(R),
                new Wait(1.3f),
                new Swipe(L),
                new Wait(1.0f),
            };
            Edit(five, 0.3f, 3.0f, "PvP 1v1 EM TEMPO REAL");
            s.Add(five);

            // 6 ─ Dart placed, then a second Dart on the same tower (a 4-card deck brings it straight back).
            ShotDefinition six = Shot(6, "", "evolua",
                new[] { CardType.TowerDart, CardType.SpellIce, CardType.SpellLance, CardType.SpellRift });
            six.Setup = Stage(pauseBot: true);
            six.Setup.Add(new SpawnCrowd(L, EnemyType.Triangle1, 5, 0.12, 0.3));
            six.Setup.Add(new Wait(1.1f));
            six.LeadInSeconds = 0.6f;
            six.Beats = new List<ICaptureAction>
            {
                new PlayCard(CardType.TowerDart, Slot(0.42f), 0.55f) { TopUpMana = true },
                new Wait(0.9f),
                new PlayCard(CardType.TowerDart, TowerOf(CardType.TowerDart), 0.6f) { TopUpMana = true },
                new Wait(2.0f),
            };
            Edit(six, 0.2f, 3.0f, "Arraste. Posicione. Evolua.");
            s.Add(six);

            // 7 ─ the natural wave 4 -> 5 change: the ceiling rises 5 -> 6 and Beacon (6) joins the cycle as the
            // only card outside the hand, so it is the next card. No staging of the mana: the table does it.
            ShotDefinition seven = Shot(7, "", "teto",
                new[] { CardType.TowerDart, CardType.SpellIce, CardType.SpellLance, CardType.TowerShard, CardType.TowerBeacon });
            seven.Setup = new List<ICaptureAction>
            {
                WaitUntil.Wave(1, L, 30f),
                new ProtectMatch { HealthFloor = 40f },
                new PlayCard(CardType.TowerDart, Slot(0.3f), 0.3f) { ShowFinger = false, WaitForCardSeconds = 15f },
                new FastForward(WaitUntil.Wave(3, L, 200f), 4f),
                new PlayCard(CardType.TowerShard, Slot(0.6f), 0.3f) { ShowFinger = false, WaitForCardSeconds = 40f },
                new PlayCard(CardType.TowerDart, Slot(0.45f), 0.3f) { ShowFinger = false, WaitForCardSeconds = 30f },
                new FastForward(WaitUntil.Wave(4, L, 200f), 4f),
                new FastForward(WaitUntil.WaveDone(4, L, 0.7f, 200f), 3f),
            };
            Edit(seven, 1.6f, 3.0f, "Cada wave libera cartas mais fortes.");
            seven.LeadInSeconds = 0.2f;
            seven.Beats = new List<ICaptureAction>
            {
                WaitUntil.Wave(5, L, 40f),
                new Mark("wave5"),
                new Wait(4.0f),
            };
            seven.TailSeconds = 0.5f;
            s.Add(seven);

            // 8 ─ a wave in single file, Lance down the lane.
            ShotDefinition eight = Shot(8, "", "restia",
                new[] { CardType.SpellLance, CardType.TowerDart, CardType.SpellIce, CardType.SpellRift });
            eight.Setup = Stage(pauseBot: true);
            eight.Setup.Add(new SpawnCrowd(L, EnemyType.Triangle1, 7, 0.335, 0.40));
            eight.Setup.Add(new Wait(1.1f));
            eight.LeadInSeconds = 0.6f;
            eight.Beats = new List<ICaptureAction>
            {
                new Wait(0.3f),
                new PlayCard(CardType.SpellLance, Bunch(L, 2.4f), 0.55f) { TopUpMana = true },
                new Wait(2.2f),
            };
            Edit(eight, 0.2f, 3.0f, "Feitiços salvam a sua defesa...");
            s.Add(eight);

            // 9 ─ swipe over, Ice on their towers, a Mini Boss walking past the frozen line.
            ShotDefinition nine = Shot(9, "", "quebram",
                new[] { CardType.SpellIce, CardType.SpawnEnemyMiniBoss, CardType.TowerDart, CardType.SpellLance });
            nine.Setup = Stage(pauseBot: true);
            nine.Setup.AddRange(new ICaptureAction[]
            {
                // Clustered where a sent troop enters their lane, so one Ice (radius 1) catches the whole line.
                new RivalPlaceTower(CardType.TowerSquare, 0.93f),
                new RivalPlaceTower(CardType.TowerDart, 0.88f),
                new RivalPlaceTower(CardType.TowerSlam, 0.83f),
                new Wait(1.0f),
            });
            nine.LeadInSeconds = 0.6f;
            nine.Beats = new List<ICaptureAction>
            {
                new Swipe(R),
                new Wait(0.3f),
                new PlayCard(CardType.SpawnEnemyMiniBoss, Path(0.7f, R), 0.5f) { TopUpMana = true },
                new Wait(0.9f),
                new PlayCard(CardType.SpellIce, TowerBunch(R, 1.0f), 0.45f) { TopUpMana = true },
                new Wait(6.0f),
            };
            Edit(nine, 0.0f, 3.0f, "...ou quebram a dele.");
            s.Add(nine);

            // 10 ─ Rift holds a packed wave in front of a tier-3 Chain (levelled off tape).
            ShotDefinition ten = Shot(10, "", "combine",
                new[] { CardType.SpellRift, CardType.TowerChain, CardType.TowerDart, CardType.SpellIce });
            ten.Setup = Stage(pauseBot: true);
            ten.Setup.AddRange(new ICaptureAction[]
            {
                new PlayCard(CardType.TowerChain, Slot(0.52f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new Wait(0.8f),
                new PlayCard(CardType.TowerChain, TowerOf(CardType.TowerChain), 0.3f) { TopUpMana = true, ShowFinger = false },
                new Wait(0.8f),
                new PlayCard(CardType.TowerChain, TowerOf(CardType.TowerChain), 0.3f) { TopUpMana = true, ShowFinger = false },
                new Wait(1.0f),
                new SpawnCrowd(L, EnemyType.Triangle1, 10, 0.34, 0.42),
                new Wait(1.1f),
            });
            ten.LeadInSeconds = 0.4f;
            ten.Beats = new List<ICaptureAction>
            {
                new PlayCard(CardType.SpellRift, Bunch(L, 1.4f), 0.5f) { TopUpMana = true },
                new Wait(4.0f),
            };
            Edit(ten, 0.2f, 4.0f, "Combine cartas.");
            s.Add(ten);

            // 11 ─ the top of the screen: us a wave ahead (6/10), their base taking leaks (staged crowd at their gate).
            ShotDefinition eleven = Shot(11, "", "topo",
                new[] { CardType.TowerMortar, CardType.TowerBeacon, CardType.TowerChain, CardType.TowerDart });
            eleven.Setup = new List<ICaptureAction>
            {
                WaitUntil.Wave(1, L, 30f),
                new ProtectMatch { HealthFloor = 30f },
                new RaiseManaCap(10),
                new PlayCard(CardType.TowerMortar, Slot(0.5f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new PlayCard(CardType.TowerBeacon, Slot(0.75f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new PlayCard(CardType.TowerChain, Slot(0.3f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new PauseBot(),
                new FastForward(WaitUntil.Wave(6, L, 300f), 5f),
                new HoldWaves(true),
                new SetLives(R, 46f),
                new SpawnCrowd(R, EnemyType.Triangle1, 6, 0.84, 0.95),
                new Wait(0.5f),
            };
            eleven.LeadInSeconds = 0.3f;
            eleven.Beats = new List<ICaptureAction>
            {
                new Wait(1.0f),
                new Swipe(R),
                new Wait(2.5f),
            };
            Edit(eleven, 0.3f, 3.0f, "Termine a última wave primeiro.");
            s.Add(eleven);

            // 12 ─ the deck page: eight cards into an empty slot, then a quick switch between two saved decks.
            ShotDefinition twelve = Shot(12, "", "deck");
            twelve.NewMatch = false;
            twelve.LeaveAfter = false;
            CardType[] newDeck = { CardType.TowerMortar, CardType.TowerBeacon, CardType.TowerChain, CardType.TowerNeedle,
                                   CardType.SpellLance, CardType.SpellRift, CardType.SpawnEnemyArmy, CardType.SpawnEnemyMiniBoss };
            twelve.Setup = new List<ICaptureAction>
            {
                new LeaveMatch(),
                new PrepareSave
                {
                    Decks = new List<DeckSlotContent>
                    {
                        new(0, CardType.TowerDart, CardType.TowerSquare, CardType.TowerSlam, CardType.TowerCircle,
                               CardType.SpellFireball, CardType.SpellIce, CardType.SpawnEnemy1, CardType.SpellHaste),
                        new(1, CardType.TowerShard, CardType.TowerPrism, CardType.TowerStinger, CardType.TowerNeedle,
                               CardType.SpellRage, CardType.SpellFerrugem, CardType.SpawnEnemyRam, CardType.SpawnEnemyShadow),
                        new(2),
                    },
                    ActiveSlot = 2,
                },
                new HideDebugUi(),
                new TapUi("Deck", "Buttons") { ShowFinger = false },
                new Wait(1.2f),
            };
            twelve.LeadInSeconds = 0.5f;
            twelve.Beats = new List<ICaptureAction>();
            foreach (CardType card in newDeck)
            {
                twelve.Beats.Add(new TapDeckCard(card) { Hold = 0.06f });
                twelve.Beats.Add(new Wait(0.12f));
                twelve.Beats.Add(new TapUi("ActionButton", "ActionButtons(Clone)") { HoverSeconds = 0f });
                twelve.Beats.Add(new Wait(0.22f));
            }
            twelve.Beats.Add(new Wait(0.4f));
            twelve.Beats.Add(new TapUi("Button", "DeckEntry1"));
            twelve.Beats.Add(new Wait(0.8f));
            twelve.Beats.Add(new TapUi("Button", "DeckEntry2"));
            twelve.Beats.Add(new Wait(0.8f));
            twelve.Beats.Add(new TapUi("Button", "DeckEntry3"));
            twelve.Beats.Add(new Wait(0.8f));
            Edit(twelve, 0.0f, 4.0f, "Monte seu deck de 8 cartas.");
            s.Add(twelve);

            // 13 a-d ─ one second per tower, each its own small match so no tower steals another's target.
            s.Add(TowerSpot("a", "agulha", CardType.TowerNeedle, 0.45f, EnemyType.Triangle1Tank, 1, 0.27, 0.27, tier3: true, beatSeconds: 7f));
            s.Add(TowerSpot("b", "farol", CardType.TowerBeacon, 0.6f, EnemyType.Triangle1Tank, 1, 0.5, 0.5));
            s.Add(TowerSpot("c", "square", CardType.TowerSquare, 0.45f, EnemyType.Triangle1, 6, 0.32, 0.37));
            s.Add(TowerSpot("d", "estilhaco", CardType.TowerShard, 0.55f, EnemyType.Triangle1, 8, 0.44, 0.47, tier3: true));
            s[^4].Caption = "30 cartas. Torres, feitiços e ataques.";

            // 14 ─ the rival sends a Mini Boss; a tier-3 Beacon melts it before it gets anywhere.
            ShotDefinition fourteen = Shot(14, "", "resposta",
                new[] { CardType.TowerBeacon, CardType.TowerDart, CardType.SpellIce, CardType.SpellLance });
            fourteen.Setup = Stage(pauseBot: true);
            foreach (float at in new[] { 0.82f, 0.72f, 0.62f })
            {
                fourteen.Setup.Add(new PlayCard(CardType.TowerBeacon, Slot(at), 0.3f) { TopUpMana = true, ShowFinger = false });
                fourteen.Setup.Add(new Wait(0.7f));
            }
            for (int i = 0; i < 6; i++)
            {
                // Two more drops on each Beacon (tier 3): TowerOf picks the first standing Beacon that is not maxed.
                fourteen.Setup.Add(new PlayCard(CardType.TowerBeacon, UpgradableTowerOf(CardType.TowerBeacon), 0.3f) { TopUpMana = true, ShowFinger = false });
                fourteen.Setup.Add(new Wait(0.7f));
            }
            fourteen.Setup.Add(new Wait(1.0f));
            fourteen.LeadInSeconds = 0.5f;
            fourteen.Beats = new List<ICaptureAction>
            {
                new RivalSendTroops(CardType.SpawnEnemyMiniBoss),
                WaitUntil.AtMost(0, L, 14f),
                new Wait(1.0f),
            };
            Edit(fourteen, 0.0f, 4.0f, "Toda carta tem resposta.");
            s.Add(fourteen);

            // 15 ─ the card page: level, tiers, and a real upgrade (gold and copies, nothing sold).
            ShotDefinition fifteen = Shot(15, "", "nivel");
            fifteen.NewMatch = false;
            fifteen.LeaveAfter = false;
            fifteen.Setup = new List<ICaptureAction>
            {
                new LeaveMatch(),
                new PrepareSave(),
                new HideDebugUi(),
                new TapUi("Deck", "Buttons") { ShowFinger = false },
                new Wait(1.0f),
            };
            fifteen.LeadInSeconds = 0.4f;
            fifteen.Beats = new List<ICaptureAction>
            {
                new TapDeckCard(CardType.TowerBeacon),
                new Wait(0.3f),
                new TapUi("InfoButton", "ActionButtons(Clone)"),
                new Wait(1.2f),
                new TapUi("UpgradeButton", "Panel"),
                new Wait(3.0f),
            };
            Edit(fifteen, 0.4f, 3.0f, "Evolua com ouro e cópias. Nada à venda.");
            s.Add(fifteen);

            // 16-18 ─ one take: last wave on both fields (cuts every 0.75 s), the final kill in slow motion
            // (the director's only simulated effect), VICTORY.
            ShotDefinition sixteen = Shot(16, "", "ultima-wave",
                new[] { CardType.TowerMortar, CardType.TowerBeacon, CardType.TowerChain, CardType.TowerDart });
            sixteen.Setup = new List<ICaptureAction>
            {
                WaitUntil.Wave(1, L, 30f),
                new ProtectMatch { HealthFloor = 6f, HoldLastWave = false },
                new RaiseManaCap(10),
                // A defence that can actually kill wave 10's 2000-HP boss (speed 0.5) before it reaches the base:
                // three Mortars and two Beacons on the last stretch, all at tier 3, a Chain and a Dart up front.
                new Sequence(BuildAndLevel(CardType.TowerMortar, 0.55f, 0.75f, 0.9f)),
                new Sequence(BuildAndLevel(CardType.TowerBeacon, 0.65f, 0.84f)),
                new PlayCard(CardType.TowerChain, Slot(0.35f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new PlayCard(CardType.TowerDart, Slot(0.45f), 0.3f) { TopUpMana = true, ShowFinger = false },
                new PauseBot(),
                new FastForward(WaitUntil.Wave(10, L, 400f), 6f),
                new SetLives(L, 22f),
                new SetLives(R, 14f),
                new SpawnCrowd(R, EnemyType.Triangle1, 6, 0.2, 0.5),
            };
            sixteen.LeadInSeconds = 0.3f;
            sixteen.Beats = new List<ICaptureAction>
            {
                new SnapCamera(L), new Wait(0.75f), new SnapCamera(R), new Wait(0.75f),
                new SnapCamera(L), new Wait(0.75f), new SnapCamera(R), new Wait(0.75f),
                new SnapCamera(L), new Wait(0.75f),
                new Mark("cuts-done"),
                // Slow motion from the moment the wave has one enemy left (often its 2000-HP boss) until it dies;
                // the edit keeps only the seconds around the kill.
                WaitUntil.LastEnemy(10, L, 1.01f, 150f),
                new Mark("last-enemy"),
                new Parallel(new CameraFocus(Lead(L), 0.55f, 0.6f, 60f) { UntilGone = true }, new TimeScaleRamp(0.3f, 0.35f)),
                WaitUntil.AtMost(0, L, 45f),
                new Mark("final-kill"),
                new Wait(0.9f),
                new Parallel(new TimeScaleRamp(1f, 0.5f), new CameraReset { Seconds = 0.5f }),
                WaitUntil.InState(GameState.EndMatch, 20f),
                new HideDebugUi(),
                new Mark("victory"),
                new Wait(5.0f),
            };
            sixteen.LeaveAfter = true;
            Edit(sixteen, 0.0f, 3.0f, "Última wave.");
            s.Add(sixteen);

            ShotDefinition seventeen = Shot(17, "", "golpe-final");
            seventeen.SourceFile = sixteen.FileName;
            Edit(seventeen, 0f, 4.0f, "");
            s.Add(seventeen);

            ShotDefinition eighteen = Shot(18, "", "vitoria");
            eighteen.SourceFile = sixteen.FileName;
            Edit(eighteen, 0f, 3.0f, "");
            s.Add(eighteen);

            // 19 ─ the logo card is built in the edit over a blurred frame of shot 5's board.
            ShotDefinition nineteen = Shot(19, "", "logo");
            nineteen.SourceFile = five.FileName;
            Edit(nineteen, 0.2f, 5.0f, "Bisect TD · Tower Defense PvP de cartas");
            s.Add(nineteen);

            return s;
        }

        // =============================================================================== helpers

        /// <summary>
        /// The usual staging for a gameplay shot: wave 1 has run and our lane is empty, waves are held so no
        /// banner or mana reset lands mid-shot, nobody can lose, the ceiling is up so any card can be dealt.
        /// </summary>
        private static List<ICaptureAction> Stage(bool pauseBot, int cap = 10) => new()
        {
            WaitUntil.Wave(1, L, 30f),
            new ProtectMatch(),
            new HoldWaves(true), // after ProtectMatch: the game keeps one hold per lane
            new FastForward(WaitUntil.WaveDone(1, L, 1f, 120f), 5f),
            pauseBot ? new PauseBot() : new Wait(0f),
            new SetLives(L, 100f),
            new SetLives(R, 100f),
            new RaiseManaCap(cap),
            new Wait(1.5f),
        };

        /// <summary>Off tape: a tower of this type on each spot, each dropped on twice more (tier 3).</summary>
        private static ICaptureAction[] BuildAndLevel(CardType tower, params float[] spots)
        {
            List<ICaptureAction> steps = new();
            foreach (float spot in spots)
            {
                steps.Add(new PlayCard(tower, Slot(spot), 0.3f) { TopUpMana = true, ShowFinger = false });
                steps.Add(new Wait(0.6f));
            }

            for (int i = 0; i < spots.Length * 2; i++)
            {
                steps.Add(new PlayCard(tower, UpgradableTowerOf(tower), 0.3f) { TopUpMana = true, ShowFinger = false });
                steps.Add(new Wait(0.6f));
            }

            return steps.ToArray();
        }

        private static ShotDefinition TowerSpot(string part, string slug, CardType tower, float slot,
            EnemyType enemy, int count, double from, double to, bool tier3 = false, bool lanceFirst = false,
            float beatSeconds = 4f)
        {
            CardType[] fillers = { CardType.SpellIce, CardType.SpellLance, CardType.SpellRift };
            List<CardType> deck = new() { tower };
            deck.AddRange(fillers);

            ShotDefinition shot = Shot(13, part, slug, deck.ToArray());
            shot.Setup = Stage(pauseBot: true);
            shot.Setup.Add(new PlayCard(tower, Slot(slot), 0.3f) { TopUpMana = true, ShowFinger = false });
            shot.Setup.Add(new Wait(0.8f));
            if (tier3)
            {
                for (int i = 0; i < 2; i++)
                {
                    shot.Setup.Add(new PlayCard(tower, UpgradableTowerOf(tower), 0.3f) { TopUpMana = true, ShowFinger = false });
                    shot.Setup.Add(new Wait(0.8f));
                }
            }

            shot.Setup.Add(new SpawnCrowd(L, enemy, count, from, to));
            shot.Setup.Add(new Wait(1.1f));
            if (lanceFirst)
            {
                // Off tape: a Lance takes the tank to within one Needle shot, so the take-down lands on camera.
                shot.Setup.Add(new PlayCard(CardType.SpellLance, Lead(L), 0.3f) { TopUpMana = true, ShowFinger = false });
                shot.Setup.Add(new Wait(1.2f));
            }

            shot.LeadInSeconds = 0.3f;
            shot.Beats = new List<ICaptureAction> { new Wait(beatSeconds) };
            Edit(shot, 0.3f, 1.0f, "");
            return shot;
        }

        private static ShotDefinition Shot(int number, string part, string slug, CardType[] deck = null)
        {
            ShotDefinition shot = ScriptableObject.CreateInstance<ShotDefinition>();
            shot.Number = number;
            shot.Part = part;
            shot.Slug = slug;
            shot.name = shot.FileName;
            shot.Deck = deck != null ? new List<CardType>(deck) : new List<CardType>();
            return shot;
        }

        private static void Edit(ShotDefinition shot, float cutIn, float seconds, string caption)
        {
            shot.CutIn = cutIn;
            shot.CutSeconds = seconds;
            shot.Caption = caption;
        }

        private static void Save(ShotDefinition shot)
        {
            string path = $"{Folder}/{shot.FileName}.asset";
            ShotDefinition existing = AssetDatabase.LoadAssetAtPath<ShotDefinition>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(shot, path);
                return;
            }

            EditorUtility.CopySerialized(shot, existing);
            existing.name = shot.FileName;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(shot);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}

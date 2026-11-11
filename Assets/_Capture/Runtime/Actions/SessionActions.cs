#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeceK.Capture
{
    /// <summary>
    /// Starts a match against the real bot on an offline local host (the tutorial's path: no Relay, no lobby,
    /// nobody can join), with a deck lent for the capture. The deck goes into the connection payload only;
    /// the save is never touched, and the player's own deck is put back by <see cref="LeaveMatch"/>.
    /// </summary>
    [Serializable]
    public class StartLocalMatch : ICaptureAction
    {
        public List<CardType> Deck = new();
        [Tooltip("Level every lent card plays at. 0 = the level the save owns (1 when not owned).")]
        public int CardLevel;
        [Tooltip("Wait until the players' introduction has cleared and the board is visible.")]
        public bool WaitForBoard = true;
        public float TimeoutSeconds = 90f;

        public StartLocalMatch() { }
        public StartLocalMatch(params CardType[] deck) => Deck = new List<CardType>(deck);

        public IEnumerator Run(CaptureContext ctx)
        {
            if (SceneManager.GetActiveScene().name != Loader.Scene.MainMenu.ToString())
                yield return WaitScene(ctx, Loader.Scene.MainMenu, TimeoutSeconds);

            DeckLoan.Lend(ctx, Deck, CardLevel);

            BaseHostManager host = ctx.Host;
            if (host == null) throw new InvalidOperationException("No BaseHostManager: start play mode from StartScene");

            Task<bool> start = host.StartLocalHostAsync(Loader.Scene.GameScene);
            while (!start.IsCompleted) yield return null;
            if (!start.Result) throw new InvalidOperationException("StartLocalHostAsync failed");

            BotFill.Shorten(ctx);

            float t = 0f;
            while (ctx.State != GameState.InMatch)
            {
                t += CaptureTime.Delta;
                if (t > TimeoutSeconds) throw new TimeoutException("Match never reached InMatch");
                yield return null;
            }

            if (WaitForBoard)
            {
                t = 0f;
                while (t < 10f && !(ServiceLocator.TryGet(out IMatchIntroduction intro) && intro.IsFinished))
                {
                    t += CaptureTime.Delta;
                    yield return null;
                }
            }

            ctx.Log($"  in match as {ctx.LocalTeam} with [{string.Join(", ", Deck)}]");
        }

        public static IEnumerator WaitScene(CaptureContext ctx, Loader.Scene scene, float timeout)
        {
            float t = 0f;
            while (SceneManager.GetActiveScene().name != scene.ToString())
            {
                t += CaptureTime.Delta;
                if (t > timeout) throw new TimeoutException($"Scene {scene} never became active");
                yield return null;
            }

            // A beat for the scene's Start methods and the iris wipe to open.
            yield return new Wait(1.2f).Run(ctx);
        }

        public string Describe() => $"StartLocalMatch [{string.Join(", ", Deck)}]";
    }

    /// <summary>Leaves the match the way the end screen's OK does, and gives the player's deck back.</summary>
    [Serializable]
    public class LeaveMatch : ICaptureAction
    {
        public IEnumerator Run(CaptureContext ctx)
        {
            BotFill.Restore();
            StageKeeper.Clear();
            if (Time.timeScale != 1f) Time.timeScale = 1f;

            BaseClientManager client = ctx.Client;
            if (client != null && Loader.IsGameplayScene(SceneManager.GetActiveScene().name))
            {
                Task leave = client.LeaveMatchAsync();
                while (!leave.IsCompleted) yield return null;
            }

            DeckLoan.Return(ctx);
            yield return StartLocalMatch.WaitScene(ctx, Loader.Scene.MainMenu, 60f);
        }

        public string Describe() => "LeaveMatch";
    }

    /// <summary>Hides what must never be in a frame: the Quantum Console and the end screen's placeholder names.</summary>
    [Serializable]
    public class HideDebugUi : ICaptureAction
    {
        public IEnumerator Run(CaptureContext ctx)
        {
            QFSW.QC.QuantumConsole console = UnityEngine.Object.FindFirstObjectByType<QFSW.QC.QuantumConsole>();
            if (console != null) console.Deactivate();

            // ClientEndGameCanvas never fills the names in (a TODO in the game): its labels read
            // "YOUR USERNAME" / "ENEMY USERNAME" placeholders.
            foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string s = text.text ?? "";
                if (s.IndexOf("USERNAME", StringComparison.OrdinalIgnoreCase) >= 0) text.gameObject.SetActive(false);
            }

            yield break;
        }

        public string Describe() => "HideDebugUi";
    }

    /// <summary>The lent deck's bookkeeping: what was in the payload before, so it can be put back.</summary>
    public static class DeckLoan
    {
        private static List<CardType> s_deck;
        private static List<int> s_levels;

        public static void Lend(CaptureContext ctx, List<CardType> deck, int level)
        {
            UserData data = ctx.Client?.UserData;
            if (data == null) throw new InvalidOperationException("No UserData");

            if (s_deck == null)
            {
                s_deck = new List<CardType>(data.DeckCards);
                s_levels = new List<int>(data.DeckCardLevels);
            }

            List<int> levels = new(deck.Count);
            foreach (CardType card in deck)
            {
                int owned = ctx.Save != null && ctx.Save.IsCardOwned(card) ? ctx.Save.GetCardLevel(card) : 1;
                levels.Add(level > 0 ? level : Mathf.Max(1, owned));
            }

            data.SetDeckCards(new List<CardType>(deck));
            data.SetDeckCardLevels(levels);
        }

        public static void Return(CaptureContext ctx)
        {
            UserData data = ctx.Client?.UserData;
            if (data == null || s_deck == null) return;

            data.SetDeckCards(s_deck);
            data.SetDeckCardLevels(s_levels);
            s_deck = null;
            s_levels = null;
        }
    }

    /// <summary>
    /// Shortens the bot's wait for a second human during a capture (the asset says 30 s). Written on the
    /// scene's BotSettings instance in memory and put back on leave; never saved.
    /// </summary>
    public static class BotFill
    {
        private static BotSettingsSO s_settings;
        private static float s_original;

        public static void Shorten(CaptureContext ctx)
        {
            BotController bot = UnityEngine.Object.FindFirstObjectByType<BotController>();
            if (bot == null) return;

            BotSettingsSO settings = Reflect.Get<BotSettingsSO>(bot, "botSettings");
            if (settings == null) return;

            if (s_settings == null)
            {
                s_settings = settings;
                s_original = settings.FillTimeoutSeconds;
            }

            settings.FillTimeoutSeconds = ctx.Settings != null ? ctx.Settings.BotFillSeconds : 1f;
        }

        public static void Restore()
        {
            if (s_settings == null) return;
            s_settings.FillTimeoutSeconds = s_original;
            s_settings = null;
        }
    }
}
#endif

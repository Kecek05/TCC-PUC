using System;
using System.Collections.Generic;
using System.Text;
using QFSW.QC;

/// <summary>
/// Quantum Console commands for the collection itself: which cards the local save owns, and how far each has
/// levelled. The reward commands in <see cref="RewardDebugCommands"/> unlock a card as a <i>side effect</i> of
/// a payout, which is the right thing to test when you care about the payout. These unlock directly, which is
/// what you want after authoring a new card and needing it to simply show up as owned.
/// <c>reward-status</c> remains the reader for both.
/// </summary>
/// <remarks>
/// Everything here goes through <see cref="BasePlayerSaveManager"/> and never around it, so the save's own
/// rules still hold: a <see cref="CardType"/> missing from the card list SO is refused, and levelling walks
/// the real <see cref="BasePlayerSaveManager.TryUpgradeCard"/> path instead of writing a level in. That is
/// the point — the console proves what the game actually does. Client-side only: no server, any scene.
/// </remarks>
public static class ProgressionDebugCommands
{
    private const string NoSaveManager =
        "No BasePlayerSaveManager registered - enter play from StartScene so ClientManager creates it.";

    /// <summary>The deepest rarity is 12 levels. The guard only exists so a malformed step table in
    /// CardProgressionSettings can never spin the editor forever.</summary>
    private const int LevelLoopGuard = 100;

    [Command("unlock-card",
        "Unlocks one card in the local save, optionally levelling it. The card must be registered in the " +
        "card list SO. Works in any scene, including the Main Menu.")]
    private static string UnlockCard(
        [CommandParameterDescription("Card to unlock, e.g. TowerPrism.")]
        CardType card,
        [CommandParameterDescription("Level to take it to. 1 just unlocks it; higher walks the real upgrade " +
                                     "path, topping up the copies and gold each step is short of.")]
        int level = 1)
    {
        if (!ServiceLocator.TryGet(out BasePlayerSaveManager save)) return NoSaveManager;
        if (card == CardType.None) return "Pass a real card, e.g. 'unlock-card TowerPrism'.";

        bool wasOwned = save.IsCardOwned(card);

        if (!wasOwned && !TryUnlock(save, card))
            return $"{card} is not in the card list SO, so the save refused it. Register the card asset first.";

        string note = LevelUpTo(save, card, level);
        CardProgressSaveData progress = save.GetCardProgress(card);

        return $"{card}: {(wasOwned ? "already owned" : "unlocked")} - level {progress.Level}, " +
               $"{progress.Copies} copies.{note}";
    }

    [Command("unlock-all-cards",
        "Unlocks every card the card list SO knows about, optionally levelling each one. The fastest way to " +
        "make freshly authored cards equippable on the deck page.")]
    private static string UnlockAllCards(
        [CommandParameterDescription("Level to take every card to. 1 just unlocks them.")]
        int level = 1)
    {
        if (!ServiceLocator.TryGet(out BasePlayerSaveManager save)) return NoSaveManager;

        int unlocked = 0;
        int already = 0;
        List<CardType> unregistered = new List<CardType>();

        foreach (CardType card in Enum.GetValues(typeof(CardType)))
        {
            if (card == CardType.None) continue;

            if (save.IsCardOwned(card))
            {
                already++;
                LevelUpTo(save, card, level);
                continue;
            }

            if (!TryUnlock(save, card))
            {
                // Refused because the CardType has no asset in the card list SO. Worth naming: it is usually
                // an enum member added ahead of its card, which is exactly the state to notice.
                unregistered.Add(card);
                continue;
            }

            unlocked++;
            LevelUpTo(save, card, level);
        }

        StringBuilder report = new StringBuilder();
        report.Append($"Unlocked {unlocked}, already owned {already}");
        if (level > 1) report.Append($", all taken to level {level} where their rarity allows");
        report.Append('.');

        if (unregistered.Count > 0)
            report.Append($" Skipped (no card asset registered): {string.Join(", ", unregistered)}.");

        return report.ToString();
    }

    /// <summary>
    /// Unlocks through the save's own debug door. There is no zero-copy unlock — <c>AddCardCopies</c> is
    /// copy-based — so an unlock always leaves one copy banked. That is harmless, and it keeps this command
    /// on the supported API instead of writing <c>PlayerSaveData</c> directly.
    /// </summary>
    private static bool TryUnlock(BasePlayerSaveManager save, CardType card)
    {
        save.AddCardCopies(card, 1);
        return save.IsCardOwned(card);
    }

    /// <summary>
    /// Raises a card to <paramref name="targetLevel"/> by paying for each step and then taking it, rather
    /// than assigning the level. Every step still goes through <c>CanUpgradeCard</c>/<c>TryUpgradeCard</c>,
    /// so the progression table is exercised and a card can never end up at a level it could not reach.
    /// Returns a note for the console, empty when nothing needed saying.
    /// </summary>
    private static string LevelUpTo(BasePlayerSaveManager save, CardType card, int targetLevel)
    {
        if (targetLevel <= 1) return string.Empty;

        int guard = LevelLoopGuard;

        while (save.GetCardLevel(card) < targetLevel && guard-- > 0)
        {
            CardUpgradeValidation next = save.CanUpgradeCard(card);

            if (next.Reason == CardUpgradeInvalidReason.MaxLevel)
                return $" Capped at level {save.GetCardLevel(card)}, its rarity's maximum.";

            // Top up exactly what this step is short of, then let the real upgrade spend it.
            int missingCopies = next.CopiesRequired - save.GetCardProgress(card).Copies;
            if (missingCopies > 0) save.AddCardCopies(card, missingCopies);

            int missingGold = next.GoldCost - save.Gold;
            if (missingGold > 0) save.AddGold(missingGold);

            if (!save.TryUpgradeCard(card))
                return $" Stopped at level {save.GetCardLevel(card)}: the upgrade was refused " +
                       $"({save.CanUpgradeCard(card).Reason}).";
        }

        return string.Empty;
    }
}

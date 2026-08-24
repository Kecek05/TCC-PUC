using System;
using System.Text;
using QFSW.QC;

/// <summary>
/// Quantum Console commands for rewards, so a payout can be exercised without playing a match to its end.
/// They drive the <i>real</i> pipeline instead of reimplementing it, so what the console proves is what the
/// game actually does.
/// </summary>
/// <remarks>
/// Two tiers, matching the two ways a reward can arrive. <c>grant-reward</c> and <c>grant-reward-to</c> take
/// the match path — <see cref="ServerEndGameManager"/> rolls and sends a targeted Rpc — so they are
/// server-side and only work inside a running match. <c>claim-reward</c> goes straight through
/// <see cref="BaseRewardService"/>, the same door a daily claim or a shop purchase will use, so it works in
/// any scene, the Main Menu included.
/// </remarks>
public static class RewardDebugCommands
{
    [Command("grant-reward",
        "Rolls and pays out a reward to every seated player, as if the match had just ended with " +
        "'winnerTeam' winning. Host only; the match is not ended.")]
    private static string GrantReward(
        [CommandParameterDescription("Team treated as the winner. None = the local player's team, i.e. you win.")]
        TeamType winnerTeam = TeamType.None)
    {
        if (!TryGetEndGameManager(out ServerEndGameManager endGame, out string error)) return error;

        if (winnerTeam == TeamType.None)
        {
            winnerTeam = ServiceLocator.Get<BaseTeamManager>().GetLocalTeam();

            if (winnerTeam == TeamType.None)
                return "Could not resolve the local team. Pass one explicitly, e.g. 'grant-reward Red'.";
        }

        endGame.DebugGrantRewards(winnerTeam);

        return $"Rolled a payout with {winnerTeam} as the winner. Each roll is logged; " +
               $"run 'reward-status' to see what landed in the save.";
    }

    [Command("grant-reward-to",
        "Sends one exact, unrolled reward to a team - the way to test a specific card unlock without " +
        "fighting the roller. Host only.")]
    private static string GrantRewardTo(
        [CommandParameterDescription("Team to pay. The bot has no client and cannot be paid.")]
        TeamType team,
        [CommandParameterDescription("Gold to add. Negative spends instead, clamped at 0.")]
        int gold = 100,
        [CommandParameterDescription("Card to award. None awards gold only, like a loser's payout.")]
        CardType card = CardType.None,
        [CommandParameterDescription("Copies of that card, unlocking it at level 1 if it was locked.")]
        int copies = 1)
    {
        if (!TryGetEndGameManager(out ServerEndGameManager endGame, out string error)) return error;
        if (team == TeamType.None) return "Pass a real team: 'grant-reward-to Red' or 'grant-reward-to Blue'.";

        Reward reward = card == CardType.None
            ? Reward.GoldOnly(RewardSource.Match, gold)
            : Reward.WithCard(RewardSource.Match, card, copies, gold);

        if (!endGame.DebugSendRewardTo(team, reward))
            return $"No connected client on {team} to pay - the slot is empty, or it is the bot.";

        return $"Sent to {team} - {reward}.";
    }

    [Command("claim-reward",
        "Grants a reward straight into the local save through BaseRewardService - the same door a daily " +
        "claim or a shop purchase will use. Works in any scene, including the Main Menu. No server needed.")]
    private static string ClaimReward(
        [CommandParameterDescription("Gold to add. Negative spends instead, clamped at 0.")]
        int gold = 100,
        [CommandParameterDescription("Card to award. None grants gold only.")]
        CardType card = CardType.None,
        [CommandParameterDescription("Copies of that card, unlocking it at level 1 if it was locked.")]
        int copies = 1,
        [CommandParameterDescription("Stamped on the reward, for logging and for whichever UI presents it.")]
        RewardSource source = RewardSource.Debug)
    {
        if (!ServiceLocator.TryGet(out BaseRewardService rewards))
            return "No BaseRewardService registered - enter play from StartScene so ClientManager creates it.";

        Reward reward = card == CardType.None
            ? Reward.GoldOnly(source, gold)
            : Reward.WithCard(source, card, copies, gold);

        if (reward.IsEmpty) return "That reward grants nothing - pass some gold or a card.";

        rewards.Grant(reward);

        return $"Granted {reward}.";
    }

    [Command("reward-status",
        "Prints the local save's gold and every owned card's level and banked copies.")]
    private static string RewardStatus()
    {
        if (!ServiceLocator.TryGet(out BasePlayerSaveManager save))
            return "No BasePlayerSaveManager registered - enter play from StartScene so ClientManager creates it.";

        StringBuilder report = new StringBuilder();
        report.AppendLine($"Gold: {save.Gold}");

        int owned = 0;
        foreach (CardType card in Enum.GetValues(typeof(CardType)))
        {
            if (card == CardType.None) continue;

            CardProgressSaveData progress = save.GetCardProgress(card);
            if (progress == null) continue;

            owned++;

            // GetCopiesRequired returns 0 at max level, where an "owned/needed" ratio is meaningless.
            int required = save.GetCopiesRequired(card);
            string copies = required > 0 ? $"{progress.Copies}/{required} copies" : $"{progress.Copies} copies (max level)";

            report.AppendLine($"  {card}: level {progress.Level}, {copies}");
        }

        if (owned == 0) report.AppendLine("  (no cards owned)");

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// The reward pipeline only exists on the real <see cref="ServerEndGameManager"/>, and only the server
    /// may roll or send. Every failure is reported as text so it shows up in the console, not the log.
    /// </summary>
    private static bool TryGetEndGameManager(out ServerEndGameManager endGame, out string error)
    {
        endGame = null;
        error = null;

        if (!ServiceLocator.TryGet(out BaseServerEndGameManager registered))
        {
            error = "No BaseServerEndGameManager registered - are you in GameScene?";
            return false;
        }

        endGame = registered as ServerEndGameManager;
        if (endGame == null)
        {
            error = $"The registered end-game manager is a {registered.GetType().Name}, which has no reward pipeline.";
            return false;
        }

        if (!endGame.IsSpawned)
        {
            error = "The end-game manager is not spawned yet - wait for the match to start.";
            return false;
        }

        if (!endGame.IsServer)
        {
            error = "Rewards are rolled and sent by the server - run this on the host.";
            return false;
        }

        return true;
    }
}

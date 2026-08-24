using System;

/// <summary>
/// The one place a reward becomes real: it banks the payout into the player save and then announces it,
/// whoever handed it over — the end of a match, a daily claim, a shop purchase.
/// </summary>
/// <remarks>
/// Plain C# and registered in <see cref="ServiceLocator"/> by <c>ClientManager</c>, so it lives exactly as
/// long as <see cref="BasePlayerSaveManager"/> does and is reachable from every scene. That is the whole
/// point of it: the only way to grant a reward used to be <c>BaseServerEndGameManager</c>'s targeted Rpc,
/// which exists solely inside GameScene, so nothing in the Main Menu could ever pay the player.
/// </remarks>
public abstract class BaseRewardService
{
    /// <summary>
    /// A reward was banked. Raised <b>after</b> the save is written, so a subscriber that reads gold or card
    /// progress in its handler already sees the new totals.
    /// </summary>
    public event Action<Reward> OnRewardGranted;

    /// <summary>Banks a payout and announces it. An empty reward is ignored.</summary>
    public abstract void Grant(Reward reward);

    protected void RaiseRewardGranted(Reward reward) => OnRewardGranted?.Invoke(reward);
}

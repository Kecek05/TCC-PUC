/// <summary>
/// Banks rewards into <see cref="BasePlayerSaveManager"/>.
/// </summary>
/// <remarks>
/// Thin on purpose. The save owns every rule — unlocking a locked card at level 1, clamping gold at 0,
/// persisting — and this owns only what the save cannot: the ordering (write, <i>then</i> announce, so a
/// listener never reads a stale balance) and the guard against a payout that would grant nothing.
/// </remarks>
public class RewardService : BaseRewardService
{
    private readonly BasePlayerSaveManager _save;

    public RewardService(BasePlayerSaveManager save)
    {
        _save = save;

        if (_save == null)
            GameLog.Error($"[{nameof(RewardService)}] Created without a player save; rewards cannot be banked.");
    }

    public override void Grant(Reward reward)
    {
        if (_save == null) return;

        if (reward.IsEmpty)
        {
            GameLog.Warn($"[{nameof(RewardService)}] Ignored an empty reward from {reward.Source}.");
            return;
        }

        _save.GrantReward(reward);
        RaiseRewardGranted(reward);
    }
}

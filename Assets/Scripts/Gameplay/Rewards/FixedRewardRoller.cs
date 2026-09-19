/// <summary>
/// Pays one reward decided in advance, whatever the outcome. For a match whose payout is authored rather
/// than rolled — the tutorial's, shaped by the menu steps that follow it — but that should still travel the
/// normal match path, so it is banked and shown on the end screen exactly like any other match reward.
/// </summary>
public class FixedRewardRoller : IRewardRoller
{
    private readonly Reward _reward;

    public FixedRewardRoller(Reward reward) => _reward = reward;

    public Reward Roll(bool won) => _reward;
}

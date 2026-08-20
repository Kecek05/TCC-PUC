/// <summary>
/// Decides what a player takes home from a match. Kept behind an interface and free of Unity randomness so
/// the payout curve can be swapped (chests, streak bonuses, a server-side backend) and tested without
/// entering play mode.
/// </summary>
public interface IRewardRoller
{
    MatchReward Roll(bool won);
}

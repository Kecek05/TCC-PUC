/// <summary>
/// Decides what a player takes home from a <b>match</b>, hence the win/lose input. Other reward sources
/// (a daily claim, a shop purchase) build their <see cref="Reward"/> however they like and hand it to
/// <see cref="BaseRewardService.Grant"/> directly; they do not come through here.
/// </summary>
/// <remarks>
/// Behind an interface and free of Unity randomness so the payout curve can be swapped (chests, streak
/// bonuses, a server-side backend) and tested without entering play mode.
/// </remarks>
public interface IRewardRoller
{
    Reward Roll(bool won);
}

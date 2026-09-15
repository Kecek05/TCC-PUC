using System;
using System.Threading.Tasks;

/// <summary>
/// Owns the first-time experience as a whole: whether it still has to run, which half the player is in,
/// and the card the match paid out so the Main Menu half can talk about it.
/// </summary>
/// <remarks>
/// Plain C# and registered by <c>ClientManager</c> beside <see cref="BasePlayerSaveManager"/> and
/// <see cref="BaseRewardService"/>, for the same reason those are: the tutorial spans three scenes
/// (AuthBootstrap decides, TutorialScene plays, MainMenu finishes) and nothing that lives inside one of
/// them could carry state across the other two. The persistent half is a single bool in the save; the
/// phase and the reward card are deliberately in-memory only, so quitting mid-tutorial restarts it rather
/// than resuming into a menu step with no match behind it.
/// </remarks>
public abstract class BaseTutorialService
{
    /// <summary>The player moved between halves, or finished. The menu director listens to decide
    /// whether to arm itself.</summary>
    public event Action<TutorialPhase> OnPhaseChanged;

    /// <summary>True when the boot should go to TutorialScene rather than the Main Menu.</summary>
    public abstract bool ShouldRunOnBoot { get; }

    public abstract TutorialPhase Phase { get; }

    /// <summary>The card the tutorial match paid out — what the Main Menu half asks the player to equip
    /// and upgrade. <see cref="CardType.None"/> outside the menu phase.</summary>
    public abstract CardType RewardCard { get; }

    /// <summary>
    /// Hosts the scripted match locally and loads TutorialScene. The player's real deck is swapped for
    /// the authored tutorial one for the duration, so every scripted step has the card it asks for.
    /// </summary>
    public abstract Task<bool> StartMatchPhaseAsync();

    /// <summary>
    /// The match is over and its reward is banked. Restores the player's own deck and arms the Main Menu
    /// half. Called from the tutorial match as it hands control back.
    /// </summary>
    public abstract void BeginMenuPhase(CardType rewardCard);

    /// <summary>Marks the whole thing finished and persists it. From here the game boots to the menu.</summary>
    public abstract void Complete();

    /// <summary>
    /// Leaves the tutorial without finishing it — the Skip button, or a match that ended some way the
    /// script did not plan for. The save is written as completed all the same: a player who opted out
    /// must not be dropped back into the tutorial on their next launch.
    /// </summary>
    public abstract void Abandon();

    protected void RaisePhaseChanged(TutorialPhase phase) => OnPhaseChanged?.Invoke(phase);
}

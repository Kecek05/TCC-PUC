using System;
using UnityEngine;

/// <summary>
/// The tutorial's whole presentation surface: the line of copy, the dimmed screen with a hole cut around
/// whatever the player is being asked to touch, and the hand that shows the gesture.
/// </summary>
/// <remarks>
/// Abstract so the two directors (match and menu) depend on the API rather than the canvas, and so a scene
/// without one simply runs its steps invisibly instead of throwing — which is what makes the sequence
/// testable from the console.
/// </remarks>
public abstract class BaseTutorialOverlay : MonoBehaviour
{
    /// <summary>The player dismissed a read-this step.</summary>
    public event Action OnContinueTapped;

    /// <summary>The player asked to leave the tutorial.</summary>
    public event Action OnSkipTapped;

    /// <param name="showContinue">True for a step the player dismisses, false for one the game ends.</param>
    public abstract void Show(string text, bool showContinue);

    /// <summary>Shows or hides Continue under the line already up — how a step reveals it late.</summary>
    public abstract void SetContinueVisible(bool visible);

    public abstract void SetHighlight(TutorialHighlight highlight);

    /// <summary>
    /// A suggestion rather than an instruction: the line and a pointer at <paramref name="highlight"/>,
    /// with <b>no dim and no Continue</b>, over a board the player is still free to play. For the free play
    /// after the scripted steps. Call every frame the tip stays up so the pointer follows its target; it
    /// leaves the input mode alone.
    /// </summary>
    public abstract void ShowTip(string text, TutorialHighlight highlight);

    public abstract void HideTip();

    /// <summary>
    /// How much of the game the player may touch. Independent of <see cref="Show"/> and <see cref="Hide"/>:
    /// the board stays held while the overlay is out of the way for a settling or watching beat.
    /// <see cref="TutorialInputMode.TargetOnly"/> opens exactly the hole of the current highlight.
    /// </summary>
    public abstract void SetInputMode(TutorialInputMode mode);

    /// <summary>
    /// Shows what the tutorial has just paid out: the card with its icon and name, then the gold. Stays up
    /// until <see cref="HideReward"/> or <see cref="Hide"/>, independent of the copy beside it.
    /// </summary>
    /// <param name="card">The reward card's data; null for a gold-only payout.</param>
    public abstract void ShowReward(Reward reward, CardDataSO card);

    public abstract void HideReward();

    public abstract void Hide();

    public abstract void SetSkipVisible(bool visible);

    protected void RaiseContinueTapped() => OnContinueTapped?.Invoke();

    protected void RaiseSkipTapped() => OnSkipTapped?.Invoke();
}

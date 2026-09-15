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

    public abstract void SetHighlight(TutorialHighlight highlight);

    /// <summary>
    /// Shows the card the tutorial has just unlocked, art and name, beside the copy. Passing a null sprite
    /// and an empty name hides it again.
    /// </summary>
    public abstract void ShowUnlockedCard(Sprite art, string cardName);

    public abstract void Hide();

    public abstract void SetSkipVisible(bool visible);

    protected void RaiseContinueTapped() => OnContinueTapped?.Invoke();

    protected void RaiseSkipTapped() => OnSkipTapped?.Invoke();
}

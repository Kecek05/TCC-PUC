using System;
using UnityEngine;

/// <summary>
/// What the overlay should point the player at while a step is running. Re-evaluated every frame, so a
/// target is free to move — a card sliding into a hand slot, a placeable the camera is panning past.
/// </summary>
public readonly struct TutorialHighlight
{
    public readonly bool HasTarget;

    /// <summary>A UI target, in its own canvas. Null when the target is a world position instead.</summary>
    public readonly RectTransform Rect;

    public readonly Vector3 WorldPosition;

    /// <summary>World units. Only meaningful for a world target.</summary>
    public readonly float WorldRadius;

    public readonly TutorialHintKind Hint;

    /// <summary>Where a drag hint should end. Only meaningful when <see cref="Hint"/> is a drag.</summary>
    public readonly Vector3 DragToWorld;

    public readonly bool HasDragTarget;

    private TutorialHighlight(bool hasTarget, RectTransform rect, Vector3 worldPosition, float worldRadius,
        TutorialHintKind hint, Vector3 dragToWorld, bool hasDragTarget)
    {
        HasTarget = hasTarget;
        Rect = rect;
        WorldPosition = worldPosition;
        WorldRadius = worldRadius;
        Hint = hint;
        DragToWorld = dragToWorld;
        HasDragTarget = hasDragTarget;
    }

    /// <summary>Text only: dim nothing, point at nothing.</summary>
    public static TutorialHighlight None => default;

    public static TutorialHighlight Ui(RectTransform rect, TutorialHintKind hint = TutorialHintKind.Tap) =>
        rect == null ? None : new TutorialHighlight(true, rect, default, 0f, hint, default, false);

    public static TutorialHighlight World(Vector3 world, float radius, TutorialHintKind hint = TutorialHintKind.Tap) =>
        new(true, null, world, Mathf.Max(0.01f, radius), hint, default, false);

    /// <summary>
    /// The shape every "play this card" step needs: the card is UI, the place it has to land is world.
    /// The overlay opens its hole on the card and animates the hand from there to the world point.
    /// </summary>
    public static TutorialHighlight DragUiToWorld(RectTransform card, Vector3 targetWorld) =>
        card == null
            ? None
            : new TutorialHighlight(true, card, default, 0f, TutorialHintKind.Drag, targetWorld, true);
}

/// <summary>
/// One beat of the tutorial: a line of copy, something to point at, and the condition that ends it.
/// </summary>
/// <remarks>
/// Deliberately a single class configured with delegates rather than a class per step. The steps differ
/// almost entirely in <i>what completes them</i> — a deploy event here, a camera side there — and the
/// delegates close over the director that already subscribes to those, so the whole script reads as one
/// list in one file instead of twenty near-empty types. The FSM shape (<c>IGameFlowState</c>) earns its
/// classes because states hold state; these do not.
/// </remarks>
public class TutorialStep
{
    public TutorialStepId Id { get; }

    /// <summary>Ends the step. Null means it never self-completes, so it must be a tap-to-continue step.</summary>
    public Func<bool> IsComplete { get; private set; }

    public Func<TutorialHighlight> Highlight { get; private set; }

    public Action OnEnter { get; private set; }

    public Action OnExit { get; private set; }

    /// <summary>Run every frame the step is waiting. Where a frozen step keeps the player topped up:
    /// entry alone is not enough, because a deploy result can land after it and spend them back down.</summary>
    public Action OnTick { get; private set; }

    /// <summary>Shows the Continue button and waits for it instead of watching the world.</summary>
    public bool WaitsForTap { get; private set; }

    /// <summary>Seconds the step is held even after <see cref="IsComplete"/> is true, so the player gets
    /// to read the line rather than having it flash past.</summary>
    public float MinDuration { get; private set; }

    /// <summary>Seconds after which the step gives up and moves on. 0 disables it. The safety net for a
    /// condition that can be made unreachable by the player, so the tutorial can never dead-end.</summary>
    public float Timeout { get; private set; }

    /// <summary>
    /// Whether the world holds still while this step waits. On by default: the player is reading, and a
    /// tutorial that lets a wave chew through their base while they do is teaching the wrong lesson.
    /// </summary>
    public bool FreezesGame { get; private set; } = true;

    /// <summary>Optional arguments for the step's copy, resolved when the step is entered. How the outro
    /// names the card it just unlocked without the copy table knowing which card that is.</summary>
    public Func<object[]> TextArgs { get; private set; }

    public TutorialStep(TutorialStepId id)
    {
        Id = id;
        MinDuration = 0.35f;
    }

    /// <summary>A step the player dismisses by tapping Continue. The default when nothing completes it.</summary>
    public TutorialStep Tap()
    {
        WaitsForTap = true;
        return this;
    }

    public TutorialStep CompletesWhen(Func<bool> predicate)
    {
        IsComplete = predicate;
        return this;
    }

    public TutorialStep Pointing(Func<TutorialHighlight> highlight)
    {
        Highlight = highlight;
        return this;
    }

    public TutorialStep Entering(Action onEnter)
    {
        OnEnter = onEnter;
        return this;
    }

    public TutorialStep WhileWaiting(Action onTick)
    {
        OnTick = onTick;
        return this;
    }

    public TutorialStep Leaving(Action onExit)
    {
        OnExit = onExit;
        return this;
    }

    public TutorialStep Holding(float seconds)
    {
        MinDuration = Mathf.Max(0f, seconds);
        return this;
    }

    public TutorialStep GivingUpAfter(float seconds)
    {
        Timeout = Mathf.Max(0f, seconds);
        return this;
    }

    /// <summary>Lets the match keep running through this step, for a beat where the point is to watch
    /// something move.</summary>
    public TutorialStep Running()
    {
        FreezesGame = false;
        return this;
    }

    public TutorialStep Formatting(Func<object[]> args)
    {
        TextArgs = args;
        return this;
    }
}

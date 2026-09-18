using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs a list of <see cref="TutorialStep"/> in order: shows each step's line, keeps its highlight tracking
/// the thing it points at, and moves on when the step's condition says so.
/// </summary>
/// <remarks>
/// The twin of <c>GameFlowFsm</c>, minus the transitions: a tutorial is a line, not a graph, so a step
/// cannot choose its successor. That is the whole reason this is a list and not a second FSM.
/// </remarks>
public class TutorialSequence
{
    /// <summary>Every step is done. The director decides what that means — end the match, write the save.</summary>
    public event Action OnFinished;

    public event Action<TutorialStepId> OnStepChanged;

    private readonly List<TutorialStep> _steps;
    private readonly BaseTutorialOverlay _overlay;
    private readonly TutorialCopySO _copy;

    /// <summary>
    /// Whether this run may stop the clock at all. A match freezes while its steps wait; the Main Menu has
    /// nothing at stake, and a freeze there only stalls the menu's own scaled-time tweens — the page strip
    /// stopped mid-slide, so the step that asked for a new page ended with the old one still on screen.
    /// </summary>
    private readonly bool _freezesWorld;

    private int _index = -1;
    private float _enteredAt;
    private bool _tapped;

    /// <summary>When the current step completed and started letting its result play out (see
    /// <see cref="TutorialStep.IsSettled"/>). Negative while the step is still waiting on the player.</summary>
    private float _settlingSince = -1f;

    private bool IsSettling => _settlingSince >= 0f;

    /// <summary>The timescale before the tutorial touched it, restored whenever the run ends. Captured
    /// rather than assumed to be 1 so a paused game is handed back paused.</summary>
    private float _timeScaleBeforeRun = 1f;
    private bool _frozen;

    public bool IsRunning { get; private set; }

    public TutorialStepId CurrentStepId =>
        _index >= 0 && _index < _steps.Count ? _steps[_index].Id : TutorialStepId.None;

    public TutorialSequence(List<TutorialStep> steps, BaseTutorialOverlay overlay, TutorialCopySO copy,
        bool freezesWorld = true)
    {
        _steps = steps ?? new List<TutorialStep>();
        _overlay = overlay;
        _copy = copy;
        _freezesWorld = freezesWorld;
    }

    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        _index = -1;
        _timeScaleBeforeRun = Time.timeScale;

        if (_overlay != null) _overlay.OnContinueTapped += HandleContinueTapped;

        EnterNext();
    }

    /// <summary>Drive from the director's Update. Cheap: one predicate and one highlight resolve.</summary>
    public void Tick()
    {
        if (!IsRunning || _index < 0 || _index >= _steps.Count) return;

        TutorialStep step = _steps[_index];

        if (IsSettling)
        {
            TickSettling(step);
            return;
        }

        step.OnTick?.Invoke();

        // Re-resolved every frame so the ring follows a card that is still sliding into its slot, or a
        // world target the camera is panning across.
        if (_overlay != null && step.Highlight != null)
            _overlay.SetHighlight(step.Highlight());

        // Unscaled throughout: a frozen step still has to time out, or freezing would turn the safety net off
        // exactly where it is needed most.
        float elapsed = Time.unscaledTime - _enteredAt;
        if (elapsed < step.MinDuration) return;

        if (step.Timeout > 0f && elapsed >= step.Timeout)
        {
            GameLog.Warn($"[Tutorial] Step {step.Id} timed out after {step.Timeout:0.#}s; moving on so the " +
                         "tutorial cannot dead-end.");
            EnterNext();
            return;
        }

        bool done = step.WaitsForTap ? _tapped : step.IsComplete == null || step.IsComplete();
        if (!done) return;

        if (step.IsSettled != null || step.WatchSeconds > 0f) BeginSettling(step);
        else EnterNext();
    }

    /// <summary>Abandons the run wherever it is. The overlay is cleared; no step's exit is skipped.</summary>
    public void Stop()
    {
        if (!IsRunning) return;

        ExitCurrent();
        SetFrozen(false);
        IsRunning = false;
        _index = _steps.Count;

        if (_overlay != null)
        {
            _overlay.OnContinueTapped -= HandleContinueTapped;
            _overlay.Hide();
        }
    }

    private void HandleContinueTapped() => _tapped = true;

    /// <summary>
    /// The player did what the step asked. Lets the world run so the result plays out - a tower rising, an
    /// upgrade landing - before the next step freezes it again: a match animates on scaled time, so going
    /// straight on would leave it hanging half-way under the next instruction. The overlay steps aside
    /// meanwhile, because its dim and its pointing hand would sit between the player and what they just did.
    /// </summary>
    private void BeginSettling(TutorialStep step)
    {
        _settlingSince = Time.unscaledTime;
        SetFrozen(false);

        if (_overlay != null) _overlay.Hide();

        GameLog.Info($"[Tutorial] Step {step.Id} done; letting it settle.");
    }

    private void TickSettling(TutorialStep step)
    {
        float elapsed = Time.unscaledTime - _settlingSince;

        // A watch is a beat the player is meant to SEE, so it always runs its full length; a settle ends the
        // moment its predicate holds. A step carrying both is watched first, then settled.
        if (elapsed < step.WatchSeconds) return;

        if (step.IsSettled == null || step.IsSettled())
        {
            EnterNext();
            return;
        }

        // Unscaled like every other tutorial timeout: a result that never settles must not dead-end the run.
        if (elapsed - step.WatchSeconds < step.SettleTimeout) return;

        GameLog.Warn($"[Tutorial] Step {step.Id} had not settled after {step.SettleTimeout:0.#}s; moving on.");
        EnterNext();
    }

    private void EnterNext()
    {
        ExitCurrent();

        _index++;
        _tapped = false;
        _settlingSince = -1f;
        _enteredAt = Time.unscaledTime;

        // Passed over unseen, never entered and exited, so a skipped step's hooks do not run at all.
        while (_index < _steps.Count && !ShouldRun(_steps[_index]))
        {
            GameLog.Info($"[Tutorial] Step {_steps[_index].Id} skipped: its precondition does not hold.");
            _index++;
        }

        if (_index >= _steps.Count)
        {
            Finish();
            return;
        }

        TutorialStep step = _steps[_index];

        // Before OnEnter, so a step that wants the world running (the outro) can act on a live one, and a
        // step that freezes has already stopped it before its enter hook tops the player up.
        SetFrozen(_freezesWorld && step.FreezesGame);

        step.OnEnter?.Invoke();

        if (_overlay != null)
        {
            _overlay.Show(ResolveText(step), step.WaitsForTap);
            _overlay.SetHighlight(step.Highlight != null ? step.Highlight() : TutorialHighlight.None);
        }

        OnStepChanged?.Invoke(step.Id);
        GameLog.Info($"[Tutorial] Step -> {step.Id}");
    }

    private static bool ShouldRun(TutorialStep step) => step.Precondition == null || step.Precondition();

    private string ResolveText(TutorialStep step)
    {
        string text = _copy != null ? _copy.Get(step.Id) : string.Empty;
        if (step.TextArgs == null || string.IsNullOrEmpty(text)) return text;

        object[] args = step.TextArgs();
        if (args == null || args.Length == 0) return text;

        // A copy line that forgot its placeholder must not take the whole tutorial down with it.
        try { return string.Format(text, args); }
        catch (FormatException)
        {
            GameLog.Warn($"[Tutorial] Copy for {step.Id} does not match the arguments it was given.");
            return text;
        }
    }

    /// <summary>
    /// Holds the world still while a step waits. <see cref="Time.timeScale"/> rather than a game-state flag
    /// because it stops everything at once - waves, towers, projectiles, mana regen - without every system
    /// needing to learn about the tutorial. What it also stops is mana regen, which is why an action step
    /// tops the player up on enter: a frozen step the player cannot afford would never end.
    /// </summary>
    private void SetFrozen(bool frozen)
    {
        if (_frozen == frozen) return;

        _frozen = frozen;
        Time.timeScale = frozen ? 0f : _timeScaleBeforeRun;
    }

    private void ExitCurrent()
    {
        if (_index < 0 || _index >= _steps.Count) return;
        _steps[_index].OnExit?.Invoke();
    }

    private void Finish()
    {
        SetFrozen(false);
        IsRunning = false;

        if (_overlay != null)
        {
            _overlay.OnContinueTapped -= HandleContinueTapped;
            _overlay.Hide();
        }

        GameLog.Info("[Tutorial] Sequence finished.");
        OnFinished?.Invoke();
    }
}

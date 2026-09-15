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

    private int _index = -1;
    private float _enteredAt;
    private bool _tapped;

    public bool IsRunning { get; private set; }

    public TutorialStepId CurrentStepId =>
        _index >= 0 && _index < _steps.Count ? _steps[_index].Id : TutorialStepId.None;

    public TutorialSequence(List<TutorialStep> steps, BaseTutorialOverlay overlay, TutorialCopySO copy)
    {
        _steps = steps ?? new List<TutorialStep>();
        _overlay = overlay;
        _copy = copy;
    }

    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        _index = -1;

        if (_overlay != null) _overlay.OnContinueTapped += HandleContinueTapped;

        EnterNext();
    }

    /// <summary>Drive from the director's Update. Cheap: one predicate and one highlight resolve.</summary>
    public void Tick()
    {
        if (!IsRunning || _index < 0 || _index >= _steps.Count) return;

        TutorialStep step = _steps[_index];

        // Re-resolved every frame so the ring follows a card that is still sliding into its slot, or a
        // world target the camera is panning across.
        if (_overlay != null && step.Highlight != null)
            _overlay.SetHighlight(step.Highlight());

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
        if (done) EnterNext();
    }

    /// <summary>Abandons the run wherever it is. The overlay is cleared; no step's exit is skipped.</summary>
    public void Stop()
    {
        if (!IsRunning) return;

        ExitCurrent();
        IsRunning = false;
        _index = _steps.Count;

        if (_overlay != null)
        {
            _overlay.OnContinueTapped -= HandleContinueTapped;
            _overlay.Hide();
        }
    }

    private void HandleContinueTapped() => _tapped = true;

    private void EnterNext()
    {
        ExitCurrent();

        _index++;
        _tapped = false;
        _enteredAt = Time.unscaledTime;

        if (_index >= _steps.Count)
        {
            Finish();
            return;
        }

        TutorialStep step = _steps[_index];
        step.OnEnter?.Invoke();

        if (_overlay != null)
        {
            _overlay.Show(_copy != null ? _copy.Get(step.Id) : string.Empty, step.WaitsForTap);
            _overlay.SetHighlight(step.Highlight != null ? step.Highlight() : TutorialHighlight.None);
        }

        OnStepChanged?.Invoke(step.Id);
        GameLog.Info($"[Tutorial] Step -> {step.Id}");
    }

    private void ExitCurrent()
    {
        if (_index < 0 || _index >= _steps.Count) return;
        _steps[_index].OnExit?.Invoke();
    }

    private void Finish()
    {
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

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UI.Game.Shapes.ImmediateComponents;
using UnityEngine;

[Serializable]
public class RectangleImmediateUIAnimationSettings
{
    [Title("References")]
    [SerializeField, Required] private RectangleImmediateUI rectangleImmediateUI;

    [Title("Settings")]
    [Tooltip("Seconds for the dash pattern to travel one full loop around the rectangle, independent of dashCount.")]
    [SerializeField] private float duration = 10f;

    // Phase in perimeter-fraction units (0..1 = one full loop). Stored here so the Shapes
    // offset can be derived as _phase*dashCount each frame — that keeps dashes visually
    // anchored to the same physical perimeter position when dashCount changes mid-animation.
    private float _phase;

    public void Tick(float deltaTime)
    {
        if (rectangleImmediateUI == null) return;
        float dashCount = rectangleImmediateUI.GetDashCount();
        if (dashCount <= 0f || duration <= 0f) return;

        _phase = (_phase + deltaTime / duration) % 1f;
        rectangleImmediateUI.SetDashOffset(_phase * dashCount);
    }

    [Button("Reset Offset")]
    public void ResetOffsetDebug()
    {
        _phase = 0f;
        if (rectangleImmediateUI == null) return;
        rectangleImmediateUI.SetDashOffset(0f);
    }

    public void SetRectangleImmediateUI(RectangleImmediateUI newRectangleImmediateUI)
    {
        rectangleImmediateUI = newRectangleImmediateUI;
    }

    public RectangleImmediateUI GetRectangleImmediateUI()
    {
        return rectangleImmediateUI;
    }
}

public class RectangleImmediateUIAnimator : MonoBehaviour
{
    [Title("Settings")]
    [SerializeField] private List<RectangleImmediateUIAnimationSettings> rectangleImmediateUIAnimationSettings;

    private void OnValidate()
    {
        if (rectangleImmediateUIAnimationSettings == null || rectangleImmediateUIAnimationSettings.Count == 0)
        {
            RectangleImmediateUIAnimationSettings newSettings = new RectangleImmediateUIAnimationSettings();
            newSettings.SetRectangleImmediateUI(GetComponent<RectangleImmediateUI>());
            rectangleImmediateUIAnimationSettings.Add(newSettings);
        }
    }

    private void Start()
    {
        foreach (RectangleImmediateUIAnimationSettings animationSettings in rectangleImmediateUIAnimationSettings)
        {
            if (animationSettings.GetRectangleImmediateUI() == null)
            {
                GameLog.Warn($"{nameof(RectangleImmediateUIAnimator)} on {gameObject.name} has no reference to a {nameof(RectangleImmediateUI)} and will not animate.");
            }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (RectangleImmediateUIAnimationSettings animationSettings in rectangleImmediateUIAnimationSettings)
        {
            animationSettings.Tick(dt);
        }
    }

    [Button("Reset All Offsets")]
    private void ResetAllDebug()
    {
        foreach (RectangleImmediateUIAnimationSettings animationSettings in rectangleImmediateUIAnimationSettings)
        {
            animationSettings.ResetOffsetDebug();
        }
    }
}
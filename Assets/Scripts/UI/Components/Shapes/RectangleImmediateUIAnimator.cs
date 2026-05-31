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
    [Tooltip("Seconds for the offset to advance by one dash cycle. Total loop time = duration * dashCount.")]
    [SerializeField] private float duration = 10f;

    public void Tick(float deltaTime)
    {
        if (rectangleImmediateUI == null) return;
        float dashCount = rectangleImmediateUI.GetDashCount();
        if (dashCount <= 0f || duration <= 0f) return;

        float delta = deltaTime / (duration / dashCount);
        float next = (rectangleImmediateUI.GetDashOffset() + delta) % 1f;
        rectangleImmediateUI.SetDashOffset(next);
    }

    [Button("Reset Offset")]
    public void ResetOffsetDebug()
    {
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
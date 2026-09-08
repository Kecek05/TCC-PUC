using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseInfoPanelService : MonoBehaviour
{
    public event Action OnInfoPanelShow;
    public event Action OnInfoPanelHide;
    public abstract void ShowInfoPanel(InfoPanelData infoPanelData);
    public abstract void HideInfoPanel();
    
    protected void TriggerOnInfoPanelShow() => OnInfoPanelShow?.Invoke();
    protected void TriggerOnInfoPanelHide() => OnInfoPanelHide?.Invoke();
}

public struct InfoPanelData
{
    public string Title;
    public string Description;
    public Sprite Icon;

    /// <summary>
    /// Rows for the stat table, one widget each. Already resolved to the viewer's level by the caller, so
    /// the panel stays a pure view — null or empty simply draws no stats.
    /// </summary>
    public IReadOnlyList<CardStatProgress> Stats;
}

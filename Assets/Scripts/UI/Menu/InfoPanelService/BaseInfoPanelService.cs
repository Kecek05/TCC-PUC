using System;
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
}

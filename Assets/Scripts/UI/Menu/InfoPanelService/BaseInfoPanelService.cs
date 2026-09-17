using System;
using UnityEngine;

public abstract class BaseInfoPanelService : MonoBehaviour
{
    public event Action OnInfoPanelShow;
    public event Action OnInfoPanelHide;
    public abstract void ShowInfoPanel(InfoPanelData infoPanelData);
    public abstract void HideInfoPanel();

    public abstract bool IsVisible { get; }

    /// <summary>The panel's Upgrade button, so the tutorial can frame it — pointed at, never pressed for the
    /// player. Null when the panel has none.</summary>
    public abstract RectTransform UpgradeButtonRect { get; }

    protected void TriggerOnInfoPanelShow() => OnInfoPanelShow?.Invoke();
    protected void TriggerOnInfoPanelHide() => OnInfoPanelHide?.Invoke();
}

/// <summary>
/// What the info panel is asked to show. Only the card's identity travels: name, art, cost, rarity, type
/// and description come off the <see cref="CardDataSO"/>, while level, copies and upgrade cost come from
/// the player's save. The panel resolves both itself rather than being handed a snapshot, because it can
/// upgrade the card it is showing — a snapshot would be stale the moment the player taps Upgrade.
/// </summary>
public struct InfoPanelData
{
    public CardDataSO Card;

    public static InfoPanelData ForCard(CardDataSO card) => new() { Card = card };
}

using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>
/// One row of the card info panel's stat table: a label, the value at the level the player owns, and the
/// gain one more level buys. Instantiated once per <see cref="CardStatProgress"/> into the StatsParent
/// grid of <c>InfoPanelCanvas</c>.
/// </summary>
/// <remarks>
/// Deliberately dumb, the same way <c>RewardEntryUI</c> is: it prints the strings it is handed and knows
/// nothing about levels, growth tables or which card it belongs to. That is what lets the same prefab
/// serve a tower, a spell and a troop without ever branching on the card family.
/// </remarks>
public class StatEntryUI : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI statTitle;
    [SerializeField] private TextMeshProUGUI statValue;

    [Tooltip("The '+N' the next level adds. Hidden for a stat that does not grow, and at max level.")]
    [SerializeField] private TextMeshProUGUI upgradeValue;

    public void SetStat(CardStatProgress stat)
    {
        if (statTitle != null) statTitle.text = stat.Label;
        if (statValue != null) statValue.text = stat.CurrentDisplay;

        if (upgradeValue == null) return;

        // Range and Hit Speed sit at 0% growth in the default table, so an always-on "+0" would show up on
        // nearly every card and read as a bug. Hide the label instead of printing a change that never comes.
        upgradeValue.gameObject.SetActive(stat.HasUpgrade);
        upgradeValue.text = stat.UpgradeDisplay;
    }

    /// <summary>
    /// The upgrade's payoff for one row: the value rolls from what the old level had to what the new one has,
    /// lit in <c>gain</c>, then the next level's "+N" pops in. Ends exactly on <see cref="SetStat"/>(after), so
    /// completing it early always leaves the row right.
    /// </summary>
    public Tween RollFrom(CardStatProgress before, CardStatProgress after)
    {
        JuiceTokensSO t = Juice.Tokens;
        if (statValue == null)
        {
            SetStat(after);
            return null;
        }

        string format = string.IsNullOrEmpty(after.Current.Format) ? "0.##" : after.Current.Format;
        Color rest = statValue.color;

        statValue.text = before.CurrentDisplay;
        if (upgradeValue != null) upgradeValue.gameObject.SetActive(false);

        return DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendCallback(() => statValue.color = t.Colors.Gain)
            .Append(DOVirtual.Float(before.Current.Value, after.Current.Value, t.Durations.Count,
                v => statValue.text = v.ToString(format)).SetEase(t.Eases.Out))
            .AppendCallback(() =>
            {
                SetStat(after);
                statValue.color = rest;
                JuiceFx.Punch(statValue.transform, t.Scales.PunchSoft);
                if (upgradeValue != null && after.HasUpgrade) JuiceFx.PopIn(upgradeValue.transform, 0.4f);
            })
            .OnKill(() =>
            {
                if (statValue != null) statValue.color = rest;
            });
    }
}

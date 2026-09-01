using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for the Fonte: it produces mana and nothing else. The slot it occupies is a slot not defending,
/// which is the card's entire cost — it is only worth it if the deck behind it is expensive enough to need
/// the ceiling raised.
/// </summary>
/// <remarks>
/// Tick interval is the shared <c>ShootCooldown</c> column, so a payout IS this tower's shot and the
/// existing haste and buff plumbing speeds it up for free.
/// </remarks>
[CreateAssetMenu(fileName = "ManaTowerData", menuName = "Scriptable Objects/Data/TowerData/ManaTowerData")]
public class ManaTowerDataSO : TowerDataSO
{
    [Title("Mana Generation")]
    [Min(0f)] public float ManaPerTickLevel1 = 1f;
    [Min(0f)] public float ManaPerTickLevel2 = 1.5f;
    [Min(0f)] public float ManaPerTickLevel3 = 2f;

    public float GetManaPerTickByLevel(int level)
    {
        switch (level)
        {
            case 1: return ManaPerTickLevel1;
            case 2: return ManaPerTickLevel2;
            case 3: return ManaPerTickLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 mana per tick.");
                return ManaPerTickLevel1;
        }
    }
}

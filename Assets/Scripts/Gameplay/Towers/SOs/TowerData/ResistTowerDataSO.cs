using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for a tower whose output is an armor-shredding aura rather than damage, the exact twin of
/// <see cref="SlowTowerDataSO"/>: same per-level table shape, but the fraction it carries is off-color
/// resistance stripped instead of move speed removed. The damage columns stay on the asset because the
/// base reads them, but a resist tower is expected to leave them at 0 — its whole contribution is making
/// the towers around it hit for full damage.
/// </summary>
[CreateAssetMenu(fileName = "ResistTowerData", menuName = "Scriptable Objects/Data/TowerData/ResistTowerData")]
public class ResistTowerDataSO : TowerDataSO
{
    [Title("Resist Clear Aura")]
    [InfoBox("Fraction of an enemy's off-color resistance removed while it stands in the aura. 1 = the " +
             "armor colour stops mattering entirely, which is what this tower used to do at every level.")]
    [PropertyRange(0f, 1f)] public float ResistClearPercentLevel1 = 0.60f;
    [PropertyRange(0f, 1f)] public float ResistClearPercentLevel2 = 0.75f;
    [PropertyRange(0f, 1f)] public float ResistClearPercentLevel3 = 0.90f;

    public float GetResistClearPercentByLevel(int level)
    {
        switch (level)
        {
            case 1: return ResistClearPercentLevel1;
            case 2: return ResistClearPercentLevel2;
            case 3: return ResistClearPercentLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 resist clear percent.");
                return ResistClearPercentLevel1;
        }
    }
}

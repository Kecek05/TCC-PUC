using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for a magazine tower: it fires a burst of heavy area shots and then reloads for a long time.
/// ShootCooldown is the gap BETWEEN shots in a burst, not the tower's real cadence — the real cadence is
/// the whole cycle, burst plus reload, which is why the card reads as "high damage, very slow".
/// </summary>
[CreateAssetMenu(fileName = "MortarTowerData", menuName = "Scriptable Objects/Data/TowerData/MortarTowerData")]
public class MortarTowerDataSO : ExplosionTowerDataSO
{
    [Title("Magazine")]
    [MinValue(1)] public int ShotsAtLevel1 = 3;

    [MinValue(0)]
    [Tooltip("Extra shots granted by each placement upgrade, on top of ShotsAtLevel1.")]
    public int ExtraShotsPerLevel = 1;

    [Unit(Units.Second)] public float ReloadDurationLevel1 = 6f;
    [Unit(Units.Second)] public float ReloadDurationLevel2 = 5.5f;
    [Unit(Units.Second)] public float ReloadDurationLevel3 = 5f;

    public int GetMagazineSizeByLevel(int level) =>
        ShotsAtLevel1 + ExtraShotsPerLevel * Mathf.Max(0, level - 1);

    public float GetReloadDurationByLevel(int level)
    {
        switch (level)
        {
            case 1: return ReloadDurationLevel1;
            case 2: return ReloadDurationLevel2;
            case 3: return ReloadDurationLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 reload duration.");
                return ReloadDurationLevel1;
        }
    }
}

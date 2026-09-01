using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for the Espelho: it never fires at anything on its own field. On each cooldown it sends one weak
/// enemy down the OPPONENT's lane, converting a defensive slot into slow, unattended offence.
/// </summary>
/// <remarks>
/// The enemy is referenced directly rather than by <see cref="EnemyType"/> so the tower carries its own
/// payload and cannot be pointed at a type the enemy list does not hold. Send interval is the shared
/// <c>ShootCooldown</c> column — a send IS this tower's shot.
/// </remarks>
[CreateAssetMenu(fileName = "MirrorTowerData", menuName = "Scriptable Objects/Data/TowerData/MirrorTowerData")]
public class MirrorTowerDataSO : TowerDataSO
{
    [Title("Mirror")]
    [Required]
    [Tooltip("The enemy sent to the opponent on every tick. Meant to be a cheap chip unit — the card's " +
             "pressure comes from never stopping, not from any single body being dangerous.")]
    public EnemyDataSO SentEnemy;

    [Min(1)]
    [Tooltip("How many are sent per tick.")]
    public int SendCountLevel1 = 1;
    [Min(1)] public int SendCountLevel2 = 1;
    [Min(1)] public int SendCountLevel3 = 2;

    public int GetSendCountByLevel(int level)
    {
        switch (level)
        {
            case 1: return SendCountLevel1;
            case 2: return SendCountLevel2;
            case 3: return SendCountLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 send count.");
                return SendCountLevel1;
        }
    }
}

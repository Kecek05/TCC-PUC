using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellCardData", menuName = "Scriptable Objects/Cards/SpellCardDataSO")]
public class SpellCardDataSO : CardDataSO
{
    [Title("Spell Data")]
    public SpellType SpellType;
    [Space(10f)]
    
    [Title("Placement Settings")]
    public bool CanUseInEnemyMap = false;
    public bool CanUseInLocalMap = true;
    [Space(10f)]
    
    [Title("Others")]
    [Tooltip("Sprite that will be used in the GhostSpellCard")]
    public Sprite SpellGhostSprite;
    public SpellDataSO SpellData;

    /// <summary>
    /// The team whose field this spell lands on, given the caster's team: the opponent for
    /// enemy-only spells (e.g. freeze), otherwise the caster's own team. Used to map both the
    /// cast point and the spell visual to the correct server-space side for translating players.
    /// </summary>
    public TeamType GetTargetFieldTeam(TeamType casterTeam)
    {
        bool enemyField = CanUseInEnemyMap && !CanUseInLocalMap;
        if (!enemyField) return casterTeam;
        return casterTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
    }
}

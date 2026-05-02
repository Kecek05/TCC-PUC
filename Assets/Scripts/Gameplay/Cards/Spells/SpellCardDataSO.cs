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

}

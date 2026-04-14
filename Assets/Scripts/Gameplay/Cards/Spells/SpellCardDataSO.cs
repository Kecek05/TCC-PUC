using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellCardData", menuName = "Scriptable Objects/Cards/SpellCardDataSO")]
public class SpellCardDataSO : CardDataSO
{
    [Title("Spell Data")]
    public SpellType SpellType;

}

using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "SpellDataListSO", menuName = "Scriptable Objects/SpellCardListSO")]
public class SpellDataListSO : ScriptableObject
{
    public List<SpellDataSO> SpellDataList;
    
    public SpellDataSO GetSpellDataByType(SpellType spellType)
    {
        return SpellDataList.Find(spellData => spellData.SpellType == spellType);
    }
}
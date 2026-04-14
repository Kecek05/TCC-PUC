using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellOffensiveData", menuName = "Scriptable Objects/Data/Spells/SpellOffensiveDataSO")]
public class SpellOffensiveDataSO : SpellDataSO
{
    [Title("Offensive Data")] 
    public float Damage = 5f;
}

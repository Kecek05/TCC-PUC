using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellEffectData", menuName = "Scriptable Objects/Data/Spells/SpellEffectDataSO")]
public class SpellEffectDataSO : SpellDataSO
{
    [Title("Effect Data")] 
    public float Duration = 3f;
}

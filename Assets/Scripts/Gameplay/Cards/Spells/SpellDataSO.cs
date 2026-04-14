using Sirenix.OdinInspector;
using UnityEngine;

// [CreateAssetMenu(fileName = "SpellData", menuName = "Scriptable Objects/SpellDataSO")]
public class SpellDataSO : ScriptableObject
{
    [Title("General")]
    public SpellType SpellType;

    public float Range = 2f;

}

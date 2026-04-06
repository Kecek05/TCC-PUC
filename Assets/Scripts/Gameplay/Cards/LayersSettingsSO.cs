using Sirenix.OdinInspector;
using UnityEngine;

// [CreateAssetMenu(fileName = "LayersSettingsSO", menuName = "Scriptable Objects/LayersSettingsSO")]
public class LayersSettingsSO : ScriptableObject
{
    [Title("Layers Settings")]
    public LayerMask PlaceableLayer;
    public LayerMask EnemyMapLayer;
}

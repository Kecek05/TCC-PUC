using Sirenix.OdinInspector;
using UnityEngine;

// [CreateAssetMenu(fileName = "LayersSettingsSO", menuName = "Scriptable Objects/LayersSettingsSO")]
public class LayersSettingsSO : ScriptableObject
{
    [Title("Layers")]
    public LayerMask PlaceableLayer;
    public LayerMask EnemyMapLayer;

    [Title("Settings")] 
    public float PlaceableRadius = 0.2f;
}

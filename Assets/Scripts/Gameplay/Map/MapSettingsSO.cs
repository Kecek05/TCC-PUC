using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "MapSettingsSO", menuName = "Scriptable Objects/MapSettingsSO")]
public class MapSettingsSO : ScriptableObject
{
    [Title("Map Positions")]
    public float BluePlayerMapY = 11f;
    public float RedPlayerMapY = 0f;

    [Title("Camera")]
    [Tooltip("Minimum world-space width that all devices must see.")]
    public float TargetWorldWidth = 10f;
    [Tooltip("Minimum world-space height that all devices must see.")]
    public float TargetWorldHeight = 10f;
}

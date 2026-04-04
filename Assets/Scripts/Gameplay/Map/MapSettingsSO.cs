using UnityEngine;

[CreateAssetMenu(fileName = "MapSettingsSO", menuName = "Scriptable Objects/MapSettingsSO")]
public class MapSettingsSO : ScriptableObject
{
    [Header("Map Positions")]
    public float BluePlayerMapY = 11f;
    public float RedPlayerMapY = 0f;
}

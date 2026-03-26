using UnityEngine;

[CreateAssetMenu(fileName = "ManaSettingsSO", menuName = "Scriptable Objects/ManaSettingsSO")]
public class ManaSettingsSO : ScriptableObject
{
    public float MaxMana = 10;
    public float StartingMana = 3;
    public float RegenPerSecond = 0.357f;
}

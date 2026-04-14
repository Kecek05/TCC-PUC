using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ManaSettings", menuName = "Scriptable Objects/Settings/ManaSettings")]
public class ManaSettingsSO : ScriptableObject
{
    [Title("Mana Pool")]
    [Tooltip("Maximum mana a player can hold")]
    [SerializeField] private float maxMana = 10f;

    [Tooltip("Mana each player starts with at match begin")]
    [SerializeField] private float startingMana = 5f;

    [Title("Regeneration")]
    [Tooltip("Mana regenerated per second (~0.357 = 1 mana per 2.8s, matching Clash Royale base rate)")]
    [SerializeField] private float regenPerSecond = 0.357f;

    public float MaxMana => maxMana;
    public float StartingMana => startingMana;
    public float RegenPerSecond => regenPerSecond;
}
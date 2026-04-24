using System;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ManaSettings", menuName = "Scriptable Objects/Settings/ManaSettings")]
public class ManaSettingsSO : ScriptableObject
{
    [Title("Mana Pool")]
    [Tooltip("Mana each player starts with at match begin")]
    [SerializeField] private float startingMana = 5f;

    [Tooltip("Maximum mana cap before any wave advances it")]
    [SerializeField] private float startingMaxMana = 5f;

    [Tooltip("Per-wave overrides for max mana. Waves not listed keep the previous max.")]
    [SerializeField] [ListDrawerSettings(ListElementLabelName = "WaveNumber")]
    private WaveManaMax[] waveManaMaxes;

    [Title("Regeneration")]
    [Tooltip("Mana regenerated per second (~0.357 = 1 mana per 2.8s, matching Clash Royale base rate)")]
    [SerializeField] private float regenPerSecond = 0.357f;

    public float StartingMana => startingMana;
    public float StartingMaxMana => startingMaxMana;
    public float RegenPerSecond => regenPerSecond;

    public bool TryGetMaxManaForWave(int waveNumber, out float maxMana)
    {
        if (waveManaMaxes != null)
        {
            for (int i = 0; i < waveManaMaxes.Length; i++)
            {
                if (waveManaMaxes[i].WaveNumber == waveNumber)
                {
                    maxMana = waveManaMaxes[i].MaxMana;
                    return true;
                }
            }
        }

        maxMana = 0f;
        return false;
    }
}

[Serializable]
public class WaveManaMax
{
    public int WaveNumber;
    public float MaxMana;
}

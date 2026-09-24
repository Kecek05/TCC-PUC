using UnityEngine;

/// <summary>
/// The instance side of a <see cref="PooledVfxSO"/>: takes a spawn tint before the effect plays. Effects are
/// authored white where they should take the colour of whatever made them (an enemy's armour, a tower's
/// attack), the same convention the sprites use — so one prefab serves every colour.
/// </summary>
public class PooledVfx : MonoBehaviour
{
    [Tooltip("Systems whose start colour takes the spawn tint. Leave empty for effects authored in their final colours.")]
    [SerializeField] private ParticleSystem[] tintedSystems = new ParticleSystem[0];

    /// <summary>Must run before the instance is activated: playOnAwake emits on enable, with whatever start
    /// colour is set at that moment.</summary>
    public void Tint(Color color)
    {
        for (int i = 0; i < tintedSystems.Length; i++)
        {
            if (tintedSystems[i] == null) continue;

            ParticleSystem.MainModule main = tintedSystems[i].main;
            main.startColor = color;
        }
    }
}

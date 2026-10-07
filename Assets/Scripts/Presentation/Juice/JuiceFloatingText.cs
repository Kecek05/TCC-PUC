using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
/// The pooled number that rises off a hit or a payout, drawn with the game's own font (a world-space
/// <see cref="TextMeshPro"/>) instead of Feel's legacy <see cref="TextMesh"/>. Spawned by the scene's
/// <see cref="MMFloatingTextSpawner"/>, which <see cref="FloatingNumbers"/> feeds; this class only knows how to
/// write text, colour and opacity onto the label.
/// </summary>
/// <remarks>
/// Feel ships a TextMeshPro variant, but it compiles only behind a define this project's assembly never sets,
/// so it would draw nothing. The spawner owns lifetime, rise, scale pop and fade; the budget (how many can be
/// alive at once) is its pool size.
/// </remarks>
public class JuiceFloatingText : MMFloatingText
{
    [SerializeField] private TextMeshPro label;

    protected override void Initialization()
    {
        base.Initialization();
        if (label != null) _initialTextColor = label.color;
    }

    public override void SetText(string newValue)
    {
        if (label != null) label.text = newValue;
    }

    public override void SetColor(Color newColor)
    {
        if (label != null) label.color = newColor;
    }

    public override void SetOpacity(float newOpacity)
    {
        if (label == null) return;
        _newColor = label.color;
        _newColor.a = newOpacity;
        label.color = _newColor;
    }

    /// <summary>Wires a label built in code (the editor tool that makes the prefab).</summary>
    public void Bind(TextMeshPro text) => label = text;
}

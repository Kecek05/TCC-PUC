using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// The grey tile a card's art sits on, one shape per card family: the face a card wears in the match hand and on
/// the next-card slot (HandCard), matching the faces the deck page draws, so a card reads as the same card
/// everywhere it appears.
/// </summary>
[CreateAssetMenu(fileName = "CardFaces", menuName = "Scriptable Objects/Cards/CardFaces")]
public class CardFacesSO : ScriptableObject
{
    [Title("Faces")]
    [SerializeField, PreviewField] private Sprite tower;
    [SerializeField, PreviewField] private Sprite spell;
    [SerializeField, PreviewField] private Sprite troop;

    [Tooltip("The faces' tint: the design system's tile colour.")]
    [SerializeField] private Color tint = new(0.584f, 0.584f, 0.584f, 1f);

    public Color Tint => tint;

    /// <summary>The face for a card family, or null for a family without one.</summary>
    public Sprite For(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Tower => tower,
        ExistingTypesOfCard.Spell => spell,
        ExistingTypesOfCard.Enemy => troop,
        _ => null
    };
}

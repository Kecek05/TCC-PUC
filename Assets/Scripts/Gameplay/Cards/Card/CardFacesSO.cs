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

    [Title("Art")]
    [InfoBox("How big a card's art is drawn on its face, as a multiple of the art box a tower fills (Card Art v2). " +
             "A tower's three-level sprite fills the box; a spell emblem and a troop sit smaller, because the " +
             "hexagon and the diamond leave less room than the square.")]
    [SerializeField, Range(0.3f, 1.2f)] private float towerArt = 1f;
    [SerializeField, Range(0.3f, 1.2f)] private float spellArt = 0.84f;
    [SerializeField, Range(0.3f, 1.2f)] private float troopArt = 0.76f;

    public Color Tint => tint;

    /// <summary>The scale a family's art is drawn at inside the shared art box.</summary>
    public float ArtScale(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Spell => spellArt,
        ExistingTypesOfCard.Enemy => troopArt,
        _ => towerArt
    };

    /// <summary>The face for a card family, or null for a family without one.</summary>
    public Sprite For(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Tower => tower,
        ExistingTypesOfCard.Spell => spell,
        ExistingTypesOfCard.Enemy => troop,
        _ => null
    };
}

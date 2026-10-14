using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The card that comes next (HandCard): a small card face with its art and its cost in a mana bubble, the way the
/// hand shows a card. A card drawn into the hand leaves from here, so the slot is also where the hand's flights
/// start.
/// </summary>
/// <remarks>The face and the cost are built in code on first use, so the scenes need no new objects.</remarks>
public class NextCardSlot : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Image cardImage;

    [Title("Face")]
    [InfoBox("Optional. With both set, the next card wears its family's face, like the hand and the deck page. The " +
             "slot is only handed the art, so the card list is how it finds the family.")]
    [SerializeField] private CardFacesSO faces;
    [SerializeField] private CardDataListSO cards;

    [Tooltip("The face's size as a multiple of the art's.")]
    [SerializeField, MinValue(1f)] private float faceScale = 1.28f;

    [Tooltip("A tower's art box as a share of the face's width, the same share the hand's cards use.")]
    [SerializeField, Range(0.3f, 1f)] private float artShare = 0.66f;

    private Image _face;
    private Image _costBubble;
    private TextMeshProUGUI _costLabel;
    private bool _built;
    private float _artRest = 1f;

    /// <summary>Where a card drawn into the hand starts its flight.</summary>
    public Transform FlightOrigin => cardImage != null ? cardImage.transform : transform;

    public void SetNextCardImage(Sprite image)
    {
        Build();

        bool changed = cardImage.sprite != image;
        cardImage.sprite = image;
        ApplyFace(image);

        // The next card arriving is a beat of its own (HandCard, Enter Pop), unscaled like the hand.
        if (!changed || image == null || !isActiveAndEnabled) return;
        JuiceFx.PopIn(cardImage.transform, 0.5f, _artRest);
        if (_face != null && _face.enabled) JuiceFx.PopIn(_face.transform, 0.5f);
        if (_costBubble != null && _costBubble.enabled) JuiceFx.PopIn(_costBubble.transform, 0.5f);
    }

    /// <summary>The family's face behind the art and the cost under it, found through the card that uses this art.</summary>
    private void ApplyFace(Sprite art)
    {
        if (_face == null) return;

        CardDataSO shown = null;
        if (art != null && cards != null)
        {
            foreach (CardDataSO card in cards.CardDataList)
            {
                if (card == null || card.CardImage != art) continue;
                shown = card;
                break;
            }
        }

        Sprite face = shown != null && faces != null ? faces.For(shown.ExistingType) : null;

        // The art in its card's own colour and at its family's size, as the hand draws it.
        cardImage.color = shown != null ? shown.CardColor : Color.white;
        _artRest = shown != null && faces != null ? faces.ArtScale(shown.ExistingType) * artShare * faceScale : 1f;
        cardImage.transform.localScale = Vector3.one * _artRest;
        _face.sprite = face;
        _face.enabled = face != null;
        if (face != null) _face.color = faces.Tint;

        _costBubble.enabled = shown != null;
        _costLabel.enabled = shown != null;
        if (shown != null) _costLabel.text = shown.Cost.ToString();
    }

    private void Build()
    {
        if (_built || cardImage == null) return;
        _built = true;

        RectTransform art = cardImage.rectTransform;
        Vector2 faceSize = art.sizeDelta * faceScale;

        // The face sits right behind the art, in the same place.
        GameObject faceGo = new GameObject("Face", typeof(RectTransform), typeof(Image));
        RectTransform faceRect = (RectTransform)faceGo.transform;
        faceRect.SetParent(art.parent, false);
        faceRect.SetSiblingIndex(art.GetSiblingIndex());
        faceRect.anchorMin = art.anchorMin;
        faceRect.anchorMax = art.anchorMax;
        faceRect.pivot = art.pivot;
        faceRect.anchoredPosition = art.anchoredPosition;
        faceRect.sizeDelta = faceSize;
        _face = faceGo.GetComponent<Image>();
        _face.preserveAspect = true;
        _face.raycastTarget = false;
        _face.enabled = false;
        faceGo.AddComponent<FaceLip>();

        // The cost straddles the face's bottom edge in a mana bubble, as on the hand's cards.
        JuiceTokensSO t = Juice.Tokens;
        float bubble = faceSize.x * 0.46f;
        GameObject bubbleGo = new GameObject("Cost", typeof(RectTransform), typeof(Image));
        RectTransform bubbleRect = (RectTransform)bubbleGo.transform;
        bubbleRect.SetParent(art.parent, false);
        bubbleRect.anchorMin = bubbleRect.anchorMax = art.anchorMin;
        bubbleRect.sizeDelta = Vector2.one * bubble;
        bubbleRect.anchoredPosition = art.anchoredPosition + new Vector2(0f, -faceSize.y * 0.5f + bubble * 0.2f);
        _costBubble = bubbleGo.GetComponent<Image>();
        _costBubble.sprite = JuiceSprites.Disc;
        _costBubble.color = t.Colors.Mana;
        _costBubble.raycastTarget = false;
        _costBubble.enabled = false;
        bubbleGo.AddComponent<FaceLip>().Configure(0.07f, 0.74f);

        GameObject labelGo = new GameObject("CostLabel", typeof(RectTransform));
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(bubbleRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        _costLabel = labelGo.AddComponent<TextMeshProUGUI>();
        TMP_Text style = FindStyle();
        if (style != null)
        {
            _costLabel.font = style.font;
            _costLabel.fontSharedMaterial = style.fontSharedMaterial;
        }

        _costLabel.fontSize = bubble * 0.62f;
        _costLabel.color = t.Colors.Ink;
        _costLabel.alignment = TextAlignmentOptions.Center;
        _costLabel.textWrappingMode = TextWrappingModes.NoWrap;
        _costLabel.raycastTarget = false;
        _costLabel.enabled = false;
    }

    /// <summary>The game's font, borrowed from any label already on this canvas.</summary>
    private TMP_Text FindStyle()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.GetComponentInChildren<TMP_Text>(true) : null;
    }
}

using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardGFXController : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI costLabel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private Image cardIconImage;
    [SerializeField] private Image cardIconBackground;
    [SerializeField] private Image cardCostGfx;
    [SerializeField] private Color costDefaultColor;
    [SerializeField] private Color costNoManaColor;
    [Tooltip("Debug crutch: shown only when CardDataSO.ShowPlaceholderNameOverlay is on. Overlays the CardName " +
             "on top of the icon so cards that share placeholder art are still visually distinguishable.")]
    [SerializeField] private TextMeshProUGUI debugPlaceholderLabel;

    [Title("Face")]
    [InfoBox("Optional. The grey tile behind the art, shaped by the card's family, so a card in the hand looks " +
             "like the same card on the deck page (HandCard). The mana clock and the refusal flash take the " +
             "face's shape. Empty: the art alone, with the clock in the art's own outline.")]
    [SerializeField] private CardFacesSO faces;
    [SerializeField] private Image faceImage;
    [SerializeField] private Image flashImage;

    [Title("Ready Shine")]
    [Tooltip("Optional. A Mask in the face's shape, drawn over the art, and the bar that sweeps across it the " +
             "moment the card becomes playable.")]
    [SerializeField] private Image shineMask;
    [SerializeField] private RectTransform shineBar;

    private BaseClientManaManager _clientManaManager;
    private int _cardCost;

    /// <summary>Whether the card could be paid for last frame, so the moment it becomes playable is a beat.</summary>
    private bool _affordable;
    private bool _primed;
    private Tween _shine;

    public void Initialize(CardDataSO cardDataSo, BaseClientManaManager clientManaManager)
    {
        _clientManaManager  = clientManaManager;
        _cardCost = cardDataSo.Cost;

        costLabel.text = _cardCost.ToString();
        titleLabel.text = cardDataSo.CardName;
        cardIconImage.sprite = cardDataSo.CardImage;
        cardIconImage.color = cardDataSo.CardColor;

        // The clock covers the share of mana still missing over the whole face; the refusal flash and the shine
        // follow the same shape, so every overlay sits exactly on the card it belongs to.
        Sprite face = faces != null ? faces.For(cardDataSo.ExistingType) : null;
        Sprite shape = face != null ? face : cardDataSo.CardImage;
        cardIconBackground.sprite = shape;
        if (flashImage != null && face != null) flashImage.sprite = face;
        if (shineMask != null) shineMask.sprite = shape;

        if (faceImage != null)
        {
            faceImage.sprite = face;
            faceImage.color = face != null ? faces.Tint : Color.clear;
        }

        if (debugPlaceholderLabel != null)
        {
            debugPlaceholderLabel.gameObject.SetActive(cardDataSo.ShowPlaceholderNameOverlay);
            if (cardDataSo.ShowPlaceholderNameOverlay)
                debugPlaceholderLabel.text = cardDataSo.CardName;
        }

        if (shineMask != null) shineMask.gameObject.SetActive(false);
        UpdateCardCostGfx();
    }

    private void Update()
    {
        if (_clientManaManager != null)
            UpdateCardCostGfx();
    }

    private void OnDisable()
    {
        _shine?.Kill();
    }

    private void UpdateCardCostGfx()
    {
        float remaining =  Mathf.Clamp01((_cardCost - _clientManaManager.PredictedMana) / _cardCost);
        cardIconBackground.fillAmount = remaining;

        // HandCard: the clock reaching zero is a beat. Not on the first read, or every card already affordable
        // when dealt would go off at once.
        bool affordable = remaining <= 0f;
        if (_primed && affordable && !_affordable) PlayReady();

        _affordable = affordable;
        _primed = true;

        cardCostGfx.color = costNoManaColor;
        cardCostGfx.color = Color.Lerp(cardCostGfx.color, costDefaultColor, (1f - remaining));
    }

    /// <summary>
    /// The card becomes playable: it pops from just under its size, an attention shine sweeps the face, one tick.
    /// No buzz: the player did nothing, the mana did.
    /// </summary>
    private void PlayReady()
    {
        JuiceFx.PopIn(transform, 0.94f);
        Juice.Sfx(SfxId.Tick, 4, 0.5f);
        Shine();
    }

    private void Shine()
    {
        if (shineMask == null || shineBar == null || Juice.ReduceMotion) return;

        RectTransform area = shineMask.rectTransform;
        float reach = area.rect.width * 0.5f + shineBar.rect.width * 1.5f;

        _shine?.Kill();
        shineMask.gameObject.SetActive(true);
        shineBar.anchoredPosition = new Vector2(-reach, 0f);
        _shine = shineBar.DOAnchorPosX(reach, 0.45f).SetEase(Juice.Tokens.Eases.Out).SetUpdate(true).SetLink(gameObject)
            .OnKill(() =>
            {
                if (shineMask != null) shineMask.gameObject.SetActive(false);
                _shine = null;
            });
    }
}

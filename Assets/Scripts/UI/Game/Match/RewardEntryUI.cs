using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tile in the end-of-match rewards row: a dark tile holding an icon and an amount (gold), or a card that
/// arrives face down and turns over (a won card). Instantiated once per line of a <see cref="Reward"/> into the
/// <c>RewardsArea</c> grid.
/// </summary>
/// <remarks>
/// Deliberately dumb — it takes a sprite, a number and, for a card, the widget to show it with, and knows nothing
/// about where they came from. That is what lets the same prefab serve a match payout, and later a daily claim or a
/// shop purchase, without branching on <see cref="RewardSource"/>.
/// </remarks>
public class RewardEntryUI : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Image rewardImage;
    [SerializeField] private TextMeshProUGUI rewardQtdLabel;

    [Tooltip("The tile itself (its dark sheet and everything on it). It is what drops and lands: the root belongs " +
             "to the grid, which would put a moving root straight back. Empty: the root.")]
    [SerializeField] private RectTransform body;

    [Title("Card Tile")]
    [Tooltip("The size a won card is shown at on its tile.")]
    [SerializeField] private float cardScale = 0.85f;

    private int _amount;
    private Color _tint = Color.white;

    private RectTransform _card;
    private Image _back;
    private GameObject _front;
    private TextMeshProUGUI _name;
    private Color _rarity = Color.white;

    private bool _layoutCaptured;
    private Vector2 _qtyRest;
    private float _qtySize;

    private RectTransform Body => body != null ? body : (RectTransform)transform;

    public void SetGold(int amount, Sprite coinSprite)
    {
        ClearCard();
        Apply(coinSprite, Juice.Tokens.Colors.Gold, amount);
        if (rewardQtdLabel != null) rewardQtdLabel.color = Juice.Tokens.Colors.Gold;
    }

    /// <summary>
    /// A won card: shown with <paramref name="cardWidget"/> (the deck page's own card, so it is drawn exactly as
    /// it will look in the collection), face down until <see cref="Reveal"/> turns it over. Without a widget the
    /// tile falls back to the card's art.
    /// </summary>
    /// <param name="rarity">The colour of the card's name, its burst and its flash. Null: attention.</param>
    public void SetCard(CardDataSO cardData, int copies, SingleCardInDeck cardWidget = null, Color? rarity = null)
    {
        ClearCard();
        Apply(cardData != null ? cardData.CardImage : null, cardData != null ? cardData.CardColor : Color.white, copies);
        if (rewardQtdLabel != null) rewardQtdLabel.color = Juice.Tokens.Colors.Ink;
        _rarity = rarity ?? Juice.Tokens.Colors.Attention;

        if (cardData == null) return;
        BuildCard(cardData, cardWidget);
    }

    /// <summary>A tile shown again for another reward (a reused tile) starts from the prefab's layout.</summary>
    private void ClearCard()
    {
        if (rewardQtdLabel != null && !_layoutCaptured)
        {
            _layoutCaptured = true;
            _qtyRest = rewardQtdLabel.rectTransform.anchoredPosition;
            _qtySize = rewardQtdLabel.fontSize;
        }

        if (_card == null) return;

        if (rewardImage != null && rewardImage.transform.parent == _card) rewardImage.rectTransform.SetParent(Body, true);
        Destroy(_card.gameObject);
        if (_name != null) Destroy(_name.gameObject);
        _card = null;
        _back = null;
        _front = null;
        _name = null;

        if (rewardQtdLabel != null)
        {
            rewardQtdLabel.rectTransform.anchoredPosition = _qtyRest;
            rewardQtdLabel.fontSize = _qtySize;
        }
    }

    private void Apply(Sprite sprite, Color tint, int amount)
    {
        _amount = amount;
        _tint = tint;

        if (rewardImage != null)
        {
            rewardImage.sprite = sprite;
            rewardImage.color = tint;

            // A null sprite on an enabled Image draws as a solid white square, which reads as a broken tile
            // rather than as missing art.
            rewardImage.enabled = sprite != null;
        }

        if (rewardQtdLabel != null) rewardQtdLabel.text = $"+{amount}";
    }

    /// <summary>
    /// The card, its back over it, and its name under it. The card is the deck page's widget with its progress
    /// showing (the copies just won are in that bar); the back is the striped card back, the same size.
    /// </summary>
    private void BuildCard(CardDataSO cardData, SingleCardInDeck cardWidget)
    {
        RectTransform anchor = rewardImage != null ? rewardImage.rectTransform : Body;

        GameObject holder = new GameObject("Card", typeof(RectTransform));
        _card = (RectTransform)holder.transform;
        _card.SetParent(Body, false);
        _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
        _card.anchoredPosition = anchor.anchoredPosition;
        _card.sizeDelta = new Vector2(180f, 250f) * cardScale;

        if (cardWidget != null)
        {
            SingleCardInDeck widget = Instantiate(cardWidget, _card);
            RectTransform widgetRect = (RectTransform)widget.transform;
            widgetRect.anchorMin = widgetRect.anchorMax = new Vector2(0.5f, 0.5f);
            widgetRect.anchoredPosition = Vector2.zero;
            widgetRect.localScale = Vector3.one * cardScale;
            widget.Initialize(cardData);

            if (ServiceLocator.TryGet(out BasePlayerSaveManager save))
            {
                widget.SetProgression(save.GetCardLevel(cardData.CardType),
                    save.GetCardProgress(cardData.CardType)?.Copies ?? 0,
                    save.GetCopiesRequired(cardData.CardType),
                    save.IsCardOwned(cardData.CardType));
                widget.SetUpgradeReady(save.CanUpgradeCard(cardData.CardType));
            }

            // A portrait, not a control: nothing on it answers a tap.
            foreach (Selectable selectable in widget.GetComponentsInChildren<Selectable>(true)) selectable.enabled = false;
            foreach (Graphic graphic in widget.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            _front = widget.gameObject;
            if (rewardImage != null) rewardImage.enabled = false;
        }
        else if (rewardImage != null)
        {
            rewardImage.rectTransform.SetParent(_card, true);
            _front = rewardImage.gameObject;
        }

        GameObject back = new GameObject("Back", typeof(RectTransform), typeof(Image));
        RectTransform backRect = (RectTransform)back.transform;
        backRect.SetParent(_card, false);
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;
        _back = back.GetComponent<Image>();
        _back.sprite = JuiceSprites.CardBack;
        _back.raycastTarget = false;

        // The name sits where the amount was; the amount moves under it.
        if (rewardQtdLabel != null)
        {
            _name = Instantiate(rewardQtdLabel, rewardQtdLabel.transform.parent);
            _name.name = "CardName";
            _name.text = cardData.CardName;
            _name.color = _rarity;
            _name.fontSize = rewardQtdLabel.fontSize * 0.62f;
            _name.rectTransform.anchoredPosition = rewardQtdLabel.rectTransform.anchoredPosition + new Vector2(0f, 8f);
            rewardQtdLabel.rectTransform.anchoredPosition += new Vector2(0f, -44f);
            rewardQtdLabel.fontSize *= 0.8f;
        }

        // Face up until a reveal turns it over: a tile shown without one (no reveal) still shows its card.
        ShowFace(true);
    }

    private void ShowFace(bool up)
    {
        if (_back != null) _back.enabled = !up;
        if (_front != null) _front.SetActive(up);
        if (_name != null) _name.enabled = up;
    }

    /// <summary>Hidden until <see cref="Reveal"/> plays it, so a tile never flashes in before its beat.</summary>
    public void HideForReveal()
    {
        Group.alpha = 0f;
    }

    /// <summary>
    /// The RewardReveal recipe for one tile, returned unstarted-relative so the caller can stagger it: the tile
    /// drops the last stretch onto the screen and lands with a squash and a puff of dust (a thud a step higher per
    /// tile), its amount rolls up from nothing with climbing coins, then punches. A card tile then trembles face
    /// down, turns over, and bursts in its rarity with a flash of the same colour, and stamps NEW! when the player
    /// did not own it before. Ends exactly on the tile as <c>Set*</c> drew it, so completing it early leaves it right.
    /// </summary>
    /// <param name="step">Which tile this is, for the climbing thud.</param>
    /// <param name="intensity">How big the card's burst is (its rarity).</param>
    public Sequence Reveal(int step, bool isNew, float intensity = 1f)
    {
        JuiceTokensSO t = Juice.Tokens;
        RectTransform tile = Body;
        CanvasGroup group = Group;
        bool isCard = _card != null;
        int amount = _amount;
        Vector2 rest = tile.anchoredPosition;
        CounterAnimator counter = rewardQtdLabel != null ? CounterAnimator.On(rewardQtdLabel, v => $"+{v}") : null;

        Sequence s = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // The drop: the last stretch from above, stretched by the fall.
        s.AppendCallback(() =>
        {
            group.alpha = 0f;
            tile.anchoredPosition = rest + Vector2.up * 80f;
            tile.localScale = new Vector3(0.86f, 1.16f, 1f);
            if (counter != null) counter.Set(0);
            if (isCard) ShowFace(false);
        });
        s.Append(group.DOFade(1f, t.Durations.Fall * 0.5f));
        s.Join(tile.DOAnchorPos(rest, t.Durations.Fall).SetEase(t.Eases.Fall));
        s.Join(tile.DOScale(new Vector3(0.9f, 1.12f, 1f), t.Durations.Fall).SetEase(t.Eases.Fall));
        s.AppendCallback(() =>
        {
            Juice.Sfx(SfxId.Thud, step * 2);
            Juice.Haptic(HapticRole.Light);
            JuiceFx.Land(tile, Vector3.one, unscaled: true);
            UiFx.Dust(tile, 10);
        });

        // The amount rolls up, coins climbing, then punches.
        if (counter != null)
        {
            s.Append(DOVirtual.Int(0, amount, t.Durations.Count, v =>
            {
                if (v == counter.Shown) return;
                counter.Set(v);
                if (!isCard) Juice.Sfx(SfxId.Coin, Mathf.FloorToInt(8f * v / Mathf.Max(1, amount)));
            }).SetEase(t.Eases.Out));
            s.AppendCallback(() =>
            {
                counter.Set(amount);
                JuiceFx.Punch(rewardQtdLabel.transform, isCard ? t.Scales.PunchSoft : t.Scales.PunchHard);
            });
        }

        if (isCard)
        {
            RectTransform card = _card;

            // The back trembles, winding up...
            s.AppendCallback(() => Juice.Sfx(SfxId.Charge));
            float wobble = Juice.ReduceMotion ? 0.4f : 1f;
            float beat = 0.42f / 5f;
            foreach (float angle in new[] { -5f, 5f, -7f, 7f, 0f })
                s.Append(card.DOLocalRotate(new Vector3(0f, 0f, angle * wobble), beat).SetEase(t.Eases.Breathe));

            // ...then turns over.
            s.Append(card.DOScaleX(0f, t.Durations.Pop * 0.45f).SetEase(t.Eases.In));
            s.AppendCallback(() =>
            {
                ShowFace(true);
                Juice.Sfx(SfxId.Flip);
            });
            s.Append(card.DOScaleX(1f, t.Durations.Pop * 0.55f).SetEase(t.Eases.Out));
            s.AppendCallback(() =>
            {
                ScreenFlash.Flash(_rarity, 0.35f);
                RarityBurst.Play(tile, _rarity, intensity, behind: true);
                JuiceFx.Punch(tile, t.Scales.PunchHard);
                Juice.Sfx(SfxId.Upgrade);
                Juice.Haptic(HapticRole.Success);
                JuiceCamera.Shake(t.Shake.Small, t.Durations.Shake);
            });

            if (isNew) s.AppendCallback(StampNew);
        }

        s.OnKill(() =>
        {
            group.alpha = 1f;
            tile.anchoredPosition = rest;
            tile.localScale = Vector3.one;
            if (_card != null)
            {
                _card.localRotation = Quaternion.identity;
                _card.localScale = Vector3.one;
                ShowFace(true);
            }

            if (counter != null) counter.Set(amount);
        });

        return s;
    }

    /// <summary>NEW! stamps onto the corner of a card the player has never owned: a deny-red pill, tilted.</summary>
    private void StampNew()
    {
        JuiceTokensSO t = Juice.Tokens;
        RectTransform tile = Body;

        GameObject go = new GameObject("NewStamp", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(tile, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-16f, -14f);
        rect.sizeDelta = new Vector2(150f, 58f);

        Image pill = go.GetComponent<Image>();
        pill.sprite = JuiceSprites.Pill;
        pill.type = Image.Type.Sliced;
        pill.color = t.Colors.Deny;
        pill.raycastTarget = false;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelGo.AddComponent<TextMeshProUGUI>();
        if (rewardQtdLabel != null)
        {
            text.font = rewardQtdLabel.font;
            text.fontSharedMaterial = rewardQtdLabel.fontSharedMaterial;
        }

        text.text = "NEW!";
        text.fontSize = 38f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = t.Colors.Ink;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        // From big and tilted the other way, slammed down onto the corner.
        rect.localScale = Vector3.one * 2.2f;
        rect.localRotation = Quaternion.Euler(0f, 0f, 8f);
        DOTween.Sequence().SetUpdate(true).SetLink(go)
            .Join(rect.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop))
            .Join(rect.DOLocalRotate(new Vector3(0f, 0f, -12f), t.Durations.Pop).SetEase(t.Eases.Pop))
            .OnComplete(() => JuiceFx.Punch(rect, t.Scales.PunchSoft));
        Juice.Sfx(SfxId.Stamp);
        Juice.Haptic(HapticRole.Medium);
    }

    private CanvasGroup Group
    {
        get
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            return group != null ? group : gameObject.AddComponent<CanvasGroup>();
        }
    }
}

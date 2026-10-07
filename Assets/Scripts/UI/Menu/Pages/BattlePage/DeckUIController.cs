using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the deck page: one <see cref="SingleCardInDeck"/> widget per card in the game, reparented
/// between the Card Collection grid and the eight deck positions. Deck state itself lives in
/// <see cref="BasePlayerSaveManager"/> — this class only presents it, so switching deck slots is a
/// relayout rather than a rebuild.
/// </summary>
public class DeckUIController : MonoBehaviour
{
    private class CardInDeckInfo
    {
        public SingleCardInDeck SingleCardInDeck;
        public bool IsEquipped;
        public CardDataSO CardData;
    }

    [Serializable]
    private class CardPosition
    {
        public Transform PositionTransform;
        [ReadOnly] public CardType CardTypeOccupied = CardType.None;
    }

    [Title("References")]
    [SerializeField] private Transform allCardsParent;
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SingleCardInDeck singleCardInDeckPrefab;
    [SerializeField] private ActionFrame actionFramePrefab;
    [SerializeField] private TextMeshProUGUI medianCostText;
    [SerializeField] private List<CardPosition> cardPositions;

    [Title("Deck Slots and Ordering")]
    [SerializeField] private DeckSlotBar deckSlotBar;
    [SerializeField] private CardSortController cardSortController;

    [Title("Motion")]
    [Tooltip("Cards fly to their new place when the page relays out (equip, remove, sort, deck slot) instead " +
             "of jumping there: FLIP. Off: the old instant relayout.")]
    [SerializeField] private bool animateLayout = true;

    [Title("Slots")]
    [Tooltip("Every deck position is framed by a dashed outline a little larger than a card (DeckEquip's slot): " +
             "an empty one shows where the next card goes, a full one holds its card inside the frame.")]
    [SerializeField] private bool showSlotOutlines = true;

    [SerializeField] private Color slotOutlineColor = new(1f, 1f, 1f, 0.16f);

    [Tooltip("How much larger than the card body the outline is, in total (half on each side).")]
    [SerializeField] private Vector2 slotOutlinePadding = new(20f, 20f);

    private readonly Dictionary<CardType, CardInDeckInfo> cardTypeToCardInDeckInfo = new();
    private readonly List<CardDataSO> _sortBuffer = new();

    /// <summary>Where every card was drawn before a relayout: the "First" of FLIP.</summary>
    private readonly Dictionary<SingleCardInDeck, Vector3> _layoutBefore = new();

    /// <summary>False until the page has been laid out once, so the opening layout never animates.</summary>
    private bool _presented;

    private float _shownAverageCost;
    private Tween _averageCostRoll;

    private BasePlayerSaveManager _playerSaveManager;
    private ActionFrame _spawnedActionFrame;

    private const string SlotOutlineName = "SlotOutline";

    public ActionFrame ActionFrame => _spawnedActionFrame;

    private void Start()
    {
        _playerSaveManager = ServiceLocator.Get<BasePlayerSaveManager>();

        if (cardPositions.Count != _playerSaveManager.DeckSize)
            GameLog.Error(
                $"[{nameof(DeckUIController)}] {cardPositions.Count} card positions wired but DeckSize is " +
                $"{_playerSaveManager.DeckSize}; the deck area cannot show a full deck.", this);

        BuildCardWidgets();
        BuildSlotOutlines();
        InitializeActionFrame();

        if (deckSlotBar != null)
        {
            deckSlotBar.OnSlotSelected += HandleSlotSelected;
            deckSlotBar.Initialize(_playerSaveManager.DeckSlotCount, _playerSaveManager.ActiveDeckIndex);
        }

        if (cardSortController != null)
        {
            cardSortController.OnSortChanged += HandleSortChanged;
            cardSortController.Initialize(_playerSaveManager.SortKey, _playerSaveManager.SortAscending);
        }

        _playerSaveManager.OnActiveDeckSlotChanged += HandleActiveDeckSlotChanged;
        _playerSaveManager.OnCardProgressChanged += HandleCardProgressChanged;
        _playerSaveManager.OnGoldChanged += RefreshAllUpgradeReady;

        RefreshAllProgression();
        ApplyDeck(_playerSaveManager.ActiveDeck);
        _presented = true;
    }

    private void OnDestroy()
    {
        if (deckSlotBar != null) deckSlotBar.OnSlotSelected -= HandleSlotSelected;
        if (cardSortController != null) cardSortController.OnSortChanged -= HandleSortChanged;
        if (_playerSaveManager != null)
        {
            _playerSaveManager.OnActiveDeckSlotChanged -= HandleActiveDeckSlotChanged;
            _playerSaveManager.OnCardProgressChanged -= HandleCardProgressChanged;
            _playerSaveManager.OnGoldChanged -= RefreshAllUpgradeReady;
        }

        _averageCostRoll?.Kill();
    }

    /// <summary>Instantiates one widget per card, all starting in the collection. Runs once.</summary>
    private void BuildCardWidgets()
    {
        foreach (CardDataSO cardData in cardDataListSO.CardDataList)
        {
            if (cardData == null) continue;

            if (cardTypeToCardInDeckInfo.ContainsKey(cardData.CardType))
            {
                GameLog.Warn($"[{nameof(DeckUIController)}] Duplicate CardType {cardData.CardType} in " +
                             $"{cardDataListSO.name}; only the first entry is shown.", this);
                continue;
            }

            SingleCardInDeck widget = Instantiate(singleCardInDeckPrefab, allCardsParent);
            widget.Initialize(cardData, this);

            cardTypeToCardInDeckInfo.Add(cardData.CardType, new CardInDeckInfo
            {
                SingleCardInDeck = widget,
                IsEquipped = false,
                CardData = cardData
            });
        }
    }

    /// <summary>
    /// A dashed outline framing each deck position, behind the card that may sit there. Dashes cannot be sliced
    /// without stretching, so the sprite is drawn once at the frame's exact size.
    /// </summary>
    private void BuildSlotOutlines()
    {
        if (!showSlotOutlines) return;

        RectTransform body = singleCardInDeckPrefab.transform.Find("Content") as RectTransform;
        Vector2 size = (body != null ? body.sizeDelta : new Vector2(180f, 250f)) + slotOutlinePadding;
        Sprite sprite = JuiceSprites.DashedRoundRect(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 24, 16, 11, 4f);

        foreach (CardPosition position in cardPositions)
        {
            if (position.PositionTransform == null) continue;

            GameObject go = new GameObject(SlotOutlineName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(position.PositionTransform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = slotOutlineColor;
            image.raycastTarget = false;
        }
    }

    private void InitializeActionFrame()
    {
        _spawnedActionFrame = Instantiate(actionFramePrefab, allCardsParent);
        _spawnedActionFrame.Initialize(this);
    }

    /// <summary>
    /// Lays the page out for one deck slot. Widgets are never re-instantiated — a slot switch only
    /// reparents the existing ones, so this is cheap enough to run on every tap.
    /// </summary>
    private void ApplyDeck(DeckSaveData deck)
    {
        _spawnedActionFrame.HideActionFrame();
        CaptureLayout();

        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
        {
            info.IsEquipped = false;
            MoveToCollection(info.SingleCardInDeck);
        }

        if (deck != null)
        {
            foreach (CardType cardType in deck.Cards)
                if (cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info)) info.IsEquipped = true;
        }

        LayoutDeckArea(deck);
        ApplySort();
        UpdateAverageCost();

        // Every widget changed container, so the grid has to run again before the next draw. Asking for
        // the rebuild here does not rely on one of the reparents above having happened to dirty it.
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)allCardsParent);

        // A deck slot switch: the collection reflows by flight, and the new deck pops in card by card, the
        // way a hand is dealt (NavTabs). Cards that left the deck fly back down with the reflow.
        PlayLayout(null, false, popDeck: true);
    }

    /// <summary>
    /// Places the equipped cards into the deck positions in saved-list order, so position i always shows
    /// <c>deck.Cards[i]</c>. Keeping the two in lockstep is what makes the deck area survive a restart
    /// looking exactly as the player left it.
    /// </summary>
    private void LayoutDeckArea(DeckSaveData deck)
    {
        foreach (CardPosition cardPosition in cardPositions)
            cardPosition.CardTypeOccupied = CardType.None;

        if (deck != null)
        {
            int count = Mathf.Min(deck.Cards.Count, cardPositions.Count);
            for (int i = 0; i < count; i++)
            {
                if (!cardTypeToCardInDeckInfo.TryGetValue(deck.Cards[i], out CardInDeckInfo info)) continue;

                cardPositions[i].CardTypeOccupied = deck.Cards[i];
                MoveToDeckPosition(info.SingleCardInDeck, cardPositions[i].PositionTransform);
            }
        }
    }

    /// <summary>
    /// Hands a card back to the Card Collection. The grid there is a <see cref="GridLayoutGroup"/>, so it
    /// owns every card's anchors, size and position — writing any of them here would only stack the whole
    /// collection on the grid's pivot until the next layout pass.
    /// </summary>
    private void MoveToCollection(SingleCardInDeck card) => card.transform.SetParent(allCardsParent, false);

    /// <summary>
    /// Drops a card into one of the deck positions. Nothing lays those out, so the card has to carry its
    /// own rect — restore the prefab's, since the collection grid overwrote it while the card sat there.
    /// </summary>
    private void MoveToDeckPosition(SingleCardInDeck card, Transform position)
    {
        RectTransform rect = (RectTransform)card.transform;
        RectTransform prefabRect = (RectTransform)singleCardInDeckPrefab.transform;

        rect.SetParent(position, false);
        rect.anchorMin = prefabRect.anchorMin;
        rect.anchorMax = prefabRect.anchorMax;
        rect.pivot = prefabRect.pivot;
        rect.sizeDelta = prefabRect.sizeDelta;
        rect.anchoredPosition = Vector2.zero;
    }

    /// <summary>Reorders the collection grid. Equipped cards are not in it, so they keep the player order.</summary>
    private void ApplySort()
    {
        _sortBuffer.Clear();
        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
            if (!info.IsEquipped) _sortBuffer.Add(info.CardData);

        CardSortKey key = _playerSaveManager.SortKey;
        bool ascending = _playerSaveManager.SortAscending;

        // Owned cards first whatever the key is: what the player can actually use should never be pushed
        // below what they cannot.
        _sortBuffer.Sort((a, b) =>
        {
            bool ownedA = _playerSaveManager.IsCardOwned(a.CardType);
            bool ownedB = _playerSaveManager.IsCardOwned(b.CardType);
            if (ownedA != ownedB) return ownedA ? -1 : 1;

            return CardSortComparer.Compare(a, b, key, ascending);
        });

        for (int i = 0; i < _sortBuffer.Count; i++)
            cardTypeToCardInDeckInfo[_sortBuffer[i].CardType].SingleCardInDeck.transform.SetSiblingIndex(i);

        // The popup ignores layout, but it still has to draw on top of the grid it sits in.
        if (_spawnedActionFrame != null && _spawnedActionFrame.transform.parent == allCardsParent)
            _spawnedActionFrame.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Opens the card's popup pinned to the top edge of the card (DeckEquip: it opens above the card and pops from
    /// its bottom edge, so the card stays visible). It lives in the container holding the card's whole section, so
    /// it draws over the neighbouring cards and moves with the page when it scrolls; it ignores layout there.
    /// </summary>
    public void RequestActionFrame(CardDataSO cardData)
    {
        CardInDeckInfo info = cardTypeToCardInDeckInfo[cardData.CardType];
        Transform frame = _spawnedActionFrame.transform;

        frame.SetParent(info.SingleCardInDeck.transform.parent.parent, false);
        frame.position = info.SingleCardInDeck.TopCenterWorld;
        frame.SetAsLastSibling();
        _spawnedActionFrame.ActivateActionFrame(cardData, info.IsEquipped);
    }

    /// <summary>
    /// Moves a card into or out of the active deck. Returns false when the change was refused, so the
    /// caller does not adopt a state the deck never entered.
    /// </summary>
    public bool SetEquippedCard(CardType cardType, bool isEquipped)
    {
        if (!cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info))
        {
            GameLog.Warn($"CardType {cardType} not found in cardTypeToCardInDeckInfo.");
            return false;
        }

        if (isEquipped)
        {
            // Ask which rule would refuse first, so the toast names it (not owned, full, already in).
            EquipRefusal refusal = _playerSaveManager.GetEquipRefusal(cardType);
            if (refusal != EquipRefusal.None || !_playerSaveManager.TryEquipCard(cardType))
            {
                Juice.Refuse(RefusalReasons.From(refusal != EquipRefusal.None ? refusal : EquipRefusal.InvalidCard));

                // A full deck is the deck's no, so the deck shakes; any other no belongs to the card.
                if (refusal == EquipRefusal.DeckFull) ShakeDeck();
                else JuiceFx.Deny(info.SingleCardInDeck.Visual, Juice.Tokens.Distances.ShakeCard);
                return false;
            }

            CaptureLayout();
            info.IsEquipped = true;
        }
        else
        {
            CaptureLayout();
            _playerSaveManager.UnequipCard(cardType);
            info.IsEquipped = false;

            MoveToCollection(info.SingleCardInDeck);

            ApplySort(); // the returned card must land in its sorted place, not at the end of the grid
        }

        LayoutDeckArea(_playerSaveManager.ActiveDeck);
        UpdateAverageCost();

        // Equip: the whoosh carries the card over. Remove: the deck closes ranks at once, and ticks as it does.
        Juice.Sfx(SfxId.Whoosh);
        if (!isEquipped) Juice.Sfx(SfxId.Tick);
        PlayLayout(info.SingleCardInDeck, isEquipped);
        return true;
    }

    private void UpdateAverageCost()
    {
        float totalCost = 0f;
        int equippedCount = 0;

        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
        {
            if (!info.IsEquipped) continue;

            totalCost += info.CardData.Cost;
            equippedCount++;
        }

        float average = equippedCount > 0 ? totalCost / equippedCount : 0f;
        _averageCostRoll?.Kill();

        // The first layout states the number; every later change rolls to it and pops, so the player sees
        // what the card they just moved did to the deck.
        if (!_presented || Juice.ReduceMotion || Mathf.Approximately(average, _shownAverageCost))
        {
            _shownAverageCost = average;
            medianCostText.text = $"{average:0.0}";
            return;
        }

        JuiceTokensSO t = Juice.Tokens;
        _averageCostRoll = DOVirtual.Float(_shownAverageCost, average, t.Durations.Count, value =>
            {
                _shownAverageCost = value;
                medianCostText.text = $"{value:0.0}";
            })
            .SetEase(t.Eases.Out).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => JuiceFx.Punch(medianCostText.transform, t.Scales.PunchSoft));
    }

    /// <summary>The "First" of FLIP: where every card is drawn before the layout moves them.</summary>
    private void CaptureLayout()
    {
        _layoutBefore.Clear();
        if (!CanAnimateLayout) return;

        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
            _layoutBefore[info.SingleCardInDeck] = info.SingleCardInDeck.VisualPosition;
    }

    /// <summary>
    /// The "Last, Invert, Play" of FLIP: settle the layout now, then draw every card that moved back where it
    /// was and fly it to its new place. The card the player moved arcs over the page and lands; the deck closes
    /// ranks behind a removed card, and the collection slides into the gap a card left, a little after one another.
    /// </summary>
    /// <param name="moved">The card the player equipped or removed, or null for a reorder.</param>
    /// <param name="movedIntoDeck">Whether that card went into the deck (a landing) or came out of it.</param>
    /// <param name="popDeck">A deck slot switch: the deck's cards pop in where they are instead of flying.</param>
    private void PlayLayout(SingleCardInDeck moved, bool movedIntoDeck, bool popDeck = false)
    {
        if (_layoutBefore.Count == 0 || !CanAnimateLayout)
        {
            _layoutBefore.Clear();
            return;
        }

        // Last: the grid has to know where everything goes before anything can fly there.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)allCardsParent);

        JuiceTokensSO t = Juice.Tokens;
        bool popped = false;

        // The deck first, in slot order: on a slot switch it pops in card by card, the way a hand is dealt; after a
        // removal the cards behind the gap close ranks one after another, overshooting into their slots.
        int closed = 0;
        for (int i = 0; i < cardPositions.Count; i++)
        {
            CardType type = cardPositions[i].CardTypeOccupied;
            if (type == CardType.None || !cardTypeToCardInDeckInfo.TryGetValue(type, out CardInDeckInfo info)) continue;

            SingleCardInDeck card = info.SingleCardInDeck;
            if (!_layoutBefore.Remove(card, out Vector3 before)) continue;

            if (popDeck)
            {
                card.StopFlight();
                card.PopIn(i * t.Durations.Stagger);
                popped = true;
                continue;
            }

            if (card == moved)
            {
                card.FlyFrom(before, 0f, SingleCardInDeck.Flight.Equip, () => HandleCardLanded(card, true));
                continue;
            }

            if (card.FlyFrom(before, closed * t.Durations.Stagger, SingleCardInDeck.Flight.Snap)) closed++;
        }

        // Then the collection: a removed card travels home, and the grid slides into the gap a card left (or opens
        // the one a returning card needs).
        int reflowed = 0;
        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
        {
            SingleCardInDeck card = info.SingleCardInDeck;
            if (!_layoutBefore.TryGetValue(card, out Vector3 before)) continue;

            if (card == moved)
            {
                card.FlyFrom(before, 0f, SingleCardInDeck.Flight.Remove, () => HandleCardLanded(card, movedIntoDeck));
                continue;
            }

            float delay = Mathf.Min(reflowed, 10) * t.Durations.Stagger * 0.5f;
            if (card.FlyFrom(before, delay, SingleCardInDeck.Flight.Slide)) reflowed++;
        }

        if (popped) Juice.Sfx(SfxId.Tick);
        _layoutBefore.Clear();
    }

    /// <summary>
    /// The moved card arrives and settles with a squash. Into the deck, its slot answers: an attention ring, a thud
    /// and a buzz. Home, the squash is all (the deck already ticked as it closed ranks).
    /// </summary>
    private void HandleCardLanded(SingleCardInDeck card, bool intoDeck)
    {
        JuiceFx.Land(card.Visual, Vector3.one, unscaled: true);
        if (!intoDeck) return;

        if (card.transform.parent is RectTransform slot)
            UiFx.Ring(slot, Juice.Tokens.Colors.Attention, 130f, 330f, 0.4f);

        Juice.Sfx(SfxId.Thud);
        Juice.Haptic(HapticRole.Select);
    }

    /// <summary>The deck says no as a whole: every card in it shakes.</summary>
    private void ShakeDeck()
    {
        float distance = Juice.Tokens.Distances.ShakeCard;
        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
            if (info.IsEquipped && !info.SingleCardInDeck.IsFlying) JuiceFx.Deny(info.SingleCardInDeck.Visual, distance);
    }

    /// <summary>Only a page on screen animates: a hidden page relays out instantly, like it always did.</summary>
    private bool CanAnimateLayout => animateLayout && _presented && isActiveAndEnabled &&
                                     allCardsParent != null && allCardsParent.gameObject.activeInHierarchy;

    /// <summary>Pushes one card's saved level and copy count into its widget.</summary>
    private void RefreshProgression(CardType cardType)
    {
        if (!cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info)) return;

        info.SingleCardInDeck.SetProgression(
            _playerSaveManager.GetCardLevel(cardType),
            _playerSaveManager.GetCardProgress(cardType)?.Copies ?? 0,
            _playerSaveManager.GetCopiesRequired(cardType),
            _playerSaveManager.IsCardOwned(cardType));

        info.SingleCardInDeck.SetUpgradeReady(_playerSaveManager.CanUpgradeCard(cardType));
    }

    /// <summary>Gold decides which cards can be upgraded, so every tile re-asks when it moves.</summary>
    private void RefreshAllUpgradeReady()
    {
        foreach (KeyValuePair<CardType, CardInDeckInfo> pair in cardTypeToCardInDeckInfo)
            pair.Value.SingleCardInDeck.SetUpgradeReady(_playerSaveManager.CanUpgradeCard(pair.Key));
    }

    private void RefreshAllProgression()
    {
        foreach (CardType cardType in cardTypeToCardInDeckInfo.Keys) RefreshProgression(cardType);
    }

    /// <summary>
    /// The widget showing one card, whichever container it currently sits in. The tutorial needs it to
    /// point at a specific card; nothing else outside this page should care which widget is which.
    /// </summary>
    public bool TryGetCardWidget(CardType cardType, out SingleCardInDeck widget)
    {
        widget = cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info) ? info.SingleCardInDeck : null;
        return widget != null;
    }

    /// <summary>Whether this card is in the active deck right now.</summary>
    public bool IsCardEquipped(CardType cardType) =>
        cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info) && info.IsEquipped;

    /// <summary>How many cards the active deck holds. The tutorial watches it to know a card came out.</summary>
    public int EquippedCount
    {
        get
        {
            int count = 0;
            foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values) if (info.IsEquipped) count++;
            return count;
        }
    }

    /// <summary>Whether the next level is affordable, and what it costs. Read by the ActionFrame.</summary>
    public CardUpgradeValidation GetUpgradeState(CardType cardType) => _playerSaveManager.CanUpgradeCard(cardType);

    /// <summary>
    /// Buys the next level of a card, or explains why it cannot. The rules live in the save manager; this
    /// only turns a refusal into player-facing feedback.
    /// </summary>
    public bool TryUpgradeCard(CardType cardType)
    {
        CardUpgradeValidation upgrade = _playerSaveManager.CanUpgradeCard(cardType);

        if (!upgrade)
        {
            Juice.Refuse(upgrade.Refusal, upgrade.RefusalDetail);
            return false;
        }

        return _playerSaveManager.TryUpgradeCard(cardType);
    }

    /// <summary>A card levelled up, or a reward unlocked it. Relayout only when ownership actually changed.</summary>
    private void HandleCardProgressChanged(CardType cardType)
    {
        bool wasInCollection = cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info)
                               && !info.SingleCardInDeck.IsOwned;

        RefreshProgression(cardType);

        // A newly unlocked card stops sorting with the locked ones.
        if (wasInCollection && _playerSaveManager.IsCardOwned(cardType))
        {
            CaptureLayout();
            ApplySort();
            PlayLayout(null, false);
        }
    }

    private void HandleSlotSelected(int index)
    {
        Juice.Sfx(SfxId.Tick);
        Juice.Haptic(HapticRole.Select);
        _playerSaveManager.SetActiveDeck(index);
    }

    private void HandleActiveDeckSlotChanged(int index)
    {
        if (deckSlotBar != null) deckSlotBar.SetSelected(index);
        ApplyDeck(_playerSaveManager.ActiveDeck);
    }

    private void HandleSortChanged(CardSortKey key, bool ascending)
    {
        CaptureLayout();
        _playerSaveManager.SetSortPreference(key, ascending);
        ApplySort();
        PlayLayout(null, false);
    }
}

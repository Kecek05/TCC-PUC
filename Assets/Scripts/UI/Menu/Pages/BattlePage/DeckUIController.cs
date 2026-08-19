using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

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

    private readonly Dictionary<CardType, CardInDeckInfo> cardTypeToCardInDeckInfo = new();
    private readonly List<CardDataSO> _sortBuffer = new();

    private BasePlayerSaveManager _playerSaveManager;
    private ActionFrame _spawnedActionFrame;
    private ScreenWarning _screenWarning;

    public ActionFrame ActionFrame => _spawnedActionFrame;

    private void Start()
    {
        _screenWarning = ServiceLocator.Get<ScreenWarning>();
        _playerSaveManager = ServiceLocator.Get<BasePlayerSaveManager>();

        if (cardPositions.Count != _playerSaveManager.DeckSize)
            GameLog.Error(
                $"[{nameof(DeckUIController)}] {cardPositions.Count} card positions wired but DeckSize is " +
                $"{_playerSaveManager.DeckSize}; the deck area cannot show a full deck.", this);

        BuildCardWidgets();
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

        ApplyDeck(_playerSaveManager.ActiveDeck);
    }

    private void OnDestroy()
    {
        if (deckSlotBar != null) deckSlotBar.OnSlotSelected -= HandleSlotSelected;
        if (cardSortController != null) cardSortController.OnSortChanged -= HandleSortChanged;
        if (_playerSaveManager != null) _playerSaveManager.OnActiveDeckSlotChanged -= HandleActiveDeckSlotChanged;
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
            widget.transform.localPosition = Vector3.zero;

            cardTypeToCardInDeckInfo.Add(cardData.CardType, new CardInDeckInfo
            {
                SingleCardInDeck = widget,
                IsEquipped = false,
                CardData = cardData
            });
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

        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
        {
            info.IsEquipped = false;
            info.SingleCardInDeck.transform.SetParent(allCardsParent);
            info.SingleCardInDeck.transform.localPosition = Vector3.zero;
        }

        if (deck != null)
        {
            foreach (CardType cardType in deck.Cards)
                if (cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info)) info.IsEquipped = true;
        }

        LayoutDeckArea(deck);
        ApplySort();
        UpdateAverageCost();
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

        if (deck == null) return;

        int count = Mathf.Min(deck.Cards.Count, cardPositions.Count);
        for (int i = 0; i < count; i++)
        {
            if (!cardTypeToCardInDeckInfo.TryGetValue(deck.Cards[i], out CardInDeckInfo info)) continue;

            cardPositions[i].CardTypeOccupied = deck.Cards[i];
            info.SingleCardInDeck.transform.SetParent(cardPositions[i].PositionTransform);
            info.SingleCardInDeck.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>Reorders the collection grid. Equipped cards are not in it, so they keep the player order.</summary>
    private void ApplySort()
    {
        _sortBuffer.Clear();
        foreach (CardInDeckInfo info in cardTypeToCardInDeckInfo.Values)
            if (!info.IsEquipped) _sortBuffer.Add(info.CardData);

        _sortBuffer.Sort(CardSortComparer.GetComparison(_playerSaveManager.SortKey, _playerSaveManager.SortAscending));

        for (int i = 0; i < _sortBuffer.Count; i++)
            cardTypeToCardInDeckInfo[_sortBuffer[i].CardType].SingleCardInDeck.transform.SetSiblingIndex(i);

        // The popup ignores layout, but it still has to draw on top of the grid it sits in.
        if (_spawnedActionFrame != null && _spawnedActionFrame.transform.parent == allCardsParent)
            _spawnedActionFrame.transform.SetAsLastSibling();
    }

    public void RequestActionFrame(CardDataSO cardData)
    {
        _spawnedActionFrame.transform.SetParent(cardTypeToCardInDeckInfo[cardData.CardType].SingleCardInDeck.transform);
        _spawnedActionFrame.transform.localPosition = Vector3.zero;
        _spawnedActionFrame.transform.SetParent(cardTypeToCardInDeckInfo[cardData.CardType].SingleCardInDeck.transform.parent.parent);
        _spawnedActionFrame.transform.SetAsLastSibling();
        _spawnedActionFrame.ActivateActionFrame(cardData, cardTypeToCardInDeckInfo[cardData.CardType].IsEquipped);
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
            if (!_playerSaveManager.TryEquipCard(cardType))
            {
                _screenWarning.ShowWarning(WarningMessages.CannotEquipCard);
                return false;
            }

            info.IsEquipped = true;
        }
        else
        {
            _playerSaveManager.UnequipCard(cardType);
            info.IsEquipped = false;

            info.SingleCardInDeck.transform.SetParent(allCardsParent);
            info.SingleCardInDeck.transform.localPosition = Vector3.zero;

            ApplySort(); // the returned card must land in its sorted place, not at the end of the grid
        }

        LayoutDeckArea(_playerSaveManager.ActiveDeck);
        UpdateAverageCost();
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

        medianCostText.text = equippedCount > 0 ? $"{totalCost / equippedCount:0.0}" : "0.0";
    }

    private void HandleSlotSelected(int index) => _playerSaveManager.SetActiveDeck(index);

    private void HandleActiveDeckSlotChanged(int index)
    {
        if (deckSlotBar != null) deckSlotBar.SetSelected(index);
        ApplyDeck(_playerSaveManager.ActiveDeck);
    }

    private void HandleSortChanged(CardSortKey key, bool ascending)
    {
        _playerSaveManager.SetSortPreference(key, ascending);
        ApplySort();
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>Taps a card in the deck page's collection (or its deck row) to open its popup.</summary>
    [Serializable]
    public class TapDeckCard : ICaptureAction
    {
        public CardType Card = CardType.None;
        public float Hold = 0.08f;

        public TapDeckCard() { }
        public TapDeckCard(CardType card) => Card = card;

        public IEnumerator Run(CaptureContext ctx)
        {
            DeckUIController deck = UnityEngine.Object.FindFirstObjectByType<DeckUIController>();
            if (deck == null || !deck.TryGetCardWidget(Card, out SingleCardInDeck widget) || widget == null)
                throw new InvalidOperationException($"No deck widget for {Card}");

            // The invisible hit area is the widget's Button; the body (Visual) is where the finger lands.
            UnityEngine.UI.Button button = widget.GetComponentInChildren<UnityEngine.UI.Button>(true);
            GameObject target = button != null ? button.gameObject : widget.gameObject;
            Vector2 at = HandCards.ScreenPoint(widget.Visual);

            yield return ctx.Finger.Tap(target, at, Hold);
        }

        public string Describe() => $"TapDeckCard {Card}";
    }

    /// <summary>
    /// Off tape: puts the save in the state a menu shot starts from (cards owned, gold, deck slots). Writes the
    /// real save file, so the capture session backs it up first and restores it after.
    /// </summary>
    [Serializable]
    public class PrepareSave : ICaptureAction
    {
        public bool OwnEveryCard = true;
        public int CopiesEach = 100;
        public int Gold;
        [Tooltip("Deck slot contents, by slot index. A slot left out is untouched; an empty list empties it.")]
        public List<DeckSlotContent> Decks = new();
        public int ActiveSlot = -1;

        public IEnumerator Run(CaptureContext ctx)
        {
            BasePlayerSaveManager save = ctx.Save;
            if (save == null) throw new InvalidOperationException("No save manager");

            if (OwnEveryCard && ctx.Settings?.Cards != null)
                foreach (CardDataSO card in ctx.Settings.Cards.CardDataList)
                    if (card != null) save.AddCardCopies(card.CardType, CopiesEach);

            if (Gold > 0) save.AddGold(Gold);

            foreach (DeckSlotContent slot in Decks)
                save.SetDeckCards(slot.Slot, new List<CardType>(slot.Cards));

            if (ActiveSlot >= 0) save.SetActiveDeck(ActiveSlot);
            yield return null;
        }

        public string Describe() => $"PrepareSave (own all {OwnEveryCard}, gold +{Gold}, {Decks.Count} slots, active {ActiveSlot})";
    }

    [Serializable]
    public class DeckSlotContent
    {
        public int Slot;
        public List<CardType> Cards = new();

        public DeckSlotContent() { }
        public DeckSlotContent(int slot, params CardType[] cards)
        {
            Slot = slot;
            Cards = new List<CardType>(cards);
        }
    }

    /// <summary>
    /// Drags a ScrollRect with the finger (a real scroll, momentum and all), e.g. the deck page's collection.
    /// </summary>
    [Serializable]
    public class ScrollUi : ICaptureAction
    {
        public string ScrollName = "";
        [Tooltip("Screen fraction to drag; positive scrolls the content up (reveals what is below).")]
        public float Amount = 0.25f;
        public float Seconds = 0.35f;

        public ScrollUi() { }
        public ScrollUi(string scrollName, float amount, float seconds = 0.35f)
        {
            ScrollName = scrollName;
            Amount = amount;
            Seconds = seconds;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            RectTransform rect = UiFinder.Find(ScrollName);
            if (rect == null) throw new InvalidOperationException($"No scroll '{ScrollName}'");

            Vector2 centre = HandCards.ScreenPoint(rect);
            float dy = Amount * Screen.height;
            Vector2 from = centre - new Vector2(0f, dy * 0.5f);
            Vector2 to = centre + new Vector2(0f, dy * 0.5f);
            yield return ctx.Finger.Drag(rect.gameObject, from, () => to, Seconds, 0.05f, 0.05f);
        }

        public string Describe() => $"ScrollUi {ScrollName} {Amount:+0.00;-0.00}";
    }
}
#endif

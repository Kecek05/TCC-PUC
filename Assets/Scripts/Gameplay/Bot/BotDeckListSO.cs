using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Authoring list of decks a fallback BOT can play. When a bot fills an empty slot, one of these
/// decks is picked at random (<see cref="GetRandomDeck"/>) and fed into the same deck pipeline a
/// human uses (<c>UserData.DeckCards</c> -> <c>ServerCardHandManager.SetDeckForPlayer</c>).
/// Mirrors the project's list-SO idiom (see <see cref="CardDataListSO"/>).
/// </summary>
[CreateAssetMenu(fileName = "BotDeckListSO", menuName = "Scriptable Objects/Bot/BotDeckListSO")]
public class BotDeckListSO : ScriptableObject
{
    [System.Serializable]
    public class BotDeckDefinition
    {
        [Tooltip("Editor-only label, so decks are identifiable in logs and the inspector.")]
        public string Name = "Deck";

        [Tooltip("The cards this deck contains. Should hold DeckSize (default 12) cards, same as a human deck.")]
        public List<CardType> Cards = new();
    }

    [ListDrawerSettings(ShowFoldout = true)]
    public List<BotDeckDefinition> Decks = new();

    /// <summary>Returns a copy of a randomly-chosen deck's card list, or null if none are configured.</summary>
    public List<CardType> GetRandomDeck()
    {
        if (Decks == null || Decks.Count == 0)
        {
            GameLog.Error("[BotDeckListSO] No bot decks configured.");
            return null;
        }

        BotDeckDefinition chosen = Decks[Random.Range(0, Decks.Count)];
        GameLog.Info($"[BotDeckListSO] Selected bot deck '{chosen.Name}' ({chosen.Cards.Count} cards).");
        return new List<CardType>(chosen.Cards);
    }
}

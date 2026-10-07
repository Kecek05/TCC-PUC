using UnityEngine;

/// <summary>
/// The local player's own card level during a match, for presentation: the aim's tags (KO, +25%) and a cast's
/// floats should say what this player's copy of the card does, which is what the server scales it by.
/// </summary>
/// <remarks>
/// The server's <see cref="MatchCardLevels"/> is filled only on the host; a pure client reads its own level from the
/// <see cref="UserData"/> it connected with (the deck the match dealt from, the tutorial's loan included) and runs it
/// through the same <see cref="CardProgressionSettingsSO"/>, so both agree. Level 1 whenever anything is missing.
/// </remarks>
public static class SpellFxLevels
{
    public static CardLevelScale Local(CardDataSO card)
    {
        if (card == null || !ServiceLocator.TryGet(out MatchCardLevels levels) || levels == null || levels.Progression == null)
            return CardLevelScale.One;

        int level = 1;
        if (ServiceLocator.TryGet(out BaseClientManager client) && client != null && client.UserData != null)
        {
            UserData data = client.UserData;
            int index = data.DeckCards != null ? data.DeckCards.IndexOf(card.CardType) : -1;
            if (index >= 0 && data.DeckCardLevels != null && index < data.DeckCardLevels.Count)
                level = Mathf.Max(1, data.DeckCardLevels[index]);
        }

        return levels.Progression.GetScale(card, level);
    }
}

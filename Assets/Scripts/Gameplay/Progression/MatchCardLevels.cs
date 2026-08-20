using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Server-side lookup of "what level is this team's copy of this card", for the duration of one match.
/// Filled once in <c>DrawingCardsState</c> from the same <c>UserData</c> the deck is dealt from, then read
/// by the three card deployers when they spawn something.
/// </summary>
/// <remarks>
/// Deliberately not a NetworkBehaviour: levels are decided before the match starts and never change during
/// it, so there is nothing to replicate. Clients see the effect through the stats that are already synced
/// (enemy health, bullet speed, tower fire rate).
/// <para>
/// Callers should resolve this with <c>ServiceLocator.TryGet</c>: debug scenes have no instance, and the
/// correct behaviour there is "everything is level 1", not an exception.
/// </para>
/// </remarks>
public class MatchCardLevels : MonoBehaviour
{
    [Title("References")]
    [SerializeField, Required] private CardProgressionSettingsSO cardProgression;
    [SerializeField, Required] private CardDataListSO cardDataListSO;

    private readonly Dictionary<TeamType, Dictionary<CardType, int>> _levelsByTeam = new();

    public CardProgressionSettingsSO Progression => cardProgression;

    private void Awake() => ServiceLocator.Register(this);

    private void OnDestroy() => ServiceLocator.Unregister<MatchCardLevels>();

    /// <summary>
    /// Records one player's deck levels. <paramref name="levels"/> is index-aligned with
    /// <paramref name="cards"/>; a short, long or missing list degrades to level 1 rather than throwing,
    /// because it arrives from a client-authored connection payload.
    /// </summary>
    public void SetLevels(TeamType team, List<CardType> cards, List<int> levels)
    {
        if (team == TeamType.None || cards == null) return;

        Dictionary<CardType, int> map = new(cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            int level = levels != null && i < levels.Count ? levels[i] : 1;
            map[cards[i]] = Mathf.Max(1, level);
        }

        _levelsByTeam[team] = map;

        if (levels == null || levels.Count != cards.Count)
            GameLog.Warn($"[{nameof(MatchCardLevels)}] {team} sent {levels?.Count ?? 0} levels for " +
                         $"{cards.Count} cards; the missing ones default to level 1.");
    }

    public int GetLevel(TeamType team, CardType cardType)
    {
        if (_levelsByTeam.TryGetValue(team, out Dictionary<CardType, int> map) &&
            map.TryGetValue(cardType, out int level))
            return level;

        return 1;
    }

    /// <summary>The multipliers to apply to whatever this team plays. Level 1 when anything is missing.</summary>
    public CardLevelScale GetScale(TeamType team, CardType cardType)
    {
        if (cardProgression == null || cardDataListSO == null) return CardLevelScale.One;

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData == null) return CardLevelScale.One;

        return cardProgression.GetScale(cardData, GetLevel(team, cardType));
    }

    /// <summary>Scale for enemies that no player summoned (the AI wave horde).</summary>
    public CardLevelScale GetWaveScale() =>
        cardProgression == null ? CardLevelScale.One : cardProgression.GetDefaultScale(cardProgression.WaveEnemyCardLevel);

    /// <summary>Static convenience so call sites stay one line and debug scenes keep working.</summary>
    public static CardLevelScale ScaleFor(TeamType team, CardType cardType) =>
        ServiceLocator.TryGet(out MatchCardLevels levels) ? levels.GetScale(team, cardType) : CardLevelScale.One;

    public static CardLevelScale WaveScale() =>
        ServiceLocator.TryGet(out MatchCardLevels levels) ? levels.GetWaveScale() : CardLevelScale.One;
}

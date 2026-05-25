using UnityEngine;

/// <summary>
/// One possible play the bot could make: a (card, target position, score) triple.
/// Position is Vector2.zero for non-spatial cards (e.g. SpawnEnemy).
/// </summary>
public readonly struct ScoredCandidate
{
    public readonly CardType Card;
    public readonly Vector2 Position;
    public readonly float Score;

    public ScoredCandidate(CardType card, Vector2 position, float score)
    {
        Card = card;
        Position = position;
        Score = score;
    }

    public ScoredCandidate WithScore(float newScore) => new ScoredCandidate(Card, Position, newScore);

    public bool IsValid => Score > 0f;
}

using System.Collections.Generic;

/// <summary>
/// Per-card-type strategy that produces zero-or-more candidate plays with scores.
/// Sub-factory dispatch pattern: each concrete scorer declares which CardDataSO it handles.
/// </summary>
public abstract class BaseCardScorer
{
    public abstract bool CanHandle(CardDataSO data);
    public abstract IEnumerable<ScoredCandidate> Score(CardDataSO data, BotContext ctx);
}

using System.Collections.Generic;

/// <summary>
/// Holds the list of BaseCardScorer instances and dispatches CardDataSO -> first scorer
/// whose CanHandle returns true. New card types only need a new scorer registered here.
/// </summary>
public class BotScorerRegistry
{
    private readonly List<BaseCardScorer> _scorers = new();

    public void Register(BaseCardScorer scorer)
    {
        if (scorer != null && !_scorers.Contains(scorer))
            _scorers.Add(scorer);
    }

    public BaseCardScorer Resolve(CardDataSO data)
    {
        for (int i = 0; i < _scorers.Count; i++)
        {
            if (_scorers[i].CanHandle(data)) return _scorers[i];
        }
        return null;
    }

    public static BotScorerRegistry CreateDefault()
    {
        BotScorerRegistry r = new BotScorerRegistry();
        r.Register(new TowerCardScorer());
        r.Register(new SpellCardScorer());
        r.Register(new SpawnEnemyCardScorer());
        return r;
    }
}

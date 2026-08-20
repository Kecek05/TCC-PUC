using UnityEngine;

public struct SpellExecutionContext
{
    public Vector2 ServerPosition;
    public TeamType CasterTeam;
    public SpellDataSO SpellData;
    public MonoBehaviour CoroutineRunner;

    /// <summary>
    /// Multipliers from the caster's persistent card level. It rides the context rather than the executor
    /// because SpellExecutorFactory hands out shared, stateless singletons - one FireballExecutor serves
    /// both players, so per-cast state cannot live on it.
    /// </summary>
    public CardLevelScale Scale;
}

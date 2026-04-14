using UnityEngine;

public interface ISpellExecutor
{
    void Execute(SpellExecutionContext context);
}

public struct SpellExecutionContext
{
    public Vector2 ServerPosition;
    public TeamType CasterTeam;
    public SpellDataSO SpellData;
    public MonoBehaviour CoroutineRunner;
}

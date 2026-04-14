using UnityEngine;

public struct SpellExecutionContext
{
    public Vector2 ServerPosition;
    public TeamType CasterTeam;
    public SpellDataSO SpellData;
    public MonoBehaviour CoroutineRunner;
}

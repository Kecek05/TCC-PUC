/// <summary>How an enemy left the field. Sent as a single byte, so keep it small and append only.</summary>
public enum EnemyExitKind : byte
{
    Killed,
    ReachedBase,
}

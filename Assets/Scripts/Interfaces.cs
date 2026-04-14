using UnityEngine;

public interface ITeamMember
{
    TeamType GetTeamType();
    
    void  SetTeamType(TeamType teamType);
}

public interface ICardActivatable
{
    /// <summary>
    /// Used for without context (in-hand, cooldown, etc)
    /// </summary>
    CardValidation CanPlayCard();

    /// <summary>
    /// Used with placement context (world-space position from camera projection)
    /// </summary>
    CardValidation CanPlayCardAt(Vector2 worldPosition);

    void ActivateCard(Vector2 worldPosition);
}

public interface ISpellExecutor
{
    void Execute(SpellExecutionContext context);
}

public interface IDamageable
{
    void TakeDamage(float damage);
}

public interface IPlaceable
{
    public bool Occupied { get; }
    public TowerManager OccupiedTower { get; }
    
    public Transform PlaceablePoint { get; }
    
    public void Occupy(TowerManager towerManager);
    
    public bool IsOccupied();
}

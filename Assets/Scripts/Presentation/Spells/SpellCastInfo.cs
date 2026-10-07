using UnityEngine;

/// <summary>
/// Everything a spell visual knows about the cast it draws, resolved on each client from the cast broadcast:
/// where it lands (in this client's view), whose field that is, who cast it and whether that was us.
/// </summary>
/// <remarks>
/// <see cref="CasterTeam"/> is worked out rather than sent: a spell that only works on the enemy field lands on
/// the caster's opponent's field, every other spell on the caster's own (<see cref="SpellCardDataSO.GetTargetFieldTeam"/>
/// run backwards), so the broadcast did not have to change.
/// </remarks>
public readonly struct SpellCastInfo
{
    public readonly SpellType Spell;
    public readonly SpellDataSO Data;
    public readonly SpellCardDataSO Card;

    /// <summary>The landing point in this client's local view.</summary>
    public readonly Vector3 Position;

    /// <summary>The team whose field the spell lands on.</summary>
    public readonly TeamType FieldTeam;

    public readonly TeamType CasterTeam;

    /// <summary>Whether this client cast it: only our own actions vibrate.</summary>
    public readonly bool CasterIsLocal;

    public readonly float Range;
    public readonly float TravelTime;

    /// <summary>The effect's duration at the caster's card level (0 for an instant spell).</summary>
    public readonly float Duration;

    /// <summary>The caster's card level, as far as this client can know it (level 1 for the opponent on a pure client).</summary>
    public readonly CardLevelScale Scale;

    public SpellCastInfo(SpellType spell, SpellDataSO data, SpellCardDataSO card, Vector3 position, TeamType fieldTeam,
        TeamType casterTeam, bool casterIsLocal, float duration, CardLevelScale scale)
    {
        Spell = spell;
        Data = data;
        Card = card;
        Position = new Vector3(position.x, position.y, 0f);
        FieldTeam = fieldTeam;
        CasterTeam = casterTeam;
        CasterIsLocal = casterIsLocal;
        Range = data != null ? data.Range * scale.Range : 1f;
        TravelTime = data != null ? Mathf.Max(0f, data.TravelTime) : 0f;
        Duration = Mathf.Max(0f, duration);
        Scale = scale;
    }
}

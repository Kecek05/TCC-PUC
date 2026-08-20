using System;
using System.Collections.Generic;

/// <summary>
/// Root of the local player save. A plain <c>[Serializable]</c> POCO so <c>JsonUtility</c> can round-trip
/// it, mirroring how <see cref="UserData"/> is serialized for the connection payload.
/// </summary>
/// <remarks>
/// <c>JsonUtility</c> writes enums as their integer value, so new <see cref="CardType"/> members must keep
/// being <b>appended</b> to the end of <c>Enums.cs</c>. Inserting one in the middle silently re-maps every
/// saved deck <i>and every saved card level</i>; <see cref="SaveVersion"/> exists so such a change can be
/// migrated instead.
/// </remarks>
[Serializable]
public class PlayerSaveData
{
    public int SaveVersion = CurrentVersion;

    public int ActiveDeckIndex;

    public List<DeckSaveData> Decks = new();

    public CardSortKey SortKey = CardSortKey.Cost;

    public bool SortAscending = true;

    /// <summary>Soft currency spent on card upgrades, earned from finished matches.</summary>
    public int Gold;

    /// <summary>One entry per <b>owned</b> card. Absence is what "locked" means.</summary>
    public List<CardProgressSaveData> Cards = new();

    /// <summary>2 added <see cref="Gold"/> and <see cref="Cards"/> on top of the v1 deck-only save.</summary>
    public const int CurrentVersion = 2;
}

/// <summary>One deck slot: a display label plus its cards, in the order the player laid them out.</summary>
[Serializable]
public class DeckSaveData
{
    public string Name;

    public List<CardType> Cards = new();
}

/// <summary>How far a single owned card has progressed.</summary>
[Serializable]
public class CardProgressSaveData
{
    public CardType CardType;

    public int Level = 1;

    /// <summary>Copies banked toward the <i>next</i> level; reset to 0 on upgrade.</summary>
    public int Copies;
}

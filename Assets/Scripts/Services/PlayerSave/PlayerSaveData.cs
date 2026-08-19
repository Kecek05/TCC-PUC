using System;
using System.Collections.Generic;

/// <summary>
/// Root of the local player save. A plain <c>[Serializable]</c> POCO so <c>JsonUtility</c> can round-trip
/// it, mirroring how <see cref="UserData"/> is serialized for the connection payload.
/// </summary>
/// <remarks>
/// <c>JsonUtility</c> writes enums as their integer value, so new <see cref="CardType"/> members must keep
/// being <b>appended</b> to the end of <c>Enums.cs</c>. Inserting one in the middle silently re-maps every
/// saved deck; <see cref="SaveVersion"/> exists so such a change can be migrated instead.
/// </remarks>
[Serializable]
public class PlayerSaveData
{
    public int SaveVersion = CurrentVersion;

    public int ActiveDeckIndex;

    public List<DeckSaveData> Decks = new();

    public CardSortKey SortKey = CardSortKey.Cost;

    public bool SortAscending = true;

    public const int CurrentVersion = 1;
}

/// <summary>One deck slot: a display label plus its cards, in the order the player laid them out.</summary>
[Serializable]
public class DeckSaveData
{
    public string Name;

    public List<CardType> Cards = new();
}

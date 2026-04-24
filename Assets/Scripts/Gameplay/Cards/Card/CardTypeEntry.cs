using System;
using Unity.Netcode;

/// <summary>
/// Value-type wrapper around <see cref="CardType"/> for use with <c>NetworkList</c>.
/// NetworkList&lt;T&gt; requires T : IEquatable&lt;T&gt;, which enums don't satisfy.
/// Implicit conversions keep call sites clean — treat it as a CardType.
/// </summary>
public struct CardTypeEntry : INetworkSerializeByMemcpy, IEquatable<CardTypeEntry>
{
    public CardType Value;

    public CardTypeEntry(CardType value) { Value = value; }

    public bool Equals(CardTypeEntry other) => Value == other.Value;
    public override bool Equals(object obj) => obj is CardTypeEntry e && Equals(e);
    public override int GetHashCode() => (int)Value;

    public static implicit operator CardType(CardTypeEntry entry) => entry.Value;
    public static implicit operator CardTypeEntry(CardType value) => new CardTypeEntry(value);
}

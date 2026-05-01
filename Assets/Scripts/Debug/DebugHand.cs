using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DebugHand", menuName = "Scriptable Objects/Debug/DebugHandSO")]
public class DebugHand : ScriptableObject
{
    public List<CardType> Deck;
}

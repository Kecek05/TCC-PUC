using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Authoring data for the local player save: how many deck slots exist, where the file lives and what a
/// brand-new player starts with. Holding the two content references here (instead of on
/// <c>ClientManager</c>) keeps the bootstrap to a single serialized field, and SO-to-SO references are
/// already the project idiom (see <c>SpellCardDataSO.SpellData</c>).
/// </summary>
[CreateAssetMenu(fileName = "PlayerSaveSettings", menuName = "Scriptable Objects/Settings/PlayerSaveSettingsSO")]
public class PlayerSaveSettingsSO : ScriptableObject
{
    [Title("Deck Slots")]
    [MinValue(1)] public int DeckSlotCount = 5;

    [Tooltip("Display name for a slot. {0} is the 1-based slot number.")]
    public string DeckNameFormat = "Deck {0}";

    [Title("Storage")]
    [Tooltip("File name inside Application.persistentDataPath.")]
    public string SaveFileName = "player_save.json";

    [Title("Content References")]
    [Required, Tooltip("Source of DeckSize: how many cards a deck must hold.")]
    public CardHandSettingsSO CardHandSettings;

    [Required, Tooltip("Every card in the game. Saved CardTypes missing from this list are dropped on load.")]
    public CardDataListSO CardDataList;

    [Title("New Save")]
    [Tooltip("Seeds deck slot 1 on a brand-new save, so a fresh player can press Battle right away. " +
             "The remaining slots start empty. Should hold CardHandSettings.DeckSize cards.")]
    public List<CardType> StarterDeck = new();
}

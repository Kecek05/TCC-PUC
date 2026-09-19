using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Everything the first-time experience is authored with: the deck it puts in the player's hands, the
/// opponent it fields, how the payout is shaped, and the copy every step prints.
/// </summary>
/// <remarks>
/// One asset, referenced from <c>ClientManager</c> beside <c>PlayerSaveSettingsSO</c>. SO-to-SO references
/// for the content lists follow the same idiom that settings asset already uses, and keep the bootstrap to
/// a single serialized field.
/// </remarks>
[CreateAssetMenu(fileName = "TutorialSettings", menuName = "Scriptable Objects/Tutorial/TutorialSettingsSO")]
public class TutorialSettingsSO : ScriptableObject
{
    [Title("Match")]
    [InfoBox("Swapped in for the player's own deck for the duration of the tutorial match, then swapped " +
             "back. Scripting a step as \"place a tower\" is only safe if a tower is guaranteed to be in " +
             "the deck, and the player's real deck is theirs to edit.\n\n" +
             "Hold exactly HandSize cards: the whole deck is then always in hand, which is what brings the " +
             "tower card straight back for the level-up step and keeps both spells available for theirs. " +
             "Must contain a Tower, a troop, and the two spells the script names by type - Ice (cast on " +
             "their towers) and Fireball (cast on your own lane).")]
    public List<CardType> TutorialDeck = new();

    [Tooltip("Level every tutorial-deck card plays at. The player's own levels are irrelevant here — the " +
             "match is scripted, so it should play identically for everyone.")]
    [MinValue(1)] public int DeckCardLevel = 1;

    [Title("Content References")]
    [Required, Tooltip("Pool the reward card is rolled from. A card the player has never owned is preferred; " +
                       "only a save that owns them all (a replay) is given one it owns outside its deck.")]
    public CardDataListSO CardDataList;

    [Required, Tooltip("Supplies the cost of the reward card's next level (1 -> 2 for a new card), which is " +
                       "exactly what the reward pays out so the menu half's upgrade step is always affordable.")]
    public CardProgressionSettingsSO CardProgression;

    [Required, Tooltip("Text for every step, keyed by TutorialStepId.")]
    public TutorialCopySO Copy;

    [Title("Reward")]
    [InfoBox("The payout is always a card, shaped by the menu steps that follow it rather than rolled for " +
             "value: the player has to be able to equip the card AND buy its next level immediately, or the " +
             "tutorial dead-ends.")]
    [Tooltip("Extra copies granted on top of the exact number the first upgrade spends. A small surplus " +
             "means the progress bar does not read a discouraging 0/N the moment the upgrade lands.")]
    [MinValue(0)] public int BonusCopies = 2;

    [Tooltip("Extra gold granted on top of the exact cost of the first upgrade.")]
    [MinValue(0)] public int BonusGold = 150;

    [Title("Flow")]
    [Tooltip("Off: the Skip button is hidden and the player must play the tutorial through.")]
    public bool AllowSkip = true;

    [Title("Free Play")]
    [InfoBox("After the scripted steps the match is played out to a normal ending. A first match must not " +
             "be lost, so the player's base is floored here — any value above zero means it cannot die — and " +
             "the bot's lane is held before its last wave, so it can never win the race to clear it.")]
    [Tooltip("The lowest the player's base can be brought to during the tutorial match.")]
    [MinValue(1f)] public float MinimumBaseHealth = 1f;

    [Tooltip("Unscaled seconds a free-play tip stays up at most, and the quiet gap before another may show. " +
             "Tips suggest; if they come back too often they start to nag.")]
    [MinValue(1f)] public float TipMaxSeconds = 8f;

    [MinValue(0f)] public float TipCooldownSeconds = 10f;

    [Tooltip("How far down the player's lane an enemy has to be (0 = entered, 1 = reached the base) before " +
             "the Fireball tip speaks up.")]
    [Range(0f, 1f)] public float DefendTipProgress = 0.5f;
}

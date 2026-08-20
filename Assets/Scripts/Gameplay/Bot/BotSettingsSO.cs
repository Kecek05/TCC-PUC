using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Tuning for the fallback BOT opponent: when it seats, which decks it can use, and how its
/// decision loop paces itself. One asset lives in the GameScene, referenced by the BotController.
/// </summary>
[CreateAssetMenu(fileName = "BotSettingsSO", menuName = "Scriptable Objects/Bot/BotSettingsSO")]
public class BotSettingsSO : ScriptableObject
{
    [Title("Fallback")]
    [Tooltip("Master switch. When off, the host waits indefinitely for a human (original behaviour).")]
    public bool EnableBotFallback = true;

    [Tooltip("Seconds the host waits alone before a bot fills the empty slot and the match starts.")]
    [Min(0f)] public float FillTimeoutSeconds = 30f;

    [Tooltip("Pool of decks the bot picks from at random when it seats.")]
    [Required] public BotDeckListSO DeckList;

    [Tooltip("Display name shown to the human as the opponent.")]
    public string BotName = "Bot";

    [Tooltip("Persistent card level the bot plays every card at. Raise it to make the bot hit harder " +
             "without touching its deck or decision loop.")]
    [Min(1)] public int CardLevel = 1;

    [Title("Decision Loop")]
    [Tooltip("Base seconds between bot decisions. Lower = faster/harder.")]
    [Min(0.1f)] public float DecisionInterval = 1.0f;

    [Tooltip("Random +/- variation added to each interval, for less robotic pacing.")]
    [Min(0f)] public float DecisionJitter = 0.35f;

    [Tooltip("Mana the bot tries to keep in reserve instead of spending, so it can react to threats.")]
    [Min(0f)] public float ManaReserve = 2f;
}

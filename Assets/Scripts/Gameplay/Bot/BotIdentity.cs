using System;

/// <summary>
/// Mints synthetic identities for server-side AI bots so they can flow through the same
/// PlayersDataManager / TeamManager pipeline as real players. Real clients never get
/// ClientIds in the reserved bot range.
/// </summary>
public static class BotIdentity
{
    public const ulong BotClientIdBase = 0xB07_0000_0000_0000UL;
    private const ulong BotClientIdMax  = BotClientIdBase + 0x10000UL;

    private const string BotAuthPrefix = "bot_";

    private static ulong _nextBotIndex;

    public static string MintAuthId() => BotAuthPrefix + Guid.NewGuid().ToString("N").Substring(0, 12);

    public static ulong MintClientId() => BotClientIdBase + (_nextBotIndex++);

    public static bool IsBot(ulong clientId) => clientId >= BotClientIdBase && clientId < BotClientIdMax;

    public static bool IsBot(string authId) => !string.IsNullOrEmpty(authId) && authId.StartsWith(BotAuthPrefix);
}

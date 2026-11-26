/// <summary>
/// The name a player gets when their account has none of its own. One owner for the format, because two
/// things depend on it looking identical: <c>ClientManager</c> rolls it for a real anonymous player, and
/// the fallback bot rolls one to pass as a real opponent (a bot named "Bot" gives away that nobody came).
/// </summary>
public static class DefaultPlayerName
{
    private const string Prefix = "Player_";

    /// <summary>
    /// A fresh default name, <c>Player_6071</c>: what <c>ClientManager</c> hands Unity Authentication for an
    /// anonymous account.
    /// </summary>
    public static string Roll() => $"{Prefix}{UnityEngine.Random.Range(1000, 10000)}";

    /// <summary>
    /// A default name as other players actually see it, <c>Player_6071#14986</c>. Unity Authentication stores
    /// a player name with a <c>#</c> and a discriminator of its own appended, and that stored form is what a
    /// returning player connects with; only the very first launch goes out without it. Every discriminator
    /// seen in this project's logs has had five digits.
    /// </summary>
    public static string RollAsStored() => $"{Roll()}#{UnityEngine.Random.Range(10000, 100000)}";
}

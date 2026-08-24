using System;
using System.Collections.Generic;
using Unity.Netcode;

public struct PlayerEndGameData : INetworkSerializable, IEquatable<PlayerEndGameData>
{
    public int Wave;
    public float WaveProgress;
    public float Health;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Wave);
        serializer.SerializeValue(ref WaveProgress);
        serializer.SerializeValue(ref Health);
    }

    public bool Equals(PlayerEndGameData other) => Wave == other.Wave && Health.Equals(other.Health);
}

public struct EndGameSnapshot : INetworkSerializable,  IEquatable<EndGameSnapshot>
{
    public TeamType WinnerTeam;
    public PlayerEndGameData BluePlayer;
    public PlayerEndGameData RedPlayer;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref WinnerTeam);
        serializer.SerializeNetworkSerializable(ref BluePlayer);
        serializer.SerializeNetworkSerializable(ref RedPlayer);
    }
    
    public bool Equals(EndGameSnapshot other) =>
        WinnerTeam == other.WinnerTeam &&
        BluePlayer.Equals(other.BluePlayer) &&
        RedPlayer.Equals(other.RedPlayer);
}

public abstract class BaseServerEndGameManager : NetworkBehaviour
{
    public event Action<EndGameSnapshot> OnGameEnded;

    /// <summary>
    /// Raised on a client with <b>that client's own</b> match reward. Separate from
    /// <see cref="OnGameEnded"/> because the snapshot is a broadcast both players see, while a reward is
    /// private to one of them.
    /// </summary>
    /// <remarks>
    /// This is the match payout's <i>transport</i>, not its destination: <c>ClientRewardHandler</c> forwards
    /// it into <see cref="BaseRewardService"/>, which banks it. Subscribe there, not here, unless you
    /// specifically care that a reward came from a match.
    /// </remarks>
    public event Action<Reward> OnRewardGranted;

    protected void TriggerOnGameEnded(EndGameSnapshot snapshot)
    {
        OnGameEnded?.Invoke(snapshot);
    }

    protected void TriggerOnRewardGranted(Reward reward)
    {
        OnRewardGranted?.Invoke(reward);
    }
}

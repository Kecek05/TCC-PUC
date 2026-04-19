using System;
using System.Collections.Generic;
using Unity.Netcode;

public struct PlayerEndGameData : INetworkSerializable, IEquatable<PlayerEndGameData>
{
    public int Wave;
    public float Health;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Wave);
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
    
    protected void TriggerOnGameEnded(EndGameSnapshot snapshot)
    {
        OnGameEnded?.Invoke(snapshot);
    }
}

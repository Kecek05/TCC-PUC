using System;

public interface IOnPlayerConnected
{
    event Action<OnCardPlayerConnectedEventArgs> OnPlayerConnected;
}

public struct OnCardPlayerConnectedEventArgs
{
    public UserData UserData;
    public ulong ClientId;
}

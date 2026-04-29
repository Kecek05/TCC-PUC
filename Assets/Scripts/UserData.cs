using System;
using UnityEngine;

[Serializable]
public class UserData
{
    //need to be public for the Json payload

    public string PlayerName;
    public string PlayerAuthId;
    public int UserTrophies;

    public void SetUserTrophies(int userTrophies) => this.UserTrophies = userTrophies;

    public void SetPlayerName(string playerName) => this.PlayerName = playerName;
    
    public void SetPlayerAuthId(string playerAuthId) => this.PlayerAuthId = playerAuthId;

    public GameInfo userGamePreferences = new();
}

[Serializable]
public class GameInfo
{

    public Arena arena;
    public GameMode gameMode;
    public GameQueue gameQueue;

    public string ToMultiplayQueue()
    {
        return gameQueue switch
        {
            GameQueue.Ranked => "solo-ranked-queue",
            GameQueue.UnRanked => "solo-unranked-queue",
            _ => "solo-ranked-queue",
        };
    }
}
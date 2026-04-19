using System;
using UnityEngine;

[Serializable]
public class UserData
{
    //need to be public for the Json payload

    public string playerName;
    public string playerAuthId;

    public int userTrophies; 

    public void SetUserTrophies(int userTrophies) => this.userTrophies = userTrophies;

    public void SetPlayerName(string playerName) => this.playerName = playerName;

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
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UserData
{
    //need to be public for the Json payload

    public string PlayerName;
    public string PlayerAuthId;
    public int UserTrophies;
    public List<CardType> DeckCards;

    public void SetUserTrophies(int userTrophies) => this.UserTrophies = userTrophies;

    public void SetPlayerName(string playerName) => this.PlayerName = playerName;
    
    public void SetPlayerAuthId(string playerAuthId) => this.PlayerAuthId = playerAuthId;
    
    public void SetDeckCards(List<CardType> deckCards) => this.DeckCards = deckCards;

    public GameInfo userGamePreferences = new();

    public byte[] TranslateToBytes()
    {
        string payload = JsonUtility.ToJson(this); //serialize the payload to json
        Debug.Log("TranslateToBytes: " + payload);
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload); // serialize the payload to bytes
        return payloadBytes;
    }
    
    public static UserData TranslateFromBytes(byte[] payloadBytes)
    {
        string payload = System.Text.Encoding.UTF8.GetString(payloadBytes);
        UserData userData = JsonUtility.FromJson<UserData>(payload);
        Debug.Log("TranslateFromBytes: " + payload);
        return userData;
    }
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
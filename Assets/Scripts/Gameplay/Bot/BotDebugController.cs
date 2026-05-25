using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hand-driven test harness for spinning up a bot opponent during play mode.
/// Use the "Spawn Bot" button while running as host or dedicated server.
/// Wires the bot into PlayersDataManager + TeamManager and starts its tick loop.
/// Not part of any production lifecycle — kept around for tuning iterations.
/// </summary>
public class BotDebugController : MonoBehaviour
{
    [Title("Bot Setup")]
    [SerializeField, Required] private BotController bot;
    [SerializeField] private TeamType team = TeamType.Red;
    [SerializeField] private string botPlayerName = "BotPlayer";
    [SerializeField] private List<CardType> deck = new();

    [Button(ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.4f)]
    public void SpawnBot()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            GameLog.Error("[BotDebugController] SpawnBot must be invoked on the server.");
            return;
        }
        if (bot == null)
        {
            GameLog.Error("[BotDebugController] Bot reference is null.");
            return;
        }
        if (deck == null || deck.Count == 0)
        {
            GameLog.Error("[BotDebugController] Deck is empty — cannot bootstrap hand.");
            return;
        }

        string authId = BotIdentity.MintAuthId();
        ulong clientId = BotIdentity.MintClientId();

        BasePlayersDataManager pdm = ServiceLocator.Get<BasePlayersDataManager>();
        UserData userData = new UserData
        {
            PlayerName = botPlayerName,
            PlayerAuthId = authId,
            UserTrophies = 0,
            DeckCards = deck,
        };
        pdm.RegisterClient(new PlayerData { UserData = userData, ClientId = clientId });

        BaseTeamManager tm = ServiceLocator.Get<BaseTeamManager>();
        if (tm == null)
        {
            GameLog.Error("[BotDebugController] TeamManager not registered.");
            return;
        }
        tm.AssignTeam(authId);

        if (tm.GetTeam(authId) != team)
        {
            GameLog.Warn($"[BotDebugController] Requested team {team} but TeamManager assigned {tm.GetTeam(authId)} (other team likely free first).");
        }

        bot.InitializeAsBot(tm.GetTeam(authId), authId, clientId, deck);
    }
}

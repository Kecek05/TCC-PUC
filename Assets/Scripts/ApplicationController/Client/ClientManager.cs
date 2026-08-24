using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientManager : BaseClientManager
{
    private NetworkClient networkClient;
    private bool _isLeavingMatch;

    [Title("Player Save")]
    [SerializeField] private PlayerSaveSettingsSO playerSaveSettings;

    [Title("Debug")]
    [SerializeField] private bool useDebugHand = false;
    [SerializeField] private DebugHand debugHand;

    private BasePlayerSaveManager _playerSaveManager;
    private BaseRewardService _rewardService;

    private void Awake()
    {
        ServiceLocator.Register<BaseClientManager>(this);
        DontDestroyOnLoad(gameObject);

        UserData = new UserData();

        InitializePlayerSave();
        InitializeRewards();

        ClientAuth = new ClientAuth();
        //TODO: Refactor the creation of NetworkClient too.
        networkClient = new NetworkClient(NetworkManager.Singleton, this);

        DoAuth();
    }

    /// <summary>
    /// Hydrates the persistent player save before the Main Menu loads, since the deck page reads it in
    /// <c>Start</c>. <see cref="UserData.DeckCards"/> is only ever a mirror of the active deck slot, so the
    /// save manager stays the single owner of deck state.
    /// </summary>
    private void InitializePlayerSave()
    {
        _playerSaveManager = new PlayerSaveManager(
            playerSaveSettings,
            new FilePlayerSaveRepository(playerSaveSettings != null ? playerSaveSettings.SaveFileName : null));

        ServiceLocator.Register<BasePlayerSaveManager>(_playerSaveManager);

        _playerSaveManager.OnActiveDeckContentChanged += HandleActiveDeckContentChanged;
        _playerSaveManager.Load();
    }

    /// <summary>
    /// Registers the one place a reward can be banked, reachable from every scene. It depends on the save,
    /// so it is created right after <see cref="InitializePlayerSave"/> and lives exactly as long: a daily
    /// claim or a shop purchase in the Main Menu grants through this, the same as the end of a match does.
    /// </summary>
    private void InitializeRewards()
    {
        _rewardService = new RewardService(_playerSaveManager);
        ServiceLocator.Register<BaseRewardService>(_rewardService);
    }

    private void HandleActiveDeckContentChanged(DeckSaveData deck)
    {
        // Copy, never alias: the connection payload must not be able to mutate the save.
        UserData.SetDeckCards(new List<CardType>(deck.Cards));

        // Levels travel index-aligned with the cards, so the server can scale what this player deploys.
        List<int> levels = new(deck.Cards.Count);
        foreach (CardType cardType in deck.Cards) levels.Add(_playerSaveManager.GetCardLevel(cardType));
        UserData.SetDeckCardLevels(levels);
    }

    private async void DoAuth()
    {
        try
        {
            if (await ClientAuth.TryInitAsync())
            {
                string playerName = AuthenticationService.Instance.PlayerName;

                // Anonymous accounts have no name — fall back to a generated one and
                // persist it to Authentication so it becomes the player's real name.
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = $"Player_{UnityEngine.Random.Range(1000, 10000)}";
                    try
                    {
                        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
                    }
                    catch (Exception e)
                    {
                        // Keep the generated name locally even if the remote update fails.
                        GameLog.Exception(e);
                    }
                }

                UserData.SetPlayerName(playerName);
                UserData.SetPlayerAuthId(AuthenticationService.Instance.PlayerId);
                UserData.SetUserTrophies(UnityEngine.Random.Range(0, 1000)); //Temp
                if (useDebugHand && debugHand != null)
                {
                    // Push through the save manager so the deck page and the match can never disagree.
                    _playerSaveManager.SetDeckCards(_playerSaveManager.ActiveDeckIndex, debugHand.Deck);
                }

                GameLog.Info($"Player authenticated. PlayerId: {AuthenticationService.Instance.PlayerId}, PlayerName: {playerName}");
                Loader.Load(Loader.Scene.MainMenu);
            }
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            return;
        }
    }

    public override async Task<bool> JoinHost(string joinCode)
    {
        return await networkClient.JoinRelay(joinCode);
    }

    public override void ConnectClient()
    {
        networkClient.ConnectClient(UserData);
    }

    public override void DisconnectClient()
    {
        _ = LeaveMatchAsync();
    }

    public override async Task LeaveMatchAsync()
    {
        if (_isLeavingMatch) return;
        _isLeavingMatch = true;

        try
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            BaseHostManager hostManager = ServiceLocator.Get<BaseHostManager>();

            if (networkManager != null && networkManager.IsHost && hostManager != null)
            {
                // Host owns lobby/relay cleanup + NetworkManager shutdown.
                await hostManager.ShutdownHostAsync();
            }
            else
            {
                await networkClient.ShutdownAsync();
            }

            if (SceneManager.GetActiveScene().name != Loader.Scene.MainMenu.ToString())
            {
                Loader.Load(Loader.Scene.MainMenu);
            }
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
        }
        finally
        {
            _isLeavingMatch = false;
        }
    }

    private void OnDestroy()
    {
        if (_playerSaveManager != null)
            _playerSaveManager.OnActiveDeckContentChanged -= HandleActiveDeckContentChanged;

        ClientAuth?.Dispose();
        networkClient?.Dispose();
        ServiceLocator.Unregister<BaseRewardService>();
        ServiceLocator.Unregister<BasePlayerSaveManager>();
        ServiceLocator.Unregister<BaseClientManager>();
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// Server-side host for the game-flow FSM. The FSM owns all transition logic;
/// this manager only ticks it and mirrors the active <see cref="GameState"/>
/// onto <see cref="BaseGameFlowManager.CurrentGameState"/> so existing readers
/// (UI, ServerWaveManager, towers, etc.) keep working unchanged.
/// </summary>
public class GameFlowManager : BaseGameFlowManager
{
    [SerializeField] private DebugSettingsSO _debugSettings;

    private GameFlowFsm _fsm;

    private void Awake()
    {
        ServiceLocator.Register<BaseGameFlowManager>(this);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        CurrentGameState.Value = GameState.WaitingForPlayers;
        StartCoroutine(WaitForReadyThenStart());
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (_fsm != null)
            _fsm.OnStateChanged -= PublishState;
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseGameFlowManager>();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsServer) return;
        _fsm?.Tick();
    }

    private IEnumerator WaitForReadyThenStart()
    {
        yield return new WaitUntil(() =>
            ServiceLocator.Get<BaseTeamManager>() != null &&
            ServiceLocator.Get<BaseMapTranslator>() != null &&
            ServiceLocator.Get<BaseServerEndGameManager>() != null);

        GameFlowContext ctx = new GameFlowContext
        {
            TeamManager = ServiceLocator.Get<BaseTeamManager>(),
            MapTranslator = ServiceLocator.Get<BaseMapTranslator>(),
            EndGameManager = ServiceLocator.Get<BaseServerEndGameManager>(),
        };

        _fsm = new GameFlowFsm(ctx,
            new WaitingForPlayersState(),
            new LoadingMatchState(),
            new MatchReadyState(),
            new InMatchState(),
            new EndMatchState(),
            new DrawingCardsState());

        _fsm.OnStateChanged += PublishState;
        _fsm.Start(GameState.WaitingForPlayers);
    }

    private void PublishState(GameState state)
    {
        CurrentGameState.Value = state;
        GameLog.Info($"GameFlowManager: Game state changed to {state}");
    }
}
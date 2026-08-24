using UnityEngine;

/// <summary>
/// Bridges the match payout into the shared reward pipeline: <see cref="ServerEndGameManager"/> delivers
/// this client's reward on a targeted Rpc, and this hands it to <see cref="BaseRewardService"/>, which banks
/// it and announces it.
/// </summary>
/// <remarks>
/// It is an adapter, not the destination — menu-side sources (a daily claim, a shop purchase) call the
/// service directly and never come through here. Lives in GameScene, the only scene the Rpc can arrive in.
/// Harmless on a dedicated server or in a debug scene: with no service registered it simply does nothing.
/// </remarks>
public class ClientRewardHandler : MonoBehaviour
{
    private BaseServerEndGameManager _endGameManager;
    private BaseRewardService _rewardService;

    private void Start()
    {
        if (!ServiceLocator.TryGet(out _endGameManager))
        {
            GameLog.Warn($"[{nameof(ClientRewardHandler)}] No end-game manager; match rewards will not be banked.");
            return;
        }

        // A dedicated server has no local save or reward service, and that is fine: it never receives the Rpc.
        ServiceLocator.TryGet(out _rewardService);

        _endGameManager.OnRewardGranted += HandleMatchReward;
    }

    private void OnDestroy()
    {
        if (_endGameManager != null) _endGameManager.OnRewardGranted -= HandleMatchReward;
    }

    private void HandleMatchReward(Reward reward)
    {
        if (_rewardService == null)
        {
            GameLog.Warn($"[{nameof(ClientRewardHandler)}] Received {reward} but there is no reward service to bank it.");
            return;
        }

        _rewardService.Grant(reward);
    }
}

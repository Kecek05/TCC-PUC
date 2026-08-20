using UnityEngine;

/// <summary>
/// Banks the match reward the server sent this client into the local player save. Deliberately separate
/// from <see cref="ClientEndGameCanvas"/>: the canvas subscribes to the same event to <i>show</i> the
/// reward, while writing the save stays out of the UI layer entirely.
/// </summary>
/// <remarks>
/// Lives in GameScene. Harmless on a dedicated server or in debug scenes — with no
/// <see cref="BasePlayerSaveManager"/> registered it simply does nothing.
/// </remarks>
public class ClientRewardHandler : MonoBehaviour
{
    private BaseServerEndGameManager _endGameManager;
    private BasePlayerSaveManager _playerSaveManager;

    private void Start()
    {
        if (!ServiceLocator.TryGet(out _endGameManager))
        {
            GameLog.Warn($"[{nameof(ClientRewardHandler)}] No end-game manager; rewards will not be banked.");
            return;
        }

        // A dedicated server has no local player save, and that is fine: it never receives the Rpc either.
        ServiceLocator.TryGet(out _playerSaveManager);

        _endGameManager.OnRewardGranted += HandleRewardGranted;
    }

    private void OnDestroy()
    {
        if (_endGameManager != null) _endGameManager.OnRewardGranted -= HandleRewardGranted;
    }

    private void HandleRewardGranted(MatchReward reward)
    {
        if (_playerSaveManager == null)
        {
            GameLog.Warn($"[{nameof(ClientRewardHandler)}] Received {reward} but there is no player save to bank it in.");
            return;
        }

        _playerSaveManager.GrantReward(reward);
    }
}

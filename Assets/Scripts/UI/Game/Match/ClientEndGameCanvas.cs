using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ClientEndGameCanvas : NetworkBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject rootCanvas;
    [SerializeField] private GameObject victoryText;
    [SerializeField] private GameObject defeatText;

    private BaseServerEndGameManager _endGameManager;

    private void Awake()
    {
        rootCanvas.SetActive(false);
        victoryText.SetActive(false);
        defeatText.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsClient)
        {
            enabled = false;
            return;
        }

        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        _endGameManager.WinnerTeam.OnValueChanged += ServerEndGameManager_OnWinnerTeamValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient && _endGameManager != null)
            _endGameManager.WinnerTeam.OnValueChanged -= ServerEndGameManager_OnWinnerTeamValueChanged;
    }

    private void ServerEndGameManager_OnWinnerTeamValueChanged(TeamType previousValue, TeamType winnerTeam)
    {
        if (winnerTeam == TeamType.None) return;

        rootCanvas.SetActive(true);

        if (winnerTeam == ServiceLocator.Get<BaseTeamManager>().GetLocalTeam())
            victoryText.SetActive(true);
        else
            defeatText.SetActive(true);

    }
}

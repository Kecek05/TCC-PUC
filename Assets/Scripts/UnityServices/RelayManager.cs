using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

public class RelayManager : MonoBehaviour
{
    [Header("Host")]
    [SerializeField] private Button createRelayButton;

    [Header("Join")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI joinCodeText;

    private void Start()
    {
        createRelayButton.onClick.AddListener(CreateRelay);
        joinButton.onClick.AddListener(JoinRelay);
    }

    private async void CreateRelay()
    {
        createRelayButton.interactable = false;

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            GameLog.Info($"Relay created. Join code: {joinCode}");
            joinCodeText.text = joinCode;

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(allocation.ToRelayServerData("dtls"));
            NetworkManager.Singleton.StartHost();

            gameObject.SetActive(false);
        }
        catch (RelayServiceException e)
        {
            GameLog.Error($"Failed to create relay: {e.Message}");
            createRelayButton.interactable = true;
        }
    }

    private async void JoinRelay()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        joinButton.interactable = false;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));
            NetworkManager.Singleton.StartClient();

            gameObject.SetActive(false);
        }
        catch (RelayServiceException e)
        {
            GameLog.Error($"Failed to join relay: {e.Message}");
            joinButton.interactable = true;
        }
    }
}
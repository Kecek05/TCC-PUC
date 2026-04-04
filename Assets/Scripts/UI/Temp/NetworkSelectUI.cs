using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkSelectUI : MonoBehaviour
{
    [SerializeField] private Button serverButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button hostButton;

    private void Start()
    {
        serverButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
            gameObject.SetActive(false);
        });

        clientButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            gameObject.SetActive(false);
        });
        
        hostButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            gameObject.SetActive(false);
        });
    }
}

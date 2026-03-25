using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkMapPosition : NetworkBehaviour
{
    [SerializeField] private EntityTeam entityTeam;
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            StartCoroutine(ApplyLocalPosition());
    }

    private IEnumerator ApplyLocalPosition()
    {
        Debug.Log("GameObject: " + gameObject.name);
        Debug.Log("Server Position: " + transform.position);
        Debug.Log(transform.position);
        
        yield return new WaitUntil(() =>
            MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized);
        transform.position = MapTranslator.Instance.ServerToLocal(transform.position, entityTeam.GetTeamType());
        
        Debug.Log("Changed to Local: " + transform.position);
        
    }
}
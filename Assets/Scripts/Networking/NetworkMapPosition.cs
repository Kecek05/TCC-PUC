using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkMapPosition : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            StartCoroutine(ApplyLocalPosition());
    }

    private IEnumerator ApplyLocalPosition()
    {
        Debug.Log("Applying local position");
        Debug.Log(transform.position);
        
        yield return new WaitUntil(() =>
            MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized);
        transform.position = MapTranslator.Instance.ServerToLocal(transform.position);
        
        Debug.Log("Applied local position");
        Debug.Log(transform.position);
        
    }
}
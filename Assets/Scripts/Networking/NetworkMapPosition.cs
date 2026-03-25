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
        yield return new WaitUntil(() =>
            MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized);

        transform.position = MapTranslator.Instance.ServerToLocal(transform.position);
    }
}
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkMapPosition : NetworkBehaviour
{
    [SerializeField] private EntityTeam entityTeam;
    public override void OnNetworkSpawn()
    {
        if (IsClient)
            StartCoroutine(ApplyLocalPosition());
    }

    private IEnumerator ApplyLocalPosition()
    {
        BaseMapTranslator mapTranslator = ServiceLocator.Get<BaseMapTranslator>();

        yield return new WaitUntil(() =>
            mapTranslator != null && mapTranslator.IsInitialized);
        transform.position = mapTranslator.ServerToLocal(transform.position, entityTeam.GetTeamType());
    }
}
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MapTranslator : MonoBehaviour
{
    public static MapTranslator Instance { get; private set; }

    [SerializeField] private float mapOffset = 10f;

    public bool IsInitialized { get; private set; }

    private bool _needsTranslation;

    private void Awake() => Instance = this;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsConnectedClient));

        // Dedicated server never translates
        if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            IsInitialized = true;
            yield break;
        }

        yield return new WaitUntil(() =>
            TeamManager.Instance != null &&
            TeamManager.Instance.HasLocalTeamBeenAssigned());

        _needsTranslation = TeamManager.Instance.GetLocalTeam() == TeamType.Red;

        if (_needsTranslation)
            RepositionSceneObjects();

        IsInitialized = true;
    }

    public Vector2 LocalToServer(Vector2 localPos)
    {
        if (!_needsTranslation) return localPos;
        return new Vector2(localPos.x, mapOffset - localPos.y);
    }

    public Vector2 ServerToLocal(Vector2 serverPos)
    {
        if (!_needsTranslation) return serverPos;
        return new Vector2(serverPos.x, mapOffset - serverPos.y);
    }

    public Vector3 LocalToServer(Vector3 localPos)
    {
        if (!_needsTranslation) return localPos;
        return new Vector3(localPos.x, mapOffset - localPos.y, localPos.z);
    }

    public Vector3 ServerToLocal(Vector3 serverPos)
    {
        if (!_needsTranslation) return serverPos;
        return new Vector3(serverPos.x, mapOffset - serverPos.y, serverPos.z);
    }

    private void RepositionSceneObjects()
    {
        TeamIdentifier[] teamIdentifiers = FindObjectsByType<TeamIdentifier>(FindObjectsSortMode.None);
        foreach (TeamIdentifier identifier in teamIdentifiers)
        {
            Vector2 pos = identifier.transform.position;
            identifier.transform.position = new Vector2(pos.x, mapOffset - pos.y);
        }
    }
}
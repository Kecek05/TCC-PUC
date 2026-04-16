using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Pools enemy NetworkObjects to avoid Instantiate/Destroy overhead on mobile.
/// Uses NGO's INetworkPrefabInstanceHandler so the network layer recycles
/// GameObjects instead of creating/destroying them.
///
/// Setup: Call RegisterPrefab() for each enemy prefab during initialization
/// (e.g. from ServerWaveManager.OnNetworkSpawn).
/// </summary>
public class EnemyNetworkPool : BaseEnemyNetworkPool
{
    [SerializeField] private int initialPoolSizePerPrefab = 10;

    private readonly Dictionary<uint, PooledPrefabHandler> _handlers = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseEnemyNetworkPool>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<BaseEnemyNetworkPool>();
    }

    /// <summary>
    /// Registers an enemy prefab for pooling. Call once per prefab type.
    /// Must be called before any Spawn() calls for that prefab.
    /// </summary>
    public override void RegisterPrefab(GameObject prefab)
    {
        var networkObject = prefab.GetComponent<NetworkObject>();
        if (networkObject == null) return;

        uint prefabHash = networkObject.PrefabIdHash;
        if (_handlers.ContainsKey(prefabHash)) return;

        var handler = new PooledPrefabHandler(prefab, initialPoolSizePerPrefab, transform);
        _handlers[prefabHash] = handler;

        NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, handler);
    }

    /// <summary>
    /// Per-prefab handler that manages a pool of instances for one specific prefab type.
    /// NGO calls Instantiate/Destroy on this handler when spawning/despawning that prefab.
    /// </summary>
    private class PooledPrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _prefab;
        private readonly Queue<NetworkObject> _pool = new();
        private readonly Transform _poolParent;

        public PooledPrefabHandler(GameObject prefab, int prewarmCount, Transform parent)
        {
            _prefab = prefab;
            _poolParent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                var instance = Object.Instantiate(prefab, parent);
                instance.SetActive(false);
                _pool.Enqueue(instance.GetComponent<NetworkObject>());
            }
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            NetworkObject instance;

            if (_pool.Count > 0)
            {
                instance = _pool.Dequeue();
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.gameObject.SetActive(true);
            }
            else
            {
                // Pool exhausted — create a new instance
                var obj = Object.Instantiate(_prefab, position, rotation, _poolParent);
                instance = obj.GetComponent<NetworkObject>();
            }

            return instance;
        }

        public void Destroy(NetworkObject networkObject)
        {
            networkObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            networkObject.gameObject.SetActive(false);
            _pool.Enqueue(networkObject);
        }
    }
}

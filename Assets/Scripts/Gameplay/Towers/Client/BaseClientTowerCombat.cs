using System;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseClientTowerCombat : NetworkBehaviour
{
    public event Action OnBulletFired;
    public event Action<int> OnTowerLevelChanged;
    public event Action<bool> OnFrozenChanged;
    public event Action<bool> OnHasteChanged;
    
    [Title("References")]
    [SerializeField] protected BaseServerTowerCombat serverTowerCombat;

    [Title("Bullet")]
    [Tooltip("Which pooled bullet visual this tower fires. It lives on the PREFAB, so a new tower gets its " +
             "own projectile by authoring rather than by editing a combat script. Must have a matching " +
             "entry in the scene's CosmeticBulletPool, and the bullet prefab's own bulletCardType must " +
             "agree with it or the bullet is returned to the wrong queue.")]
    [SerializeField] private CardType bulletCardType = CardType.None;

    // Runtime cache (not serialized); protected so concrete combats (e.g. ClientSlamTowerCombat) reuse it
    // instead of shadowing it with a same-named field, which Unity rejects as a duplicate serialized name.
    protected TowerManager _towerManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        serverTowerCombat.TowerLevel.OnValueChanged += OnTowerLevelValueChanged;
        OnTowerLevelValueChanged(0, serverTowerCombat.TowerLevel.Value);

        serverTowerCombat.IsFrozen.OnValueChanged += OnIsFrozenChanged;
        OnIsFrozenChanged(false, serverTowerCombat.IsFrozen.Value);

        serverTowerCombat.IsHasted.OnValueChanged += OnHastedChanged;
        OnHastedChanged(false, serverTowerCombat.IsHasted.Value);

        // Client-visible towers self-register so the shared range indicator can resolve taps.
        _towerManager = GetComponent<TowerManager>();
        ClientTowerRegistry.Register(_towerManager);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        ClientTowerRegistry.Unregister(_towerManager);

        if (serverTowerCombat == null) return;

        serverTowerCombat.TowerLevel.OnValueChanged -= OnTowerLevelValueChanged;
        serverTowerCombat.IsFrozen.OnValueChanged -= OnIsFrozenChanged;
        serverTowerCombat.IsHasted.OnValueChanged -= OnHastedChanged;
    }
    
    protected void TriggerOnBulletFired() => OnBulletFired?.Invoke();

    /// <summary>Which pooled bullet visual this tower fires, as authored on its prefab.</summary>
    public CardType BulletCardType => bulletCardType;

    /// <summary>
    /// Client-only. Takes THIS tower's own bullet visual out of the shared pool. Every concrete combat goes
    /// through here, which is what stops a tower cloned from another one from firing the original's
    /// projectile. Returns null (and says why) rather than throwing, because a missing bullet is a cosmetic
    /// problem and must never break the shot itself.
    /// </summary>
    protected CosmeticBullet GetPooledBullet()
    {
        if (CosmeticBulletPool.Instance == null) return null;

        if (bulletCardType == CardType.None)
        {
            GameLog.Warn($"{name}: bulletCardType is None, so this tower fires no bullet visual. " +
                         "Set it on the tower prefab.");
            return null;
        }

        return CosmeticBulletPool.Instance.Get(bulletCardType);
    }

    protected virtual void OnTowerLevelValueChanged(int previousValue, int newValue)
    {
        OnTowerLevelChanged?.Invoke(newValue);
    }

    protected virtual void OnIsFrozenChanged(bool previousValue, bool newValue)
    {
        OnFrozenChanged?.Invoke(newValue);
    }

    protected virtual void OnHastedChanged(bool previousValue, bool newValue)
    {
        OnHasteChanged?.Invoke(newValue);
    }
}

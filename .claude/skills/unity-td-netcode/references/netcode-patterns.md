# Netcode for GameObjects — Deep-Dive Patterns

This reference covers advanced Netcode for GameObjects (NGO) patterns specific to a real-time PvP
tower defense card game on mobile. **NGO v2.6.0** on Unity 6000.3.x.

For connection/relay/lobby/matchmaking, see `references/unity-gaming-services.md` — that covers
the unified **Multiplayer Services SDK** (`com.unity.services.multiplayer`) which replaces the
standalone Relay and Lobby packages.

---

## Table of Contents

1. [Connection Flow (Sessions API)](#connection-flow)
2. [NetworkVariable Patterns](#networkvariable-patterns)
3. [AnticipatedNetworkVariable (Client Prediction)](#anticipated-netvars)
4. [NetworkVariable Update Traits (Throttling)](#update-traits)
5. [RPC Patterns & Validation](#rpc-patterns)
6. [Spawning & Despawning](#spawning--despawning)
7. [Object Pooling with Netcode](#object-pooling)
8. [Ownership & Permission](#ownership--permission)
9. [State Synchronization Strategy](#state-sync-strategy)
10. [Network Tickrate & Update Loops](#tickrate)
11. [NetworkTransform — NGO 2.x Interpolation](#networktransform-interpolation)
12. [Late Join / Reconnection](#late-join)
13. [SinglePlayerTransport (Solo Mode)](#singleplayer-transport)
14. [Testing Multiplayer Locally](#testing)

---

## 1. Connection Flow (Sessions API) {#connection-flow}

**Use the Multiplayer Services SDK** — see `references/unity-gaming-services.md` for the full
guide. The standalone Relay and Lobby packages are deprecated in Unity 6.

### Quick Summary

```csharp
using Unity.Services.Multiplayer;

// HOST: one line creates lobby + relay + starts host
var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
var session = await MultiplayerService.Instance.CreateSessionAsync(options);
string joinCode = session.Code; // share this with opponent

// CLIENT: one line joins relay + starts client
await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
```

The SDK handles relay allocation, transport configuration, lobby heartbeats, and
`NetworkManager.StartHost()` / `StartClient()` automatically. You focus on game logic.

### Legacy Relay API (still works, but not recommended)

If you need low-level control (custom transport config, non-standard topologies), you can
still use `RelayService.Instance.CreateAllocationAsync(...)` directly with
`AllocationUtils.ToRelayServerData(allocation, "dtls")`. But for a standard 1v1 PvP game,
the Sessions API is simpler and handles more edge cases.

---

## 2. NetworkVariable Patterns {#networkvariable-patterns}

### When to Use NetworkVariable vs RPC

| Use case | Mechanism | Why |
|---|---|---|
| Persistent state (health, elixir, score) | `NetworkVariable` | Auto-syncs to late joiners; dirty-flag = bandwidth efficient |
| One-shot event (play card, deal damage) | `ServerRpc` → logic → `ClientRpc` | No state to persist; just trigger + result |
| Frequently changing value (unit position) | `NetworkTransform` or custom `NetworkVariable` | Built-in interpolation; or custom with tick-based updates |

### Custom NetworkVariable for Complex State

For card hand state (each player's hand is private to them):

```csharp
// Server tracks full hand; sends only to the owning client via targeted ClientRpc
public class HandManager : NetworkBehaviour
{
    // Server-side only — not synced
    private readonly Dictionary<ulong, List<CardId>> _serverHands = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        _serverHands[clientId] = DeckManager.Instance.DrawInitialHand(clientId);
        SendHandToClientRpc(SerializeHand(_serverHands[clientId]),
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            });
    }

    [ClientRpc]
    private void SendHandToClientRpc(CardId[] hand, ClientRpcParams rpcParams = default)
    {
        // Client updates its local hand UI
        HandUI.Instance.DisplayHand(hand);
    }
}
```

### NetworkVariable with Custom Serialization

For complex structs that need to sync (e.g., tower state):

```csharp
public struct TowerState : INetworkSerializable, IEquatable<TowerState>
{
    public int Health;
    public int Level;
    public float AttackCooldown;
    public ulong TargetId; // NetworkObjectId of current target

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Level);
        serializer.SerializeValue(ref AttackCooldown);
        serializer.SerializeValue(ref TargetId);
    }

    public bool Equals(TowerState other) =>
        Health == other.Health && Level == other.Level &&
        Mathf.Approximately(AttackCooldown, other.AttackCooldown) &&
        TargetId == other.TargetId;
}
```

### NetworkList for Dynamic Collections

For syncing a list of active tower IDs or buff stacks:

```csharp
private NetworkList<ulong> _activeTowerIds;

private void Awake()
{
    // Must initialize in Awake, not field initializer
    _activeTowerIds = new NetworkList<ulong>();
}

public override void OnNetworkSpawn()
{
    _activeTowerIds.OnListEvent += OnTowerListChanged;
}

private void OnTowerListChanged(NetworkListEvent<ulong> changeEvent)
{
    // React to Add, Remove, Insert, Value, Clear events
    switch (changeEvent.Type)
    {
        case NetworkListEvent<ulong>.EventType.Add:
            // Update UI to show new tower
            break;
        case NetworkListEvent<ulong>.EventType.Remove:
            // Update UI to remove tower
            break;
    }
}
```

---

## 3. AnticipatedNetworkVariable — Client Prediction (NGO 2.0-pre.2+) {#anticipated-netvars}

For a fast-paced card game, the delay between "I played a card" and "the server confirms and
the tower appears" feels sluggish on mobile (150ms+ round-trip). `AnticipatedNetworkVariable<T>`
lets the client anticipate the outcome immediately while the server validates.

### Elixir Anticipation Example

The elixir bar should decrease instantly when the player deploys a card, not after the server
round-trip:

```csharp
using Unity.Netcode;

public class ElixirDisplay : NetworkBehaviour
{
    // Server-authoritative, but client can anticipate locally
    private AnticipatedNetworkVariable<float> _elixir = new();

    public override void OnNetworkSpawn()
    {
        _elixir.OnAuthoritativeValueChanged += OnServerElixirChanged;
    }

    // Client-side: immediately show the elixir cost deduction
    public void AnticipateCardCost(float cost)
    {
        if (!IsServer)
        {
            _elixir.Anticipate(_elixir.Value - cost);
            // UI updates instantly — smooth feel
        }
    }

    private void OnServerElixirChanged(float previous, float authoritative)
    {
        // Server corrected the value — snap or smooth to the real amount
        // If prediction was correct, this is a no-op visually
    }
}
```

**When to use in this game:**
- Elixir bar: anticipate cost deduction on card play.
- Health bars: anticipate damage when a local projectile hits (if client-predicted).
- Card hand: anticipate card removal on play.

**When NOT to use:** Don't anticipate opponent state. Only anticipate your own player's actions.

### AnticipatedNetworkTransform

For client-predicted unit movement (e.g., a unit you just deployed should start walking
immediately, not wait for server confirmation):

```csharp
// Add AnticipatedNetworkTransform instead of NetworkTransform to the unit prefab
// It handles prediction + reconciliation with server state automatically
```

---

## 4. NetworkVariable Update Traits — Throttling (NGO 2.0-pre.2+) {#update-traits}

`NetworkVariableUpdateTraits` let you throttle how often a `NetworkVariable` sends updates.
Critical for mobile bandwidth savings.

```csharp
private NetworkVariable<float> _elixir = new(
    writePerm: NetworkVariableWritePermission.Server
)
{
    // Don't send updates more than 4 times per second
    UpdateTraits = new NetworkVariableUpdateTraits
    {
        MinSecondsBetweenUpdates = 0.25f,
        MaxSecondsBetweenUpdates = 1.0f, // force send at least once per second
    },
    // Only send if value changed by more than 0.05 (prevents micro-updates)
    CheckExceedsDirtinessThreshold = (prev, curr) => Mathf.Abs(curr - prev) > 0.05f
};
```

### Recommended Traits for This Game

| Variable | MinSeconds | MaxSeconds | Dirtiness Threshold |
|---|---|---|---|
| Elixir (float) | 0.25 | 1.0 | 0.05 |
| Health (int) | 0 (send immediately) | — | — |
| Match timer (float) | 0.5 | 1.0 | 0.5 |
| Score (int) | 0 (send immediately) | — | — |
| Tower target (ulong) | 0 (send immediately) | — | — |

Health and score should send immediately (they're important game events). Elixir and timer
can tolerate throttling — the UI interpolates between updates anyway.

### CheckDirtyState for Collections (NGO 2.0+)

If you use a `List<T>`, `Dictionary<K,V>`, or `HashSet<T>` inside a `NetworkVariable`,
call `CheckDirtyState()` after modifying items to trigger change detection:

```csharp
private NetworkVariable<List<string>> _activeBuffs = new();

public void AddBuff(string buffId)
{
    _activeBuffs.Value.Add(buffId);
    _activeBuffs.CheckDirtyState(); // tells NGO the list changed
}
```

---

## 5. RPC Patterns & Validation {#rpc-patterns}

### Card Play — Full Flow

```csharp
// Client requests to play a card
[ServerRpc]
private void PlayCardServerRpc(int handIndex, Vector2 position, ServerRpcParams rpcParams = default)
{
    ulong clientId = rpcParams.Receive.SenderClientId;

    // 1. Validate hand index
    if (handIndex < 0 || handIndex >= _serverHands[clientId].Count)
    {
        RejectCardPlayClientRpc("Invalid card index", TargetClient(clientId));
        return;
    }

    CardData card = CardDatabase.Get(_serverHands[clientId][handIndex]);

    // 2. Validate elixir cost
    if (!ElixirManager.Instance.TrySpend(clientId, card.ElixirCost))
    {
        RejectCardPlayClientRpc("Not enough elixir", TargetClient(clientId));
        return;
    }

    // 3. Validate placement position (in player's territory)
    if (!BattlefieldManager.Instance.IsValidPlacement(clientId, position, card))
    {
        ElixirManager.Instance.Refund(clientId, card.ElixirCost);
        RejectCardPlayClientRpc("Invalid placement", TargetClient(clientId));
        return;
    }

    // 4. Execute: spawn tower/unit, remove card from hand, draw replacement
    SpawnCardEntity(card, position, clientId);
    _serverHands[clientId].RemoveAt(handIndex);
    DrawAndSendCard(clientId);

    // 5. Notify all clients for VFX
    CardPlayedClientRpc(clientId, card.Id, position);
}

private ClientRpcParams TargetClient(ulong clientId) => new()
{
    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
};
```

### Rate Limiting RPCs

```csharp
private readonly Dictionary<ulong, float> _lastCardPlayTime = new();
private const float CardPlayCooldown = 0.5f; // minimum time between card plays

[ServerRpc]
private void PlayCardServerRpc(int handIndex, Vector2 position, ServerRpcParams rpcParams = default)
{
    ulong clientId = rpcParams.Receive.SenderClientId;
    float now = Time.time;

    if (_lastCardPlayTime.TryGetValue(clientId, out float lastTime) &&
        now - lastTime < CardPlayCooldown)
    {
        return; // silently drop — rate limited
    }
    _lastCardPlayTime[clientId] = now;

    // ... rest of validation
}
```

---

## 6. Spawning & Despawning {#spawning--despawning}

### Prefab Registration

All NetworkObject prefabs must be in `NetworkManager.NetworkConfig.Prefabs`. In Unity 6 with
Netcode 2.x, use the `NetworkPrefabs` ScriptableObject asset referenced by `NetworkManager`.

### Spawn with Ownership

```csharp
// Tower owned by the deploying player — they can send RPCs to it
var towerGo = Instantiate(card.TowerPrefab, position, Quaternion.identity);
towerGo.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
```

### Spawn without Ownership (server-owned entities)

```csharp
// Projectile owned by server — no client can directly control it
var projGo = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
projGo.GetComponent<NetworkObject>().Spawn();
```

### Despawn Cleanup

```csharp
public void DestroyTower(NetworkObject towerNetObj)
{
    if (!IsServer) return;

    // Clean up references before despawning
    TowerManager.Instance.UnregisterTower(towerNetObj.NetworkObjectId);

    towerNetObj.Despawn(); // removes from all clients; optionally pass true to destroy
}
```

**Never call `Destroy()` on a NetworkObject.** Always use `Despawn()` — it handles network cleanup.

---

## 7. Object Pooling with Netcode {#object-pooling}

For projectiles, units, and VFX that spawn/despawn frequently:

```csharp
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkObjectPool : MonoBehaviour, INetworkPrefabInstanceHandler
{
    [SerializeField] private GameObject _prefab;
    [SerializeField] private int _prewarmCount = 20;

    private readonly Queue<NetworkObject> _pool = new();

    private void Start()
    {
        NetworkManager.Singleton.PrefabHandler.AddHandler(_prefab, this);
        Prewarm();
    }

    private void Prewarm()
    {
        for (int i = 0; i < _prewarmCount; i++)
        {
            var go = Instantiate(_prefab);
            go.SetActive(false);
            _pool.Enqueue(go.GetComponent<NetworkObject>());
        }
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        NetworkObject netObj;
        if (_pool.Count > 0)
        {
            netObj = _pool.Dequeue();
            netObj.transform.SetPositionAndRotation(position, rotation);
        }
        else
        {
            netObj = UnityEngine.Object.Instantiate(_prefab, position, rotation)
                .GetComponent<NetworkObject>();
        }

        netObj.gameObject.SetActive(true);
        return netObj;
    }

    public void Destroy(NetworkObject networkObject)
    {
        networkObject.gameObject.SetActive(false);
        _pool.Enqueue(networkObject);
    }
}
```

---

## 8. Ownership & Permission {#ownership--permission}

### Who Owns What

| Entity | Owner | Rationale |
|---|---|---|
| Tower | Deploying player | Owner can send upgrade/sell RPCs |
| Unit (troop) | Deploying player | Owner identity used for team logic |
| Projectile | Server (no owner) | Pure server-sim; clients just see it |
| Elixir state | Server | Server-authoritative economy |
| Match state | Server | Win/lose, timer, phase |

### Ownership Transfer

Rarely needed in a TD card game, but if a spell steals a unit:

```csharp
netObj.ChangeOwnership(newOwnerClientId);
```

---

## 9. State Synchronization Strategy {#state-sync-strategy}

### What Syncs How

| Data | Sync method | Update freq |
|---|---|---|
| Tower health | `NetworkVariable<int>` | On damage |
| Tower target | `NetworkVariable<ulong>` (target NetworkObjectId) | On target switch |
| Unit position | `NetworkTransform` (server-auth) | Every network tick |
| Elixir amount | `NetworkVariable<float>` per player | Continuous (generation) |
| Card hand | Targeted `ClientRpc` (private per player) | On draw/play |
| Match timer | `NetworkVariable<float>` | Every second |
| Score / crown | `NetworkVariable<int>` | On score event |
| VFX trigger | `ClientRpc` (one-shot) | On event |

### Bandwidth Budget

Target: **< 10 KB/s per client** for mobile. Strategies:

- Use `NetworkVariable` dirty flags — only sends when value changes.
- `NetworkTransform` tick rate: set to 20-30 Hz instead of default (saves ~40% bandwidth for units).
- Compress positions: for a 2D game, only sync X/Y (disable Z sync in `NetworkTransform`).
- Batch VFX ClientRpcs when multiple events happen in the same frame.
- Avoid syncing cosmetic-only state (sprite frame, particle intensity).

---

## 10. Network Tick Rate & Update Loops {#tickrate}

```csharp
// Set in NetworkManager or via code
NetworkManager.Singleton.NetworkConfig.TickRate = 30; // 30 ticks/second
```

For a mobile TD card game, **30 ticks/second** is a good balance between responsiveness and
bandwidth. Clash Royale uses a similar tickrate.

**Where to put game logic:**

| Loop | Use for |
|---|---|
| `Update()` | Presentation, animations, UI |
| `FixedUpdate()` | Physics (if using Physics2D) |
| `OnNetworkTick()` | N/A in NGO 2.x — use `NetworkManager.NetworkTickSystem.Tick` event |

For deterministic simulation, run game logic in a fixed tick callback:

```csharp
public override void OnNetworkSpawn()
{
    if (IsServer)
    {
        NetworkManager.NetworkTickSystem.Tick += OnNetworkTick;
    }
}

private void OnNetworkTick()
{
    // Run game simulation at fixed network tick rate
    UpdateUnitMovement(NetworkManager.LocalTime.FixedDeltaTime);
    UpdateTowerTargeting();
    ProcessProjectileHits();
}
```

---

## 9. Late Join / Reconnection {#late-join}

`NetworkVariable`s automatically sync their current value to late-joining clients — this is the
primary reason to prefer them for persistent state.

For state not covered by NetworkVariables (e.g., which cards are in each player's hand), implement
a sync callback:

```csharp
NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
{
    if (IsServer && clientId != NetworkManager.Singleton.LocalClientId)
    {
        SyncFullGameStateToClient(clientId);
    }
};
```

### Reconnection via Sessions API (NGO 2.4+)

The Multiplayer Services SDK supports reconnection natively:

```csharp
var sessionIds = await MultiplayerService.Instance.GetJoinedSessionIdsAsync();
if (sessionIds.Count > 0)
{
    await MultiplayerService.Instance.ReconnectToSessionAsync(sessionIds[0]);
}
```

For a competitive PvP game, set a reconnection window (e.g., 30 seconds). If the player
doesn't reconnect within that window, declare the match forfeit. See
`references/unity-gaming-services.md` for the full reconnection flow.

---

## 10. NetworkTransform — NGO 2.x Interpolation {#networktransform-interpolation}

NGO 2.3+ overhauled interpolation with new interpolator types.

### Interpolator Types (available in inspector)

| Type | Behavior | Best for |
|---|---|---|
| `LegacyLerp` | Original lerp (pre-2.3 behavior) | Backward compatibility |
| `Lerp` | New lerp with improved buffer consumption | Most cases |
| `SmoothDampening` | Smooth damp — nice balance between precision and smoothing | Units moving along paths |

Select per-axis in the `NetworkTransform` inspector under Position, Rotation, and Scale.

### Key NetworkTransform Properties (NGO 2.0+)

```csharp
// Sync children together to prevent jitter between parent/child NetworkTransforms
networkTransform.TickSyncChildren = true;

// Handle transform space transitions when reparenting (e.g., unit entering a vehicle)
networkTransform.SwitchTransformSpaceWhenParented = true;

// Adjust interpolation buffer offset — increase for smoother results on high-latency
NetworkTransform.InterpolationBufferTickOffset = 2; // default is 1
```

### For This TD Card Game

- **Units**: Use `NetworkTransform` with `Lerp` or `SmoothDampening` interpolator, tick rate 20 Hz.
  Sync only Position X, Y. Disable Z, Rotation, Scale sync.
- **Towers**: Do NOT use `NetworkTransform` — towers are placed once and never move. Sync
  position once on spawn via the `Instantiate` position parameter.
- **Projectiles**: Server-authoritative, visual-only on clients. Use a `ClientRpc` for spawn
  position + target, let clients simulate the visual arc locally. No `NetworkTransform` needed.

### TickLatency (NGO 2.3+)

```csharp
// Get average latency for the local client in ticks
float latencyInTicks = NetworkManager.Singleton.NetworkTimeSystem.TickLatency;
float latencyMs = latencyInTicks * (1f / NetworkManager.Singleton.NetworkConfig.TickRate) * 1000f;
```

Useful for adaptive UI (show "poor connection" indicator) or adjusting input prediction.

---

## 11. SinglePlayerTransport (NGO 2.4+) {#singleplayer-transport}

NGO 2.4 added `SinglePlayerTransport` — start a host session without any underlying network
transport. Useful for:

- **Solo/practice mode** in a TD card game (play against AI without relay costs).
- **Testing game logic** without network setup.
- **Offline play** on mobile (no internet required).

```csharp
// Swap transport at runtime for solo mode
var networkManager = NetworkManager.Singleton;

if (isSoloMode)
{
    // Replace UnityTransport with SinglePlayerTransport
    var singlePlayerTransport = networkManager.gameObject.AddComponent<SinglePlayerTransport>();
    networkManager.NetworkConfig.NetworkTransport = singlePlayerTransport;
}

networkManager.StartHost(); // works without any real network connection
```

All `NetworkBehaviour`, `NetworkVariable`, RPCs, and spawning work identically — the transport
just loops back locally. Zero networking overhead.

---

## 12. Testing Multiplayer Locally {#testing}

### ParrelSync (recommended for editor testing)
- Clone the project into a second editor instance.
- One runs as Host, the other as Client.
- Both share the same codebase — changes reflect instantly.

### Build + Editor
- Build the game for desktop (faster iteration than mobile builds).
- Run the build as Host, the editor as Client (or vice versa).
- Use `NetworkManager`'s built-in connection UI or your session flow.

### Unity Multiplayer Play Mode (MPPM)
- Unity 6 includes this as a package — allows running multiple virtual players in the same editor.
- Useful for quick tests but less reliable for real networking bugs.
- Supports up to 4 simulated players (main editor + 3 virtual).

### Always test on device
- Editor-to-editor won't reveal mobile-specific issues (touch, thermal throttling, real latency).
- Use Unity Remote or actual device builds for final validation.
- Test on 4G/LTE to validate real-world latency and packet loss behavior.

---

## Appendix: NGO 2.x Feature Quick Reference

Features added in NGO 2.0–2.6 that are relevant to this project:

| Version | Feature | Relevance |
|---|---|---|
| 2.0 | `NetworkTransform.TickSyncChildren` | Prevents jitter on nested transforms (e.g., unit + weapon) |
| 2.0 | `NetworkTransform.SwitchTransformSpaceWhenParented` | Smooth reparenting transitions |
| 2.0 | `NetworkObject.AllowOwnerToParent` | Clients can parent owned objects |
| 2.0 | `NetworkVariable.CheckDirtyState` | Detect changes in collections (List, Dict) inside NetworkVars |
| 2.0-pre.2 | `AnticipatedNetworkVariable<T>` | Client-side prediction for responsive gameplay |
| 2.0-pre.2 | `AnticipatedNetworkTransform` | Client-predicted movement |
| 2.0-pre.2 | `NetworkVariableUpdateTraits` | Throttle updates (min/max interval, dirtiness threshold) |
| 2.1 | `NetworkManager.OnInstantiated` / `OnDestroying` (static) | Track NetworkManager lifecycle |
| 2.1 | `RigidbodyContactEventManager` improvements | Better collision handling |
| 2.3 | New interpolator types (Lerp, SmoothDampening) | Smoother unit movement |
| 2.3 | `NetworkTransform.InterpolationBufferTickOffset` | Tune interpolation buffer per-project |
| 2.3 | `NetworkTimeSystem.TickLatency` | Measure client latency in ticks |
| 2.3 | `NetworkManager.OnPreShutdown` | Cleanup hook before shutdown |
| 2.4 | `SinglePlayerTransport` | Solo/practice mode without network |
| 2.4 | Hostname support in `SetConnectionData` | Use hostnames instead of IPs |
| 2.5 | `NetworkList<T>.AsNativeArray()` | Zero-copy read access for perf |
| 2.5 | `AttachableBehaviour` + `AttachableNode` | Parenting alternative without NetworkObject hierarchy |
| 2.5 | `ComponentController` | Sync enable/disable of components |
| 2.5 | `OnNetworkPreDespawn` | Cleanup hook before despawn sequence |
| 2.5 | `NetworkPrefabInstanceHandlerWithData<T>` | Pool handler with custom instantiation data |
| 2.6 | `NetworkList.Set()` with force parameter | Force update even if value equals previous |
| 2.6 | NetworkVariable performance improvements | Reduced overhead per-variable |

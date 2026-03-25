# Network Synchronization Architecture

## Why This Change Was Made

The game has **two maps visible simultaneously** (Player 1's and Player 2's). Both players see both maps, so all gameplay entities must stay visually synchronized with the server's authoritative state. Simply syncing spawn position + time causes drift over time due to timing differences, physics divergence, and network latency.

**Core principle:** the server runs the entire gameplay simulation. Clients are visual renderers that read server state and display it with smooth interpolation. Clients never decide gameplay outcomes.

---

## Design Decisions

### A) Enemy Movement — Hybrid Waypoint Progress Sync

**Problem:** With 50–100 enemies across both maps, sending `Vector3` positions via `NetworkTransform` every tick is ~3000 updates/sec — too expensive for mobile.

**Solution:** Enemies follow predetermined `WaypointPath`s. The server syncs a single `float` (0.0 → 1.0) representing path progress. Clients sample the same baked path at that progress value to compute position. No drift possible — client always reads the server's authoritative progress.

**Bandwidth optimization:** `ServerEnemyMovement` tracks a local `_localProgress` every frame but only writes to the `NetworkVariable` when the delta exceeds `0.005`. On a 20-unit path that's ~0.1 units — imperceptible with client-side interpolation.

### B) Tower Combat — Server Decides, Client Plays Cosmetic VFX

**Problem:** The old `AbstractClientTower` used local physics triggers + real `Rigidbody2D` bullets. Each client would target different enemies and apply different damage — fundamentally broken for multiplayer.

**Solution:** `ServerTowerCombat` runs all targeting (distance-based, not physics) and applies damage instantly. Clients receive a "tower fired" RPC and spawn a `CosmeticBullet` — a local-only lerp animation with no collider, no physics, no damage logic.

**Why distance checks instead of physics triggers?** Physics simulations diverge across machines. Distance checks are deterministic and produce identical targeting on the server regardless of client frame rate.

### C) Health & Damage — NetworkVariable per Enemy

Each enemy has `NetworkVariable<float> CurrentHealth` (server-write). `TakeDamage()` is called directly by `ServerTowerCombat` on the server. Clients read changes via `OnValueChanged` for health bar UI and VFX. On death, `NetworkObject.Despawn()` notifies all clients automatically.

### D) Projectiles — Never NetworkObjects

Bullets are local GameObjects spawned from a `CosmeticBulletPool`. Server applies damage instantly on fire. The bullet animation is purely visual. Object pooling avoids GC allocations from frequent Instantiate/Destroy on mobile.

### E) Waves — PvPvE Server-Driven Spawning

The game is PvPvE: each player defends their own base from automated waves, and can send extra enemies to the opponent's map. `ServerWaveManager` runs independent wave coroutines per map and handles both PvE (automated) and PvP (player-sent via `RequestSendEnemyServerRpc`) spawning.

### F) Architecture — Separate Server/Client Components (Composition)

Each prefab uses composition with self-disabling components rather than monolithic scripts with `IsServer` checks everywhere. Each component calls `enabled = false` in `OnNetworkSpawn()` when it's on the wrong side (server vs client).

---

## File Reference

### Enemies (`Assets/Scripts/Gameplay/Enemies/`)

| File | Side | Purpose |
|------|------|---------|
| `EnemyDataSO.cs` | Shared | ScriptableObject — enemy config (ID, name, health, speed, sprite, prefab) |
| `EnemyDataHolder.cs` | Shared | MonoBehaviour — holds reference to EnemyDataSO on the prefab |
| `IDamageable.cs` | Shared | Interface — `void TakeDamage(float)`, implemented by ServerEnemyHealth |
| `WaypointPath.cs` | Shared | Scene path — both server and client sample position at normalized progress (0–1). Editor gizmos + Odin auto-populate button |
| `ServerEnemyMovement.cs` | Server | Advances `NetworkVariable<float> PathProgress` each tick with bandwidth threshold. Updates server transform for tower targeting |
| `ClientEnemyMovement.cs` | Client | Reads PathProgress, samples WaypointPath, applies `MapTranslator.ServerToLocal()`, interpolates visual position smoothly |
| `ServerEnemyHealth.cs` | Server | `NetworkVariable<float> CurrentHealth`. `TakeDamage()` reduces health, `Despawn()` on death. Registers/unregisters with `ServerTowerCombat` |
| `ClientEnemyHealth.cs` | Client | Subscribes to `CurrentHealth.OnValueChanged`, updates `EnemyHealthBar`, hook point for hit VFX |
| `EnemyPathAssignment.cs` | Shared | `NetworkVariable<TeamType>` — server writes target map, client reads it to look up correct WaypointPath |
| `EnemyHealthBar.cs` | Client | World-space health bar — scales fill based on health ratio, follows enemy with offset |
| `EnemyNetworkPool.cs` | Shared | NGO object pool via `INetworkPrefabInstanceHandler` — per-prefab handlers, prewarms 10 per type |

### Towers (`Assets/Scripts/Gameplay/Towers/`)

| File | Side | Purpose |
|------|------|---------|
| `TowerDataSO.cs` | Shared | ScriptableObject — tower config (damage, range, cooldown) |
| `TowerDataHolder.cs` | Shared | MonoBehaviour — holds TowerDataSO, draws range gizmo in editor |
| `ServerTowerCombat.cs` | Server | Distance-based targeting, cooldown, instant damage via `ServerEnemyHealth.TakeDamage()`, broadcasts shot RPC via `ClientTowerCombat` |
| `ClientTowerCombat.cs` | Client | Receives `FireBulletRpc`, map-translates origin, spawns `CosmeticBullet` from pool |
| `CosmeticBullet.cs` | Client | Local-only visual — lerps from tower to target, no collider/physics/damage. Tracks moving target. Returns to pool on arrival |
| `CosmeticBulletPool.cs` | Client | Object pool singleton — prewarms 20, recycles to avoid GC |

### Waves (`Assets/Scripts/Gameplay/Waves/`)

| File | Side | Purpose |
|------|------|---------|
| `WaveDataSO.cs` | Shared | ScriptableObject — wave sequence (list of WaveEntry: enemy type, count, interval) + timing |
| `ServerWaveManager.cs` | Server | Independent wave coroutines per map, spawns enemies with correct path, PvP send-enemy RPC |
| `ClientWaveUI.cs` | Client | Reads wave NetworkVariables, displays wave number + countdown timer per team |

### Deprecated (replaced by the above)

| File | Replaced By |
|------|-------------|
| `Assets/Scripts/Gameplay/Towers/AbstractClientTower.cs` | ServerTowerCombat + ClientTowerCombat |
| `Assets/Scripts/old/Bullet.cs` | CosmeticBullet + CosmeticBulletPool |
| `Assets/Scripts/old/Enemy.cs` | ServerEnemyHealth + ClientEnemyHealth + ServerEnemyMovement + ClientEnemyMovement |
| `Assets/Scripts/old/IDamageable.cs` | Moved to `Gameplay/Enemies/IDamageable.cs` |

---

## Remaining TODOs

### Must-do before gameplay works

1. **`ServerEnemyMovement.cs:83`** — `// TODO: Apply damage to the player's base, then despawn`
   - When an enemy reaches the end of the path (progress = 1.0), it should deal damage to the player's base before despawning. Requires a base health system.

2. **`ServerWaveManager.cs:129`** — `// TODO: Validate cost/cooldown for sending enemies`
   - The PvP send-enemy RPC currently spawns without validation. Need to add elixir/cost check and cooldown before players can send enemies.

### Nice-to-have polish

3. **`ClientEnemyHealth.cs:40`** — `// TODO: Play hit VFX (Feel/MMFeedbacks) when newValue < previousValue`
   - Integrate Feel/MMFeedbacks for screen shake, floating damage numbers, or flash when an enemy takes damage.

4. **`CosmeticBullet.cs:79`** — `// TODO: Play impact VFX / particle at _lastTargetPos`
   - Spawn a small particle effect when the cosmetic bullet arrives at the target position.

---

## Data Flow

```
SERVER TICK:
  ServerWaveManager spawns enemy
    → Instantiate prefab at path start
    → ServerEnemyMovement.Initialize(path)
    → EnemyPathAssignment.SetTargetMap(team)
    → NetworkObject.Spawn()
    → ServerEnemyHealth registers with ServerTowerCombat

  ServerEnemyMovement.Update():
    → _localProgress += speed * dt / pathLength
    → if delta > threshold: PathProgress.Value = _localProgress
    → transform.position = path.SamplePosition(_localProgress)

  ServerTowerCombat.Update():
    → Find closest enemy within range (distance check)
    → ServerEnemyHealth.TakeDamage(damage)    [instant]
    → ClientTowerCombat.FireBulletRpc(pos, ref) [notify clients]

  ServerEnemyHealth.TakeDamage():
    → CurrentHealth.Value -= damage
    → if dead: NetworkObject.Despawn()

CLIENT FRAME:
  EnemyPathAssignment.OnNetworkSpawn():
    → Reads TargetMap NetworkVariable
    → Looks up WaypointPath from ServerWaveManager.GetPath()
    → ClientEnemyMovement.Initialize(path)

  ClientEnemyMovement.Update():
    → Read PathProgress NetworkVariable
    → path.SamplePosition(progress) → server-space position
    → MapTranslator.ServerToLocal() → local-space position
    → Lerp transform toward that position

  ClientEnemyHealth.OnHealthChanged():
    → EnemyHealthBar.SetHealth(newValue)

  ClientTowerCombat.FireBulletRpc():
    → MapTranslator.ServerToLocal(origin)
    → CosmeticBulletPool.Get()
    → bullet.Fire(localOrigin, targetTransform)
```

---

## Step-by-Step Setup Guide

### Step 1: Create ScriptableObject Assets

1. **Right-click in Project** → Create → Scriptable Objects → **EnemyData**
   - Fill in: enemyId, enemyName, maxHealth, moveSpeed, enemySprite
   - Assign the enemy prefab (create in step 2)

2. **Right-click in Project** → Create → Scriptable Objects → **TowerData**
   - Fill in: damage, range, shootCooldown, towerSprite

3. **Right-click in Project** → Create → Scriptable Objects → **WaveData**
   - Set initialDelay (seconds before first wave)
   - Set delayBetweenWaves
   - Add WaveEntry items: pick an EnemyDataSO, set count and spawnInterval

### Step 2: Set Up Enemy Prefab

Create a new prefab (or update the existing `Prefabs/Enemy.prefab`) with these components:

1. `NetworkObject`
2. `EnemyDataHolder` — assign your EnemyDataSO asset
3. `EnemyPathAssignment`
4. `NetworkMapPosition` (from `Scripts/Networking/`)
5. `ServerEnemyMovement`
6. `ServerEnemyHealth`
7. `ClientEnemyMovement`
8. `ClientEnemyHealth` — assign the EnemyHealthBar (child object, see step 3)
9. Add a `SpriteRenderer` for the enemy visual
10. Add a `Collider2D` if needed for other gameplay (not for tower targeting — that uses distance checks)

**Register the prefab** in NetworkManager's Network Prefab List.

### Step 3: Set Up Enemy Health Bar

1. Create a child GameObject under the enemy prefab
2. Add `EnemyHealthBar` component
3. Create two child sprites:
   - **Background bar** (dark color, full width)
   - **Fill bar** (bright color) — assign this Transform to `EnemyHealthBar.fillBar`
4. Adjust the `offset` field so the bar appears above the enemy sprite

### Step 4: Set Up Tower Prefab

Update the existing `Prefabs/Tower.prefab`:

1. Ensure it has `NetworkObject`
2. Add `TowerDataHolder` — assign your TowerDataSO asset
3. Add `ServerTowerCombat`
4. Add `ClientTowerCombat`
5. Add `NetworkMapPosition`
6. **Remove** `AbstractClientTower` (deprecated)
7. **Remove** the old Bullet prefab reference and range Collider2D (no longer needed)

### Step 5: Set Up CosmeticBullet Prefab

1. Create a new prefab: small sprite (the bullet visual)
2. Add `CosmeticBullet` component
3. Assign the SpriteRenderer reference
4. Set speed (default 15 is good)
5. **No collider, no Rigidbody2D, no NetworkObject**

### Step 6: Set Up Scene Objects

1. **WaypointPaths** (one per map):
   - Create a new empty GameObject on each map (e.g., `BlueMapPath`, `RedMapPath`)
   - Add `WaypointPath` component
   - Create child empty GameObjects as waypoint positions
   - Use the Odin "Auto-Populate from Children" button, or assign manually
   - Position waypoints to define the enemy lane(s)

2. **CosmeticBulletPool**:
   - Create an empty GameObject in the scene
   - Add `CosmeticBulletPool` component
   - Assign the CosmeticBullet prefab
   - Set initialPoolSize (20 is a good default)

3. **EnemyNetworkPool**:
   - Create an empty GameObject in the scene
   - Add `EnemyNetworkPool` component
   - Set initialPoolSizePerPrefab (10 default)

4. **ServerWaveManager**:
   - Create an empty GameObject in the scene
   - Add `ServerWaveManager` (it's a NetworkBehaviour — needs NetworkObject)
   - Assign: WaveDataSO, blueMapPath (WaypointPath), redMapPath (WaypointPath)

5. **ClientWaveUI**:
   - Add to a Canvas or UI object
   - Add `ClientWaveUI` component (NetworkBehaviour — needs NetworkObject, or place on the same object as ServerWaveManager)
   - Assign TMP_Text references for wave number and timer

### Step 7: Register Enemy Prefabs with the Pool

In `ServerWaveManager.OnNetworkSpawn()` (or a startup script), call:
```csharp
EnemyNetworkPool.Instance.RegisterPrefab(enemyData.EnemyPrefab);
```
for each enemy type before any waves start spawning. This hooks NGO's prefab handler to use pooling.

### Step 8: Update CardDeployer for New Tower Prefab

The existing `CardDeployer` spawns tower prefabs via `SpawnWithOwnership()`. Ensure the `CardDataSO` for tower cards references the updated tower prefab (with `ServerTowerCombat` + `ClientTowerCombat` instead of `AbstractClientTower`).

### Step 9: Test

1. Start a **dedicated server** + **2 clients**
2. Verify team assignment (Blue/Red) via TeamManager
3. Let automated waves spawn — enemies should walk the path on both clients
4. Place towers via card drag — towers should target and shoot enemies
5. Verify on both clients:
   - Enemies follow the same path at the same pace
   - Towers shoot the same target
   - Health bars decrease in sync
   - Cosmetic bullets fly from tower to enemy
   - Dead enemies despawn on both clients simultaneously
6. Test PvP: send an enemy to the opponent's map
7. Extended play test: verify no visual drift after several minutes

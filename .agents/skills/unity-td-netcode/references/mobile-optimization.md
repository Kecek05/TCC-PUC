# Mobile Optimization Guide — 2D Multiplayer Unity 6

Performance guide for a 2D mobile tower defense card game targeting 60fps on mid-range devices.

---

## Table of Contents

1. [Performance Budgets](#budgets)
2. [Rendering & Draw Calls](#rendering)
3. [Memory Management](#memory)
4. [GC Pressure & Allocations](#gc-pressure)
5. [Object Pooling](#object-pooling)
6. [Touch Input](#touch-input)
7. [Network Bandwidth on Mobile](#network-mobile)
8. [Battery & Thermal](#battery-thermal)
9. [Build Size](#build-size)
10. [Profiling Tools](#profiling)

---

## 1. Performance Budgets {#budgets}

| Metric | Target | Hard limit |
|---|---|---|
| Frame time | < 16.6ms (60fps) | < 33ms (30fps minimum) |
| Draw calls | < 50 | < 100 |
| Triangles per frame | < 50K (2D sprites) | < 100K |
| Active NetworkObjects | < 100 | < 200 |
| RPCs per second | < 30 total | < 60 |
| Bandwidth per client | < 10 KB/s | < 20 KB/s |
| Heap allocations per frame | 0 in gameplay loop | < 1 KB/frame |
| Total RAM | < 400 MB | < 600 MB |
| App bundle size | < 100 MB | < 200 MB |

---

## 2. Rendering & Draw Calls {#rendering}

### SpriteAtlas Usage

Pack all related sprites into atlases to enable batching:

```
Assets/Art/Atlases/
├── Towers.spriteatlasv2    # all tower sprites
├── Units.spriteatlasv2     # all unit sprites
├── Projectiles.spriteatlasv2
├── UI.spriteatlasv2        # UI icons and elements
└── VFX.spriteatlasv2       # particle sprites
```

**Rules:**
- One atlas per category. Keep atlas texture size ≤ 2048x2048 on mobile.
- Use **Sprite Atlas V2** (Unity 6 default) for proper variant support.
- Never mix sprites from different atlases in the same sorting layer when avoidable.
- Set atlas compression to ASTC 6x6 (Android) / ASTC 4x4 (iOS) for good quality-to-size ratio.

### Sorting Layers (draw order)

```
Background          (0)
Battlefield         (1)
Units               (2)
Towers              (3)
Projectiles         (4)
VFX                 (5)
UI                  (6)
```

Use `SortingGroup` on tower/unit prefabs to batch their child sprites together.

### Batching Checklist

- Same material + same atlas texture = one draw call.
- Use `SpriteRenderer` order-in-layer to control z-ordering within a batch.
- Avoid per-instance material property changes (`MaterialPropertyBlock` breaks batching).
- For health bars above units: use a single `Canvas` in **World Space** mode with all health bars,
  or use `SpriteRenderer`-based health bars to avoid UI canvas overhead.

### Camera Setup (2D)

```csharp
// Orthographic camera sized for mobile aspect ratios
Camera.main.orthographicSize = 7f; // adjust for your battlefield
// Clip unused depth range
Camera.main.nearClipPlane = -1f;
Camera.main.farClipPlane = 10f;
```

---

## 3. Memory Management {#memory}

### Texture Memory (biggest offender on mobile)

| Asset type | Max resolution | Format |
|---|---|---|
| Character sprites | 256x256 per frame | ASTC 6x6 |
| Tower sprites | 256x256 | ASTC 6x6 |
| UI icons | 128x128 | ASTC 4x4 |
| Background | 1024x2048 | ASTC 8x8 |
| Particle sprites | 64x64 | ASTC 6x6 |

- **Enable mipmaps: NO** for 2D sprites. Mipmaps waste 33% memory and aren't needed for
  orthographic cameras at fixed zoom.
- **Read/Write: OFF** on all textures unless you need runtime pixel access.
- Use `Addressables` or `Resources.UnloadUnusedAssets()` between scenes.

### Audio Memory

- Compress all SFX to Vorbis, quality 50-70%.
- Background music: stream from disk, don't load into memory.
- Short SFX (< 200ms): `Decompress On Load` for zero-latency playback.

---

## 4. GC Pressure & Allocations {#gc-pressure}

### Rules for Update / Tick Loops

**Zero allocations in hot paths.** These are the critical loops:

```csharp
// BAD — allocates every frame
void Update()
{
    var enemies = FindObjectsOfType<Unit>(); // GC allocation
    var closest = enemies.OrderBy(e => Vector2.Distance(...)).First(); // LINQ = GC
    string debugText = $"Target: {closest.name}"; // string allocation
}

// GOOD — pre-allocated, zero GC
private readonly List<Unit> _cachedUnits = new();
private readonly Collider2D[] _overlapResults = new Collider2D[32];

void ServerTick()
{
    int count = Physics2D.OverlapCircleNonAlloc(pos, range, _overlapResults, enemyLayer);
    for (int i = 0; i < count; i++)
    {
        // process _overlapResults[i]
    }
}
```

### Common Allocation Sources to Avoid

| Allocation | Fix |
|---|---|
| `FindObjectsOfType<T>()` | Maintain a static registry; entities register/unregister |
| LINQ (`.Where`, `.Select`, `.OrderBy`) | Manual loops with pre-allocated lists |
| `string` concatenation in loops | `StringBuilder` or avoid entirely in gameplay |
| `foreach` on non-List collections | Use `for` loop with index, or cache enumerator |
| `new List<T>()` in methods | Reuse a class-level list and `.Clear()` it |
| Lambda closures in hot paths | Use methods or cache the delegate |
| `Physics2D.OverlapCircleAll` | Use `NonAlloc` variant with pre-sized array |
| Boxing value types | Avoid passing structs as `object`; use generics |

### Incremental GC

Unity 6 supports incremental GC. Enable it:
**Project Settings → Player → Other Settings → Use Incremental GC: ON**

This spreads GC pauses across frames. Still avoid allocations — incremental GC reduces spikes
but doesn't eliminate cost.

---

## 5. Object Pooling {#object-pooling}

Mandatory for: projectiles, units, VFX particles, floating damage numbers.

### Simple Generic Pool

```csharp
public class GameObjectPool
{
    private readonly GameObject _prefab;
    private readonly Queue<GameObject> _pool = new();
    private readonly Transform _parent;

    public GameObjectPool(GameObject prefab, int initialSize, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < initialSize; i++)
        {
            var go = Object.Instantiate(prefab, parent);
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        var go = _pool.Count > 0 ? _pool.Dequeue() : Object.Instantiate(_prefab, _parent);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Return(GameObject go)
    {
        go.SetActive(false);
        _pool.Enqueue(go);
    }
}
```

For **networked** objects, use `INetworkPrefabInstanceHandler` (see `netcode-patterns.md`
section 5).

### Pool Sizing Guide

| Object type | Pool size | Rationale |
|---|---|---|
| Projectiles | 30 | Multiple towers firing simultaneously |
| Units | 20 | Several troop cards in play at once |
| Damage numbers | 15 | Burst damage events |
| Hit VFX | 15 | Matches projectile pool |
| Spell VFX | 5 | One spell at a time usually |

---

## 6. Touch Input {#touch-input}

### Input Abstraction

```csharp
public interface IInputProvider
{
    bool TryGetPointerDown(out Vector2 screenPosition);
    bool TryGetPointerDrag(out Vector2 screenPosition);
    bool TryGetPointerUp(out Vector2 screenPosition);
    bool IsPointerOverUI();
}

public class MobileInputProvider : IInputProvider
{
    public bool TryGetPointerDown(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (Input.touchCount == 0) return false;
        var touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return false;
        screenPosition = touch.position;
        return true;
    }

    public bool IsPointerOverUI()
    {
        if (Input.touchCount == 0) return false;
        return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
    }
}

public class EditorInputProvider : IInputProvider
{
    public bool TryGetPointerDown(out Vector2 screenPosition)
    {
        screenPosition = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
    }

    public bool IsPointerOverUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }
}
```

### Card Drag-and-Drop Flow

```
1. Player touches card in hand UI          → UI layer handles
2. Player drags onto battlefield            → Show placement preview (ghost sprite)
3. Validate placement locally (fast reject) → Green/red indicator
4. Player releases                          → Send PlayCardServerRpc(cardIndex, worldPos)
5. Server validates + executes              → Spawn entity or reject
```

**Critical: Don't wait for server confirmation to show VFX.** Use client-side prediction for the
deployment animation, then reconcile if the server rejects (remove the ghost, refund UI elixir
display). This masks latency.

---

## 7. Network Bandwidth on Mobile {#network-mobile}

### Mobile Network Characteristics

| Condition | Latency | Packet loss |
|---|---|---|
| Wi-Fi (good) | 20-50ms | < 1% |
| Wi-Fi (congested) | 50-200ms | 1-5% |
| 4G LTE | 30-80ms | 1-3% |
| 3G | 100-500ms | 5-10% |

### Bandwidth Optimization Techniques

1. **Reduce NetworkTransform tick rate**: 20 Hz is plenty for a 2D TD game (units move slowly).
   ```csharp
   // On NetworkTransform component:
   // Tick Rate = 20 (instead of default 30)
   // Sync only Position X, Y (disable Z, Rotation, Scale)
   ```

2. **Quantize positions**: For a 2D game, 16-bit fixed-point positions may suffice (~0.01 unit
   precision over a typical battlefield).

3. **Aggregate updates**: Instead of one RPC per damage event, batch damage events per tick.

4. **Delta compression**: NetworkVariables already use dirty flags. For custom sync, only send
   changed fields.

5. **Reduce sync frequency for non-critical state**: Tower targeting updates can be 5 Hz. VFX
   events are one-shot. Only unit positions need high-frequency sync.

---

## 8. Battery & Thermal {#battery-thermal}

### Frame Rate Management

```csharp
// Target 60fps but allow thermal throttling
Application.targetFrameRate = 60;

// If device gets hot, reduce to 30fps
public void OnThermalWarning()
{
    Application.targetFrameRate = 30;
    // Reduce particle counts, disable non-essential VFX
}
```

### Reduce GPU Load

- Disable unnecessary post-processing on mobile.
- Use simpler shaders — `Sprites/Default` for most 2D elements.
- Avoid real-time shadows in 2D (they're pointless and expensive).
- Limit particle emitter counts — max 3-4 active particle systems at once.

---

## 9. Build Size {#build-size}

### Asset Compression

- **Textures**: ASTC compression (default for modern mobile).
- **Audio**: Vorbis for music (quality 50%), ADPCM for short SFX.
- **Meshes**: Irrelevant for 2D, but strip unused mesh data.
- **Code stripping**: Enable IL2CPP with "High" stripping level.
- **Addressables**: Split large asset bundles; load on demand.

### Strip Unused Engine Features

In **Project Settings → Player → Other Settings → Strip Engine Code: ON**.

Disable unused Unity modules:
- Physics 3D (use Physics2D only)
- Terrain
- Wind
- Cloth
- Vehicle physics

---

## 10. Profiling Tools {#profiling}

### Unity Profiler

Connect to device over USB or Wi-Fi:
```
Window → Analysis → Profiler → Target: <your device>
```

Key modules to watch:
- **CPU**: Check for frame time spikes; look at GC.Alloc column.
- **Rendering**: Draw call count, batch count, triangle count.
- **Memory**: Total allocated, texture memory, mesh memory.
- **Network**: Bytes sent/received, RPC count.

### Frame Debugger

`Window → Analysis → Frame Debugger` — shows every draw call in order. Use this to find
batching breaks (different materials, different atlas textures, render order issues).

### Memory Profiler Package

Install `com.unity.memoryprofiler` for detailed snapshots. Take snapshots on device and compare
before/after to find leaks.

### Network Profiling

Netcode for GameObjects includes a runtime network stats monitor:
```csharp
// Add NetworkStatsMonitor component to a UI canvas for live stats
// Shows: RTT, packet loss, bytes in/out, RPCs/sec, NetworkObject count
```

### Checklist Before Every Device Build

- [ ] Profile CPU in editor — any frame > 8ms? (half budget to leave room for mobile)
- [ ] Profile memory — under 400MB?
- [ ] Check draw calls in Frame Debugger — under 50?
- [ ] Run for 10 minutes — does memory grow? (leak test)
- [ ] Test on lowest-target device — sustained 60fps?
- [ ] Test on 4G network — acceptable latency?

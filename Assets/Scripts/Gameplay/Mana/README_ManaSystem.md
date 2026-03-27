# Mana System - Design Document

## Overview
Server-authoritative mana system inspired by Clash Royale's elixir mechanic.
Smooth continuous regeneration, starting mana, max cap at 10.

**Scope:** Mana management only — no card spending integration yet.

---

## Files

| File | Status | Description |
|------|--------|-------------|
| `ManaSettingsSO.cs` | New | ScriptableObject for balance tuning |
| `ServerManaManager.cs` | Rewrite | Server-authoritative mana manager |
| `ClientManaManager.cs` | Rewrite | Client-side predicted mana + UI |

---

## ManaSettingsSO

ScriptableObject holding all mana balance values. Asset goes in `Assets/ScriptableObjects/Mana/`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `maxMana` | float | 10 | Maximum mana cap |
| `startingMana` | float | 5 | Mana at match start |
| `regenPerSecond` | float | 0.357 | Regen rate (~1 mana per 2.8s, similar to CR) |

---

## ServerManaManager

Singleton (`ServerManaManager.Instance`), follows `ServerWaveManager` pattern.

### NetworkVariables (synced to clients)
- `NetworkVariable<float> BlueMana` — blue team's mana
- `NetworkVariable<float> RedMana` — red team's mana

### Regeneration Approach
**Smooth per-frame** instead of chunky ticks:
```
mana += regenPerSecond * Time.deltaTime   // every Update()
```

Uses **local float accumulators** (`_blueLocalMana`, `_redLocalMana`) on the server for precision.
The NetworkVariable is only updated when the delta exceeds a **sync threshold** (0.01), same pattern as `ServerEnemyMovement`. This avoids marking the NetworkVariable dirty every single frame.

When mana hits max (10), syncs stop entirely (delta stays 0 = no bandwidth).

### Public API

| Method | Returns | Description |
|--------|---------|-------------|
| `GetMana(TeamType)` | `float` | Current authoritative mana (reads local accumulator) |
| `CanAfford(TeamType, int cost)` | `bool` | `FloorToInt(mana) >= cost` — 3.99 mana can't afford 4 |
| `TrySpendMana(TeamType, int cost)` | `bool` | Atomic check + deduct + immediate sync |

### Why FloorToInt for affordability?
Matches Clash Royale: the elixir counter shows integers, and you need the full integer to play a card. At 3.99 elixir you see "3" and can't play a 4-cost card.

### Why TrySpendMana forces immediate sync?
After spending, the client needs to see the mana drop instantly (not wait for the next threshold crossing in Update). So it pushes the value to the NetworkVariable right away.

---

## ClientManaManager (Client-Side Prediction + UI)

Plain `MonoBehaviour` (not NetworkBehaviour). Reads `ServerManaManager`'s NetworkVariables, owns no network state.

### How It Works

```
┌─────────────────────────────────────────────────────┐
│  ServerManaManager (NetworkVariable<float>)          │
│    BlueMana / RedMana                                │
│         │  OnValueChanged                            │
│         ▼                                            │
│  ClientManaManager                                   │
│    _serverMana  ← updated by OnValueChanged          │
│    _predictedMana ← drives all UI                    │
│         │                                            │
│         ▼                                            │
│  UI: manaBarFill.fillAmount + manaText               │
└─────────────────────────────────────────────────────┘
```

### Prediction Flow

**Normal regen (no spend):**
- `Update()` advances `_predictedMana += regenPerSecond * deltaTime` locally
- `OnValueChanged` snaps `_predictedMana = serverValue` to stay aligned
- Result: perfectly smooth bar, no jitter between network ticks

**On card spend (optimistic):**
1. Card UI calls `PredictSpend(cost)` → bar drops instantly
2. Sets `_pendingSpend = true` so `OnValueChanged` doesn't immediately overwrite
3. Server processes spend → sends updated mana via NetworkVariable
4. `OnValueChanged` sees server value dropped (confirming spend) → clears `_pendingSpend`, snaps to server

**On server reject:**
1. Card system calls `RevertSpend(cost)` → bar jumps back up
2. Clears `_pendingSpend`, resumes normal reconciliation

### Public API

| Method | Description |
|--------|-------------|
| `PredictSpend(int cost)` | Optimistic UI deduction, sets pending flag |
| `RevertSpend(int cost)` | Undo prediction on server reject |
| `CanAffordLocally(int cost)` | `FloorToInt(_predictedMana) >= cost` — for graying out cards |
| `float PredictedMana` | Read-only property for other UI |

### UI Elements (serialized fields)
- **`Image manaBarFill`** — `fillAmount` for smooth continuous bar
- **`TMP_Text manaText`** — integer display via `FloorToInt`
- **`ManaSettingsSO manaSettings`** — for `maxMana` (bar divisor) and `regenPerSecond` (local prediction)

### Visual Behavior
- Bar fills smoothly using local prediction (no network jitter)
- On spend: bar drops instantly (optimistic)
- On server reject: bar snaps back (revert)
- On server confirm: seamless transition

---

## Visual Timeline Example

```
Time 0s:    Mana = 5.00    Bar = 50%    Text = "5"
Time 1.4s:  Mana = 5.50    Bar = 55%    Text = "5"
Time 2.8s:  Mana = 6.00    Bar = 60%    Text = "6"   ← text jumps here
Time 5.6s:  Mana = 7.00    Bar = 70%    Text = "7"
...
Time 14s:   Mana = 10.00   Bar = 100%   Text = "10"  ← capped, no more syncs
```

---

## What's NOT Included (for later)

- **Card spending hook:** `TrySpendMana` exists but won't be called from `CardDeployer` yet. When ready, add: `if (!ServerManaManager.Instance.TrySpendMana(team, cardData.Cost)) return;` in `RequestPlaceCardServerRpc`.
- **Overtime regen multiplier:** Add `float overtimeRegenMultiplier` to `ManaSettingsSO` and multiply in `Update()` when overtime is active.
- **Mana segments visual:** The bar supports it via a sliced sprite with 10 segment markers baked into the texture.

---

## Questions to Consider

1. **Starting mana = 5?** — CR starts at 5. Want a different value?
2. **Regen rate = 0.357/s (~2.8s per mana)?** — This matches CR base rate. Want faster/slower for your game's pacing?
3. **Max mana = 10?** — Standard CR cap. Any reason to change?
4. **Bar hardcodes max at 10** — If max mana could change mid-match (overtime mode?), it should become a NetworkVariable instead.
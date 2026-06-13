---
name: create-card
description: >
  Senior skill for creating a brand-new playable card in this multiplayer tower-defense game,
  end-to-end, following the project's existing card architecture. Trigger whenever the user wants
  to "add a card", "create a card", "make a new spell / tower / enemy card", "new SpellCard /
  TowerCard / SpawnEnemyCard", add a CardType, a new spell + executor, a new tower (with combat),
  or a new enemy that a card can summon. Handles the full pipeline: the CardType enum entry, the
  CardDataSO subclass asset, the card prefab, the gameplay entity (spell executor / tower prefab +
  combat / enemy prefab), every list-SO + Netcode registration, and deck wiring for testing.
  Authoring is done through the live Unity MCP bridge (Unity_RunCommand / Unity_CreateScript /
  Unity_ManageScript). Drive it interactively: ask, confirm, then build.
---

# Create Card

Create a new card the way this project already builds cards — not from first principles. Cards are
**data-driven and routed by C# type**, so most of the work is authoring ScriptableObjects + a prefab
and registering them; new C# is only needed for genuinely new behavior (a spell executor, a new tower
combat). Read `references/architecture.md` once per session before building — it is the source of truth
for every touch-point and the serialized field names. The exact, copy-pasteable Unity MCP recipes live
in `references/recipes.md`.

## Prerequisites (check first)

- **The Unity Editor must be open with the MCP bridge running.** Verify with `Unity_GetProjectData`
  (tiny limits). If it fails, ask the user to open the project in Unity, then retry. All asset/prefab
  authoring goes through `Unity_RunCommand`; all C# goes through `Unity_CreateScript` / `Unity_ManageScript`.
- This skill **never hand-edits `.asset`, `.prefab`, or `.meta` files** and never invents GUIDs. Unity
  generates them. (C# `.cs` files are the only thing written as text, via the MCP script tools.)

## The three families (pick one)

| Family | Card class | Card data SO | Card prefab to clone | Gameplay entity it needs |
|---|---|---|---|---|
| **Spell** | `SpellCard` | `SpellCardDataSO` | `CardSpellFireball.prefab` | `SpellDataSO` asset + an `ISpellExecutor` |
| **Tower** | `TowerCard` | `TowerCardDataSO` | `CardCircleTower.prefab` | `TowerDataSO` asset + a networked tower prefab (+combat) |
| **Enemy** | `SpawnEnemyCard` | `SpawnEnemyCardDataSO` | `CardEnemySpawn.prefab` | `EnemyDataSO` asset + a networked enemy prefab |

A new card of an existing family needs **no new deployer and no new UI sub-factory** — those dispatch on
the C# type of the data SO. See the routing note in `architecture.md`.

## Step 1 — Intake (interactive Q&A)

Ask only what you can't infer. Use `AskUserQuestion` for the family + small choices; free text for names/numbers.

**Always:**
- Family (Spell / Tower / Enemy).
- `CardName` (display) and an **Identifier** in PascalCase (drives the `CardType` member + asset/prefab file names, e.g. `Poison` → `CardType.SpellPoison`, `SpellPoisonCardData`, `CardSpellPoison`).
- `Cost` (int), `Rarity` (Common/Rare/Epic/Legendary), `Description`.
- `CardImage` sprite — existing asset path, or **placeholder** (reuse the cloned card's sprite + leave a TODO).
- Which deck(s) to add it to for testing (default `Assets/ScriptableObjects/CardHand/DEBUG_Hand.asset`), or none.

**Spell also:**
- `SpellType` identifier; **offensive** (`SpellOffensiveDataSO`, has `Damage`) or **effect** (`SpellEffectDataSO`, has `Duration`).
- `Range`, `TravelTime`; `CanUseInEnemyMap` / `CanUseInLocalMap`; `SpellGhostSprite` + `VisualPrefab` (reuse Fireball's or none).
- **Behavior:** reuse an existing executor's logic pattern, or new logic? → you will write `<Id>Executor.cs`.

**Tower also:**
- `TowerType` identifier; reuse an existing tower **behavior** (Circle / Square-explosion) or new behavior (→ new `Server<Id>TowerCombat` / `Client<Id>TowerCombat`).
- Per-level stats (damage / range / shoot cooldown / bullet speed / setup ×3); `TowerGhostSprite`; tower body sprite; bullet visuals.

**Enemy also:**
- `EnemyType` identifier; `EnemyName`, `MaxHealth`, `MoveSpeed`, `SpawnDuration`, `Damage`, `EnemySprite`. (Usually no new C#.)

## Step 2 — Resolve new-vs-reuse

Decide, and state, what is **new** vs **reused**:
- Is the family enum value (`SpellType`/`TowerType`/`EnemyType`) new, or does it already exist?
- For Tower/Enemy: does the gameplay entity (prefab + data) for that type already exist? If yes, the card just
  references it. If no, you are in **full end-to-end** territory and must author the entity + register it as a
  Netcode prefab (`Assets/DefaultNetworkPrefabs.asset`). See the entity sub-procedures in `recipes.md`.

## Step 3 — Confirm manifest (hard gate)

Before writing anything, present a complete manifest and **wait for an explicit "go"**:
- Enum members to append (always append — see guardrails).
- New `.cs` files (executor / tower combat).
- New `.asset` files (card data, spell/tower/enemy data) and their values.
- New prefabs (card prefab; tower/enemy prefab) and what they're cloned from.
- Every list/registry touched: `CardDataListSO`, plus `SpellDataListSO` / `TowerDataListSO` / `EnemyDataListSO`,
  `DefaultNetworkPrefabs`, and the chosen `DebugHand`.
- Any art left as a placeholder (call it out explicitly as a TODO).

## Step 4 — Execute (order matters)

Follow `references/recipes.md`. Sequence, because `Unity_RunCommand` can only reference **already-compiled** types:

1. **Phase A — C# first.** Append enum members to `Assets/Scripts/Enums.cs` (`CardType.<X>` + the family enum if
   new). Create any new scripts (`<Id>Executor.cs` + register it in `SpellExecutorFactory`; or the tower combat
   scripts). Then let Unity compile and **check the console is clean** before Phase B.
2. **Phase B — assets, prefabs, wiring** via `Unity_RunCommand` (AssetDatabase / PrefabUtility / SerializedObject):
   gameplay-entity data + prefab (if new) → register entity prefab in `DefaultNetworkPrefabs` → clone the card
   prefab → create the `CardDataSO` subclass asset and set all fields → wire the circular ref (`cardDataSo` ↔
   `CardPrefab`) → append to `CardDataListSO` (+ the family list SO) → add the `CardType` to the chosen `DebugHand`.
   Finish with `SetDirty` + `AssetDatabase.SaveAssets()` + `Refresh()`.
3. **Phase C — verify.** `Unity_GetConsoleLogs` for errors; confirm the asset is in `CardDataListSO`, the prefab's
   `cardDataSo` points back, and (towers/enemies) the prefab is in `DefaultNetworkPrefabs`.

## Step 5 — Report

Tell the user: files created/edited, what's wired, any placeholder art TODOs, and how to test (it's in the
DEBUG deck → enter Play; spells/towers also need real art to look right). Suggest committing.

## Guardrails (do not skip)

- **Enums are append-only.** `CardType`, `SpellType`, `TowerType`, `EnemyType` are serialized **by integer**
  in assets (e.g. `DebugHand.Deck`, every `CardDataSO.CardType`). Never reorder or insert — only append, or you
  silently remap existing cards.
- **Route by type.** The new card data asset MUST be the correct subclass (`SpellCardDataSO` / `TowerCardDataSO`
  / `SpawnEnemyCardDataSO`) or no deployer/factory will handle it.
- **Set `ExistingType` and `Rarity` correctly** (int maps in `architecture.md`: Tower=1, Spell=2, Enemy=3).
- **Spells fail silently without a registered executor.** `SpellExecutorFactory` must contain the `SpellType`.
  Cautionary precedent: `SpellType.Ice` exists and `IceExecutor` exists, but it is **not** registered, so Ice is
  dead. Don't repeat that.
- **Networked entities must be registered.** A tower/enemy prefab not in `DefaultNetworkPrefabs` will throw at
  `Spawn()`. The card will look fine and then fail at play time.
- **A card only appears in a match if its `CardType` is in a deck.** `CardDataListSO` makes it available to the
  deck-builder UI; `DebugHand.Deck` is what actually gets drawn in dev play.

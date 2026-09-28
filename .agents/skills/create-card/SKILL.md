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
  Authoring is done through the Unity Pipeline CLI (`unity command ...`) against the live Editor.
  Drive it interactively: ask, confirm, then build.
---

# Create Card

Create a new card the way this project already builds cards — not from first principles. Cards are
**data-driven and routed by C# type**, so most of the work is authoring ScriptableObjects + a prefab
and registering them; new C# is only needed for genuinely new behavior (a spell executor, a new tower
combat). Read `references/architecture.md` once per session before building — it is the source of truth
for every touch-point and the serialized field names. The exact, copy-pasteable CLI recipes live in
`references/recipes.md`.

## Prerequisites (check first)

- **The Unity Editor must be open**, with the `com.unity.pipeline` package running its server. Verify with:
  ```bash
  unity command editor_status
  ```
  Expect `{"status":"ready","compiling":false,...}`. If the CLI cannot find an instance, ask the user to open
  the project in Unity, then retry. If `status` is `settling`, the Editor is still importing — wait and re-poll.
- Run every `unity command ...` from the project root (`E:\UnityProjects\TCC-PUC`); the CLI discovers the
  Editor from `Library/Pipeline/.unity-pipeline-port`.
- **Two ways to author, and the split matters:**
  - **C# (`.cs`) is edited directly on disk** with the normal Read/Edit/Write tools, then compiled via
    `unity command recompile`. Do not round-trip source through the CLI — you lose diffs for no benefit.
  - **Assets, prefabs and wiring go through `unity command`** (`create_asset`, `copy_asset`,
    `set_serialized_field`, …), never by hand-editing `.asset` / `.prefab` / `.meta` text. Never invent a
    GUID — Unity generates them.
- `eval` is the escape hatch for anything without a dedicated command (list-SO appends, deck appends). It is
  Roslyn scripting: bare `UnityEngine` + `UnityEditor` usings, multi-statement, `return` a value.
  **Always pass `--timeout 60000`** — see Guardrails.

## The three families (pick one)

| Family | Card class | Card data SO | Card prefab to clone | Gameplay entity it needs |
|---|---|---|---|---|
| **Spell** | `SpellCard` | `SpellCardDataSO` | `Assets/Prefabs/Cards/CardSpellFireball.prefab` | `SpellDataSO` asset + an `ISpellExecutor` |
| **Tower** | `TowerCard` | `TowerCardDataSO` | `Assets/Prefabs/Cards/Towers/CardCircleTower.prefab` | `TowerDataSO` asset + a networked tower prefab (+combat) |
| **Enemy** | `SpawnEnemyCard` | `SpawnEnemyCardDataSO` | `Assets/Prefabs/Cards/CardEnemySpawn.prefab` | `EnemyDataSO` asset + a networked enemy prefab |

A new card of an existing family needs **no new deployer and no new UI sub-factory** — those dispatch on
the C# type of the data SO. See the routing note in `architecture.md`.

## Step 1 — Intake (interactive Q&A)

Ask only what you cannot infer. Use `AskUserQuestion` for the family + small choices; free text for names/numbers.

**Always:**
- Family (Spell / Tower / Enemy).
- `CardName` (display) and an **Identifier** in PascalCase (drives the `CardType` member + asset/prefab file names, e.g. `Poison` → `CardType.SpellPoison`, `SpellPoisonCardData`, `CardSpellPoison`).
- `Cost` (int), `Rarity` (Common/Rare/Epic/Legendary), `Description`.
- `CardImage` sprite — existing asset path, or **placeholder** (reuse the cloned card's sprite + leave a TODO).
- Which deck(s) to add it to for testing, or none. Confirm the target deck by listing them live —
  `unity command find_assets --type DebugHand` — rather than assuming a filename.

**Spell also:**
- `SpellType` identifier; which `SpellDataSO` subclass (`SpellOffensiveDataSO` = `Damage`,
  `SpellEffectDataSO` = `Duration`, `SpellBuffDataSO` = `AttackSpeedBonus`, `SpellRageDataSO` = `MoveSpeedBonus`).
- `Range`, `TravelTime`; `CanUseInEnemyMap` / `CanUseInLocalMap`; `SpellGhostSprite` + `VisualPrefab` (reuse Fireball's or none).
- **Behavior:** reuse an existing executor's logic pattern, or new logic? → you will write `<Id>Executor.cs`.

**Tower also:**
- `TowerType` identifier; reuse an existing tower **behavior** (Circle / Square-explosion / Slam / Dart) or new behavior (→ new `Server<Id>TowerCombat` / `Client<Id>TowerCombat`).
- Per-level stats (damage / range / shoot cooldown / bullet speed / setup ×3); `TowerGhostSprite`; tower body sprite; bullet visuals.

**Enemy also:**
- `EnemyType` identifier; `EnemyName`, `MaxHealth`, `MoveSpeed`, `SpawnDuration`, `Damage`, `EnemySprite`. (Usually no new C#.)

**Progression (all families, ask once):** the card inherits its rarity's default growth table from
`CardProgressionSettings`. Only set `OverrideStatGrowth` + `StatGrowth` if this card must diverge. Default
is "leave it off" — say so rather than silently deciding.

## Step 2 — Resolve new-vs-reuse

Decide, and state, what is **new** vs **reused**:
- Is the family enum value (`SpellType`/`TowerType`/`EnemyType`) new, or does it already exist? **Read
  `Assets/Scripts/Enums.cs` to check** — do not trust any snapshot, including this skill's.
- For Tower/Enemy: does the gameplay entity (prefab + data) for that type already exist? If yes, the card just
  references it. If no, you are in **full end-to-end** territory and must author the entity + register it as a
  Netcode prefab (`Assets/DefaultNetworkPrefabs.asset`). See the entity sub-procedures in `recipes.md`.

## Step 3 — Confirm manifest (hard gate)

Before writing anything, present a complete manifest and **wait for an explicit "go"**:
- Enum members to append (always append — see guardrails).
- New `.cs` files (executor / tower combat).
- New `.asset` files (card data, spell/tower/enemy data) and their values.
- New prefabs (card prefab; tower/enemy prefab) and what they are cloned from.
- Every list/registry touched: the card list SO, plus the family's data list SO, `DefaultNetworkPrefabs`,
  and the chosen deck — each by its **verified live path** (resolve with `find_assets`, do not hardcode).
- Any art left as a placeholder (call it out explicitly as a TODO).

## Step 4 — Execute (order matters)

Follow `references/recipes.md`. Sequence, because CLI commands can only reference **already-compiled** types:

1. **Phase A — C# first.** Append enum members to `Assets/Scripts/Enums.cs` (`CardType.<X>` + the family enum if
   new) with a normal Edit. Create any new scripts (`<Id>Executor.cs` + register it in `SpellExecutorFactory`; or
   the tower combat scripts). Then `unity command recompile`, poll `recompile_status` until `completed` /
   `up_to_date`, and **confirm the console is clean** before Phase B.
2. **Phase B — assets, prefabs, wiring.** Gameplay-entity data + prefab (if new) → ensure the entity prefab is in
   `DefaultNetworkPrefabs` exactly once → clone the card prefab → create the `CardDataSO` subclass asset and set
   all fields → wire the circular ref (`cardDataSo` ↔ `CardPrefab`) → append to the card list SO (+ the family
   list SO) → add the `CardType` to the chosen deck.
3. **Phase C — verify.** Read every write back (`get_serialized_fields`) and check `get_console_logs`. A write
   that reported success is **not** proof it landed — see Guardrails.

## Step 5 — Report

Tell the user: files created/edited, what is wired, any placeholder art TODOs, and how to test (it is in the
chosen deck → enter Play; spells/towers also need real art to look right). Suggest committing.

## Guardrails (do not skip)

- **`set_serialized_field` reports success even when the value does not stick.** A type-mismatched reference
  (e.g. a non-Sprite guid into `CardImage`) returns `success: true` and silently leaves the field `None`.
  **Verified the hard way.** Every field you set must be read back with `get_serialized_fields` in Phase C.
- **`eval` defaults to a 5000 ms main-thread budget and a trivial call can take ~3.3 s.** Blowing it returns
  `400 Bad Request: Main thread operation timed out after 5000ms` — which looks like a dead connection but
  is not, and it also lands in the Unity console as an Error that will pollute your Phase C log check. Always
  pass `--timeout 60000`.
- **Enum values in `--value` are bare, not JSON-quoted.** `--value SpellFireball`, never `--value '"SpellFireball"'`.
  Object references are a bare **path or guid string**, not a JSON object. A component-typed field
  (`CardPrefab` is an `AbstractCard`) resolves from the plain **prefab path** — the CLI picks the matching
  component for you.
- **Enums are append-only.** `CardType`, `SpellType`, `TowerType`, `EnemyType` are serialized **by integer**
  in assets (every deck, every `CardDataSO.CardType`). Never reorder or insert — only append, or you
  silently remap existing cards. This is also why `CardType` must keep being appended for the player save
  (`JsonUtility` writes enums as ints).
- **Route by type.** The new card data asset MUST be the correct subclass (`SpellCardDataSO` / `TowerCardDataSO`
  / `SpawnEnemyCardDataSO`) or no deployer/factory will handle it.
- **Set `ExistingType` and `Rarity` correctly** (`ExistingTypesOfCard`: Tower=1, Spell=2, Enemy=3).
- **Spells fail silently without a registered executor.** `SpellExecutorFactory._executors` must contain the
  new `SpellType`. Every `SpellType` that exists today is registered — keep that invariant true.
- **Networked entities must be registered.** A tower/enemy prefab not in `DefaultNetworkPrefabs` will throw at
  `Spawn()`. The card will look fine and then fail at play time. NGO auto-adds on import here, so check for a
  **duplicate** rather than assuming you need to append (recipes §B6).
- **A card only appears in a match if its `CardType` is in a deck.** The card list SO makes it available to the
  deck-builder UI; a `DebugHand`'s `Deck` is what actually gets drawn in dev play.
- **Do not trust hardcoded asset paths — including the ones in this skill.** Several drifted between the skill
  being written and the port (list SOs gained `_` prefixes, tower card prefabs moved into `Towers/`, the old
  `DEBUG_Hand.asset` no longer exists). Resolve with `find_assets` and verify before writing.

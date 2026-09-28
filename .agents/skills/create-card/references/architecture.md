# Card Architecture — reference map

Everything under `Assets/Scripts/Gameplay/Cards/`. This is the source of truth for touch-points and
serialized field names. Read it before building; verify a type/field still exists before relying on it.

## Runtime pipeline (how a card is played)

1. Server draws a `CardType` for the local player → `IOnLocalDrawnACard.OnLocalDrawACard`.
2. `CardUIFactory` looks up the `CardDataSO` by type in `CardDataListSO`, then asks each
   `BaseCardSubFactory.CanHandle(data)` and the first match `Create(...)`s the card UI (instantiates
   `cardDataSO.CardPrefab` and `Initialize`s it). **Sub-factories dispatch on `data is XxxCardDataSO`.**
3. `AbstractCard` handles drag. On `OnEndDrag` it runs validation (`CanPlayCardAt` + `CanPlayCardAtCanvas`),
   then `ActivateCard(worldPos)`.
4. `ActivateCard` predicts mana spend and sends a request to the family's **deployer**
   (`BaseCardTowerDeployer` / `BaseCardSpellDeployer` / `BaseCardSpawnEnemyDeployer`) via a Server RPC.
5. The deployer (server) re-validates, looks up the data by `CardType` in `CardDataListSO`, casts to the
   expected subclass, does the gameplay (spawn tower / run spell executor / send enemy), and replies with a
   result struct. The client confirms or reverts the predicted mana.

**Key consequence:** deployers and UI sub-factories are generic per family. Adding a card to an existing
family requires **zero** new deployer/factory code — only data + enum + registration (+ executor for spells).

## Routing is by C# type, not by enum

`CardUIFactory` → `factory.CanHandle(data)` is `data is TowerCardDataSO` / `SpellCardDataSO` /
`SpawnEnemyCardDataSO`. Each deployer casts `cardData is XxxCardDataSO`. So the **class of the data asset**
determines the family. `CardType` is just a unique id; `ExistingTypesOfCard` is informational (UI). If you
create the asset as the wrong subclass, nothing handles it.

## Touch-point matrix

Legend: ✅ always · ⤳ only if that enum value / entity is new · — n/a

| Touch-point | Spell | Tower | Enemy |
|---|---|---|---|
| `Enums.cs` → append `CardType.<X>` | ✅ | ✅ | ✅ |
| `Enums.cs` → append family enum (`SpellType`/`TowerType`/`EnemyType`) | ⤳ | ⤳ | ⤳ |
| Card data SO asset (`SpellCardDataSO`/`TowerCardDataSO`/`SpawnEnemyCardDataSO`) | ✅ | ✅ | ✅ |
| Card prefab (clone of the family's card prefab, repoint `cardDataSo`) | ✅ | ✅ | ✅ |
| Register card asset in `CardDataListSO.asset` | ✅ | ✅ | ✅ |
| Gameplay data SO (`SpellDataSO`/`TowerDataSO`/`EnemyDataSO`) | ✅ | ⤳ | ⤳ |
| Register gameplay data in its list SO (`SpellDataListSO`/`TowerDataListSO`/`EnemyDataListSO`) | ✅ | ⤳ | ⤳ |
| `ISpellExecutor` script + register in `SpellExecutorFactory` | ✅ | — | — |
| Networked gameplay prefab (tower/enemy) + register in `DefaultNetworkPrefabs.asset` | — | ⤳ | ⤳ |
| New combat scripts (`Server/Client<X>TowerCombat`) | — | ⤳(new behavior) | — |
| Add `CardType.<X>` to a `DebugHand` deck (to actually draw it) | ✅ | ✅ | ✅ |

## Enums (`Assets/Scripts/Enums.cs`) — APPEND ONLY

Serialized by integer in assets, so order == value and must never change. **Read the file for the current
members — do not trust a snapshot here.** An earlier version of this document listed a `CardType` that ended at
`SpellIce=5`; the enum has grown well past that, and acting on the stale list would have appended a duplicate
value. `cat Assets/Scripts/Enums.cs` takes a second and is always right.

Only these mappings are stable enough to write down, because other code depends on the exact numbers:

- `ExistingTypesOfCard`: None=0, **Tower=1, Spell=2, Enemy=3**. (Set `CardDataSO.ExistingType` to match the family.)
- `CardRarityType`: None=0, Common=1, Rare=2, Epic=3, Legendary=4. (Set `CardDataSO.Rarity`.)

`CardType`, `SpellType`, `TowerType` and `EnemyType` are append-only and read live. `CardType` in particular is
also serialized into the player save as an int (`JsonUtility`), so inserting mid-enum silently remaps every
saved deck.

## Serialized field names (for `set_serialized_field`)

Data SO fields are **public**, so the serialized name is just the field name. The card prefab's `cardDataSo` is
**protected**, but `set_serialized_field` reaches it the same way (it goes through `SerializedObject`):
`--target <prefabPath> --component SpellCard --field cardDataSo --value <cardAssetPath>`.

- `CardDataSO` (base): `CardType`, `ExistingType`, `Rarity`, `CardPrefab` (typed `AbstractCard`!), `CardName`,
  `Description`, `CardImage`, `CardColor`, `Cost`, `UseCustomSizeCardInMenu`, `CustomSizeCardInMenu`,
  `UseCustomPositionCardInMenu`, `CustomPositionCardInMenu`, plus the progression pair `OverrideStatGrowth`
  and `StatGrowth` (`List<CardStatGrowth>`). Leave the override **off** unless the card must diverge from its
  rarity's default table in `CardProgressionSettings`. `GetStats(CardLevelScale)` is overridden per family and
  feeds the inspector level preview — a new family field that should scale belongs in that override too.
- `SpellCardDataSO` adds: `SpellType`, `CanUseInEnemyMap`, `CanUseInLocalMap`, `SpellGhostSprite`, `SpellData` (→`SpellDataSO`).
- `TowerCardDataSO` adds: `TowerType`, `TowerGhostSprite`, `TowerPrefab` (→`GameObject`).
- `SpawnEnemyCardDataSO` adds: `EnemyType`.
- `SpellDataSO`: `SpellType`, `Range`, `TravelTime`, `VisualPrefab`. Subclasses each add their own stat:
  `SpellOffensiveDataSO`→`Damage`, `SpellEffectDataSO`→`Duration`, `SpellBuffDataSO`→`AttackSpeedBonus`
  (Haste), `SpellRageDataSO`→`MoveSpeedBonus` (Rage). Pick the subclass that already carries the stat you
  need rather than adding a field to the base.
- `TowerDataSO`: `TowerType`, per-level `{Setup,Damage,Range,ShootCooldown,BulletSpeed}Level{1,2,3}` (note `MaxLevel` is `readonly`=3). `ExplosionTowerDataSO` adds `ExplosionRangeLevel{1,2,3}`.
- `EnemyDataSO`: `EnemyType`, `EnemyName`, `MaxHealth`, `MoveSpeed`, `SpawnDuration`, `Damage`, `EnemySprite`, `EnemyPrefab` (→`GameObject`, self-reference to its own prefab).
- List SOs — **field name, then the asset it lives on** (note the `_` prefixes; they are inconsistent, so
  always resolve the path with `unity command find_assets --type <ListSOType>` instead of typing it):
  `CardDataListSO.CardDataList` → `Assets/ScriptableObjects/Cards/_CardDataListSO.asset`;
  `SpellDataListSO.SpellDataList` → `Assets/ScriptableObjects/Spells/SpellDataListSO.asset` (no underscore);
  `TowerDataListSO.TowerDataList` → `Assets/ScriptableObjects/Towers/_TowerDataListSO.asset`;
  `EnemyDataListSO.EnemyDataList` → `Assets/ScriptableObjects/Enemies/_EnemyDataListSO.asset`.
- `DebugHand.Deck` (`List<CardType>`). `NetworkPrefabsList.List` (each element `{ Override, Prefab, ... }`).
- Appending to any of these from the CLI is the array-path recipe in `recipes.md` §B4: read
  `<Field>.Array.size`, set it to N+1, then write `<Field>.Array.data[N]`.

**`CardDataSO.CardPrefab` is typed `AbstractCard`** (a Component), so assign the card prefab's
`GetComponent<AbstractCard>()`, not the GameObject. `TowerPrefab` / `EnemyPrefab` are `GameObject`.

## The card prefab (why we clone, never hand-build)

Card prefabs are **variants of `Assets/Prefabs/Cards/CardBase.prefab`** with the family card component
(`SpellCard`/`TowerCard`/`SpawnEnemyCard`) added and references wired: `cardDataSo`, `layersSettings`,
`gfxController` (`CardGFXController`), `rectTransform`, `selfCanvasGroup`, and (tower/spell) `fadeInFeedback`/
`fadeOutFeedback` (`MMF_Player` with nested feedback graphs). The only per-card-unique field is `cardDataSo`.
So **clone the closest existing card prefab and repoint `cardDataSo`** — it preserves all the feedback wiring.

Card prefabs, all under `Assets/Prefabs/Cards/` — spells and enemies at the top level, **towers in a
`Towers/` subfolder**: `CardSpellFireball`, `CardSpellIce`, `CardSpellHaste`, `CardSpellRage`,
`CardEnemySpawn`, `CardEnemyMiniBoss`, `CardSpawnEnemyArmy`, and `Towers/CardCircleTower`,
`Towers/CardSquareTower`, `Towers/CardSlamTower`, `Towers/CardDartTower`. `CardBase.prefab` is the base
they all vary from — clone a sibling, never `CardBase` directly.

## Gameplay-entity systems (the "full end-to-end" parts)

### Spell → executor
`CardSpellDeployer` (server): looks up `SpellDataSO` in `SpellDataListSO` by `SpellType`, gets
`SpellExecutorFactory.GetExecutor(spellType)`, spends mana, `executor.Execute(SpellExecutionContext{ ServerPosition,
CasterTeam, SpellData, CoroutineRunner=this })`, and RPCs a cosmetic visual (`SpellDataSO.VisualPrefab`,
optional `CosmeticSpellProjectile`). An `ISpellExecutor` is a plain C# class (`void Execute(SpellExecutionContext)`).
`FireballExecutor` casts `SpellData` to `SpellOffensiveDataSO`, then on a coroutine after `TravelTime` damages
enemies of the caster's team within `Range` via `EnemyRegistry.ActiveEnemies` → `enemy.ServerHealth.TakeDamage`.
**Register every new executor in `SpellExecutorFactory._executors`.**

### Tower → networked prefab + combat
`CardTowerDeployer` (server): finds the closest valid `IPlaceable` for the team, checks mana/occupancy/upgrade,
then `Instantiate(towerCardData.TowerPrefab)` + `NetworkObject.SpawnWithOwnership(clientId)`. A tower prefab is a
`NetworkObject` with `TowerManager` (refs: `towerDataSO`, `networkObject`, `serverTowerCombat`, `clientTowerCombat`,
`entityTeam`), a concrete `BaseServerTowerCombat` (e.g. `ServerCircleTowerCombat` overriding `TryTriggerShot`), a
concrete `BaseClientTowerCombat`, `EntityTeam`, and GFX (`ClientTowerGFX`). Behavior is chosen by **which concrete
combat components are on the prefab** — there is no `TowerType` switch. New stats only → reuse a combat type. New
firing logic → new `Server<Id>TowerCombat : BaseServerTowerCombat` (+ client). `TowerDataSO` is per-level stats;
`ExplosionTowerDataSO` is the AoE variant. Register the tower prefab in `DefaultNetworkPrefabs`.

### Enemy → networked prefab
`SpawnEnemyCard` → `CardSpawnEnemyDeployer` → `ServerWaveManager.SendEnemyFromPlayer(enemyType, authId)`:
resolves `EnemyDataSO` from `EnemyDataListSO` by `EnemyType` and spawns it on the **opponent's** map. `SpawnEnemy`
does `Instantiate(enemyData.EnemyPrefab)` + `NetworkObject.Spawn()`. An enemy prefab is a `NetworkObject` with
`EnemyManager` (refs: `enemyData`, `networkObject`, `serverEnemyMovement` `ServerEnemyMovement`,
`serverEnemyHealth` `ServerEnemyHealth`, `entityTeam`, `enemyPathAssignment`). `EnemyDataSO.EnemyPrefab` points
back at its own prefab. Register the enemy prefab in `DefaultNetworkPrefabs`. (Wave enemies are pooled via
`EnemyNetworkPool.RegisterPrefab` from `WaveDataSO.GetAllEnemyPrefabs()`; a player-summon-only enemy just needs
the data-list entry + the Netcode prefab registration. To also use it in waves, add it to a `WaveDataSO`.)

## Netcode prefab registry

`Assets/DefaultNetworkPrefabs.asset` (`Unity.Netcode.NetworkPrefabsList`, field `List`, each element `{ Override:0,
Prefab:{...}, SourcePrefabToOverride, SourceHashToOverride, OverridingTargetPrefab }`). Any prefab spawned at
runtime via `NetworkObject.Spawn()`/`SpawnWithOwnership()` (towers, enemies) must be in this list. Card prefabs are
**UI**, not networked — they do **not** go here.

## Decks / hands (so the card is actually drawn)

`DebugHand` SOs (field `Deck : List<CardType>`) are the dev decks that get distributed and drawn. They live in
`Assets/ScriptableObjects/CardHand/` — **list them live** (`unity command find_assets --type DebugHand`) and ask
the user which one to touch; there is no single canonical `DEBUG_Hand.asset` (an older version of this document
named one that no longer exists). `HandData.Distribute` shuffles the deck, locks cards whose `Cost > maxMana`,
and draws from the queue. The real deck-builder UI (`DeckUIController`) lists everything in the card list SO, so
registering there makes a card available to equip; adding to a `DebugHand` makes it appear immediately in dev play.

Registering in the card list SO also makes the card **rollable as a match reward** — `WeightedRewardRoller`
picks a rarity and then a card within it, so a new card starts appearing in end-of-match payouts as soon as it
is in the list. That is usually what you want; if not, say so at the manifest gate.

## File index

- Cards: `Assets/Scripts/Gameplay/Cards/` — `Card/AbstractCard.cs`, `Card/CardDataSO.cs`, `Card/CardDataListSO.cs`,
  `Card/CardValidation.cs`, `Card/CardUIFactory/*`, `Spells/*` (`SpellCard.cs`, `SpellCardDataSO.cs`,
  `SpellExecutorFactory.cs`, `Executors/*`, `ImplementationsSO/*`), `Tower/*`, `Enemy/*`, `Deployers/*`.
- Gameplay entities: `Assets/Scripts/Gameplay/Towers/*`, `Assets/Scripts/Gameplay/Enemies/*`, `Assets/Scripts/Gameplay/Waves/*`.
- Enums: `Assets/Scripts/Enums.cs`. Interfaces: `Assets/Scripts/Interfaces.cs`.
- Assets: card data `Assets/ScriptableObjects/Cards/**`, spells `Assets/ScriptableObjects/Spells/**`, towers
  `Assets/ScriptableObjects/Towers/**`, enemies `Assets/ScriptableObjects/Enemies/**`; registries
  `_CardDataListSO.asset` / `SpellDataListSO.asset` / `_TowerDataListSO.asset` / `_EnemyDataListSO.asset`
  (see the list-SO entry above for full paths — the `_` prefix is inconsistent), plus
  `Assets/DefaultNetworkPrefabs.asset`.
- Prefabs: `Assets/Prefabs/Cards/**` (card UI, towers under `Cards/Towers/`), tower/enemy gameplay prefabs
  under `Assets/Prefabs/**`.

## Known gotchas

- **An unregistered executor fails silently.** `SpellExecutorFactory.GetExecutor` returns `null` for a
  `SpellType` that is not in its dictionary, and the spell just does nothing server-side — no exception, no
  log. Ice used to be exactly this bug (the type and `IceExecutor` existed, the registration did not); it has
  since been fixed, and today Fireball, Ice, Haste and Rage are all registered. **Keep that invariant: every
  `SpellType` appears in `_executors`.**
- Enum reordering corrupts existing saved data — append only.
- Wrong data-SO subclass → card silently unhandled by every sub-factory/deployer.
- Missing `DefaultNetworkPrefabs` entry → tower/enemy throws at spawn, not at creation.
- **NGO auto-add-on-import is ON here:** creating/cloning a prefab with a `NetworkObject` auto-registers it in
  `DefaultNetworkPrefabs`. Do not also append manually or you get a duplicate (NGO warns; can break spawning).
  Use the idempotent dedupe-and-ensure block (recipes §B6) and verify the prefab appears exactly once.

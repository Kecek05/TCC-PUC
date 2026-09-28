# Recipes — exact Unity Pipeline CLI calls

Copy-paste and substitute the `<Placeholders>`. Run everything from the project root
(`E:\UnityProjects\TCC-PUC`) so the CLI auto-discovers the running Editor.

**The split:** C# source is edited **directly on disk** with the normal Read/Edit/Write tools and then
compiled with `unity command recompile`. Assets, prefabs and wiring go through `unity command`. Never
hand-edit `.asset` / `.prefab` / `.meta`.

**Three rules that bite (all verified against this project):**

1. **A successful write is not a landed write.** `set_serialized_field` returns `success: true` even when
   the value is silently dropped (type mismatch). Always read back with `get_serialized_fields`.
2. **`--value` shapes:** enums are **bare** (`--value SpellFireball`, not `'"SpellFireball"'`); object
   references are a bare **path or guid string**, not a JSON object; numbers and booleans are bare.
3. **`eval` needs `--timeout 60000`.** The 5000 ms default is too tight (a trivial call measured ~3.3 s), and
   a blown budget both fails the call and writes an Error into the Unity console that will confuse §6.

---

## 0. Preflight

```bash
unity command editor_status
```
Expect `{"status":"ready","compiling":false,"domainReloadInProgress":false,...}`. `settling` means the Editor
is still importing — wait and re-poll. If no instance is found, ask the user to open the project in Unity.

Then resolve the paths you are about to touch, instead of trusting a snapshot:
```bash
unity command find_assets --type CardDataListSO       # the card registry
unity command find_assets --type DebugHand            # candidate dev decks
unity command find_assets --type SpellDataListSO      # or TowerDataListSO / EnemyDataListSO
```
Re-read `architecture.md` and `Assets/Scripts/Enums.cs` for current enum values and field names.

---

## 1. Phase A — C# (must compile before Phase B)

### 1a. Append enum members (`Assets/Scripts/Enums.cs`) — APPEND ONLY
Edit the file directly. Add the new member **after the current last member** of the enum, before the `}`.
Do the same for `SpellType` / `TowerType` / `EnemyType` only if that value is new.
**Never reorder or insert in the middle** — values are serialized by int across every deck and card asset.

### 1b. Spell executor script
Write `Assets/Scripts/Gameplay/Cards/Spells/Executors/<Id>Executor.cs` directly:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class <Id>Executor : ISpellExecutor
{
    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellOffensiveDataSO data)   // or SpellEffectDataSO / SpellBuffDataSO / SpellRageDataSO
        {
            GameLog.Error("<Id>Executor: SpellData is not the expected type");
            return;
        }

        context.CoroutineRunner.StartCoroutine(Run(context.ServerPosition, context.CasterTeam, data));
    }

    private IEnumerator Run(Vector2 position, TeamType team, SpellOffensiveDataSO data)
    {
        yield return new WaitForSeconds(data.TravelTime);

        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];
            if (enemy == null || !enemy.NetworkObject.IsSpawned) continue;
            if (enemy.Team.GetTeamType() != team) continue;             // matches Fireball: hits the caster's own inbound lane
            if (Vector2.Distance(position, enemy.transform.position) <= data.Range)
                enemy.ServerHealth.TakeDamage(data.Damage);
        }
    }
}
```
Model new behavior on `Executors/FireballExecutor.cs` (damage), `HasteExecutor.cs` / `RageExecutor.cs`
(persistent zone buffs — read the buff-system notes in the project `CLAUDE.md` before writing one).

If the card level scale must apply, resolve it once per cast from `context.Scale` — `Add*Buff` and
`Remove*Buff` must be passed the identical value or the target keeps a stack it can never shed.

### 1c. Register the executor (`SpellExecutorFactory.cs`)
Edit `Assets/Scripts/Gameplay/Cards/Spells/SpellExecutorFactory.cs` and add a line to the initializer:
```csharp
private static readonly Dictionary<SpellType, ISpellExecutor> _executors = new()
{
    { SpellType.Fireball, new FireballExecutor() },
    { SpellType.Ice, new IceExecutor() },
    { SpellType.Haste, new HasteExecutor() },
    { SpellType.Rage, new RageExecutor() },
    { SpellType.<Id>, new <Id>Executor() },
};
```
Every `SpellType` that exists is currently registered here. Keep that invariant — an unregistered spell
resolves to a null executor and fails silently server-side.

### 1d. Tower combat scripts (only for genuinely new tower behavior)
Create `Assets/Scripts/Gameplay/Towers/Server/Concrete/Server<Id>TowerCombat.cs` (`: BaseServerTowerCombat`,
override `TryTriggerShot()`) and `.../Client/Concrete/Client<Id>TowerCombat.cs` (`: BaseClientTowerCombat`).
Model on `ServerCircleTowerCombat` / `ClientCircleTowerCombat`. For stat-only towers, **skip** and reuse the
Circle combat components.

### 1e. Compile gate — do not skip
```bash
unity command recompile
# then poll until completed / up_to_date:
unity command recompile_status
```
`recompile` answers `{"status":"up_to_date"}` immediately when nothing changed; after a real edit it returns
`triggered` and you poll `recompile_status`. The server keeps the Editor ticking while unfocused, so this
works with the window in the background. Then confirm a clean console:
```bash
unity command get_console_logs --severity Error --limit 20
```
Phase B commands can only reference **already-compiled** types — a new `CardType` member does not exist for
`set_serialized_field` until this gate passes.

---

## 2. Phase B — building blocks

### B1. Clone a card prefab
```bash
unity command copy_asset \
  --asset "Assets/Prefabs/Cards/CardSpellFireball.prefab" \
  --destination "Prefabs/Cards/Card<Id>.prefab"
```
Paths are relative to the authoring root (`Assets`); the `Assets/` prefix is optional on `--destination`.
Add `--dry_run true` first if you want to validate. The clone keeps all feedback/GFX wiring; only
`cardDataSo` is per-card unique (set in B3). Pick the closest same-family card prefab as the source —
tower card prefabs live under `Assets/Prefabs/Cards/Towers/`.

### B2. Create a gameplay-data SO
```bash
unity command create_asset --path "ScriptableObjects/Spells/Spell<Id>Data.asset" --type SpellOffensiveDataSO
unity command set_serialized_field --target "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset" --field SpellType   --value <Id>
unity command set_serialized_field --target "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset" --field Range      --value <range>
unity command set_serialized_field --target "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset" --field TravelTime --value <travel>
unity command set_serialized_field --target "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset" --field Damage     --value <dmg>
# VisualPrefab is an object reference — a bare path:
unity command set_serialized_field --target "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset" --field VisualPrefab --value "<visualPrefabPath>"
```
`--type` accepts the short type name. Substitute the subclass and its own stat field:
`SpellOffensiveDataSO`→`Damage`, `SpellEffectDataSO`→`Duration`, `SpellBuffDataSO`→`AttackSpeedBonus`,
`SpellRageDataSO`→`MoveSpeedBonus`. `TowerDataSO` / `EnemyDataSO` follow the same pattern (see §4 / §5).

Then register it in its list SO with the append recipe in **B4**, targeting the family list
(`SpellDataList` / `TowerDataList` / `EnemyDataList`).

### B3. Create the card data SO + wire the circular ref
```bash
unity command create_asset --path "ScriptableObjects/Cards/CardSpells/Spell<Id>CardData.asset" --type SpellCardDataSO
C="Assets/ScriptableObjects/Cards/CardSpells/Spell<Id>CardData.asset"

unity command set_serialized_field --target "$C" --field CardType     --value <X>            # bare enum
unity command set_serialized_field --target "$C" --field ExistingType --value Spell
unity command set_serialized_field --target "$C" --field Rarity       --value <Rarity>
unity command set_serialized_field --target "$C" --field CardName     --value "<Name>"
unity command set_serialized_field --target "$C" --field Description  --value "<Description>"
unity command set_serialized_field --target "$C" --field Cost         --value <cost>
unity command set_serialized_field --target "$C" --field CardImage    --value "<cardImageAssetPath>"
# family fields:
unity command set_serialized_field --target "$C" --field SpellType        --value <Id>
unity command set_serialized_field --target "$C" --field SpellData        --value "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset"
unity command set_serialized_field --target "$C" --field SpellGhostSprite --value "<ghostSpritePath>"
unity command set_serialized_field --target "$C" --field CanUseInEnemyMap --value <true|false>
unity command set_serialized_field --target "$C" --field CanUseInLocalMap --value <true|false>

# CardPrefab is typed AbstractCard (a Component) — pass the plain PREFAB PATH, the CLI resolves
# the matching component on it (verified: a card prefab path lands as its SpellCard component):
unity command set_serialized_field --target "$C" --field CardPrefab --value "Assets/Prefabs/Cards/Card<Id>.prefab"

# the other half of the circular ref — cardDataSo is protected, but set_serialized_field reaches it:
unity command set_serialized_field \
  --target "Assets/Prefabs/Cards/Card<Id>.prefab" --component SpellCard \
  --field cardDataSo --value "$C"
```
Use the family's card component for `--component`: `SpellCard` / `TowerCard` / `SpawnEnemyCard`.

**A sprite path that points at the wrong asset type will report success and leave the field `None`.**
Read it back in §6.

### B4. Append to a list SO (card registry, family data list)
There is no dedicated "append" command; use the array SerializedProperty path — read the size, resize by
one, write the last slot (verified working):
```bash
L="Assets/ScriptableObjects/Cards/_CardDataListSO.asset"          # resolve with find_assets, do not assume
unity command get_serialized_fields --target "$L" --field "CardDataList.Array.size"        # -> N
unity command set_serialized_field --target "$L" --field "CardDataList.Array.size"     --value $((N+1))
unity command set_serialized_field --target "$L" --field "CardDataList.Array.data[N]"  --value "$C"
```
Same shape for `SpellDataList` / `TowerDataList` / `EnemyDataList` on their own list assets.
Note the underscore prefixes on several of these assets (`_CardDataListSO`, `_TowerDataListSO`,
`_EnemyDataListSO`) — `SpellDataListSO` has none. Always resolve with `find_assets`.

### B5. Add the card to a dev deck (so it is drawn in play)
`DebugHand.Deck` is a `List<CardType>` — the same resize-and-set pattern, with a bare enum value:
```bash
D="Assets/ScriptableObjects/CardHand/DEBUG_Hand_OnlyTowers.asset"   # confirm the deck with the user first
unity command get_serialized_fields --target "$D" --field "Deck.Array.size"       # -> N
unity command set_serialized_field --target "$D" --field "Deck.Array.size"    --value $((N+1))
unity command set_serialized_field --target "$D" --field "Deck.Array.data[N]" --value <X>
```

### B6. Ensure a networked prefab (towers/enemies) is in `DefaultNetworkPrefabs` — IDEMPOTENT
**Heads-up (verified the hard way):** this project has NGO **auto-add-on-import** enabled, so the moment you
copy/create a prefab with a `NetworkObject`, NGO **already registers it**. Blindly appending gives you a
**duplicate** (NGO warns; it can break spawning). So inspect first, and only add if genuinely absent. This is
the one place `eval` earns its keep, because it is a scan-and-repair rather than a single field write:

```bash
unity command eval --timeout 60000 --code '
var npl = AssetDatabase.LoadAssetAtPath<Object>("Assets/DefaultNetworkPrefabs.asset");
var target = AssetDatabase.LoadAssetAtPath<GameObject>("<entityPrefabPath>");
var s = new SerializedObject(npl);
var L = s.FindProperty("List");
var seen = new System.Collections.Generic.HashSet<Object>();
int i = 0; bool present = false;
while (i < L.arraySize)
{
    var p = L.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue;
    if (p == target) present = true;
    if (p == null || !seen.Add(p)) L.DeleteArrayElementAtIndex(i); else i++;
}
if (!present)
{
    int idx = L.arraySize; L.InsertArrayElementAtIndex(idx);
    var e = L.GetArrayElementAtIndex(idx);
    e.FindPropertyRelative("Override").enumValueIndex = 0;
    e.FindPropertyRelative("Prefab").objectReferenceValue = target;
    e.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
    e.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
}
s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(npl);
AssetDatabase.SaveAssets();
return "present=" + present + " count=" + L.arraySize;
'
```
In §6, confirm the prefab appears **exactly once**. Card (UI) prefabs do **not** go here — only
runtime-spawned tower/enemy prefabs.

### B7. Save
Asset commands write through the AssetDatabase already. To flush open scene edits (only if you touched a
scene), use `unity command save_all`. After a batch of asset writes it is still worth one:
```bash
unity command eval --timeout 60000 --code 'AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); return "saved";'
```

---

## 3. Per-family checklists

### Spell (existing-family path)
A(enums: `CardType.<X>`, `SpellType.<Id>` if new) → A(executor 1b + register 1c) → **1e compile gate** →
B2(spell data + its list SO) → B1(clone `CardSpellFireball`) → B3(`SpellCardDataSO` + wire both directions) →
B4(card list SO) → B5(deck) → §6 verify. No new deployer/factory/sub-factory.

### Tower
A(enums: `CardType.<X>`, `TowerType.<Id>` if new; + combat scripts 1d **only if new behavior**) → **1e** →
**§4 (tower entity)** if the tower prefab/data does not exist yet → B1(clone
`Assets/Prefabs/Cards/Towers/CardCircleTower.prefab`) → B3(`TowerCardDataSO`: set `TowerType`,
`TowerGhostSprite`, `TowerPrefab`=the tower prefab path) → B4 → B5 → §6.

### Enemy
A(enums: `CardType.<X>`, `EnemyType.<Id>` if new) → **1e** → **§5 (enemy entity)** if it does not exist →
B1(clone `CardEnemySpawn`) → B3(`SpawnEnemyCardDataSO`: set `EnemyType`) → B4 → B5 → §6.
(`SpawnEnemyCard` has no position/ghost; the simplest card.)

---

## 4. New tower gameplay entity (full end-to-end)

1. **`TowerDataSO`** (or `ExplosionTowerDataSO`): `create_asset` under `ScriptableObjects/Towers/`, then
   `set_serialized_field` for `TowerType` and the per-level stats
   (`{Setup,Damage,Range,ShootCooldown,BulletSpeed}Level{1,2,3}`). Register it in the tower list SO (B4).
2. **Tower prefab:** clone the closest existing tower prefab —
   find it by reading an existing card's `TowerPrefab`:
   ```bash
   unity command get_serialized_fields \
     --target "Assets/ScriptableObjects/Cards/CardTower/Circle_TowerCardData.asset" --field TowerPrefab
   ```
   `copy_asset` it, then repoint `TowerManager.towerDataSO` at the new data asset:
   ```bash
   unity command set_serialized_field --target "<newTowerPrefabPath>" --component TowerManager \
     --field towerDataSO --value "<newTowerDataPath>"
   ```
   If the tower has **new behavior**, swap the combat components for your `Server<Id>TowerCombat` /
   `Client<Id>TowerCombat` (`remove_component` / `add_component`) and rewire
   `TowerManager.serverTowerCombat` / `clientTowerCombat` plus their cross-refs. For nested-prefab-safe
   structural edits use `save_prefab_contents`.
3. **B6** — confirm the tower prefab is in `DefaultNetworkPrefabs` exactly once (it is spawned via
   `SpawnWithOwnership`).
4. `TowerCardDataSO.TowerPrefab` (set in B3) points at this prefab.

## 5. New enemy gameplay entity (full end-to-end)

1. **`EnemyDataSO`:** `create_asset`, then set `EnemyType`, `EnemyName`, `MaxHealth`, `MoveSpeed`,
   `SpawnDuration`, `Damage`, `EnemySprite`. Leave `EnemyPrefab` for step 3. Register in the enemy list SO (B4).
2. **Enemy prefab:** clone an existing enemy prefab (the one with `EnemyManager`, found via an existing
   `EnemyDataSO.EnemyPrefab`), then point it at the new data:
   ```bash
   unity command set_serialized_field --target "<newEnemyPrefabPath>" --component EnemyManager \
     --field enemyData --value "<newEnemyDataPath>"
   ```
3. **Close the self-reference:** `EnemyDataSO.EnemyPrefab` → the cloned prefab path.
4. **B6** — register the enemy prefab (spawned via `NetworkObject.Spawn()`).
5. Optional: to use it in scripted waves too, add it to a `WaveDataSO.Waves[*].waveEnemies`.

---

## 6. Verify (Phase C — mandatory)

```bash
unity command get_console_logs --severity Error --limit 50
```
Ignore any `Main thread operation timed out after 5000ms` entries — those are the pipeline server's own
`eval` timeouts, not your card. Everything else must be clean.

Then **read every write back** — a `success: true` from `set_serialized_field` does not prove the value landed:
```bash
# 1. the card asset's own fields, all of them at once:
unity command get_serialized_fields --target "$C"

# 2. the circular ref, from the prefab side:
unity command get_serialized_fields --target "Assets/Prefabs/Cards/Card<Id>.prefab" \
  --component SpellCard --field cardDataSo

# 3. the card is in the registry (size grew, last slot is the new asset):
unity command get_serialized_fields --target "$L" --field "CardDataList.Array.size"

# 4. the deck contains the new CardType:
unity command get_serialized_fields --target "$D" --field "Deck.Array.size"

# 5. towers/enemies: the entity prefab appears exactly once in DefaultNetworkPrefabs (B6 returns the count)
```
Check specifically that no object-reference field came back `None` when you set it — that is the silent
type-mismatch failure. Report placeholder-art TODOs (CardImage / ghost / visual / enemy sprite) and tell the
user to enter Play with the chosen deck to test.

# Recipes — exact Unity MCP calls

Copy-paste and substitute the `<Placeholders>`. Everything runs through the live Unity bridge. C# source
goes through `Unity_CreateScript` / `Unity_ManageScript`; assets/prefabs/wiring through `Unity_RunCommand`.

`Unity_RunCommand` rules: class **must** be `internal class CommandScript : IRunCommand`, with
`public void Execute(ExecutionResult result)`. `using UnityEngine;` and `using UnityEditor;` are available;
add others (`using Unity.Netcode;`, `using System.Collections.Generic;`) as needed. Log with
`result.Log("...")`. Don't use top-level statements.

---

## 0. Preflight

```
Unity_GetProjectData(maxAssetItems=1, maxOutputChars=300, maxTaxonomyDepth=1)   // bridge alive?
```
If it errors, ask the user to open the project in Unity, then retry. Re-read `architecture.md` for the
current enum values and exact field names before generating any code.

---

## 1. Phase A — C# (must compile before Phase B)

### 1a. Append enum members (`Assets/Scripts/Enums.cs`) — APPEND ONLY
1. `Unity_ManageScript(action="read", name="Enums", path="Assets/Scripts/")` → get text + `precondition_sha256`,
   and find the line of the family's last member (e.g. `SpellIce` in `CardType`, before the `}`).
2. `Unity_ManageScript(action="apply_text_edits", name="Enums", path="Assets/Scripts/", precondition_sha256=<sha>, edits=[...])`
   with a range edit that inserts `,\n    <NewMember>` right after the last member. Each edit item is
   `{startLine,startCol,endLine,endCol,newText}` (a zero-width range at the end of the last-member line inserts).
   Example: after `    SpellIce` insert `,\n    SpellPoison`. Do the same for `SpellType`/`TowerType`/`EnemyType`
   only if that value is new. **Never reorder or insert in the middle** (values are serialized by int).

### 1b. Spell executor script
Call `Unity_CreateScript(Path="Assets/Scripts/Gameplay/Cards/Spells/Executors/<Id>Executor.cs",
ScriptType="MonoBehaviour", Contents=<the file body below>)`. The body is plain C# — use normal single
double-quotes in the real file:
```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class <Id>Executor : ISpellExecutor
{
    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellOffensiveDataSO data)   // or SpellEffectDataSO
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
Model new behavior on `Assets/Scripts/Gameplay/Cards/Spells/Executors/FireballExecutor.cs`.

### 1c. Register the executor (`SpellExecutorFactory.cs`)
`Unity_ManageScript(action="apply_text_edits", name="SpellExecutorFactory", path="Assets/Scripts/Gameplay/Cards/Spells/")`
— insert a line into the dictionary initializer so it reads:
```
private static readonly Dictionary<SpellType, ISpellExecutor> _executors = new()
{
    { SpellType.Fireball, new FireballExecutor() },
    { SpellType.<Id>, new <Id>Executor() },
};
```

### 1d. Tower combat scripts (only for genuinely new tower behavior)
Create `Assets/Scripts/Gameplay/Towers/Server/Concrete/Server<Id>TowerCombat.cs` (`: BaseServerTowerCombat`,
override `TryTriggerShot()`) and `.../Client/Concrete/Client<Id>TowerCombat.cs` (`: BaseClientTowerCombat`).
Model on `ServerCircleTowerCombat` / `ClientCircleTowerCombat`. For stat-only towers, **skip** and reuse the
Circle combat components.

> After Phase A, wait for compilation, then run a no-op `Unity_RunCommand` (or `Unity_GetConsoleLogs`) and
> confirm **no compile errors** before Phase B — `Unity_RunCommand` can't reference uncompiled types.

---

## 2. Phase B — building blocks (`Unity_RunCommand`)

Helper conventions used below: `AssetDatabase.LoadAssetAtPath<T>(path)`; sprites by path via
`AssetDatabase.LoadAssetAtPath<Sprite>(path)` (if it's a sub-asset of a texture, use
`AssetDatabase.LoadAllAssetsAtPath(path)` and pick by name). `EditorUtility.SetDirty(obj)` after mutating an
existing asset; `AssetDatabase.CreateAsset(newObj, path)` for new ones. End with `AssetDatabase.SaveAssets()`
+ `AssetDatabase.Refresh()`.

### B1. Clone a card prefab and repoint `cardDataSo`
```csharp
string src = "Assets/Prefabs/Cards/CardSpellFireball.prefab";        // closest same-family card prefab
string dst = "Assets/Prefabs/Cards/Card<Id>.prefab";
AssetDatabase.CopyAsset(src, dst);
// cardDataSo is set in B3 (needs the card asset first). The clone keeps all feedback/GFX wiring.
```

### B2. Create a gameplay-data SO (public fields → typed assignment)
```csharp
// Spell example (offensive):
var spellData = ScriptableObject.CreateInstance<SpellOffensiveDataSO>();   // or SpellEffectDataSO
spellData.SpellType  = SpellType.<Id>;
spellData.Range      = <range>;
spellData.TravelTime = <travel>;
spellData.Damage     = <dmg>;                                              // Duration for SpellEffectDataSO
spellData.VisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("<visualPrefabPathOrNull>");
AssetDatabase.CreateAsset(spellData, "Assets/ScriptableObjects/Spells/Spell<Id>Data.asset");
// register:
var spellList = AssetDatabase.LoadAssetAtPath<SpellDataListSO>("Assets/ScriptableObjects/Spells/SpellDataListSO.asset");
spellList.SpellDataList.Add(spellData); EditorUtility.SetDirty(spellList);
```
TowerDataSO / EnemyDataSO follow the same pattern (see B6/B7 for their prefabs + list SOs).

### B3. Create the card data SO + wire the circular ref
```csharp
var card = ScriptableObject.CreateInstance<SpellCardDataSO>();          // family subclass!
card.CardType     = CardType.<X>;
card.ExistingType = ExistingTypesOfCard.Spell;                         // Tower / Spell / Enemy
card.Rarity       = CardRarityType.<Rarity>;
card.CardName     = "<Name>";
card.Description  = "<Description>";
card.Cost         = <cost>;
card.CardImage    = AssetDatabase.LoadAssetAtPath<Sprite>("<cardImagePath>");
// family fields:
card.SpellType        = SpellType.<Id>;
card.SpellData        = spellData;                                     // from B2
card.SpellGhostSprite = AssetDatabase.LoadAssetAtPath<Sprite>("<ghostPath>");
card.CanUseInEnemyMap = <bool>;  card.CanUseInLocalMap = <bool>;
// link to the cloned prefab (CardPrefab is typed AbstractCard → assign the component):
var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cards/Card<Id>.prefab");
card.CardPrefab = prefabGo.GetComponent<AbstractCard>();
AssetDatabase.CreateAsset(card, "Assets/ScriptableObjects/Cards/CardSpells/Spell<Id>CardData.asset");

// set the prefab's protected cardDataSo (SerializedObject, via prefab contents):
var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Cards/Card<Id>.prefab");
var so = new SerializedObject(root.GetComponent<AbstractCard>());
so.FindProperty("cardDataSo").objectReferenceValue = card;
so.ApplyModifiedPropertiesWithoutUndo();
PrefabUtility.SavePrefabAsset(root);
PrefabUtility.UnloadPrefabContents(root);
```

### B4. Register the card in `CardDataListSO`
```csharp
var list = AssetDatabase.LoadAssetAtPath<CardDataListSO>("Assets/ScriptableObjects/Cards/CardDataListSO.asset");
list.CardDataList.Add(card); EditorUtility.SetDirty(list);
```

### B5. Add the card to a DEBUG deck (so it's drawn in play)
```csharp
var hand = AssetDatabase.LoadAssetAtPath<DebugHand>("Assets/ScriptableObjects/CardHand/DEBUG_Hand.asset");
hand.Deck.Add(CardType.<X>); EditorUtility.SetDirty(hand);
```

### B6. Register a networked prefab (towers/enemies only) in `DefaultNetworkPrefabs`
```csharp
using Unity.Netcode;
var npl = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
npl.Add(new NetworkPrefab { Prefab = entityPrefabGo });   // entityPrefabGo = the tower/enemy prefab GameObject
EditorUtility.SetDirty(npl);
```
Fallback if `NetworkPrefabsList.Add` differs across the NGO version — append via SerializedObject:
```csharp
var s = new SerializedObject(npl); var L = s.FindProperty("List");
int i = L.arraySize; L.InsertArrayElementAtIndex(i);
var e = L.GetArrayElementAtIndex(i);
e.FindPropertyRelative("Override").enumValueIndex = 0;            // NetworkPrefabOverride.None
e.FindPropertyRelative("Prefab").objectReferenceValue = entityPrefabGo;
s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(npl);
```
Card (UI) prefabs do **not** go here — only runtime-spawned tower/enemy prefabs.

### Always finish a RunCommand with
```csharp
AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
result.Log("Created <X>: <files>");
```

---

## 3. Per-family checklists

### Spell (existing-family path)
A(enums: `CardType.<X>`, `SpellType.<Id>` if new) → A(executor 1b + register 1c) → compile-check →
B2(spell data + SpellDataListSO) → B1(clone `CardSpellFireball`) → B3(SpellCardDataSO + wire) →
B4(CardDataListSO) → B5(deck) → verify. No new deployer/factory/sub-factory.

### Tower
A(enums: `CardType.<X>`, `TowerType.<Id>` if new; + combat scripts 1d **only if new behavior**) → compile-check →
**§4 (tower entity)** if the tower prefab/data doesn't exist yet → B1(clone `CardCircleTower`) →
B3(`TowerCardDataSO`: set `TowerType`, `TowerGhostSprite`, `TowerPrefab`=the tower GameObject) →
B4 → B5 → verify.

### Enemy
A(enums: `CardType.<X>`, `EnemyType.<Id>` if new) → compile-check → **§5 (enemy entity)** if it doesn't exist →
B1(clone `CardEnemySpawn`) → B3(`SpawnEnemyCardDataSO`: set `EnemyType`) → B4 → B5 → verify.
(`SpawnEnemyCard` has no position/ghost; the simplest card.)

---

## 4. New tower gameplay entity (full end-to-end)

1. `TowerDataSO` (or `ExplosionTowerDataSO`): `CreateInstance`, set `TowerType` + per-level stats, `CreateAsset`
   under `Assets/ScriptableObjects/Towers/`. Register in `TowerDataListSO.asset` (`.TowerDataList.Add`).
2. Tower prefab: **clone the closest existing tower prefab** (find it via the GUID in `Circle_TowerCardData.asset`'s
   `TowerPrefab`, or search `Assets/Prefabs/**` for the tower with `TowerManager`). `AssetDatabase.CopyAsset` →
   open with `LoadPrefabContents` → set `TowerManager.towerDataSO` (SerializedObject) to the new `TowerDataSO`;
   if new behavior, swap the combat components for your `Server<Id>TowerCombat`/`Client<Id>TowerCombat` and rewire
   `TowerManager.serverTowerCombat`/`clientTowerCombat` + their cross-refs → `SavePrefabAsset`.
3. **B6 — register the tower prefab in `DefaultNetworkPrefabs`** (it's spawned via `SpawnWithOwnership`).
4. The `TowerCardDataSO.TowerPrefab` (set in B3) points to this prefab GameObject.

## 5. New enemy gameplay entity (full end-to-end)

1. `EnemyDataSO`: `CreateInstance`, set `EnemyType`, `EnemyName`, `MaxHealth`, `MoveSpeed`, `SpawnDuration`,
   `Damage`, `EnemySprite`; leave `EnemyPrefab` for step 3. `CreateAsset` under `Assets/ScriptableObjects/...`.
   Register in `EnemyDataListSO.asset` (`.EnemyDataList.Add`).
2. Enemy prefab: clone an existing enemy prefab (the one with `EnemyManager`, found via an existing
   `EnemyDataSO.EnemyPrefab`). `CopyAsset` → `LoadPrefabContents` → set `EnemyManager.enemyData` to the new
   `EnemyDataSO` (and adjust the sprite renderer if desired) → `SavePrefabAsset`.
3. Set `enemyDataSO.EnemyPrefab = clonedEnemyGo; EditorUtility.SetDirty(enemyDataSO);` (self-reference).
4. **B6 — register the enemy prefab in `DefaultNetworkPrefabs`** (spawned via `NetworkObject.Spawn()`).
5. Optional: to use it in scripted waves too, add it to a `WaveDataSO.Waves[*].waveEnemies`.

---

## 6. Verify

```
Unity_GetConsoleLogs(logTypes="Error,Warning", maxEntries=50)     // clean compile + no asset errors?
```
Then confirm via a small `Unity_RunCommand` that logs: the card asset loads, `CardDataListSO` contains it,
`prefab.GetComponent<AbstractCard>()`'s serialized `cardDataSo` equals the card asset, and (tower/enemy) the
entity prefab is present in `DefaultNetworkPrefabs.List`. Report placeholder-art TODOs (CardImage / ghost /
visual / enemy sprite) and tell the user to enter Play with the DEBUG deck to test.
```

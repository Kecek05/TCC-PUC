# Project Context

Multiplayer tower defense game (Clash Royale-style) — university project (TCC - PUC).
Stack: Unity, Netcode for GameObjects, Odin Inspector, DOTween.

## User Preferences

- Values SOLID principles and scalable architecture
- Prefers discussion about design trade-offs before implementing
- Works across multiple PCs

## Architecture Decisions

### Card Validation System

Card activation uses a composable validation chain via `CardValidation` struct (not raw bool or Predicate<T>).

**Why:** Cards have shared base predicates (mana cost) and type-specific ones (buildable needs position check, spell does not). A bool loses the failure reason needed for UI feedback. A Predicate delegate loses Unity Inspector visibility and stack clarity.

**How it works:**
- `AbstractCard` implements `ICardActivatable` and owns the base mana check in `CanPlayCard()`
- `CanPlayCardAt()` defaults to `CanPlayCard()` — cards that don't need position checks (spells) inherit this for free
- Subclasses override and chain `base.CanPlayCard()` / `base.CanPlayCardAt()` to add their own checks (e.g. BuildableCard adds position + waitingResult)
- `CardValidation` struct carries `IsValid` + `CardInvalidReason` enum, with `implicit operator bool` for ergonomic if-checks
- `OnEndDrag` in AbstractCard handles the validate-then-activate flow — subclasses only override the three ICardActivatable methods
- Key files: `CardValidation.cs`, `ICardActivatable.cs`, `AbstractCard.cs`, `BuildableCard.cs` (all under `Assets/Scripts/Gameplay/Cards/`)

### Buff System — stacking speed modifiers (enemies)

Persistent buff spells (e.g. Rage) apply their effect through an additive **accumulator** on the entity, not a single multiplier — the enemy-side twin of the tower attack-speed buff (`BaseServerTowerCombat.AddAttackSpeedBuff`/`RemoveAttackSpeedBuff`).

**Why:** Multiple independent sources (overlapping Rage zones, a future slow) must stack and be removed independently without clobbering each other. `ServerEnemyMovement`'s original single `SetSpeedMultiplier` (last-writer-wins) couldn't express "+20% from zone A *and* +20% from zone B, each removable on its own." Keeping buffs as a separate accumulated term also means slows/upgrades that recompute base speed never wipe an active buff.

**How it works:**
- `ServerEnemyMovement` composes speed from independent terms: `effective = baseSpeed * slowMultiplier * (1 + speedBuffPercent)`, recomputed in `RecalculateSpeed()`. `SetSpeedMultiplier` feeds the slow term; `AddSpeedBuff(p)`/`RemoveSpeedBuff(p)` (clamped ≥ 0) feed the additive buff term.
- **Zero bandwidth:** no new NetworkVariable — the speed change replicates for free via the existing `PathProgress` sync; buffs are pure server-side state.
- Zone spells run one server coroutine (`RageExecutor`, modeled on `HasteExecutor`): re-scan each tick, apply the bonus **once per troop per cast** (tracked in a dict), and on expiry remove exactly this cast's own contribution. Rage adds a per-troop **linger** (buff persists N seconds after a troop leaves the radius / the zone ends).
- **Team targeting:** an enemy's `EntityTeam` is set to the *map it attacks* in `ServerWaveManager.SpawnEnemy` — identically for wave and player-sent troops — so an enemy-field spell filters `enemyTeam != casterTeam && != None` to hit both kinds. Mirrors `IceExecutor`.
- Key files: `ServerEnemyMovement.cs`, `RageExecutor.cs`, `SpellRageDataSO.cs` (under `Assets/Scripts/Gameplay/`), with `BaseServerTowerCombat.cs` (tower-side twin) and `HasteExecutor.cs` (the zone pattern) as references.

### Bot AI — fallback opponent (single-player)

When the host waits alone in `WaitingForPlayersState` past a timeout (default 30s), a bot fills the empty **Blue** slot and the match starts. The bot is a **server-side "virtual player" represented by data only — not a network client**.

**Why:** Players are keyed by AuthId (string), not clientId; there are no per-player NetworkObjects (`CreatePlayerObject = false`). So a bot needs only a synthetic AuthId + `PlayerData` + a Blue team assignment — no relay connection. This mirrors the existing `*_DEBUG` single-player stand-ins.

**How it works:**
- **Seating** (`BotController.SeatBot`, server-only): pick a random deck from `BotDeckListSO`, `PlayersDataManager.RegisterBot(authId, data)` (auth→data only, no clientId map), `TeamManager.AssignTeamForAuthId(authId)` (host holds Red → bot lands on Blue), and `MapTranslator.MarkPlayerInitialized(Blue)` (satisfies the `LoadingMatch→MatchReady` gate that a real client normally clears via `InitializeTeamServerRpc`). Triggered from `WaitingForPlayersState.Tick` via `ctx.BotController` (carried on `GameFlowContext`).
- **Playing cards:** the three deployers expose a team-parameterized **core** extracted from their `[Rpc(SendTo.Server)]` bodies — `BaseCardTowerDeployer.TryDeployTower`, `BaseCardSpellDeployer.TryDeploySpell`, `BaseCardSpawnEnemyDeployer.TryDeploySpawnEnemy`. Humans reach them via the RPC (team from `SenderClientId`); the bot calls them directly. One shared path keeps mana-spend + spawn + `TriggerOnCardDeployed` (hand advance) DRY. Bot towers spawn with `NetworkManager.ServerClientId` ownership.
- **Deciding:** `IBotBrain.Decide(BotContext) → BotDecision` (pure, side-effect-free strategy; `HeuristicBotBrain` ships as default). A server coroutine ticks each `BotSettingsSO.DecisionInterval`±jitter during `InMatch`, reading live state (mana, hand, base HP, `EnemyRegistry`/`TowerRegistry`, cached Blue placeables) all in **server-space** (no MapTranslator round-trip). Priority: defend own lane (place/upgrade tower, Fireball, Haste) → attack (troops, Ice, Rage) → hold a mana reserve.
- **Client-less integration:** `ServerCardHandManager` guards its per-client draw-sync RPCs (skips when `GetClientIdByTeamType` returns `ulong.MaxValue`, i.e. the bot). Commit-the-match: leaving `WaitingForPlayers` fires `GameFlowContext.CommitMatch` (in `WaitingForPlayersState.Exit`), which both flips `IMatchAdmission.StopAcceptingPlayers()` (on `NetworkConnectionServer`, gating `ApprovalCheck`) and calls `BaseHostManager.CloseLobbyToNewPlayers()` (stops the lobby heartbeat + deletes the discovery lobby, host keeps running) — covering the bot and 2-human paths alike.
- Key files: `BotController.cs`, `HeuristicBotBrain.cs`, `BotContext.cs`, `IBotBrain.cs`, `BotDeckListSO.cs`, `BotSettingsSO.cs` (under `Assets/Scripts/Gameplay/Bot/`); the `BotController` GameObject lives in `GameScene`; assets under `Assets/ScriptableObjects/Bot/`.

### Player Save — 5 deck slots + collection ordering

The player owns **5 persistent deck slots** and a collection sort preference, stored as JSON in
`Application.persistentDataPath`. `UserData.DeckCards` is no longer edited directly — it is a **mirror**
of whichever slot is active.

**Why:** there was no save system at all; `UserData` was `new`'d every launch, so deck edits died with the
process and a fresh player started with an empty deck and could never press Battle (`ConnectionManagerUI.CanPlay()`
requires exactly `DeckSize` cards). Putting deck rules in a save manager rather than the UI gives one owner
of deck state, and hiding storage behind `IPlayerSaveRepository` means Unity Cloud Save can replace the file
backend later without touching the deck page. `JsonUtility` + a `[Serializable]` POCO matches the idiom
`UserData.TranslateToBytes` already uses.

**How it works:**
- `BasePlayerSaveManager` (abstract, plain C#) owns `PlayerSaveData` — 5 `DeckSaveData` slots, the active
  index and the sort preference — and enforces the rules: `TryEquipCard` refuses a full deck or a duplicate,
  `SetDeckCards` copies rather than aliases. Every mutation persists immediately (the file is a few hundred bytes).
- `PlayerSaveManager` is created in `ClientManager.Awake()` beside `ClientAuth` (so it is loaded before
  `Loader.Load(MainMenu)`, which the deck page reads in `Start`) and registered as `BasePlayerSaveManager`.
  `Load()` **normalizes** whatever it reads — resizes to `DeckSlotCount`, drops unknown/duplicate/`None` cards,
  trims to `DeckSize`, clamps the active index — and rewrites the file if anything moved. A missing or corrupt
  file degrades to a starter save, never an exception.
- **Two events, two consumers:** `OnActiveDeckContentChanged` → `ClientManager` copies into
  `UserData.SetDeckCards` (so the connection payload always matches the selected slot);
  `OnActiveDeckSlotChanged` → `DeckUIController.ApplyDeck` relayouts the page.
- **Deck area == saved order.** `LayoutDeckArea` places `deck.Cards[i]` into `cardPositions[i]`, so what the
  player sees is exactly what is saved, on every edit and after a restart. Removing a mid-deck card compacts
  the rest leftward.
- `DeckSlotBar` reuses `MenuNavButton` (the nav-bar highlight component) on `DeckEntry1..5`.
  `CardSortController` cycles Name/Rarity/Cost/Type on `TypeButton` and flips `OrderButton`'s icon 180° with
  DOTween; `CardSortComparer` is pure, ties always broken by card name so the grid order is deterministic.
  Sorting reorders `AllCardsParent` siblings only — the deck slots keep the player's order.
- **Enum drift caveat:** `JsonUtility` writes enums as ints, so new `CardType` members must keep being
  **appended** to `Enums.cs`. Inserting one mid-enum silently re-maps every saved deck (`SaveVersion` is there
  to migrate if that ever happens).
- Editor helpers: `Kecek/Debug Tools/Delete Player Save` and `Open Save Folder`.
- **Runtime reset: the Quantum Console `reset-save [skipTutorial]`** (`SaveDebugCommands.cs`). It goes through
  `BasePlayerSaveManager.ResetToDefault()` rather than deleting the file, because the save lives in memory
  for the whole session and its next write would put the old one straight back. The reset raises *every*
  save event (card progress first, so the deck page already knows what is locked when the slot change
  relays it out), then routes the way a finished boot does: a fresh save goes straight into the tutorial,
  `skipTutorial` reloads the menu instead. It refuses inside a gameplay scene: the match has dealt from the
  old deck, and the tutorial's deck loan would hand the old deck back to `UserData` on the way out. Unlike
  the editor menu item it also works in a build.
- Key files: `BasePlayerSaveManager.cs`, `PlayerSaveManager.cs`, `PlayerSaveData.cs`,
  `IPlayerSaveRepository.cs`, `FilePlayerSaveRepository.cs`, `PlayerSaveSettingsSO.cs` (under
  `Assets/Scripts/Services/PlayerSave/`); `DeckSlotBar.cs`, `CardSortController.cs`, `CardSortComparer.cs`
  (under `Assets/Scripts/UI/Menu/Pages/Deck/`); `DeckUIController.cs`, `ActionFrame.cs`; asset at
  `Assets/ScriptableObjects/PlayerSave/PlayerSaveSettings.asset`.

### Card Upgrade + Rewards — persistent card level and match payouts

Cards have a **persistent level and a copy count** in the player save. Upgrading spends copies + gold and
makes that card's own stats better in the match. Finishing a match pays gold to both players and a random
card to the winner, unlocking it at level 1 if new.

**Why:** every progression surface was a `Random.Range` placeholder (`SingleCardInDeck.SetPlaceholderProgression`,
`PlayerInfoUI`, and a `// TODO: Handle trophies and rewards` in `ServerEndGameManager`). Stat growth is a
**compounding percent per stat** rather than a hand-authored value per level, because 11 cards x ~10 levels x
3-5 stats is several hundred numbers to keep balanced; the percentages live per card so a card can still
diverge from its rarity's default. Rewards are rolled **server-side** and applied client-side because the save
is a local file — the server owns the decision, the client owns the storage.

**Two level axes — do not confuse them:**
- `BaseServerTowerCombat._towerLevel` (NetworkVariable, 1-3, reset every spawn) is the **in-match placement
  upgrade**, selecting between `TowerDataSO`'s `DamageLevel1/2/3` tiers.
- `CardLevelScale` is the **persistent card level**, a multiplier applied on top of whichever tier that
  selected. They compose; nothing in this feature touches `_towerLevel` or `TowerReason.LevelUp`.

**How it works:**
- **Authoring:** `CardProgressionSettingsSO` holds one `RarityProgression` per rarity (MaxLevel + a `[TableList]`
  of `CardLevelStep{CopiesRequired, GoldCost}`, with an Odin `[Button] AutoFill` that generates a geometric
  ramp) plus a default `CardStatGrowth` table. A card overrides growth only when it differs (`CardDataSO.OverrideStatGrowth`).
  `CardDataSO` carries an editor-only `[ShowInInspector, ReadOnly, TableList]` **live preview** of every level's
  costs and resulting stats — pure derived data, no extra state.
- **One stats API, three consumers:** `CardDataSO.GetStats(CardLevelScale)` is overridden per card family
  (tower reads its prefab's `TowerDataSO`, spell its `SpellData`, troop its `EnemyDataListSO`). It feeds the
  inspector preview and the Clash Royale-style current-vs-next panel (below), so they cannot disagree.
- **Transport, zero new plumbing:** `UserData.DeckCardLevels` is index-aligned with `DeckCards` and rides the
  existing Netcode connection payload. `DrawingCardsState` fills `MatchCardLevels` (server-only lookup) in the
  same loop that deals the decks. Deployers call `MatchCardLevels.ScaleFor(team, cardType)`.
- **Applying the scale, one choke point per family:** towers in `BaseServerTowerCombat.UpdateData()` (attack
  speed *divides* the cooldown, matching the existing haste maths); enemies via `EnemyManager.SetCardLevelScale`
  written **before `Spawn()`**, since pooled instances only re-initialise in `OnNetworkSpawn`; spells via
  `SpellExecutionContext.Scale`, because `SpellExecutorFactory` hands out shared stateless singletons.
- **Buff symmetry:** Haste/Rage resolve the scaled bonus into a local **once per cast** — `Add*Buff` and
  `Remove*Buff` must pass the identical value or the tower/troop keeps a stack it can never shed.
- **Enemy max health is now replicated** (`ServerEnemyHealth.MaxHealth`): `ClientEnemyHealth` normalised its
  bar against the shared `EnemyDataSO`, which is wrong once health scales.
- **Range deliberately does not scale by default.** `ClientTowerRangeGFX`, `ClientSlamTowerCombat` and
  `TowerCard` read range straight off the SO client-side, so scaled range would draw the wrong ring. Clash
  Royale does not scale range either. Turning it on means replicating range too.
- **Rewards — one grant path, many sources.** `BaseRewardService.Grant(Reward)` is the single door: it banks
  into the save, *then* raises `OnRewardGranted`, so a listener never reads a stale balance. It is plain C#,
  created beside `PlayerSaveManager` in `ClientManager.InitializeRewards()` and registered in `ServiceLocator`,
  so it lives as long as the save and works in **every scene** — which is the point: a daily claim or a shop
  purchase in the Main Menu grants exactly the way a finished match does. `Reward` carries a `RewardSource`
  (`Match`/`DailyReward`/`Shop`/`Debug`) for presentation and logging only; banking never branches on it.
- **The match is just one source.** `ServerEndGameManager` rolls per player and delivers each with a
  **targeted** Rpc (`RpcTarget.Single`) — the `EndGameSnapshot` broadcast is shared, a reward is private; the
  bot is skipped by its `ClientId == ulong.MaxValue` sentinel. `ClientRewardHandler` (GameScene) is an
  **adapter**, not a destination: it forwards that Rpc into `BaseRewardService` and nothing else.
  `ClientEndGameCanvas` displays off the *service*, not the Rpc, so it can only ever show a payout that
  actually banked. `WeightedRewardRoller` is the **match** roller — `Roll(bool won)` is win/lose-shaped, and
  other sources build a `Reward` themselves rather than going through `IRewardRoller`. A match whose payout
  is decided in advance swaps the roller for that match only (`BaseServerEndGameManager.OverrideRewardRoller`,
  `FixedRewardRoller`) and keeps the whole delivery path — how the tutorial is paid. It picks rarity first,
  then a card uniformly within it, and takes a `System.Random` so the distribution is seedable and testable.
- **Save:** `PlayerSaveData` v2 adds `Gold` and `List<CardProgressSaveData>`; presence in that list *is*
  ownership. `NormalizeCards` migrates a v1 save by granting whatever its decks already reference, so nobody
  loses a deck. `TryEquipCard` refuses unowned cards and `SanitizeCards` drops them from decks.
  `CardUpgradeValidation` mirrors the `CardValidation`/`TowerValidation` idiom so the UI gets a typed reason.
- **Debug helpers:** `Kecek/Debug Tools/Progression/Grant 5000 Gold` / `Grant 100 Copies To Every Card` write
  the save directly. The Quantum Console (in `GameScene`) drives the *real* pipeline instead —
  two tiers matching the two ways a reward arrives. **Match path** (server-side, GameScene, leaning on the
  pass-through seams `ServerEndGameManager.DebugGrantRewards`/`DebugSendRewardTo`): `grant-reward [team]` rolls
  and pays everyone as if that team had just won; `grant-reward-to <team> <gold> <card> <copies>` sends one
  exact reward for deterministic unlock testing. **Service path** (any scene, no server): `claim-reward <gold>
  <card> <copies> [source]` grants straight through `BaseRewardService` — the daily/shop door — so it is how
  you test rewards from the Main Menu. `reward-status` reads the save back anywhere. Bodies live in
  `RewardDebugCommands.cs` (under `Assets/Scripts/Debug/`).
- Key files: `CardProgressionSettingsSO.cs`, `CardLevelScale.cs`, `CardStatGrowth.cs`, `CardUpgradeValidation.cs`,
  `MatchCardLevels.cs` (under `Assets/Scripts/Gameplay/Progression/`); `Reward.cs`, `BaseRewardService.cs`,
  `RewardService.cs` (under `Assets/Scripts/Services/Rewards/` — scene-independent, beside `Services/PlayerSave/`);
  `IRewardRoller.cs`, `WeightedRewardRoller.cs`, `RewardSettingsSO.cs`, `ClientRewardHandler.cs` (under
  `Assets/Scripts/Gameplay/Rewards/` — the match payout specifically); assets at
  `Assets/ScriptableObjects/Progression/` and `Assets/ScriptableObjects/Rewards/`.

### Cartas v2 — support towers, the hold, and splitting troops

Six cards from the *Cartas v2* Notion table (Anel, Ancora, Espelho, Fonte, Cisma, Miragem). None of them
needed a new deployer, sub-factory or client combat: every one is a data SO + a prefab + (for the towers)
one server combat class. Realocar is deliberately **not** built — see the note at the end.

**Why these shapes:**
- **Buffs go where the existing accumulators already are.** Anel needed towers to take a *damage* bonus, so
  `BaseServerTowerCombat` gained `AddDamageBuff`/`RemoveDamageBuff` as the exact twin of the attack-speed
  pair. It is applied in **`DealDamage`**, not in `UpdateData`, because Beacon's ramp, Shard's fragments and
  Chain's bounces each derive their own number from `_damage` — multiplying at the point of the hit is the
  only place all of them pick the aura up without every combat re-implementing it.
- **A hold is not a slow.** `ServerEnemyMovement` caps stacked slows at `MaxSlowPercent` (0.85) precisely so
  control can never replace damage. Ancora is the deliberate exception, so it gets its own
  `AddHold`/`RemoveHold` counter that freezes path progress outright. Its `PullBack` drags an enemy backwards
  **along its own lane** — enemies are pinned to a path, so pulling one *toward the tower* is not expressible,
  and pulling it back down the path produces the effect the card actually wants (the wave bunching up behind
  it). `PullBack` writes `PathProgress` directly: the throttle in `Update` only ever syncs an *increase*, so a
  pull that merely lowered `_localProgress` would never reach clients.
- **Ancora restarts its cooldown on RELEASE, not on the grab.** `TryTriggerShot` returns false for the whole
  hold, which leaves the cooldown sitting full — without the explicit reset the anchor re-grabs on the next
  frame and the authored cadence gates nothing, an unbreakable lock with no window to push through.
- **Support towers reuse Prism's shape.** Anel/Ancora/Espelho/Fonte are all clones of `TowerPrism.prefab` with
  the server combat swapped and `EmptyClientTowerCombat` kept. They never shoot, so `ShootCooldown` is
  repurposed as the tick interval — which means haste, upgrades and Anel itself speed up an aura re-scan, a
  mana payout or a mirror send for free.
- **Fonte grants through `BaseServerManaManager.GrantMana`**, never by touching the pools, so the max-mana
  clamp stays in one place: a Fonte raises how *fast* the ceiling is reached, never the ceiling. Base regen is
  0.357/s, so the authored 1-per-5s is about +56% at level 1. **These numbers are first-pass and want
  playtesting.**
- **Espelho calls `SpawnEnemy` directly**, not `SendEnemyFromPlayer` — that one resolves the destination from a
  sending player's auth id, and a tower only knows its own team.
- **Cisma splits with a per-instance counter, not a prefab per generation.** `EnemyDataSO` carries
  `SplitCount`/`SplitGenerations`/`SplitChildStatPercent`; `EnemyManager` carries the remaining generations and
  a compounding `SplitStatMultiplier`. One data asset and one prefab therefore cover big -> 2 medium -> 4 small.
  The multiplier is kept **separate from `CardLevelScale`** because they mean different things (player
  investment vs. depth down the split chain), and folding them together would make a level-5 Cisma's
  grandchildren indistinguishable from a level-1's.
- **A kill is identified by reading the health, not by the event.** `ServerEnemyHealth.OnDeath` is static and
  fires for *every* despawn, a leak into the base included — the same trap `ServerShardTowerCombat` documents.
  `ServerWaveManager.TrySplit` checks `CurrentHealth <= 0`, and **defers the spawn by one frame**: it runs
  inside `OnNetworkDespawn`, and calling `NetworkObject.Spawn` there mutates NGO's spawn tables while it is
  still walking them for the despawn. All parent state is captured into arguments before the yield.
  Children are fanned out by `SplitSpreadProgress` so they do not stack into a single sprite and a single
  area-damage target.
- **`SpawnEnemy` gained `startProgress`** so a split child enters where its parent died rather than at the
  mouth of the lane. Children inherit the parent's reversed flag via `fromPlayer`, which also keeps them out of
  wave bookkeeping — **a splitter placed in a `WaveDataSO` would need the wave counter taught about children
  first**; every splitter today is a player card.
- **Miragem's decoys are a card-level concern.** `SpawnEnemyCardDataSO.BuildSpawnOrder` returns the whole
  column, real troops and decoys **shuffled together**, because a fixed order would let the defender learn
  which position is real and read straight through the bluff. The decoy is an ordinary enemy with 1 HP and 0
  damage whose `MoveSpeed` and `SpawnDuration` **must** match the real body or the bluff is readable at a
  glance.
- **`TowerCardDataSO.GetStats` now walks the data hierarchy** the way `SpellCardDataSO.GetStats` already did,
  and hides the Damage row when a tower deals none — four of these six show "Damage 0" otherwise, which reads
  as a bug rather than as a design.
- **Realocar (Feitico def., 2) is intentionally unbuilt.** Moving a tower needs a *source* and a *destination*,
  and the spell path carries exactly one position; every player-controlled version needs a second position on
  the RPC plus client selection plumbing. Left out by decision rather than oversight.
- Placeholder art throughout (Circle / BaseEnemy1 sprites + `ShowPlaceholderNameOverlay`), matching what Prism
  and Shadow already do.
- Key files: `AuraTowerDataSO.cs`, `AnchorTowerDataSO.cs`, `MirrorTowerDataSO.cs`, `ManaTowerDataSO.cs` (under
  `Assets/Scripts/Gameplay/Towers/SOs/TowerData/`); `ServerAnelTowerCombat.cs`, `ServerAncoraTowerCombat.cs`,
  `ServerEspelhoTowerCombat.cs`, `ServerFonteTowerCombat.cs` (under
  `Assets/Scripts/Gameplay/Towers/Server/Concrete/`); test deck at
  `Assets/ScriptableObjects/CardHand/DEBUG_Hand_CartasV2.asset`.

### Card Info Panel — the card page

The Details button on a card in the Main Menu opens `InfoPanelCanvas`: the card's own portrait, its name,
the level the player owns coloured by its rarity, its rarity and type, an Upgrade button priced at the next
level, and two swipeable pages — a grid of stat rows (one `StatPrefab` per stat, each showing the value **at
the level the player owns** and the gain the **next** level buys) and the card's description.

**Why:** the third consumer of `CardDataSO.GetStats(CardLevelScale)` that the progression feature was built
for. Calling it twice — at level *n* and *n+1* — is the whole feature; nothing re-derives growth, so the
panel, the inspector preview and the server can never disagree about a card's numbers. Rows are shaped
per card family by the existing `GetStats` overrides, so a spell showing Radius/Duration and a troop
showing Health/Damage/Speed cost no branching in the UI at all.

**How it works:**
- **One pure computation, one player-facing lookup.** `CardProgressionSettingsSO.GetStatProgress(card, level)`
  is the balance-only half: it pairs `GetStats` at *level* with `GetStats` at *level+1* by index (both come
  from the same override, so row i is the same stat; checked by `CardStatId` anyway) and reports `hasNext = false`
  at the rarity's cap. `BasePlayerSaveManager.GetCardStatProgress(cardType)` is the per-player half: it joins
  that with the saved level, exactly as `CanUpgradeCard` already joins the save with the cost table. A **locked**
  card previews at level 1 — the level it would unlock at — so the collection can explain a card before it is owned.
- **`CardStatProgress` decides whether there is an upgrade to announce, and it decides on the FORMATTED
  values, not the raw floats.** A Prism's 0.25s hit speed grows to 0.2451s, which still prints "0.25";
  captioning that with "-0.00" reads as a bug. If the number the player sees does not change, there is no
  upgrade — which also covers Range, sitting at 0% growth on every card.
- **Towers are authored in seconds-between-shots but displayed as "Hits Per Second"** — inverted in
  `TowerCardDataSO.GetStats`, the one place numbers become text, so `BaseServerTowerCombat` keeps reading
  `GetShootCooldownByLevel` untouched. A cooldown is a lower-is-better stat, which reads backwards in a
  table whose every other row rewards a bigger number; inverting makes every upgrade in the panel a `+`.
  It also buys precision where it matters: at 2 dp a 0.25s cooldown could not show its 2%/level gain at
  all, while 4.00/s shows `+0.08`. The two slowest support towers (Espelho 0.13/s, Fonte 0.20/s) are now
  the ones too coarse to show a per-level change.
- **A tower reports every stat once per in-match tier** — "Damage Lvl 1/2/3" — because the two level axes
  are independent and the player pays for them separately: the card level is bought in the menu, the tier
  on the board. This table is the only place the two can be seen composing. `AddPerTier` in
  `TowerCardDataSO` walks 1..`MaxLevel` through the existing `GetXByLevel` accessors, so the panel reads
  the same path combat does rather than the `*Level1` fields.
- **A stat that is flat across tiers collapses to one unlabelled row.** Three identical "Lvl 1/2/3" rows
  say nothing and cost three of the panel's ten slots, which is what pushes the rows that *do* differ out
  of view. Torniquete's and Anel's hit rate and Espelho's range are flat by design, so this is the common
  case for support towers, not an edge one. It also keeps the row **shape** a property of the authored data
  alone (the card level multiplies every tier by the same number), which is what lets
  `GetStatProgress` keep pairing current with next by index.
- That pairing now compares the **label**, not just the `CardStatId`: three "Damage Lvl n" rows share
  `CardStatId.Damage`, so the id alone would happily pair tier 1 with tier 2.
- **The stat grid scales itself to fit, rather than being sized to a worst case.** `StatsParent` sits inside
  the page scroll at 681x449 with 300x80 cells and 30x22 spacing — 2 fixed columns x 4 rows, so 8 stats.
  Circle reports 9 and Anel 10, which need a fifth row there is no height for. `FitStatGrid` shrinks the
  whole grid uniformly until the rows fit: at 5 rows the factor is 0.88, and the table renders inside the
  681x449 it was authored to occupy. **Never above 1** — a two-row card blown up to fill the panel would
  read as a different widget from the eight-row card beside it, so short cards are untouched.
- **`statsBottomMargin` (20 by default) is held back from the fit, not from the rect.** A grid scaled to
  exactly fill its area ends flush against the frame, which is what a shrunk card looked like before. The
  margin comes off the height the scale may use, so the table simply stops short; leaving it out of the
  rect is what makes the gap a constant 20 on screen rather than something that shrinks with the grid —
  i.e. that gets smallest exactly when the table is tallest and needs it most.
- **It scales `StatsParent` and widens its rect by the same factor; it does not shrink `cellSize`.**
  StatPrefab's three labels are anchored to its top-left corner at a fixed font size, so a shorter cell
  would clip them rather than fit them. Scaling the parent takes the text down with everything else and
  keeps every row pixel-identical to the design, only smaller.
- **The grid is authored `FixedColumnCount = 2` on purpose.** Flexible derives the column count from the
  rect width — the same width the fit just widened — so it would change the column count under the maths
  that widened it, and the row count with it. The authored cell, spacing and *rendered* area are captured
  once in `Awake` (`rect.size * localScale`, so a fit accidentally saved into the scene recovers rather
  than compounds), and every fit scales from those rather than from what the last card left behind.
- **`InfoPanelData` carries the card, and nothing else.** It started generic — pre-resolved stat rows and
  no `CardDataSO` — but the panel now *is* the card page: it embeds a `SingleCardInDeck` portrait, prints
  the rarity and type, and sells the next level. A generic contract in front of that would be a fiction, so
  the struct is just `{ CardDataSO Card }` (built with `InfoPanelData.ForCard`) and the panel resolves the
  rest from `BasePlayerSaveManager` itself.
- **The panel reads the save directly because it can change it.** `DeckUIController` owns every save lookup
  on the *deck page*; the panel is its own surface and owns its own, for one decisive reason — the Upgrade
  button lives here, so a snapshot handed in at open time would be stale the instant it is tapped. Instead
  the panel subscribes to `OnCardProgressChanged` **while visible** and redraws level, copies, cost and the
  whole stat table off the save. Buying a level updates the panel that bought it; closing unsubscribes, so
  a reward banked elsewhere never redraws a panel nobody is looking at.
- The refusal text moved onto `CardUpgradeValidation.WarningMessage`. Two doors now lead to an upgrade — the
  deck popup and this panel — and a reason must not be explained with different words depending on which.
- **The portrait is the same `Card_V2` widget the collection grid uses**, with its level pill and its button
  deleted on that instance: the panel prints the level itself, larger, and a portrait is not a tap target.
  Every reference `SingleCardInDeck` does not need for that trimmed layout is therefore optional, and
  `Initialize` takes the `DeckUIController` as an optional argument.
- **One widget serving many cards forced a latent bug out.** `Initialize` only wrote `CardImage`'s rect when
  the card ticked `UseCustomPosition/SizeCardInMenu`, which is invisible on the deck page (a widget is
  initialised once, for one card) but leaks on the panel: a custom-positioned card would leave its offset
  on the next card shown. The rect is now always written, against defaults captured on the first
  `Initialize` — not in `Awake`, which has not run yet for a widget instantiated under an inactive page.
- **`CardRarityType.None` is fully transparent in `CardsRarityData`**, and it is where a freshly authored
  card starts. Painting the level and rarity labels with it would make them vanish, so a zero-alpha rarity
  falls back to the colour the label was authored with.
- `StatEntryUI` is deliberately dumb, the twin of `RewardEntryUI`: three labels, no knowledge of the card.
- Rows are **pooled, not rebuilt** (unlike `ClientEndGameCanvas`'s rewards area, which runs once a match):
  this panel opens on every card tap and cards differ by a row or two. Surplus rows are deactivated, and a
  `GridLayoutGroup` skips inactive children so they leave no hole.
- **The page strip is reset to the stats page on every open.** The panel is hidden, never destroyed, so the
  `ScrollRect` keeps the offset the previous card was left on — reopening would land on the description
  with the stat table already swiped off screen. `StopMovement()` first: the inertia from the last swipe
  would otherwise carry it straight back off. Reset *after* `contentObject.SetActive(true)`, so the
  ScrollRect measures a laid-out viewport.
- Key files: `CardStatProgress.cs`, `CardUpgradeValidation.cs` (under `Assets/Scripts/Gameplay/Progression/`),
  `StatEntryUI.cs`, `BaseInfoPanelService.cs`, `InfoPanelService.cs` (under
  `Assets/Scripts/UI/Menu/InfoPanelService/`), `SingleCardInDeck.cs`, `ActionFrame.cs`; prefabs at
  `Assets/Prefabs/UI/Elements/StatPrefab.prefab` and `Assets/Prefabs/UI/Menu/DeckPage/Card_V2.prefab`, wired
  into `MainMenu/UI/InfoPanelCanvas/InfoPanel/SafeArea/Panel`.

### Armor Break — the color-resist clear became a scalable fraction

Torniquete (tower) and Ferrugem (spell) used to strip off-color armor resistance **completely, at every
level**. Both now strip a *fraction* of it that grows with the card's persistent level.

**Why:** the card info panel made it visible that Torniquete was the only card in the set whose defining
ability gained nothing from a level — its whole table was Range (0% growth by design) and a 0.25s aura
re-scan. `ServerEnemyHealth` held `_colorResistCleared` as a count of *sources* and read it as
`cleared > 0 ? 0f : OffColorResistance`, so there was no magnitude anywhere to scale. Levelling it bought
nothing.

**How it works:**
- `_colorResistClearPercent` is now the **additive sum of each source's fraction**, the exact twin of
  `ServerEnemyMovement._slowPercent`: a source adds and removes only its own contribution, so overlapping
  auras and zones never clobber each other on expiry. `TakeDamage` reads
  `OffColorResistance * (1 - Clamp01(sum))`. At 1 the armor stops mattering entirely — bit-for-bit the old
  behaviour — so the change is a pure generalisation, not a new rule.
- **Clamped on read, not on the accumulator.** Stacked sources may over-subscribe; they simply cannot push
  resistance below zero and start healing the target. Clamping on write would break the add/remove pairing.
- `ResistTowerDataSO` / `SpellResistDataSO` are clones of `SlowTowerDataSO` / `SpellSlowDataSO` — same
  shape, the fraction just means armor stripped rather than speed removed. Both scale by
  `CardLevelScale.EffectBonus`, like every other percentage effect in the game.
- **The tower stores the applied amount PER ENEMY** (`Dictionary<EnemyManager, float>`, copied from
  `ServerPrismTowerCombat`); the spell resolves it **once per cast** into a local (copied from Haste/Rage).
  Both exist for the same reason: `Add` and `Remove` must be handed the identical number or the enemy keeps
  a strip it can never shed. A placement upgrade mid-hold is exactly what would strand one.
- **This is a level-1 nerf and a max-level wash, deliberately.** Against the 0.35-resistance wave enemies a
  Torniquete used to turn 65% damage into 100% (+53%); it now gives +32% at level 1 and +50% at level 10.
  That gap is the whole point — it is what a level buys. **First-pass numbers, want playtesting**: tower
  0.60/0.75/0.90 per placement tier, spell 0.50.
- Both cards gained an **"Armor Break"** row in the info panel, clamped to 100% so it can never promise more
  than the aura applies. Tourniquet reads `60% (+3%)` at level 1, `93.1%` at 10.
- **Migrating an SO's script type leaves already-loaded referencing objects holding a dead pointer** —
  `TowerManager.Data` and `SpellCardDataSO.SpellData` both read null until a domain reload, even though the
  YAML guid/fileID never changed and `SerializedObject` resolved fine. Reimport is not enough;
  `EditorUtility.RequestScriptReload()` (or any recompile) is what fixes it.
- Key files: `ResistTowerDataSO.cs`, `SpellResistDataSO.cs`, `ServerTorniqueteTowerCombat.cs`,
  `FerrugemExecutor.cs`, `ServerEnemyHealth.cs`; assets `Torniquete_TowerData.asset`,
  `SpellFerrugemData.asset`.

### Tutorial / FTUE — a scripted match, then a scripted menu

A new player boots into `TutorialScene` instead of the Main Menu, plays a scripted match against a bot and
then plays it out to a normal ending they cannot lose, is paid a card they do not own on the normal end
screen, and is then walked through equipping and upgrading it and searching for a real match from the main
page. One bool in the save decides all of it.

**Why:** there was no first-time experience at all — a fresh save dropped straight into the Main Menu with
a starter deck and no explanation of mana, placement, the level-up gesture or the enemy-field half of the
board. The two halves are split because the things being taught are: the match teaches gestures, the menu
teaches progression, and progression is only teachable once the player owns something to spend on.

**How it works:**
- **One persistent bit, everything else in memory.** `PlayerSaveData.TutorialCompleted` (save v3) is all
  that is written; `BaseTutorialService.Phase` and the reward card live in RAM. A player who quits halfway
  starts the tutorial over rather than resuming into a menu step with no match behind it. The v2 -> v3
  migration **grants** the flag: a save written before the tutorial existed belongs to someone who already
  knows the game.
- **The gate is one line.** `ClientManager.DoAuth` ends in `RouteAfterAuth()` instead of
  `Loader.Load(MainMenu)`. `BaseTutorialService` is created beside the save and the reward service for
  exactly the reason those are — the tutorial spans AuthBootstrap, TutorialScene and MainMenu, so nothing
  living in one of them could carry state across the other two.
- **The tutorial hosts itself, offline.** `HostManager.StartLocalHostAsync(scene)` skips the Relay
  allocation, the join code and the discovery lobby: `StartHost()` on loopback, then the scene load. It
  needs no internet, starts instantly, and publishes nothing, so a stranger can never join a scripted match.
  `ShutdownHostAsync` and `CloseLobbyToNewPlayers` already tolerate an empty lobby id, so teardown is shared
  with the relay path.
  - **It binds port 0 (OS-assigned), not the authored 7777.** Nothing connects to this host, so the port
    means nothing, while a fixed one is a shared resource: a second Editor, a running build, or a socket
    leaked by an earlier play session all hold 7777, and `StartHost()` then fails and drops a first-time
    player into the Main Menu with no tutorial. Found live: the Editor process itself held a leaked
    `127.0.0.1:7777` that survived domain reloads. The relay paths call `SetRelayServerData`, which replaces
    the connection data wholesale, so they are unaffected.
- **`TutorialScene` is a copy of `GameScene`** (chosen over a tutorial-mode flag so the tutorial board can
  diverge). The copy immediately exposed a latent bug: `NetworkConnectionServer` gated player-loaded on the
  literal string `GameScene`, so in the copy nobody ever counted as loaded, no team was assigned, and the
  match sat in `WaitingForPlayers` forever. Anything asking "are we in a match scene" must now go through
  **`Loader.IsGameplayScene`** — a check that names one scene silently does nothing in the other.
- **The player is lent a deck.** `TutorialService` swaps `UserData.DeckCards` for
  `TutorialSettingsSO.TutorialDeck` for the match and swaps it back on handover; the save is never touched.
  Scripting "place a tower" is only safe when a tower is guaranteed to be in the deck, and the starter deck
  has **no troop card at all**, so the SendTroop step could never have completed on it. The authored deck is
  **4 cards** (Dart, SpawnEnemy1, Rage, Fireball) and is **exactly `HandSize`** on purpose: the whole deck
  is therefore always in hand, which is what brings the tower card straight back for the level-up step and
  keeps both spells available when their steps name them by type. That slot is what the second tower card
  (Stinger) was traded for — two towers taught nothing the one does not, while the two spells are cast on
  opposite fields for opposite reasons.
  - **The script guarantees its own deck is affordable.** `RefillMana` also lifts the player's mana ceiling
    to the costliest card in the deck (`RaiseManaCapForDeck`): the shared mana table drops the cap to **4**
    at wave 1 — `ServerManaManager` applies the wave-1 override at spawn — while Rage costs **5**, so the
    step asking for it would point at a card that can never be paid for and could only time out. That is the
    same failure `RefillMana` already exists to prevent, one layer up. Raised only, only for the player's own
    team (the bot keeps the table's value, so this buys the script its cards rather than the tutorial an
    easier opponent), and re-applied every frame an action step waits, so a later wave's cap cannot undo it.
    Read **from the deck**, not authored, so re-authoring the deck can never strand a card no step can pay for.
  - **The ceiling also decides what is dealt.** `ServerCardHandManager` only deals cards the current max
    mana can pay for, and re-deals on `OnMaxManaChanged`. Raised only from the first action step, Rage was
    held out of the hand through Welcome/Mana/Hand — the Hand step described a 3-card hand, and the fourth
    card popped in a step later. The raise therefore also runs **once before the script's first line**.
- **Steps are one class configured with delegates, not a class each.** `TutorialStep` is a builder
  (`.Tap()`, `.CompletesWhen()`, `.Pointing()`, `.GivingUpAfter()`) and the whole script reads as one list in
  the director, whose closures capture the subscriptions that complete each step
  (`BaseCardTowerDeployer.OnPlaceResult` for place vs. `TowerReason.LevelUp`, `CardDeploymentBus` filtered to
  the local team for troop/spell, `CameraSlide.SideChanged` for the table swap). `IGameFlowState` earns its
  classes because states hold state; these do not. **Every action step carries a timeout** — the one failure
  a first-time experience must not have is a dead end. **A new step id is appended to `TutorialStepId`, never
  inserted**: `TutorialCopySO` serializes the enum as ints, so slotting one into the middle silently re-keys
  every line after it onto the wrong step. What order the steps run in is the director's list, not the enum.
- **A waiting step freezes the world** (`Time.timeScale = 0`, driven by `TutorialSequence`, opt out per step
  with `.Running()`). One line stops waves, towers, projectiles and mana regen at once without a single
  system learning about the tutorial — and a player reading an instruction should not be losing their base
  while they read it. Four things had to follow from it:
  - **Timeouts are measured in `Time.unscaledTime`.** Scaled, freezing would switch the dead-end safety net
    off exactly where it matters most.
  - **A step that asks for a card closes the rest of the hand** (`Asking(...)`, which pairs the mana top-up
    below with a per-frame `ApplyHandLock`). The ask and the lock are the same question — can the player do
    what they were just told to, and only that — and a misdrop otherwise spends both the card the step is
    waiting on and the mana it needs, leaving them stuck until it times out. Blocked at the **raycast**
    (`AbstractCard.SetInteractable`), so no drag begins at all and none of the per-family drag code (a
    spell's ghost, a tower's preview) runs on a card that may not be played; the overlay's dim already says
    which card is meant, so the lock needs no visual of its own. It is **immediate-mode** — cleared at the
    top of `Update`, re-asserted by the waiting step's own tick — so settling, a timeout, a skipped step and
    a Skip all reopen the hand without any step remembering to, and a hand that re-deals mid-step is covered
    for free. A predicate nothing matches leaves the hand **open** rather than closing all of it. It is the
    fine half of a two-part gate: the overlay's input shield (below) holds everything outside the
    highlighted hole, and this settles the hand itself, where the hole's 24-unit padding can take in the
    edge of the neighbouring card.
  - **Mana is topped up every frame an action step waits** (`.WhileWaiting(RefillMana)`), not on entry. A
    frozen step regenerates none, so a player who spent down to nothing would be stuck on an instruction
    they cannot carry out; and on-entry alone is not enough, because the deploy that completed the
    *previous* step spends on the server when its Rpc lands, which can be after the next step already
    refilled.
  - **Three tweens had to become unscaled**: `AbstractCard`'s drop-return (or the card hangs wherever it was
    dropped), and `CameraSlide`'s `TweenCameraTo`/`SnapBack` (or the camera strands half-way between the two
    fields on the very swipe the tutorial just asked for). All three are responses to a gesture the player
    just made, so unscaled is the right answer in normal play too.
  - **An action's result gets to finish before the next step freezes it** (`.SettlingUntil(...)`). The
    *request* completes under a frozen clock, but what it starts does not. A tower spawns as a 0.01-scale
    dot (its `MMF_Player` scale-in is 0.7s of scaled time) inside a `SetupDuration` window that is also
    scaled. The next step froze it right there, so the level-up step pointed at a tower the player could
    not see. Once a settling step completes, `TutorialSequence` unfreezes, hides the overlay (its dim and
    hand would sit between the player and what they did), and enters the next step only when the predicate
    holds. The wait is bounded (3s, unscaled). Both tower steps settle on `IsLastBuiltTowerSettled`:
    `BaseServerTowerCombat.IsSettingUp` and `ClientTowerGFX.IsPlayingLevelFeedback` both false, read off
    the tower the place result landed on (the host reads server state directly). Measured: placing settles
    in ~0.73s and levelling up in ~0.91s, about 1.6s of live match in total, well inside the waves' 7s
    `InitialDelay`. Settling was chosen over making tower visuals unscaled: it is one mechanism in the
    tutorial rather than a timescale audit of every prefab an action can touch, and it gives each action a
    visible beat. **SendTroop and CastSpell have the same shape** (a troop's `SpawnDuration`, a spell's
    cast) and carry no `.SettlingUntil` — SendTroop gets that beat anyway from the running step below it,
    CastSpell still has none.
  - Verified under a frozen clock: the placement Rpc round-trip, the level-up, the troop, the spell and both
    swipes all complete at `timeScale = 0`.
- **Three beats mid-match are deliberately live; the first is `TroopDirection`.** A `.Running()` tap step right after
  SendTroop, holding while the troop walks — because what a troop *does* is where it goes, and a frozen one
  demonstrates nothing. A player-sent troop is spawned `fromPlayer: true`, which becomes
  `ServerEnemyMovement`'s **`reversed`** flag: it enters the opponent's lane at the end **their** own waves
  finish at and marches back up it, against the traffic. Letting the clock run is what puts that on screen —
  their waves come down the same lane while yours climbs it — and this is the only place the tutorial shows
  that the sending goes both ways, which the bot does to the player for the rest of the match.
  - **Its Continue is held back 2.5s** (`TroopWatchSeconds`, via `TutorialStep.Tap(continueAfter:)`): the beat
    is watched, not read, and a player tapping on at once never saw the troop move. The button is *hidden*
    until then rather than shown and ignored — a button that does nothing when pressed reads as broken.
    `TutorialSequence` shows the line without Continue and reveals it through `SetContinueVisible` once the
    step has held long enough. Verified live with a 5s delay: line up and Continue hidden at +3.2s, shown by
    +10.6s.
  - **It is the only tap step with a timeout** (30s). Every other read-this beat is safe to leave open
    because the clock is stopped; this one is not — and the input shield holds the whole board while it
    runs, so the player cannot defend during it either. Unbounded, it would be the first point in the
    tutorial that could actually *lose* the match. 30s of wave 1 is about two leaks at 2 damage each out of
    100, and expiring lands the player on the next step.
  - **The ring tracks the troop**, re-resolved per frame like every highlight. "Ours" is read off that same
    `Reversed` flag rather than remembered from the deploy: a wave enemy on their lane is never reversed, and
    what the bot sends walks **our** lane, so it carries our team instead. The leader (furthest `Progress`) is
    framed at a 1.5-unit radius, which holds the whole two-troop column the card sends.
    `.OnlyWhen(_troopSent)` keeps it from explaining a troop that was never sent, and one killed mid-step
    drops the ring rather than cutting a hole over nothing.
- **The two spells are taught as a pair, on opposite fields, with the category explained first.**
  `SpellKinds` is a read-this beat naming the two kinds while both cards sit in hand; then `CastSpell` is
  **Rage, dropped on the troops in their lane**, and `CastDefensiveSpell`, after the swipe home, is
  **Fireball, dropped on the wave in your own**. The pair *is* the lesson, because a spell's field is part of
  what it is: `RageExecutor` speeds up only what attacks the **opponent's** map (`CanUseInEnemyMap`),
  `FireballExecutor` damages only what attacks the **caster's** (`CanUseInLocalMap`).
  - **Both name a `CardType`, never "the first spell in hand".** The hand holds both at once and each does
    nothing whatsoever on the other's field — the old step took whichever came up first and aimed it at
    `enemyFieldAnchor`, so it could hand the player a cast that buffed or froze nothing at all.
  - **Both run live.** A frozen troop cannot be seen surging, and a frozen clock never walks an enemy into
    the player's lane at all, so the defensive step would have nothing to aim at: the waves' `InitialDelay`
    is 7s of *scaled* time. Both keep the 60s action-step timeout and `RefillMana`.
  - **Both casts are then watched** — `.Watching(...)`, new on `TutorialStep`. A watch is the twin of a
    settle: a settle waits for a result to **finish**, a watch waits for one to be **seen**, which is all a
    duration effect has to offer. `TutorialSequence` serves the watch first, then any predicate, and reuses
    the settle's unfreeze, overlay-hide and `Blocked` shield. Rage needs it because `SwapBackHome` freezes
    next and would stop the surge dead; Fireball needs it even though the outro after it runs live, because
    the outro dims the board and lays the reward over it the moment it is entered.
  - **A watch is counted from the LANDING, not the cast** (`SpellWatchSeconds`: the spell's
    `SpellDataSO.TravelTime` + `SpellLandedWatchSeconds` 2.5s → Rage 2.6s, Fireball 3.5s). The step
    completes on the cast, but a Fireball flies for a whole second before it hits: an earlier draft claimed
    the running outro would show it, and the player instead watched the reward panel cover a fireball still
    in the air. Read from the spell's data, so retuning a travel time can never cut the watch short again.
  - **Each hint degrades instead of pointing nowhere.** Rage prefers the player's own sent troop (the one
    they just watched march), else anything walking that lane — it buffs the lane, not an allegiance — else
    `enemyFieldAnchor`. An empty home lane falls back to `localFieldAnchor`. Running the steps is what makes
    both fallbacks temporary.
  - **Fireball aims at the enemy furthest down the lane**: the one actually about to cost health, and the
    only one certain to be past its spawn invincibility. `ServerEnemyHealth.TakeDamage` drops the hit
    outright while `Invincible` is up, so a hint on a fresh spawn would teach a cast that does nothing.
- **The script starts when the board is visible, not when the match is.** The server reaches `InMatch`
  while `PlayersIntroductionCanvas` is still showing both names (`MatchReadyState` holds 2s; the intro holds
  3s, then fades for 0.5s). Keyed on `InMatch` alone, the director put the Welcome line on top of that
  loading screen, and its freeze then stopped the intro's *scaled* fade with a second of delay left, so the
  names covered the board for the whole tutorial. Three changes, each covering a hole the others leave:
  - The intro registers as **`IMatchIntroduction`** (`IsFinished` flips in the fade's `OnComplete`), and the
    director waits for it after `InMatch`. It goes through the ServiceLocator rather than a serialized
    reference, because the director otherwise depends on nothing concrete in the scene.
  - The wait is **bounded (10s, unscaled)** — the dead-end rule again. That bound is only safe because the
    fade is now **unscaled** too: a freeze that lands before the intro clears can no longer pin it.
  - The intro hides on **any state from `MatchReady` on**, not `MatchReady` alone. A canvas that first
    looked after the server had passed it never hid, and the director now waits on it.
  - Measured in play mode: `InMatch` at +2.1s with the intro opaque, Welcome at +3.5s in the same frame the
    intro reports finished. The ~1.5s of live match in between is harmless: the tutorial waves'
    `InitialDelay` is 7s and the bot's first decision is at least 3s away.
- **The script ends; the match does not.** The last step (`MatchOutro`, a `.Running()` tap) only states the
  goal — "clear all **{0}** waves", with the count read from the wave data via `.Formatting` so re-authoring
  the waves keeps it true. The match then plays out to a **normal ending, on the normal end screen**, and the
  tutorial is paid there like any match. It replaced an outro that banked the reward itself and showed it on
  the overlay's own reward panel before leaving; `BaseTutorialOverlay.ShowReward`/`HideReward` are no
  longer called by either director.
  - **TutorialScene has its own waves**: `WaveData_Tutorial.asset`, **3** waves, wired into that scene's
    `ServerWaveManager` only (GameScene keeps `WaveData.asset`). Wave 1 is identical to the standard one —
    every live beat and measurement above is calibrated against it — and waves 2-3 are gentler cuts of the
    standard 2-3 (7-10 enemies each instead of 13-26).
  - **The match cannot be lost, by construction** (`PrepareMatchEnding`, run before the first line so it
    holds however the match ends). A normal match ends two ways and each is closed off for the bot:
    **a base dies** — the player's base is floored (`BaseServerPlayerHealthManager.SetHealthFloor`,
    `TutorialSettingsSO.MinimumBaseHealth`, default 1), so it cannot, while the bot's still can; **a lane
    clears its last wave first** — a race (`ServerEndGameManager` → `OnTeamDefeatLastWave`), which a new
    player could lose to a bot that simply clears faster, so the bot's lane is held before its last wave
    (`BaseServerWaveManager.HoldLaneBeforeWave`). It plays every wave up to that one, so their field still has
    traffic for the troop and Rage lessons, but it never finishes. Both seams keep their state in the base
    class and are consumed by one concrete class each, so the `*_DEBUG` stand-ins needed no change.
  - **Paid through the normal match path, with the tutorial's reward.** `BaseServerEndGameManager.
    OverrideRewardRoller(new FixedRewardRoller(reward))` replaces *what* the match pays and nothing else: the
    payout still travels `SendRewardRpc` → `ClientRewardHandler` → `BaseRewardService.Grant`, so it is banked
    once and shown by `ClientEndGameCanvas` exactly like a real match reward. The reward is rolled up front
    by `TutorialRewardRoller` against a save that cannot change mid-match; nothing in the director grants it,
    so nothing can grant it twice.
  - **`HandleMatchEnded` (on `OnGameEnded`) only clears the tutorial off the end screen** and calls
    `BeginMenuPhase` while that screen is up, since its buttons are what leave the match. It also **stops the
    sequence**: a base can fall mid-script, and a running step would keep the input shield over the end
    screen's buttons so the player could never leave.
  - **No Play Again on the tutorial's end screen.** Both of its buttons leave to the menu, but "Play Again"
    promises another match and the tutorial's next stop is the menu half. Removed in `TutorialScene` only
    (the `PlayAgainButton` GameObject is inactive there — the scene copy exists so the tutorial can diverge),
    and `ButtonsArea`'s HorizontalLayoutGroup (MiddleCenter) re-centres OK on its own. Nothing re-activates it
    at runtime; `ClientEndGameCanvas` only wires listeners and toggles `interactable`, both safe on an
    inactive button.
  - **Verified live, both endings.** Forcing the bot's base to 0 ended the match (`EndMatch`), put 2 reward
    tiles on the normal end screen, banked the reward once (gold 350 → 700, not 1050) and armed the menu;
    pressing OK loaded MainMenu straight into `MenuWelcome`. Left alone, an **undefended** player won the race
    (Red on wave 3, Blue held at 2) with 10 HP to spare, so the floor is a safety net rather than the thing
    that decides the match.
- **Free play gets tips, not steps.** After `MatchOutro` the board is the player's (`Free`), Skip is hidden
  (it would throw away a match that cannot be lost and is already paying), and `TickFreePlay` offers one tip
  at a time through `BaseTutorialOverlay.ShowTip`: text and a pointing hand, **no dim and no Continue**, the
  input mode untouched. Every decorative overlay graphic is non-raycast, so a tip never eats a touch.
  - `TipDefend` (an enemy past `DefendTipProgress` down the lane and Fireball affordable) outranks
    `TipBuildTower` (a tower card affordable and a free slot). Only on the player's own field. Each resolver
    returns a target only while its situation holds, so "is this tip still true" and "where does it point"
    are one question, and affordability does the rest: playing the suggested card spends the mana it needed,
    so the tip takes itself down that frame. A tip lasts `TipMaxSeconds` (8) at most, then
    `TipCooldownSeconds` (10) of quiet.
  - Tip lines reuse the copy table (`TipDefend`/`TipBuildTower` appended to `TutorialStepId`): they are not
    steps, but they are lines the tutorial says.
  - The lent deck's mana-cap raise continues through free play, so the 5-mana spell never becomes a card
    held unaffordable until the last wave.
- **Armor is taught the first time it can be seen** — a lesson the *match* triggers, not a step the script
  schedules. `ArmorResistance.Resolve`: full damage when the colors match or either side is colorless
  (`ArmorColor.None`); otherwise the enemy's `OffColorResistance` (35% on every armored wave enemy) cuts it,
  reduced by the attack's penetration. The script has nothing armored to point at — wave 1 is all unarmored
  `EnemyData1` — so the first armored enemy (wave 2's **orange** Fast) only ever arrives in free play, and a
  mechanic explained with nothing on screen is explained twice.
  - **Armor is already visible.** No UI shows it, but each armored variant's sprite is authored in its armor
    color (Fast orange, Tank pink, EnemyData2 purple; unarmored Enemy1 white), so the ring lands on the thing
    the line names, and `{0}` in the copy is the enemy's actual `ArmorColor`. Careful with the other half:
    **card color is not attack color** — Dart's card is orange but its attack is colorless, Chain attacks
    orange with a white card — so the line calls the player's Dart and Fireball *colorless*, which is what
    they are (neither sets `AttackColor`). The info panel has no attack-color row either; a gap, not a lie.
  - **Run as a one-step `TutorialSequence` of its own** (`_lesson`, `StartLesson`), so an interrupting beat
    gets the script's whole machinery for free: freeze (the enemy holds still in its ring), dim, `Blocked`
    shield, Continue, then `Free` and unfrozen on the way out. Tips pause while it runs and resume after a
    cooldown. It triggers once, on the first armored enemy on the player's own lane that is past its spawn
    invincibility and 5% in; if the player is on the other field, it waits for them to come home.
  - **Its Continue appears after 1s** (`InterruptContinueDelay`): it lands mid-gesture, and a button popping up
    under a finger aimed at the board would be dismissed unread.
  - Verified live: the first wave-2 Fast enemy froze the match (`timeScale 0`), `Blocked`, ringed, "orange
    armor"; Continue handed the board back (`Free`, unfrozen) and it did not repeat.
- **A "Cannot generate 9 slice ... 22014864 vertices" error appears during free play, and it is NOT the
  tutorial's.** It is raised natively from a view repaint (`GUIUtility.ProcessEvent`), 3-4 times while the
  bot's lane runs its waves, then stops once that lane is held. An A/B with the overlay's Canvas **disabled**
  for the whole of free play still produced it, and every sliced/tiled renderer in the scene checked out
  (buff zones size to their radius; projectiles are Simple; range rings scale by transform, which adds no
  tiles). It only surfaced because the tutorial now plays a live match; the source is still open.
- `RewardPrefab`'s quantity label auto-sizes (max = the authored 97.3) and never wraps: a replayed save's
  payout is five digits, which wrapped onto a second line below the tile.
- **Only the thing asked for can be touched; a step asking for nothing leaves nothing to touch.** Four solid
  panels frame a hole around the target, but they are decoration — input belongs to a
  **`TutorialInputShield`**: one invisible, full-screen `Graphic` + `ICanvasRaycastFilter` with three modes.
  `TutorialSequence` sets them: a tap step → **`Blocked`** (only Continue/Skip answer, *even though* the
  Mana and Hand reads show a hole — it is there to be looked at, not touched); an action step →
  **`TargetOnly`** (exactly the padded, clamped undimmed hole passes); a settling or watching beat →
  `Blocked`; finished or stopped → **`Free`**. The rule is derived (`TutorialStep.AcceptsInputAtTarget` is
  `!WaitsForTap`), so a new step cannot forget to declare it. This replaced `blockInput`, which was off
  precisely so the player could defend while reading — the design now holds the board for the whole script.
  - **One UI-level filter is the whole gate** because every input the game takes arrives through the
    EventSystem: the cards, and the full-screen `CameraSlideArea` that carries both the table swipe
    (`CameraSlide`) and a tower tap (`TowerSelectionInput`). Card drops validate against their own
    `BlockCardsCanvas` raycaster, never the overlay's, so a drag that starts in the hole lands wherever it is
    dropped; Unity keeps delivering a drag to the object it began on. Sort order makes it hold: the overlay
    canvas is Screen Space - Overlay at **20**, above everything the player touches in both scenes. The
    Quantum Console (30) deliberately stays above it.
  - **Built in code, beside `Content`, as its first sibling.** Beside, so hiding the overlay for a settle
    leaves the board held (a blocker that vanished with the dims would hand the board back exactly while the
    tutorial waits on it); first, so Continue and Skip raycast in front of it. It draws **nothing** — a
    zero-alpha Image would block the same way but rasterise a full-screen quad every frame on mobile. That
    a non-drawing graphic is still raycast (`GraphicRaycaster` skips graphics with `depth == -1`) was
    **verified live**: on Welcome/Mana/Hand the board and all four cards hit the shield while Continue
    answers; on PlaceTower only Dart reaches its own graphic.
  - **A step with no target holds the whole board**, since `TargetOnly` with no hole has nothing to pass.
    That is why `LevelUpTower` is now `.OnlyWhen(_towerPlaced)`: after a timed-out place there is nothing to
    point at, and skipping costs the lesson nothing, where a minute of dead board would not.
  - Two traps found by running the dims: they must have **no sprite** (a 32px rounded `UISprite` stretched
    across half the screen trips Unity's "Cannot generate 9 slice" and draws nothing), and a target
    `RectTransform` may be **0x0** — the deck page's card widget is a shell around a `Content` child — so the
    overlay unions the children when a target has no area of its own. Cross-canvas maths is the other
    subtlety: the hand and mana bar are on a **Screen Space - Camera** canvas, so their corners reach screen
    space through *that* camera and come back through this overlay's (null) one.
- **The table swap is a swipe DOWN.** The opponent's field sits *above* ours (`BluePlayerMapY` 11 vs
  `RedPlayerMapY` -1.1) and `CameraSlide` moves the camera *against* the finger, so the board follows the
  drag: reaching their lane is a finger-down swipe, coming home a finger-up one. The first draft hinted and
  worded both backwards, and a swipe up at home is clamped and does nothing. Running it also exposed a
  regression from the `CameraSlide` refactor (`d866e93d`): `EvaluateRelease` divided by Red - Blue, so drag
  progress ran 0 -> -1. A slow drag therefore never committed toward the enemy field (only a flick did),
  and releasing any drag on the enemy field committed back home. It is Blue - Red again.
- **The payout is always a card, shaped by the steps that follow it, not rolled for value.**
  `TutorialRewardRoller` picks uniformly within the first non-empty tier: a card the player has **never
  owned** (every real first run lands here — a fresh save owns 8 of 30), else an owned card **outside the
  active deck that can still level**, else any owned card outside the deck. It grants exactly the cost of
  that card's *next* level (1 -> 2 for a new card) plus a small surplus, so "equip it" and "upgrade it" are
  both always possible. The fallback tiers exist because of replays: "Replay Tutorial On Next Launch" keeps
  the collection, and a developed save that owned everything used to be paid 150 gold with no card to show
  at all. A replay can therefore grant a large sum (a level-10 Common's next step is 645 copies + 10k gold).
  It is deliberately not an `IRewardRoller` itself — that interface is win/lose-shaped — so its result is
  handed to the match as a `FixedRewardRoller`, and pays out through the normal end of the match (above).
- **The menu half teaches remove-then-add**, because the starter deck is exactly `DeckSize` cards and
  `TryEquipCard` refuses a full deck. It points at the page's own state through `DeckUIController` and never
  drives it: the tutorial points, the player acts.
  - **The menu never freezes** (`new TutorialSequence(..., freezesWorld: false)`). There is nothing at stake
    there, and the menu animates on scaled time: under the freeze, `HorizontalPageStrip`'s snap tween stood
    still, while `CurrentPageIndex` had already flipped on the tap. "Open the Deck page" therefore completed
    at once, and the next step pointed at cards on a page that never slid in.
  - **Page changes settle on the strip.** `OpenDeckPage`/`OpenBattlePage` complete on `CurrentPageIndex`
    and then `.SettlingUntil(pageStrip.IsSettled)`; `IsSettled` is false from a drag or a snap's start until
    the snap completes. The overlay steps aside for the ~0.3s slide, then the next step appears on a page
    that is at rest (traced frame by frame in play mode). Deck-page hints go through `OnPage(...)`, which
    points at that page's nav button instead whenever the player has swiped off it.
  - **`TutorialStep.OnlyWhen`** passes a step over unseen when its premise does not hold for this save —
    `RemoveCard` only with a full deck, `UpgradeCard` only while the upgrade is still ahead and affordable
    (a replay can hand out a maxed card). Upgrade progress is measured from the reward's level **when the
    menu half started**, not "above 1", since a replay's reward may already be level 10.
  - **A per-card action is pointed at in the two taps it takes** (`PointAtCardAction`): the card while its
    `ActionFrame` popup is closed, then the popup's Use/Remove button (`ActionButtonRect`) once it is open on
    that card — the shape `PointAtDetailsButton` already had. The popup opens *beside* the card, so under the
    input shield a step framing only the card left the button it asked for outside the hole. `RemoveCard`
    therefore names one card (the first that is not the reward) instead of accepting any.
  - **The upgrade step frames the info panel's Upgrade button** (`BaseInfoPanelService.UpgradeButtonRect`,
    the same idiom as `ActionFrame`'s rects). Beyond the ring, this is what moves the copy: with no target
    the text box parks low, exactly over that button, and the player was told to tap something hidden.
  - **Closing that panel is its own step** (`CloseCardDetails`, on `CloseButtonRect` — the twin of
    `UpgradeButtonRect`). The panel is modal and covers the nav bar, so without it the next step pointed at a
    Battle nav button the player could not reach. It completes on the panel going away by *any* route — close
    button, backdrop tap — and is `.OnlyWhen` it is actually open, because nothing guarantees that:
    `UpgradeCard` passes over a maxed card without ever showing, and the player may have closed it
    themselves. No fallback target either: while a modal is up, nothing behind it is worth framing.
- Debug: `Kecek/Debug Tools/Tutorial/Replay Tutorial On Next Launch` (and `Mark Tutorial Completed`). Both
  edit the save **file**, because the flag is read in `ClientManager.Awake` long before a menu item can be
  clicked. Replay keeps the collection; for a genuine first run use the Quantum Console's **`reset-save`**
  from the menu, which wipes the save and boots straight into the tutorial (see Player Save).
- Key files: `BaseTutorialService.cs`, `TutorialService.cs`, `TutorialSettingsSO.cs`, `TutorialCopySO.cs`
  (under `Assets/Scripts/Services/Tutorial/`); `TutorialStep.cs`, `TutorialSequence.cs`,
  `TutorialMatchDirector.cs`, `TutorialRewardRoller.cs` (under `Assets/Scripts/Gameplay/Tutorial/`);
  `BaseTutorialOverlay.cs`, `TutorialOverlayCanvas.cs`, `TutorialInputShield.cs`,
  `TutorialMenuDirector.cs` (under `Assets/Scripts/UI/Tutorial/`); `IMatchIntroduction.cs`,
  `PlayersIntroductionCanvas.cs` (under `Assets/Scripts/UI/Game/Match/`); the match-ending seams
  `BaseServerPlayerHealthManager.SetHealthFloor`, `BaseServerWaveManager.HoldLaneBeforeWave`,
  `BaseServerEndGameManager.OverrideRewardRoller` and `FixedRewardRoller.cs` (under
  `Assets/Scripts/Gameplay/Rewards/`); `HorizontalPageStrip.cs` (`IsSettled`); `SaveDebugCommands.cs` (under
  `Assets/Scripts/Debug/`); assets under `Assets/ScriptableObjects/Tutorial/` (including
  `WaveData_Tutorial.asset`); prefab at `Assets/Prefabs/UI/Tutorial/TutorialOverlayCanvas.prefab`;
  `Assets/Scenes/TutorialScene.unity`.

### Card art — generated vectors, and colour as a readable rule

Every card that was still on placeholder art (21 of 30) now has its own shape, authored as **code** in
`Art/shapes.ps1`, rendered to an editable `.svg` source under `Art/Vector/` and to the `.png` sprites Unity
imports. The three armour colours are now spread deliberately across the set, and colour finally means the
same thing on the card as it does in the rules.

**Why generated rather than drawn:** the two hard parts are constraint problems, not drawing problems. A
tower's three sprites stack on the prefab's `GFX/Level1|2|3` renderers and must nest concentrically, and the
whole set has to stay consistent with the four hand-drawn towers. Authoring geometry once and deriving
levels, shadows and the `.svg` from it means a level-2 sprite can never drift from its level 1. WPF's
`Geometry.Parse` speaks the same path mini-language as SVG, so one string is both what lands in the `.svg`
and what the rasteriser draws — they cannot disagree. No new dependency: the rasteriser is WPF, already on
the machine.

**How it works:**
- **Nothing was invented; it was measured.** Canvas 748, level ratios 1 : 0.466 : 0.241, drop shadow +22/+30
  at level 1, troop silhouette 496 white inside 582 black, spell icons 512 edge-to-edge with no shadow,
  pixels-per-unit 1300/2000/1300 — all read off `CircleTower1/2/3`, `SlamTower1`, `BaseEnemy1` and
  `meteorite`. `Art/README.md` has the table.
- **Art is authored WHITE; colour is a tint.** `CardDataSO.CardColor` for the card, `SpriteRenderer.color`
  on the prefab — exactly what Dart/Square/Slam already did. A card recolours without re-rendering, and the
  baked black shadow survives any tint because anything times zero is zero.
- **A tower's tint is stated exactly once, on the renderers.** `ClientTowerGFX` repaints Level 1 for the
  freeze/haste status visual and used to restore a serialized `normalColor` that defaulted to **white**, so
  the colour was two facts instead of one. Giving the towers their own art drifted them apart immediately:
  the first freeze or haste repainted a coloured tower and it never came back — and Mortar went *pink*
  rather than merely white, its `normalColor` left over from when it borrowed Square's sprite. The field is
  gone; `ClientTowerGFX` captures `level1Renderer.color` in `Awake` (before `OnNetworkSpawn` replays the
  initial frozen/haste state) and restores that. Recolouring a tower is now one edit, not two.
- **Two things are solved, not authored**, because the four references only *look* uniform.
  **Where the shape sits:** centring each level's bounding box drifts for anything not symmetric about its
  own centre — a triangle's bbox centre is nowhere near the point it shrinks toward, so nested copies crawl
  upward. `SlamTower1/2/3`'s bbox centres sit at y 342 / 358.5 / 369, *converging* on 374 rather than on it,
  which is the signature of anchoring the shape's own centre; every shape declares that point.
  **How big the nested levels are:** 0.466x works for a circle and drops level 2 onto level 1's wall on a
  triangle or a hexagram. `Fit-Next` binary-searches the largest size at which the next level's ink misses
  this one's, probing with the **real silhouette** — a triangle nests in a triangular hole at nearly its full
  width, where the largest circle that fits is less than half of it. Ten of twelve towers clear the reference
  ratio and use it; Needle, Shard and Ancora nest tighter.
- **A tower is an outline = outer contour + a shrunken copy of itself, filled even-odd.** A constant-width
  stroke was the first attempt and cannot do the job: 114px leaves a circle reading as a ring but fills a
  triangle or a star solid. Level 3 drops the hole and goes solid, which is what `CircleTower3` and
  `SquareTower3` already do.
- **Every tower silhouette must keep its centre open**, since levels 2 and 3 nest inside it. That constraint
  is why Ancora's stock stops at the crossbar instead of running the full height, and why Espelho is split
  along its mirror line rather than down the middle.
- **Colour is now a rule the board can be read with: coloured = deals that colour's damage.** The six towers
  that deal no damage at all (Prism, Tourniquete, Anel, Ancora, Espelho, Fonte) stay **white** — an
  `AttackColor` on them would be mechanically meaningless and would promise damage they never deal. Circle
  stays neutral as the starter, Stinger stays colourless by design (`ArmorPenetration 1`). The eight
  remaining damage towers split 3/3/2: Orange {Dart, Chain, Mortar}, Pink {Square, Needle, Beacon}, Purple
  {Slam, Shard}. **The starter deck is what fixes Circle as neutral** — it holds Dart, Square and Slam, one
  of each colour, so a fourth colour there would double one up and blunt the lesson.
- **Dart/Square/Slam were already painted orange/pink/purple but dealt colourless damage.** The art and the
  rules disagreed before this change; their `AttackColor` is now set to what the card had been claiming all
  along. This makes them genuinely worse against off-colour armour, which is the point.
- **Troops gained real armour**, 2/2/2 across Orange {Cisma, Swarm}, Pink {Ram, MiniBoss}, Purple {Shadow,
  Miragem}, with `SpawnEnemy1` left neutral the way Circle is. **`MiragemDecoy` is given Miragem's exact
  colour and armour** — for the same reason its `MoveSpeed` and `SpawnDuration` already match, a decoy that
  is tinted differently is a bluff the defender can read at a glance.
- **Swarm and MiniBoss were never flagged `ShowPlaceholderNameOverlay` but had no art either** — Swarm's card
  pointed at `Robot.png` from the *Quantum Console demo scene*, and MiniBoss borrowed `BaseEnemy1` from the
  basic troop. Both got their own shape. Nothing in the set now shares art it does not own.
- **Not done:** projectile sprites (`*TowerShoot1.png`) are still shared — the new towers fire the base
  prefab's bullet. Spells stay white deliberately: colour is the tower/troop language, and the tutorial's
  armour lesson names Fireball colourless in hand-written copy.
- Key files: `Art/shapes.ps1` (geometry, pixel-free), `Art/render.ps1` (pixels, levels, shadows, the card
  manifest with every tint), `Art/README.md`; sources at `Art/Vector/{Towers,Enemies,Spells}/*.svg`; sprites
  under `Assets/Sprites/Towers/<Name>/`, `Assets/Sprites/Enemies/` and `Assets/Sprites/UI/Cards/`.

### Battle = quick match, and descriptions that carry no numbers

Two small rules, both about removing something the player should not have had to deal with.

**Battle finds a match by itself.** The button used to *create* a relay, and the only way to reach someone
else's was to type their room code. It now joins whoever is already waiting and hosts only when nobody is.

- **Nothing new is advertised.** The host already publishes a discovery lobby carrying its Relay join code,
  so matchmaking is just "ask for any open lobby, read the code out of it, join that Relay" — and hosting is
  what happens when the answer is *none*. `BaseMatchmaker.FindOrCreateMatchAsync` returns
  `Joined`/`Hosting`/`Failed`; the first two both mean a scene load is already under way, so only `Failed`
  puts the button back.
- **The lobby needed one more seat than it had.** `CreateLobbyAsync`'s `maxPlayers` counts the host;
  `CreateAllocationAsync`'s `maxConnections` counts peers *besides* the host. Both were passed
  `MAX_CONNECTIONS = 1`, so the host filled its own lobby, advertised **zero** free seats, and
  `QuickJoinLobbyAsync` could never return it — quick match would have created a fresh relay on every press
  and two players would never have met. `LOBBY_SEATS = MAX_CONNECTIONS + 1` names the distinction.
- **The join code's `Member` visibility is why this works, not an obstacle to it.** Whoever quick-joins is a
  lobby member by the time they read the code; browsing the public list never could be, which is the point.
- **A stale lobby is "no match", not an error.** A host that crashed keeps its lobby advertised until the
  heartbeat lapses, so quick join will happily hand out a join code whose allocation is gone. Every failure
  in the join path therefore returns false rather than throwing, releases the seat
  (`RemovePlayerAsync`) so a dead lobby is not held open by a phantom member, and falls through to hosting.
- **The client stays in the lobby on success**, deliberately: that seat is what stops a third player
  quick-joining a match that is already full. The host's `CloseLobbyToNewPlayers` deletes the whole lobby on
  commit, which releases it.
- **`BaseHostManager` is resolved per call, never cached.** `HostManager` registers itself in its own
  `Awake`, and nothing orders that against `ClientManager`'s, where the matchmaker is constructed — the same
  reason `HostManager` grabs its client manager in `Start`.
- **A refused connection no longer strands the player.** `NetworkClient`'s disconnect handler deliberately
  never navigates, because a drop *inside* a match must not yank a player off the end screen. Before the
  match scene is ever reached the opposite holds — there is no snapshot and no button — so a disconnect
  outside `Loader.IsGameplayScene` now returns to the Main Menu. Quick match is what made this reachable:
  it can hand out a lobby whose host committed (bot filled the slot) a moment before we connect, and
  `NetworkConnectionServer.ApprovalCheck` then refuses us.
- **Join-by-code is kept**, wired to its own button, for playing with a specific person and for debugging.
- **`BotSettingsSO.FillTimeoutSeconds` (30s) is now the window in which two humans can meet** — past it a bot
  takes the slot and the lobby closes. Worth raising for a real playtest; left alone because it is a design
  call, not a bug.
- Key files: `BaseMatchmaker.cs`, `Matchmaker.cs` (under `Assets/Scripts/ApplicationController/Matchmaking/`);
  `HostManager.CreateLobby` (`LOBBY_SEATS`), `ClientManager.InitializeMatchmaking`, `ConnectionManagerUI.QuickPlay`,
  `NetworkClient.NetworkManager_OnClientDisconnectCallback`.

**Card descriptions say what a card does and never what it is worth.** All 30 were rewritten to one or two
sentences with **no numbers at all** — no percentages, durations, counts or multipliers.

**Why:** the card page already prints every stat at the player's own level *and* what the next level buys,
from `CardDataSO.GetStats`. A number repeated in prose is a second source of truth that silently goes stale
the moment balance moves — and several already had: descriptions promised "+20%", "45% less speed", "10
fodder" and "six seconds" while the tables beside them scaled with card level. Prose keeps the trade-off
("devastating down a packed lane, wasted on a lone target"), which is the part no stat row can show. The
rewrite also dropped Anel's reference to *Ariete* and *Erosao*, two cards that do not exist.

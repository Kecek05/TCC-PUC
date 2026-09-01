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
  inspector preview today and the Clash Royale-style current-vs-next panel later, so they cannot disagree.
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
  other sources build a `Reward` themselves rather than going through `IRewardRoller`. It picks rarity first,
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

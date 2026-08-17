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
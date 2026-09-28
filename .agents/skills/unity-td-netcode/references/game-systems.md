# Tower Defense Card Game Systems

Design patterns for a real-time PvP tower defense card game (Clash Royale / Bloons TD Battles style)
built in Unity 6 with Netcode for GameObjects. All systems are server-authoritative.

---

## Table of Contents

1. [Elixir / Mana Economy](#elixir-economy)
2. [Card System (Deck, Hand, Deployment)](#card-system)
3. [Tower System](#tower-system)
4. [Unit System (Troops)](#unit-system)
5. [Projectile & Damage System](#projectile-damage)
6. [Battlefield & Lanes](#battlefield-lanes)
7. [Win Condition & Match Flow](#win-condition)
8. [ScriptableObject Data Architecture](#data-architecture)
9. [Spell System](#spell-system)

---

## 1. Elixir / Mana Economy {#elixir-economy}

The elixir system gates card deployment — identical to Clash Royale's model.

```csharp
public class ElixirManager : NetworkBehaviour
{
    public const float MaxElixir = 10f;
    public const float BaseRegenRate = 1f / 2.8f; // ~1 elixir every 2.8 seconds
    public const float DoubleElixirRate = BaseRegenRate * 2f;

    // Per-player elixir — synced to respective client only
    private readonly Dictionary<ulong, float> _elixir = new();
    private float _currentRegenRate = BaseRegenRate;

    // Sync to each player (they only see their own)
    private NetworkVariable<float> _hostElixir = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _clientElixir = new(writePerm: NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                _elixir[clientId] = 5f; // starting elixir
            }
        }
    }

    // Called from network tick on server
    public void TickElixir(float deltaTime)
    {
        if (!IsServer) return;

        foreach (ulong clientId in _elixir.Keys.ToArray())
        {
            _elixir[clientId] = Mathf.Min(_elixir[clientId] + _currentRegenRate * deltaTime, MaxElixir);
        }

        // Sync to NetworkVariables (host sees _hostElixir, client sees _clientElixir)
        SyncElixirToNetVars();
    }

    public bool TrySpend(ulong clientId, float cost)
    {
        if (_elixir[clientId] < cost) return false;
        _elixir[clientId] -= cost;
        SyncElixirToNetVars();
        return true;
    }

    public void Refund(ulong clientId, float amount)
    {
        _elixir[clientId] = Mathf.Min(_elixir[clientId] + amount, MaxElixir);
        SyncElixirToNetVars();
    }

    public void SetDoubleElixir() => _currentRegenRate = DoubleElixirRate;
}
```

**Design notes:**
- Elixir is `float` server-side for smooth regen; the UI can display as a bar or rounded int.
- Each player only sees their own elixir — opponent's is hidden.
- Use `NetworkVariable` per player slot (2-player game = 2 variables) rather than a dictionary sync.

---

## 2. Card System {#card-system}

### Card Data (ScriptableObject)

```csharp
public enum CardType { Tower, Unit, Spell }
public enum CardRarity { Common, Rare, Epic, Legendary }
public enum TargetType { Ground, Air, Both }

[CreateAssetMenu(menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string CardId;
    public string DisplayName;
    public Sprite CardArt;
    public CardType Type;
    public CardRarity Rarity;

    [Header("Cost")]
    public int ElixirCost;

    [Header("Spawning")]
    public GameObject Prefab; // NetworkObject prefab
    public int SpawnCount = 1; // e.g., 3 archers from one card

    [Header("Placement")]
    public bool CanPlaceOnEnemySide;
    public float PlacementRadius = 0.5f;

    [Header("Stats — filled per card type")]
    public int Health;
    public float AttackDamage;
    public float AttackSpeed; // attacks per second
    public float Range;
    public float MoveSpeed; // 0 for towers
    public TargetType TargetType;
}
```

### Deck Manager (Server-Side)

```csharp
public class DeckManager
{
    private readonly Dictionary<ulong, Queue<CardData>> _drawPiles = new();
    private const int HandSize = 4;
    private const int NextCardPreview = 1;

    public void InitializeDeck(ulong clientId, List<CardData> deckList)
    {
        // Shuffle the deck
        var shuffled = new List<CardData>(deckList);
        ShuffleFisherYates(shuffled);
        _drawPiles[clientId] = new Queue<CardData>(shuffled);
    }

    public List<CardData> DrawInitialHand(ulong clientId)
    {
        var hand = new List<CardData>();
        for (int i = 0; i < HandSize; i++)
        {
            hand.Add(DrawOne(clientId));
        }
        return hand;
    }

    public CardData DrawOne(ulong clientId)
    {
        var pile = _drawPiles[clientId];
        if (pile.Count == 0)
        {
            // Reshuffle discard pile — for a card game, cycle the deck
            RefillDrawPile(clientId);
        }
        return pile.Dequeue();
    }

    private void ShuffleFisherYates<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
```

**Key design decisions:**
- Deck is a cycle — when exhausted, reshuffle all played cards back in.
- Hand size is fixed (4 cards + 1 next preview), same as Clash Royale.
- Client never knows the full deck order — prevents cheating.

---

## 3. Tower System {#tower-system}

```csharp
public class Tower : NetworkBehaviour, IDamageable
{
    [SerializeField] private TowerTargeting _targeting;

    private NetworkVariable<int> _health = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<ulong> _targetId = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _level = new(writePerm: NetworkVariableWritePermission.Server);

    private CardData _cardData;
    private float _attackTimer;

    public void Initialize(CardData data)
    {
        _cardData = data;
        if (IsServer)
        {
            _health.Value = data.Health;
            _level.Value = 1;
        }
    }

    // Called from server tick
    public void ServerTick(float deltaTime)
    {
        if (!IsServer || _health.Value <= 0) return;

        // Find target
        var target = _targeting.FindBestTarget(transform.position, _cardData.Range, _cardData.TargetType);
        _targetId.Value = target != null ? target.NetworkObjectId : 0;

        // Attack
        if (target != null)
        {
            _attackTimer += deltaTime;
            if (_attackTimer >= 1f / _cardData.AttackSpeed)
            {
                _attackTimer = 0f;
                Attack(target);
            }
        }
    }

    private void Attack(NetworkObject target)
    {
        // Spawn projectile or apply direct damage
        if (_cardData.Type == CardType.Tower)
        {
            ProjectileManager.Instance.SpawnProjectile(
                transform.position, target, _cardData.AttackDamage, OwnerClientId);
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        _health.Value = Mathf.Max(0, _health.Value - Mathf.RoundToInt(damage));

        if (_health.Value <= 0)
        {
            OnDestroyedClientRpc();
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    [ClientRpc]
    private void OnDestroyedClientRpc()
    {
        // VFX, SFX on all clients
    }
}
```

### Tower Targeting

```csharp
public class TowerTargeting : MonoBehaviour
{
    public enum TargetPriority { Closest, LowestHealth, HighestDamage, First }

    [SerializeField] private TargetPriority _priority = TargetPriority.Closest;

    // Call server-side only — uses Physics2D overlap for range detection
    public NetworkObject FindBestTarget(Vector2 origin, float range, TargetType targetType)
    {
        var hits = Physics2D.OverlapCircleAll(origin, range, LayerMasks.EnemyUnits);
        if (hits.Length == 0) return null;

        NetworkObject best = null;
        float bestScore = float.MaxValue;

        foreach (var hit in hits)
        {
            var unit = hit.GetComponent<Unit>();
            if (unit == null || unit.IsDead) continue;
            if (!MatchesTargetType(unit, targetType)) continue;

            float score = _priority switch
            {
                TargetPriority.Closest => Vector2.Distance(origin, hit.transform.position),
                TargetPriority.LowestHealth => unit.CurrentHealth,
                _ => Vector2.Distance(origin, hit.transform.position)
            };

            if (score < bestScore)
            {
                bestScore = score;
                best = hit.GetComponent<NetworkObject>();
            }
        }

        return best;
    }
}
```

---

## 4. Unit System (Troops) {#unit-system}

Units are mobile entities that follow a path or target buildings/other units.

```csharp
public class Unit : NetworkBehaviour, IDamageable
{
    private NetworkVariable<int> _health = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<UnitState> _state = new(writePerm: NetworkVariableWritePermission.Server);

    public enum UnitState : byte { Moving, Attacking, Dead }

    public bool IsDead => _state.Value == UnitState.Dead;
    public int CurrentHealth => _health.Value;

    private CardData _cardData;
    private NetworkObject _attackTarget;

    public void Initialize(CardData data)
    {
        _cardData = data;
        if (IsServer)
        {
            _health.Value = data.Health;
            _state.Value = UnitState.Moving;
        }
    }

    // Server tick: move toward lane end or engage enemies
    public void ServerTick(float deltaTime)
    {
        if (!IsServer || IsDead) return;

        switch (_state.Value)
        {
            case UnitState.Moving:
                MoveAlongLane(deltaTime);
                CheckForEnemiesInRange();
                break;
            case UnitState.Attacking:
                AttackTarget(deltaTime);
                break;
        }
    }

    private void MoveAlongLane(float deltaTime)
    {
        Vector2 direction = GetLaneDirection();
        transform.Translate(direction * _cardData.MoveSpeed * deltaTime);
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        _health.Value -= Mathf.RoundToInt(damage);
        if (_health.Value <= 0)
        {
            _state.Value = UnitState.Dead;
            UnitDeathClientRpc();
            // Return to pool after death animation
            StartCoroutine(DespawnAfterDelay(0.5f));
        }
    }
}
```

---

## 5. Projectile & Damage System {#projectile-damage}

```csharp
public interface IDamageable
{
    void TakeDamage(float damage);
}

public class Projectile : NetworkBehaviour
{
    private NetworkObject _target;
    private float _damage;
    private float _speed = 15f;
    private ulong _ownerPlayerId;

    public void Initialize(NetworkObject target, float damage, ulong ownerPlayerId)
    {
        _target = target;
        _damage = damage;
        _ownerPlayerId = ownerPlayerId;
    }

    // Server-side update
    private void Update()
    {
        if (!IsServer) return;

        if (_target == null || !_target.IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
            return;
        }

        Vector2 dir = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
        transform.Translate(dir * _speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, _target.transform.position) < 0.2f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        if (_target.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(_damage);
        }

        HitVfxClientRpc(transform.position);
        GetComponent<NetworkObject>().Despawn();
    }

    [ClientRpc]
    private void HitVfxClientRpc(Vector2 position)
    {
        VfxManager.Instance.PlayHitEffect(position);
    }
}
```

---

## 6. Battlefield & Lanes {#battlefield-lanes}

### Battlefield Layout (Clash Royale-style)

```
┌─────────────────────────────┐
│       Player B Territory     │
│  [Crown Tower B]             │
│  [Princess Tower B1] [PT B2] │
│─────────── River ────────────│
│  [Princess Tower A1] [PT A2] │
│  [Crown Tower A]             │
│       Player A Territory     │
└─────────────────────────────┘
```

### Placement Validation

```csharp
public class BattlefieldManager : NetworkBehaviour
{
    [SerializeField] private Bounds _playerATerritory;
    [SerializeField] private Bounds _playerBTerritory;
    [SerializeField] private float _minDistanceBetweenTowers = 1.5f;

    public bool IsValidPlacement(ulong clientId, Vector2 position, CardData card)
    {
        // 1. Check territory
        Bounds territory = GetPlayerTerritory(clientId);
        if (card.CanPlaceOnEnemySide)
            territory = GetFullBattlefield();

        if (!territory.Contains(position))
            return false;

        // 2. Check overlap with existing towers
        var overlap = Physics2D.OverlapCircle(position, _minDistanceBetweenTowers, LayerMasks.Towers);
        if (overlap != null)
            return false;

        // 3. Check not on river / blocked tiles
        if (IsBlockedTile(position))
            return false;

        return true;
    }
}
```

---

## 7. Win Condition & Match Flow {#win-condition}

### Match Phases

```csharp
public enum MatchPhase : byte
{
    WaitingForPlayers,
    Countdown,      // 3-2-1 pre-match
    Regular,        // normal elixir rate
    DoubleElixir,   // 2x elixir (last 60s)
    Overtime,       // sudden death or timed OT
    Finished
}
```

### Match Manager

```csharp
public class MatchManager : NetworkBehaviour
{
    private NetworkVariable<MatchPhase> _phase = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _timer = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _scoreA = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _scoreB = new(writePerm: NetworkVariableWritePermission.Server);

    private const float RegularDuration = 120f;
    private const float DoubleElixirDuration = 60f;
    private const float OvertimeDuration = 60f;

    public void ServerTick(float deltaTime)
    {
        if (!IsServer) return;

        _timer.Value -= deltaTime;

        switch (_phase.Value)
        {
            case MatchPhase.Regular:
                if (_timer.Value <= 0)
                {
                    _phase.Value = MatchPhase.DoubleElixir;
                    _timer.Value = DoubleElixirDuration;
                    ElixirManager.Instance.SetDoubleElixir();
                }
                break;
            case MatchPhase.DoubleElixir:
                if (_timer.Value <= 0)
                    EvaluateEndOrOvertime();
                break;
            case MatchPhase.Overtime:
                if (_timer.Value <= 0)
                    EndMatch();
                break;
        }
    }

    public void OnCrownTowerDestroyed(ulong destroyerClientId)
    {
        // Destroying crown tower = instant win
        EndMatch(destroyerClientId);
    }

    public void OnPrincessTowerDestroyed(ulong destroyerClientId)
    {
        if (IsPlayerA(destroyerClientId))
            _scoreA.Value++;
        else
            _scoreB.Value++;
    }
}
```

---

## 8. ScriptableObject Data Architecture {#data-architecture}

### Card Database (Runtime Lookup)

```csharp
[CreateAssetMenu(menuName = "Game/Card Database")]
public class CardDatabase : ScriptableObject
{
    [SerializeField] private List<CardData> _allCards;

    private Dictionary<string, CardData> _lookup;

    public void Initialize()
    {
        _lookup = new Dictionary<string, CardData>();
        foreach (var card in _allCards)
            _lookup[card.CardId] = card;
    }

    public CardData Get(string cardId) => _lookup.TryGetValue(cardId, out var card) ? card : null;

    // Network serialization uses CardId strings (not SO references)
}
```

### Balance Config

```csharp
[CreateAssetMenu(menuName = "Game/Balance Config")]
public class BalanceConfig : ScriptableObject
{
    [Header("Elixir")]
    public float StartingElixir = 5f;
    public float MaxElixir = 10f;
    public float BaseRegenPerSecond = 0.357f;
    public float DoubleElixirMultiplier = 2f;

    [Header("Match")]
    public float RegularPhaseDuration = 120f;
    public float DoubleElixirPhaseDuration = 60f;
    public float OvertimeDuration = 60f;

    [Header("Deployment")]
    public float DeployCooldownSeconds = 0.5f;
    public float MinTowerSpacing = 1.5f;
}
```

Use `BalanceConfig` as a single source of truth for all tuning values — never hardcode numbers
in MonoBehaviours. This allows designers to tweak balance without touching code.

---

## 9. Spell System {#spell-system}

Spells are cards that apply area effects without spawning persistent entities.

```csharp
public class SpellExecutor : NetworkBehaviour
{
    public void ExecuteSpell(CardData spell, Vector2 position, ulong casterClientId)
    {
        if (!IsServer) return;

        switch (spell.CardId)
        {
            case "fireball":
                ApplyAreaDamage(position, spell.Range, spell.AttackDamage, casterClientId);
                break;
            case "freeze":
                ApplyAreaFreeze(position, spell.Range, duration: 4f, casterClientId);
                break;
            case "heal":
                ApplyAreaHeal(position, spell.Range, spell.AttackDamage, casterClientId);
                break;
        }

        // VFX for all clients
        SpellVfxClientRpc(spell.CardId, position);
    }

    private void ApplyAreaDamage(Vector2 center, float radius, float damage, ulong casterId)
    {
        var hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var hit in hits)
        {
            // Only damage enemies of the caster
            if (!IsEnemyOf(hit, casterId)) continue;
            if (hit.TryGetComponent(out IDamageable d))
                d.TakeDamage(damage);
        }
    }

    [ClientRpc]
    private void SpellVfxClientRpc(string spellId, Vector2 position)
    {
        VfxManager.Instance.PlaySpellEffect(spellId, position);
    }
}
```

**Spell validation** follows the same pattern as card play: validate elixir, validate position,
then execute server-side and broadcast VFX via ClientRpc.

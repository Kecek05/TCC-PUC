# CardHand System — Refactoring Notes

Notes from the SOLID refactor of the CardHand system. Saved so the reasoning stays with the code.

## Why interfaces here, and what you're actually gaining

### Before the refactor
`CardHandManager` had a line like:
```csharp
[SerializeField] private CardDataListSO _cardDataListSO;
// ...
int cost = _cardDataListSO.GetCardDataByType(cardType).Cost;
```

That's a **concrete dependency**. The manager knows about `CardDataListSO`, which is a `ScriptableObject`, which is a Unity asset type, which has a whole `List<CardDataSO>` on it. `CardHandManager` only needs **one tiny thing** from all that: the cost of a card.

### After
```csharp
private ICardCostProvider _costs;
// ...
int cost = _costs.GetCost(cardType);
```

`CardHandManager` now depends on an abstraction that says "give me a cost for a CardType." Nothing about ScriptableObjects, asset lists, or Unity.

## Concrete benefits

### 1. You can swap implementations without touching the consumer
`CardHandManager` works with anything implementing `ICardCostProvider`. Tomorrow you might want:
- A cheat-test mode where all costs are 1 → 5-line `DebugCardCostProvider : ICardCostProvider`.
- Costs pulled from a server balance file instead of a local SO → `RemoteCardCostProvider`.
- A modifier that halves costs during a power-up → `DiscountedCostProvider` wrapping the SO.

Zero changes to `CardHandManager` for any of these. That's the **Open/Closed Principle** working — open to extension, closed to modification.

### 2. Unit-testable without Unity
`HandData.Distribute` and `HandData.Unlock` now take `ICardCostProvider` as a parameter. In a NUnit test you can write a 3-line fake:
```csharp
class FakeCostProvider : ICardCostProvider {
    public int GetCost(CardType t) => (int)t;
}
```
and run `HandData.Distribute(...)` in a plain `[Test]` without booting Unity, without a ScriptableObject asset, without any serialization. The original version dragged `CardDataListSO` into every test path — you'd have had to create a test SO in the editor first.

### 3. Smaller API surface → lower cognitive load
`ICardCostProvider` exposes one method. When you read `CardHandManager` and see `_costs.GetCost(card)`, you know the exact capability it's using. Compared to a `CardDataListSO` reference — which advertises `List<CardDataSO>`, `GetCardDataByType`, and whatever else — you had to scan the manager to figure out what it actually used.

### 4. The NetworkVariable case shows this benefit more sharply
The old manager had:
```csharp
NetworkVariable<float> maxVar = _serverManaManager.GetMaxManaNetworkVariable(team);
maxVar.OnValueChanged += handler;
```
The manager knew about **Netcode**. That's a whole networking library leaking into a client-side card system. With `IMaxManaProvider.OnMaxManaChanged`, the manager just sees a C# event — completely unaware that Netcode exists. If you later swap to an offline single-player mode with a different mana source, the card manager doesn't care.

### 5. Additive, not invasive
Notice `CardDataListSO` still has its original `GetCardDataByType` method — nothing was removed. Other code that uses the SO directly (like `CardTowerDeployer`) still works. Adding the interface costs nothing at call sites that don't need the abstraction.

---

## How to improve your coding

Concrete habits, ordered by impact for this project.

### Ask "what does this class actually need?" before declaring a field
When you're about to write `[SerializeField] private SomeBigSO _data`, pause. What methods/properties do you call on `_data`? If it's one or two, that's an interface candidate. You pull in the big concrete type only at the boundary (a MonoBehaviour field, a factory, or the bootstrap) — the **logic classes** depend on the narrow interface.

### Let data classes own their transitions
Before the refactor, `HandData` was a passive struct-of-lists and the manager mutated its fields directly (`handData.HandCards.Remove(...)`, `handData.QueuedCards.Enqueue(...)`). That's **procedural code wearing an OO mask** — the data and the operations on it are in different places.

Rule of thumb: **if you can describe an operation in terms of a single object's state ("play a card from this hand"), the method belongs on that object.** If you can't (because it needs external collaborators), then it belongs on a coordinator. `Play(CardType)` fits on `HandData`. `SubscribeToMaxMana()` does not — it needs `IMaxManaProvider`.

### Return booleans/enums for operations that can fail silently
`HandData.Play` returns `bool` (did something actually change?). `HandData.Unlock` returns `bool`. The caller decides whether to publish events or log. This is better than both:
- Silently doing nothing (hides bugs).
- Throwing exceptions (adds noise for expected flows).

Your own `CardValidation` struct already does this — it carries `IsValid` + a reason enum. Apply the same pattern elsewhere.

### Avoid "manager classes" that do too much
When you find yourself writing a class called `XyzManager` that:
- Holds state,
- Resolves dependencies,
- Runs algorithms,
- Fires events,
- Touches UI,

…split it. The coordinator (wiring + events) stays as `XyzManager`. The algorithm moves to a static helper or a strategy class. The state moves to a data class that knows how to mutate itself. You'll know it worked if each piece fits on one screen.

### Think in terms of boundaries, not layers
The old code crossed many boundaries in one class: Unity (ScriptableObject), Netcode (NetworkVariable), domain (card game rules), infrastructure (ServiceLocator). Each boundary is a place where a different concern lives. A good class **owns one boundary** and depends on interfaces to talk across others. `CardHandManager` is now purely a domain coordinator; the Unity/Netcode/infra concerns live behind interfaces.

### Read the Dependency Inversion Principle directly
Bob Martin's original articulation is short and worth rereading twice a year:
> High-level modules should not depend on low-level modules. Both should depend on abstractions.
> Abstractions should not depend on details. Details should depend on abstractions.

`CardHandManager` (high-level card game logic) shouldn't depend on `CardDataListSO` (a low-level Unity asset detail). Both depend on `ICardCostProvider` (the abstraction). The SO implements the abstraction; the manager consumes it.

### Don't over-apply interfaces
One caveat — don't interface-ify everything. A good heuristic: **add an interface when there's real value** (testing, swappability, clear API shrinkage). `HandData` itself doesn't need an `IHandData` interface right now — there's only one kind of hand and no test that benefits. If you later add an `AIHandData` with different rules, you extract the interface then.

The mistake juniors make is "interface everything because SOLID." The mistake seniors make is "never extract interfaces because YAGNI." The right cut is: **interface across boundaries (Unity↔domain, network↔domain, data↔logic); don't interface within a tight unit of code that's cohesive and stable.**

---

## TL;DR
The two interfaces added (`ICardCostProvider`, `IMaxManaProvider`) each mark a real boundary: **Unity-asset↔game-rules** and **networking↔game-rules**. The card system is now a pure domain class that doesn't know whether its inputs come from a SO or from Netcode. That's the shape you want for game logic in a multiplayer project — it'll still work when you add replay mode, AI opponents, or a server simulation.

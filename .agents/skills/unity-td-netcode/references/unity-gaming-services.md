# Unity Gaming Services — Multiplayer Services SDK

Reference for the unified Multiplayer Services SDK (`com.unity.services.multiplayer`), which replaces
the standalone Relay, Lobby, and Matchmaker packages in Unity 6. This is the recommended approach
for all new multiplayer projects.

**Package**: `com.unity.services.multiplayer` (v1.2+)
**Namespace**: `Unity.Services.Multiplayer`
**Replaces**: standalone `com.unity.services.relay`, `com.unity.services.lobby`, `com.unity.services.matchmaker`

---

## Table of Contents

1. [Overview: Sessions vs Manual Relay+Lobby](#overview)
2. [Installation & Setup](#installation)
3. [Authentication](#authentication)
4. [Creating a Session (Host)](#create-session)
5. [Joining a Session (Client)](#join-session)
6. [Session Lifecycle & Events](#session-lifecycle)
7. [Session Query & Browsing](#session-query)
8. [Host Migration](#host-migration)
9. [Reconnection](#reconnection)
10. [Matchmaking](#matchmaking)
11. [Integration with Netcode for GameObjects](#ngo-integration)
12. [Error Handling & Mobile Considerations](#error-handling)

---

## 1. Overview: Sessions vs Manual Relay+Lobby {#overview}

**Before (deprecated pattern):** You manually coordinated Relay allocations, Lobby creation,
join codes, and heartbeats across three separate SDKs. Error-prone and verbose.

**Now (Sessions API):** A single `CreateSessionAsync` call handles lobby creation, relay
allocation, and Netcode connection automatically. The MPS SDK manages the full lifecycle.

```
OLD WAY (3 separate SDKs):
  1. RelayService.Instance.CreateAllocationAsync(...)
  2. RelayService.Instance.GetJoinCodeAsync(...)
  3. LobbyService.Instance.CreateLobbyAsync(...)
  4. Set relay data on UnityTransport
  5. NetworkManager.StartHost()
  6. Manually heartbeat the lobby every 15s

NEW WAY (Sessions API):
  1. MultiplayerService.Instance.CreateSessionAsync(options)
     → Lobby + Relay + Netcode connection all handled automatically
```

---

## 2. Installation & Setup {#installation}

### Required Packages

```
com.unity.services.multiplayer     (Multiplayer Services SDK — v1.2+)
com.unity.netcode.gameobjects      (NGO — v2.4+, currently v2.6.0)
com.unity.transport                (Unity Transport — v2.4+)
com.unity.services.authentication  (pulled in automatically)
```

Install via Package Manager → Unity Registry → "Multiplayer Services".

**Migration note:** If you have the standalone `com.unity.services.relay` or `com.unity.services.lobby`
packages installed, the MPS SDK will warn you about incompatibilities. Remove the standalone packages
and migrate to the unified SDK.

### Unity Dashboard Setup

1. Create a project in Unity Cloud Dashboard.
2. Under **Gaming Services → Multiplayer**, enable Relay (and Lobby/Matchmaker if needed).
3. In Unity Editor: **Edit → Project Settings → Services** → Link to your cloud project.

---

## 3. Authentication {#authentication}

All UGS services require authentication. For development, use anonymous sign-in:

```csharp
using Unity.Services.Authentication;
using Unity.Services.Core;

public async Task InitializeServices()
{
    await UnityServices.InitializeAsync();

    if (!AuthenticationService.Instance.IsSignedIn)
    {
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
}
```

Call this once at app startup (e.g., in a `Bootstrap` MonoBehaviour in your MainMenu scene)
before any session operations.

---

## 4. Creating a Session (Host) {#create-session}

```csharp
using Unity.Services.Multiplayer;

public class SessionManager : MonoBehaviour
{
    private ISession _currentSession;

    public async Task<string> CreateSession(string sessionName = "Battle", int maxPlayers = 2)
    {
        var options = new SessionOptions
        {
            Name = sessionName,
            MaxPlayers = maxPlayers, // includes the host
        }.WithRelayNetwork(); // uses Relay for NAT traversal — essential for mobile

        _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

        Debug.Log($"Session created! Code: {_currentSession.Code}");
        return _currentSession.Code; // share this with the other player
    }
}
```

**What happens behind the scenes:**
- MPS creates a Lobby (public by default, can be set private).
- MPS allocates a Relay server in the closest region.
- MPS configures `UnityTransport` with relay data.
- MPS calls `NetworkManager.Singleton.StartHost()`.
- The session is now live and joinable.

### Session Options

| Option | Type | Purpose |
|---|---|---|
| `Name` | `string` | Display name for session browsers |
| `MaxPlayers` | `int` | Max players including host (2 for 1v1 PvP) |
| `IsPrivate` | `bool` | If true, session won't appear in queries |
| `Password` | `string` | Password-protect the session |
| `IsLocked` | `bool` | Prevent new players from joining |

### Private Sessions (invite-only)

```csharp
var options = new SessionOptions
{
    MaxPlayers = 2,
    IsPrivate = true, // won't appear in QuerySessions results
}.WithRelayNetwork();
```

Players must use the join code to enter.

---

## 5. Joining a Session (Client) {#join-session}

### Join by Code (primary flow for PvP)

```csharp
public async Task JoinByCode(string joinCode)
{
    _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
    Debug.Log($"Joined session: {_currentSession.Id}");
    // NetworkManager is now connected as a client automatically
}
```

### Join by Session ID

```csharp
public async Task JoinById(string sessionId)
{
    _currentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
}
```

### Race-Safe Create-or-Join

When multiple clients might try to create the same session simultaneously:

```csharp
public async Task CreateOrJoin(string sessionId)
{
    var options = new SessionOptions
    {
        MaxPlayers = 2
    }.WithRelayNetwork();

    _currentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionId, options);
}
```

This atomically creates the session if it doesn't exist, or joins if it does.

---

## 6. Session Lifecycle & Events {#session-lifecycle}

### Host Operations

```csharp
// Lock session (prevent new joins during gameplay)
_currentSession.IsLocked = true;
await _currentSession.SaveAsync();

// Kick a player
await _currentSession.AsHost().RemovePlayerAsync(playerId);

// End the session
await _currentSession.LeaveAsync(); // host leaving destroys the session
```

### Client Operations

```csharp
// Leave the session gracefully
await _currentSession.LeaveAsync();
```

### Session Events

```csharp
// Subscribe to player join/leave events
_currentSession.PlayerJoined += OnPlayerJoined;
_currentSession.PlayerLeft += OnPlayerLeft;
_currentSession.SessionPropertiesChanged += OnPropertiesChanged;
_currentSession.Deleted += OnSessionDeleted;

private void OnPlayerJoined(ISession session, ISessionPlayerEvent evt)
{
    Debug.Log($"Player joined: {evt.PlayerId}");
    // Good place to trigger match start if both players are in
}

private void OnPlayerLeft(ISession session, ISessionPlayerEvent evt)
{
    Debug.Log($"Player left: {evt.PlayerId}");
    // Handle disconnect — forfeit the match or wait for reconnect
}
```

### Session Observer (watch sessions you're not in)

```csharp
// Observe a specific session type
var observer = new SessionObserver(sessionType: "ranked");
observer.AddingSessionStarted += (obs, evt) => Debug.Log("New ranked session starting");
```

---

## 7. Session Query & Browsing {#session-query}

For a matchmaking lobby browser:

```csharp
public async Task<List<ISession>> BrowseSessions()
{
    var queryOptions = new QuerySessionsOptions
    {
        // Only returns public, non-full sessions by default
    };

    var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
    return results.Sessions;
}
```

Display session name, player count, and let the player pick one to join by ID.

---

## 8. Host Migration {#host-migration}

If the host disconnects, the session can migrate to a new host:

```csharp
var options = new SessionOptions
{
    MaxPlayers = 2,
}.WithRelayNetwork(new RelayNetworkOptions
{
    // Preserve the relay region on migration — reduces latency spike
    PreserveRegion = true,
});
```

The MPS SDK reallocates the Relay server and promotes a connected client to host.

**For a competitive 1v1 TD game:** Host migration is less critical since there are only 2 players.
If the host disconnects, the match is effectively over. Consider forfeiting the match instead
of implementing full host migration for v1.

---

## 9. Reconnection {#reconnection}

If a player disconnects but the session is still alive:

```csharp
public async Task TryReconnect()
{
    // Get sessions this player is still a member of
    var sessionIds = await MultiplayerService.Instance.GetJoinedSessionIdsAsync();

    if (sessionIds.Count > 0)
    {
        _currentSession = await MultiplayerService.Instance
            .ReconnectToSessionAsync(sessionIds[0]);
        Debug.Log("Reconnected to session!");
    }
}
```

**Important:** Reconnection only works if the player hasn't been removed from the session
by the host or by timeout. For a PvP card game, set a reconnection window (e.g., 30 seconds)
before declaring forfeit.

---

## 10. Matchmaking {#matchmaking}

For ranked or skill-based matchmaking, use Matchmaker integration:

```csharp
var options = new SessionOptions
{
    MaxPlayers = 2,
}.WithRelayNetwork();

var matchmakingOptions = new MatchmakingOptions
{
    QueueName = "ranked-1v1",
};

// This finds an opponent and creates/joins a session automatically
_currentSession = await MultiplayerService.Instance
    .MatchmakeSessionAsync(matchmakingOptions, options);
```

**Setup required:** Configure matchmaking queues and pools in the Unity Cloud Dashboard
under **Gaming Services → Matchmaker**. Define skill tiers, region preferences, etc.

---

## 11. Integration with Netcode for GameObjects {#ngo-integration}

The MPS SDK automatically manages `NetworkManager` when creating/joining sessions.
You don't need to call `StartHost()` or `StartClient()` manually — the SDK does it.

### What You Still Handle

| Your responsibility | MPS handles |
|---|---|
| Game logic (spawning towers, cards, etc.) | Relay allocation |
| `OnNetworkSpawn` / `OnNetworkDespawn` | Lobby lifecycle |
| `NetworkVariable` and RPC design | Transport configuration |
| Spawn registered prefabs | `StartHost()` / `StartClient()` |
| Match flow and win conditions | Heartbeats and session cleanup |

### Detecting Connection in Game Code

```csharp
public class BattleManager : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Session is live — both players are connected
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.ConnectedClientsIds.Count == 2)
        {
            StartMatch(); // both players are in — begin the game
        }
    }
}
```

### Player Name Integration

```csharp
// When creating or joining a session, attach the player's name
var createOptions = new SessionOptions
{
    MaxPlayers = 2,
}.WithRelayNetwork()
 .WithPlayerName("PlayerOne"); // synced to all session members

// Later, read other players' names
foreach (var player in _currentSession.Players)
{
    string name = player.GetPlayerName();
}
```

---

## 12. Error Handling & Mobile Considerations {#error-handling}

### Network Error Handling

```csharp
public async Task SafeCreateSession()
{
    try
    {
        var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
        _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
    }
    catch (SessionException e)
    {
        Debug.LogError($"Session error: {e.Message} (Code: {e.Error})");
        // Show user-friendly error in UI
    }
    catch (AuthenticationException e)
    {
        Debug.LogError($"Auth error: {e.Message}");
        // Retry sign-in
    }
    catch (RequestFailedException e)
    {
        Debug.LogError($"Service error: {e.Message}");
        // Network issue — show "check your connection" UI
    }
}
```

### Mobile-Specific Concerns

- **App backgrounding (iOS/Android):** When the app goes to background, the transport connection
  may drop after ~30 seconds. Handle `Application.focusChanged` to detect this and trigger
  reconnection when the app returns to foreground.
- **Cellular network switches:** Wi-Fi → 4G transitions kill the socket. Implement reconnection
  logic that listens for `NetworkManager.OnClientDisconnectCallback`.
- **Session timeout:** Sessions auto-expire if no heartbeat is sent. The MPS SDK handles
  heartbeats automatically, but if the host's app is killed, the session dies.
- **Relay regions:** The SDK auto-selects the closest relay region. For a global PvP game,
  both players connect to the same relay — latency depends on the relay's distance to
  the furthest player.

### Full Connection Flow for the TD Card Game

```
1. App Start
   └─ InitializeAsync() + SignInAnonymouslyAsync()

2. Main Menu → "Play" button
   └─ Player picks: Quick Match (matchmaking) or Private Match (join code)

3a. Quick Match
    └─ MatchmakeSessionAsync() → auto-paired with opponent → battle scene

3b. Private Match (Host)
    └─ CreateSessionAsync() → get join code → share code → wait for opponent

3c. Private Match (Join)
    └─ JoinSessionByCodeAsync(code) → connected → battle scene

4. Battle
   └─ OnNetworkSpawn → game logic runs via NGO (NetworkVariables, RPCs, spawning)
   └─ Both players connected → StartMatch()

5. Match End
   └─ session.LeaveAsync() → back to main menu

6. Disconnect Recovery
   └─ OnClientDisconnect → try ReconnectToSessionAsync()
   └─ If reconnection fails within 30s → forfeit match
```

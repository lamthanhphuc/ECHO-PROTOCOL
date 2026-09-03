# M2-042 / M2-043 Networking authority handoff

## 1. Authoritative match UUID

- Owner: `MatchAuthorityService` on the persistent `EchoNetworkRunner`; replicated by the Host's `LobbyPlayerState` player object.
- The Host creates one `Guid.NewGuid()` after Fusion is running. Room name and `PlayerRef` are never used as the ID.
- Wire type: `NetworkString<_64>` (`MatchIdValue`), written only through `SetAuthoritativeMatchId` on State Authority.
- Read/API: `IHostAuthority.TryGetMatchId`; ready callback: `MatchAuthorityService.MatchIdReady`.
- Player objects use `NetworkSpawnFlags.DontDestroyOnLoad`. If the lobby object is replaced for gameplay, the service retains the UUID and writes the same value to the replacement Host object.
- Fusion's Networked state snapshot gives a late joiner the same value.

## 2. Authoritative match lifecycle

- `LobbyManager.TryStartMatch` explicitly calls `MatchAuthorityService.TryStartMatch` after ready validation and before loading `Game`. Scene load callbacks never start a match.
- Only `runner.IsServer` can raise `MatchStarted(Guid)` or `MatchEnded(Guid, AuthoritativeMatchEndReason)`.
- `MATCH_STARTED` is sequence 1. Gameplay sequences begin at 2. End uses the next sequence and `_ended` rejects all later events.
- Graceful Host leave/shutdown emits `HostShutdown` before shutting down Fusion.
- Host migration is outside MVP. An abrupt Host/network loss terminates the session; clients must not synthesize an authoritative end. Backend should close an abandoned Host binding by lease/timeout as `HostDisconnected`.

## 3. Unified Host authority API

Use `IHostAuthority` / `MatchAuthorityService`; telemetry must not discover runners itself:

```csharp
bool IsAuthoritativeHost { get; }
bool TryGetMatchId(out Guid matchId);
NetworkRunner Runner { get; }
int? GetAuthorityTick();
```

## 4. Backend User UUID mapping

- Mapping is `PlayerRef -> PlayerId -> Backend User.Id` in `MatchAuthorityService` roster. `PlayerId` is a match-local logical ID derived deterministically from the Fusion actor ID.
- Each owned `LobbyPlayerState` parses `AuthSession.CurrentUserId`, then sends it through `RpcSubmitBackendUserId` (Input Authority -> State Authority).
- Photon wire type is `NetworkString<_64>` containing canonical UUID text. No JWT or password is sent through Photon.
- Host validates RPC source equals object Input Authority, that Fusion maps the source to this owned player object, and that the value parses as UUID.
- `BackendUserIdValue` is Networked, so late join state is included in Fusion snapshots. Replacement gameplay objects resubmit from the local authenticated session.
- Disconnected roster entries are retained until match teardown for telemetry authorization/audit.
- Photon ownership validation does not prove that a claimed UUID belongs to a backend account. The backend registration endpoint must bind the Host JWT subject and validate participant identity via a server-issued join proof (deferred to Member D).

## 5. Authoritative roster

`AuthoritativeRosterEntry` exposes match-local `PlayerRef`, logical integer `PlayerId`, backend UUID, and disconnected status. Service APIs:

```csharp
bool TryGetBackendUserId(PlayerRef player, out Guid userId);
bool ContainsBackendUser(Guid userId);
IReadOnlyCollection<Guid> GetRosterUserIds();
IReadOnlyCollection<AuthoritativeRosterEntry> GetRoster();
```

Host identity is the backend UUID mapped to the Host's player object. `RosterChanged` drives backend roster registration/update.

## 6. Backend Host binding contract (Member D)

- Host creates the match UUID in Photon/Unity; backend must not replace it.
- Immediately after `MatchIdReady` and Host UUID mapping, Host calls an authenticated create/register-match endpoint using its own JWT. Backend stores `matchId -> JWT subject User.Id` as the registered Host.
- Send full roster at registration and idempotent updates on `RosterChanged` (join/disconnect). Retain disconnected membership until match closes.
- Telemetry authorization must require: valid sender JWT; JWT `sub` equals registered Host User.Id; event `userId` belongs to stored match roster; match is open; sequence is monotonic; event ID/sequence is idempotent.
- On graceful leave, close with `MATCH_ENDED`. On abrupt Host loss, reject new events and close through a short backend lease timeout. Do not migrate Host in MVP.
- Forbidden: hard-coded secret, `X-Is-Host`, client-declared `isHost`, forwarding another player's JWT, or allowing any JWT to submit for any user.

## 7. Authoritative gameplay callback contract

Subscribe to `MatchAuthorityService.GameplayFactAccepted`. `AuthoritativeGameplayFact` includes `matchId`, monotonic sequence, authority tick, kind, `PlayerRef`, logical `PlayerId`, backend UUID, `NetworkId subjectId`, and transition code.

Kinds are provided for core pickup/drop/place, phase transition, player down/revive/eliminated/escaped, Host-accepted noise, puzzle transition, and objective transition. Producers call `TryPublishGameplayFact` only after State Authority accepts/mutates state. `NetworkPickupItem` is wired as the first real producer (`CorePickedUp`). Remaining gameplay owners should wire their State Authority mutation sites to the corresponding kind; the common contract prevents duplicate identity ambiguity.

## 8. 2-4 player test harness

- Current harness scenes: `Bootstrap -> Lobby -> Game`; runner object/prefab is `EchoNetworkRunner`; player prefab is `Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab`.
- Default session name: `EchoProtocol` (or enter the same room name on all instances).
- Open Unity in non-Play mode, create one Windows build, run 1 Editor Host plus 1-3 build Clients. Sign in as a different backend user in each instance before joining.
- Verify Host logs `Host created matchId=...`, `MATCH_STARTED ... sequence=1`, and `authoritativeHost=True`. Every client must log the identical match ID.
- Verify roster logs `PlayerRef -> PlayerId -> backendUserId`. Join/leave a client; disconnected status remains. Join a new client after start to verify UUID and roster snapshot.
- Graceful Host leave must log `MATCH_ENDED ... HostShutdown`; clients return to Bootstrap. Kill the Host process to test abrupt disconnect; clients must not emit an end or telemetry.
- Subscribe a temporary diagnostic listener to `GameplayFactAccepted`, or inspect `[MatchAuthority]` logs. Clients should receive replicated state but `TryPublishGameplayFact` must return false because `IsAuthoritativeHost` is false.

## 9. Delivery status

No scene or prefab was edited. Unity Editor/MCP validation is still required because the Editor readiness state was unavailable. Remaining work: Member D backend endpoints/lease/join proof; gameplay owners wiring the remaining fact kinds; 2-4 instance runtime evidence.

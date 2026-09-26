# Networking

Local development runs with no credentials and no external services. `LocalNetworkService` reports `LOCAL_TEST_MODE` and is authoritative in-process.

## Authority

`LocalMatchAuthority` validates commands before it changes state.

Accepted only after checks:

- capture, from server-side discovery or an explicit debug call
- rescue, when the caller controls the rescuer, the target is warehoused, and rescue rules allow it
- escape, when the caller is at the exit, holds any required key, and required missions are complete
- surveillance read, only for that player's own captured or surveillance state

Rejected always:

- `AcceptClientClaim` for escaped, mission complete, not seen, picked up, unlocked or captured

Clients must not be given a way to call the authority methods directly. A future transport should deliver intent ("interact with this exit") and let the authority repeat the checks.

`ILobbyService` creates and joins local room ids. `IPlayerDataService` stores profile settings. Neither stores suspicion, inventory, objectives or phase.

## Photon Fusion

Not integrated. No Fusion type is referenced.

Fusion 2.0.13 Stable (16 September 2026, build 2379) is the official line that lists Unity 6.0.x support, which matches this pin. Fusion 2.1.3 requires a newer Unity. The 2.0.13 package URL responds with Photon’s sign-in wall (`403 - No Access`). The SDK was not downloaded, and none of its API was written from memory.

When the Unity pin moves to a version Fusion supports, integration should:

- keep gameplay rules in Core
- run `LocalMatchAuthority` only on the state authority (host/server)
- give each mannequin input authority for its own commands
- replicate accepted state: phase, clock, player state, warehouse, suspicion, inventory, doors, objectives, guard state
- spawn networked objects on the authority, not from an arbitrary client
- use Fusion's predicted input and interpolation for movement instead of syncing transforms every frame
- store the App Id in Photon App Settings locally, never in git

Until that SDK is compiled in this project, local tests are not evidence of network correctness.

## PlayFab

Not installed. Add it only after online matches are validated, and only behind `IPlayerDataService` / `ILobbyService`. No titles, secrets or session tickets belong in the repository.

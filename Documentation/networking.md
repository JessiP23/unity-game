# Networking

Local development runs with no credentials and no external services. `LocalNetworkService` reports `LOCAL_TEST_MODE` and is authoritative in-process.

## Authority

`LocalMatchAuthority` validates commands before it changes state.

Accepted only after checks:

- capture, from server-side discovery or an explicit debug call
- rescue, when the caller controls the rescuer, the target is warehoused, and rescue rules allow it
- escape, when the caller is at the exit, holds any required key, and required missions are complete
- surveillance read, only for that player's own captured or surveillance state

- civilian reports, only during the night, from NPCs the authority simulates (`TryReport`)

Rejected always:

- `AcceptClientClaim` for escaped, mission complete, not seen, picked up, unlocked or captured

Clients must not be given a way to call the authority methods directly. A future transport should deliver intent ("interact with this exit") and let the authority repeat the checks.

`ILobbyService` creates and joins local room ids. `IPlayerDataService` stores profile settings. Neither stores suspicion, inventory, objectives or phase.

## NPCs

Only the authority simulates customers and staff: navigation, perception, suspicion, and reports. Clients apply `NpcSnapshot` (id, role, position, yaw, speed, behaviour state, visible awareness state) through `CustomerPopulationManager.ApplySnapshots`, which creates, moves, and removes proxies and never runs AI. Paths, perception rays, shopping lists, and suspicion values stay on the authority.

## Photon Fusion

SDK is imported at `Assets/Photon/` (Fusion 2.0.13 Stable, build 2379). Physics 2D is enabled only because Fusion's scene manager references `PhysicsScene2D`; the game remains 3D.

The App Id is stored locally in `.photon-app-id` and written into Photon App Settings by `python3 Tools/unity.py setup`. Both files are gitignored.

`FusionSession` starts a **Host** session when you press Play in the Editor. The host stays the match authority. Customer/player state is not replicated to remote clients yet.

```sh
python3 Tools/unity.py setup     # writes the App Id into PhotonAppSettings
python3 Tools/unity.py connect   # starts a host session and asserts Photon answers
python3 Tools/unity.py open      # Play: HUD shows Photon CONNECTING / CONNECTED
```

When Fusion 2.0.13 is installed, connect the existing seams. Do not rewrite Core. Exact steps are in `Documentation/npcs.md` under **Multiplayer limitations**. Summary:

- keep gameplay rules in Core
- run `LocalMatchAuthority` only on the state authority (host/server)
- give each mannequin input authority for its own commands
- customers and the AI guard stay authority-spawned; they must not use player slots
- replicate accepted state: phase, clock, player state, warehouse, suspicion, inventory, doors, objectives, guard state, NPC snapshots
- each tick: authority `CaptureSnapshots` → replicate list → clients `ApplySnapshots`
- spawn networked objects on the authority, not from an arbitrary client
- use Fusion's predicted input and interpolation for mannequin movement
- store the App Id in Photon App Settings locally, never in git

Until that SDK is compiled in this project, local tests are not evidence of network correctness.

## PlayFab

Not installed. Add it only after online matches are validated, and only behind `IPlayerDataService` / `ILobbyService`. No titles, secrets or session tickets belong in the repository.

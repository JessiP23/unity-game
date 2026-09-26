# Phase status

| Phase | Deliverable | Status |
| --- | --- | --- |
| 0 | Unity, dependencies, project configuration and test harness | Passed: import, setup, both smoke tests, Windows build |
| 1 | Rules, state, events, service boundaries | Passed: 7 core tests, 8 total EditMode |
| 2 | Input, movement, camera | Passed: movement PlayMode test |
| 3 | Vision, detection, suspicion | Passed: 13 EditMode, 3 PlayMode total |
| 4 | Physical object interaction | Passed: 4 PlayMode total; push wired, dedicated coverage pending |
| 5 | Inventory | Passed: 16 EditMode total |
| 6 | Missions | Passed: 24 EditMode total |
| 7 | Keys and doors | Passed: 26 EditMode total |
| 8 | Guard AI and hearing | Passed: 29 EditMode, 5 PlayMode total |
| 9 | Capture, warehouse, surveillance, rescue | Passed with phases 10–11 |
| 10 | Timer, escape, outcomes | Passed: dawn, victory, all-captured defeat |
| 11 | Local networking abstraction | Passed: claims rejected, lobby/profile separate from match |
| 12 | Fusion integration | Blocked: Photon sign-in required for Fusion 2.0.13. No invented API |
| 13 | Online multiplayer tests | Blocked on Fusion. Local authority tests cover the trust rules |
| 14 | Staged vision cost | Passed: out-of-range and out-of-FOV checks do not raycast |
| 15 | Player guard and push | Passed locally. Same guard body can be driven by a player |
| Backend | PlayFab accounts/lobby | Not installed. `IPlayerDataService` is in-memory only |
| Art | Models, environment, animations, audio, UI | After programming acceptance |
| Polish | Final refinement | After asset integration |

Latest Unity results after phases 9–11: EditMode 41 passed, PlayMode 7 passed, zero failed. The PlayMode total includes a prototype scene load that builds the night, guard, rescue console, escape door and surveillance terminal.

A prepared dependency is not evidence that a system is implemented or tested. Local hot-seat play is not online multiplayer.

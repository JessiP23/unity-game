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
| Redesign | Readable HUD, capture flow, fair detection, scoring, night schedule, poses, seeded shifts, six back-hall trials | Compiled and Core-tested 2026-10-07; needs an Editor play run. See redesign.md |
| Redesign 2–4 | Job pool and props, cameras, procedural poses, shopper reactions, guard roster, career gadgets, per-shift leaderboard with optional shared server | Compiled, 72 Core tests pass 2026-10-08; PlayMode and feel need an Editor run. Online co-op planned only: coop-plan.md |
| Redesign 5 | Hidden gems, outfits as camouflage, second floor with escalator and fire exit, lockdown shutter, closing-time light sweep, skylights | Compiled, 77 Core tests pass 2026-10-08; MezzanineTests need an Editor run. See redesign.md |
| Redesign 6 | Moving escalator and stairs, atrium with cameras, toy store with carousel and rattling shelves, two jobs, lockdown over the supermarket doors | Compiled, 77 Core tests pass 2026-10-09; MallTests need an Editor run. See redesign.md |
| Redesign 7 | Real fountain with wish, carousel models, Clothing try-on and E badge, fitted screens, plank capture, photo booth, nightly dare and streak | Compiled, 80 Core tests pass 2026-10-11; MallPolishTests and new art need a Mac run. See redesign.md |
| Redesign 8 | Busier mall: 10–20 shoppers growing 10% a night to +80%, group arrivals, two flash sales a night on tonight's job departments, live frame-rate budget up to 40, crowd LOD | Compiled, 91 Core tests pass 2026-10-12; MallCrowdTests (PlayMode) need a Mac run. See redesign.md |
| Redesign 9 | Hide in the crowd, crowds at jobs with quiet windows, heat map and job crowd chip, families/phone zombies/influencer/cleaners/restockers, shift card (Relaxed/Normal/Rush hour), night metrics | Compiled, 104 Core tests pass 2026-10-13; MallCrowdLifeTests (PlayMode) need a Mac run. See redesign.md |
| Art | Models, environment, animations, audio, UI | After programming acceptance |
| Polish | Final refinement | After asset integration |

Latest Unity results after phases 9–11: EditMode 41 passed, PlayMode 7 passed, zero failed. The PlayMode total includes a prototype scene load that builds the night, guard, rescue console, escape door and surveillance terminal.

A prepared dependency is not evidence that a system is implemented or tested. Local hot-seat play is not online multiplayer.

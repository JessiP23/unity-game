# Customers, staff, and reports

The store is full of ordinary people. They shop, and they can notice a mannequin that moves. They never chase and never capture; they tell security where they last saw it.

The people walking the aisles are **AI customers**, not extra players and not networked avatars. They do not consume mannequin slots. Four human-controlled mannequins plus one AI guard plus this civilian crowd is the intended local match.

This document describes the **final current implementation**. Anything that cannot yet be tested because Photon Fusion is not installed is marked **UNTESTED (Fusion)**.

## Store structure (concept map)

The playable store is the same supermarket from the *MANIQUÍES — El supermercado* floor plan: one night, one building, departments a shopper would recognise. The current prototype is a compact arrangement of those departments, not a visual remake of the poster.

| Concept | `ZoneType` | Role |
| --- | --- | --- |
| Entrada / Salida | `EntranceExit` | Customer spawn and exit |
| Cajas | `Checkout` | Pay, then leave |
| Supermercado | `Supermarket` | Grocery aisles |
| Ropa | `Clothing` | Shirt mission, display mannequins |
| Electrónica | `Electronics` | Gadget browsers |
| Hogar | `Home` | Housewares |
| Almacén | `Warehouse` | Restricted; capture / rescue |
| Zona de empleados | `Employee` | Restricted; staff only |
| Seguridad | `Security` | Restricted; cameras |

Customers cannot path into warehouse, employee, or security. Staff and the guard can. Lockable doors, emergency exits, and cameras stay on the existing interactable / surveillance systems.

## How it fits the existing game

| Layer | Existing | Added for NPCs |
| --- | --- | --- |
| Sensing | `GuardVisionSystem` (range, FOV, line of sight) | Logic moved to shared `VisionSensor`; the guard class is now a thin subclass, so guard code and tests are unchanged |
| Interpretation | `DetectionSystem` (guard: GREEN/ORANGE/RED, discovery, capture) | `AwarenessTracker` (civilians: UNAWARE, OBSERVING, SUSPICIOUS, REPORTING, with decay and no capture) |
| Authority | `LocalMatchAuthority` | `TryReport` accepts reports during the night, counts them, posts them to the security cameras, and republishes them on `Reports` |
| Guard | Noise leads to `Brain.Hear()` and `LastKnownPosition` | `InvestigateReports` treats a report like a noise at its last-known spot |
| Surveillance | Guard, players, doors, objectives, noises | `EntitySighting` (mannequin, customer, employee, guard: position only) and recent report alerts |

Customers reuse the guard's eyes but not its judgement. Bending `DetectionSystem` to decay suspicion and skip capture would have changed guard behaviour, so civilians get a sibling interpreter on the same sensor.

## NPC architecture (`Assets/Scripts/Game/Npc`)

Composition, not a second player stack. One body, one NavMesh agent, one sensor, one role behaviour.

| Component | Job |
| --- | --- |
| `NpcController` | Composition root. Ticks perception and behaviour on the state authority; applies `NpcSnapshot`s on clients |
| `NpcMovement` | NavMeshAgent wrapper: human acceleration and turning, avoidance priorities, stuck detection, facing |
| `NpcNavigation` | Turns "shop in Clothing" into a free, allowed `NavigationPoint` and holds its reservation |
| `NpcPerception` | `VisionSensor` plus one `AwarenessTracker` per mannequin; staggered sampling with distance and FOV rejection before any ray |
| `NpcBehavior` | Base for roles. Owns the shared civilian reaction: stop, stare at the last-known spot, submit a report |
| `CustomerBehavior` | Drives a `CustomerBrain` through departments, checkout, and the exit |
| `EmployeeBehavior` | Minimal staff loop over work spots, including staff-only zones. Not a full employee gameplay system |
| `NpcShopping` | Basket while shopping, bag after paying (**visual only**; shelf stock is never removed) |
| `NpcAnimationHooks` | Only link to looks: Rocketbox body via `CharacterProfile`, or a tinted capsule when art is missing |
| `CustomerPopulationManager` | Spawning, pooling, crowd ebb and flow, snapshots |
| `NpcDebugOverlay` | In-game labels, vision cones, focus lines, and paths |

Pure logic lives in Core (`Assets/Scripts/Core/Npc`) and is EditMode-tested: `ZoneType`/`ZoneAccess`, `ShoppingList` and generator, `CustomerBrain`, `AwarenessTracker`, `SuspiciousActivityEvent`, `NpcSnapshot`.

## Customer lifecycle

```
SPAWN → ENTER → WALKING (department) → BROWSING → SHOPPING (pick) → WAITING?
      → next department or CHECKOUT → LEAVING → GONE → recycle into the pool
```

An alert interrupts any of these. `ALERTED` means the customer stares and then shrugs it off; `REPORTING` means they wait for the report to go out, then either leave in a hurry or keep shopping (`LeaveAfterReport`). They never chase, follow, capture, or keep a hidden lock on the player.

Unreachable destinations use a safe fallback: retry the same spot → choose another valid spot → skip the item / leave. A leaving customer that cannot reach an exit is removed after `ExitTimeoutSeconds`. A visit that lasts longer than `MaxVisitSeconds` also walks out. No customer is allowed to stand stuck.

Shopping lists come from `ShoppingCatalog`, weighted by each profile's preferred departments, with each department visited once in a shuffled order. Every customer rolls their own speed, browse times, sensitivity, notice, reaction, and report delays inside their profile's ranges.

## Customer personalities

Five ScriptableObject archetypes under `Assets/Resources/Npc/`. They share one AI; only data changes.

| Profile | Walk | Browse | Favourite department | Reaction | Sensitivity |
| --- | --- | --- | --- | --- | --- |
| Casual shopper | 1.0–1.4 | 3–8 s | Supermarket | average | average |
| In a hurry | 1.35–1.6 | 1.5–3.5 s | Supermarket | slower to bother | lower |
| Fashion browser | 0.9–1.2 | 5–10 s | Clothing | average | average |
| Gadget fan | 1.0–1.4 | 4–9 s | Electronics | a bit slower | a bit lower |
| Nosy neighbour | 0.9–1.3 | 3–7 s | Home | fastest | highest (wider FOV / range) |

## Perception

`VisionSensor` is shared: distance → field of view → one obstacle ray. Customers use short range and a narrow cone (about 7 m / 70°, nosy neighbour 8.5 m / 85°). The guard uses the stronger `GameRules` values (12 m / 90°) plus the flashlight. They must not feel the same.

Targets are free mannequins (`PerceptionTarget.Noticeable`). Captured players in the warehouse are ignored. Other NPCs are not targets. Samples run every `perceptionInterval` (0.2 s) with a random phase.

## Suspicion

```
NORMAL (Unaware)
  ──sees it──▶ BEING OBSERVED (Observing)
                 ──sees it move after NoticeTime, suspicion ≥ threshold──▶ SUSPICIOUS
                                                                              │ ReactionTime
                                                                              ▼
                                                                         REPORTING ──ReportDelay──▶ one report
```

Suspicion rises only while the observer can see the mannequin **and** it moves faster than `MovementThreshold` **and** they have been looking for at least `NoticeTime`. A still mannequin slowly drains suspicion; it is just a mannequin. Out of sight, suspicion decays and the observer eventually forgets. It does not persist forever and does not reveal hidden player identity.

The player HUD shows **People: NORMAL / BEING OBSERVED / SUSPICIOUS** only. No civilian suspicion number is shown.

## Reporting

A `SuspiciousActivityEvent` carries:

- source customer id
- target mannequin id
- last-known `MapPoint` (updated **only while visible**)
- zone of that point
- timestamp
- `ReportKind.MovingMannequin`
- confidence (the observer's suspicion at send time)

**Critical:** after line of sight is lost, the report does not follow the mannequin. Seen at A, lost, then moved to B then C → the report still points at A.

`LocalMatchAuthority.TryReport` accepts reports only during the night. Accepted reports increment night stats, appear as camera alerts, and publish on `Reports` for the guard.

After reporting, the customer forgets the tracker and either leaves shaken or resumes shopping. They do not keep tracking.

## Guard investigation

```
customer report → LocalMatchAuthority.TryReport → Reports stream
  → GuardController.InvestigateReports → Brain.Hear() at last-known
  → NavMesh walk to that spot → Search
```

The guard physically walks. It never teleports. It never learns the mannequin's current hidden position from the report. The same `BeginInvestigation` path is used for noise and for civilian reports, and it forces `agent.isStopped = false` so a guard already standing at an old waypoint cannot "arrive" without walking.

## Population

`Assets/Resources/Npc/NpcPopulation.asset`:

- minimum 6, target wave 6–12, hard cap 16
- 7 already inside at night start
- spawn interval 6 s, crowd cycle 180 s
- 2 employees
- spawn at entrance spawn points, leave at exit points, recycle into a pool

No duplicate active ids, no destroyed-object references, no population leaks. Do not raise the cap here; balancing comes later.

## Employee architecture

Employees use the same controller / movement / navigation / perception / report path. They do **not** use `CustomerBrain`, shopping lists, or baskets. They walk work spots, including restricted staff zones. Stocking, registers, and schedules are not built yet.

## Object interactions and the clothing mission

Customer "picks" are visual (`NpcShopping`). They do not take, move, or destroy `PhysicalItem` mission objects. The shirt stays on the clothing table until a mannequin picks it up.

"Steal a shirt from Clothing" still runs on the existing mission system: shirt `ItemDefinition` (`id = shirt`) → inventory → tracker complete. Customers cannot complete or break it.

## Authority model

| Who | Simulates |
| --- | --- |
| State authority (`INetworkService.IsAuthority`) | NavMesh, perception rays, suspicion math, shopping lists, reports, spawn/recycle |
| Remote clients | `ApplySnapshots` only: create / move / remove proxy bodies. Agents disabled. No AI. |

`NpcSnapshot` is the only NPC payload: id, role, position, yaw, speed, behaviour byte, visible awareness. Not replicated: raycasts, path corners, shopping lists, suspicion numbers, internal timers.

Customers are not player objects. Four mannequin `PlayerRecord`s remain the only player slots.

**UNTESTED (Fusion):** real host/client replication, interpolation over a network tick, late join, host migration, and packet loss. In-process snapshot tests exist (`ProxyNpcNeverMovesOnItsOwn`).

## HUD and debug

| Surface | Values |
| --- | --- |
| Guard detection | GREEN / ORANGE / RED |
| People | NORMAL / BEING OBSERVED / SUSPICIOUS |
| Debug Z | Labels: role, state, destination, list, awareness |
| Debug X | Vision cone, focus line, last-known marker |
| Debug Y | Current NavMesh path |

Letter keys: Mac keyboards send F1–F12 as media keys. Debug drawing is development-only and adds no colliders.

## Configuration

Editable data stays out of gameplay scripts:

- `Assets/Resources/Npc/NpcPopulation.asset` — crowd size, employees, employee vision
- `Assets/Resources/Npc/Customer *.asset` — the five personalities
- `python3 Tools/unity.py setup` (or *Create NPC Profiles*) recreates missing assets from code defaults

## Performance

Each NPC samples every 0.2 s with a random phase. Distance and FOV reject most pairs before a ray. Targets are the four mannequins, not other NPCs. Bodies are pooled. A snapshot is a small struct (well under 48 bytes unpacked).

A PlayMode budget check at the hard cap writes `TestResults/performance.txt`. Do not raise the population toward 50 without a player-build profile.

## Multiplayer limitations — UNTESTED (Fusion)

Photon Fusion 2.0.13 is imported. A host session can reach Photon Cloud (`python3 Tools/unity.py connect`). Customer and mannequin state are **not** replicated to remote clients yet. Do not treat a host connect as a finished multiplayer match.

When wiring the rest, connect the **existing** seams. Do not rewrite Core.

1. Sign in at the Photon dashboard, create a Fusion app, put the App Id in Photon App Settings locally. Never commit it.
2. Import Fusion 2.0.13. Do not invent API from memory before the package compiles.
3. Keep `LocalMatchAuthority` on the state authority only. Clients send intent (move, interact, escape), never "I completed the mission" / "I was not seen".
4. Give each mannequin input authority. Customers and the AI guard stay authority-spawned; they must not use player spawn slots.
5. Replicate accepted match state: phase, clock, player state, warehouse, guard detection, inventory, doors, objectives, guard brain/pose.
6. Each network tick, authority calls `CustomerPopulationManager.CaptureSnapshots` and replicates the list. Clients call `ApplySnapshots`. Do not replicate rays, paths, or suspicion floats.
7. Publish `LocalMatchAuthority.Reports` from the authority; clients render alerts only.
8. Use Fusion prediction/interpolation for mannequin movement. NPC proxies can keep the current snapshot apply (or a short interpolation of position/yaw).
9. Spawn networked objects on the authority. `CustomerPopulationManager.Populate` already no-ops when `Authority` is false.
10. Re-run the in-process snapshot tests against a real host + 1 client before calling multiplayer done.

## Current known limitations

- **UNTESTED (Fusion):** all real-network behaviour above.
- Employee gameplay is a baseline only (walk, work, report).
- Customer population is capped at 16 by design.
- The prototype floor is a compact version of the concept map, not a 1:1 rebuild of the poster art.
- PlayFab is not installed.
- Visual design, lighting, atmosphere, and UI styling are owned separately and are not part of this NPC pass.

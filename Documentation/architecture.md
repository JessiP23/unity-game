# Architecture

Domain rules live in `NightSupermarket.Core` and do not reference Unity. Unity adapters in `NightSupermarket.Game` own input, CharacterController movement, physics perception, NavMesh, placeholder presentation and the primitive supermarket. `PrototypeRoot` constructs those pieces and schedules ticks. It does not decide win, loss, capture or rescue.

Match mutations go through `LocalMatchAuthority`. It owns the clock, phase flow, warehouse roster, escape ledger, surveillance board, lighting mode and audio cue stream. `MatchOutcomeEvaluator` is the only place that turns those facts into victory or defeat.

Player commands must match `IPlayerSession`. `AcceptClientClaim` ignores declarations such as "I escaped" or "I completed the mission". Validated methods re-check phase, identity, inventory and mission completion.

Networking and account interfaces (`INetworkService`, `ILobbyService`, `IMatchService`, `IPlayerDataService`) have local implementations. Profile storage keeps look sensitivity only. Match progress is not written there.

Presentation is replaceable. `PoseMap` exposes the pose an animator would play. `FlashlightModel` is the gameplay cone; a spot light only draws it. `AudioCue` is a hook with no clips. `PrototypeHud` lays out uGUI text with Unity's built-in font, and IMGUI otherwise.

`NightSupermarket.Game` is split by feature:

| Folder | Contents |
| --- | --- |
| `Player/` | `PlayerMotor` (fixed-step movement with acceleration), `PlayerView` (first-person camera), input, carry, inventory, interaction |
| `Guard/` | `GuardController` (NavMesh body, smooth turning, chase speed), vision, flashlight |
| `Items/` | Physical items (carve the NavMesh while resting), doors, rescue and escape interactables |
| `Missions/` | Mission definitions and the tracker bridge |
| `Presentation/` | Reusable visual building blocks, listed below |
| `UI/` | `PrototypeHud` layout, `HudText` formatting, `KeyCommandMap` |

Reusable presentation pieces, none of which feed gameplay:
- `MotionInterpolator` blends the last two fixed-step poses, so cameras and bodies render smoothly at any frame rate.
- `CharacterVisual` plus `CharacterProfile` put any Rocketbox body on any host, with stride-matched idle/walk/run blending.
- `ArtLibrary` loads fetched art and builds URP materials.
- `DressingKit`, `ShelfStocker`, and `SignFactory` dress any greybox: skins, blockers, stocked shelves, readable signs.
- `StoreLighting` presents each `LightingMode`.
- `StoreDressing` is the only file that knows this store's layout.

`KeyCommandMap` binds keys and documents them in one place, so the on-screen help can't drift from the input the game actually polls.

The local prototype runs two mannequin identities in one process. Tab changes which identity receives input. That is a test control scheme, not a second ruleset.

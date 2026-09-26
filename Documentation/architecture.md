# Architecture

Domain rules live in `NightSupermarket.Core` and do not reference Unity. Unity adapters in `NightSupermarket.Game` own input, CharacterController movement, physics perception, NavMesh, placeholder presentation and the primitive supermarket. `PrototypeRoot` constructs those pieces and schedules ticks. It does not decide win, loss, capture or rescue.

Match mutations go through `LocalMatchAuthority`. It owns the clock, phase flow, warehouse roster, escape ledger, surveillance board, lighting mode and audio cue stream. `MatchOutcomeEvaluator` is the only place that turns those facts into victory or defeat.

Player commands must match `IPlayerSession`. `AcceptClientClaim` ignores declarations such as "I escaped" or "I completed the mission". Validated methods re-check phase, identity, inventory and mission completion.

Networking and account interfaces (`INetworkService`, `ILobbyService`, `IMatchService`, `IPlayerDataService`) have local implementations. Profile storage keeps look sensitivity only. Match progress is not written there.

Presentation is replaceable. `PoseMap` exposes the pose an animator would play. `FlashlightModel` is the gameplay cone; a spot light only draws it. `AudioCue` is a hook with no clips. `PrototypeHud` uses TextMeshPro when an OS font can be built, and IMGUI otherwise.

The local prototype runs two mannequin identities in one process. Tab changes which identity receives input. That is a test control scheme, not a second ruleset.

# Detection plan

Separate perception, observation state and suspicion. Filter distance, then FOV, then physics line of sight. Measure authoritative displacement over time; ignore teleports and configurable jitter. Multiple guards combine visibility before observation updates. Test grace expiry/loss, stillness, movement cadence, occlusion and threshold boundaries. All pending.

Implemented in Phase 3: GuardVisionSystem returns distance/angle/LOS; filtering
precedes raycasts and ignores the guard layer. DetectionCoordinator consumes
actual CharacterController displacement. DetectionSystem owns grace/state and
SuspicionSystem owns integer increments: movement onset, then fixed cadence.
Suspicion persists when vision is lost; discovery persists until explicit reset.
Default discovery requires a further valid movement event after reaching the
threshold. Orange overflow time is applied to red, so large steps do not erase it.
Discovery notifies the match authority, which captures the mannequin. Vision can
also accept a flashlight cone, and that cone still requires line of sight. The
rendered light is not part of the check.

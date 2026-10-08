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

## Consistency correction — 2026-10-01
The guard capsule pivot is one metre above its feet. Its gameplay eyes now use a
0.6m local offset, matching the player-guard camera, rather than adding 1.55m to
that already elevated pivot. Guards and civilians share horizontal FOV checks:
vertical chest-to-eye angle no longer creates a blind spot at arm's length.
Within range/cone, check chest then head; full-height cover blocks both.

Detection uses the supplied simulation delta without a close-range multiplier.
Cover removes live sight immediately; the removed 0.35s hold no longer lets an
unseen player accumulate suspicion. Retained integer suspicion is a separate rule.
Guard AI receives the same sampled visibility as detection on each authority tick
instead of recasting and potentially disagreeing. Fallback direct guard tests still
use the same sensor. NPCs retain staggered samples (normally 0.2s); their displayed
memory no longer claims live sight, and their suspicion uses current actual speed
rather than extending a previous peak movement after stopping.

HUD labels distinguish GUARD (unseen / stop with seconds remaining / freeze with
suspicion count) and PEOPLE (watching / lost sight / remembers movement / calling
guard). Civilian reactions and security punishment remain different game rules.
The HUD refreshes at 10Hz; simulation remains at its configured fixed rate.

Regression coverage includes front distances 0.3/1/3/8m, low/full cover, reopening
sight, real grace seconds and immediate loss of sight. Existing tests cover rear
FOV, maximum range, stillness/jitter, discovery, NPC reports, capture and rescue.

## Display camouflage — 2026-10-02

A shirt, empty hands, grounded stillness and E at the model stand arm a display
pose. Moving away, turning more than 20 degrees, jumping, carrying or capture
cancels that pose. If posed when a new guard sighting begins, DetectionSystem
latches a two-second bonus to the usual orange grace. The HUD labels that window
DISGUISE and shows the remaining seconds. The earned window survives stepping
away, giving a useful chance to leave; it cannot be renewed while still visible.
Sight loss resets the encounter, using the same LOS rules as normal detection.
Existing suspicion and civilian memory are never erased by posing.

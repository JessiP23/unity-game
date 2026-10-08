# Redesign pass — 2026-10-07

One pass across the HUD, the capture flow, detection fairness, scoring, the night
schedule, poses, seeded layouts and the back hall. Everything compiles against the
Unity 6000.0.71 assemblies and the Core EditMode suite passes (62 tests, 16 new).
The Game assembly has **not** been run in the Editor in this pass: open the project,
run `python3 Tools/unity.py edit` and `python3 Tools/unity.py play`, then play a night.

## What a player sees now

| Moment | Before | Now |
| --- | --- | --- |
| Reading the HUD | Oswald everywhere, dots between words, 8–12 px text over the bright ceiling | Inter body font, no dots, 18 px minimum at a 1600x900 reference, shadows, panels under every line |
| Guard attention | "GUARD · · · UNSEEN" | GUARD UNSEEN / LOOKING AWAY / STOP 1.2s / FREEZE with three suspicion pips, plus a red vignette and a heartbeat that quicken as you are watched |
| Next job | A faint line on the ceiling | A NEXT chip with the job and its count; the gold world marker is unchanged |
| Being caught | Instant teleport into a yellow hall, no reason given | A 2 s CAUGHT card with the cause and the points lost, then THE BACK HALL card, then input returns |
| Back hall | 3 fixed rooms, messages wiped after one frame, broken floor between rooms | 3 of 6 rooms per capture (sweeping light, liar doors, echo pads, red light, odd one out, price tags), a rule sign on every gate, ROOM n / 3 marks, notes that stay 2.6 s, pad replay from the start strip, one hall per captive, continuous floor |
| Coming back | Dropped at the guard's first waypoint | The walkable release spot farthest from the guard, 3 s of cover, a BACK ON THE FLOOR card |
| Night | Flat six minutes | STORE OPEN → CLOSING (4:00) → LIGHTS OUT (2:00, torch on, guard quicker) → LOCKDOWN (1:00, emergency lights); the HUD counts down to each |
| Score | Grade from reports and captures only | Points: jobs +500, optional +300, close call +100, perfect pose +150, comeback +200, seconds left +10 each; reports −150, captures −400; an unseen streak multiplies gains x1.5 at 60 s and x2 at 120 s; the end card shows the breakdown and your best for this shift |
| Freezing | A single blue model stand | Hold RIGHT MOUSE to freeze in a pose, scroll to pick Display / Browsing / Lounging / Staff; the pose that fits the department buys the same +2 s doubt as the stand; holding past 8 s builds strain until the body twitches once |
| Replay | The same store every night | A daily shift number seeds crate, shirt, key and stand positions and the guard's route; ENTER replays the shift to beat your best, N rolls a new one |
| Debug keys | 20 testing keys always live (F skipped a minute) | Off by default; ` toggles them in the Editor or a development build and the Controls page then lists them |

## Second pass — job pool and props (same day)

Nine jobs now live in `JobCatalog` (Core). Shift 0 keeps the classic trio; every other shift
draws three with no two from the same family (carry / grab / hold / trick), so a night mixes
a carry, a hold-still and a trick:

| Job | Verb | Built by |
| --- | --- | --- |
| Collect three crates | carry | `JobBuilder.EnsureCrates` |
| Place a crate on the Clothing rug | carry | crates + `PlacementZone` |
| Steal a shirt | grab | `BuildShirts` |
| Model in the shop window 20 s | hold | `HoldSpot` zone by the checkout, auto-starts, keeps half its progress when you move |
| Swap places with a display mannequin | hold | `HoldSpot` on a marked plinth mannequin, only starts when nobody is looking, 6 s |
| Swap two price tags | trick | two `HoldSpot`s at shelf ends, 3 s each |
| Turn the checkout camera, cross the lane | trick | `HoldSpot` pole (2 s) that gates a `CrossZone` |
| Return the lost toy | grab | inventory item in Home, `ReturnPoint` on the service desk |
| Carry a vase to the Home rug | carry | breakable `PhysicalItem` with `Rattle` noise, Home `PlacementZone` |

`JobBuilder` owns the world side (positions, props, signs) and answers `Guide(job, player)` for
the gold marker, so `PrototypeRoot` no longer knows job names. New job = one template in
`JobCatalog.All`, one `Build…` method, one `case` in `Guide`.

Every prototype box is dressed through `ObjectiveDressing`: the box keeps its collider and
loses its renderer; a Poly Haven prop is fitted to a target height on top (crates →
`cardboard_box_01`, exit → `rollershutter_door` with a glowing EXIT sign, promo stands →
`wooden_crate_02` with fruit, terminal → `classic_laptop`, toy → `gamepad`, vases →
`ceramic_vase_01/02`). Shirts and rugs use fabric surfaces, the model stand is a white plinth,
the bell a brass dome on a wooden pedestal, the key a key card. Without the art pack every
box falls back to a muted colour instead of magenta. `JobPoolTests` asserts no magenta
placeholder is visible and that every drawn job is reachable on six different shifts.

## Rule changes (Core)

- `DetectionSystem`: losing sight still shows UNSEEN at once, but the guard's attention
  drains at `attentionDrain` (2) seconds per second instead of resetting. A sighting that
  resumes inside that window continues the old grace (`FreshSighting` is false), so a
  one-frame break in line of sight no longer restarts the 1.5 s and the warning sound
  no longer spams.
- `SuspicionSystem.Forget`: one suspicion point is forgiven per `suspicionDecay` (30)
  seconds spent out of every watcher's sight. 0 restores the old permanent memory.
- `NightScore`, `NightSchedule`, `PoseStrain` / `PoseLibrary`: new, pure, tested.
- `BackroomCourse`: six trials, every door shows a different number, difficulty (captures
  so far) lengthens the echo and speeds the lights.

Both new rule values live on `GameRulesAsset` (`suspicionDecay`, `attentionDrain`) and in
`Assets/Settings/GameRules.asset`.

## Tuning knobs

| What | Where | Default |
| --- | --- | --- |
| Phase thresholds | `NightSchedule.ClosingAt / DarkAt / LockdownAt` (fractions of the night) | 2/3, 1/3, 1/6 |
| Guard pace per phase | `NightSchedule.At` | 1 / 1.1 / 1.25 / 1.4 |
| Points and streak tiers | `NightScore` constants | see table above |
| Grade bands | `NightScore.Grade` | S 2500, A 1800, B 1200, C 600 |
| Pose comfort / build / recovery | `PoseStrain.Comfort / BuildSeconds / RecoverRate` | 8 s / 5 s / 2x |
| Release cover | `PrototypeRoot.Release` | 3 s |
| Comeback window | `PrototypeRoot.OnBackroomCleared` | 45 s, no misses |
| HUD reference size | `PrototypeHud.Configure` | 1600x900, scaled by height |

## Tests

- New EditMode: `ScoreTests`, `NightScheduleTests`, `PoseTests`; `DetectionTests` and
  `BackroomTests` describe the new behaviour.
- New PlayMode: `BackroomWalkTests` checks the hall floor is continuous and every hall
  label faces the player.
- Test runs use shift 0, the classic layout, so object positions in existing tests hold.
  `PrototypeRoot` detects the test runner and turns off pause-on-focus-loss.

## Known gaps

- Poses have no animation yet: the body leans with strain, nothing else. `CharacterVisual.HoldPose`
  is the hook for per-stance idle frames.
- Seeded positions come from hand-picked pools of open floor; add to the pools in
  `PrototypeRoot.BuildObjectives` rather than randomising freely.
- The job pool (window display, tag swap, tableau) from the review doc is not built; the
  mission assets are unchanged.
- `Assets/InitTestScene*.unity` are leftovers from interrupted test runs and can be deleted.

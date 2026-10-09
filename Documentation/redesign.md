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

## Third pass — cameras, poses you can see, shoppers who react, guards with character, gadgets

- **Camera modes.** C cycles eyes → over the shoulder → front. The front view stands in front of
  you looking back, so you see the pose you hold. Holding right mouse in first person swings to
  the front view by itself and back when you let go (`PlayerView.ShowPose`). A sphere cast keeps
  the camera out of shelves.
- **Visible poses.** The art pack has no pose clips, so `CharacterVisual.ApplyStance` bends the
  Biped's upper arms, forearms, head and spine procedurally over the frozen idle frame: Display
  (arms out, chin up), Browsing (hand to the shelf), Lounging (arms folded, leaning back), Staff
  (hands behind the back). The Euler offsets live in `CharacterVisual.StancePoses`; if an arm
  bends the wrong way on the rig, flip that sign.
- **Shopper reactions** (`ShopperReactions`). A calm shopper within 3.2 m who can see a mannequin
  that has held still for 1.5 s stops for a moment and says something in a speech bubble: a
  compliment, a poke question up close, or a selfie with a phone flash when you are posing. Each
  shopper admires each mannequin once (+75, "A shopper admired you") and reacts at most every
  ~30 s. Never a report.
- **Guards with character** (`GuardRoster`, Core). Six guards; the shift picks one and the start
  toast and Night page tell you who: Marco (by the book), Dana (sharp eyes, slow feet), Ilya
  (quick, short-sighted), Rosa (all ears), Teo (wide torch), Nadia (tunnel vision). They are
  multipliers on a private copy of the rules asset. Shift 0 is always Marco.
- **Career and gadgets** (`Career`, `GadgetKit`). Points from won nights add up in
  `PlayerPrefs` "ns-career". 3,000 unlocks the wind-up toy (X: a toy that walks off clattering,
  once a night); 6,000 the squeaky price gun (Z: a squeak where you aim, up to 14 m, twice a
  night). Both are noise the guard's hearing already follows. The end card shows career points
  and the next unlock. Gadget keys are off while testing keys are on. The testing "capture me" key
  moved from C to ;.

## Fourth pass — poses you can see from any rig, a leaderboard, and the co-op plan

- **Procedural poses on the real skeleton** (`CharacterVisual`). The Rocketbox pack ships
  only idle/walk/run clips, so poses are now built in `LateUpdate` on top of the animator:
  bones are found by name suffix (upper arms, forearms, head, spine, hands), each stance is a
  set of character-space directions (`StancePose`), and the arms/head are aimed with
  `Quaternion.FromToRotation` blended by `stanceWeight`. Works on any humanoid whose bones carry
  the usual names; tune directions in `StancePose.For(Stance)` by eye with the C front camera.
- **Leaderboard per shift** (`Leaderboard` in Core, `LeaderboardStore` in Game). One best per
  name, ten entries, `name|points|grade|date` lines. Stored in `PlayerPrefs` "ns-board-<shift>";
  the end card shows the shift's board on the right with your row in green, your rank, and
  your name. **TAB** on the end card edits the name (letters, digits, space, `_`, `-`, 16 max;
  ENTER saves and carries tonight's entry over). The start toast names who holds the top spot.
- **Shared board, optional.** `Tools/leaderboard_server.py` is a dependency-free HTTP server
  (GET/POST `/shift/<n>`, same line format, merges by best-per-name). Set its address once via
  *Night Supermarket ▸ Leaderboard ▸ Settings…* (PlayerPrefs "ns-board-url"). The game posts
  after each night and merges the reply; offline it says so on the card and keeps everything
  locally. Test runs never post.
- **Online co-op** is documented, not built: [coop-plan.md](coop-plan.md) lists what each
  new system replicates and the order to do it in. Nothing in Core changes for it.
- **Second store**: instead of a new store, the fifth pass adds a second floor to this one.

## Fifth pass — hidden gems, outfits as camouflage, the second floor, closing-time lighting

The question this pass answers is "what is after the exit?". Before: a score, a grade, ENTER.
Now a night leaves something behind you can see and use the next night.

- **Hidden gems** (`GemPlan` in Core, `Gem` + `JobBuilder.GemSpots` in Game). Three per night, picked
  by the shift seed from twelve hiding spots (four of them upstairs), so everyone on the daily shift
  hunts the same ones. By day they are dull stones with no prompt. After **lights out** they glint
  (emissive pulse + a small cyan light), show as cyan marks on the Tab map, and can be taken with E
  (+250 points, `ScoreEvent.Gem`). At least one gem a night is **pose-gated**: it sits among the
  display mannequins, under a TV ledge, on the lounge rug, and can only be picked up while holding
  the pose that department expects. Taking it means standing exposed, in the dark, in a pose — that
  is the price. `JobBuilder.GemSpots` is the pool; add spots there, `GatedSpots` lists the gated ones.
- **Outfits** (`Wardrobe`, `WardrobeState` in Core). Gems are the only currency. Five outfits:
  Shopper's coat (aisles, 3), Boutique black (Clothing, 3), Tech tee (Electronics, 3), Loungewear
  (Home and Upstairs, 4), Staff apron (Checkout, Service, warehouse, staff room, Security, 6). An outfit
  that fits the department you stand in gives the guard the same extra doubt a fitting pose does
  (`DetectionCoordinator.PoseCamouflage` is now pose **or** outfit), with no pose needed, so clothes
  decide where you can afford to be caught moving. Bought and worn on the Night page of the briefing
  (Tab): **O** moves the cursor, **B** buys or wears. Saved in `PlayerPrefs` "ns-wardrobe"
  (tokens|worn|owned|gems ever). The body shows it: a torso garment in the outfit's colour and a light
  tint on the skin materials (`CharacterVisual.SetOutfit`), rig-agnostic. Entering a department your
  outfit fits shows a one-time toast.
- **The second floor** (`PrimitiveWorld.BuildMezzanine`). A mezzanine over Electronics and the staff
  room (x 1..15, z 5..15, floor at y 3.3), reached by an **escalator** that runs along the
  Clothing/Electronics boundary (x -1..1, z 6.5 → 13, landing to z 15). Rails on its open edges: you
  look down on the sales floor, and the guard on route 4 walks up there, so you can watch him from
  above — and he you. Upstairs is the Home & Electronics gallery (TV wall, gadget tables, a lounge
  with sofas where the Lounging pose belongs), with seven browse points and a work point so shoppers
  and staff use it. The store ceiling rose to 6.35 m with the outer walls extended; ground-floor lights
  hang on pendants; two fixtures light the gallery. `ZoneType.Mezzanine` is told apart from Electronics
  underneath by height: `StoreDirectory.ZoneAt` is now y-aware (ground zones y -1..3, upstairs 2.6..6.85).
  The map page has an **Upstairs inset**; marks above 2.5 m draw there.
- **Lockdown means something.** At lockdown the **front shutter** rolls down over the escape door
  (`FrontShutter`, 2.4 s, with a rattle), the door refuses to open and says so, the guide marker moves
  to the **fire exit** upstairs (`PrimitiveWorld.FireExit`, green sign that never goes out), and the
  last sixth of the night is a climb past the guard's torch. `NightSchedule`'s lockdown headline says it.
- **Closing-time lighting** (`StoreLighting.Sweep`). Going Open → Closing and Closing → Dark the
  fixtures no longer snap; banks go out from the back of the store toward the entrance, a row every
  1.3 s (0.55 s after lights out), each with a relay clunk you hear. After lights out four
  **skylights** in the raised ceiling throw cool moonlight cones, so the dark reads as silhouettes
  instead of black. No shadow-casting lights were added; the skylights and gem lights only exist in
  the dark phases.
- **Third-person interaction.** The look-ray used for E now starts at the body's eyes and follows the
  body's facing (tilted down) whenever the camera is not the eyes — over the shoulder or in the front
  pose view — so you can take a pose-gated gem while the camera is showing you the pose.

### Tuning knobs for this pass

| What | Where | Default |
| --- | --- | --- |
| Gems per night, pool, gated spots | `GemPlan.PerNight`, `JobBuilder.GemSpots` / `GatedSpots` | 3 of 12, 4 gated |
| Gem points | `NightScore.GemPoints` | 250 |
| Outfit costs and fits | `Wardrobe.Cost` / `Wardrobe.Fits` | 3 / 3 / 3 / 4 / 6 |
| Escalator run, upstairs height, ceiling | `PrimitiveWorld.RampStartZ / RampEndZ / UpstairsY / CeilingY` | 6.5 → 13, 3.3, 6.35 |
| Closing sweep pace | `StoreLighting.ClosingStep / DarkStep` | 1.3 s / 0.55 s |
| Shutter time | `FrontShutter.Seconds` | 2.4 s |

### Tests

- EditMode `WardrobeTests`: buying, wearing, text round-trip, fits, mezzanine access and pose, seeded gem picks.
- PlayMode `MezzanineTests`: zone by height, a complete NavMesh path from the entrance up the escalator
  to the fire exit, the ramp's height halfway up, gems on walkable floor and not inside fixtures, dark
  before lights out and lit after, the shutter down and the front door blocked after lockdown.

## Fixes after the first playtest (2026-10-08)

- **Shirts were on the floor, not the table.** `JobBuilder.SnapLooseItemsToFloor` judged "inside
  furniture" by distance to the NavMesh, which is eroded by the agent radius, so every item on a
  table top looked buried and was pushed off (the `[JOBS] … moved to open floor` warning). It now
  uses a physics overlap of the item's own box against store fixtures. `JobPoolTests.ShirtsStayOnTheClothingTables`
  guards it and checks the probe offers the shirt from the aisle.
- **Nothing worked in the back hall.** `InteractionValidation.CanReach` refused any player who was
  not `Free`, and a captive is not Free, so doors, gaps, figures and tags never showed an E prompt.
  Captives in the hall are now allowed; the bottom line shows what you are looking at ("E — through
  the gap on green") instead of only the room rule; and walking into the gap at the far end of the
  Still Light / Red Light rooms counts as pressing E. `BackroomWalkTests.ACaptiveCanUseTheFirstRoomsStation`
  guards it.
- Two CS0108 warnings: fields named `audio` / `name` hid Unity's own members; renamed.

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

- New EditMode: `ScoreTests`, `NightScheduleTests`, `PoseTests`, `JobTests`, `CareerTests`,
  `LeaderboardTests`; `DetectionTests` and `BackroomTests` describe the new behaviour.
- New PlayMode: `BackroomWalkTests` checks the hall floor is continuous and every hall
  label faces the player.
- Test runs use shift 0, the classic layout, so object positions in existing tests hold.
  `PrototypeRoot` detects the test runner and turns off pause-on-focus-loss.

## Known gaps

- Poses are procedural (arms and head aimed on the rig); there are no authored pose clips. If a
  bone name differs on a new character, `CharacterVisual.Configure` logs a warning once and that part keeps the animation pose.
- Seeded positions come from hand-picked pools of open floor; add to the pools in
  `PrototypeRoot.BuildObjectives` rather than randomising freely.
- The Tableau (two-player) job waits for online co-op; see coop-plan.md.
- The leaderboard server has no authentication: anyone who can reach it can post a score. Run it
  on a LAN or behind a reverse proxy with basic auth.
- `Assets/InitTestScene*.unity` are leftovers from interrupted test runs and can be deleted.

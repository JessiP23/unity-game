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

## Twelfth pass — the guard comes to look, the whole mall every night, a readable end card

From the second playtest: "I moved in front of the guard, stopped, and nothing happened."

### The close look (`GuardController.LookInto / LookOver`, `PrototypeRoot.TickInspection`)

- **He saw you move and you froze: he walks over.** The Red state now sends the guard to the spot
  (an investigation, like a noise or a report). The tip says so: "He saw that. He's coming to look — hold a pose
  that fits this department, or he'll grab you."
- **Up close, standing still is not enough.** Within 2.1 m, looking at a suspicious body, he stops and looks it
  over. A pose that fits the department (Display in Clothing and the toy store, Browsing in Electronics and the
  aisles, Lounging in Home, the gallery and the atrium, Staff at the checkout), display clothes, the crowd, or a
  seat fools him in 1.8 s: "Huh. Just a mannequin", a close call, and for 8 s he takes you for furniture. A body
  that just stands there is grabbed after 2.5 s. The cover chip turns red and counts down either way. Moving
  while he looks still works as before: suspicion, the chase, caught.
- A hiding spot he walks right up to is a hiding spot he has found: it does not fool the close look.

### Every night crosses the whole mall (`JobCatalog.Draw`, `JobArea`)

- A night now draws **one job on the supermarket floor, one out in the mall (atrium, toy store) and one up in
  the gallery**, never two of a family. Shift 0 keeps the classic trio for the tests.
- Two new jobs so the mall and gallery pools have three each:
  - **Water two of the atrium planters** (hold, 3 s each, a watering can on the rim) — in the sweep of the two
    atrium cameras.
  - **Carry the throw pillows up to the gallery sofa** (carry): from the ottoman in Home, up the escalator or the
    stairs, G on the rug by the gallery sofa.

### End card

- The detail text had three lines in a box for two and a half, so it ran into the score list; and a long night can
  earn more kinds of points than the card had rows for, so they spilled over the footer. The box is taller, the
  rows squeeze and their text shrinks to fit.
- The card says what the night unlocked ("★ NEW NIGHT UNLOCKED: Blackout — pick it on the menu") or how many
  nights until the next special night, and the footer says ENTER / N go back to the menu.

### Tests

- PlayMode `GuardInspectionTests`: standing still in front of the guard in Red gets you grabbed after the look;
  the Display pose in Clothing fools him (close call, suspicion 0, back to Green, no second look); seen moving
  from 7 m, he walks over.
- `JobPoolTests` also runs shift 6 (the planters). New capture `end_card`.

## Eleventh pass — sit and ride, readable speech, a real live stream, and a gallery worth climbing to

From the first full playtest of the tenth pass (86/88 PlayMode tests; the TV test's two sets overlapped).

### Sit down: carousel horses, sofas, armchairs, benches (`SeatSpot`, `Seating`)

- **E on a carousel horse climbs on.** You sit astride the saddle, hands on the pole, carried round with the
  horse's bob; riding never counts as moving, and a mannequin on a horse passes for a display pose. E or any move
  key climbs down onto the deck (inside the ring of horses, else outside it). Caught while riding, you are put
  down beside the horse, never inside it.
- **The ride job counts a whole turn** in degrees the ride turns with you on it (sitting on a horse, or standing
  on the deck in the Display pose). A stop pauses the count; walking about on the ride costs half. The ride now
  turns at 18°/s, so a turn (20 s) fits in one 28 s run. Before, a turn took 30 s and the run lasted 28 s, so
  standing on it "for one full turn" could never finish.
- **Sofas, armchairs, ottomans and the atrium benches are seats.** Seat heights come from each model's own
  cushion (its vertices, read before static batching merges them). Seated, your eyes are low, watchers aim at a
  lower chest, and a seated mannequin on display furniture passes for a display pose.

### Speech bubbles you can always read

No camera was tagged `MainCamera`, so `Camera.main` was null: bubbles never turned to face you (their text only
showed from behind the shopper) and the crowd's level of detail never ran. The player's view is now tagged,
`ViewCameras.Current` is used everywhere, and bubbles are printed on both faces.

### The live stream (`LiveStream`)

- A red "● LIVE 2.4k" tag over the influencer's head, likes floating up from the phone, and a faint red fan on
  the floor that is exactly their shot (10 m, 70°: their eyes use the same numbers).
- Two fans follow the influencer, phones up.
- Move in the shot and you go viral, as before. **Hold a pose in the shot for 2.2 s for a shout-out** (+350, once
  per stream, a photo). The cover chip turns red: "● IN THE LIVE SHOT", "● ON THE STREAM · shout-out in 2s",
  "● LIVE 9 m away".

### The gallery upstairs

- Dressing: a balcony sign the sales floor can read ("THE GALLERY · HOME & ELECTRONICS"), a games demo by the
  rail with an ottoman where shoppers stop to watch, a homeware shelf unit, plants, a wall band and two warm lights.
- **New job "Switch three gallery TVs to the mannequin channel"** (family `tv`): E on three marked TVs on the
  gallery wall, 2 s still each; each one switched shows the store's camera feed.
- **New job "Sit for the catalogue photo on the gallery sofa"** (family `sit`): sit on the sofa and keep still
  15 s while the photographer's flash goes off.

### Tests

- PlayMode `MallSeatsTests`: a horse carries its rider without counting as movement and the ride counts degrees;
  move climbs down onto the deck; every seat's stand-up point is open floor, there are four benches and seats
  upstairs, sitting starts the photo and E stands you up; a TV channel switches and counts; the player's view is the
  main camera; the live stream's tag, fans, sight and a single shout-out.
- `ScreensSitOnTheirTelevisions` now spawns each TV on its own spot (the second ray was hitting the first TV).
- New captures: `gallery`, `gallery_demo`, `carousel_rider`, `sofa_sit`, `live_stream`, `live_tag`.

## Tenth pass — crouch and hide, a real main menu, settings, and four special nights

### Crouch and hiding spots

- **Crouch** with Ctrl (either side) or Option/Alt. You drop to 1.15 m, walk at 45% pace, can't sprint or
  jump, and watchers and cameras aim at your chest height, so counters, planters and sofa backs block them.
  You only stand back up where there is headroom. A held pose keeps the crouch.
- **Hiding spots** (`HideSpot`, `HidingPlaces`). Found once the world settles: open floor tucked against
  cover between 0.6 and 1.4 m tall, with nothing tall right above it (a shelf wall is not cover), and room
  to stand back up. At most 16 in the mall, 3 per department, 5 m apart. Within 5 m a soft ring shows the
  spot; crouch and keep still in it for 0.35 s and the ring turns green: shoppers, staff and cameras
  can't see you, and the guard only finds you within 1.6 m. The badge says "HIDDEN".

### Main menu and settings

- The shift card is now the **main menu**: NIGHT MALL, a row of five night cards (locked ones say when
  they open, today's featured night pays ×1.25), the difficulty row, SETTINGS (S), CLOCK IN (Enter) and
  QUIT. N / ↑ / ↓ step the night; your last night and difficulty are remembered.
- **Settings** (`GameSettings`, `SettingsPanel`; also the 7th Tab page): volume, mouse sensitivity, invert
  mouse, graphics (Low / Medium / High: shadows, anti-aliasing, post-processing per camera, and a crowd
  ceiling of 24 / 32 / 40) and full screen. Saved as you change them; test runs ignore saved settings.

### Special nights (`NightKinds`, `ScenarioDirector`)

| Night | Opens after | What changes | Score |
| --- | --- | --- | --- |
| Blackout | 1 night | Lights out all night, the guard's torch on; shoppers see 3 m (70°), 8 m with a phone torch (45% carry one, at most 7 lit); fewer people. | ×1.15 |
| Kids' birthday party | 2 nights | Party table, cake and balloons in the toy store, 8 party kids (they see everything). Every minute it's CAKE TIME: the kids rush the table for 20 s. Grab the present (hold E, 2.5 s, +600). | ×1.1 |
| Black Friday | 3 nights | 70% more shoppers, eight walk in at once, four 30 s doorbusters through the night. | ×1.2 |
| Jewellery heist | 4 nights | A fashion runway east or west of the fountain and a diamond case. Every minute the show draws the crowd for 20 s; lift the diamond then (hold E, 3.5 s; it sets off an alarm). It pays +1000 only if you escape with it; caught, it goes back in the case. | ×1 |

The featured night rotates with the daily shift. Night metrics gain a `night` column; an older
`crowd-metrics.csv` with the old header is renamed aside and a fresh one started.

### Tests

- EditMode `NightKindTests`: unlocks and featured night, crowd/score/sales/lighting rules, show timing,
  doorbusters never overlapping.
- PlayMode `MallNightsTests`: crouching is low, slow and lower to watchers; hiding spots sit on open floor
  and hide you only crouched and still; settings reach the game; each special night sets itself up
  (Black Friday's doorbusters, the Blackout's dark and short sight, the heist's diamond paying only on
  escape and returning when caught, the party filling the toy store and cake time pulling the kids).

## Ninth pass — a crowd you can read, hide in and plan around

Built from the plan agreed after the eighth pass: more people must make every job harder **and** give the
player new ways to win. Three updates, end to end.

### Update 1 — hide in the crowd, pressure on the jobs, see it coming

- **Hide in the crowd** (`CoverRules`, `CrowdCover`). Walk with two or more shoppers going your way (within
  2 m, heading within 55°) and watchers' suspicion is cut by 55% (two) or 75% (three or more); the guard
  doubts what he saw, like a fitting display stand. Running, walking against the flow or alone: no cover.
  Alongside a group you **fall into step** (`PlayerMotor.PaceCap`: their pace + 0.3 m/s), so you stay in it
  for a good while and drift out ahead; sprint to break away. A soft cyan ring at your feet and an
  "IN THE CROWD · 3 shoppers hide you" badge show it working; near people but not with them, the badge
  says why ("walk their way, at their pace", "running gives you away").
- **People gather at your jobs** (`JobPressure`, `CrowdDirector.Life`). Two to four shoppers (more with
  level and Rush hour, fewer on Relaxed) hang around each unfinished job on linger spots 1.6–3 m out,
  facing it, never on it, checked against the NavMesh, furniture, props and drop rugs. The crowd stays put;
  it only follows a moved target after 20 s.
- **A guaranteed quiet moment.** Every job breathes on its own seeded 60–90 s rhythm: busy, a 6 s
  "quiet in 6s — get ready" warning, then 15 s when everyone drifts off. The map marks the job green.
- **See it before it happens.** Under the NEXT chip: "CROWD HERE: BUSY (5) · quiet in 0:20", gold when it
  is about to go quiet, green "QUIET NOW · 12s — go!". On the map every department is tinted by its crowd
  (Quiet / Steady / Busy / Packed) with an arrow when it is filling up or emptying.

### Update 2 — who is in the crowd

Each kind reads at a glance (`ShopperKind`, `ShopperLook`, new `Stance` poses):

- **Families** (40% of groups arriving together): a grown-up and kids at 70% size who stay at their side.
  Kids see everything (200° view) but take two seconds to tug the grown-up, who turns to look: freeze and
  it fizzles. The crowd half-hides you from kids.
- **Phone zombies** (18% of arrivals): head bowed over a glowing phone, 40° view and 3.5 m. Safe to pass,
  and good cover.
- **The influencer** (one at a time, from 45 s, never on Relaxed): phone held out, ring light, REC dot and
  a red camera light sweeping the floor — the mall cameras' "don't move in the red light" rule again.
  The camera sees through any crowd and reports at once; you **go viral** (a toast with the clip's caption
  and a photo). Marked "REC" on the map.
- **Cleaners and restockers** (a cleaner always, a restocker from level 3 or on Rush hour): staff with a mop
  or a box who walk to tonight's job spots and work there ~20 s. Cleaners leave a **wet floor** with a
  yellow sign: walk across and nothing happens, run across and it squeaks loud enough for the guard.

### Update 3 — clock in, and make sure it is fair

- **Shift card** (`ShiftCard`, `DifficultyRules`): before the night, pick Relaxed (×0.7 crowd, no
  influencer, one fewer at each job, score ×0.8), Normal, or Rush hour (×1.3 crowd, three 40 s flash sales,
  one more at each job, score ×1.3; unlocks after three nights). Keys 1/2/3 or click, Enter to clock in;
  last night's choice is preselected. The automatic "busier every night" level stacks on top. The end card
  shows the shift, the crowd level, how long you hid in the crowd and how often you went viral.
- **Night metrics** (`CrowdMetrics`): every real night appends a row to `crowd-metrics.csv` in the game's
  saved-data folder (Unity's persistentDataPath): difficulty, level, jobs done, reports and reports per
  minute, influencer clips, peak crowd, mean budget and frame time, seconds hidden in the crowd, and seconds
  spent near each unfinished job. Tuning targets: Normal at level 0 — about 7 nights in 10 end with every
  job done; Rush hour at level 8 — still beatable; reports per minute under 1 on Normal.

Scripted tests run with crowd pressure, new kinds and sales off unless a test turns them on; hiding in the
crowd is always on (it only ever helps). Known gap: co-op clients see new kinds as ordinary shoppers (the
snapshot carries no kind yet) and only the host picks the shift.

Tests: EditMode `CrowdLifeTests` (cover geometry and strength, concealment in awareness, lingerer counts,
quiet windows within every 90 s on 40 shifts, heat, difficulty factors, rush sales never overlapping, score
factor, metrics row, lingering shoppers). PlayMode `MallCrowdLifeTests` (walking with three shoppers covers
you and slows you into step, sprinting breaks it; shoppers gather at a job and leave in its quiet window;
heat map counts; family, phone zombie, influencer and cleaner are dressed and behave; wet floor squeaks only
when running; shifts change crowd and sales). `Tools/unity.py shots` adds `crowd_cast.png`,
`crowd_influencer.png` and `shift_card.png`.

Next, as asked: crouching, and hiding spots to crouch into.

## Eighth pass — a busier mall that makes every job harder

Playtest feedback: more people, so each mission is harder to cover. Done so the crowd is something to
read and plan around, not just noise, and so it stays smooth on a laptop.

- **More shoppers, growing with you** (`CrowdPlan`, `NpcPopulation.asset`). The mall now breathes between
  10 and 20 shoppers on a first night (was 6–12), with 12 already inside at opening and a third member of
  staff. Each night you play adds 10% (`CrowdPlan.Level`, from `ns-nights`), up to +80% at level 8
  ("Steady", "Busy", "Bustling", "Packed", "Heaving" — shown on the start toast and the Night page). Closing
  time still halves the crowd and lights out still empties it, so the night keeps its shape.
- **People arrive like people** — in ones, couples and little groups through the mall doors, faster when
  the mall is far below where it should be (`CrowdPlan.Arrivals`, `ArrivalGap`).
- **Flash sales** (`CrowdDirector`, `CrowdPull`). Twice a night (once while the store is open, once at
  closing time; same times for everyone on a shift) the PA chimes: "FLASH SALE in Toys in 10 seconds".
  For 50 s that department is mobbed — 30% more people come in, nearly half of those already shopping drop
  what they are doing, and 70% of the rest make a detour — and everywhere else goes quiet. Sales are called
  in the departments **tonight's jobs are in**, so the crowd lands on your objective: get it done before,
  wait it out, or use the empty mall elsewhere. A spinning orange sign hangs over the sale, the map marks
  it, and the top of the screen counts it down. Shoppers stand on extra sale spots placed round the
  department's shelves (checked against the NavMesh, furniture, job props and drop rugs once the store has
  settled), and come away carrying a bargain.
- **Smooth on any machine.** A live budget (`CrowdBudget`) watches the frame time: below ~48 fps it stops
  people coming in (never cutting far below who is already there, and pausing to see if that helped), and
  if the mall is well over budget one distant shopper goes home; above ~56 fps it earns room back, up to 40
  shoppers. One-off hitches are ignored. Crowd level of detail: only shoppers within ~13 m cast shadows,
  further ones skin with two bones and cheaper avoidance, and nobody off screen is animated or skinned.
- Tests: EditMode `CrowdTests` (levels, budget, sale timing, group arrivals, shoppers' sale detours);
  PlayMode `MallCrowdTests` (opening crowd and budget cap, sale spots on open floor on four shifts, a sale is
  announced, floods its department and ends, two sales use different departments). Scripted scenario tests
  run without sales; the walkthrough test clears the crowd first, and the shutter test looks past shoppers
  walking out. The Core test build now compiles only against the Unity modules `Packages/manifest.json`
  enables, so a disabled built-in module fails here exactly as in the editor.

## Seventh pass — real fountain and carousel, Clothing that answers E, flush screens, funny moments

From the playtest: the fountain and the carousel were "basic squares", E in Clothing did nothing, and
TV/monitor screens floated off their sets. Plus: make it unique, funny and worth coming back to.

- **Clothing answers E** (`FittingRack`). Every rack and table lends you clothes: E puts on a borrowed
  piece (disguise in Clothing and the toy store while you stand still), E again puts it back, capture
  loses it. The model stand accepts borrowed clothes too. The old hint that always said "E on a blue
  shirt" now only appears on nights that have the shirt job. Racks and the fountain rim are
  `ILowPriorityInteractable`: when you aim near something small on them (the job shirt, a gem), the
  small thing wins.
- **Everything usable says so.** A glowing **E** badge floats over whatever the probe would use
  (`InteractBadge`, fed by `InteractionProbe.Target`).
- **The fountain** (`FountainDressing`, `FountainWish`). Marble basin and coping, a tiered
  centrepiece spun from profiles (`MeshKit.Lathe`), moving water (generated ripple normals scrolled on
  `_BaseMap_ST`, which URP Lit uses for every map), a jet and two curtains of spray (~200 particles),
  coins on the bottom, three rubber ducks drifting round, the sound of water. **E on the rim tosses a
  coin, once a night**: the splash is a noise the guard comes to check, and the fountain grants the
  shift's wish (`FountainWish`: +150 points, a gem, or 45 s in which no shopper will report you).
  Wading in and moving splashes too.
- **The carousel** (`CarouselRide`). Poly Haven horse statues on brass poles (rounded primitive horses
  without the art), a striped tent canopy with valance and brass trim, a mirrored centre column, a
  wooden deck, 24 chasing bulbs.
- **Screens sit on their sets** (`DressingKit.FitScreen`). The glow is fitted from the model's own
  triangles: the depth with the most forward-facing area is the screen face, the panel covers its middle
  and sits 3 mm in front. Used for the TV wall and the security monitors. A test fires a ray at the
  middle of a fitted panel and requires the glass within 1.5 cm behind it.
- **Funny on purpose.** Caught mannequins **keel over stiff as a plank** with a thud and the camera
  swings round to show it (`CharacterVisual.Topple`, `PrototypeRoot.Plank`, presentation only: tests
  still enter the hall at once). The **photo booth** (`PhotoBooth`) saves the night's moments to
  Pictures/Night Mall — a shopper's selfie with you, the plank, the escape — and slides a polaroid in.
- **A reason to come back tomorrow.** One **nightly dare** per shift (`NightDare`: Photobomb,
  Merry-go-round, Make a wish, Nobody saw a thing, Treasure hunter, Slow and steady, Fire drill) worth
  300 points and a gem, shown on the start toast, the Night page and the end card. Completing the daily
  shift's dare on consecutive days builds a **dare streak**; missing a day just starts again at one —
  nothing is taken away (no fear-of-missing-out timers, no loot boxes).

New art (run `python3 Tools/fetch_art.py` once): `horse_statue_01`, `potted_plant_01`,
`potted_plant_02`, `rubber_duck_toy`, surface `marble_01`. Everything falls back when missing.

Tests: EditMode `DareTests`; PlayMode `MallPolishTests` (racks lend clothes and the probe offers them,
one coin a night and the splash is heard, fitted screens sit on the glass, the dare and the photo booth
exist and test runs never write photos). `Tools/unity.py shots` now also photographs the atrium,
fountain, toy store, carousel, robot shelf, escalator, upstairs, TV wall and security monitors.

## Sixth pass — the mall: a real escalator and its rival stairs, the atrium, the toy store, the carousel

"Bigger" here means more decisions per minute, not more square metres. Everything new is a place
with its own rule of cover, and every new system reuses noise, reports and poses the game already had.

- **The climb has two answers** (`PrimitiveWorld.BuildMezzanine`, `MovingSurface`). The escalator is
  now a belt (`EscalatorBelt` on the ramp): treads travel up it (`EscalatorTreads`), glass
  balustrades, a handrail and an under-light strip dress it, and it **carries you** — and because a
  mannequin riding up is a mannequin that moves, the carry counts as movement for every watcher. Beside
  it, through a stairwell cut out of the slab, a **staircase** (x 1.1..2.7, twenty steps) is slower but
  you can stop on any step. The sign at the foot says exactly that. `PlayerMotor` samples the floor
  under the feet each step and asks any `MovingSurface` how far to carry the body; the surface decides
  whether that counts (`CountsAsMovement`).
- **The mall** (`PrimitiveWorld.BuildMall`, `MallDressing`). The supermarket's front doors now stand
  open onto an **atrium** (x -12..12, z -33..-15): an 8 m glass roof with moonlight through it, a
  fountain with a rim to crouch behind, planters, benches (the Lounging pose belongs here), and two
  **security cameras** that sweep a red pool of light across the floor. Shoppers arrive and leave
  through the mall doors at the far end, so the atrium is always crossed by people. The **toy store**
  opens off its west side (x -30..-12, z -33..-18): stocked shelves that **rattle** if you run past
  them (`NoisyShelf`, a noise the guard hears), a tall shelf with a robot on the top and a step stool
  in front of it, and the **carousel**. The escape door is now beside the mall doors: the jobs are in
  the supermarket, the way out is across the atrium.
- **The carousel** (`CarouselRide`). A turning platform with six bobbing horses under a lit canopy,
  a synthesised waltz that slows when it does, bulbs that chase while it runs. It runs 28 s, rests
  10 s, repeats. Riders are carried, and because a carousel is supposed to turn, **being carried does
  not count as moving**: a mannequin posed on a horse is the most natural thing in the store. When it
  stops you are a mannequin standing on a ride, and the usual rules apply. The platform is under the
  controller's step height so you walk straight on. One rotating transform.
- **Security cameras** (`SecurityCamera`). The rule is one line and the light is the whole interface:
  *don't move in the red light*. Stand still inside it and nothing happens; move for more than 0.45 s
  and the camera reports you like a shopper would (`authority.TryReport`, same consequences), its LED
  turns amber and the pool flashes. A one-time toast explains it the first time the light touches you.
  Twelve-second cooldown per body so one slip is one report. Reports read "A security camera reported
  movement near the atrium".
- **Two new jobs** (`JobCatalog`): *Ride the carousel for one full turn* (a `HoldSpot` on the spinner;
  it starts by itself when you hold the Display pose while the ride turns, pauses when it stops —
  `HoldSpot.Gate` — and keeps half its progress if you move), and *Take the robot from the top shelf*
  (climb the stool, E; the robot is kinematic so it stays put on the shelf edge). Shoppers now also
  shop the toy store and the gallery (`ShoppingCatalog.Default`).
- **Lockdown, revised.** The shutter drops over the **supermarket's own doors** (with a NavMesh obstacle
  so shoppers turn back). Inside, the fire exit upstairs is the way out; already out in the mall, the
  front doors still are. The guide marker picks by which side of the doors you are on.
- **Four more gem spots** in the mall: a planter edge, the fountain rim, the carousel pole (pose-gated,
  Display, the platform turning under you), the floor between the west toy shelves.
- **Map**: covers the whole mall now (x -30..15, z -33..15); zone labels scale down.

### Tuning knobs

| What | Where | Default |
| --- | --- | --- |
| Escalator belt speed | `EscalatorBelt.Speed` | 0.55 m/s |
| Carousel run / rest / speed | `CarouselRide.RunSeconds / RestSeconds / TopSpeed` | 28 s / 10 s / 12°/s |
| Camera cone, range, sweep, grace, cooldown | `SecurityCamera` constants | 44°, 15 m, ±38° over 7 s, 0.45 s, 12 s |
| Toy shelf rattle loudness | `NoisyShelf.Loudness` | 1.1 |
| Ride job length | `JobBuilder.BuildCarouselJob` | run − 3 s |

### Tests

- PlayMode `MallTests`: complete NavMesh routes from the mall doors to the aisles, the carousel, the
  robot stool, the escape door and upstairs; the new zones; the stairs climb without gaps and end at
  the mezzanine; the carousel carries a rider without its `ActualSpeed` reaching the movement
  threshold; mall gem spots on open floor. `MezzanineTests` now checks the shutter closes the
  supermarket doors and leaves the mall's escape door open.

### Fixes from the first full PlayMode run (2026-10-11: 52 passed, 14 failed)

- Upstairs was unreachable: the gallery TV wall plus the top ends of the escalator side panels and
  the stair rail pinched the landing shut on the NavMesh. The TV wall now starts at x 3.8, the
  panels and rail stop 0.4 m short of the top, and the fire exit moved to x 12.
- Shoppers left without shopping: the toy store and gallery were on every shopping list, including
  test stores that have neither, and the height-aware zone check rejected bodies a hair below the
  floor. `ShoppingCatalog.Default` is the classic four again; real nights use `ShoppingCatalog.Mall`
  (`CustomerPopulationManager.Catalog`); `StoreDirectory.ZoneAt` allows 0.5 m below a zone's floor.
- The robot marker snapped to the toy-store roof. It now stands in front of the stool, and roofs,
  benches, planters, the fountain and the robot shelf are marked not-walkable (`PrimitiveWorld.NotFloor`).
- Tests updated for the new layout: the escape door is found by name (there are two exits now), the
  back-hall test no longer assumes room 1 has a button, the hold-spot test uses open aisle floor, and
  the carousel gem is exempt from the walkable-floor check (you reach it by riding).

### Known gaps

- The carousel angle is local time; for online co-op it must be driven from the authority's clock.
- The food court (east of the atrium) is the next space; the wall at x 12 is where it goes.

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

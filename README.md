# Night Supermarket

Unity primitive cooperative stealth prototype. See [development](Documentation/development.md)
for the installed local toolchain and commands, [architecture](Documentation/architecture.md)
for system boundaries and [phase status](Documentation/phase-status.md) for progress.

The store is dressed with free realistic art (Poly Haven props and Microsoft
Rocketbox characters); run `python3 Tools/fetch_art.py` once on a fresh clone.
The local night loop is playable: move, get seen, freeze, get
captured, rescue a teammate, finish the crate objectives and escape before dawn.
Cross-device gameplay synchronization and PlayFab remain unfinished. See
[phase status](Documentation/phase-status.md).

Open the prototype scene and press Play. Shoppers and staff fill the store; freeze when anyone
looks your way. Hold RIGHT MOUSE to freeze in a pose and scroll to pick one; the pose that fits
the department buys extra doubt. Tab opens the briefing (and pauses). The store closes, goes
dark and locks down as the six minutes run out. Caught? You wake in the back hall: clear three
rooms to come back. Points, an unseen-streak multiplier and a best score per daily shift are on
the end card; ENTER replays the shift, N rolls a new one. Each shift draws three of nine jobs
(window modelling, swapping places with a display mannequin, price tags, the checkout camera,
a lost toy, a fragile vase, crates, a shirt). See [redesign](Documentation/redesign.md).

C cycles the camera (eyes, shoulder, front); posing swings to the front view so you can see it.
Shoppers react to a mannequin that holds still. Each shift has a named guard with a tell. Points
from won nights unlock gadgets: X wind-up toy at 3,000, Z price gun at 6,000.

After lights out three hidden gems glint somewhere in the store (cyan on the Tab map); some can only
be taken while holding the pose the department expects. Gems buy outfits on the Night page (O, B),
and an outfit that fits the department you stand in buys the same doubt a pose does. The store has
a second floor: the escalator by Clothing climbs to the Home & Electronics gallery, and when the front
shutter drops at lockdown the fire exit up there is the only way out.

The supermarket sits in a small night mall: the front doors open onto an atrium (fountain, benches,
two security cameras — don't move in the red light) and the toy store beyond it, where the carousel
turns: climb on a horse (E) and nobody counts you as moving. Sofas, armchairs and benches are seats too (E). The escalator carries you up (and
that counts as moving); the stairs beside it let you stop. The escape door is beside the mall doors.

The game opens on the main menu: pick a night, a difficulty (Relaxed, Normal or Rush hour, which unlocks
after three nights; busier pays more) and clock in. Special nights open as you play: Blackout, a kids'
birthday party, Black Friday and a jewellery heist (steal the diamond during the fashion show and escape
with it); one is featured each day for ×1.25. SETTINGS has volume, mouse, graphics and full screen.
Crouch with Ctrl or Option: you are lower and slower, and crouched still in a hiding spot (a soft ring by
low cover) nobody can see you unless the guard walks right up.
Walk WITH a group of shoppers and they hide you; running gives you away. People gather at your jobs, but
every job goes quiet for 15 s about once a minute — the job card under NEXT counts it down. Watch for
kids (they point), the influencer's red shot on the floor (hold a pose in it for a shout-out), and cleaners' wet floors (don't run on them).
The mall gets busier the more nights you play, and twice a night a FLASH SALE floods one department —
usually one your jobs are in. Watch the countdown under the clock and plan around the crowd.
Every night has a dare (Tab, Night page) worth points and a gem; do the daily shift's dare on
consecutive days to build a streak. Toss a coin in the fountain for the night's wish (the splash
brings the guard). Any Clothing rack or table lends you clothes (E). Get caught and you go down like a
plank; selfies, planks and escapes are saved as photos to Pictures/Night Mall.

The end card shows the shift's leaderboard; TAB there edits your name. Scores stay on this
computer unless you run `python3 Tools/leaderboard_server.py` somewhere your friends can reach
and paste its address into *Night Supermarket ▸ Leaderboard ▸ Settings…*. Online co-op is
planned, not built: see [coop-plan](Documentation/coop-plan.md).

Testing keys are off by default. Press ` in the Editor or a development build to turn them on;
the Controls page then lists them. [ / ] switches mannequins, then the guard. See [NPCs](Documentation/npcs.md)
and [night choices](Documentation/night-choices.md) for the E interactions.

```sh
python3 Tools/unity.py open
```

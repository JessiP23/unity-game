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

Testing keys are off by default. Press ` in the Editor or a development build to turn them on;
the Controls page then lists them. [ / ] switches mannequins, then the guard. See [NPCs](Documentation/npcs.md)
and [night choices](Documentation/night-choices.md) for the E interactions.

```sh
python3 Tools/unity.py open
```

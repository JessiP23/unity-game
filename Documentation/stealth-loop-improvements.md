# A replayable mannequin stealth loop

The identity is pretending to be an ordinary shop display while people and security
pass by. Keep the interesting decision: move now, freeze, or create a distraction.
The following are design hypotheses for playtesting, not promised retention gains.

## First: make failure understandable
A player should be able to explain every warning and capture. Geometry, live sight,
remembered suspicion and reporting must be distinct. Security gives a fixed grace
period in real simulation seconds. Stopping in red prevents further suspicion;
cover removes live sight but does not erase witnesses' memories. Use the downward objective marker and store map for navigation. Never hide timing
multipliers.

## Next small playable experiment
Build one 5–8 minute night around three escalating choices:
1. Cross a quiet aisle and freeze before the guard focuses (teach the rule).
2. Carry an awkward object through a crowded department (risk versus a longer route).
3. Finish an optional display swap or escape safely with the team's completed work.

Make a near miss satisfying with a short, unmistakable return-to-safety cue. Keep
warnings legible and avoid constant alarms. Let players restart quickly after a
loss and see what caused their capture: who, where, and whether they moved in red.

## Distinctive follow-on mechanics
- Display camouflage: stand in a marked display pose wearing an appropriate item.
  This buys a clearly communicated extra moment of doubt, not random immunity.
- Cooperative misdirection: a teammate creates a noise so a carrier can cross;
  captured players use surveillance to time rescues. The warehouse remains play.
- Optional high-risk swaps: rearrange displays for a better end-of-night grade.
  Players choose whether to bank their escape or risk another objective.
- Seeded nights: vary patrol timing, customer routes and objective combinations,
  while preserving the same sight rules and guaranteed reachable objectives.

Do not add combat, daily chores or random punishment to manufacture replayability.
Meaningful mastery and funny shared near misses fit this game's premise better.

## How to judge the experiment
Ask players to explain their last capture and locate their next objective without
help. Record failed interactions, abandoned objectives, near-miss recoveries,
voluntary retries, and frame-time spikes. Watch whether stopping actually feels
safe and whether players choose different routes on a second run. Tune grace and
crowd density only after detection regressions pass. Measure frame time in a
rendered player build; headless test speed is not the player's FPS.

## First implementation — 2026-10-02

The six-minute night, display posing, delayed distraction bell, rescue map cues,
and two optional promotional swaps are implemented. See [night-choices.md](night-choices.md)
for controls and rules. The screen compass was removed at the user's request;
only the downward world objective marker remains. Seeded objective variants and
additional display animations remain future work.

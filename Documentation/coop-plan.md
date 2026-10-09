# Online co-op — plan, not code

Everything in the redesign runs locally with up to three mannequins on one keyboard
(`[` / `]` under testing keys). Online play over Photon Fusion 2 is the one piece of the
full redesign that is *not* built, on purpose: it cannot be verified without two machines and
a Photon App Id, and the rest of the game is where the fun is being tuned right now. This
page is what to do when that time comes, so it is a two-week job and not a rewrite.
Transport details are in [networking](networking.md); this page is about the new systems.

## Principle

Nothing in `NightSupermarket.Core` changes. The host runs `LocalMatchAuthority`, the guard,
the shoppers and every rule; clients send intent and show state. All the pass 1–4 systems
were written on that side of the line already:

| System | Where it runs | What to replicate |
| --- | --- | --- |
| `NightScore` (points, streak) | host, one per match | `Total`, `Multiplier`, `StreakSeconds`, last `ScoreLine` for toasts |
| `NightSchedule` phases | host | the phase enum; clients already derive labels from it |
| `PoseStrain` / `Stance` | **client-owned** per mannequin | selected `Stance` + `Holding` as networked input; host computes `PoseCamouflage` |
| `BackroomCourse` trials | host | the trial list for the captive; inputs (door picked, position) are commands |
| `JobCatalog` / `JobBuilder` | host builds, clients receive | job rule ids + seeded positions (the `shift` seed is enough: both sides build the same layout) |
| `GuardRoster` | host | the profile index (one byte) |
| `ShopperReactions` | host decides | a "react" event (npc id, mannequin id, line index) so bubbles appear on all screens |
| `GadgetKit` | command → host | `UseToy` / `UseGun(aim)` commands; host spawns the toy and publishes noise |
| `Leaderboard` | not networked | each client posts its own score to the shared server (already works) |

## Order of work

1. **Seeded world.** Both sides call `JobBuilder` with the same `shift`; assert the prop
   positions match across two Editor instances (ParrelSync or a build + Editor). This is the
   cheapest proof that the layout is deterministic. `PrototypeRoot.NextShift` already carries the
   seed across scene reloads.
2. **Mannequin input.** Movement, `PoseHeld`, selected stance, interact, jump as Fusion
   `NetworkInput`. `PlayerInputReader` is the only producer; `PlayerMotor` the only consumer.
3. **Authority snapshot.** Extend `NpcSnapshot` with the guard's `Pace` and alert state, and
   add a `MatchSnapshot` (phase, clock, score fields above, captive list with their hall index).
4. **Commands.** Interact-at-objective, use-gadget, backroom door pick, rescue. The authority
   repeats the checks it does today; nothing in `PrototypeRoot`'s key maps calls rule methods
   directly, so the swap is in `KeyCommandMap` bindings only.
5. **Per-screen presentation.** `PrototypeHud` is already fed from a `HudModel` built on the
   local client from replicated state. Shopper speech bubbles and the capture banner are the
   two events that need an RPC.
6. **Back hall for a remote captive.** `BackroomPocket` places halls at `Origin + index * 26`;
   the captive's client needs only its own hall, so spawn halls on all clients (they are cheap
   primitives) and let the host validate door picks.

## What co-op adds to the design

- Only when **all** mannequins are free does the streak build (`PrototypeRoot` already
  gates it). A captured friend is everyone's problem, which is the point.
- Rescue is a job for the free player: reach the back-hall gate, press E. The captive can
  shorten it by clearing trials. Both get the `Rescue` score line.
- Two players can hold the **Tableau** pose together (two mannequins, matching stances)
  for a one-off bonus — the first job to add once two clients are moving.

## Not planned

- Matchmaking with strangers. Rooms by code, friends only.
- Voice. Text bubbles already exist; a four-emote wheel on a key is enough.
- PlayFab or any account system. The shared leaderboard is a text file on a machine you own.

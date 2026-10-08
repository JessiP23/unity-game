# Night choices — playable prototype, 2026-10-02

The default night lasts six minutes. Required crate/shirt jobs still unlock the
exit. Two optional purple promotional stands never block escape.

- **Blue model stand, clothing department:** collect a shirt, empty your hands,
  stop near the stand and press E. Keep your position and facing to model the
  shirt. The HUD confirms the pose. Posing before a guard sighting adds two
  seconds to that sighting's orange grace, shown as DISGUISE. That earned window
  remains if you step away, and cannot be renewed while still seen. It does not erase suspicion or NPC
  memories. Moving, turning over 20 degrees, jumping or carrying breaks the pose.
- **Amber delayed bell, west aisle:** E arms it. It rings after three simulation
  seconds, giving a player time to leave or switch to their teammate. The guard
  investigates through the existing hearing system, within twice normal hearing
  range, unless already chasing. A shared eighteen-second cooldown starts when
  armed. The countdown pauses with the game.
- **Rescue coordination:** captured players use V / Cameras. The map marks the
  rescue console and bell and shows how far the guard is from the console.
  The existing rescue console releases a teammate. Use the bell to draw the
  guard away, then approach the console and press E.
- **Back hall:** getting caught shows a CAUGHT card with the cause, then drops
  you into a hall off the store. Clear three of six rooms to walk back onto the
  floor; the rooms, their order and their answers change every capture, and each
  gate carries a sign with its one rule. Sweeping light: freeze while it is on
  you. Liar doors: open the door whose number matches the lit lamps. Echo pads:
  step the pads in the flashed order (stand on the start strip to see it again).
  Red light: move on green only. Odd one out: E on the mannequin facing the wrong
  way. Price tags: E on the dearest. A miss restarts that room only and the note
  stays on screen. You return at the walkable spot farthest from the guard with
  3 s of cover. A teammate can still pull you out from the warehouse console.
  The night does not end just because everyone is in the hall. Dawn still does.
  See [redesign.md](redesign.md) for poses, scoring and the night schedule.
- **Purple optional swaps:** press E, then stay within interaction reach and
  still for four seconds. Starting makes noise. Moving, losing reach/sight or
  capture cancels progress. Each stand pays once for the whole team, turns green,
  and offsets one grade penalty on a successful escape. Bonuses never turn a
  defeat into a win; the HUD shows bonus and rescue totals.

The remaining objective marker is the downward world marker; the screen compass
has not been restored. Optional stands appear on the map and Missions page.

The prototype supports local mannequin switching with [ / ]; these additions
use the existing local authority and are not a completed cross-device Fusion
replication implementation. See networking.md for that boundary. No retention
or performance improvement is claimed without playtesting/profiling.

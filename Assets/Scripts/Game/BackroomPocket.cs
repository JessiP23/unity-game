using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;

namespace NightSupermarket.Game
{
    /// <summary>
    /// The hall under the store. Every captive gets their own hall (so two captives never share
    /// gates), built from three of six trial rooms in a seeded order. Each room carries a hanging
    /// sign with its one rule and a "ROOM n / 3" mark on the gate. Failure notes stay on screen
    /// for a few seconds instead of one frame. Clearing the last door puts the mannequin back on
    /// the sales floor through <see cref="LocalMatchAuthority.TryFinishBackroom"/>.
    /// </summary>
    public sealed class BackroomPocket : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(80f, 0f, 40f);
        private const float Span = 10f, HallGap = 26f, NoteSeconds = 2.6f;
        private readonly List<Trip> trips = new List<Trip>();
        private readonly List<Hall> halls = new List<Hall>();
        private LocalMatchAuthority authority;
        private bool teammates;
        /// <summary>Fires when a captive clears the hall: (motor, seconds it took, attempts).</summary>
        public System.Action<PlayerMotor, float, int> Cleared;

        private sealed class Hall
        {
            public Vector3 Origin;
            public GameObject Root;
            public bool InUse;
            public Vector3[] Starts = new Vector3[BackroomCourse.Rooms];
            public GameObject[] Gates = new GameObject[BackroomCourse.Rooms];
            public TextMesh[] GateMarks = new TextMesh[BackroomCourse.Rooms];
            public GameObject[][] Stations = new GameObject[BackroomCourse.Rooms][];
            public Transform[] Signs = new Transform[BackroomCourse.Rooms];
            public TextMesh[] SignText = new TextMesh[BackroomCourse.Rooms];
            public Transform[] Beams = new Transform[BackroomCourse.Rooms];
            public Renderer[][] Lights = new Renderer[BackroomCourse.Rooms][];
            public TextMesh[][] DoorLabels = new TextMesh[BackroomCourse.Rooms][];
            public Transform[][] Pads = new Transform[BackroomCourse.Rooms][];
            public Renderer[][] PadPaint = new Renderer[BackroomCourse.Rooms][];
            public Renderer[] Camera = new Renderer[BackroomCourse.Rooms];
            public Transform[][] Figures = new Transform[BackroomCourse.Rooms][];
            public TextMesh[][] PriceLabels = new TextMesh[BackroomCourse.Rooms][];
            public GameObject WayOut;
        }

        private sealed class Trip
        {
            public PlayerMotor Motor;
            public BackroomCourse Course;
            public Hall Hall;
            public float EchoQuietUntil, NoteUntil, Entered, StartStripSince;
            public int LastPad = -1;
            public string Note;
            public bool ReplayArmed = true;
        }

        public void Configure(LocalMatchAuthority match, bool otherMannequins)
        {
            authority = match;
            teammates = otherMannequins;
        }

        public void Admit(PlayerMotor motor, int seed, int difficulty = 0)
        {
            if (motor == null || authority == null) return;
            if (motor.Record.InBackroom && Find(motor) != null) return;
            var hall = FreeHall();
            var trip = new Trip { Motor = motor, Course = new BackroomCourse(seed, difficulty), Hall = hall, Entered = Time.time };
            trips.Add(trip);
            motor.Record.SetBackroom(true);
            Show(trip);
            Place(motor, hall.Starts[0]);
            if (trip.Course.Trial == BackroomTrial.EchoTiles) trip.EchoQuietUntil = Time.time + 2.2f;
        }

        /// <summary>Ticks every captive, not only the active one, so a teammate's hall keeps running.</summary>
        public void TickAll()
        {
            Prune();
            for (int i = 0; i < trips.Count; i++) Tick(trips[i]);
        }

        public void Tick(PlayerMotor motor)
        {
            Prune();
            var trip = Find(motor);
            if (trip != null) Tick(trip);
        }

        private void Tick(Trip trip)
        {
            var motor = trip.Motor;
            if (trip.Course.Complete || !motor.Record.InBackroom) return;
            Show(trip);
            int room = trip.Course.Room;
            var hall = trip.Hall;
            Vector3 at = hall.Origin + new Vector3(0f, 0f, room * Span);
            bool moving = motor.ActualSpeed > motor.Rules.movementThreshold;
            switch (trip.Course.Trial)
            {
                case BackroomTrial.StillLight:
                {
                    float swing = Mathf.PingPong(Time.time / (float)trip.Course.BeamSeconds, 1f);
                    hall.Beams[room].position = at + new Vector3(Mathf.Lerp(-3.2f, 3.2f, swing), 1.2f, 5.4f);
                    if (BeamHits(trip, motor.transform.position) && moving)
                    {
                        Place(motor, hall.Starts[room]);
                        Say(trip, "The light caught you moving. Freeze when it is on you; cross when it passes.");
                    }
                    break;
                }
                case BackroomTrial.RedLight:
                {
                    bool red = trip.Course.RedLightIsRed(Time.time - trip.Entered);
                    hall.Camera[room].material.color = red ? new Color(1f, 0.2f, 0.15f) : new Color(0.2f, 0.9f, 0.4f);
                    if (red && moving && motor.transform.position.z > at.z + 2.4f)
                    {
                        Place(motor, hall.Starts[room]);
                        Say(trip, "The camera was red. Move on green only, and stop before it turns.");
                    }
                    break;
                }
                case BackroomTrial.EchoTiles:
                {
                    PaintEcho(trip, room);
                    // Standing on the start strip for a second replays the flash.
                    bool onStrip = motor.transform.position.z < at.z + 2.0f;
                    if (onStrip && !moving)
                    {
                        if (trip.StartStripSince == 0) trip.StartStripSince = Time.time;
                        else if (trip.ReplayArmed && Time.time - trip.StartStripSince > 1f && Time.time >= trip.EchoQuietUntil)
                        { trip.EchoQuietUntil = Time.time + 2.2f; trip.ReplayArmed = false; Say(trip, "Watch the pads flash again.", 2.2f); }
                    }
                    else { trip.StartStripSince = 0; if (!onStrip) trip.ReplayArmed = true; }
                    if (Time.time < trip.EchoQuietUntil) break;
                    int pad = PadUnder(hall, room, motor.transform.position);
                    if (pad < 0) trip.LastPad = -1;
                    else if (pad != trip.LastPad)
                    {
                        trip.LastPad = pad;
                        int before = trip.Course.Room;
                        if (!trip.Course.TryPad(pad))
                        {
                            trip.EchoQuietUntil = Time.time + 2.2f;
                            Say(trip, "Wrong pad. The order flashes again — step back to the start strip to see it any time.", 3f);
                            break;
                        }
                        if (trip.Course.Complete || trip.Course.Room != before) Opened(trip, before);
                    }
                    break;
                }
            }
            if (Time.time > trip.NoteUntil) trip.Note = null;
        }

        public string PromptFor(PlayerMotor motor)
        {
            var trip = Find(motor);
            if (trip == null) return "";
            if (!string.IsNullOrEmpty(trip.Note)) return trip.Note + RescueLine();
            if (trip.Course.Complete) return "Cleared. The green door leads back to the store." + RescueLine();
            string room = "Room " + (trip.Course.Room + 1) + " of 3 · ";
            return trip.Course.Trial switch
            {
                BackroomTrial.StillLight => room + "Cross while the light is off you. Freeze when it is on you.",
                BackroomTrial.LiarDoors => room + trip.Course.LitCount + (trip.Course.LitCount == 1 ? " lamp is" : " lamps are") + " lit. Open the door with that number.",
                BackroomTrial.EchoTiles => room + "Step the pads in the order they flashed. Stand on the start strip to see it again.",
                BackroomTrial.RedLight => room + "Walk on green. Stop before red. Reach the gap at the far end.",
                BackroomTrial.OddOneOut => room + "Five mannequins. One faces the wrong way. E on that one.",
                _ => room + "Three price tags. E on the dearest one."
            } + RescueLine();
        }

        public string PromptFor(int room, int option) => PromptFor(occupant: null, room, option);

        public string PromptFor(PlayerMotor occupant, int room, int option)
        {
            var trip = occupant != null ? Find(occupant) : trips.Count > 0 ? trips[trips.Count - 1] : null;
            if (trip == null) return "";
            if (room == BackroomCourse.Rooms) return "E — step back onto the sales floor";
            if (trip.Course.Complete || room != trip.Course.Room) return "That room is already behind you.";
            switch (trip.Course.Trial)
            {
                case BackroomTrial.StillLight:
                    return BeamHits(trip, occupant != null ? occupant.transform.position : Vector3.zero) ? "Freeze. The light is on you." : "E — slip through while the light is off you.";
                case BackroomTrial.RedLight:
                    return trip.Course.RedLightIsRed(Time.time - trip.Entered) ? "Red. Hold still." : "E — through the gap on green.";
                case BackroomTrial.LiarDoors:
                    return "E — door " + trip.Course.DoorMark(option) + ". Does it match the lit lamps?";
                case BackroomTrial.OddOneOut:
                    return "E — this one is the odd one out";
                case BackroomTrial.PriceTags:
                    return "E — this is the dearest tag";
                default:
                    return "Walk the pads in the order they flashed.";
            }
        }

        public bool TryUse(PlayerMotor player, int room, int option)
        {
            var trip = Find(player);
            if (trip == null || authority == null) return false;
            if (room == BackroomCourse.Rooms)
            {
                if (!trip.Course.Complete) return false;
                if (!authority.TryFinishBackroom(player.Record.Id)) return false;
                Cleared?.Invoke(player, Time.time - trip.Entered, trip.Course.Attempts);
                return true;
            }
            if (room != trip.Course.Room) return false;
            bool opened = trip.Course.Trial switch
            {
                BackroomTrial.StillLight => trip.Course.TryPassStill(BeamHits(trip, player.transform.position)),
                BackroomTrial.RedLight => trip.Course.TryPassRedLight(trip.Course.RedLightIsRed(Time.time - trip.Entered)),
                BackroomTrial.LiarDoors => trip.Course.TryDoor(option),
                BackroomTrial.OddOneOut => trip.Course.TryFigure(option),
                BackroomTrial.PriceTags => trip.Course.TryPrice(option),
                _ => false
            };
            if (!opened)
            {
                Say(trip, trip.Course.Trial switch
                {
                    BackroomTrial.LiarDoors => "That door lies. Count the lit lamps on the ceiling again.",
                    BackroomTrial.OddOneOut => "Not that one. Look at which way each mannequin faces.",
                    BackroomTrial.PriceTags => "Not the dearest. Compare all three numbers.",
                    BackroomTrial.RedLight => "Red light. Wait for green.",
                    _ => "The light is on you. Wait for it to pass."
                });
                return false;
            }
            Opened(trip, room);
            return true;
        }

        private void Opened(Trip trip, int room)
        {
            Show(trip);
            trip.Hall.Gates[room].SetActive(false);
            if (trip.Course.Complete) trip.Hall.WayOut.SetActive(true);
            else if (trip.Course.Trial == BackroomTrial.EchoTiles) trip.EchoQuietUntil = Time.time + 2.2f;
            trip.LastPad = -1;
            trip.ReplayArmed = true;
            Say(trip, trip.Course.Complete ? "Hall cleared. Green door ahead." : "Room " + (room + 1) + " cleared.", 1.8f);
        }

        /// <summary>Teleport facing down the hall (+Z), so the player never arrives staring at a wall.</summary>
        private static void Place(PlayerMotor motor, Vector3 at)
        {
            motor.Teleport(at);
            motor.transform.rotation = Quaternion.identity;
        }

        private static void Say(Trip trip, string note, float seconds = NoteSeconds)
        {
            trip.Note = note;
            trip.NoteUntil = Time.time + seconds;
        }

        private void Show(Trip trip)
        {
            var hall = trip.Hall;
            for (int room = 0; room < BackroomCourse.Rooms; room++)
            {
                var trial = trip.Course.TrialAt(room);
                for (int t = 0; t < hall.Stations[room].Length; t++) hall.Stations[room][t].SetActive((int)trial == t);
                hall.Gates[room].SetActive(room >= trip.Course.Room);
                hall.GateMarks[room].text = "ROOM " + (room + 1) + " / 3";
                hall.SignText[room].text = SignFor(trial);
                if (trial == BackroomTrial.LiarDoors)
                    for (int light = 0; light < 3; light++)
                    {
                        hall.Lights[room][light].enabled = light < trip.Course.LitCount;
                        hall.DoorLabels[room][light].text = trip.Course.DoorMark(light).ToString();
                    }
                else if (trial == BackroomTrial.OddOneOut)
                    for (int f = 0; f < BackroomCourse.Figures; f++)
                        hall.Figures[room][f].rotation = Quaternion.Euler(0, trip.Course.FigureFacesYou(f) ? 0f : 180f, 0);
                else if (trial == BackroomTrial.PriceTags)
                    for (int tag = 0; tag < 3; tag++)
                        hall.PriceLabels[room][tag].text = "$" + trip.Course.Price(tag);
            }
            hall.WayOut.SetActive(trip.Course.Complete);
        }

        private static string SignFor(BackroomTrial trial) => trial switch
        {
            BackroomTrial.StillLight => "DON'T MOVE\nIN THE LIGHT",
            BackroomTrial.LiarDoors => "COUNT THE LAMPS\nOPEN THAT DOOR",
            BackroomTrial.EchoTiles => "REPEAT\nTHE PADS",
            BackroomTrial.RedLight => "GREEN: GO\nRED: FREEZE",
            BackroomTrial.OddOneOut => "ONE FACES\nTHE WRONG WAY",
            _ => "PICK THE\nDEAREST TAG"
        };

        private Trip Find(PlayerMotor motor)
        {
            for (int i = 0; i < trips.Count; i++) if (trips[i].Motor == motor) return trips[i];
            return null;
        }

        private void Prune()
        {
            for (int i = trips.Count - 1; i >= 0; i--)
                if (trips[i].Motor == null || !trips[i].Motor.Record.InBackroom)
                {
                    trips[i].Hall.InUse = false;
                    trips.RemoveAt(i);
                }
        }

        private string RescueLine() => teammates ? " A teammate can still free you from the warehouse console." : "";

        private bool BeamHits(Trip trip, Vector3 position)
        {
            int room = trip.Course.Room;
            if (room >= BackroomCourse.Rooms || trip.Course.TrialAt(room) != BackroomTrial.StillLight) return false;
            Vector3 beam = trip.Hall.Beams[room].position;
            return Mathf.Abs(position.x - beam.x) < 0.95f && Mathf.Abs(position.z - beam.z) < 4.3f;
        }

        private static int PadUnder(Hall hall, int room, Vector3 position)
        {
            int found = -1;
            float best = 0.75f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 pad = hall.Pads[room][i].position;
                float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(pad.x, pad.z));
                if (distance < best) { best = distance; found = i; }
            }
            return found;
        }

        private void PaintEcho(Trip trip, int room)
        {
            float age = trip.EchoQuietUntil - Time.time;
            int length = trip.Course.EchoLength;
            float step = 2.2f / length;
            int shown = age > 0f ? Mathf.Clamp((int)((2.2f - age) / step), 0, length) : -1;
            for (int i = 0; i < 4; i++)
            {
                bool on = shown >= 0 && shown < length && trip.Course.EchoAt(shown) == i;
                if (shown < 0 && trip.Course.EchoProgress > 0)
                    for (int s = 0; s < trip.Course.EchoProgress; s++)
                        if (trip.Course.EchoAt(s) == i) on = true;
                trip.Hall.PadPaint[room][i].material.color = on ? new Color(0.95f, 0.85f, 0.35f) : new Color(0.28f, 0.24f, 0.16f);
            }
        }

        // ---------- building ----------

        private Hall FreeHall()
        {
            foreach (var hall in halls) if (!hall.InUse) { hall.InUse = true; return hall; }
            var built = BuildHall(halls.Count);
            built.InUse = true;
            halls.Add(built);
            return built;
        }

        private Hall BuildHall(int index)
        {
            var hall = new Hall { Origin = Origin + new Vector3(index * HallGap, 0f, 0f) };
            hall.Root = new GameObject("Back hall " + (index + 1));
            hall.Root.transform.SetParent(transform, false);
            var parent = hall.Root.transform;
            var wall = new Color(0.78f, 0.69f, 0.38f);
            var carpet = new Color(0.42f, 0.34f, 0.2f);
            for (int room = 0; room < BackroomCourse.Rooms; room++)
            {
                Vector3 at = hall.Origin + new Vector3(0f, 0f, room * Span);
                hall.Starts[room] = at + new Vector3(0f, 0.1f, 1.2f);
                PrimitiveWorld.Box(parent, "Backroom floor", at + new Vector3(0f, -0.5f, 5f), new Vector3(8f, 1f, 10f), carpet);
                PrimitiveWorld.Box(parent, "Backroom start strip", at + new Vector3(0f, 0.005f, 1.0f), new Vector3(7.6f, 0.01f, 1.9f), new Color(0.5f, 0.42f, 0.26f));
                if (room == 0) PrimitiveWorld.Box(parent, "Backroom wall", at + new Vector3(0f, 1.5f, 0.2f), new Vector3(8f, 3f, 0.3f), wall);
                PrimitiveWorld.Box(parent, "Backroom wall", at + new Vector3(-4f, 1.5f, 5f), new Vector3(0.3f, 3f, 10f), wall);
                PrimitiveWorld.Box(parent, "Backroom wall", at + new Vector3(4f, 1.5f, 5f), new Vector3(0.3f, 3f, 10f), wall);
                PrimitiveWorld.Box(parent, "Backroom ceiling", at + new Vector3(0f, 3f, 5f), new Vector3(8f, 0.2f, 10f), new Color(0.93f, 0.9f, 0.72f));
                hall.Gates[room] = PrimitiveWorld.Box(parent, "Backroom gate", at + new Vector3(0f, 1.5f, 10f), new Vector3(8f, 3f, 0.35f), wall);
                // The rule hangs on the gate the player walks toward; the room counter sits beside it.
                hall.GateMarks[room] = Mark(hall.Gates[room].transform, at + new Vector3(-3.1f, 2.6f, 9.78f), "ROOM", 0.12f, new Color(0.9f, 0.85f, 0.7f));
                var gateFace = new GameObject("Gate face").transform;
                gateFace.SetParent(hall.Gates[room].transform, true); // keeps world scale 1 under the scaled gate
                var board = PrimitiveWorld.Box(gateFace, "Backroom sign", at + new Vector3(0f, 2.55f, 9.76f), new Vector3(3.2f, 0.74f, 0.05f), new Color(0.12f, 0.1f, 0.08f));
                var boardSolid = board.GetComponent<Collider>(); if (boardSolid != null) boardSolid.enabled = false;
                hall.Signs[room] = board.transform;
                hall.SignText[room] = Mark(hall.Gates[room].transform, at + new Vector3(0f, 2.55f, 9.72f), "RULE", 0.16f, new Color(1f, 0.85f, 0.35f));
                hall.Stations[room] = new GameObject[6];
                hall.Stations[room][(int)BackroomTrial.StillLight] = BuildStill(hall, parent, at, room);
                hall.Stations[room][(int)BackroomTrial.LiarDoors] = BuildDoors(hall, parent, at, room);
                hall.Stations[room][(int)BackroomTrial.EchoTiles] = BuildEcho(hall, parent, at, room);
                hall.Stations[room][(int)BackroomTrial.RedLight] = BuildRedLight(hall, parent, at, room);
                hall.Stations[room][(int)BackroomTrial.OddOneOut] = BuildFigures(hall, parent, at, room);
                hall.Stations[room][(int)BackroomTrial.PriceTags] = BuildPrices(hall, parent, at, room);
                foreach (var station in hall.Stations[room]) station.SetActive(false);
            }
            hall.WayOut = PrimitiveWorld.Box(parent, "Backroom exit", hall.Origin + new Vector3(0f, 1.2f, BackroomCourse.Rooms * Span + 1f), new Vector3(2f, 2.4f, 0.3f), new Color(0.25f, 0.55f, 0.38f));
            var exit = hall.WayOut.AddComponent<BackroomUse>();
            exit.Pocket = this; exit.Room = BackroomCourse.Rooms; exit.Option = -1;
            Mark(hall.WayOut.transform, hall.Origin + new Vector3(0f, 2.0f, BackroomCourse.Rooms * Span + 0.78f), "STORE", 0.16f);
            hall.WayOut.SetActive(false);
            PrimitiveWorld.Box(parent, "Backroom landing", hall.Origin + new Vector3(0f, -0.5f, BackroomCourse.Rooms * Span + 2f), new Vector3(8f, 1f, 4f), carpet);
            PrimitiveWorld.Box(parent, "Backroom landing wall", hall.Origin + new Vector3(0f, 1.5f, BackroomCourse.Rooms * Span + 4f), new Vector3(8f, 3f, 0.3f), wall);
            PrimitiveWorld.Box(parent, "Backroom landing wall", hall.Origin + new Vector3(-4f, 1.5f, BackroomCourse.Rooms * Span + 2f), new Vector3(0.3f, 3f, 4f), wall);
            PrimitiveWorld.Box(parent, "Backroom landing wall", hall.Origin + new Vector3(4f, 1.5f, BackroomCourse.Rooms * Span + 2f), new Vector3(0.3f, 3f, 4f), wall);
            return hall;
        }

        private GameObject BuildStill(Hall hall, Transform parent, Vector3 at, int room)
        {
            var root = new GameObject("Still light"); root.transform.SetParent(parent, false);
            var beam = PrimitiveWorld.Box(root.transform, "Backroom beam", at + new Vector3(0f, 1.2f, 5.4f), new Vector3(1.7f, 2.4f, 8.4f), new Color(1f, 0.96f, 0.75f));
            var solid = beam.GetComponent<Collider>(); if (solid != null) solid.enabled = false;
            hall.Beams[room] = beam.transform;
            Gap(root.transform, at, room);
            return root;
        }

        private GameObject BuildRedLight(Hall hall, Transform parent, Vector3 at, int room)
        {
            var root = new GameObject("Red light"); root.transform.SetParent(parent, false);
            var pole = PrimitiveWorld.Box(root.transform, "Camera pole", at + new Vector3(0f, 1.6f, 9.0f), new Vector3(0.15f, 3.2f, 0.15f), new Color(0.2f, 0.2f, 0.22f));
            var solid = pole.GetComponent<Collider>(); if (solid != null) solid.enabled = false;
            var lamp = PrimitiveWorld.Box(root.transform, "Camera lamp", at + new Vector3(0f, 2.75f, 8.9f), new Vector3(0.9f, 0.5f, 0.3f), new Color(0.2f, 0.9f, 0.4f));
            var lampSolid = lamp.GetComponent<Collider>(); if (lampSolid != null) lampSolid.enabled = false;
            hall.Camera[room] = lamp.GetComponent<Renderer>();
            PrimitiveWorld.Box(root.transform, "Go line", at + new Vector3(0f, 0.006f, 2.4f), new Vector3(7.6f, 0.012f, 0.1f), new Color(0.9f, 0.9f, 0.9f));
            Gap(root.transform, at, room);
            return root;
        }

        private void Gap(Transform root, Vector3 at, int room)
        {
            var gap = PrimitiveWorld.Box(root, "Backroom gap", at + new Vector3(0f, 1.2f, 9.2f), new Vector3(1.6f, 2.2f, 0.3f), new Color(0.2f, 0.2f, 0.18f));
            var use = gap.AddComponent<BackroomUse>();
            use.Pocket = this; use.Room = room; use.Option = -1;
        }

        private GameObject BuildDoors(Hall hall, Transform parent, Vector3 at, int room)
        {
            var root = new GameObject("Liar doors"); root.transform.SetParent(parent, false);
            hall.Lights[room] = new Renderer[3];
            hall.DoorLabels[room] = new TextMesh[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -2.2f + i * 2.2f;
                var door = PrimitiveWorld.Box(root.transform, "Backroom door", at + new Vector3(x, 1.2f, 8.6f), new Vector3(1.3f, 2.3f, 0.25f), new Color(0.55f, 0.28f, 0.22f));
                var use = door.AddComponent<BackroomUse>();
                use.Pocket = this; use.Room = room; use.Option = i;
                hall.DoorLabels[room][i] = Mark(door.transform, at + new Vector3(x, 1.6f, 8.35f), "?", 0.28f);
                var lamp = PrimitiveWorld.Box(root.transform, "Backroom lamp", at + new Vector3(x, 2.7f, 6f), new Vector3(0.35f, 0.12f, 0.8f), new Color(1f, 0.95f, 0.7f));
                hall.Lights[room][i] = lamp.GetComponent<Renderer>();
            }
            return root;
        }

        private GameObject BuildEcho(Hall hall, Transform parent, Vector3 at, int room)
        {
            var root = new GameObject("Echo pads"); root.transform.SetParent(parent, false);
            hall.Pads[room] = new Transform[4];
            hall.PadPaint[room] = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1.3f : 1.3f;
                float z = i < 2 ? 3.2f : 6.4f;
                var pad = PrimitiveWorld.Box(root.transform, "Backroom pad", at + new Vector3(x, 0.08f, z), new Vector3(1.3f, 0.12f, 1.3f), new Color(0.28f, 0.24f, 0.16f));
                var solid = pad.GetComponent<Collider>(); if (solid != null) solid.enabled = false;
                hall.Pads[room][i] = pad.transform;
                hall.PadPaint[room][i] = pad.GetComponent<Renderer>();
            }
            return root;
        }

        private GameObject BuildFigures(Hall hall, Transform parent, Vector3 at, int room)
        {
            var root = new GameObject("Odd one out"); root.transform.SetParent(parent, false);
            hall.Figures[room] = new Transform[BackroomCourse.Figures];
            for (int i = 0; i < BackroomCourse.Figures; i++)
            {
                float x = -2.8f + i * 1.4f;
                var figure = new GameObject("Figure " + (i + 1)).transform;
                figure.SetParent(root.transform, false);
                figure.position = at + new Vector3(x, 0f, 6.5f);
                var body = PrimitiveWorld.Box(figure, "Body", figure.position + new Vector3(0f, 0.9f, 0f), new Vector3(0.45f, 1.8f, 0.3f), new Color(0.85f, 0.82f, 0.75f));
                var nose = PrimitiveWorld.Box(figure, "Face", figure.position + new Vector3(0f, 1.6f, -0.2f), new Vector3(0.2f, 0.12f, 0.12f), new Color(0.2f, 0.2f, 0.25f));
                var noseSolid = nose.GetComponent<Collider>(); if (noseSolid != null) noseSolid.enabled = false;
                var use = body.AddComponent<BackroomUse>();
                use.Pocket = this; use.Room = room; use.Option = i;
                hall.Figures[room][i] = figure;
            }
            return root;
        }

        private GameObject BuildPrices(Hall hall, Transform parent, Vector3 at, int room)
        {
            var root = new GameObject("Price tags"); root.transform.SetParent(parent, false);
            hall.PriceLabels[room] = new TextMesh[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -2.2f + i * 2.2f;
                var stand = PrimitiveWorld.Box(root.transform, "Price stand", at + new Vector3(x, 0.6f, 6.5f), new Vector3(0.7f, 1.2f, 0.4f), new Color(0.3f, 0.32f, 0.36f));
                var use = stand.AddComponent<BackroomUse>();
                use.Pocket = this; use.Room = room; use.Option = i;
                var tag = PrimitiveWorld.Box(root.transform, "Price tag", at + new Vector3(x, 1.5f, 6.5f), new Vector3(0.9f, 0.5f, 0.08f), new Color(0.95f, 0.9f, 0.3f));
                var tagSolid = tag.GetComponent<Collider>(); if (tagSolid != null) tagSolid.enabled = false;
                hall.PriceLabels[room][i] = Mark(tag.transform, at + new Vector3(x, 1.5f, 6.44f), "$", 0.24f, new Color(0.1f, 0.1f, 0.1f));
            }
            return root;
        }

        /// <summary>World label facing the player (who walks toward +Z). An unscaled holder keeps text crisp under scaled boxes.</summary>
        private static TextMesh Mark(Transform parent, Vector3 position, string text, float letterHeight, Color? ink = null)
        {
            var holder = new GameObject("Mark").transform;
            holder.position = position;
            holder.rotation = Quaternion.identity; // readable by someone walking toward +Z, which is how the hall runs
            holder.SetParent(parent, true);
            return SignFactory.Label(holder, text, letterHeight, ink ?? Color.white);
        }
    }

    public sealed class BackroomUse : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        public BackroomPocket Pocket;
        public int Room;
        public int Option;
        public string Prompt => Pocket != null ? Pocket.PromptFor(null, Room, Option) : "";
        public string PromptFor(PlayerMotor player) => Pocket != null ? Pocket.PromptFor(player, Room, Option) : "";
        public bool TryInteract(PlayerMotor player) => Pocket != null && Pocket.TryUse(player, Room, Option);
    }
}

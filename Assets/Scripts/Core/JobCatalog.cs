using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    /// <summary>
    /// One kind of job the store can ask for. Rules only: what counts, how many, the words a
    /// player reads. Where the things stand and what they look like is the world's business.
    /// </summary>
    public sealed class JobTemplate
    {
        public readonly string Id, Title, Tag, Destination, RequiredItem, How, Department;
        public readonly ActionKind Kind;
        public readonly int Quantity;
        /// <summary>Jobs in the same family would feel like repeats, so a draw takes at most one per family.</summary>
        public readonly string Family;
        public JobTemplate(string id, string title, ActionKind kind, string tag, int quantity, string how, string department, string family,
            string destination = "", string requiredItem = "")
        {
            Id = id; Title = title; Kind = kind; Tag = tag; Quantity = quantity; How = how; Department = department; Family = family;
            Destination = destination; RequiredItem = requiredItem;
        }
        public MissionRule Rule() => new MissionRule(Id, Title, Kind, Tag, Quantity, Destination);
    }

    /// <summary>Where in the mall a job takes you. A night draws one job from each, so every night crosses the whole mall.</summary>
    public enum JobArea { Supermarket, Mall, Gallery }

    /// <summary>
    /// The jobs and the draw. Shift 0 is the classic trio every test knows; any other shift draws one job
    /// on the supermarket floor, one out in the mall (atrium, toy store) and one up in the gallery, with no two
    /// from the same family, so a night mixes a carry, a hold-still and a trick and sends you across all of it.
    /// </summary>
    public static class JobCatalog
    {
        public const int PerNight = 3;

        public static readonly JobTemplate[] All =
        {
            new JobTemplate("collect-crates", "Collect three crates", ActionKind.Collect, "object", 3,
                "One at a time. E picks up, G drops. Follow the gold marker.", "Aisles", "carry"),
            new JobTemplate("place-crate", "Place a crate on the Clothing rug", ActionKind.Place, "object", 1,
                "Carry a crate to the green rug in Clothing and press G.", "Clothing", "carry", "clothing"),
            new JobTemplate("steal-shirt", "Steal a shirt from Clothing", ActionKind.Collect, "shirt", 1,
                "Blue shirt on a Clothing table. Press E.", "Clothing", "grab"),
            new JobTemplate("window-display", "Model in the shop window for 20 seconds", ActionKind.Place, "window", 1,
                "Stand in the window by the checkout and hold still. Moving loses time.", "Checkout", "hold", "window"),
            new JobTemplate("swap-places", "Swap places with a display mannequin", ActionKind.Place, "display", 1,
                "E at the marked plinth in Clothing while nobody is looking, then hold still 6 s.", "Clothing", "hold", "plinth"),
            new JobTemplate("price-tags", "Swap two price tags", ActionKind.Steal, "pricetag", 2,
                "E on a marked tag at a shelf end and stay still 3 s. Twice.", "Aisles", "trick"),
            new JobTemplate("camera-blind", "Turn the checkout camera, then cross the lane", ActionKind.Move, "camera", 1,
                "E on the camera pole for 2 s, then walk through the checkout lane while it looks away.", "Checkout", "trick", "checkout"),
            new JobTemplate("lost-toy", "Return the lost toy to the service desk", ActionKind.Place, "toy", 1,
                "Find the toy in Home, pick it up with E, then E at the Lost & Found on the service desk.", "Home", "grab", "lostfound", "toy"),
            new JobTemplate("fragile-vase", "Carry a vase to the Home rug without breaking it", ActionKind.Place, "vase", 1,
                "Vases are in the aisles. Carrying rattles; a hard drop breaks it. G on the Home rug.", "Home", "carry", "home"),
            new JobTemplate("carousel-ride", "Ride the toy-store carousel for one full turn", ActionKind.Place, "carousel", 1,
                "Through the atrium to the toy store. Climb on a horse (E) and ride a whole turn, about 20 s. A rider is part of the ride; climbing on and off in plain sight is not.", "Toy store", "hold", "carousel"),
            new JobTemplate("tv-channel", "Switch three gallery TVs to the mannequin channel", ActionKind.Place, "tvchannel", 3,
                "Upstairs: the escalator or the stairs by Clothing. E on a marked TV on the gallery wall and keep still 2 s, three times. Shoppers stop to watch the wall.", "Gallery", "tv"),
            new JobTemplate("sofa-shoot", "Sit for the catalogue photo on the gallery sofa", ActionKind.Place, "sofa", 1,
                "Upstairs in the gallery lounge. E on the sofa to sit down, then keep still 15 s while the photographer's flash goes off. A seated mannequin is on display; sitting down and getting up are moving.", "Gallery", "sit"),
            new JobTemplate("top-shelf-robot", "Take the robot from the toy store's top shelf", ActionKind.Collect, "robot", 1,
                "Toy store, tall shelf by the step stool. Climb the stool, E on the robot. Run past a toy shelf and it rattles.", "Toy store", "grab"),
            new JobTemplate("water-planters", "Water two of the atrium planters", ActionKind.Place, "planter", 2,
                "The marked planters in the atrium. E at one and keep still 3 s while the can pours, twice. The atrium cameras sweep that floor: don't move in the red light.", "Atrium", "hold"),
            new JobTemplate("pillows-upstairs", "Carry the throw pillows up to the gallery sofa", ActionKind.Place, "pillows", 1,
                "The pillows are on the ottoman in Home. E picks them up; take the escalator or the stairs; G on the rug by the gallery sofa.", "Gallery", "carry", "gallery")
        };

        public static JobArea AreaOf(JobTemplate job) => job.Department switch
        {
            "Gallery" => JobArea.Gallery,
            "Toy store" => JobArea.Mall,
            "Atrium" => JobArea.Mall,
            _ => JobArea.Supermarket
        };

        public static JobTemplate Find(string id)
        {
            foreach (var job in All) if (job.Id == id) return job;
            return null;
        }

        public static IReadOnlyList<JobTemplate> Draw(int shift)
        {
            if (shift == 0) return new[] { All[0], All[1], All[2] };
            var rng = new Random(shift * 104729 + 7);
            var picked = new List<JobTemplate>(PerNight);
            var families = new HashSet<string>();
            // One from each area, in the order a night walks them: the supermarket floor, out into the mall, up to the gallery.
            foreach (var area in new[] { JobArea.Supermarket, JobArea.Mall, JobArea.Gallery })
            {
                var pool = new List<JobTemplate>();
                foreach (var job in All) if (AreaOf(job) == area && !families.Contains(job.Family)) pool.Add(job);
                if (pool.Count == 0) foreach (var job in All) if (AreaOf(job) == area) pool.Add(job); // never leave an area out
                var chosen = pool[rng.Next(pool.Count)];
                families.Add(chosen.Family);
                picked.Add(chosen);
            }
            return picked;
        }
    }
}

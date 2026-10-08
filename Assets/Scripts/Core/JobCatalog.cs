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

    /// <summary>
    /// The nine jobs and the draw. Shift 0 is the classic trio every test knows; any other shift
    /// draws three jobs from the pool with no two from the same family, so a night mixes a carry,
    /// a hold-still and a trick instead of three errands.
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
                "Vases are in the aisles. Carrying rattles; a hard drop breaks it. G on the Home rug.", "Home", "carry", "home")
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
            var pool = new List<JobTemplate>(All);
            var picked = new List<JobTemplate>(PerNight);
            var families = new HashSet<string>();
            while (picked.Count < PerNight && pool.Count > 0)
            {
                var job = pool[rng.Next(pool.Count)];
                pool.Remove(job);
                if (!families.Add(job.Family)) continue;
                picked.Add(job);
            }
            // Fill from the pool if family limits left a slot empty (cannot happen with four families, kept for safety).
            while (picked.Count < PerNight && pool.Count > 0) { picked.Add(pool[0]); pool.RemoveAt(0); }
            return picked;
        }
    }
}

using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public sealed class MissionRule
    {
        public readonly string Id, Title, Tag, Destination, Owner;
        public readonly ActionKind Kind;
        public readonly int Quantity;
        public MissionRule(string id, string title, ActionKind kind, string tag, int quantity, string destination = "", string owner = "")
        {
            if (string.IsNullOrWhiteSpace(id) || quantity < 1) throw new ArgumentException("Mission ID and positive quantity required.");
            Id = id; Title = title; Kind = kind; Tag = tag; Quantity = quantity; Destination = destination; Owner = owner;
        }
    }
    public sealed class MissionTracker
    {
        public MissionRule Rule { get; }
        public int Progress { get; private set; }
        public bool Failed { get; private set; }
        public bool Complete => !Failed && Progress >= Rule.Quantity;
        private readonly HashSet<string> objects = new HashSet<string>();
        public event Action Changed;
        public MissionTracker(MissionRule rule) { Rule = rule; }
        /// <summary>Consumes accepted authority events, never client claims.</summary>
        public void Apply(ObjectAction action)
        {
            if (Complete || Failed || action.Kind != Rule.Kind || string.IsNullOrEmpty(action.ObjectId)) return;
            if (!string.IsNullOrEmpty(Rule.Tag) && Rule.Tag != action.Tag) return;
            if (!string.IsNullOrEmpty(Rule.Destination) && Rule.Destination != action.Destination) return;
            if (!string.IsNullOrEmpty(Rule.Owner) && Rule.Owner != action.PlayerId) return;
            if (!objects.Add(action.ObjectId)) return;
            Progress++; Changed?.Invoke();
        }
        public void Fail() { if (Complete || Failed) return; Failed = true; Changed?.Invoke(); }
        /// <summary>Debug completion. Accepted world actions remain the gameplay path.</summary>
        public void DebugComplete()
        {
            if (Failed || Complete) return;
            Progress = Rule.Quantity; Changed?.Invoke();
        }
    }
}

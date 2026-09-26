using System;
using System.Collections.Generic;
using NightSupermarket.Core;
namespace NightSupermarket.Game
{
    public sealed class MissionSystem : IDisposable
    {
        public IReadOnlyList<MissionTracker> Missions => trackers.AsReadOnly();
        private readonly List<MissionTracker> trackers = new List<MissionTracker>();
        private readonly MissionDefinition[] definitions;
        private readonly IDisposable subscription;
        public bool Complete
        {
            get { for (int i = 0; i < trackers.Count; i++) if (definitions[i].required && !trackers[i].Complete) return false; return true; }
        }
        public MissionSystem(MissionDefinition[] definitions, WorldSignals signals, Func<string, PlayerInventory> inventory)
        {
            this.definitions = definitions;
            foreach (var definition in definitions) trackers.Add(new MissionTracker(definition.CreateRule()));
            subscription = signals.Actions.Subscribe(action =>
            {
                var items = inventory(action.PlayerId); if (items == null) return;
                for (int i = 0; i < trackers.Count; i++)
                    if (string.IsNullOrEmpty(definitions[i].requiredItem) || items.Items.Count(definitions[i].requiredItem) > 0)
                        trackers[i].Apply(action);
            });
        }
        public void Dispose() => subscription.Dispose();
    }
}

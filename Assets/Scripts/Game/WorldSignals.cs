using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Loudness;
        public readonly string Source;
        public readonly double Timestamp;
        public NoiseEvent(Vector3 position, float loudness, string source)
        { Position = position; Loudness = loudness; Source = source; Timestamp = Time.timeAsDouble; }
    }
    public sealed class WorldSignals
    {
        public EventStream<ObjectAction> Actions { get; } = new EventStream<ObjectAction>();
        public EventStream<NoiseEvent> Noise { get; } = new EventStream<NoiseEvent>();
    }
}

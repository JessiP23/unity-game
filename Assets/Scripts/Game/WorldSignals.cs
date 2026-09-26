using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public enum ActionKind { Collect, Move, Place, Break, Steal, Unlock, Escape }
    public readonly struct ObjectAction
    {
        public readonly string EventId, ObjectId, Tag, PlayerId, Destination;
        public readonly ActionKind Kind;
        public ObjectAction(ActionKind kind, string objectId, string tag, string playerId, string destination = "")
        { EventId = Guid.NewGuid().ToString("N"); Kind = kind; ObjectId = objectId; Tag = tag; PlayerId = playerId; Destination = destination; }
    }
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

using System;
namespace NightSupermarket.Core
{
    public enum ActionKind { Collect, Move, Place, Break, Steal, Unlock, Escape }
    public readonly struct ObjectAction
    {
        public readonly string EventId, ObjectId, Tag, PlayerId, Destination;
        public readonly ActionKind Kind;
        public ObjectAction(ActionKind kind, string objectId, string tag, string playerId, string destination = "")
        { EventId = Guid.NewGuid().ToString("N"); Kind = kind; ObjectId = objectId; Tag = tag; PlayerId = playerId; Destination = destination; }
    }
}

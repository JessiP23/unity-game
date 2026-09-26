# Mission plan

Data-driven definitions: stable ID, type, target tags, quantity, destination and requirements. Authoritative accepted world actions update progress exactly once. Support collect, move, place, break, steal, unlock and escape. Test invalid targets, duplicate actions, failure and completion. No mission code in Phase 0.

Phase 6 implements MissionDefinition assets and pure MissionTracker rules.
Accepted ObjectAction events carry actor/object/tag/destination. Each mission
counts an object once, preventing repeated pickup/event replay from farming
progress. Optional personal owner and required inventory item are validated.
PlacementZone reports a released, settled item inside its trigger. Prototype
assets configure collect-three and place-one objectives without name-based logic.

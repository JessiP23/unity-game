# Guard AI plan

NavMesh adapter executes patrol, investigate, search, chase, capture and return decisions. Hearing consumes typed noise events. Vision is independent of decision source so a human guard can share the same perception/rules. Tests precede implementation. AI Navigation is deferred to the guard phase.

Phase 8: GuardBrain uses independent state handlers, while GuardController
adapts its decisions to NavMeshAgent. Noise is filtered by distance × loudness;
chase/capture take priority over noise. Search rotates locally, then returns to
patrol. Physics geometry supplies the runtime NavMesh; actors/items are excluded,
and the door is a carving obstacle. Vision/suspicion remain separate services.
Discovery capture is performed by the match authority, including when the guard
has not yet closed to capture distance. The guard's Capture handler then returns
to patrol. `[` / `]` can drive that same guard body; vision and hearing stay on it.
The AI brain is not ticked while a player is driving.

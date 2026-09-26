# Architecture

Planned boundaries (not yet implemented):
- Domain: plain C# rules, explicit tick/delta inputs, no Unity or SDK dependencies.
- Unity adapters: input, CharacterController, physics perception, NavMesh, presentation.
- Composition root: constructs services and coordinates lifetime, not gameplay rules.
- Networking: authority validates commands; adapters replicate accepted state.
- Backend: accounts/lobbies/profile persistence separate from match state.

Add Assets/Scripts/Core and a noEngineReferences assembly in Phase 1. Add feature folders as needed. Keep Editor code separate and tests outside shipping assemblies. Prefer injected dependencies, typed events, explicit subscription teardown, and configuration assets. Models, audio and animation are replaceable presentation.

Phase 1 implements Core (no engine references) and Game (Unity adapters).
GameRules validates immutable domain settings; GameRulesAsset is editable Unity
configuration. GameClock receives explicit delta time. MatchFlow only permits
bootstrap → lobby → night → a terminal outcome. EventStream subscriptions are
owned/disposed by consumers. LocalSession uses generated identities and rejects
unknown/mismatched callers. GameLog can be disabled independently of gameplay.

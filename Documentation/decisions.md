# Decisions

1. Start with Phase 0 per the brief; do not generate untested gameplay wholesale.
2. Pin Unity 6000.0.71f1 as a reproducible Unity 6 baseline, not a claim of latest release.
3. Defer unused packages and feature folders to keep the scaffold understandable.
4. Generate scene content through an explicit Editor command, avoiding hand-written scene serialization.
5. URP assets/settings must be generated and verified in Unity; a package declaration alone does not enable URP.
6. No Editor found in standard location or PATH. Phase 0 acceptance remains pending; no Phase 1 work begins yet.

7. Follow-up request authorizes preparing dependencies in advance: Input System,
   Navigation and uGUI/TMP are now declared; implementation still stays phase-gated.
8. Keep official Unity applications/build module under ignored `.toolchain` so
   the user has a local setup without copying binaries into Assets or committing them.
9. Replace manual setup with explicit, repeatable ProjectSetup and a runner that
   rejects missing/empty/failed test results, including stale XML from prior runs.

10. Phase 0 gate passed after installing the Editor locally. Windows build succeeds; Windows runtime checks remain a platform limitation. Bootstrap tests use a preview scene to preserve open user scenes.

11. Discovery captures a mannequin into a warehouse roster and `PlayerState.Captured`. The player object and id stay in the match. Rescue returns that record to `Normal`.
12. `MatchOutcomeEvaluator` is the only win/loss decision. Default victory is the configured escape count after required missions. Dawn, or every mannequin captured with no rescue path left, is defeat. Self-rescue is off unless configured.
13. One successful escape wins by default because `requiredEscapes` is 1. Raise it when every mannequin must leave.
14. Offline play uses `LocalMatchAuthority` as the server. Hot-seat Tab switching only changes which local identity may send commands.
15. Photon Fusion is not installed. Fusion 2.0.13 is the official build that lists Unity 6.0.x. Its download requires a signed-in Photon account and returned 403 here. Fusion 2.1 targets Unity 6000.3 or newer. No Fusion API is invented in source.
16. PlayFab is not installed. The current Unity 6 package depends on Microsoft GDK, which this Mac editor does not use. Account data stays behind `IPlayerDataService` and is not mixed with match state.
17. The guard flashlight is a gameplay cone the vision query can consume. The Unity `Light` does not decide detection.
18. A player can drive the same guard body. That replaces navigation input only. Vision, hearing, and detection stay on that body.
19. Realistic art comes from Poly Haven (CC0 photoscanned props and surfaces) and Microsoft Rocketbox (MIT rigged humans and clips). The Asset Store top-free chart and itch.io free Unity tag were reviewed; they are mostly tools, low-poly, or stylized, and complete realistic supermarkets are paid. Redistributed paid packs are not used.
20. Visuals never feed gameplay. Character animation speed is measured from how far the body moved, and mannequins hold their current frame when they stop so they freeze mid-step.
21. Movement stays fixed-step for authority (and later Fusion), with acceleration and deceleration in `GameRulesAsset`. Smoothness comes from `MotionInterpolator` on cameras and bodies, not from moving gameplay into Update. Stopping from a sprint takes about 0.2 s so the freeze mechanic stays responsive.
22. Resting physical items and solid floor props carve the NavMesh, so the guard walks around them. A PlayMode test puts a crate on the guard's path and fails without carving.

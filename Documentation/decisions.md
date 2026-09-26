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

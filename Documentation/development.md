# Development

## Required environment
Install Unity Hub and Unity Editor **6000.0.71f1**, activate an appropriate license, and add Windows Build Support for PC validation. Open this repository as an existing project. Unity generates missing default settings and resolves packages on first import. Commit generated ProjectSettings, package lock and asset metadata after review; do not commit Library.

Immediate declared packages:
| Package | Version | Reason / dependent systems |
| --- | --- | --- |
| com.unity.render-pipelines.universal | 17.0.4 | URP rendering baseline |
| com.unity.test-framework | 1.4.6 | EditMode and PlayMode tests |
| com.unity.modules.physics | 1.0.0 | Built-in physics; smoke test and later gameplay |

These are declarations, not verified installed dependencies. Unity resolves transitive dependencies.

Defer Input System to Phase 2, AI Navigation to guard AI, uGUI/TextMeshPro to placeholder UI, Fusion 2 until local tests pass, PlayFab until multiplayer tests pass. Verify and document exact supported versions when adding them.

## First import setup
1. Wait for package resolution and compilation; fix all Console errors.
2. Set asset serialization to Force Text and version control to Visible Meta Files.
3. Create a URP Pipeline Asset with Universal Renderer through Assets > Create > Rendering; save under Assets/Settings. Assign it in Graphics and every applicable Quality level. Use Linear color space.
4. Run Night Supermarket > Setup > Create Bootstrap Scene. Save it and add it to the Windows build scene list.
5. Run both test suites as documented in testing.md. Enter Play Mode and check the Console. A blank camera view is expected at this phase.
6. Review and commit generated settings, scene, URP assets and Packages/packages-lock.json.

Before Phase 2, enable the new Input System in Active Input Handling.

Conventions: small responsibilities; PascalCase types/methods; explicit dependencies; XML docs for important public APIs; no global service locator, name-based gameplay lookup, or credentials. Tests precede major systems.

Sources: [Editor release](https://unity.com/releases/editor/whats-new/6000.0.71f1), [package release history](https://release-notes.ds.unity3d.com/search?from_release=6000.0.32f1&to_release=6000.0.71f1).

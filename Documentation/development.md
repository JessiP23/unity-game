# Development

## Local toolchain
The pinned baseline is Unity **6000.0.71f1** (Apple Silicon Editor on this Mac),
with Windows Mono build support. The local toolchain lives under `.toolchain/`.
This folder is ignored by Git; it contains vendor applications and installers,
not game source. Unity licenses and Hub sign-in stay in Unity's normal per-user
locations, never in this repository.

Unity Hub is `.toolchain/Unity Hub.app`. Sign in there and activate an appropriate
license. The Editor executable is `.toolchain/Unity/Unity.app/Contents/MacOS/Unity`.
Alternatively set `UNITY_EDITOR` to a compatible installed Editor executable.

## Configure, test, open
Run from the repository root, with other Editors for this project closed:
```sh
python3 Tools/unity.py setup
python3 Tools/unity.py edit
python3 Tools/unity.py play
python3 Tools/unity.py build
python3 Tools/unity.py open
```
The setup command creates URP renderer/pipeline assets, assigns all Quality
levels, selects Linear color space and Input System, enables text serialization
and visible metadata, creates the bootstrap scene, and adds it to build settings.
It preserves existing assets and other build scenes. Setup and tests use separate
Editor processes so input configuration takes effect after restart.

The build produces `Builds/Windows/NightSupermarket.exe`; Windows runtime testing
still requires Windows. A blank camera scene is expected in Phase 0.

## Dependencies
The user requested preparing dependencies up front. Gameplay still follows the
phase order; preinstalling a package does not mean its system is implemented.

| Package | Version | Reason / dependent systems |
| --- | --- | --- |
| com.unity.render-pipelines.universal | 17.0.4 | Primitive rendering and later presentation |
| com.unity.test-framework | 1.4.6 | EditMode and PlayMode validation |
| com.unity.inputsystem | 1.17.0 | Movement, camera, interaction and debug input |
| com.unity.ai.navigation | 2.0.9 | Guard NavMesh patrol and pursuit |
| com.unity.ugui | 2.0.0 | Placeholder UI, includes TextMeshPro in Unity 6 |

Built-in modules use version 1.0.0 and ship with the Editor: physics, AI,
animation, audio, IMGUI, JSON serialization, UI, UIElements and UnityWebRequest.
Transitive packages are resolved by Unity, with exact resolution recorded in
`Packages/packages-lock.json` after import. No separate legacy TMP package.

Fusion 2 and PlayFab are still absent. Fusion 2.1.1 expects Unity 6000.3+, which
is newer than the pinned 6000.0.71f1 Editor, so it was not added. See
[networking](networking.md). No SDK credentials are stored in the project.
No final art is imported.

The prototype scene is the local night loop: two mannequins, an AI guard,
collect/place objectives, an employee door, warehouse rescue and an escape door.
Tab switches the controlled mannequin. F1 lists debug keys. This is hot-seat
authority, not online multiplayer.

## Conventions
Small responsibilities; PascalCase types/methods; explicit dependencies; XML
docs for important public APIs; no global service locator, name-based gameplay
lookup or credentials. Tests precede major systems. Commit generated settings,
scene and asset metadata after review. Ignore Library, build outputs and toolchain.

## Sources
- [Unity Editor release and official downloads](https://unity.com/releases/editor/whats-new/6000.0.71f1)
- [Unity Input System registry](https://packages.unity.com/com.unity.inputsystem)
- [Unity AI Navigation registry](https://packages.unity.com/com.unity.ai.navigation)
- [uGUI/TextMeshPro documentation](https://docs.unity.cn/Packages/com.unity.ugui@2.0/manual/TextMeshPro/TMPObjects.html)

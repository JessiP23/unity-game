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
python3 Tools/unity.py connect
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

Fusion 2.0.13 is imported under `Assets/Photon/` from the official unitypackage.
Physics 2D is enabled only because Fusion's scene manager references it; this
game stays 3D. Put the Photon App Id in Photon App Settings locally, never in
git. PlayFab is still absent. See [networking](networking.md).

## Art
Free art is downloaded, not hand-copied:
```sh
python3 Tools/fetch_art.py      # ~110 MB into Assets/Resources/Art, skips files already present
python3 Tools/unity.py shots    # renders review screenshots to TestResults/Screenshots
```
Props and surfaces are Poly Haven (CC0). The guard and both mannequins, plus their
idle/walk/run clips, are Microsoft Rocketbox (MIT). Credits and the MIT notice are
in `Assets/Resources/Art/THIRD_PARTY.txt`. `Assets/Editor/ArtImportRules.cs` sets
import settings for anything under `Resources/Art`, so fetched files need no
hand-written `.meta`. `python3 Tools/unity.py setup` also runs rendering setup:
Forward+, post-processing, SSAO, and soft flashlight shadows.

Gameplay does not depend on art. The primitives keep every collider and rule;
`StoreDressing` hides them and dresses the level after the NavMesh bake. With
`Resources/Art` missing, the prototype falls back to plain primitives.

The prototype scene is the local night loop: two mannequins, one guard body,
collect/place objectives, an employee door, warehouse rescue and an escape door.
Tab opens the briefing menu (missions, inventory, reports, cameras, controls).
`[` / `]` switch mannequins and then the guard. The guard uses the same eyes either
way. H opens the Controls page (letters and numbers only, since Mac keyboards send F1-F12 as media keys). This is hot-seat authority, not online multiplayer.
Hot-seat `[` / `]` switching is still the local way to try four mannequins. Online
play needs a Photon App Id in Fusion's Photon App Settings (local only).

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

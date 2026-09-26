# Testing

## Available repository check
Run `python3 Tools/check_scaffold.py`. This checks metadata, JSON, test assembly isolation and required files. It does not compile C# or validate Unity behavior.

## Unity tests
Use Window > General > Test Runner: run EditMode, then PlayMode.
- BootstrapTests: generated additive scene contains a camera and directional-light object.
- PhysicsSmokeTests: a primitive collider intersects a known ray at expected distance.

Batch equivalent (set UNITY_EDITOR to the installed executable):
```sh
mkdir -p TestResults
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/TestResults/edit.xml" -logFile "$PWD/TestResults/edit.log"
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults "$PWD/TestResults/play.xml" -logFile "$PWD/TestResults/play.log"
```
Do not run concurrent Editors on the same project. Inspect XML for actual test count, failures and skipped tests; exit status alone is insufficient.

## Phase 0 acceptance gate
Package resolution, C# compilation, both Unity suites, bootstrap Play Mode, active URP renderer and a Windows smoke build must be verified before advancing. Current Unity results: NOT RUN (Editor unavailable in standard path/PATH). No gameplay tests exist yet.

Phase 1: configurable game states/rules, event lifetimes, local service boundaries, debug logging, with meaningful pure-logic tests first. Later phases add the full scenarios in the supplied brief.

## Repository validation record
Four scaffold checks passed (required files, package pins, asset metadata,
assembly isolation); zero failed. Unity EditMode/PlayMode tests were not run.
Git initialization succeeded after filesystem permission was granted.

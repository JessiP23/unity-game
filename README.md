# Night Supermarket

Unity primitive cooperative stealth prototype. See [development](Documentation/development.md)
for the installed local toolchain and commands, [architecture](Documentation/architecture.md)
for system boundaries and [phase status](Documentation/phase-status.md) for progress.

The store is dressed with free realistic art (Poly Haven props and Microsoft
Rocketbox characters); run `python3 Tools/fetch_art.py` once on a fresh clone.
The local night loop is playable: move, get seen, freeze, get
captured, rescue a teammate, finish the crate objectives and escape before dawn.
Online Fusion and PlayFab are not connected yet. See
[phase status](Documentation/phase-status.md).

Open the prototype scene and press Play. Shoppers and staff fill the store; freeze when anyone looks your way. Tab switches mannequins, then the guard. H shows every control and testing key. See [NPCs](Documentation/npcs.md).

```sh
python3 Tools/unity.py open
```

# Project Lucid

An in-progress reconstruction of Sonic Dream Team for macOS, Windows, and Linux.
You must supply your own macOS game bundle. Game assets are not included.

**Current status:** extraction tooling has been exercised with release 1.10.1.
The original game is not yet playable from this project.

## Getting started

Install Python 3.9 or newer. Place `SonicDreamTeam.app` inside `input/`, then run
these commands from the project folder:

```sh
python3 tools/sdt.py doctor
python3 tools/sdt.py inspect --input input/SonicDreamTeam.app
python3 tools/sdt.py bootstrap
python3 tools/sdt.py recover-code --input input/SonicDreamTeam.app
python3 tools/sdt.py extract-assets --input input/SonicDreamTeam.app
python3 tools/sdt.py prepare
```

The tools keep your supplied bundle unchanged. Downloads and generated files
are stored locally and are not part of the repository.

Use Unity **2022.3.54f1** to open this project. Follow the current
[setup and recovery guide](docs/setup.md) for prerequisites and troubleshooting.

## Building

Once reconstruction validation passes:

```sh
python3 tools/sdt.py validate
python3 tools/sdt.py build --target macos
```

Use `windows` or `linux` for the other desktop targets. Builds are written to
`Builds/`. Building is blocked while required game behavior remains unresolved.

The intended game runs offline with local saves, achievements, and personal
records. Original cloud saves and global rankings are not included.

# Setup and recovery

Use Python 3.9+ and Unity 2022.3.54f1. The currently supported extraction host is
macOS; recovered source and rebuilt content target macOS, Windows, and Linux.

Place a macOS `SonicDreamTeam.app` in `input/`. Run commands from the repository
root. The supplied bundle remains unchanged. Inspection reads its metadata and
hashes files; subsequent operations write only to generated directories.

`bootstrap` installs pinned recovery tools into `.cache/project-lucid/tools/`.
`recover-code` defaults to declarations and serialization schemas. Experimental
body recovery is a separate mode and must be audited before its output is used.
`extract-assets` exports to a new staging directory, verifies it, and records a
manifest. `prepare` promotes generated content without overwriting maintained
source. None of these steps alone restores playable game behavior.

Preparation requires the matching Unity Editor to check restored script bindings.
Install the Editor and recover the code schemas before running `prepare`.

Run `python3 tools/lucid.py --help` for commands and options. `--work-dir` is limited
to generated subdirectories of `.cache/project-lucid/`. Reports and full tool logs
are stored there; failed operations return a nonzero exit status.

Unity Editor installation and build modules are separate from tool bootstrap.
With the Unity CLI installed, use:

```sh
unity install 2022.3.54f1 --architecture arm64 --yes --accept-eula
```

Choose x86_64 on an Intel Mac. Install Windows/Linux Mono build support before
cross-building those targets. A valid Unity license is required for Editor work.

```sh
unity install-modules --editor-version 2022.3.54f1 --module windows-mono linux-mono --yes --accept-eula
"/Applications/Unity/Hub/Editor/2022.3.54f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" \
  -executeMethod ProjectLucid.Editor.PackageInstaller.Install
```

Adjust the executable path if the Editor is installed elsewhere. The package
installer owns Editor shutdown after the asynchronous request; omit `-quit`.
The ARM64 Editor embeds an Intel Package Manager server; Apple Silicon machines
need Rosetta 2. A Package Manager startup crash or “bad CPU type” from that server
can indicate a missing Rosetta installation.

The exported WAV samples are preserved while preparation repairs the zero-length
RIFF/data headers produced by AssetRipper 2.0.0. Raw exports remain in the cache.

The export and prepared project need substantially more disk space than the game
bundle. Release 1.10.1 produced about 21 GB of exports before preparation, which
makes an additional copy.

The tools can run Unity checks and build desktop executables after extraction and
preparation. Once the game implementation is complete, run:

```sh
python3 tools/lucid.py audit
python3 tools/lucid.py test --mode editmode
python3 tools/lucid.py test --mode playmode
python3 tools/lucid.py validate
python3 tools/lucid.py build --target macos
```

Use `windows` or `linux` to select the other build targets. Outputs go into
`Builds/`. The CLI invokes the installed matching Editor directly. Set
`LUCID_UNITY_EDITOR` to its executable path if you use a custom installation.
Close other Editors using the project before running these commands.

To check standalone source compilation before the complete game is ready, run
`python3 tools/lucid.py player-code --target macos` (or `windows` or `linux`).
This records compiled assemblies in the cache without packaging a game. Passing
this check does not establish working assets, startup, or gameplay.

Validation uses generated asset audits and test reports. Changing code or assets
requires fresh checks. The current implementation has passing subsystem tests;
full startup and gameplay checks are still being restored, so release builds
remain blocked. An import that generates metadata during testing requires a
second test run against the imported project.

If a download or extraction fails, inspect its report/log and rerun the command.
Each export uses a fresh staging directory, and failed runs retain earlier valid
content. Do not point an exporter at the repository root or your input bundle.

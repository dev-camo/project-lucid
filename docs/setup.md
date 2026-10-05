# Setup and recovery

Use Python 3.9+ and Unity 2022.3.54f1. The currently supported extraction host is
macOS; recovered source and rebuilt content target macOS, Windows, and Linux.

Place a macOS `SonicDreamTeam.app` in `input/`. Run commands from the repository
root. The supplied bundle remains unchanged. Inspection reads its metadata and
hashes files; subsequent operations write only to generated directories.

`bootstrap` installs pinned recovery tools into `.cache/sdt-recovery/tools/`.
`recover-code` defaults to declarations and serialization schemas. Experimental
body recovery is a separate mode and must be audited before its output is used.
`extract-assets` exports to a new staging directory, verifies it, and records a
manifest. `prepare` promotes generated content without overwriting maintained
source. None of these steps alone restores playable game behavior.

Run `python3 tools/sdt.py --help` for commands and options. `--work-dir` is limited
to generated subdirectories of `.cache/sdt-recovery/`. Reports and full tool logs
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
  -executeMethod SDT.Reconstruction.Editor.PackageInstaller.Install
```

Adjust the executable path if the Editor is installed elsewhere. The package
installer owns Editor shutdown after the asynchronous request; omit `-quit`.
The ARM64 Editor embeds an Intel Package Manager server; Apple Silicon machines
need Rosetta 2. A Package Manager startup crash or “bad CPU type” from that server
can indicate a missing Rosetta installation.

The exported WAV samples are preserved while preparation repairs the zero-length
RIFF/data headers produced by AssetRipper 2.0.0. Raw exports remain in the cache.

If a download or extraction fails, inspect its report/log and rerun the command.
Each export uses a fresh staging directory, and failed runs retain earlier valid
content. Do not point an exporter at the repository root or your input bundle.

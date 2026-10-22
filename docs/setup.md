# Setup and recovery

Use Python 3.9+ and Unity 2022.3.54f1. Recovery-tool downloads are pinned for
macOS ARM64, Windows AMD64, and Linux x86_64. Linux source compilation and code
progress regeneration have been exercised with the matching licensed Editor;
complete Linux asset extraction and gameplay remain unqualified. Recovered
source and rebuilt content target macOS, Windows, and Linux.

Place a macOS `SonicDreamTeam.app` in `input/`. Run commands from the repository
root. The supplied bundle remains unchanged. Inspection reads its metadata and
hashes files; subsequent operations write only to generated directories.

On Windows, some paths inside the bundle are close to 280 characters, longer than
the classic 260-character limit. Extract the bundle into a short folder such as
`C:\sdt` with a tool that supports long paths (7-Zip, or `tar -xf` in a terminal);
Explorer's "Extract All" stops at the first path over the limit and leaves the
rest of the bundle missing. Also enable Windows long paths (the registry value
`HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled` set to 1, or
the Group Policy "Enable Win32 long paths") so the tools can read and copy those
files. `doctor` reports the setting and prints the fix; it never changes the
registry. `inspect` explains a partial extraction and refuses to run on Windows
when long paths are disabled and the input has paths over the limit.

The recovery tools inspect and slice universal Mach-O binaries directly in
Python. `doctor` reports whether `lipo` is present, but recovery does not require
installing it on Linux or Windows.

`bootstrap` installs pinned recovery tools into `.cache/project-lucid/tools/`.
`recover-code` defaults to declarations and serialization schemas. Experimental
body recovery is a separate mode and must be audited before its output is used.
`extract-assets` exports to a new staging directory, verifies it, and records a
manifest. `prepare` promotes generated content without overwriting maintained
source. None of these steps alone restores playable game behavior.

`fingerprint-input <app> [--output file]` writes a read-only JSON fingerprint of a
bundle (file manifest, Mach-O sections, UUID and signature ranges, metadata tables).
`compare-input-fingerprints <a.json> <b.json>` lists where two bundles differ and
whether the differences are packaging only; the result is evidence for review and
qualifies nothing by itself.

Preparation requires the matching Unity Editor to check restored script bindings.
Install the Editor, then run `recover-code` and `native-schema` before `prepare`.
They write different outputs. `recover-code` writes declarations and serialization schemas
(`reports/code-recovery-schemas.json`). `native-schema` writes the original native serialization
evidence (`native-recovery/latest-schemas.json`, updated only by a run that reports `ready` in
`reports/native-schemas.json`), which the script-binding layout proof in `prepare` reads; when
that proof applies and the file is missing, `prepare` fails before Unity starts. On first use `native-schema`
bootstraps a pinned .NET SDK and builds its harness inside the cache. Neither step shows that
the assets are complete or that the game is playable.

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

For asset-import diagnostics, `python3 tools/lucid.py player-schema --target macos`
inspects a bounded group of serialized types from the compiled player assemblies.
Run `player-code` for the same target first, and rerun it after changing source.
The resulting metadata report is stored in the cache; it does not approve asset
bindings or establish that the game is playable. Other targets use `windows` or
`linux`.

For the current theme import diagnostics, `runtime-contracts --target macos`
reads exact contract metadata from the installed Editor and compiled player modules.
Run `native-schema` and use its `evidence_report` path as `--schema PATH`.
Run `script-layouts --schema PATH` and use its `inventory_path` as
`--inventory PATH`. First run
`player-code` and `player-schema` for each desktop target, then `runtime-contracts`
for `macos`, `windows`, and `linux`. `theme-evidence` accepts the same schema and
inventory paths and rechecks all three targets. These checks use Unity 2022.3.54f1
on macOS and store reports in the cache. They establish compatibility evidence;
asset bindings still require separate import and roundtrip validation.

The README's code progress is measured with `python3 tools/lucid.py progress`,
which needs the supplied bundle and a current `player-code` result for the
target. It rewrites the README table and `docs/code-progress.json` locally for
inspection only; do not commit them. The Refresh native progress workflow
publishes both for the current main head after merges, and detailed method
inventories remain in the local cache. Use `python3 tools/lucid.py progress
--check` to check the published table without the game bundle, Unity, or
downloads.

`git config core.hooksPath tools/hooks` installs a pre-commit check that runs
`python3 tools/lucid.py progress --stage`. It requires the staged index to match
every source file on disk under `Assets`, `Packages`, `ProjectSettings`, and
`tools`, excluding the generated `Assets/Recovered` and `Assets/StreamingAssets`,
so stage new files and remove untracked or ignored local files there before
committing. It rejects staged changes to `docs/code-progress.json` and the
generated README progress section, requires that section to match the rendering
of the published snapshot, and checks the snapshot's public counts. It
needs no bundle, Unity, or `player-code` result, and it does not compile,
measure, or refresh progress.
Automatic pushing is opt-in: `git config lucid.pushOnCommit true` pushes each
successful commit to `origin`.

Passing the hook proves nothing about compilation or behaviour. Compilation is
established by `player-code` locally and by the pull-request compilation check;
behavioural evidence comes only from the test reports described below.

Validation uses generated asset audits and test reports. Changing code or assets
requires fresh checks. The current implementation has passing subsystem tests;
full startup and gameplay checks are still being restored, so release builds
remain blocked. An import that generates metadata during testing requires a
second test run against the imported project.

If a download or extraction fails, inspect its report/log and rerun the command.
Each export uses a fresh staging directory, and failed runs retain earlier valid
content. Do not point an exporter at the repository root or your input bundle.

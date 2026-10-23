"""Project validation and guarded portable builds."""

import json
from pathlib import Path
import platform
import plistlib
import shutil
import subprocess
import sys
from . import verification
from .verification import artifact_fingerprint, current_identity, find_editor, verified_receipt
from .bootstrap import managed_path, write_json
from .scriptmetadata import validate_script_metadata
from .macho import inspect_macho
from .longpaths import ENABLE_HELP, long_paths_status

UNITY_VERSION = "2022.3.54f1"
# Public fields of LucidBuild.BuildRecord, as JsonUtility writes them.
BUILD_RECORD_FIELDS = ("target", "output", "architecture")
# The original SonicDreamTeam.app executable, UnityPlayer.dylib and
# GameAssembly.dylib are each universal x86_64 + arm64 Mach-O files.
MAC_SLICES = ("arm64", "x86_64")
# Development builds (owner decision D1, 2026-10-22) are never releases. They go
# under Builds/Development/<target>/, carry this marker and a labelled name, and
# neither `port` nor release validation accepts anything that carries the marker.
DEVELOPMENT_MARKER = "DEVELOPMENT-BUILD.txt"
DEVELOPMENT_OUTPUTS = {"macos": "ProjectLucid-DEVELOPMENT.app", "windows": "ProjectLucid-DEVELOPMENT.exe",
                       "linux": "ProjectLucid-DEVELOPMENT.x86_64"}


def doctor(repo_root, work_dir):
    tools = {name: shutil.which(name) for name in ("unity", "git", "lipo")}
    editors = []
    errors = []
    if tools["unity"]:
        process = subprocess.run([tools["unity"], "editors", "--installed", "--format", "json"],
                                 capture_output=True, text=True, timeout=60)
        try:
            data = json.loads(process.stdout)
            editors = data.get("data") or []
        except ValueError:
            errors.append("Unity CLI did not return an installed Editor inventory.")
    long_paths = long_paths_status()
    if long_paths == "disabled":
        errors.append("Windows long paths are disabled, but the game bundle contains paths over 260 characters. "
                      + ENABLE_HELP)
    elif long_paths == "unreadable":
        errors.append("Could not read the Windows long-path setting (LongPathsEnabled); if inspection reports "
                      "missing or unreadable files, check it. " + ENABLE_HELP)
    return {"status": "complete", "python": sys.version.split()[0], "host": platform.system(),
            "architecture": platform.machine(), "tools": tools, "editors": editors, "long_paths": long_paths,
            "required_editor": UNITY_VERSION, "work_dir": str(work_dir), "errors": errors,
            "message": "Inspection uses Python only; extraction requires bootstrapped tools, and project work requires the matching Editor."}


def _read_json(path, errors, description):
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(value, dict):
            raise ValueError("expected a JSON object")
        return value
    except (OSError, ValueError) as error:
        errors.append("{} unavailable: {}".format(description, error))
        return {}


def validate_project(repo_root, work_dir, stage="release"):
    repo_root = Path(repo_root)
    work_dir = Path(work_dir)
    if stage not in ("extraction", "release", "development"):
        raise ValueError("Validation stage must be extraction, release or development")
    errors = []
    inspection = _read_json(work_dir / "reports" / "inspection.json", errors, "Input inspection")
    missing = inspection.get("addressables", {}).get("missing_bundles", [])
    if missing:
        errors.append("{} catalog bundles missing from input".format(len(missing)))
    if inspection.get("addressables", {}).get("complete") is not True:
        errors.append("Input catalog completeness has not been established")
    assets = _read_json(work_dir / "assets" / "latest-assets.json", errors, "Asset export")
    if assets.get("status") != "exported":
        errors.append("Asset export has not completed successfully")
    export_path = assets.get("project_path")
    if export_path:
        resolved = Path(export_path).resolve()
        if work_dir.resolve() not in resolved.parents:
            errors.append("Recorded asset export lies outside the generated work directory")
        elif not (resolved / "Assets").is_dir():
            errors.append("Asset export directory no longer exists")
    if not export_path:
        errors.append("No validated asset export is recorded")
    try:
        from .assets import _validate_export_identity
        _validate_export_identity(assets)
    except (ValueError, OSError, KeyError) as error:
        errors.append("Asset export provenance invalid: " + str(error))
    mapping = {}
    map_path = assets.get("asset_map_path")
    if not map_path or work_dir.resolve() not in Path(map_path).resolve().parents:
        errors.append("No generated asset identity map is recorded")
    else:
        mapping = _read_json(Path(map_path), errors, "Asset identity map")
        if not mapping.get("assets"):
            errors.append("Asset identity map contains no assets")
        if mapping.get("duplicate_guids"):
            errors.append("Export contains duplicate asset GUIDs")
        catalog_hash = inspection.get("addressables", {}).get("catalog_sha256")
        if not catalog_hash or mapping.get("catalog_sha256") != catalog_hash:
            errors.append("Asset map and inspected catalog differ")
    identity = {}
    checks = {}
    if stage in ("release", "development"):
        # A development build keeps the input, asset-identity and prepare-state
        # checks; only the audit and the EditMode/PlayMode gates are skipped.
        checks["script_metadata"] = validate_script_metadata(repo_root)
        errors.extend(checks["script_metadata"]["errors"])
        recovered = repo_root / "Assets" / "Recovered"
        if not recovered.is_dir():
            errors.append("Extracted content has not been prepared in the Unity project")
        else:
            # The prepare receipt ties the project's content to the recorded export (#453).
            prepared = _read_json(work_dir / "assets" / "latest-prepare.json", errors, "Prepare receipt")
            if prepared.get("status") != "prepared" or not prepared.get("run_id") \
                    or prepared.get("run_id") != assets.get("run_id"):
                errors.append("Prepared content does not come from the recorded asset export")
            elif not any(path.name != ".lucid-owner.json" for path in recovered.iterdir()):
                errors.append("Prepared content is empty")
        version_path = repo_root / "ProjectSettings" / "ProjectVersion.txt"
        if not version_path.exists() or "m_EditorVersion: " + UNITY_VERSION not in version_path.read_text():
            errors.append("Unity project version is absent or differs from the original")
        if not (repo_root / "Packages" / "packages-lock.json").is_file():
            errors.append("Unity dependencies have not been resolved and locked")
        audit = {}
        if stage == "release":
            audit = _read_json(work_dir / "reports" / "unity-reference-audit.json", errors, "Unity reference audit")
            if audit.get("status") != "complete" or audit.get("unresolved_references", 1) != 0:
                errors.append("Required asset and script references remain unresolved")
        try:
            identity = current_identity(repo_root)
            if stage == "release":
                if audit.get("unity_version") != UNITY_VERSION or any(audit.get(key) != value for key, value in identity.items()):
                    errors.append("Unity reference audit is stale for the current project")
                for mode in ("editmode", "playmode"):
                    try:
                        checks[mode] = verified_receipt(work_dir, mode, identity)
                    except (ValueError, OSError) as error:
                        errors.append("Required " + mode + " verification unavailable: " + str(error))
        except (ValueError, OSError) as error:
            errors.append("Project identity could not be verified: " + str(error))
    return {"schema_version": 1, "generated_by": "ProjectLucid.validate_project",
            "status": "failed" if errors else "complete", "stage": stage, "errors": errors,
            "automated_checks": checks, **identity,
            "asset_inventory": mapping.get("summary", {}),
            "message": "Extraction readiness and verified gameplay are separate results."}


def is_development_output(path):
    """True when path is, holds or sits inside a labelled development build."""
    path = Path(path)
    if path.name in DEVELOPMENT_OUTPUTS.values() or path.name == DEVELOPMENT_MARKER:
        return True
    try:
        if path.is_dir() and any(True for _ in path.rglob(DEVELOPMENT_MARKER)):
            return True
    except OSError:
        return True
    return any((parent / DEVELOPMENT_MARKER).exists() or (parent.name == "Development" and parent.parent.name == "Builds")
               for parent in path.parents)


# What a release requires and a development build does not check at all.
SKIPPED_RELEASE_CHECKS = ("EditMode verification receipt: no test result was checked",
                          "PlayMode verification receipt: no test result was checked",
                          "unity-reference-audit (unresolved_references == 0)")


def _unwritten_gates():
    """The six whole-game PlayMode gates, which do not exist yet."""
    return list(verification.PLAYMODE_TESTS[:6])


def _commit(repo_root):
    def run(*args):
        return subprocess.run(["git", "-C", str(repo_root), *args], capture_output=True, text=True, timeout=60).stdout.strip()
    try:
        commit = run("rev-parse", "HEAD") or "unknown"
        return commit + (" (uncommitted changes present)" if run("status", "--porcelain") else "")
    except (OSError, subprocess.SubprocessError):
        return "unknown"


def _development_marker(repo_root, target):
    return "\n".join((
        "DEVELOPMENT BUILD - NOT A RELEASE",
        "",
        "This player was produced by `tools/lucid.py build --development` (owner decision D1).",
        "It is unvalidated, is not a faithful or complete game, must not be published or",
        "distributed, and is never accepted by `lucid.py port` or release validation.",
        "",
        "target: " + target,
        "commit: " + _commit(repo_root),
        "unity build options: BuildOptions.Development",
        "",
        "Release checks skipped for this build:",
        *("  - " + check for check in SKIPPED_RELEASE_CHECKS),
        "",
        "Whole-game gates not written yet:",
        *("  - " + gate for gate in _unwritten_gates()),
        "")) + "\n"


def build_project(repo_root, work_dir, target, *, destination=None, development=False):
    if target not in ("macos", "windows", "linux"):
        raise ValueError("Build target must be macos, windows, or linux")
    if development and destination is not None:
        raise ValueError("Development builds always use Builds/Development/<target>; port never builds them")
    if destination is not None and is_development_output(destination):
        raise ValueError("A development build is never accepted as a release output")
    development_root = Path(repo_root) / "Builds" / "Development" / target
    if development:
        if development_root.is_symlink() or development_root.parent.is_symlink() or development_root.parent.parent.is_symlink():
            raise ValueError("Development build directory cannot be a symbolic link")
        # Clear the previous attempt first, so a blocked attempt never leaves an older player at the documented path.
        shutil.rmtree(development_root, ignore_errors=True)
    validation = validate_project(repo_root, work_dir, "development" if development else "release")
    if validation["status"] != "complete":
        return {"status": "blocked", "target": target, "errors": validation["errors"],
                "message": ("Development" if development else "Release") + " build blocked: required reconstruction is incomplete."}
    try:
        executable = find_editor()
    except ValueError as error:
        return {"status": "blocked", "errors": [str(error)]}
    receipt = managed_path(work_dir, "reports", "development-validation.json" if development else "release-validation.json")
    write_json(receipt, validation)
    outputs = {"macos": "macOS/ProjectLucid.app", "windows": "Windows/ProjectLucid.exe",
               "linux": "Linux/ProjectLucid.x86_64"}
    marker = None
    if development:
        development_root.mkdir(parents=True)
        destination = development_root / DEVELOPMENT_OUTPUTS[target]
        marker = development_root / DEVELOPMENT_MARKER
        marker.write_text(_development_marker(repo_root, target), encoding="utf-8")
    elif destination is None:
        destination = Path(repo_root) / "Builds" / outputs[target]
    else:
        from .port import _physical, _overlap, OUTPUTS
        destination = _physical(destination)
        root = _physical(repo_root)
        protected = [root / name for name in ("input", "Assets", "Packages", "ProjectSettings", "tools", ".cache", "Library", "Temp")]
        if destination.name != OUTPUTS[target] or any(_overlap(destination.parent, path) for path in protected):
            raise ValueError("Custom build destination overlaps protected project content")
        if destination.exists() or destination.is_symlink() or not destination.parent.is_dir() or any(destination.parent.iterdir()):
            raise ValueError("Custom build output requires a fresh physical staging directory")
    destination.parent.mkdir(parents=True, exist_ok=True)
    log = managed_path(work_dir, "reports", "build-" + target + ".log")
    record = managed_path(work_dir, "reports", "build-" + target + ".json")
    record.unlink(missing_ok=True)
    source = artifact_fingerprint(repo_root)
    command = [str(executable), "-batchmode", "-quit", "-projectPath", str(repo_root),
               "-logFile", str(log), "-executeMethod", "ProjectLucid.Editor.LucidBuild.Build",
               "-lucidTarget", target, "-lucidOutput", str(destination), "-lucidValidation", str(receipt),
               "-lucidBuildRecord", str(record)]
    if development:
        command.append("-lucidDevelopment")
    process = subprocess.run(command, timeout=1800)
    errors = []
    if artifact_fingerprint(repo_root) != source:
        errors.append("The build changed maintained source after release validation")
    try:
        built = json.loads(record.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        built = None
    if not isinstance(built, dict) or sorted(built) != sorted(BUILD_RECORD_FIELDS) or \
            not all(isinstance(value, str) for value in built.values()):
        built = {}
    if built.get("target") != target or built.get("output") != str(destination):
        errors.append("The build record does not identify this target and destination")
    slices = None
    if target == "macos" and destination.exists():
        slices, problems = _macos_slices(destination)
        errors += problems
    success = process.returncode == 0 and destination.exists() and not errors
    return {"status": "complete" if success else "failed", "target": target,
            "output_dir": str(destination), "log": str(log), "exit_code": process.returncode,
            "architecture_setting": built.get("architecture"), "slices": slices,
            "source_fingerprint": source, "errors": errors,
            **({"development": True, "marker": str(marker),
                "message": "DEVELOPMENT BUILD, not a release: never publish it; port and release validation refuse it."}
               if development else {})}


def _macos_slices(app):
    """Read the CPU slices of the built player's executable and Unity dylibs."""
    contents = Path(app) / "Contents"
    try:
        with (contents / "Info.plist").open("rb") as stream:
            executable = plistlib.load(stream)["CFBundleExecutable"]
    except (OSError, ValueError, KeyError, TypeError, plistlib.InvalidFileException):
        executable = None
    if not isinstance(executable, str) or not executable or Path(executable).name != executable:
        return None, ["The macOS build has no readable CFBundleExecutable"]
    binaries = [contents / "MacOS" / executable] + [
        path for path in (contents / "Frameworks" / name for name in ("UnityPlayer.dylib", "GameAssembly.dylib"))
        if path.exists()]
    slices, problems = {}, []
    for binary in binaries:
        relative = binary.relative_to(contents).as_posix()
        try:
            names = sorted(row["name"] for row in inspect_macho(binary)["architectures"])
        except ValueError as error:
            problems.append("The macOS build binary " + relative + " is not readable Mach-O: " + str(error))
            continue
        slices[relative] = names
        if names != list(MAC_SLICES):
            problems.append("The macOS build binary " + relative + " has slices " + ", ".join(names) +
                            ", not " + ", ".join(MAC_SLICES))
    return slices, problems

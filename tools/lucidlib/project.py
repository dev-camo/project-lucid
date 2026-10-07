"""Project validation and guarded portable builds."""

import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys
from .verification import current_identity, find_editor, verified_receipt
from .bootstrap import managed_path, write_json

UNITY_VERSION = "2022.3.54f1"


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
    return {"status": "complete", "python": sys.version.split()[0], "host": platform.system(),
            "architecture": platform.machine(), "tools": tools, "editors": editors,
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
    if stage not in ("extraction", "release"):
        raise ValueError("Validation stage must be extraction or release")
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
    if stage == "release":
        if not (repo_root / "Assets" / "Recovered").is_dir():
            errors.append("Extracted content has not been prepared in the Unity project")
        version_path = repo_root / "ProjectSettings" / "ProjectVersion.txt"
        if not version_path.exists() or "m_EditorVersion: " + UNITY_VERSION not in version_path.read_text():
            errors.append("Unity project version is absent or differs from the original")
        if not (repo_root / "Packages" / "packages-lock.json").is_file():
            errors.append("Unity dependencies have not been resolved and locked")
        audit = _read_json(work_dir / "reports" / "unity-reference-audit.json", errors, "Unity reference audit")
        if audit.get("status") != "complete" or audit.get("unresolved_references", 1) != 0:
            errors.append("Required asset and script references remain unresolved")
        try:
            identity = current_identity(repo_root)
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


def build_project(repo_root, work_dir, target, *, destination=None):
    if target not in ("macos", "windows", "linux"):
        raise ValueError("Build target must be macos, windows, or linux")
    validation = validate_project(repo_root, work_dir)
    if validation["status"] != "complete":
        return {"status": "blocked", "target": target, "errors": validation["errors"],
                "message": "Release build blocked: required reconstruction is incomplete."}
    try:
        executable = find_editor()
    except ValueError as error:
        return {"status": "blocked", "errors": [str(error)]}
    receipt = managed_path(work_dir, "reports", "release-validation.json")
    write_json(receipt, validation)
    outputs = {"macos": "macOS/ProjectLucid.app", "windows": "Windows/ProjectLucid.exe",
               "linux": "Linux/ProjectLucid.x86_64"}
    if destination is None:
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
    command = [str(executable), "-batchmode", "-quit", "-projectPath", str(repo_root),
               "-logFile", str(log), "-executeMethod", "ProjectLucid.Editor.LucidBuild.Build",
               "-lucidTarget", target, "-lucidOutput", str(destination), "-lucidValidation", str(receipt)]
    process = subprocess.run(command, timeout=1800)
    success = process.returncode == 0 and destination.exists()
    return {"status": "complete" if success else "failed", "target": target,
            "output_dir": str(destination), "log": str(log), "exit_code": process.returncode}

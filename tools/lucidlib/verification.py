"""Generate test receipts and compare them with the current project content."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import xml.etree.ElementTree as ET

from .bootstrap import managed_path, write_json

UNITY_VERSION = "2022.3.54f1"
EDITMODE_TESTS = tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "MeshGeometryMatchesNativeStoresAndIndexBlob",
    "StateMachinePrimitivesMatchNativeHashAndIdentitySemantics",
    "StateMachineTransitionsPreserveNativeUpdateAndCallbackOrder",
    "LocalStoragePersistsAndRecoversWithoutCloudNotifications",
    "CloudFacadePreservesOriginalCallbacksKeysAndConflictLifecycle",
    "SavePropertiesPreserveNativeIdentityAndCultureSemantics",
    "PropertyStoreRetainsNativeLifecycleAndRecoversLocalFiles",
    "SaveDataRetainsOriginalDefaultsDirtyChildrenAndJson",
    "ScriptPointersRequireExactLoadedAssetAndComponentIdentities",
    "OriginalPipelineMarkerRetainsEngineFieldsAfterScriptBinding",
    "TimeConversionsRetainOriginalUnitsRoundingAndDateKinds",
    "SkyCubemapGpuOutputMatchesDecodedMetalCases",
    "EggmanLogoGpuOutputMatchesDecodedColorBlendAndLayerCases"))
PLAYMODE_TESTS = (
    "ProjectLucid.Tests.OfflineStartupTests.ReachesOriginalMainMenu",
    "ProjectLucid.Tests.LocalSaveSlotTests.CreateCopyDeleteAndRestart",
    "ProjectLucid.Tests.OriginalFirstActTests.CompletesOriginalFirstAct",
    "ProjectLucid.Tests.ContentParityTests.AllShippedContentHasVerifiedPlaythroughs",
    "ProjectLucid.Tests.DesktopServiceTests.PlatformCallbacksCompleteOffline",
    "ProjectLucid.Tests.ControlAndCameraTests.OriginalMovementAndCameraMatchReference")


def artifact_fingerprint(repo_root, prepared=False):
    """Canonical path/content hash, shared with LucidArtifactIdentity in Unity."""
    root = Path(repo_root).resolve()
    bases = ("Assets/Recovered", "Assets/StreamingAssets") if prepared else (
        "Assets", "Packages", "ProjectSettings", "tools")
    files = []
    for base in bases:
        directory = root / base
        if directory.is_symlink():
            raise ValueError("Refusing a symlinked project directory: " + str(directory))
        if not directory.is_dir():
            continue
        for parent, directories, names in os.walk(directory):
            for name in directories:
                if (Path(parent) / name).is_symlink():
                    raise ValueError("Refusing a symlinked project directory: " + str(Path(parent) / name))
            directories[:] = [name for name in directories if name not in ("obj", "bin", "__pycache__")
                              and not name.startswith(".")]
            if not prepared and Path(parent) == root / "Assets":
                directories[:] = [name for name in directories if name not in ("Recovered", "StreamingAssets")]
                names = [name for name in names if name not in ("Recovered.meta", "StreamingAssets.meta")]
            for name in names:
                path = Path(parent) / name
                if path.is_symlink():
                    raise ValueError("Refusing a symlinked project file: " + str(path))
                if name.endswith((".pyc", ".pyo", ".tmp")) or name == ".DS_Store":
                    continue
                files.append((path.relative_to(root).as_posix(), path))
    result = hashlib.sha256()
    for relative, path in sorted(files):
        content = hashlib.sha256()
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                content.update(chunk)
        result.update((relative + "\0" + content.hexdigest() + "\n").encode("utf-8"))
    return result.hexdigest()


def current_identity(repo_root):
    return {"source_fingerprint": artifact_fingerprint(repo_root),
            "prepared_asset_fingerprint": artifact_fingerprint(repo_root, prepared=True)}


def find_editor():
    explicit = os.environ.get("LUCID_UNITY_EDITOR")
    if explicit:
        candidate = Path(explicit).expanduser()
        if not candidate.is_file():
            raise ValueError("LUCID_UNITY_EDITOR does not identify an Editor executable")
        return candidate
    system = platform.system()
    if system == "Darwin":
        candidate = Path("/Applications/Unity/Hub/Editor") / UNITY_VERSION / "Unity.app/Contents/MacOS/Unity"
    elif system == "Windows":
        candidate = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Unity/Hub/Editor" / UNITY_VERSION / "Editor/Unity.exe"
    else:
        candidate = Path.home() / "Unity/Hub/Editor" / UNITY_VERSION / "Editor/Unity"
    if not candidate.is_file():
        raise ValueError("Install Unity " + UNITY_VERSION + "; set LUCID_UNITY_EDITOR for a custom installation path")
    return candidate


def test_verdict(path, required):
    path = Path(path)
    if not path.is_file() or path.stat().st_size > 8 * 1024 * 1024:
        raise ValueError("Unity test XML is absent or exceeds the supported size")
    data = path.read_bytes()
    if b"<!DOCTYPE" in data.upper() or b"<!ENTITY" in data.upper():
        raise ValueError("Unsupported declarations in Unity test XML")
    try:
        root = ET.fromstring(data)
    except ET.ParseError as error:
        raise ValueError("Invalid Unity test XML: " + str(error)) from error
    passed = {test.get("fullname") for test in root.iter("test-case") if test.get("result") == "Passed"}
    missing = sorted(set(required) - passed)
    success = root.tag == "test-run" and root.get("result") == "Passed" and root.get("failed") == "0" and not missing
    return {"status": "complete" if success else "incomplete", "result": root.get("result"),
            "passed": len(passed), "missing_required_tests": missing,
            "xml_sha256": hashlib.sha256(data).hexdigest()}


def run_tests(repo_root, work_dir, mode):
    if mode not in ("editmode", "playmode"):
        raise ValueError("Test mode must be editmode or playmode")
    editor = find_editor()
    before = current_identity(repo_root)
    xml = managed_path(work_dir, "reports", mode + ".xml")
    log = managed_path(work_dir, "reports", mode + ".log")
    xml.parent.mkdir(parents=True, exist_ok=True)
    # A failed Editor invocation must never accidentally reuse a passing result.
    xml.unlink(missing_ok=True)
    log.unlink(missing_ok=True)
    command = [str(editor), "-batchmode", "-projectPath", str(Path(repo_root).resolve()),
               "-runTests", "-testPlatform", "EditMode" if mode == "editmode" else "PlayMode",
               "-testResults", str(xml), "-logFile", str(log)]
    result = subprocess.run(command, timeout=1800)
    after = current_identity(repo_root)
    errors = []
    version_match = re.search(r"Unity Editor version:\s+(\S+)", log.read_text(errors="replace")) if log.is_file() else None
    actual_version = version_match.group(1) if version_match else None
    if actual_version != UNITY_VERSION:
        errors.append("Test Editor version is absent or differs from " + UNITY_VERSION)
    if result.returncode:
        errors.append("Unity test process exited " + str(result.returncode))
    if before != after:
        errors.append("Project content changed during testing; rerun with the imported project")
    try:
        verdict = test_verdict(xml, EDITMODE_TESTS if mode == "editmode" else PLAYMODE_TESTS)
        if verdict["missing_required_tests"]:
            errors.append("Missing required tests: " + ", ".join(verdict["missing_required_tests"]))
    except ValueError as error:
        errors.append(str(error))
        verdict = {"status": "incomplete"}
    report = {"schema_version": 1, "generated_by": "ProjectLucid.run_tests", "mode": mode,
              "status": "complete" if not errors and verdict["status"] == "complete" else "incomplete",
              "unity_version": actual_version, "editor": str(editor), "exit_code": result.returncode,
              "xml": str(xml), "log": str(log), "verdict": verdict, "errors": errors, **after}
    write_json(managed_path(work_dir, "reports", mode + "-verification.json"), report)
    return report


def verified_receipt(work_dir, mode, identity):
    path = Path(work_dir) / "reports" / (mode + "-verification.json")
    receipt = json.loads(path.read_text())
    if not isinstance(receipt, dict):
        raise ValueError(mode + " test receipt is not a JSON object")
    if receipt.get("generated_by") != "ProjectLucid.run_tests" or receipt.get("status") != "complete":
        raise ValueError(mode + " has no successful generated test receipt")
    if receipt.get("unity_version") != UNITY_VERSION or any(receipt.get(key) != value for key, value in identity.items()):
        raise ValueError(mode + " test receipt is stale for the current project")
    xml = Path(work_dir) / "reports" / (mode + ".xml")
    verdict = test_verdict(xml, EDITMODE_TESTS if mode == "editmode" else PLAYMODE_TESTS)
    if verdict["status"] != "complete" or verdict["xml_sha256"] != receipt.get("verdict", {}).get("xml_sha256"):
        raise ValueError(mode + " test result is incomplete or differs from its generated receipt")
    return verdict


def run_audit(repo_root, work_dir):
    editor = find_editor()
    output = managed_path(work_dir, "reports", "unity-reference-audit.json")
    log = managed_path(work_dir, "reports", "unity-reference-audit.log")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.unlink(missing_ok=True)
    command = [str(editor), "-batchmode", "-quit", "-projectPath", str(Path(repo_root).resolve()),
               "-executeMethod", "ProjectLucid.Editor.LucidReferenceAudit.Run",
               "-lucidAuditOutput", str(output), "-logFile", str(log)]
    result = subprocess.run(command, timeout=1800)
    if result.returncode or not output.is_file():
        return {"status": "failed", "exit_code": result.returncode, "log": str(log),
                "errors": ["Unity reference audit did not complete"]}
    report = json.loads(output.read_text())
    identity = current_identity(repo_root)
    if report.get("unity_version") != UNITY_VERSION or any(report.get(key) != value for key, value in identity.items()):
        raise ValueError("Unity reference audit does not match the current project content")
    return {"status": report["status"], "unresolved_references": report["unresolved_references"],
            "scanned_assets": report["scanned_assets"], "report_path": str(output), "log": str(log)}

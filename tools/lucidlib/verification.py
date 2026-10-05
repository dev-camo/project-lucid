"""Generate test receipts and compare them with the current project content."""

from __future__ import annotations

import base64
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import uuid
import xml.etree.ElementTree as ET

from .bootstrap import managed_path, write_json

UNITY_VERSION = "2022.3.54f1"
EDITMODE_TESTS = tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "OriginalBindableRetainsNativeNotificationBindingAndComparisonOrder",
    "OriginalProgressionWidgetRetainsRealBaseFieldsAndCallbacks",
    "OriginalProgressionWidgetRetainsGenuineUnitySerializationAndRuntimeFields",
    "OriginalManagedAssetRetainsNativeClosureAndReferenceCounting",
    "OriginalManagedAssetUsesGenuineEngineCompletionAndRelease",
    "OriginalInspectorConditionsRetainAttributesAndComparisonOrder",
    "OriginalZoneThemeRetainsUnityLifecycleAndAssignmentOrder",
    "OriginalRewardRequirementRetainsFieldAndJsonIdentity",
    "ReadOnlyListsPreserveNativeComparisonMutationAndEnumerationOrder",
    "OriginalGameplayUiDefinitionRetainsAbstractContractsAndGuidBase",
    "OriginalSceneAuthoringAttributesRetainInheritedUsageAndValues",
    "OriginalSystemSceneDefinitionsBindAndRoundtripWithoutIdentityChanges",
    "OriginalBootSceneUnloadRetainsFactoryAndNativeOperation",
    "OriginalLevelDefinitionsRetainSceneNameAndNativeRuntimeContract",
    "StackableConfigurationPreservesNativeOrderCacheAndOperations",
    "OriginalConfigurationProviderRetainsUnityConstructionAndLookup",
    "EnumStringRegistryRetainsCompleteNativeTablesAndMutableMisses",
    "OriginalBootPersistenceStatesRetainKeysSaveAndShutdownOrder",
    "OriginalRuntimeDelegatesStayOutsideUnitySerialization",
    "OriginalStartingPointDefinitionsBindToMaintainedGuidType",
    "ScriptableGuidRetainsOriginalSerializationAndUnityNullRules",
    "CoroutineCallbacksRetainOriginalNextFrameOrdering",
    "EnumComparersRetainNativeHashesAndStaticIdentities",
    "ProgressRecordsRetainOriginalGraphAndUnityJson",
    "StateMachineStorageConditionsPreserveNativeTypeAndTimingSemantics",
    "MeshGeometryMatchesNativeStoresAndIndexBlob",
    "StateMachinePrimitivesMatchNativeHashAndIdentitySemantics",
    "StateMachineTransitionsPreserveNativeUpdateAndCallbackOrder",
    "StateMachineComponentsRetainOriginalUserAndLifecycleOrder",
    "StateMachineCompletionRetainsOriginalEndAndFinishedRules",
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
    environment = os.environ.copy()
    environment["LUCID_RECOVERY_WORK_DIR"] = str(Path(work_dir).resolve())
    result = subprocess.run(command, env=environment, timeout=1800)
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


def _audit_context_bytes(root, identity, nonce):
    """Small exact-format context, parsed independently by the matching Editor."""
    if not isinstance(nonce, str) or re.fullmatch(r"[0-9a-f]{32}", nonce) is None or any(
            not isinstance(identity.get(key), str) or re.fullmatch(r"[0-9a-f]{64}", identity[key]) is None
            for key in ("source_fingerprint", "prepared_asset_fingerprint")):
        raise ValueError("Invalid reference audit content identity")
    encoded = base64.b64encode(str(root).encode("utf-8")).decode("ascii")
    value = ("ProjectLucid.audit-identity-context-v1\nalgorithm=sha256-path-content-v1\n"
             "nonce=" + nonce + "\nproject_root_base64=" + encoded + "\nunity_version=" + UNITY_VERSION +
             "\nsource_fingerprint=" + identity["source_fingerprint"] +
             "\nprepared_asset_fingerprint=" + identity["prepared_asset_fingerprint"] + "\n").encode("utf-8")
    if len(value) > 16384:
        raise ValueError("Reference audit identity context exceeds its supported size")
    return value


def _audit_wrapper_paths_supported(root):
    """UTF16 Ordinal and Python path order agree for these prepared paths.

    Preserve the existing algorithms: unusual paths use direct Editor hashing,
    whose result must still equal the wrapper's native content fingerprint.
    """
    for base in ("Assets/Recovered", "Assets/StreamingAssets"):
        for parent, directories, names in os.walk(Path(root) / base):
            directories[:] = [name for name in directories if name not in ("obj", "bin", "__pycache__")
                              and not name.startswith(".")]
            for name in names:
                if name.endswith((".pyc", ".pyo", ".tmp")) or name == ".DS_Store":
                    continue
                relative = (Path(parent) / name).relative_to(root).as_posix()
                if "\\" in relative or any(ord(char) > 0xffff for char in relative):
                    return False
    return True


def _audit_read_bytes(work_dir, path, limit):
    """Read only owned, bounded evidence; reject changes during the read."""
    path = managed_path(work_dir, *path.relative_to(Path(work_dir)).parts)
    if not path.is_file() or path.stat().st_size > limit:
        raise ValueError("Reference audit evidence is absent or exceeds its supported size")
    before = path.stat()
    data = path.read_bytes()
    after = path.stat()
    fields = ("st_dev", "st_ino", "st_size", "st_mtime_ns", "st_ctime_ns")
    if not data or len(data) > limit or any(getattr(before, key) != getattr(after, key) for key in fields):
        raise ValueError("Reference audit evidence changed while reading")
    return data


def _audit_json_pairs(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("Duplicate reference audit JSON field")
        value[key] = item
    return value


def _audit_invalid_constant(value):
    raise ValueError("Invalid reference audit JSON constant: " + value)


def _audit_check_report(report, identity, *, nonce=None, context_digest=None):
    if not isinstance(report, dict) or report.get("unity_version") != UNITY_VERSION or any(
            report.get(key) != value for key, value in identity.items()):
        raise ValueError("Unity reference audit does not match the current project content")
    expected = {"identity_mode": "wrapper-prepost-v1", "identity_status": "pending-wrapper",
                "status": "pending-identity-verification", "identity_nonce": nonce,
                "identity_context_sha256": context_digest} if nonce is not None else {
                    "identity_mode": "editor-content-sha256-v1", "identity_status": "complete"}
    if any(report.get(key) != value for key, value in expected.items()):
        raise ValueError("Unity reference audit identity handshake is incomplete or stale")
    if nonce is None and (report.get("identity_nonce") not in (None, "") or report.get("identity_context_sha256") not in (None, "")):
        raise ValueError("Direct reference audit unexpectedly contains supplied identity evidence")
    for key in ("scanned_assets", "unresolved_references"):
        if type(report.get(key)) is not int or not 0 <= report[key] <= 0x7fffffff:
            raise ValueError("Invalid reference audit count: " + key)
    for key in ("missing_guids", "invalid_script_bindings", "shader_errors"):
        if not isinstance(report.get(key), list):
            raise ValueError("Invalid reference audit diagnostics: " + key)
    for item in report["missing_guids"]:
        if (not isinstance(item, dict) or not isinstance(item.get("guid"), str) or re.fullmatch(r"[0-9a-f]{32}", item["guid"]) is None or
                not isinstance(item.get("examples"), list) or not 0 < len(item["examples"]) <= 8 or
                any(not isinstance(value, str) or not value for value in item["examples"])):
            raise ValueError("Invalid missing reference diagnostic")
    for key in ("invalid_script_bindings", "shader_errors"):
        if any(not isinstance(value, str) or not value for value in report[key]):
            raise ValueError("Invalid reference audit diagnostic text")
    unresolved = sum(len(report[key]) for key in ("missing_guids", "invalid_script_bindings", "shader_errors"))
    reference_status = "complete" if unresolved == 0 else "incomplete"
    if report["unresolved_references"] != unresolved or report.get("reference_status") != reference_status or (
            nonce is None and report.get("status") != reference_status):
        raise ValueError("Reference audit status differs from its diagnostics")


def _audit_require_current_source(root, identity, phase):
    # current_identity hashes source before its much larger prepared-data pass.
    # Recheck the small source tree after that pass and before publication.
    if artifact_fingerprint(root) != identity["source_fingerprint"]:
        raise ValueError("Maintained source changed " + phase)


def run_audit(repo_root, work_dir):
    """Publish an Editor audit only after native pre/post hashes agree."""
    root, work = Path(repo_root).resolve(strict=True), Path(os.path.abspath(os.fspath(work_dir)))
    editor = find_editor()
    output = managed_path(work, "reports", "unity-reference-audit.json")
    lock = managed_path(work, "locks", "reference-audit.lock")
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        lock.mkdir()
    except FileExistsError as error:
        raise ValueError("Another reference audit owns the generated audit lock") from error
    try:
        before = current_identity(root)
        _audit_require_current_source(root, before, "while preparing the reference audit")
        nonce = uuid.uuid4().hex
        run = managed_path(work, "reports", "audit-runs", nonce)
        run.mkdir(parents=True, exist_ok=False)
        pending = managed_path(work, "reports", "audit-runs", nonce, "pending-audit.json")
        log = managed_path(work, "reports", "audit-runs", nonce, "editor.log")
        command = [str(editor), "-batchmode", "-quit", "-projectPath", str(root),
                   "-executeMethod", "ProjectLucid.Editor.LucidReferenceAudit.Run",
                   "-lucidAuditOutput", str(pending), "-logFile", str(log)]
        context = context_bytes = digest = None
        fast = _audit_wrapper_paths_supported(root)
        if fast:
            context = managed_path(work, "reports", "audit-runs", nonce, "identity-context.txt")
            context_bytes = _audit_context_bytes(root, before, nonce)
            with context.open("xb") as stream:
                stream.write(context_bytes)
                stream.flush()
                os.fsync(stream.fileno())
            digest = hashlib.sha256(context_bytes).hexdigest()
            command.extend(["-lucidAuditIdentityContext", str(context), "-lucidAuditIdentityDigest", digest,
                            "-lucidAuditIdentityNonce", nonce])
        result = subprocess.run(command, timeout=1800)
        if result.returncode or not pending.is_file():
            return {"status": "failed", "exit_code": result.returncode, "log": str(log),
                    "errors": ["Unity reference audit did not complete"]}
        raw = _audit_read_bytes(work, pending, 32 * 1024 * 1024)
        report = json.loads(raw, object_pairs_hook=_audit_json_pairs, parse_constant=_audit_invalid_constant)
        after = current_identity(root)
        _audit_require_current_source(root, after, "during post-audit identity verification")
        if before != after:
            raise ValueError("Project content changed during the Unity reference audit; rerun with stable imported content")
        if fast and _audit_read_bytes(work, context, 16384) != context_bytes:
            raise ValueError("Reference audit identity context changed during the Editor invocation")
        _audit_check_report(report, after, nonce=nonce if fast else None, context_digest=digest)
        # Recheck destinations/evidence immediately before the only authoritative
        # write. A failed or interrupted run never replaces the previous audit.
        output = managed_path(work, "reports", "unity-reference-audit.json")
        if _audit_read_bytes(work, pending, 32 * 1024 * 1024) != raw or (
                fast and _audit_read_bytes(work, context, 16384) != context_bytes):
            raise ValueError("Reference audit evidence changed before publication")
        _audit_require_current_source(root, after, "before reference audit publication")
        report.update(status=report["reference_status"], identity_status="complete",
                      generated_by="ProjectLucid.run_audit", identity_verified_by="native-python-prepost-v1",
                      staged_report_sha256=hashlib.sha256(raw).hexdigest(), staged_report_path=str(pending))
        write_json(output, report)
        return {"status": report["status"], "unresolved_references": report["unresolved_references"],
                "scanned_assets": report["scanned_assets"], "report_path": str(output), "log": str(log)}
    finally:
        lock.rmdir()

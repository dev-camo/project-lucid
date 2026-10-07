"""Run the existing recovery and full release gates, then publish physical players.

Each attempt retains its evidence. Recovery and extraction run fresh until their
complete output/tool provenance has a supported resume validator; pinned tool,
harness, and analysis-input caches retain their existing independent checks.
"""
from __future__ import annotations

from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import platform
import plistlib
import shutil
import stat
import traceback
import uuid

from . import assets, bootstrap, codeprogress, inspection, native, playercode, project, recovery, verification

OUTPUTS = {"macos": "ProjectLucid.app", "windows": "ProjectLucid.exe", "linux": "ProjectLucid.x86_64"}
PROTOCOL = 1
MARKER = ".project-lucid-output.json"


def _bytes(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"),
                      ensure_ascii=False, allow_nan=False).encode("utf-8")


def _digest(value):
    return hashlib.sha256(_bytes(value)).hexdigest()


def _physical(path):
    path = Path(path).expanduser()
    if ".." in path.parts:
        raise ValueError("Parent traversal is not an owned port path")
    path = Path(os.path.abspath(os.fspath(path)))
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError("Port paths cannot have symbolic-link ancestors: " + str(path))
    return path


def _overlap(first, second):
    return first == second or first in second.parents or second in first.parents


def _file(path):
    path = _physical(path)
    info = path.stat()
    if not stat.S_ISREG(info.st_mode):
        raise ValueError("Port evidence must be a regular file: " + str(path))
    digest = inspection.sha256_file(path)
    after = path.stat()
    if any(getattr(info, k) != getattr(after, k) for k in ("st_dev", "st_ino", "st_size", "st_mtime_ns", "st_ctime_ns")):
        raise ValueError("Port evidence changed while hashing: " + str(path))
    return {"path": str(path), "size": info.st_size, "sha256": digest}


def _tree(directory, internal_links=False):
    """Hash every physical file and directory, including permitted bundle links."""
    directory = _physical(directory)
    if not directory.is_dir():
        raise ValueError("Missing physical port tree: " + str(directory))
    rows = []
    for parent, folders, names in os.walk(directory, followlinks=False):
        for name in sorted(folders + names):
            path = Path(parent) / name
            relative = path.relative_to(directory).as_posix()
            if path.is_symlink():
                if not internal_links:
                    raise ValueError("Symbolic link in recovery/export evidence: " + str(path))
                if Path(os.readlink(path)).is_absolute():
                    raise ValueError("Player bundle link must be relative to its physical payload")
                resolved = path.resolve(strict=True)
                if resolved == directory or directory not in resolved.parents:
                    raise ValueError("Player bundle link escapes its physical payload")
                rows.append({"path": relative, "kind": "symlink", "target": os.readlink(path),
                             "resolved_path": resolved.relative_to(directory).as_posix()})
            elif path.is_file():
                row = _file(path)
                row.update(path=relative, kind="file")
                rows.append(row)
            elif path.is_dir():
                rows.append({"path": relative, "kind": "directory"})
            else:
                raise ValueError("Special filesystem object in port tree: " + str(path))
    if not any(r["kind"] == "file" for r in rows):
        raise ValueError("Empty port payload/evidence tree")
    return sorted(rows, key=lambda r: r["path"])


def _atomic_json(path, value):
    path = _physical(path)
    if path.exists() and not path.is_file():
        raise ValueError("Port JSON destination is not a regular file")
    temporary = path.with_name("." + path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(_bytes(value) + b"\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def _owner(root):
    return {"schema_version": PROTOCOL, "owner": "ProjectLucid.port", "project_root": str(root)}


def _output_owner(output, root, create=False):
    output = _physical(output)
    if output.exists() and not output.is_dir():
        raise ValueError("Output root must be a directory")
    marker = output / MARKER
    if marker.exists() or marker.is_symlink():
        raw = _file(marker)
        if _bytes(playercode._strict_json(marker.read_bytes())) != _bytes(_owner(root)) or _file(marker) != raw:
            raise ValueError("Output root belongs to another project or protocol")
    elif output.exists() and any(output.iterdir()):
        raise ValueError("Refusing a nonempty unowned output root")
    elif create:
        output.mkdir(parents=True, exist_ok=True)
        with marker.open("xb") as stream:
            stream.write(_bytes(_owner(root)) + b"\n")
            stream.flush()
            os.fsync(stream.fileno())
    return output


def _locations(repo_root, work_dir, input_path, output_root):
    root = _physical(repo_root)
    if not root.is_dir():
        raise ValueError("Port project does not exist")
    work = bootstrap.validate_work_dir(_physical(work_dir))
    supplied = _physical(input_path)
    app = _physical(inspection._locate_app(supplied))
    output = _physical(output_root)
    if not app.is_dir() or output == root or output in root.parents:
        raise ValueError("Invalid input/output root")
    if any(_overlap(a, b) for a, b in ((app, work), (app, output), (work, output))):
        raise ValueError("Input, generated cache, and output must not overlap")
    sources = [root / name for name in ("Assets", "Packages", "ProjectSettings", "tools", "Library", "Temp", "Builds")]
    if any(_overlap(app, p) for p in sources):
        raise ValueError("Supplied input overlaps a maintained/generated project tree")
    protected = sources + [root / "input"]
    if any(_overlap(output, p) for p in protected) or root in output.parents and any(
            part.startswith(".") for part in output.relative_to(root).parts):
        raise ValueError("Output overlaps maintained source or another project-owned tree")
    _output_owner(output, root)
    return root, work, app, output


@contextmanager
def _project_lock(root):
    """A process-held project-wide lock; a leftover file is not a live owner."""
    if bootstrap.fcntl is None:
        raise ValueError("The supported extraction host requires process-held file locks")
    path = _physical(root / ".cache/project-lucid/locks/port.lock")
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a+b") as stream:
        try:
            bootstrap.fcntl.flock(stream, bootstrap.fcntl.LOCK_EX | bootstrap.fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise ValueError("Another port attempt owns this project") from error
        try:
            yield
        finally:
            bootstrap.fcntl.flock(stream, bootstrap.fcntl.LOCK_UN)


def _copy_reports(work, attempt, stage, report):
    destination = attempt / "evidence" / stage
    paths = [Path(report[k]) for k in ("log", "log_path", "xml", "report_path", "evidence_report")
             if isinstance(report.get(k), str)]
    mode = stage.removesuffix("-rerun")
    if mode in ("editmode", "playmode"):
        paths += [work / "reports" / (mode + suffix) for suffix in (".xml", ".log", "-verification.json")]
    for index, path in enumerate(dict.fromkeys(paths)):
        path = _physical(path)
        if work not in path.parents:
            raise ValueError("Stage report points outside its generated workspace")
        if path.is_file():
            before = _file(path)
            destination.mkdir(parents=True, exist_ok=True)
            copied = destination / (str(index) + "-" + path.name)
            shutil.copy2(path, copied)
            if any(_file(copied)[k] != before[k] for k in ("size", "sha256")) or _file(path) != before:
                raise ValueError("Stage evidence changed while retaining its report")


def _import_drift(report, before, after, mode, source):
    """Accept only the genuine all-required-tests-passed prepared-import receipt."""
    expected = {"schema_version": 1, "generated_by": "ProjectLucid.run_tests", "mode": mode,
                "status": "incomplete", "exit_code": 0, "unity_version": verification.UNITY_VERSION,
                "source_fingerprint": source, "prepared_asset_fingerprint": after["prepared_asset_fingerprint"]}
    if any(type(report.get(k)) is not type(v) or report.get(k) != v for k, v in expected.items()) or report.get(
            "errors") != ["Project content changed during testing; rerun with the imported project"] or (
            before["source_fingerprint"] != source or after["source_fingerprint"] != source or
            before["prepared_asset_fingerprint"] == after["prepared_asset_fingerprint"]):
        return False
    verdict = report.get("verdict")
    if not isinstance(verdict, dict) or verdict.get("status") != "complete" or verdict.get("result") != "Passed" or verdict.get(
            "missing_required_tests") != []:
        return False
    required = verification.EDITMODE_TESTS if mode == "editmode" else verification.PLAYMODE_TESTS
    try:
        return verification.test_verdict(report["xml"], required) == verdict
    except (KeyError, ValueError, OSError):
        return False


def _payload(stage, target):
    entry = _physical(stage / OUTPUTS[target])
    if target == "macos":
        if not entry.is_dir():
            raise ValueError("The macOS entrypoint must be a physical app bundle")
        with (entry / "Contents/Info.plist").open("rb") as stream:
            info = plistlib.load(stream)
        name = info.get("CFBundleExecutable")
        if not isinstance(name, str) or not name or Path(name).name != name:
            raise ValueError("Built macOS bundle has no safe executable name")
        _file(entry / "Contents/MacOS" / name)
        data = entry / "Contents/Resources/Data"
    else:
        _file(entry)
        data = stage / "ProjectLucid_Data"
    for required in (data / "globalgamemanagers", data / "Managed/Game.Runtime.dll"):
        if _file(required)["size"] == 0:
            raise ValueError("Built player data/maintained game assembly is empty")
    return _tree(stage, internal_links=True)


def _promote(root, output, target, nonce, stage, identity, check, provenance):
    _output_owner(output, root)
    parent, final = stage.parent, stage.parent / nonce
    _physical(parent)
    latest = _physical(parent / "latest.json")
    if final.exists() or final.is_symlink() or stage.stat().st_dev != parent.stat().st_dev:
        raise ValueError("Output staging and new version are not fresh on one filesystem")
    check()
    rows = _payload(stage, target)
    if _payload(stage, target) != rows:
        raise ValueError("Build payload changed before promotion")
    check()
    stage.rename(final)
    if _payload(final, target) != rows:
        raise ValueError("Promoted player changed; previous selection remains authoritative")
    check()
    pointer = {**_owner(root), "target": target, "run": nonce, "payload": target + "/" + nonce,
               "entrypoint": OUTPUTS[target], "identity": identity, "provenance": provenance, "files": rows}
    _atomic_json(latest, pointer)
    return final / OUTPUTS[target]


def run_port(repo_root, work_dir, input_path, target, output_root):
    """Return nonzero evidence unless every existing full release gate succeeds."""
    attempt = None
    phase = "preflight"
    report = {"schema_version": PROTOCOL, "generated_by": "ProjectLucid.run_port",
              "status": "failed", "exit_code": 1, "target": target, "stages": [],
              "transform_policy": "fresh-recovery-and-export-with-complete-output-seals-v1"}
    try:
        if target not in OUTPUTS:
            raise ValueError("Port target must be macos, windows, or linux")
        root, base, app, output = _locations(repo_root, work_dir, input_path, output_root)
        with _project_lock(root):
            nonce = uuid.uuid4().hex
            work = bootstrap.managed_path(base, "port", "work")
            attempt = bootstrap.managed_path(base, "port", "runs", nonce)
            attempt.mkdir(parents=True, exist_ok=False)
            report.update(run=nonce, run_dir=str(attempt), work_dir=str(work), output_root=str(output))
            lock = bootstrap.load_lock()
            host = platform.system().lower() + "-" + platform.machine().lower()
            if lock.get("platform") != host:
                raise ValueError("Tool lock supports " + str(lock.get("platform")) + "; detected " + host)
            source = verification.artifact_fingerprint(root)
            engine = playercode._engine_identity(verification.find_editor())
            original = inspection._manifest(app)
            supplied = assets.fingerprint_manifest(original)
            lock_seal = _file(bootstrap.LOCK_FILE)
            _atomic_json(attempt / "input-manifest.json", {"files": original, "fingerprint": supplied})
            _atomic_json(attempt / "preflight.json", {"source_fingerprint": source, "engine": engine, "tool_lock": lock_seal})
            transforms = []

            def unchanged():
                if verification.artifact_fingerprint(root) != source:
                    raise ValueError("Maintained source changed during the port attempt")
                if assets.fingerprint_manifest(inspection._manifest(app)) != supplied or _file(bootstrap.LOCK_FILE) != lock_seal:
                    raise ValueError("Supplied input or tool lock changed during the port attempt")
                if playercode._engine_identity(verification.find_editor()) != engine:
                    raise ValueError("Installed compiler/engine changed during the port attempt")

            def step(name, wanted, call, import_before=None):
                nonlocal phase
                phase = name
                unchanged()
                result = call()
                if not isinstance(result, dict):
                    raise ValueError("Stage did not return an evidence object: " + name)
                saved = attempt / ("%02d-%s.json" % (len(report["stages"]), name))
                _atomic_json(saved, result)
                report["stages"].append({"stage": name, "status": result.get("status"), "report": _file(saved)})
                _copy_reports(work, attempt, name, result)
                if result.get("status") != wanted and not (import_before is not None and _import_drift(
                        result, import_before, verification.current_identity(root), name, source)):
                    raise ValueError("Port stage " + name + " is " + str(result.get("status")) + ": " +
                                     "; ".join(str(x) for x in result.get("errors", [])) + str(result.get("error", "")))
                unchanged()
                return result

            def inspect():
                result = inspection.inspect_bundle(app)
                result["status"] = "complete" if result.get("addressables", {}).get("complete") is True and result.get(
                    "identity", {}).get("unity_version") == verification.UNITY_VERSION else "failed"
                bootstrap.write_json(bootstrap.managed_path(work, "reports", "inspection.json"), result)
                return result

            def seal(name, directory):
                directory = _physical(directory)
                if work not in directory.parents:
                    raise ValueError("Transform output is outside its generated workspace")
                transforms.append({"stage": name, "directory": str(directory), "files": _tree(directory)})
                _atomic_json(attempt / "transform-seals.json", transforms)

            def recheck_transforms():
                for sealed in transforms:
                    if _tree(sealed["directory"]) != sealed["files"]:
                        raise ValueError("Recovery/export output changed: " + sealed["stage"])

            step("inspect", "complete", inspect)
            step("bootstrap", "ready", lambda: bootstrap.bootstrap(work, ["assetripper", "cpp2il", "dumper"]))
            declarations = step("declarations", "ready", lambda: recovery.recover_code(app, work, mode="schemas"))
            seal("declarations", declarations["output_dir"])
            step("native-schema", "ready", lambda: native.native_schema(app, work))
            exported = step("asset-export", "exported", lambda: assets.extract_assets(app, work))
            assets._validate_export_identity(exported)
            seal("asset-export", exported["project_path"])
            step("extraction-validation", "complete", lambda: project.validate_project(root, work, stage="extraction"))
            recheck_transforms()
            step("prepare", "prepared", lambda: assets.prepare_assets(root, work))
            recheck_transforms()
            step("player-code", "compiled", lambda: playercode.run_player_code(root, work, target))
            step("source-measurement", "ready", lambda: codeprogress.run_code_progress(root, work, app, target))
            for mode in ("editmode", "playmode"):
                before_tests = verification.current_identity(root)
                tested = step(mode, "complete", lambda: verification.run_tests(root, work, mode), import_before=before_tests)
                if tested["status"] != "complete":
                    step(mode + "-rerun", "complete", lambda: verification.run_tests(root, work, mode))
            identity = verification.current_identity(root)
            audit = step("audit", "complete", lambda: verification.run_audit(root, work))
            if type(audit.get("unresolved_references")) is not int or audit["unresolved_references"] != 0:
                raise ValueError("The full reference audit has unresolved or missing reference counts")
            validation = step("release-validation", "complete", lambda: project.validate_project(root, work, stage="release"))
            if validation.get("errors") != [] or validation.get("stage") != "release" or validation.get("generated_by") != "ProjectLucid.validate_project" or any(
                    validation.get(k) != v for k, v in identity.items()):
                raise ValueError("Release validation does not prove the prepared project identity")
            recheck_transforms()
            _output_owner(output, root, create=True)
            parent = _physical(output / target)
            parent.mkdir(exist_ok=True)
            stage = _physical(parent / (".stage-" + nonce))
            stage.mkdir(exist_ok=False)
            built = step("build", "complete", lambda: project.build_project(root, work, target, destination=stage / OUTPUTS[target]))
            if built.get("target") != target or type(built.get("exit_code")) is not int or built["exit_code"] != 0 or built.get(
                    "output_dir") != str(stage / OUTPUTS[target]):
                raise ValueError("Build receipt does not identify the owned full-player destination")
            step("release-recheck", "complete", lambda: project.validate_project(root, work, stage="release"))
            recheck_transforms()
            provenance = {"input_manifest_sha256": _file(attempt / "input-manifest.json")["sha256"],
                          "tool_lock_sha256": lock_seal["sha256"], "stage_reports": report["stages"]}
            _atomic_json(stage / "port-receipt.json", {"target": target, "identity": identity,
                                                       "validation": validation, "build": built, "provenance": provenance})

            def final_check():
                unchanged()
                if verification.current_identity(root) != identity:
                    raise ValueError("Prepared project changed before output publication")

            phase = "promotion"
            entry = _promote(root, output, target, nonce, stage, identity, final_check, provenance)
            report.update(status="complete", exit_code=0, output_dir=str(entry), identity=identity)
    except (Exception, KeyboardInterrupt) as error:
        report.update(status="interrupted" if isinstance(error, KeyboardInterrupt) else "failed",
                      exit_code=130 if isinstance(error, KeyboardInterrupt) else 1,
                      failed_stage=phase, error=str(error) or type(error).__name__)
        if attempt is not None:
            (attempt / "failure.log").write_text(traceback.format_exc(), encoding="utf-8")
    if attempt is not None:
        _atomic_json(attempt / "result.json", report)
        report["report_path"] = str(attempt / "result.json")
    return report

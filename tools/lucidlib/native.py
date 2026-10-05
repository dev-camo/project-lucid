"""Pinned metadata schemas and bounded native/ISIL evidence, with no IL recovery."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import shutil
import uuid
import xml.etree.ElementTree as ET

from .bootstrap import (REPO_ROOT, ToolError, _install, bootstrap, download_artifact,
                        dotnet_environment, fcntl, load_lock, managed_path,
                        run_logged, validate_work_dir, write_json)
from .inspection import sha256_file
from .metadata import parse_metadata
from .recovery import _resolve_input, _thin_binary


_PIN = "b5ad444b82267cb1e4b88b8b373c008105bdea52"
_ARCHIVE_HASH = "e396fc9a5321a121458b261d6a164945b916c9d024bf2cc96e2953dfe7d4069f"
_SDK = "10.0.401"
_PROJECT = REPO_ROOT / "tools/native-recovery/NativeRecovery.csproj"
_MANIFEST = _PROJECT.with_name("SourcePin.props")
_HARNESS = "ProjectLucid.NativeRecovery.dll"
_SOURCE_PROJECTS = {"Cpp2IL.Core", "LibCpp2IL", "StableNameDotNet", "WasmDisassembler"}
_LIMITATIONS = [
    "Metadata schemas and initial ISIL translation do not establish recovered managed behavior.",
    "Unity serialization candidates require independent type and layout verification.",
    "Native addresses require independent binary corroboration.",
    "Generated schemas and evidence are never installed into Unity runtime assemblies.",
]


def _load_json(path: Path, limit: int) -> dict:
    if not path.is_file() or path.stat().st_size > limit:
        raise ToolError(f"Missing or oversized native recovery report: {path}")
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ToolError("Native recovery report must be a JSON object")
    return value


def _lock_identity(lock: dict) -> dict:
    source, sdk = lock["artifacts"]["cpp2il-source"], lock["artifacts"]["dotnet"]
    if (source.get("version"), source.get("sha256"), sdk.get("version")) != (_PIN, _ARCHIVE_HASH, _SDK):
        raise ToolError("Native harness requires the exact pinned Cpp2IL source and .NET SDK")
    return {"cpp2il_pin": _PIN, "cpp2il_source_archive_sha256": _ARCHIVE_HASH,
            "sdk_version": _SDK, "sdk_archive_sha512": sdk["sha512"]}


def _verify_cpp2il_source(source: Path, work_dir: Path) -> None:
    """Validate source content before reusing a build, as well as during MSBuild."""
    expected = {}
    for item in ET.parse(_MANIFEST).getroot().findall("./ItemGroup/PinnedCpp2ILSource"):
        name = item.attrib["Include"]
        prefix = "$(Cpp2ILSource)/"
        if not name.startswith(prefix):
            raise ToolError("Invalid pinned source manifest")
        relative = Path(name[len(prefix):])
        path = managed_path(work_dir, *source.relative_to(work_dir).parts, *relative.parts)
        expected[relative.as_posix()] = item.attrib["ExpectedHash"].lower()
        if not path.is_file() or sha256_file(path) != expected[relative.as_posix()]:
            raise ToolError(f"Cpp2IL source differs from the pinned archive: {relative}")
    if not expected:
        raise ToolError("Empty pinned source manifest")
    for project in _SOURCE_PROJECTS:
        for path in (source / project).rglob("*.cs"):
            relative = path.relative_to(source)
            if "bin" not in relative.parts and "obj" not in relative.parts and relative.as_posix() not in expected:
                raise ToolError(f"Additional unpinned Cpp2IL source: {relative}")


def _file_hashes(directory: Path, work_dir: Path) -> dict[str, str]:
    result = {}
    for path in sorted(directory.rglob("*")):
        checked = managed_path(work_dir, *path.relative_to(work_dir).parts)
        if checked.is_file():
            result[path.relative_to(directory).as_posix()] = sha256_file(checked)
    return result


def _cached_build(pointer: Path, work_dir: Path, fingerprint: str) -> dict | None:
    if not pointer.is_file():
        return None
    try:
        report = _load_json(pointer, 1024 * 1024)
        if report.get("status") != "ready" or report.get("fingerprint") != fingerprint:
            return None
        directory = managed_path(work_dir, *Path(report["output_dir"]).relative_to(work_dir).parts)
        binary = managed_path(work_dir, *Path(report["path"]).relative_to(work_dir).parts)
        if binary.name != _HARNESS or not binary.is_file():
            return None
        if not report.get("file_hashes") or _file_hashes(directory, work_dir) != report["file_hashes"]:
            return None
        return dict(report, reused=True)
    except (OSError, ValueError, KeyError, ToolError):
        return None


def build_native_harness(work_dir: Path) -> dict:
    """Build/reuse the exact Core harness; all generated files stay in the cache."""
    work_dir = validate_work_dir(work_dir)
    run_id = uuid.uuid4().hex
    report_path = managed_path(work_dir, "reports", f"native-build-{run_id}.json")
    report = {"schema_version": 1, "status": "failed", "tool": "project-lucid-native",
              "report_path": str(report_path), "reused": False}
    stage = None
    try:
        lock = load_lock()
        identity = _lock_identity(lock)
        report.update(identity)
        installed = bootstrap(work_dir, ["dotnet"])
        sdk = installed.get("tools", {}).get("dotnet", {})
        if installed.get("status") != "ready" or sdk.get("status") != "ready":
            raise ToolError(sdk.get("error", "Pinned SDK bootstrap failed"))
        dotnet = managed_path(work_dir, "tools", "dotnet", "dotnet")
        source_lock = managed_path(work_dir, "tools", ".cpp2il-source.lock")
        build_lock = managed_path(work_dir, "native-recovery", ".build.lock")
        build_lock.parent.mkdir(parents=True, exist_ok=True)
        if fcntl is None:
            raise ToolError("Native harness requires the platform supported by the tool lock")
        with build_lock.open("a") as build_stream, source_lock.open("a") as source_stream:
            fcntl.flock(build_stream, fcntl.LOCK_EX)
            fcntl.flock(source_stream, fcntl.LOCK_EX)
            source = _install("cpp2il-source", lock, work_dir)
            archive = download_artifact(lock["artifacts"]["cpp2il-source"], work_dir)
            _verify_cpp2il_source(source, work_dir)
            identity["harness_sources"] = {path.name: sha256_file(path) for path in
                                            (_PROJECT, _MANIFEST, _PROJECT.with_name("Program.cs"))}
            identity["dotnet_host_sha256"] = sha256_file(dotnet)
            fingerprint = hashlib.sha256(json.dumps(identity, sort_keys=True).encode()).hexdigest()
            report.update(identity, fingerprint=fingerprint)
            pointer = managed_path(work_dir, "native-recovery", "latest-build.json")
            cached = _cached_build(pointer, work_dir, fingerprint)
            if cached:
                return cached
            stage = managed_path(work_dir, "native-recovery", "builds", f".stage-{run_id}")
            destination = managed_path(work_dir, "native-recovery", "builds", run_id)
            stage.mkdir(parents=True, exist_ok=False)
            process = run_logged([
                str(dotnet), "build", str(_PROJECT), "-c", "Release", "-p:TargetFramework=net10.0",
                "-p:GeneratePackageOnBuild=false", "-p:RestorePackagesWithLockFile=true",
                "-p:Cpp2ILSource=" + str(source), "-p:Cpp2ILArchive=" + str(archive),
                "-p:NativeRecoveryBuildRoot=" + str(stage / "build") + "/",
            ], work_dir, f"native-build-{run_id}.log", timeout=600,
                env=dotnet_environment(work_dir, dotnet.parent))
            report["process"] = process
            output = stage / "build/bin/Release/net10.0"
            if process["returncode"] or process["timed_out"] or not (output / _HARNESS).is_file():
                raise ToolError(f"Native harness build failed; inspect {process['log']}")
            _verify_cpp2il_source(source, work_dir)
            hashes = _file_hashes(output, work_dir)
            stage.replace(destination)
            stage = None
            final_output = destination / "build/bin/Release/net10.0"
            report.update(status="ready", path=str(final_output / _HARNESS), output_dir=str(final_output),
                          file_hashes=hashes)
            write_json(report_path, report)
            write_json(pointer, report)
            return report
    except (ToolError, OSError, ValueError, KeyError, ET.ParseError) as error:
        report["error"] = str(error)
        write_json(report_path, report)
        return report
    finally:
        if stage is not None and stage.exists():
            shutil.rmtree(stage)


def _selection(metadata: dict, assembly: str | None, token: str | int | None) -> tuple[str | None, str | None]:
    if assembly is not None and (not isinstance(assembly, str) or not assembly or assembly.strip() != assembly or assembly.startswith("--")):
        raise ValueError("Assembly must be an exact original assembly name")
    if assembly is not None and assembly not in {image["assembly_name"] for image in metadata["images"]}:
        raise ValueError(f"Unknown original assembly: {assembly}")
    if token is None:
        return assembly, None
    if assembly is None:
        raise ValueError("Native method evidence requires an exact original assembly name")
    if isinstance(token, int) and not isinstance(token, bool) and 0 <= token <= 0xffffffff:
        token = f"0x{token:08x}"
    if not isinstance(token, str) or not re.fullmatch(r"0x06[0-9a-fA-F]{6}", token) or int(token[2:], 16) == 0x06000000:
        raise ValueError("Method token must be an original MethodDef token in exact 0x06xxxxxx form")
    token = token.lower()
    image_names = {image["name"] for image in metadata["images"] if image["assembly_name"] == assembly}
    if not any(method["assembly"] in image_names and method["token"].lower() == token for method in metadata["methods"]):
        raise ValueError(f"Unknown method token in {assembly}: {token}")
    return assembly, token


def _validate_evidence(evidence: dict, command: str, source: dict, assembly: str | None,
                       token: str | None, directory: Path, work_dir: Path, expected_assemblies: set[str]) -> None:
    expected = {"schema_version": 1, "command": command, "cpp2il_pin": _PIN,
                "cpp2il_source_archive_sha256": _ARCHIVE_HASH,
                "native_binary_sha256": source["analysis_binary_sha256"],
                "metadata_sha256": source["metadata_sha256"],
                "unity_version": source["unity_version"], "managed_semantics_recovered": False,
                "native_addresses_verified": False}
    for key, value in expected.items():
        if evidence.get(key) != value or (value is False and evidence.get(key) is not False):
            raise ToolError(f"Native evidence provenance or safety flag differs: {key}")
    if evidence.get("status") not in {"ready", "incomplete"}:
        raise ToolError("Native decoder did not finish; pending evidence is not ready")
    if command == "schemas":
        names = [item["name"] for item in evidence.get("assemblies", [])]
        if len(names) != len(set(names)) or set(names) != expected_assemblies:
            raise ToolError("Native schema assembly coverage differs from the selected original metadata")
        if evidence.get("errors") and evidence["status"] == "ready":
            raise ToolError("Native schema reported readiness despite decode failures")
        for item in evidence["assemblies"]:
            for type_ in item.get("types", []):
                if evidence["status"] == "ready" and type_.get("schema_complete") is not True:
                    raise ToolError("Native schema reported readiness despite an incomplete type")
                for field in type_.get("fields", []):
                    if evidence["status"] == "ready" and any(field.get(flag) is not True for flag in
                                                              ("schema_complete", "custom_attributes_complete", "default_value_complete")):
                        raise ToolError("Native schema reported readiness despite an incomplete field")
    else:
        method = evidence.get("method", {})
        if method.get("assembly") != assembly or method.get("token") != token or evidence.get("analysis_pipeline_invoked") is not False:
            raise ToolError("Native method selection or analysis pipeline differs from the request")
        native = managed_path(work_dir, *directory.relative_to(work_dir).parts, "native.bin")
        if not native.is_file() or not 0 < native.stat().st_size <= 30000 or native.stat().st_size != method.get("native_size"):
            raise ToolError("Native method body is missing, truncated, or exceeds its evidence bound")
        if sha256_file(native) != method.get("native_sha256"):
            raise ToolError("Native method body fingerprint differs from the report")
        for name in ("native.txt", "isil.txt"):
            path = managed_path(work_dir, *directory.relative_to(work_dir).parts, name)
            if not path.is_file() or not 0 < path.stat().st_size <= 16 * 1024 * 1024:
                raise ToolError(f"Native method text evidence is missing or oversized: {name}")


def _native_evidence(input_path: Path, work_dir: Path, command: str, assembly: str | None,
                     token: str | int | None) -> dict:
    work_dir = validate_work_dir(work_dir)
    app, binary, metadata_path, version = _resolve_input(input_path)
    if app == work_dir or work_dir in app.parents:
        raise ValueError("Native recovery input cannot be inside the generated work directory")
    metadata = parse_metadata(metadata_path)
    assembly, token = _selection(metadata, assembly, token)
    if command == "method" and token is None:
        raise ValueError("Native method evidence requires an original MethodDef token")
    original_hash = sha256_file(binary)
    metadata_hash = sha256_file(metadata_path)
    thin, architecture = _thin_binary(binary, work_dir, original_hash)
    source = {"app": str(app), "unity_version": version, "metadata_version": metadata["version"],
              "metadata_sha256": metadata_hash, "binary_sha256": original_hash,
              "architecture": architecture["name"], "analysis_binary": str(thin),
              "analysis_binary_sha256": sha256_file(thin)}
    build = build_native_harness(work_dir)
    run_id = command + "-" + uuid.uuid4().hex
    stage = managed_path(work_dir, "native-recovery", "runs", ".stage-" + run_id)
    destination = managed_path(work_dir, "native-recovery", "runs", run_id)
    stage.mkdir(parents=True, exist_ok=False)
    report = {"schema_version": 1, "status": "failed", "command": command, "run_id": run_id,
              "source": source, "tool": build, "assembly": assembly, "token": token,
              "output_dir": str(destination), "evidence_dir": str(destination / "evidence"),
              "report_path": str(destination / "report.json"), "evidence_report": None,
              "managed_semantics_recovered": False, "native_addresses_verified": False,
              "verified_gameplay_methods": 0, "limitations": list(_LIMITATIONS)}
    evidence_dir = managed_path(work_dir, *stage.relative_to(work_dir).parts, "evidence")
    try:
        if build.get("status") != "ready":
            raise ToolError(build.get("error", "Pinned native harness build failed"))
        environment = dotnet_environment(work_dir, managed_path(work_dir, "tools", "dotnet"))
        environment.update(DOTNET_TieredCompilation="0", COMPlus_TieredCompilation="0", DOTNET_TieredPGO="0")
        args = [str(managed_path(work_dir, "tools", "dotnet", "dotnet")), build["path"], command,
                "--binary", str(thin), "--metadata", str(metadata_path), "--unity-version", version,
                "--output", str(evidence_dir)]
        if assembly is not None:
            args.extend(["--assembly", assembly])
        if token is not None:
            args.extend(["--token", token])
        process = run_logged(args, work_dir, f"native-{run_id}.log", timeout=180 if command == "schemas" else 120,
                             env=environment)
        report["process"] = process
        evidence_path = managed_path(work_dir, *evidence_dir.relative_to(work_dir).parts, "report.json")
        evidence = _load_json(evidence_path, 512 * 1024 * 1024 if command == "schemas" else 1024 * 1024)
        names = {image["assembly_name"] for image in metadata["images"] if assembly is None or image["assembly_name"] == assembly}
        _validate_evidence(evidence, command, source, assembly, token, evidence_dir, work_dir, names)
        if process["timed_out"] or process["returncode"] not in (0, 1) or (process["returncode"] == 1 and evidence["status"] != "incomplete"):
            raise ToolError(f"Native evidence process failed; inspect {process['log']}")
        if sha256_file(binary) != original_hash or sha256_file(metadata_path) != metadata_hash:
            raise ToolError("Native recovery input changed during the evidence run")
        evidence["output_dir"] = report["evidence_dir"]
        write_json(evidence_path, evidence)
        report["status"] = evidence["status"]
        report["evidence_report"] = str(destination / "evidence/report.json")
        if command == "schemas":
            report["counts"] = {key: evidence[key] for key in ("assembly_count", "type_count", "field_count")}
            report["decode_error_count"] = len(evidence.get("errors", []))
        else:
            report["method"] = evidence["method"]
            report["isil_instruction_count"] = evidence["isil_instruction_count"]
            report["analysis_pipeline_invoked"] = False
    except (ToolError, OSError, ValueError, KeyError) as error:
        report["error"] = str(error)
        if (evidence_dir / "report.json").is_file():
            report["partial_evidence_report"] = str(destination / "evidence/report.json")
    write_json(stage / "report.json", report)
    stage.replace(destination)
    write_json(managed_path(work_dir, "reports", f"native-{command}.json"), report)
    if report["status"] == "ready":
        write_json(managed_path(work_dir, "native-recovery", f"latest-{command}.json"), report)
    return report


def native_schema(input_path: Path, work_dir: Path, assembly: str | None = None) -> dict:
    """Emit original metadata/serialization candidates without managed assemblies."""
    return _native_evidence(input_path, work_dir, "schemas", assembly, None)


def native_method(input_path: Path, work_dir: Path, assembly: str, token: str | int) -> dict:
    """Emit at most 30,000 native bytes plus initial ISIL for one original method."""
    return _native_evidence(input_path, work_dir, "method", assembly, token)

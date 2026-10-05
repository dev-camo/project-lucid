"""Restore IL2CPP declarations and retain bounded, experimental body evidence."""

from __future__ import annotations

import collections
import hashlib
import json
from pathlib import Path
import plistlib
import re
import uuid

from .bootstrap import (ToolError, bootstrap, dotnet_environment, managed_path,
                        run_logged, validate_work_dir, write_json)
from .inspection import sha256_file
from .macho import inspect_macho
from .metadata import parse_metadata


_MODES = {"schemas": "dll_default", "bodies": "dll_il_recovery", "analysis": "isil"}


def classify_il(prefix: bytes, length: int | None = None, has_body: bool = True) -> str:
    """Classify evidence conservatively; nontrivial IL is never 'verified'.

    Empty/default methods may be legitimate original code, so they are labelled
    ambiguous. These patterns also identify Cpp2IL's failed-analysis output.
    The prefix can be truncated to 16 bytes if the complete length is provided.
    """
    length = len(prefix) if length is None else length
    if not has_body:
        return "declaration_only"
    if length == 0:
        return "empty_body"
    if (length == 2 and prefix == b"\x14\x7a") or (
            length == 11 and prefix[0] == 0x72 and prefix[5] == 0x73 and prefix[10] == 0x7A):
        return "throwing_stub_or_trivial_throw"
    patterns = (b"\x2a", b"\x14\x2a", b"\x16\x2a", b"\x16\x6a\x2a",
                b"\x22\0\0\0\0\x2a", b"\x23" + b"\0" * 8 + b"\x2a")
    if any(length == len(pattern) and prefix == pattern for pattern in patterns):
        return "trivial_or_default_body"
    if length == 7 and prefix[0] == 0x02 and prefix[1] == 0x28 and prefix[6] == 0x2A:
        return "trivial_or_default_body"
    if length == 10 and prefix[:4] == b"\x12\0\xfe\x15" and prefix[8:] == b"\x06\x2a":
        return "trivial_or_default_body"
    return "provisional_il_body"


def _resolve_input(input_path: Path) -> tuple[Path, Path, Path, str]:
    root = Path(input_path).resolve()
    if root.suffix == ".app" and root.is_dir():
        app = root
    else:
        apps = sorted(path for path in root.glob("*.app") if path.is_dir())
        if len(apps) != 1:
            raise ValueError("Input must be a .app bundle or contain exactly one .app bundle")
        app = apps[0]
    metadata = app / "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"
    binary = app / "Contents/Frameworks/GameAssembly.dylib"
    for required in (metadata, binary, app / "Contents/Info.plist"):
        if not required.is_file():
            raise ValueError(f"Required recovery input is missing: {required}")
        try:
            required.resolve().relative_to(app.resolve())
        except ValueError as exc:
            raise ValueError("Recovery input symbolic links cannot leave the supplied bundle") from exc
    with (app / "Contents/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    version = re.search(r"Unity Player version (\d+\.\d+\.\d+[abfp]\d+)", info.get("CFBundleGetInfoString", ""))
    if not version:
        raise ValueError("Cannot identify Unity version from the supplied app Info.plist")
    return app, binary, metadata, version.group(1)


def _thin_binary(binary: Path, work_dir: Path, fingerprint: str) -> tuple[Path, dict]:
    native = inspect_macho(binary)
    architectures = native["architectures"]
    supported = [a for a in architectures if a["name"] in ("x86_64", "arm64")]
    if not supported:
        raise ValueError("GameAssembly has no supported x86_64 or arm64 architecture")
    architecture = min(supported, key=lambda item: item["name"] != "x86_64")
    if architecture["encrypted"]:
        raise ValueError("Selected native architecture is encrypted; recovery requires a readable supplied binary")
    output = managed_path(work_dir, "recovery", "inputs", f"{fingerprint}.{architecture['name']}.dylib")
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.is_file() and output.stat().st_size == architecture["size"]:
        # Existing cache files are not trusted merely because they have the right size.
        checksum = hashlib.sha256()
        with binary.open("rb") as source:
            source.seek(architecture["offset"])
            remaining = architecture["size"]
            while remaining:
                block = source.read(min(1024 * 1024, remaining))
                if not block:
                    raise ValueError("Native architecture is truncated")
                checksum.update(block)
                remaining -= len(block)
        if sha256_file(output) == checksum.hexdigest():
            return output, architecture
        raise ToolError(f"Cached analysis binary changed: {output}; move it aside and retry")
    if output.exists():
        raise ToolError(f"Incomplete cached analysis binary: {output}; move it aside and retry")
    temporary = managed_path(work_dir, "recovery", "inputs", f".{uuid.uuid4().hex}.part")
    try:
        with binary.open("rb") as source, temporary.open("xb") as destination:
            source.seek(architecture["offset"])
            remaining = architecture["size"]
            while remaining:
                block = source.read(min(1024 * 1024, remaining))
                if not block:
                    raise ValueError("Native architecture is truncated")
                destination.write(block)
                remaining -= len(block)
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    return output, architecture


_INSPECTOR_SOURCE = r'''using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

var files = Directory.GetFiles(args[0], "*", SearchOption.AllDirectories);
var methods = new List<object>();
var assemblies = new List<object>();
foreach (var path in files.Order()) {
    using var stream = File.OpenRead(path);
    if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') continue;
    stream.Position = 0;
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata) continue;
    var reader = pe.GetMetadataReader();
    var assembly = reader.GetString(reader.GetAssemblyDefinition().Name);
    assemblies.Add(new { name = assembly, file = Path.GetFileName(path), types = reader.TypeDefinitions.Count,
                        methods = reader.MethodDefinitions.Count, fields = reader.FieldDefinitions.Count });
    foreach (var handle in reader.MethodDefinitions) {
        var method = reader.GetMethodDefinition(handle);
        var type = reader.GetTypeDefinition(method.GetDeclaringType());
        var name = reader.GetString(method.Name);
        var ns = reader.GetString(type.Namespace);
        var typeName = reader.GetString(type.Name);
        var il = method.RelativeVirtualAddress == 0 ? Array.Empty<byte>() :
                 pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes() ?? Array.Empty<byte>();
        methods.Add(new { assembly, token = "0x" + MetadataTokens.GetToken(handle).ToString("x8"),
                         name, declaring_type = string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName,
                         has_body = method.RelativeVirtualAddress != 0, il_length = il.Length,
                         il_prefix = Convert.ToHexString(il.Take(16).ToArray()),
                         il_sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(il)) });
    }
}
File.WriteAllText(args[1], JsonSerializer.Serialize(new { assemblies, methods }));
'''


def _inspect_managed_output(directory: Path, work_dir: Path, run_id: str, *, schemas: bool = False) -> dict:
    """Inspect actual emitted IL using .NET's metadata reader, not tool counters."""
    helper = managed_path(work_dir, "tools", "il-inspector")
    helper.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256(_INSPECTOR_SOURCE.encode()).hexdigest()
    marker = managed_path(work_dir, "tools", "il-inspector", "sdt-inspector.json")
    assembly = managed_path(work_dir, "tools", "il-inspector", "bin/Release/net10.0/RecoveryInspector.dll")
    project = managed_path(work_dir, "tools", "il-inspector", "RecoveryInspector.csproj")
    source_file = managed_path(work_dir, "tools", "il-inspector", "Program.cs")
    dotnet_root = managed_path(work_dir, "tools", "dotnet")
    dotnet = dotnet_root / "dotnet"
    if not dotnet.is_file():
        raise ToolError("Native .NET 10 SDK is missing for managed-output inspection")
    env = dotnet_environment(work_dir, dotnet_root)
    if not marker.is_file() or not assembly.is_file() or json.loads(marker.read_text()).get("source_sha256") != source_hash:
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            '<OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings>'
            '</PropertyGroup></Project>\n', encoding="utf-8")
        source_file.write_text(_INSPECTOR_SOURCE, encoding="utf-8")
        built = run_logged([str(dotnet), "build", str(project), "-c", "Release"],
                           work_dir, "il-inspector-build.log", timeout=120, env=env, cwd=helper)
        if built["returncode"] or built["timed_out"]:
            raise ToolError(f"Could not build the managed-output inspector: {built['log']}")
        write_json(marker, {"source_sha256": source_hash})
    output = managed_path(work_dir, "recovery", run_id, "managed-methods.json")
    executed = run_logged([str(dotnet), str(assembly), str(directory), str(output)], work_dir,
                          f"il-inspection-{run_id}.log", timeout=120, env=env)
    if executed["returncode"] or executed["timed_out"] or not output.is_file():
        raise ToolError(f"Could not inspect emitted managed assemblies: {executed['log']}")
    inspected = json.loads(output.read_text(encoding="utf-8"))
    counts = collections.Counter()
    for method in inspected["methods"]:
        classification = classify_il(bytes.fromhex(method.pop("il_prefix")), method["il_length"], method["has_body"])
        method["recovery_status"] = ("schema_placeholder" if schemas and method["has_body"] else classification)
        counts[method["recovery_status"]] += 1
    write_json(output, inspected)
    return {"counts": dict(sorted(counts.items())), "assemblies": inspected["assemblies"],
            "method_inventory": str(output), "verified_gameplay_methods": 0}


def _cpp2il(thin: Path, metadata: Path, version: str, work_dir: Path, mode: str, run_id: str) -> dict:
    installed = bootstrap(work_dir, ["cpp2il"])
    item = installed["tools"]["cpp2il"]
    if item["status"] != "ready":
        return {"tool": "cpp2il", "status": "failed", "error": item.get("error", "Bootstrap failed")}
    output = managed_path(work_dir, "recovery", run_id, "cpp2il")
    output.parent.mkdir(parents=True, exist_ok=True)
    dotnet_root = managed_path(work_dir, "tools", "dotnet")
    environment = dotnet_environment(work_dir, dotnet_root)
    # The selected upstream build has intermittently crashed during parallel
    # emission/analysis on the Mac host. Keep this experimental process bounded
    # and serialize its worker pools instead of allowing a fatal error to be
    # mistaken for a completed recovery.
    environment.update(DOTNET_PROCESSOR_COUNT="1", DOTNET_TieredCompilation="0", DOTNET_TieredPGO="0")
    process = run_logged([
        item["path"], "--force-binary-path", str(thin), "--force-metadata-path", str(metadata),
        "--force-unity-version", version, "--use-processor", "attributeanalyzer",
        "--output-as", _MODES[mode], "--output-to", str(output),
    ], work_dir, f"cpp2il-{run_id}.log", timeout=300 if mode == "schemas" else 180,
        env=environment)
    files = _managed_files(output)
    analyses = sorted(output.rglob("*.txt")) if output.is_dir() else []
    enough_output = bool(analyses if mode == "analysis" else files)
    ready = not process["returncode"] and not process["timed_out"] and enough_output
    failure_kind = None
    if not ready:
        diagnostic = Path(process["log"]).read_text(encoding="utf-8", errors="replace")[-16384:]
        if process["timed_out"]:
            failure_kind = "timeout"
        elif "AccessViolationException" in diagnostic:
            failure_kind = "access_violation"
        elif process["returncode"]:
            failure_kind = "process_error"
        else:
            failure_kind = "missing_output"
    return {"status": "ready" if ready else "failed", "tool": "cpp2il", "version": item["version"],
            "output_dir": str(output), "assembly_files": [str(path) for path in files],
            "analysis_file_count": len(analyses), "process": process, "failure_kind": failure_kind,
            "runtime_controls": {"processor_count": 1, "tiered_compilation": False, "tiered_pgo": False},
            "error": None if ready else "Cpp2IL did not finish with the expected output; inspect its log"}


def _managed_files(directory: Path) -> list[Path]:
    """Include valid extensionless assembly files, such as __Generated."""
    files = []
    if directory.is_dir():
        for path in sorted(directory.rglob("*")):
            if path.is_file():
                with path.open("rb") as stream:
                    if stream.read(2) == b"MZ":
                        files.append(path)
    return files


def _dumper(thin: Path, metadata: Path, work_dir: Path, run_id: str) -> dict:
    installed = bootstrap(work_dir, ["dumper"])
    item = installed["tools"]["dumper"]
    if item["status"] != "ready":
        return {"tool": "dumper", "status": "failed", "error": item.get("error", "Bootstrap failed")}
    output = managed_path(work_dir, "recovery", run_id, "dumper")
    output.mkdir(parents=True, exist_ok=False)
    runtime = managed_path(work_dir, "tools", "runtime6")
    process = run_logged([str(runtime / "dotnet"), item["path"], str(thin), str(metadata), str(output)],
                         work_dir, f"dumper-{run_id}.log", timeout=300,
                         env=dotnet_environment(work_dir, runtime))
    files = _managed_files(output / "DummyDll")
    ready = (not process["returncode"] and not process["timed_out"] and bool(files)
             and (output / "dump.cs").is_file() and (output / "script.json").is_file())
    return {"status": "ready" if ready else "failed", "tool": "dumper", "version": item["version"],
            "output_dir": str(output), "assembly_files": [str(path) for path in files],
            "native_addresses_verified": False,
            "limitations": ["Dumper native method addresses are unverified; this Unity metadata31.1 variant showed address drift against named native symbols."],
            "process": process, "error": None if ready else "Dumper did not emit complete schema/address evidence"}


def recover_code(input_path: Path, work_dir: Path, mode: str = "schemas") -> dict:
    """Recover into a fresh cache run; never promote experimental code to Assets.

    'schemas' emits managed declarations. 'bodies' and 'analysis' retain
    experimental evidence and report it as unverified. Cpp2IL failures receive
    an independent Dumper schema attempt; a fallback cannot satisfy body mode.
    """
    if mode not in _MODES:
        raise ValueError("Code recovery mode must be schemas, bodies, or analysis")
    work_dir = validate_work_dir(work_dir)
    app, binary, metadata_path, version = _resolve_input(input_path)
    try:
        app.relative_to(work_dir)
    except ValueError:
        pass
    else:
        raise ValueError("Recovery input cannot be inside the generated work directory")
    metadata = parse_metadata(metadata_path)
    binary_sha = sha256_file(binary)
    thin, architecture = _thin_binary(binary, work_dir, binary_sha)
    run_id = mode + "-" + uuid.uuid4().hex[:12]
    attempt = _cpp2il(thin, metadata_path, version, work_dir, mode, run_id)
    report = {
        "schema_version": 1, "status": attempt["status"], "mode": mode, "run_id": run_id,
        "source": {"app": str(app), "unity_version": version, "metadata_version": metadata["version"],
                   "metadata_sha256": sha256_file(metadata_path), "binary_sha256": binary_sha,
                   "architecture": architecture["name"], "analysis_binary": str(thin)},
        "tool": attempt["tool"], "output_dir": attempt.get("output_dir"),
        "log": attempt.get("process", {}).get("log"), "attempts": [attempt],
        "counts": {"input_assemblies": metadata["image_count"], "input_types": metadata["type_count"],
                   "input_methods": metadata["method_count"], "input_fields": metadata["field_count"],
                   "emitted_assemblies": len(attempt.get("assembly_files", []))},
        "limitations": ["Declarations and native address mappings are not recovered executable game behavior.",
                        "Recovered method bodies require independent semantic review and gameplay tests.",
                        "No extracted or experimental assemblies are automatically installed into Assets."],
        "verified_gameplay_methods": 0, "playable_game": False,
    }
    if attempt["status"] != "ready":
        fallback = _dumper(thin, metadata_path, work_dir, run_id)
        report["attempts"].append(fallback)
        report["fallback"] = fallback
        report["limitations"].extend(fallback.get("limitations", []))
        if mode == "schemas" and fallback["status"] == "ready":
            attempt = fallback
            report.update(status="ready", tool="dumper", output_dir=fallback["output_dir"],
                          log=fallback["process"]["log"])
            report["counts"]["emitted_assemblies"] = len(fallback["assembly_files"])
        elif fallback["status"] == "ready":
            report["limitations"].append("Fallback produced declaration-only evidence; requested body analysis remains incomplete.")
    files = attempt.get("assembly_files", [])
    if files:
        try:
            quality = _inspect_managed_output(Path(attempt["output_dir"]), work_dir, run_id,
                                              schemas=mode == "schemas" or attempt["tool"] == "dumper")
            report["quality"] = quality
            report["counts"]["output_methods"] = sum(row["methods"] for row in quality["assemblies"])
            report["counts"]["output_types"] = sum(row["types"] for row in quality["assemblies"])
        except (ToolError, OSError, ValueError) as exc:
            report["limitations"].append(f"Managed-output inspection could not finish: {exc}")
    if report["status"] == "ready" and mode == "schemas":
        names = {Path(path).stem for path in files}
        missing = sorted(image["name"] for image in metadata["images"]
                         if Path(image["name"]).stem not in names)
        report["missing_assemblies"] = missing
        if missing:
            report["status"] = "incomplete"
            report["limitations"].append("Some input assembly declarations were not emitted.")
    output = managed_path(work_dir, "reports", f"code-recovery-{mode}.json")
    report["report_path"] = str(output)
    write_json(output, report)
    return report

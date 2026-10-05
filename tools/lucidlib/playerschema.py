"""Bounded genuine player metadata inventories; comparison and bindings stay separate."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import subprocess
import uuid

from . import bindings, playercode
from .bootstrap import dotnet_environment, load_lock, managed_path, validate_work_dir, write_json
from .verification import UNITY_VERSION, artifact_fingerprint, find_editor

PROFILE = "zone-theme-v1"
MAX_SCHEMA = 32 * 1024 * 1024
ROOTS = {
    ("Game.Runtime", "HardlightProject.ZoneThemeOverride"),
    ("UnityEngine.CoreModule", "UnityEngine.Color"),
    ("UnityEngine.CoreModule", "UnityEngine.Sprite"),
    ("Unity.Addressables", "UnityEngine.AddressableAssets.AssetReference"),
    ("Unity.Addressables", "UnityEngine.AddressableAssets.AssetReferenceT`1"),
    ("Unity.Addressables", "UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite"),
    ("HLUnityCore.Runtime", "Hardlight.ShowIfAttribute"),
    ("HLUnityCore.Runtime", "Hardlight.InspectorConditionalField"),
    ("HLUnityCore.Runtime", "Hardlight.InspectorConditionalAttribute+ComparisonType"),
}
# Exact names are filled by the versioned profile, with no namespace wildcard.
ALLOWED = {
    ('Game.Runtime', 'HardlightProject.ZoneThemeOverride'),
    ('HLUnityCore.Runtime', 'Hardlight.InspectorConditionalAttribute'),
    ('HLUnityCore.Runtime', 'Hardlight.InspectorConditionalAttribute+ComparisonType'),
    ('HLUnityCore.Runtime', 'Hardlight.InspectorConditionalField'),
    ('HLUnityCore.Runtime', 'Hardlight.ShowIfAttribute'),
    ('HLUnityCore.Runtime', 'Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute'),
    ('HLUnityCore.Runtime', 'Unity.IL2CPP.CompilerServices.Option'),
    ('Unity.Addressables', 'UnityEngine.AddressableAssets.AssetReference'),
    ('Unity.Addressables', 'UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite'),
    ('Unity.Addressables', 'UnityEngine.AddressableAssets.AssetReferenceT`1'),
    ('UnityEngine.CoreModule', 'UnityEngine.Color'),
    ('UnityEngine.CoreModule', 'UnityEngine.CreateAssetMenuAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ExcludeFromPresetAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ExtensionOfNativeClassAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.Object'),
    ('UnityEngine.CoreModule', 'UnityEngine.PropertyAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ScriptableObject'),
    ('UnityEngine.CoreModule', 'UnityEngine.Serialization.FormerlySerializedAsAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.SerializeField'),
    ('UnityEngine.CoreModule', 'UnityEngine.Sprite'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeHeaderAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeTypeAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.VisibleToOtherModulesAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.NativeClassAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.RequiredByNativeCodeAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.UsedByNativeCodeAttribute'),
    ('netstandard', 'System.Attribute'),
    ('netstandard', 'System.AttributeTargets'),
    ('netstandard', 'System.AttributeUsageAttribute'),
    ('netstandard', 'System.ComponentModel.EditorBrowsableAttribute'),
    ('netstandard', 'System.ComponentModel.EditorBrowsableState'),
    ('netstandard', 'System.Diagnostics.DebuggerBrowsableAttribute'),
    ('netstandard', 'System.Diagnostics.DebuggerBrowsableState'),
    ('netstandard', 'System.Enum'),
    ('netstandard', 'System.FlagsAttribute'),
    ('netstandard', 'System.Object'),
    ('netstandard', 'System.Reflection.DefaultMemberAttribute'),
    ('netstandard', 'System.Runtime.CompilerServices.CompilerGeneratedAttribute'),
    ('netstandard', 'System.Runtime.CompilerServices.IsReadOnlyAttribute'),
    ('netstandard', 'System.ValueType'),
}


def _read_json(path, limit=MAX_SCHEMA):
    raw = playercode._regular(path, limit).read_bytes()
    value = playercode._strict_json(raw)
    if not isinstance(value, dict): raise ValueError("Player schema evidence must be a JSON object")
    return raw, value


def _same_file(path, record):
    actual = playercode._file_record(path)
    if any(type(record.get(k)) is not type(v) or record.get(k) != v for k, v in actual.items()):
        raise ValueError("Sealed player schema input changed")


def _player_evidence(root, work, target, source, engine):
    """Recheck the actual returned outputs and their staged compiler receipt."""
    path = managed_path(work, "player-code", "latest-" + target + ".json")
    raw, receipt = _read_json(path)
    expected = {"command": "player-code", "status": "compiled", "identity_status": "complete",
                "generated_by": "ProjectLucid.run_player_code", "target": target,
                "source_fingerprint_before": source, "source_fingerprint_after": source,
                "player_schema_verified": False, "gameplay_verified": False}
    if any(type(receipt.get(k)) is not type(v) or receipt.get(k) != v for k, v in expected.items()):
        raise ValueError("A current genuine successful player compilation is required")
    nonce = receipt.get("identity_nonce")
    if not isinstance(nonce, str) or re.fullmatch(r"[a-f0-9]{32}", nonce) is None:
        raise ValueError("Invalid genuine player run identity")
    run = managed_path(work, "player-code", "runs", nonce)
    pending = managed_path(work, "player-code", "runs", nonce, "pending.json")
    context = managed_path(work, "player-code", "runs", nonce, "context.txt")
    staged_raw, staged = _read_json(pending)
    if (receipt.get("run") != str(run) or receipt.get("staged_report_path") != str(pending) or
            receipt.get("staged_report_sha256") != hashlib.sha256(staged_raw).hexdigest() or
            any(receipt.get(k) != v for k, v in staged.items() if k not in ("status", "identity_status"))):
        raise ValueError("Published player evidence differs from its genuine staged report")
    context_raw = playercode._regular(context, 16384).read_bytes()
    if context_raw != playercode._context(root, work, run, target, source, nonce, engine):
        raise ValueError("Player compiler identity context is stale or altered")
    digest = hashlib.sha256(context_raw).hexdigest()
    playercode._check_report(staged, root, work, run, target, nonce, source, engine, digest)
    return {"receipt_path": str(path), "receipt_sha256": hashlib.sha256(raw).hexdigest(),
            "receipt_raw": raw, "staged_raw": staged_raw, "context_raw": context_raw,
            "receipt": receipt, "staged": staged}


def _modules(evidence):
    records = []
    for entry in evidence["staged"]["modules"]:
        records.append({k: entry[k] for k in ("path", "size", "sha256", "assembly_name", "mvid")})
        records[-1]["kind"] = "returned_player_assembly"
    for entry in evidence["staged"]["inputs"]:
        if entry["kind"] != "precompiled_reference": continue
        if Path(entry["path"]).suffix.lower() != ".dll":
            raise ValueError("Non-managed reference is unsupported by the exact player metadata profile")
        _same_file(entry["path"], entry)
        records.append({k: entry[k] for k in ("path", "size", "sha256")})
        records[-1].update(playercode._pe_identity(entry["path"]), kind="sealed_plan_reference")
    names, paths = set(), set()
    for entry in records:
        entry["name"] = ""
        if entry["assembly_name"] in names or entry["path"] in paths:
            raise ValueError("Ambiguous sealed metadata resolver candidates")
        names.add(entry["assembly_name"]); paths.add(entry["path"])
    if not 1 <= len(records) <= 4096: raise ValueError("Invalid sealed metadata candidate count")
    return sorted(records, key=lambda r: r["assembly_name"])


def _tree_records(directory, work):
    result = []
    for parent, directories, names in os.walk(directory):
        for name in directories:
            managed_path(work, * (Path(parent) / name).relative_to(work).parts)
        for name in sorted(names):
            path = managed_path(work, * (Path(parent) / name).relative_to(work).parts)
            result.append(dict(relative_path=path.relative_to(directory).as_posix(), **playercode._file_record(path)))
    return sorted(result, key=lambda r: r["relative_path"])


def _build_reader(root, work, run, engine):
    project = playercode._regular(root / "tools/player-schema/PlayerSchema.csproj", 65536)
    code = playercode._regular(project.with_name("Program.cs"), 1024 * 1024)
    sources = [playercode._file_record(project), playercode._file_record(code)]
    lock = load_lock()
    sdk = lock.get("artifacts", {}).get("dotnet", {})
    if sdk.get("version") != "10.0.401" or not isinstance(sdk.get("sha512"), str) or re.fullmatch(r"[a-f0-9]{128}", sdk["sha512"]) is None:
        raise ValueError("Player metadata reader requires the pinned .NET SDK")
    host = managed_path(work, "tools", "dotnet", "dotnet")
    host_seal = playercode._file_record(host)
    env = dotnet_environment(work, host.parent)
    version = subprocess.run([str(host), "--version"], capture_output=True, text=True, timeout=30, env=env)
    if version.returncode or version.stdout.strip() != sdk["version"]:
        raise ValueError("Installed metadata SDK version differs from tool lock")
    managed = Path(next(e["path"] for e in engine if e["name"] == "cecil")).parent
    output, intermediate = run / "reader", run / "obj"
    output.mkdir(exist_ok=False); intermediate.mkdir(exist_ok=False)
    log = managed_path(work, *run.relative_to(work).parts, "build.log")
    command = [str(host), "build", str(project), "--nologo", "-c", "Release",
               "-p:UnityManagedRoot=" + str(managed), "-p:BaseIntermediateOutputPath=" + str(intermediate) + os.sep,
               "-p:OutputPath=" + str(output) + os.sep, "-p:GeneratePackageOnBuild=false"]
    with log.open("xb") as stream:
        result = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, timeout=180, env=env)
    if result.returncode: raise ValueError("Player metadata reader compilation failed; inspect its build log")
    binary = playercode._regular(output / "ProjectLucid.PlayerSchema.dll")
    for entry in sources: _same_file(entry["path"], entry)
    _same_file(host, host_seal)
    return {"host": host_seal, "sdk_version": sdk["version"], "sdk_archive_sha512": sdk["sha512"],
            "sources": sources, "binary": dict(playercode._file_record(binary), **playercode._pe_identity(binary)),
            "output": _tree_records(output, work), "log": str(log), "env": env}


def _attributes(record, module, owner_token):
    bindings._attributes(record, require_string_evidence=True)
    for entry in record["custom_attributes"]:
        blob = entry.get("ecma_blob_hex")
        if not isinstance(blob, str) or re.fullmatch(r"[a-f0-9]{8,2097152}", blob) is None or len(blob) % 2:
            raise ValueError("Player attribute raw ECMA blob is absent or unbounded")
        raw = bytes.fromhex(blob)
        expected = {"module_path": module["path"], "module_sha256": module["sha256"],
                    "module_mvid": module["mvid"], "owner_token": owner_token}
        if (entry.get("ecma_decoding_complete") is not True or entry.get("ecma_blob_sha256") != hashlib.sha256(raw).hexdigest() or
                entry.get("ecma_provenance") != expected):
            raise ValueError("Player attribute blob provenance differs from its owning metadata module")
        # This exact ambiguous constructor must preserve the primitive string
        # tag as well as its null value. Never accept a generic null replacement.
        if entry.get("full_name") == "Hardlight.ShowIfAttribute":
            args = entry.get("arguments")
            params = entry.get("constructor_parameter_types")
            if not isinstance(args, list) or len(args) != 2 or not isinstance(params, list) or len(params) != 2:
                raise ValueError("ShowIf constructor metadata is incomplete")
            value = args[1]
            if (params[0].get("canonical_name") != "System.String" or params[1].get("canonical_name") != "System.Object" or
                    entry["fields"] or entry["properties"] or not raw.endswith(bytes.fromhex("0eff0000")) or
                    value.get("kind") != "primitive" or value.get("type") != "IL2CPP_TYPE_STRING" or value.get("value") is not None or
                    value.get("value_complete") is not True or value.get("string_length") != -1 or value.get("ecma_tag") != 14 or
                    value.get("ecma_boxed_tag") != 14 or value.get("ecma_boxed") is not True or
                    value.get("raw_encoding_sha256") != hashlib.sha256(b"\xff").hexdigest()):
                raise ValueError("ShowIf boxed null string tag evidence is missing or altered")


def _schema_check(report, manifest, manifest_raw, reader):
    expected = {"schema_version": 1, "command": "player-schema", "status": "pending-native-verification",
                "identity_status": "pending-wrapper", "metadata_graph_complete": True, "references_modified": False,
                "player_schema_verified": False, "remap_approved": False, "gameplay_verified": False,
                "compiler_response_files_verified": False, "resolver_provenance": "metadata-resolution-over-sealed-plan-candidates",
                "manifest_sha256": hashlib.sha256(manifest_raw).hexdigest()}
    for key in ("profile", "target", "nonce", "source_fingerprint", "player_receipt_sha256", "project_root", "work_root", "run"):
        expected[key] = manifest[key]
    if any(type(report.get(k)) is not type(v) or report.get(k) != v for k, v in expected.items()) or report.get("errors") != []:
        raise ValueError("Player metadata inventory is stale, incomplete or incorrectly approved")
    for field, name in (("reader", "cecil"), ("serializer", "serialization")):
        seal = next(e for e in manifest["engine"] if e["name"] == name)
        actual_path = str(Path(reader["binary"]["path"]).parent / Path(seal["path"]).name)
        if report.get(field) != {"path": actual_path, "sha256": seal["sha256"], "mvid": seal["mvid"]}:
            raise ValueError("Inventory used a different installed metadata reader or serializer")
    index = bindings._index(report)
    if (not ROOTS <= index.keys() or set(index) != ALLOWED or not 1 <= len(index) <= 128 or
            type(report.get("type_count")) is not int or report["type_count"] != len(index) or
            report.get("assembly_count") != len(report["assemblies"])):
        raise ValueError("Player inventory differs from its exact bounded profile")
    modules = {m["assembly_name"]: m for m in manifest["modules"]}
    for assembly in report["assemblies"]:
        module = modules.get(assembly["name"])
        if module is None or assembly.get("module") != module:
            raise ValueError("Player inventory assembly is outside the sealed resolver candidates")
        for record in assembly["types"]:
            token = record.get("token")
            if not isinstance(token, str) or re.fullmatch(r"0x02[a-f0-9]{6}", token) is None or record.get("module_provenance") != module:
                raise ValueError("Player type token/module provenance is incomplete")
            bindings._candidate_fields(record, require_string_evidence=True)
            _attributes(record, module, token)
            if record.get("base_type") is not None: bindings.type_identity(record["base_type"])
            if type(record.get("attributes")) is not int or any(type(record.get(k)) is not bool for k in ("is_value_type", "is_enum", "is_abstract", "builtin_serializer_eligibility")):
                raise ValueError("Player type/eligibility flags are incomplete")
            params = record.get("generic_parameters")
            if not isinstance(params, list) or len(params) > 32:
                raise ValueError("Player generic parameter graph is absent or oversized")
            for i, parameter in enumerate(params):
                if (parameter.get("index") != i or type(parameter.get("attributes")) is not int or
                        parameter.get("constraints_complete") is not True or not isinstance(parameter.get("constraints"), list)):
                    raise ValueError("Player generic constraints are incomplete")
                for constraint in parameter["constraints"]: bindings.type_identity(constraint)
            if record["is_enum"]: bindings.type_identity(record.get("enum_underlying_type"))
            for field in record["fields"]:
                if (not isinstance(field.get("token"), str) or re.fullmatch(r"0x04[a-f0-9]{6}", field["token"]) is None or
                        field.get("default_value_complete") is not True or type(field.get("has_default_value")) is not bool or
                        field.get("unity_type_eligibility_verified") is not True or type(field.get("will_unity_serialize")) is not bool):
                    raise ValueError("Player field constants or installed serializer eligibility are incomplete")
                _attributes(field, module, field["token"])
                if field["has_default_value"]: bindings.type_identity(field.get("default_value_type"))
    edges = report.get("closure_edges")
    if not isinstance(edges, list) or not 1 <= len(edges) <= 16384 or any(
            not isinstance(e, dict) or e.get("type") not in {a + ":" + t for a, t in index} or
            e.get("reason") not in ("profile-root", "base", "generic-constraint", "serialized-field", "custom-attribute", "attribute-value") for e in edges):
        raise ValueError("Player metadata closure evidence is incomplete")


def run_player_schema(repo_root, work_dir, target, profile=PROFILE):
    """Generate metadata only from a current sealed genuine player compilation."""
    if target not in playercode.TARGETS or profile != PROFILE:
        raise ValueError("Unsupported player metadata target or exact profile")
    root, work = Path(repo_root).resolve(strict=True), validate_work_dir(Path(work_dir))
    version = playercode._regular(root / "ProjectSettings/ProjectVersion.txt", 65536).read_text()
    if re.search(r"^m_EditorVersion: " + re.escape(UNITY_VERSION) + "$", version, re.M) is None:
        raise ValueError("Player metadata requires the matching project Unity version")
    latest = managed_path(work, "player-schema", "latest-" + target + "-" + profile + ".json")
    if latest.exists(): playercode._regular(latest, MAX_SCHEMA)
    lock = managed_path(work, "locks", "player-schema.lock")
    lock.parent.mkdir(parents=True, exist_ok=True)
    try: lock.mkdir()
    except FileExistsError as error: raise ValueError("Another player metadata run owns the generated lock") from error
    run, created = None, False
    try:
        source = artifact_fingerprint(root)
        editor = playercode._regular(find_editor(), 2 * 1024**3)
        engine = playercode._engine_identity(editor)
        evidence = _player_evidence(root, work, target, source, engine)
        modules = _modules(evidence)
        nonce = uuid.uuid4().hex
        run = managed_path(work, "player-schema", "runs", nonce)
        run.mkdir(parents=True, exist_ok=False); created = True
        reader = _build_reader(root, work, run, engine)
        manifest = {"schema_version": 1, "command": "player-schema", "profile": profile, "target": target,
                    "nonce": nonce, "source_fingerprint": source, "project_root": str(root), "work_root": str(work),
                    "run": str(run), "player_receipt_sha256": evidence["receipt_sha256"], "modules": modules, "engine": engine}
        manifest_path = managed_path(work, *run.relative_to(work).parts, "manifest.json")
        pending = managed_path(work, *run.relative_to(work).parts, "pending.json")
        write_json(manifest_path, manifest)
        manifest_raw = playercode._regular(manifest_path, 4 * 1024 * 1024).read_bytes()
        if artifact_fingerprint(root) != source: raise ValueError("Source changed before player metadata analysis")
        log = managed_path(work, *run.relative_to(work).parts, "reader.log")
        with log.open("xb") as stream:
            process = subprocess.run([reader["host"]["path"], reader["binary"]["path"], str(manifest_path), str(pending)],
                                     stdout=stream, stderr=subprocess.STDOUT, timeout=180, env=reader["env"])
        if process.returncode: raise ValueError("Player metadata reader failed; inspect its run log")
        pending_raw, report = _read_json(pending)
        _schema_check(report, manifest, manifest_raw, reader)
        for _ in range(2):
            if playercode._regular(manifest_path, 4 * 1024 * 1024).read_bytes() != manifest_raw or playercode._regular(pending, MAX_SCHEMA).read_bytes() != pending_raw:
                raise ValueError("Player metadata context or pending evidence changed")
            again = _player_evidence(root, work, target, source, engine)
            if any(again[k] != evidence[k] for k in ("receipt_raw", "staged_raw", "context_raw")) or _modules(again) != modules:
                raise ValueError("Genuine player compilation changed during metadata analysis")
            for record in [reader["host"], *reader["sources"], *reader["output"]]: _same_file(record["path"], record)
            if _tree_records(Path(reader["binary"]["path"]).parent, work) != reader["output"]:
                raise ValueError("Metadata reader output manifest changed")
            if artifact_fingerprint(root) != source or playercode._engine_identity(editor) != engine:
                raise ValueError("Source or installed engine changed during player metadata analysis")
            if artifact_fingerprint(root) != source:
                raise ValueError("Source changed during final installed engine identity check")
        _schema_check(report, manifest, manifest_raw, reader)
        report.update(status="metadata-ready", identity_status="complete", generated_by="ProjectLucid.run_player_schema",
                      identity_verified_by="native-python-prepost-player-pe-cecil-metadata-v1", staged_report_path=str(pending),
                      staged_report_sha256=hashlib.sha256(pending_raw).hexdigest(), player_receipt_path=evidence["receipt_path"],
                      reader_build={k: v for k, v in reader.items() if k != "env"}, log=str(log))
        latest = managed_path(work, "player-schema", "latest-" + target + "-" + profile + ".json")
        if latest.exists(): playercode._regular(latest, MAX_SCHEMA)
        if artifact_fingerprint(root) != source or playercode._regular(pending, MAX_SCHEMA).read_bytes() != pending_raw or playercode._regular(manifest_path, 4 * 1024 * 1024).read_bytes() != manifest_raw:
            raise ValueError("Source or staged evidence changed immediately before metadata publication")
        write_json(latest, report)
        return {"status": "metadata-ready", "target": target, "profile": profile, "type_count": report["type_count"],
                "report_path": str(latest), "run": str(run), "player_schema_verified": False, "remap_approved": False}
    except (OSError, ValueError, subprocess.SubprocessError, bindings.LayoutError) as error:
        if created and run is not None and run.is_dir():
            failure = managed_path(work, *run.relative_to(work).parts, "wrapper-failure.json")
            write_json(failure, {"status": "failed", "target": target, "profile": profile, "error": str(error),
                                 "generated_by": "ProjectLucid.run_player_schema"})
        raise
    finally:
        lock.rmdir()

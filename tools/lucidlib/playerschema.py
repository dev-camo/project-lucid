"""Bounded genuine player metadata inventories; comparison and bindings stay separate."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import subprocess
import uuid

from . import bindings, playercode
from .bootstrap import (run_owned, NO_BUILD_SERVER_ARGS, dotnet_environment, host_lock, load_lock, managed_path,
                        validate_work_dir, write_json, reader_build_paths, tool_work_dir,
                        verified_tool_path, take_work_lock, release_work_lock)
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


# The URP 14.0.11 renderer profile: the five package types whose Editor
# declarations carry UNITY_EDITOR-only members (URPHelpURL is
# [Conditional("UNITY_EDITOR")]; ShaderKeywordFilter and the prefilter fields
# sit in #if UNITY_EDITOR). Their player compile is the shipped layout.
URP_PROFILE = "urp-renderer-v1"
URP_ROOTS = {
    ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"),
    ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal.UniversalRendererData"),
    ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings"),
    ("Unity.RenderPipelines.Universal.Runtime", "FullScreenPassRendererFeature"),
    ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal.DecalRendererFeature"),
}
# The complete closure the reader may reach from those roots, by exact name.
URP_ALLOWED = {
    ('Unity.RenderPipelines.Core.Runtime', 'UnityEngine.Rendering.ReloadAttribute'),
    ('Unity.RenderPipelines.Core.Runtime', 'UnityEngine.Rendering.ReloadAttribute+Package'),
    ('Unity.RenderPipelines.Core.Runtime', 'UnityEngine.Rendering.ReloadGroupAttribute'),
    ('Unity.RenderPipelines.Core.Runtime', 'UnityEngine.Rendering.ShaderVariantLogLevel'),
    ('Unity.RenderPipelines.Universal.Runtime', 'FullScreenPassRendererFeature'),
    ('Unity.RenderPipelines.Universal.Runtime', 'FullScreenPassRendererFeature+InjectionPoint'),
    ('Unity.RenderPipelines.Universal.Runtime', 'FullScreenPassRendererFeature+Version'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ColorGradingMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.CopyDepthMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DBufferSettings'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DecalNormalBlend'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DecalRendererFeature'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DecalScreenSpaceSettings'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DecalSettings'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DecalSurfaceData'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DecalTechniqueOption'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DepthPrimingMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.DisallowMultipleRendererFeature'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.Downsampling'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.HDRColorBufferPrecision'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.IntermediateTextureMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.LODCrossFadeDitheringType'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.LightCookieFormat'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.LightCookieResolution'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.LightRenderingMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.MsaaQuality'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.PipelineDebugLevel'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.PostProcessData'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.PostProcessData+ShaderResources'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.PostProcessData+TextureResources'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.RendererType'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.RenderingMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ScriptableRenderPassInput'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ScriptableRendererData'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ScriptableRendererData+DebugShaderResources'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ScriptableRendererFeature'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ShEvalMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ShadowCascadesOption'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ShadowQuality'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.ShadowResolution'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.SoftShadowQuality'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.StencilStateData'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.StoreActionsOptimization'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset+TextureResources'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.UniversalRendererData'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.UniversalRendererData+ShaderResources'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.UpscalingFilterSelection'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.VolumeFrameworkUpdateMode'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.XRSystemData'),
    ('Unity.RenderPipelines.Universal.Runtime', 'UnityEngine.Rendering.Universal.XRSystemData+ShaderResources'),
    ('UnityEngine.CoreModule', 'Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstructionAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ExcludeFromPresetAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ExtensionOfNativeClassAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.HelpURLAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.HideInInspector'),
    ('UnityEngine.CoreModule', 'UnityEngine.InspectorNameAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.LayerMask'),
    ('UnityEngine.CoreModule', 'UnityEngine.Material'),
    ('UnityEngine.CoreModule', 'UnityEngine.Object'),
    ('UnityEngine.CoreModule', 'UnityEngine.PropertyAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.Rendering.CompareFunction'),
    ('UnityEngine.CoreModule', 'UnityEngine.Rendering.RenderPipelineAsset'),
    ('UnityEngine.CoreModule', 'UnityEngine.Rendering.RenderPipelineGlobalSettings'),
    ('UnityEngine.CoreModule', 'UnityEngine.Rendering.StencilOp'),
    ('UnityEngine.CoreModule', 'UnityEngine.ScriptableObject'),
    ('UnityEngine.CoreModule', 'UnityEngine.SerializeField'),
    ('UnityEngine.CoreModule', 'UnityEngine.Shader'),
    ('UnityEngine.CoreModule', 'UnityEngine.Texture'),
    ('UnityEngine.CoreModule', 'UnityEngine.Texture2D'),
    ('UnityEngine.CoreModule', 'UnityEngine.TooltipAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.Vector2'),
    ('UnityEngine.CoreModule', 'UnityEngine.Vector3'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeHeaderAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeNameAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeTypeAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.VisibleToOtherModulesAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.NativeClassAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.RequiredByNativeCodeAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.UsedByNativeCodeAttribute'),
    ('netstandard', 'System.Attribute'),
    ('netstandard', 'System.AttributeTargets'),
    ('netstandard', 'System.AttributeUsageAttribute'),
    ('netstandard', 'System.Collections.Generic.List`1'),
    ('netstandard', 'System.ComponentModel.EditorBrowsableAttribute'),
    ('netstandard', 'System.ComponentModel.EditorBrowsableState'),
    ('netstandard', 'System.Diagnostics.DebuggerBrowsableAttribute'),
    ('netstandard', 'System.Diagnostics.DebuggerBrowsableState'),
    ('netstandard', 'System.Enum'),
    ('netstandard', 'System.FlagsAttribute'),
    ('netstandard', 'System.Object'),
    ('netstandard', 'System.ObsoleteAttribute'),
    ('netstandard', 'System.Reflection.DefaultMemberAttribute'),
    ('netstandard', 'System.Runtime.CompilerServices.CompilerGeneratedAttribute'),
    ('netstandard', 'System.ValueType'),
}
PROFILES = {PROFILE: (ROOTS, ALLOWED), URP_PROFILE: (URP_ROOTS, URP_ALLOWED)}


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
    if context_raw != playercode._context(root, work, run, target, source, nonce, engine, schema_version=staged.get("schema_version")):
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
    host_work = tool_work_dir(work)
    if directory == host_work or host_work in directory.parents:
        work = host_work
    result = []
    for parent, directories, names in os.walk(directory):
        for name in directories:
            managed_path(work, * (Path(parent) / name).relative_to(work).parts)
        for name in sorted(names):
            path = managed_path(work, * (Path(parent) / name).relative_to(work).parts)
            result.append(dict(relative_path=path.relative_to(directory).as_posix(), **playercode._file_record(path)))
    return sorted(result, key=lambda r: r["relative_path"])


def _build_reader(root, work, run, engine):
    project_root, work, run = reader_build_paths(root, work, run, "player-schema")
    project = playercode._regular(project_root / "PlayerSchema.csproj", 65536)
    code = playercode._regular(project.with_name("Program.cs"), 1024 * 1024)
    sources = [playercode._file_record(project), playercode._file_record(code)]
    lock = load_lock()
    sdk = host_lock(lock)["artifacts"].get("dotnet", {})
    if sdk.get("version") != "10.0.401" or not isinstance(sdk.get("sha512"), str) or re.fullmatch(r"[a-f0-9]{128}", sdk["sha512"]) is None:
        raise ValueError("Player metadata reader requires the pinned .NET SDK")
    host = verified_tool_path(work, "dotnet")
    host_seal = playercode._file_record(host)
    env = dotnet_environment(work, host.parent)
    version = run_owned([str(host), "--version"], capture_output=True, text=True, timeout=30, env=env, cwd=work)
    if version.returncode or version.stdout.strip() != sdk["version"]:
        raise ValueError("Installed metadata SDK version differs from tool lock")
    managed = Path(next(e["path"] for e in engine if e["name"] == "cecil")).parent
    output, intermediate = run / "reader", run / "obj"
    output.mkdir(exist_ok=False); intermediate.mkdir(exist_ok=False)
    log = managed_path(work, *run.relative_to(work).parts, "build.log")
    command = [str(host), "build", str(project), "--nologo", "-c", "Release",
               "-p:UnityManagedRoot=" + str(managed), "-p:BaseIntermediateOutputPath=" + str(intermediate) + os.sep,
               "-p:OutputPath=" + str(output) + os.sep, "-p:GeneratePackageOnBuild=false", *NO_BUILD_SERVER_ARGS]
    with log.open("xb") as stream:
        result = run_owned(command, stdout=stream, stderr=subprocess.STDOUT, timeout=180, env=env, cwd=work)
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
    roots, allowed = PROFILES[manifest["profile"]]
    if (not roots <= index.keys() or set(index) != allowed or not 1 <= len(index) <= 128 or
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
    if target not in playercode.TARGETS or profile not in PROFILES:
        raise ValueError("Unsupported player metadata target or exact profile")
    root, work = Path(repo_root).resolve(strict=True), validate_work_dir(Path(work_dir))
    version = playercode._regular(root / "ProjectSettings/ProjectVersion.txt", 65536).read_text()
    if re.search(r"^m_EditorVersion: " + re.escape(UNITY_VERSION) + "$", version, re.M) is None:
        raise ValueError("Player metadata requires the matching project Unity version")
    latest = managed_path(work, "player-schema", "latest-" + target + "-" + profile + ".json")
    if latest.exists(): playercode._regular(latest, MAX_SCHEMA)
    lock = take_work_lock(managed_path(work, "locks", "player-schema.lock"), "Another player metadata run owns the generated lock")
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
            process = run_owned([reader["host"]["path"], reader["binary"]["path"], str(manifest_path), str(pending)],
                                     stdout=stream, stderr=subprocess.STDOUT, timeout=180, env=reader["env"], cwd=tool_work_dir(work))
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
        # The serialization comparison applies Unity 2022.3.54f1's builtin value
        # rules only when both planes name that exact engine version.
        if evidence["receipt"].get("unity_version") != UNITY_VERSION:
            raise ValueError("Player compilation was made by a different Unity version")
        report.update(status="metadata-ready", unity_version=UNITY_VERSION, identity_status="complete", generated_by="ProjectLucid.run_player_schema",
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
        release_work_lock(lock)

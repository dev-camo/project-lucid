"""Measure exact original method identities with maintained compiled source bodies.

Counts measure code-work coverage, not native-byte matching or behavioral parity.
Raw evidence stays in the generated work directory; public output has counts and
content identities only. Generated declarations never supply a numerator.
"""
from __future__ import annotations

from collections import Counter, defaultdict
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import uuid

from . import native, playercode, playerschema, recovery
from .bootstrap import dotnet_environment, load_lock, managed_path, validate_work_dir, write_json
from .inspection import sha256_file
from .verification import UNITY_VERSION, artifact_fingerprint, find_editor

SCOPE_PATH = "tools/code-progress/scope.json"
HASH = re.compile(r"[0-9a-f]{64}\Z")
TOKEN = re.compile(r"0x06[0-9a-f]{6}\Z")
BODY_STATUSES = {"implementation_candidate", "no_body", "compiler_generated_unresolved",
                 "empty_body_withheld", "exception_placeholder_withheld", "throw_only_withheld",
                 "constant_or_default_body_withheld"}
COUNT_FIELDS = {"original_native_bodies", "implemented_source_bodies", "unresolved_identities", "withheld_bodies", "not_implemented", "original_declarations"}
SOURCE_STATUSES = {"verified", "no_sequence_points", "outside_maintained_runtime_source", "source_checksum_mismatch"}


def _json(path, limit=64 * 1024 * 1024):
    raw = playercode._regular(path, limit).read_bytes()
    value = playercode._strict_json(raw)
    if not isinstance(value, dict): raise ValueError("Code progress evidence must be an object")
    return raw, value


def _scope(root):
    raw, config = _json(Path(root) / SCOPE_PATH, 65536)
    names = config.get("assemblies")
    filters = config.get("namespace_filters")
    if (config.get("schema_version") != 2 or set(config) != {"schema_version", "assemblies", "namespace_filters"} or
            not isinstance(names, list) or not names or len(set(names)) != len(names) or
            any(not isinstance(name, str) or re.fullmatch(r"Game\.Runtime|HL[A-Za-z0-9]+(?:\.Runtime)?|Assembly-CSharp(?:-firstpass)?", name) is None for name in names) or
            filters != {"Assembly-CSharp": ["HardlightProject."], "Assembly-CSharp-firstpass": ["Hardlight."]}):
        raise ValueError("Invalid fixed proprietary assembly scope")
    return sorted(names), filters, hashlib.sha256(raw).hexdigest()


def _signature(value):
    if not isinstance(value, dict) or set(value) != {"owner", "name", "is_static", "return_type", "parameters", "generic_parameters", "declaring_generic_parameters"}:
        raise ValueError("Incomplete exact method signature")
    for name in ("owner", "name", "return_type"):
        if not isinstance(value[name], str) or not value[name] or len(value[name]) > 32768:
            raise ValueError("Invalid exact signature text")
    if type(value["is_static"]) is not bool: raise ValueError("Invalid exact signature static flag")
    if not isinstance(value["parameters"], list) or len(value["parameters"]) > 4096: raise ValueError("Invalid exact parameter set")
    for parameter in value["parameters"]:
        if (not isinstance(parameter, dict) or set(parameter) != {"type", "direction"} or not isinstance(parameter["type"], str) or
                not parameter["type"] or type(parameter["direction"]) is not int or not 0 <= parameter["direction"] <= 3):
            raise ValueError("Invalid exact parameter graph")
    for key in ("generic_parameters", "declaring_generic_parameters"):
        if not isinstance(value[key], list) or len(value[key]) > 256: raise ValueError("Incomplete generic signature")
        for parameter in value[key]:
            if (not isinstance(parameter, dict) or set(parameter) != {"flags", "constraints"} or type(parameter["flags"]) is not int or
                    not 0 <= parameter["flags"] <= 65535 or not isinstance(parameter["constraints"], list) or
                    any(not isinstance(c, str) or not c for c in parameter["constraints"]) or
                    parameter["constraints"] != sorted(set(parameter["constraints"]))):
                raise ValueError("Invalid complete generic constraints")
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def measure_code_work(original, maintained, scope, namespace_filters=None):
    """Pure deterministic matching; the wrapper independently seals all inputs."""
    namespace_filters = namespace_filters or {}
    if len(scope) != len(set(scope)) or not scope or any(not isinstance(name, str) or re.fullmatch(r"Game\.Runtime|HL[A-Za-z0-9]+(?:\.Runtime)?|Assembly-CSharp(?:-firstpass)?", name) is None for name in scope):
        raise ValueError("Invalid assembly scope")
    if any(name.startswith("Assembly-CSharp") and namespace_filters.get(name) != (["Hardlight."] if name.endswith("firstpass") else ["HardlightProject."]) for name in scope):
        raise ValueError("Mixed proprietary/third-party assemblies require exact namespace filters")
    def selected(row):
        filters = namespace_filters.get(row["assembly"])
        return not filters or any(row.get("declaring_type", "").startswith(prefix) for prefix in filters)
    if original.get("kind") != "original-native-method-inventory-v1" or maintained.get("kind") != "maintained-player-source-body-inventory-v1":
        raise ValueError("Wrong method evidence kind")
    for inventory in (original, maintained):
        if inventory.get("native_byte_matching_verified") is not False or inventory.get("gameplay_verified") is not False:
            raise ValueError("Code presence cannot claim parity")
    original_rows = original.get("methods"); player_rows = maintained.get("methods")
    if not isinstance(original_rows, list) or not original_rows or not isinstance(player_rows, list): raise ValueError("Missing method inventories")
    original_index, player_index, player_names = defaultdict(list), defaultdict(list), defaultdict(list)
    seen = set(); original_tokens = set(); player_seen = set(); counts = {name: Counter() for name in scope}
    for row in original_rows:
        if not isinstance(row, dict) or row.get("assembly") not in scope or not TOKEN.fullmatch(row.get("token", "")) or not selected(row):
            raise ValueError("Original method outside fixed scope")
        identity = row["assembly"] + ":" + row.get("declaring_type", "") + ":" + row["token"]
        if identity != row.get("id") or identity in seen or not row.get("declaring_type"):
            raise ValueError("Original method identity duplicated or altered")
        if (row["assembly"], row["token"]) in original_tokens: raise ValueError("Duplicate original MethodDef token")
        original_tokens.add((row["assembly"], row["token"]))
        seen.add(identity)
        if type(row.get("native_body")) is not bool: raise ValueError("Missing native body classification")
        counts[row["assembly"]]["declarations"] += 1
        if not row["native_body"]: continue
        counts[row["assembly"]]["native_bodies"] += 1
        if row.get("signature") is not None:
            if row["signature"].get("owner") != row["declaring_type"] or row["signature"].get("name") != row["name"]:
                raise ValueError("Signature differs from original method owner")
            key = (row["assembly"], _signature(row["signature"]))
            original_index[key].append(row)
    for row in player_rows:
        if (not isinstance(row, dict) or row.get("assembly") not in scope or not TOKEN.fullmatch(row.get("token", "")) or
                row.get("body_status") not in BODY_STATUSES or row.get("source_status") not in SOURCE_STATUSES or not selected(row)):
            raise ValueError("Invalid maintained method classification")
        key_id = (row["assembly"], row["token"])
        if key_id in player_seen: raise ValueError("Duplicate maintained method token")
        player_seen.add(key_id)
        if not isinstance(row.get("declaring_type"), str) or not row["declaring_type"] or not isinstance(row.get("name"), str) or not row["name"]:
            raise ValueError("Maintained method owner/name is missing")
        player_names[(row["assembly"], row["declaring_type"], row["name"])].append(row)
        if row.get("signature") is None: continue
        signature = row["signature"]
        if signature.get("owner") != row.get("declaring_type") or signature.get("name") != row.get("name"):
            raise ValueError("Signature differs from maintained method owner")
        key = (row["assembly"], _signature(signature))
        player_index[key].append(row)
    method_map = []
    for row in sorted(original_rows, key=lambda r: r["id"]):
        if not row["native_body"]: continue
        signature = row.get("signature"); matched = None
        if signature is None: state = "unresolved_original_signature"
        else:
            key = (row["assembly"], _signature(signature)); candidates = player_index[key]
            if len(original_index[key]) != 1 or len(candidates) > 1: state = "ambiguous_identity"
            elif not candidates:
                state = "unresolved_signature_difference" if player_names[(row["assembly"], signature["owner"], signature["name"])] else "not_implemented"
            else:
                matched = candidates[0]
                if matched["body_status"] != "implementation_candidate": state = matched["body_status"]
                elif matched["source_status"] != "verified": state = matched["source_status"]
                elif not matched.get("source_documents") or not isinstance(matched.get("il_sha256"), str) or HASH.fullmatch(matched["il_sha256"]) is None:
                    raise ValueError("Verified source body has no sealed body/documents")
                else: state = "implemented_source_body"
        counts[row["assembly"]][state] += 1
        method_map.append({"original_id": row["id"], "status": state,
                           "maintained_token": matched["token"] if matched else None,
                           "il_sha256": matched.get("il_sha256") if matched else None})
    groups = []
    for name in sorted(scope):
        group = counts[name]
        groups.append({"assembly": name, "original_native_bodies": group["native_bodies"],
                       "implemented_source_bodies": group["implemented_source_body"],
                       "unresolved_identities": sum(group[k] for k in ("unresolved_original_signature", "unresolved_signature_difference", "ambiguous_identity")),
                       "withheld_bodies": sum(v for k, v in group.items() if k in BODY_STATUSES or k in SOURCE_STATUSES),
                       "not_implemented": group["not_implemented"], "original_declarations": group["declarations"]})
    total = {key: sum(group[key] for group in groups) for key in groups[0] if key != "assembly"}
    if total["implemented_source_bodies"] > total["original_native_bodies"]:
        raise ValueError("Impossible method body credit")
    return {"schema_version": 1, "measure": "maintained-original-source-body-coverage-v1", "assemblies": groups,
            "totals": total, "method_map": method_map, "behavior_parity_verified": False, "native_byte_matching_verified": False}


def _executable_sections(binary):
    """Read a thin little-endian64-bit Mach-O executable section map."""
    with playercode._regular(binary, 2 * 1024**3).open("rb") as stream:
        header = stream.read(32)
        if len(header) != 32 or header[:4] != b"\xcf\xfa\xed\xfe": raise ValueError("Native inventory requires a thin Mach-O64 input")
        fields = struct.unpack("<8I", header); ncmds, sizeofcmds = fields[4:6]
        if ncmds > 65536 or sizeofcmds > 64 * 1024 * 1024: raise ValueError("Invalid native command limits")
        commands = stream.read(sizeofcmds)
        if len(commands) != sizeofcmds: raise ValueError("Truncated native command map")
        result = []; position = 0
        for _ in range(ncmds):
            if position + 8 > len(commands): raise ValueError("Truncated native command")
            cmd, length = struct.unpack_from("<2I", commands, position)
            if length < 8 or position + length > len(commands): raise ValueError("Invalid native command extent")
            if cmd == 0x19:
                if length < 72: raise ValueError("Truncated native segment")
                row = struct.unpack_from("<2I16s4Q4I", commands, position)
                nsects = row[-2]
                if 72 + nsects * 80 > length: raise ValueError("Invalid native section table")
                for index in range(nsects):
                    section = struct.unpack_from("<16s16s2Q8I", commands, position + 72 + index * 80)
                    if (section[0].rstrip(b"\0") in (b"__text", b"il2cpp") and section[1].rstrip(b"\0") == b"__TEXT" and
                            row[8] & 4 and section[8] & 0x80000000):
                        result.append((section[2], section[3], section[4]))
            position += length
        if position != len(commands) or not result: raise ValueError("No exact native executable section")
        return result


def validate_native_bytes(inventory, binary):
    sections = _executable_sections(binary)
    with Path(binary).open("rb") as stream:
        for method in inventory["methods"]:
            if not method["native_body"]: continue
            pointer = int(method["native_pointer"], 16); offset = method["file_offset"]
            if type(offset) is not int or not any(address <= pointer and pointer + 16 <= address + size and offset == raw + pointer - address for address, size, raw in sections):
                raise ValueError("Native method pointer is not in exact executable input bytes")
            stream.seek(offset); data = stream.read(16)
            if len(data) != 16 or hashlib.sha256(data).hexdigest() != method["native_prefix_sha256"]:
                raise ValueError("Native body inventory bytes changed")


def _count_groups(measurement):
    if (type(measurement.get("schema_version")) is not int or measurement.get("schema_version") != 1 or measurement.get("measure") != "maintained-original-source-body-coverage-v1" or
            measurement.get("behavior_parity_verified") is not False or measurement.get("native_byte_matching_verified") is not False):
        raise ValueError("Unsupported code-work measurement or parity claim")
    groups, totals = measurement.get("assemblies"), measurement.get("totals")
    if not isinstance(groups, list) or not 1 <= len(groups) <= 256 or not isinstance(totals, dict) or set(totals) != COUNT_FIELDS:
        raise ValueError("Incomplete public code-work groups")
    names, result = [], []
    for row in groups:
        if (not isinstance(row, dict) or set(row) != COUNT_FIELDS | {"assembly"} or not isinstance(row["assembly"], str) or
                re.fullmatch(r"Game\.Runtime|HL[A-Za-z0-9]+(?:\.Runtime)?|Assembly-CSharp(?:-firstpass)?", row["assembly"]) is None):
            raise ValueError("Unsupported public assembly group")
        names.append(row["assembly"])
        if any(type(row[k]) is not int or not 0 <= row[k] <= 1000000 for k in COUNT_FIELDS):
            raise ValueError("Invalid public method counts")
        if (row["original_native_bodies"] != sum(row[k] for k in ("implemented_source_bodies", "unresolved_identities", "withheld_bodies", "not_implemented")) or
                row["original_native_bodies"] > row["original_declarations"]):
            raise ValueError("Public method counts do not account for original bodies")
        result.append({key: row[key] for key in sorted(COUNT_FIELDS | {"assembly"})})
    if names != sorted(set(names)) or any(type(totals.get(k)) is not int or totals[k] != sum(row[k] for row in result) for k in COUNT_FIELDS):
        raise ValueError("Public group totals differ")
    return result, {key: totals[key] for key in sorted(COUNT_FIELDS)}


def public_counts(measurement, *, source, input_binary, input_metadata, scope_sha256, compiler_receipt_sha256):
    """Return only fixed public count/hash fields; method/source paths stay local."""
    identities = {"source_fingerprint": source, "input_binary_sha256": input_binary, "input_metadata_sha256": input_metadata,
                  "scope_sha256": scope_sha256, "compiler_receipt_sha256": compiler_receipt_sha256}
    if any(not isinstance(value, str) or HASH.fullmatch(value) is None for value in identities.values()):
        raise ValueError("Invalid sealed code-work identity")
    groups, totals = _count_groups(measurement)
    return {"schema_version": 1, "generated_by": "ProjectLucid.code_progress", "measure": measurement["measure"], **identities,
            "assemblies": groups, "totals": totals, "behavior_parity_verified": False, "native_byte_matching_verified": False}


def validate_public_counts(report, repo_root=None):
    """Check a sanitized snapshot offline; freshness requires the source tree."""
    keys = {"schema_version", "generated_by", "measure", "source_fingerprint", "input_binary_sha256", "input_metadata_sha256",
            "scope_sha256", "compiler_receipt_sha256", "assemblies", "totals", "behavior_parity_verified", "native_byte_matching_verified"}
    if not isinstance(report, dict) or set(report) != keys or report.get("generated_by") != "ProjectLucid.code_progress":
        raise ValueError("Unsupported public code-work snapshot")
    rebuilt = public_counts(report, source=report["source_fingerprint"], input_binary=report["input_binary_sha256"],
                            input_metadata=report["input_metadata_sha256"], scope_sha256=report["scope_sha256"], compiler_receipt_sha256=report["compiler_receipt_sha256"])
    if rebuilt != report: raise ValueError("Public code-work snapshot differs from sanitized counts")
    if repo_root is not None:
        names, _, scope_hash = _scope(repo_root)
        if report["source_fingerprint"] != artifact_fingerprint(repo_root) or report["scope_sha256"] != scope_hash or names != [g["assembly"] for g in report["assemblies"]]:
            raise ValueError("Public code-work snapshot is stale for this source/scope")
    return report


def render_readme_section(report):
    """Basic-user code progress, generated without a graphic."""
    validate_public_counts(report)
    def percentage(row):
        denominator = row["original_native_bodies"]
        return format(100 * row["implemented_source_bodies"] / denominator, ".2f") + "%" if denominator else "—"
    total = report["totals"]
    out = ["<!-- LUCID_CODE_PROGRESS:START -->", "## Progress", "",
           "**" + format(total["implemented_source_bodies"], ",") + " of " + format(total["original_native_bodies"], ",") +
           " original methods have maintained source implementations (" + percentage(total) + ").**", "",
           "| Assembly | Methods with source | Original methods | Progress |", "|---|---:|---:|---:|"]
    for group in report["assemblies"]:
        label = group["assembly"] + ({"Assembly-CSharp": " (game code)", "Assembly-CSharp-firstpass": " (studio code)"}.get(group["assembly"], ""))
        out.append("| " + label + " | " + format(group["implemented_source_bodies"], ",") + " | " + format(group["original_native_bodies"], ",") + " | " + percentage(group) + " |")
    out += ["| **Total** | **" + format(total["implemented_source_bodies"], ",") + "** | **" + format(total["original_native_bodies"], ",") + "** | **" + percentage(total) + "** |", "",
            "Generated from original method signatures and compiled source. Counts are conservative: ambiguous signatures and recognized trivial or placeholder patterns are excluded, including some genuine trivial methods. These counts cover methods with original native bodies; the game is not yet playable.", "<!-- LUCID_CODE_PROGRESS:END -->"]
    return "\n".join(out) + "\n"


def _build_reader(root, work, run, engine):
    built = native.build_native_harness(work)
    if built.get("status") != "ready": raise ValueError("Pinned native dependency build is unavailable")
    dependency = Path(built["path"]).parent
    playercode._regular(dependency / "Cpp2IL.Core.dll")
    project = playercode._regular(root / "tools/code-progress/CodeProgress.csproj", 65536)
    program = playercode._regular(project.with_name("Program.cs"), 1024 * 1024)
    host = managed_path(work, "tools", "dotnet", "dotnet")
    environment = dotnet_environment(work, host.parent)
    sdk = load_lock().get("artifacts", {}).get("dotnet", {})
    if sdk.get("version") != native._SDK: raise ValueError("Code progress requires the pinned SDK")
    host_seal = playercode._file_record(host)
    version = subprocess.run([str(host), "--version"], capture_output=True, text=True, env=environment, timeout=30)
    if version.returncode or version.stdout.strip() != sdk["version"]: raise ValueError("Code progress SDK version differs")
    output = run / "reader"
    command = [str(host), "build", str(project), "-c", "Release", "-o", str(output),
               "-p:BaseIntermediateOutputPath=" + str(run / "obj") + os.sep,
               "-p:NativeDependencyRoot=" + str(dependency),
               "-p:UnityManagedRoot=" + str(Path(next(e["path"] for e in engine if e["name"] == "cecil")).parent)]
    with (run / "build.log").open("xb") as log:
        process = subprocess.run(command, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=300)
    reader = output / "ProjectLucid.CodeProgress.dll"
    if process.returncode or not reader.is_file(): raise ValueError("Code progress reader build failed")
    if playercode._file_record(host) != host_seal: raise ValueError("Code progress host changed during build")
    return {"host": host_seal, "path": str(reader), "sha256": sha256_file(reader),
            "sources": [playercode._file_record(project), playercode._file_record(program)],
            "dependencies": built, "outputs": playerschema._tree_records(output, work)}


def _check_reader(report, context_raw, context, reader):
    expected = {"schema_version": 1, "status": "pending-wrapper-verification", "identity_nonce": context["identity_nonce"],
                "source_fingerprint": context["source_fingerprint"], "context_sha256": hashlib.sha256(context_raw).hexdigest(),
                "recovered_behavior_verified": False, "native_byte_matching_verified": False, "gameplay_verified": False}
    if any(type(report.get(k)) is not type(v) or report.get(k) != v for k, v in expected.items()):
        raise ValueError("Code progress reader context or approval claims differ")
    if report.get("reader", {}).get("sha256") != reader["sha256"]:
        raise ValueError("Code progress reader binary changed")
    core = Path(reader["path"]).with_name("Cpp2IL.Core.dll")
    if report["reader"].get("cpp2il_core") != {"sha256": sha256_file(core), "mvid": playercode._pe_identity(core)["mvid"]}:
        raise ValueError("Loaded pinned native metadata reader differs")


def _maintained_document(relative):
    return (isinstance(relative, str) and "\\" not in relative and relative.startswith(("Assets/", "Packages/")) and relative.endswith(".cs") and
            not any(p in ("", ".", "..", "Editor", "Tests", "bin", "obj", "__pycache__") or p.startswith(".") for p in relative.split("/")) and
            not relative.startswith(("Assets/Recovered/", "Assets/StreamingAssets/")))


def _validate_player_inventory(inventory, evidence, root):
    source_inputs = {r["path"]: r for r in evidence["staged"]["inputs"] if r["kind"] == "source"}
    selected = {row["assembly_name"]: row for row in evidence["staged"]["modules"]}
    files = {row["path"]: row for row in evidence["staged"]["files"]}
    seen = set()
    for module in inventory["modules"]:
        name = module["assembly"]
        if name in seen or name not in selected: raise ValueError("Duplicate or foreign compiled module")
        seen.add(name); expected = selected[name]
        if module["mvid"] != expected["mvid"]: raise ValueError("Compiled module MVID differs")
        for key in ("dll", "pdb"):
            record = module[key]; supplied = files.get(record["path"])
            if supplied is None or any(record.get(k) != supplied.get(k) for k in ("path", "sha256", "size")):
                raise ValueError("Compiled module/PDB is not a genuine returned output")
            playerschema._same_file(record["path"], record)
        if module["dll"]["path"] != expected["path"] or module["pdb"]["path"] != str(Path(expected["path"]).with_suffix(".pdb")):
            raise ValueError("Compiled module/PDB paths differ")
    for row in inventory["methods"]:
        if row.get("assembly") not in seen: raise ValueError("Maintained method has no genuine compiled module")
        for document in row["source_documents"]:
            relative = document["relative_path"]
            if not _maintained_document(relative): raise ValueError("Foreign or generated source document")
            actual = playercode._file_record(root / relative)
            supplied = source_inputs.get(actual["path"])
            if supplied is None or any(type(supplied.get(k)) is not type(v) or supplied.get(k) != v for k, v in actual.items()):
                raise ValueError("PDB document is absent from the genuine sealed compiler source plan")
            if actual["size"] != document["size"] or actual["sha256"] != document["sha256"]:
                raise ValueError("Maintained source changed after PDB inspection")
            raw = Path(actual["path"]).read_bytes()
            algorithm = document["pdb_checksum_algorithm"]
            checksum = hashlib.sha256(raw).hexdigest() if algorithm == "SHA256" else hashlib.sha1(raw).hexdigest() if algorithm == "SHA1" else None
            if checksum != document["pdb_checksum"] or row["source_status"] == "source_checksum_mismatch":
                raise ValueError("Compiled source PDB is stale")
    return seen


def run_code_progress(repo_root, work_dir, input_path, target="macos"):
    """Generate source-sealed code-work evidence in a nonce-owned cache run."""
    if target not in playercode.TARGETS: raise ValueError("Unsupported desktop compilation target")
    root = Path(repo_root).resolve(strict=True); work = validate_work_dir(work_dir)
    source = artifact_fingerprint(root); scope, namespace_filters, scope_digest = _scope(root)
    engine = playercode._engine_identity(find_editor())
    evidence = playerschema._player_evidence(root, work, target, source, engine)
    app, binary, metadata, version = recovery._resolve_input(input_path)
    if version != UNITY_VERSION: raise ValueError("Original Unity version differs")
    binary_seal = playercode._file_record(binary, 2 * 1024**3); metadata_seal = playercode._file_record(metadata)
    thin, _ = recovery._thin_binary(binary, work, binary_seal["sha256"])
    thin_seal = playercode._file_record(thin, 2 * 1024**3)
    lock = managed_path(work, "locks", "code-progress.lock"); lock.parent.mkdir(parents=True, exist_ok=True)
    try: lock.mkdir()
    except FileExistsError as error: raise ValueError("Another code progress run owns the lock") from error
    try:
        nonce = uuid.uuid4().hex; run = managed_path(work, "code-progress", "runs", nonce); run.mkdir(parents=True, exist_ok=False)
        reader = _build_reader(root, work, run, engine)
        context = {"schema_version": 1, "kind": "project-lucid-code-progress-v1", "identity_nonce": nonce,
                   "source_fingerprint": source, "source_root": str(root), "work_root": str(work), "run": str(run), "binary": str(thin), "metadata": str(metadata),
                   "scope": scope, "namespace_filters": namespace_filters,
                   "player_modules": [row["path"] for row in evidence["staged"]["modules"] if row["assembly_name"] in scope]}
        context_raw = (json.dumps(context, sort_keys=True, separators=(",", ":")) + "\n").encode()
        context_path = run / "context.json"
        with context_path.open("xb") as stream: stream.write(context_raw)
        reports = {}
        for mode in ("native", "player"):
            pending = run / (mode + ".json")
            with (run / (mode + ".log")).open("xb") as log:
                process = subprocess.run([reader["host"]["path"], reader["path"], mode, str(context_path), str(pending)],
                                         env=dotnet_environment(work, Path(reader["host"]["path"]).parent), stdout=log, stderr=subprocess.STDOUT, timeout=300)
            if process.returncode or not pending.is_file(): raise ValueError("Code progress metadata reader failed")
            raw, report = _json(pending); _check_reader(report, context_raw, context, reader)
            cecil = next(e for e in engine if e["name"] == "cecil")
            if report["reader"]["cecil"] != {"sha256": cecil["sha256"], "mvid": cecil["mvid"]}:
                raise ValueError("Code progress Cecil reader differs from installed engine")
            reports[mode] = (raw, report)
        original = reports["native"][1]["inventory"]; maintained = reports["player"][1]["inventory"]
        if original.get("cpp2il_pin") != native._PIN: raise ValueError("Original reader dependency pin differs")
        if original.get("modules") != [thin_seal, metadata_seal]: raise ValueError("Original reader input seals differ")
        validate_native_bytes(original, thin)
        seen = _validate_player_inventory(maintained, evidence, root)
        if seen != {Path(p).stem for p in context["player_modules"]}: raise ValueError("Reader omitted a maintained proprietary module")
        measurement = measure_code_work(original, maintained, scope, namespace_filters)
        # Genuine input, compiler outputs, reader binaries and source must remain
        # identical immediately before the only authoritative cache publication.
        for seal in (binary_seal, metadata_seal, thin_seal):
            actual = playercode._file_record(seal["path"], 2 * 1024**3)
            if actual != seal: raise ValueError("Original input changed during measurement")
        if artifact_fingerprint(root) != source or playercode._engine_identity(find_editor()) != engine:
            raise ValueError("Source or installed engine changed during measurement")
        final = playerschema._player_evidence(root, work, target, source, engine)
        if final["receipt_raw"] != evidence["receipt_raw"] or final["staged_raw"] != evidence["staged_raw"] or final["context_raw"] != evidence["context_raw"]:
            raise ValueError("Genuine compiler evidence changed during measurement")
        for mode, (raw, _) in reports.items():
            if playercode._regular(run / (mode + ".json")).read_bytes() != raw: raise ValueError("Reader output changed")
        if playercode._regular(context_path).read_bytes() != context_raw or playerschema._tree_records(Path(reader["path"]).parent, work) != reader["outputs"]:
            raise ValueError("Reader/context evidence changed")
        if playercode._file_record(reader["host"]["path"]) != reader["host"] or any(playercode._file_record(seal["path"]) != seal for seal in reader["sources"]):
            raise ValueError("Reader host/source changed before publication")
        _validate_player_inventory(maintained, final, root)
        public = public_counts(measurement, source=source, input_binary=binary_seal["sha256"], input_metadata=metadata_seal["sha256"],
                               scope_sha256=scope_digest, compiler_receipt_sha256=evidence["receipt_sha256"])
        result = {"schema_version": 1, "status": "ready", "generated_by": "ProjectLucid.run_code_progress", "source_fingerprint": source,
                  "target": target, "identity_nonce": nonce, "run": str(run), "context_sha256": hashlib.sha256(context_raw).hexdigest(),
                  "original_input": [binary_seal, metadata_seal, thin_seal], "reader": reader,
                  "compiler_receipt_sha256": evidence["receipt_sha256"], "measurement": measurement, "public": public,
                  "behavior_parity_verified": False, "native_byte_matching_verified": False}
        if artifact_fingerprint(root) != source: raise ValueError("Source changed before publication")
        write_json(managed_path(work, "code-progress", "latest-" + target + ".json"), result)
        return result
    finally: lock.rmdir()

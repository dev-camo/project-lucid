"""Compile genuine player scripts and seal their metadata-only output evidence."""
from __future__ import annotations

import base64
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import struct
import subprocess
import uuid

from .bootstrap import managed_path, validate_work_dir, write_json
from .verification import UNITY_VERSION, artifact_fingerprint, find_editor

TARGETS = {"macos": ("osxuniversal", "StandaloneOSX"),
           "windows": ("win64", "StandaloneWindows64"),
           "linux": ("linux64", "StandaloneLinux64")}
REQUIRED_ASSEMBLIES = {"Game.Runtime", "HLUnityCore.Runtime", "Unity.Addressables", "Unity.ResourceManager"}
MAX_REPORT = 32 * 1024 * 1024
MAX_FILE = 256 * 1024 * 1024
SHA = re.compile(r"[a-f0-9]{64}\Z")
MVID = re.compile(r"[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}\Z")


def _regular(path, maximum=MAX_FILE):
    path = Path(os.path.abspath(os.fspath(path)))
    for component in [path, *path.parents]:
        if component.is_symlink():
            raise ValueError("Refusing a symbolic link in player compilation evidence: " + str(component))
    info = path.stat()
    if not stat.S_ISREG(info.st_mode) or not 0 < info.st_size <= maximum:
        raise ValueError("Player compilation evidence is not a bounded regular file: " + str(path))
    return path


def _file_record(path, maximum=MAX_FILE):
    path = _regular(path, maximum)
    size = path.stat().st_size
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for part in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(part)
    if path.stat().st_size != size:
        raise ValueError("Evidence file size changed while hashing")
    return {"path": str(path), "size": size, "sha256": digest.hexdigest()}


def _pe_identity(path):
    """Read Module MVID and Assembly name from PE/CLI metadata, without loading code."""
    data = _regular(path).read_bytes()
    def unpack(fmt, offset):
        size = struct.calcsize(fmt)
        if offset < 0 or offset + size > len(data):
            raise ValueError("Truncated PE/CLI metadata")
        return struct.unpack_from(fmt, data, offset)
    if data[:2] != b"MZ":
        raise ValueError("Player output is not a PE managed assembly")
    pe, = unpack("<I", 0x3c)
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    sections, = unpack("<H", pe + 6)
    optional_size, = unpack("<H", pe + 20)
    optional = pe + 24
    magic, = unpack("<H", optional)
    directory = optional + (96 if magic == 0x10b else 112 if magic == 0x20b else -999999)
    if not 1 <= sections <= 96 or directory < optional or directory + 15 * 8 > optional + optional_size:
        raise ValueError("Unsupported PE header")
    section_rows = [unpack("<IIII", optional + optional_size + i * 40 + 8) for i in range(sections)]
    def rva(address, length):
        matches = []
        for virtual_size, start, raw_size, raw_start in section_rows:
            delta = address - start
            if 0 <= delta and delta + length <= raw_size and delta + length <= max(virtual_size, raw_size):
                offset = raw_start + delta
                if offset + length <= len(data): matches.append(offset)
        if len(matches) != 1:
            raise ValueError("PE RVA does not identify one bounded section")
        return matches[0]
    cli_rva, cli_size = unpack("<II", directory + 14 * 8)
    if cli_size < 0x48: raise ValueError("PE has no complete CLI header")
    cli = rva(cli_rva, 0x48)
    metadata_rva, metadata_size = unpack("<II", cli + 8)
    md = rva(metadata_rva, metadata_size)
    limit = md + metadata_size
    if data[md:md + 4] != b"BSJB": raise ValueError("Invalid CLI metadata signature")
    version_size, = unpack("<I", md + 12)
    if not 0 < version_size <= 256: raise ValueError("Invalid metadata version length")
    cursor = md + 16 + version_size
    cursor = (cursor + 3) & ~3
    _, stream_count = unpack("<HH", cursor)
    cursor += 4
    if not 1 <= stream_count <= 16: raise ValueError("Unsupported metadata stream count")
    streams = {}
    for _ in range(stream_count):
        offset, size = unpack("<II", cursor); cursor += 8
        end = data.find(b"\0", cursor, min(cursor + 32, limit))
        if end < 0: raise ValueError("Invalid metadata stream name")
        name = data[cursor:end].decode("ascii")
        cursor = (end + 4) & ~3
        if name in streams or md + offset + size > limit:
            raise ValueError("Duplicate or unbounded metadata stream")
        streams[name] = (md + offset, size)
    if "#Strings" not in streams or "#GUID" not in streams or ("#~" in streams) == ("#-" in streams):
        raise ValueError("Missing or ambiguous metadata streams")
    ranges = sorted((p, p + n) for p, n in streams.values() if n)
    if any(a < cursor or a < prior for (a, b), prior in zip(ranges, [cursor] + [b for a, b in ranges[:-1]])):
        raise ValueError("Overlapping metadata streams")
    table, table_size = streams.get("#~", streams.get("#-"))
    if table_size < 24: raise ValueError("Truncated metadata table stream")
    heap_flags = data[table + 6]
    valid, = unpack("<Q", table + 8)
    row_counts = {}
    cursor = table + 24
    for i in range(64):
        if valid & (1 << i):
            row_counts[i], = unpack("<I", cursor); cursor += 4
            if row_counts[i] > 0x1000000: raise ValueError("Unsupported metadata row count")
    def idx(i): return 4 if row_counts.get(i, 0) >= 0x10000 else 2
    def coded(tag_bits, tables): return 4 if any(row_counts.get(i, 0) >= (1 << (16 - tag_bits)) for i in tables) else 2
    s, g, b = [4 if heap_flags & flag else 2 for flag in (1, 2, 4)]
    td = coded(2, [2, 1, 27]); rs = coded(2, [0, 26, 35, 1])
    sizes = {0: 2+s+3*g, 1: rs+2*s, 2: 4+2*s+td+idx(4)+idx(6), 3: idx(4), 4: 2+s+b,
             5: idx(6), 6: 8+s+b+idx(8), 7: idx(8), 8: 4+s, 9: idx(2)+td,
             10: coded(3,[2,1,26,6,27])+s+b, 11: 2+coded(2,[4,8,23])+b,
             12: coded(5,[6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43])+coded(3,[6,10])+b,
             13: coded(1,[4,8])+b, 14: 2+coded(2,[2,6,32])+b, 15: 6+idx(2), 16: 4+idx(4),
             17: b, 18: idx(2)+idx(20), 19: idx(20), 20: 2+s+td, 21: idx(2)+idx(23),
             22: idx(23), 23: 2+s+b, 24: 2+idx(6)+coded(1,[20,23]),
             25: idx(2)+2*coded(1,[6,10]), 26: s, 27: b,
             28: 2+coded(1,[4,6])+s+idx(26), 29: 4+idx(4), 30: 8, 31: 4}
    if row_counts.get(0) != 1 or row_counts.get(32) != 1:
        raise ValueError("Player output must contain one Module and one Assembly")
    module = cursor
    mvid_index, = unpack("<I" if g == 4 else "<H", module + 2 + s)
    guid_start, guid_size = streams["#GUID"]
    if mvid_index == 0 or mvid_index * 16 > guid_size:
        raise ValueError("Invalid Module MVID index")
    mvid = str(uuid.UUID(bytes_le=data[guid_start + (mvid_index-1)*16:guid_start + mvid_index*16]))
    if mvid == str(uuid.UUID(int=0)): raise ValueError("Empty Module MVID")
    assembly = cursor + sum(row_counts.get(i, 0) * sizes[i] for i in range(32))
    if assembly + 16 + b + 2*s > table + table_size: raise ValueError("Truncated Assembly metadata")
    name_index, = unpack("<I" if s == 4 else "<H", assembly + 16 + b)
    string_start, string_size = streams["#Strings"]
    end = data.find(b"\0", string_start + name_index, string_start + string_size)
    if not 0 < name_index < string_size or end < 0: raise ValueError("Invalid Assembly name")
    name = data[string_start + name_index:end].decode("utf-8")
    if re.fullmatch(r"[A-Za-z0-9_.-]{1,200}", name) is None: raise ValueError("Unsupported Assembly name")
    return {"assembly_name": name, "mvid": mvid}


def _engine_identity(editor):
    editor = _regular(editor, 2 * 1024 * 1024 * 1024)
    candidates = [editor.parents[1] / "Managed", editor.parent / "Data" / "Managed"]
    managed = next((p for p in candidates if (p / "Unity.Cecil.dll").is_file()), None)
    if managed is None: raise ValueError("Cannot identify the installed Editor managed libraries")
    def module(name):
        paths = [managed / "UnityEngine" / name, managed / name]
        return next((p for p in paths if p.is_file()), paths[0])
    records = []
    for name, path in [("editor", editor), ("editor_core", module("UnityEditor.CoreModule.dll")),
                       ("engine_core", module("UnityEngine.CoreModule.dll")), ("cecil", managed / "Unity.Cecil.dll"),
                       ("serialization", managed / "Unity.SerializationLogic.dll")]:
        record = dict(name=name, **_file_record(path, 2 * 1024**3 if name == "editor" else MAX_FILE))
        record.update({"assembly_name": "", "mvid": ""} if name == "editor" else _pe_identity(path))
        records.append(record)
    return records


def _context(root, work, run, target, source, nonce, engine):
    encode = lambda value: base64.b64encode(str(value).encode("utf-8")).decode("ascii")
    values = {"schema": "1", "kind": "project-lucid-player-code-v1", "nonce": nonce,
              "project_root_base64": encode(root), "work_root_base64": encode(work), "run_base64": encode(run), "target": target,
              "unity_version": UNITY_VERSION, "source_fingerprint": source}
    for entry in engine:
        for key in ("path", "sha256", "size", "mvid", "assembly_name"):
            values[entry["name"] + "_" + ("path_base64" if key == "path" else key)] = encode(entry[key]) if key == "path" else str(entry[key])
    return ("\n".join(k + "=" + v for k, v in values.items()) + "\n").encode("ascii")


def _strict_json(data):
    def pairs(items):
        result = {}
        for k, v in items:
            if k in result: raise ValueError("Duplicate player evidence JSON field")
            result[k] = v
        return result
    def constant(value): raise ValueError("Non-finite player evidence JSON number")
    return json.loads(data, object_pairs_hook=pairs, parse_constant=constant)


def _owned_file(work, run, relative):
    if (not isinstance(relative, str) or not relative or "\\" in relative or
            relative.startswith("/") or any(p in ("", ".", "..") for p in relative.split("/"))):
        raise ValueError("Invalid relative player output path")
    path = managed_path(work, *run.relative_to(Path(work)).parts, "assemblies", *relative.split("/"))
    return _regular(path)


def _input_identity(records):
    result = hashlib.sha256()
    for record in sorted(records, key=lambda r: (r["kind"] + "\0" + r["path"]).encode("utf-16-be")):
        result.update((record["kind"] + "\0" + record["path"] + "\0" + record["sha256"] + "\n").encode("utf-8"))
    return result.hexdigest()


def _check_report(report, root, work, run, target, nonce, source, engine, digest):
    expected = {"schema_version": 1, "command": "player-code", "status": "pending-identity-verification",
                "identity_status": "pending-wrapper", "compilation_status": "compiled", "unity_version": UNITY_VERSION,
                "target": target, "active_target": TARGETS[target][1], "build_target": TARGETS[target][1],
                "build_group": "Standalone", "subtarget": 0, "options": "None", "extra_scripting_defines": [],
                "compilation_api": "PlayerBuildInterface.CompilePlayerScripts", "identity_nonce": nonce,
                "identity_context_sha256": digest, "source_fingerprint_before": source, "source_fingerprint_after": source,
                "project_root": str(root), "work_root": str(work), "run": str(run), "player_schema_verified": False,
                "gameplay_verified": False}
    if not isinstance(report, dict) or any(type(report.get(k)) is not type(v) or report.get(k) != v for k, v in expected.items()):
        raise ValueError("Player compilation identity/settings are stale, failed or incomplete")
    if report.get("engine_before") != engine or report.get("engine_after") != engine:
        raise ValueError("Player evidence differs from the actual installed Editor identity")
    diagnostics = report.get("diagnostics")
    if not isinstance(diagnostics, list) or len(diagnostics) > 10000 or any(
            not isinstance(d, dict) or d.get("type") not in ("Warning", "Info") or
            not isinstance(d.get("message"), str) or len(d["message"]) > 16384 for d in diagnostics):
        raise ValueError("Player compilation contains compiler errors or invalid diagnostics")
    inputs = report.get("inputs")
    if not isinstance(inputs, list) or not 1 <= len(inputs) <= 30000:
        raise ValueError("Player compilation input plan is absent or oversized")
    seen_inputs = set()
    for item in inputs:
        if not isinstance(item, dict) or item.get("kind") not in ("source", "precompiled_reference"):
            raise ValueError("Invalid player compilation input kind")
        path = Path(item.get("path", ""))
        if not path.is_absolute() or str(path) != os.path.abspath(path) or str(path) in seen_inputs:
            raise ValueError("Duplicate or noncanonical compilation input")
        seen_inputs.add(str(path))
        allowed = [root / "Assets", root / "Packages", root / "Library" / "PackageCache"]
        if item["kind"] == "precompiled_reference":
            allowed.extend([root / "Library", Path(engine[0]["path"]).parents[3] if "Unity.app" in Path(engine[0]["path"]).parts else Path(engine[0]["path"]).parent])
        if not any(path == base or base in path.parents for base in allowed) or any(
                root / "Assets" / folder in path.parents for folder in ("Recovered", "StreamingAssets")):
            raise ValueError("Unexpected player compiler input location")
        actual = _file_record(path)
        if any(type(item.get(k)) is not type(v) or item.get(k) != v for k, v in actual.items()):
            raise ValueError("Player compiler input changed or was forged")
    if not any(r["kind"] == "source" for r in inputs): raise ValueError("No player source files were captured")
    inputs_hash = _input_identity(inputs)
    if report.get("inputs_fingerprint_before") != inputs_hash or report.get("inputs_fingerprint_after") != inputs_hash:
        raise ValueError("Player source/reference plan changed during compilation")
    modules, returned, files = report.get("modules"), report.get("returned_assemblies"), report.get("files")
    if not isinstance(modules, list) or not 1 <= len(modules) <= 4096 or not isinstance(returned, list) or len(returned) != len(modules):
        raise ValueError("Actual returned player assembly collection is absent or inconsistent")
    if not isinstance(files, list) or not 1 <= len(files) <= 8192:
        raise ValueError("Player output file manifest is absent or oversized")
    file_records = {}
    total = 0
    for item in files:
        if not isinstance(item, dict) or item.get("relative_path") in file_records:
            raise ValueError("Duplicate output manifest path")
        path = _owned_file(work, run, item.get("relative_path"))
        actual = _file_record(path)
        total += actual["size"]
        if total > 8 * 1024**3: raise ValueError("Player output exceeds bounded evidence size")
        if any(type(item.get(k)) is not type(v) or item.get(k) != v for k, v in actual.items()):
            raise ValueError("Player output file changed or was forged")
        file_records[item["relative_path"]] = item
    disk_files = set()
    output = run / "assemblies"
    for parent, directories, names in os.walk(output):
        for name in directories:
            if (Path(parent) / name).is_symlink(): raise ValueError("Symlink in player compiler output")
        for name in names: disk_files.add((Path(parent) / name).relative_to(output).as_posix())
    if set(file_records) != disk_files: raise ValueError("Player output manifest does not account for every file")
    names, paths = set(), set()
    for raw, item in zip(returned, modules):
        if not isinstance(raw, str) or not raw or not isinstance(item, dict) or item.get("returned_path") != raw:
            raise ValueError("Invalid actual compiler-returned assembly path")
        path = _owned_file(work, run, item.get("relative_path"))
        raw_path = Path(raw) if os.path.isabs(raw) else output / raw
        if ".." in raw_path.parts or "\\" in raw and os.name != "nt" or os.path.abspath(raw_path) != str(path):
            raise ValueError("Compiler-returned assembly escapes the fresh output directory")
        if path.suffix != ".dll" or str(path) in paths: raise ValueError("Invalid or duplicate returned assembly")
        paths.add(str(path))
        identity = _pe_identity(path)
        if any(item.get(k) != v for k, v in identity.items()) or path.name != identity["assembly_name"] + ".dll":
            raise ValueError("Player assembly name/MVID differs from actual PE metadata")
        if identity["assembly_name"] in names: raise ValueError("Duplicate player assembly identity")
        names.add(identity["assembly_name"])
        if item.get("relative_path") not in file_records or any(item.get(k) != file_records[item["relative_path"]][k] for k in ("path", "size", "sha256")):
            raise ValueError("Returned module is not sealed by the output manifest")
    if not REQUIRED_ASSEMBLIES <= names: raise ValueError("Required maintained/package player assemblies are missing")


def run_player_code(repo_root, work_dir, target):
    """Publish player compilation evidence; this never builds a game or approves layouts."""
    if target not in TARGETS: raise ValueError("Player target must be macos, windows or linux")
    root, work = Path(repo_root).resolve(strict=True), validate_work_dir(Path(work_dir))
    version = _regular(root / "ProjectSettings" / "ProjectVersion.txt", 65536).read_text()
    if re.search(r"^m_EditorVersion: " + re.escape(UNITY_VERSION) + r"$", version, re.M) is None:
        raise ValueError("Player compilation requires the matching project Unity version")
    editor = _regular(find_editor(), 2 * 1024**3)
    latest = managed_path(work, "player-code", "latest-" + target + ".json")
    if latest.exists(): _regular(latest, MAX_REPORT)
    lock = managed_path(work, "locks", "player-code.lock")
    lock.parent.mkdir(parents=True, exist_ok=True)
    try: lock.mkdir()
    except FileExistsError as error: raise ValueError("Another player compilation owns the generated lock") from error
    run = None
    run_created = False
    try:
        source = artifact_fingerprint(root)
        engine = _engine_identity(editor)
        nonce = uuid.uuid4().hex
        run = managed_path(work, "player-code", "runs", nonce)
        run.mkdir(parents=True, exist_ok=False)
        run_created = True
        context = managed_path(work, "player-code", "runs", nonce, "context.txt")
        pending = managed_path(work, "player-code", "runs", nonce, "pending.json")
        log = managed_path(work, "player-code", "runs", nonce, "editor.log")
        raw_context = _context(root, work, run, target, source, nonce, engine)
        with context.open("xb") as stream:
            stream.write(raw_context); stream.flush(); os.fsync(stream.fileno())
        digest = hashlib.sha256(raw_context).hexdigest()
        if artifact_fingerprint(root) != source: raise ValueError("Maintained source changed before player compilation")
        command = [str(editor), "-batchmode", "-quit", "-projectPath", str(root),
                   "-buildTarget", TARGETS[target][0], "-standaloneBuildSubtarget", "Player",
                   "-executeMethod", "ProjectLucid.Editor.LucidPlayerCompilation.Run",
                   "-lucidPlayerContext", str(context), "-lucidPlayerDigest", digest,
                   "-lucidPlayerNonce", nonce, "-logFile", str(log)]
        result = subprocess.run(command, timeout=1800)
        if result.returncode or not pending.is_file():
            return {"status": "failed", "exit_code": result.returncode or 1, "log": str(log),
                    "errors": ["Unity player script compilation did not complete"], "run": str(run)}
        raw = _regular(pending, MAX_REPORT).read_bytes()
        report = _strict_json(raw)
        if _regular(context, 16384).read_bytes() != raw_context: raise ValueError("Player identity context changed")
        if artifact_fingerprint(root) != source or _engine_identity(editor) != engine:
            raise ValueError("Source or installed Editor changed during player compilation")
        _check_report(report, root, work, run, target, nonce, source, engine, digest)
        if _regular(pending, MAX_REPORT).read_bytes() != raw or _regular(context, 16384).read_bytes() != raw_context:
            raise ValueError("Player evidence changed before publication")
        _check_report(report, root, work, run, target, nonce, source, engine, digest)
        if artifact_fingerprint(root) != source or _engine_identity(editor) != engine:
            raise ValueError("Source or installed Editor changed before player evidence publication")
        if artifact_fingerprint(root) != source:
            raise ValueError("Maintained source changed during the final installed Editor identity check")
        if _regular(pending, MAX_REPORT).read_bytes() != raw or _regular(context, 16384).read_bytes() != raw_context:
            raise ValueError("Player evidence changed during final identity validation")
        latest = managed_path(work, "player-code", "latest-" + target + ".json")
        if latest.exists(): _regular(latest, MAX_REPORT)
        report.update(status="compiled", identity_status="complete", generated_by="ProjectLucid.run_player_code",
                      identity_verified_by="native-python-prepost-and-pe-metadata-v1", staged_report_path=str(pending),
                      staged_report_sha256=hashlib.sha256(raw).hexdigest(), log=str(log),
                      limitation="Script compilation and sealed metadata do not prove player layout compatibility, authored assets, startup or gameplay.")
        if artifact_fingerprint(root) != source:
            raise ValueError("Maintained source changed immediately before player evidence publication")
        write_json(latest, report)
        return {"status": "compiled", "target": target, "assembly_count": len(report["modules"]),
                "report_path": str(latest), "run": str(run), "log": str(log)}
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        if run_created and run is not None and run.is_dir():
            failure = managed_path(work, "player-code", "runs", run.name, "wrapper-failure.json")
            write_json(failure, {"status": "failed", "target": target, "error": str(error), "generated_by": "ProjectLucid.run_player_code"})
        raise
    finally:
        lock.rmdir()

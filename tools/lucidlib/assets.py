"""Export supplied Unity assets into quarantine, then promote data only.

AssetRipper's headless HTTP export deletes a nonempty destination. This module
never gives it an existing destination and never points it at the repository.
Generated scripts describe serialization; they are not recovered gameplay.
"""

from __future__ import annotations

from collections import Counter, defaultdict
import base64
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import struct
import time
from urllib import error, parse, request
import uuid

from .audio import normalize_audio
from .inspection import _manifest, sha256_file


SCHEMA_VERSION = 1
OWNER = "project-lucid-assets-v1"
MARKER = ".lucid-owner.json"
GUID = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
REFERENCE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
SCRIPT_REFERENCE = re.compile(r"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})")
CODE_EXTENSIONS = {".cs", ".dll", ".asmdef", ".asmref", ".pdb", ".mdb", ".exe", ".dylib", ".so", ".bundle"}
YAML_EXTENSIONS = {".unity", ".prefab", ".asset", ".mat", ".controller", ".overridecontroller", ".playable", ".mixer", ".anim", ".spriteatlas", ".spriteatlasv2", ".rendertexture"}
EXPORT_SETTINGS = {
    "BundledAssetsExportMode": "DirectExport",
    "ScriptContentLevel": "Level1",
    "ScriptExportMode": "Decompiled",
    "ShaderExportMode": "Yaml",
    "AudioExportFormat": "Default",
    "SpriteExportMode": "Yaml",
    "LightmapTextureExportFormat": "Yaml",
    "TextExportMode": "Parse",
    "ImageExportFormat": "Png",
    "PreferOriginalTextureExtension": "on",
}


def fingerprint_manifest(manifest: list[dict]) -> dict:
    """Fingerprint paths and content, independent of timestamps and app location."""
    ordered = sorted(manifest, key=lambda row: row["path"])
    raw = json.dumps(ordered, sort_keys=True, separators=(",", ":")).encode("utf-8")
    critical = {"Contents/Info.plist", "Contents/Frameworks/GameAssembly.dylib",
                "Contents/Resources/Data/globalgamemanagers",
                "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat",
                "Contents/Resources/Data/StreamingAssets/aa/catalog.json"}
    return {"algorithm": "sha256-content-manifest-v1", "sha256": hashlib.sha256(raw).hexdigest(),
            "file_count": len(ordered), "total_bytes": sum(row.get("size", 0) for row in ordered),
            "critical_files": {row["path"]: row["sha256"] for row in ordered if row["path"] in critical}}


def _tool_lock() -> dict:
    lock = json.loads((Path(__file__).resolve().parents[1] / "tool-lock.json").read_text())
    tool = lock["artifacts"]["assetripper"]
    return {"name": "AssetRipper", "version": tool["version"], "archive_sha256": tool["sha256"]}


def _validate_export_identity(report: dict) -> Path:
    expected = report.get("input_fingerprint")
    if not expected or not report.get("tool"):
        raise ValueError("Asset export lacks captured input/tool provenance; extract assets again")
    app = _resolve_app(Path(report["input_path"]))
    current = fingerprint_manifest(_manifest(app))
    if current != expected:
        raise ValueError("Supplied game content differs from the asset export; extract assets again")
    tool = _tool_lock()
    if any(report["tool"].get(key) != value for key, value in tool.items()) or report.get("settings") != EXPORT_SETTINGS:
        raise ValueError("Asset export tool version/settings differ from this pipeline; extract assets again")
    return app


def _json_write(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def _resolve_app(input_path: Path) -> Path:
    path = input_path.expanduser().resolve(strict=True)
    if path.is_dir() and path.suffix.lower() == ".app":
        app = path
    else:
        candidates = sorted(path.glob("*.app")) if path.is_dir() else []
        if len(candidates) != 1:
            raise ValueError("Supply a macOS .app directory, or an input directory containing exactly one .app")
        app = candidates[0].resolve(strict=True)
    if not (app / "Contents/Resources/Data/globalgamemanagers").is_file():
        raise ValueError(f"Not a Unity macOS game bundle: {app}")
    return app


def _safe_work_dir(work_dir: Path, protected: Path) -> Path:
    work = work_dir.expanduser().resolve()
    protected = protected.resolve()
    if work == protected or work in protected.parents or protected in work.parents:
        raise ValueError("The work directory must be separate from the supplied game bundle")
    work.mkdir(parents=True, exist_ok=True)
    return work


def _assert_no_symlinks(root: Path) -> None:
    if root.is_symlink():
        raise ValueError(f"Refusing symlink: {root}")
    for path in root.rglob("*"):
        if path.is_symlink():
            raise ValueError(f"Refusing symlink in generated assets: {path}")


def _is_code(path: Path) -> bool:
    asset = Path(str(path)[:-5]) if path.suffix.lower() == ".meta" else path
    return asset.suffix.lower() in CODE_EXTENSIONS or any(part.lower() in {"scripts", "plugins"} for part in asset.parts)


def _is_compiled_shader(path: Path) -> bool:
    asset = Path(str(path)[:-5]) if path.suffix.lower() in {".meta", ".ress"} else path
    if not asset.is_file():
        return False
    with asset.open("rb") as stream:
        prefix = stream.read(4096)
    return prefix.startswith(b"%YAML") and b"--- !u!48 " in prefix


def _exported_project(stage: Path) -> Path:
    for candidate in [stage / "ExportedProject", stage]:
        if (candidate / "Assets").is_dir():
            return candidate
    raise ValueError(f"AssetRipper did not export an Assets directory below: {stage}")


def _request(base: str, endpoint: str, fields: dict | None = None, timeout: int = 3600) -> bytes:
    data = None if fields is None else parse.urlencode(fields).encode("utf-8")
    req = request.Request(base + endpoint, data=data)
    # AssetRipper redirects after a completed command; urllib follows it.
    # Never route the local control plane through an environment HTTP proxy.
    with request.build_opener(request.ProxyHandler({})).open(req, timeout=timeout) as response:
        return response.read()


def _start_ripper(executable: Path, run_dir: Path) -> tuple[subprocess.Popen, str, object]:
    log = (run_dir / "assetripper.log").resolve()
    console_path = run_dir / "assetripper-console.log"
    console = console_path.open("wb")
    process = subprocess.Popen(
        [str(executable), "--headless", "--port", "0", "--log-path", str(log)],
        cwd=run_dir, stdout=console, stderr=subprocess.STDOUT,
    )
    try:
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError(f"AssetRipper exited during startup; see {run_dir / 'assetripper-console.log'}")
            match = re.search(r"Now listening on:\s*(http://127\.0\.0\.1:[1-9][0-9]*)", console_path.read_text(errors="replace"))
            if not match:
                time.sleep(0.2)
                continue
            base = match.group(1)
            try:
                _request(base, "/", timeout=1)
                return process, base, console
            except (error.URLError, OSError):
                time.sleep(0.2)
        raise RuntimeError(f"AssetRipper did not start within 60 seconds; see {log}")
    except BaseException:
        _stop_ripper(process, console)
        raise


def _stop_ripper(process: subprocess.Popen, console: object) -> None:
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=10)
    console.close()


def _canonical(path: str) -> str:
    value = path.replace("\\", "/").strip("/")
    if value.lower().startswith("assets/"):
        value = value[7:]
    return value.casefold()


def _expanded_id(prefixes: list[str], value: str) -> str:
    if "#" in value:
        prefix, rest = value.rsplit("#", 1)
        if prefix.isdigit() and int(prefix) < len(prefixes):
            return prefixes[int(prefix)] + rest
    return value


def decode_catalog_locations(catalog: dict) -> list[dict]:
    """Decode Addressables 1.22.3's packed location/key tables as inert data.

    Layout follows Unity's ContentCatalogData.CreateLocator and
    SerializationUtilities.ReadObjectFromByteArray. No serialized type is
    loaded or instantiated. Unknown key forms fail visibly instead of guessing.
    """
    fields = ["m_BucketDataString", "m_KeyDataString", "m_EntryDataString"]
    if not all(catalog.get(field) for field in fields):
        return []
    buckets, keys, entries = [base64.b64decode(catalog[field], validate=True) for field in fields]

    def integer(data: bytes, offset: int) -> int:
        if offset < 0 or offset + 4 > len(data):
            raise ValueError("Truncated Addressables integer")
        return struct.unpack_from("<i", data, offset)[0]

    def slice_at(data: bytes, offset: int, size: int) -> bytes:
        if offset < 0 or size < 0 or offset + size > len(data):
            raise ValueError("Truncated Addressables key")
        return data[offset:offset + size]

    def key_at(offset: int):
        kind = slice_at(keys, offset, 1)[0]
        offset += 1
        if kind in (0, 1):
            length = integer(keys, offset)
            return slice_at(keys, offset + 4, length).decode("ascii" if kind == 0 else "utf-16-le")
        if kind in (2, 3, 4):
            size, format_string = (2, "<H") if kind == 2 else (4, "<I" if kind == 3 else "<i")
            return struct.unpack(format_string, slice_at(keys, offset, size))[0]
        if kind in (5, 6):
            length = slice_at(keys, offset, 1)[0]
            value = slice_at(keys, offset + 1, length).decode("ascii")
            return value if kind == 5 else {"serialized_type_clsid": value}
        if kind == 7:
            assembly_len = slice_at(keys, offset, 1)[0]
            assembly = slice_at(keys, offset + 1, assembly_len).decode("ascii")
            offset += 1 + assembly_len
            class_len = slice_at(keys, offset, 1)[0]
            class_name = slice_at(keys, offset + 1, class_len).decode("ascii")
            offset += 1 + class_len
            length = integer(keys, offset)
            value = slice_at(keys, offset + 4, length).decode("utf-16-le")
            return {"serialized_assembly": assembly, "serialized_class": class_name, "json": value}
        raise ValueError(f"Unsupported Addressables key kind: {kind}")

    bucket_count = integer(buckets, 0)
    if bucket_count < 0 or bucket_count > (len(buckets) - 4) // 8:
        raise ValueError("Invalid Addressables bucket count")
    if integer(keys, 0) != bucket_count:
        raise ValueError("Addressables key count differs from bucket count")
    bucket_rows = []
    offset = 4
    for _ in range(bucket_count):
        key_offset, count = integer(buckets, offset), integer(buckets, offset + 4)
        offset += 8
        if count < 0 or count > (len(buckets) - offset) // 4:
            raise ValueError("Invalid Addressables bucket entries")
        indices = [integer(buckets, offset + i * 4) for i in range(count)]
        offset += count * 4
        bucket_rows.append((key_at(key_offset), indices))
    if offset != len(buckets):
        raise ValueError("Trailing Addressables bucket data")
    count = integer(entries, 0)
    if count < 0 or len(entries) != 4 + count * 28:
        raise ValueError("Invalid Addressables location table length")
    location_keys = [[] for _ in range(count)]
    for key, indices in bucket_rows:
        for index in indices:
            if index < 0 or index >= count:
                raise ValueError("Addressables bucket points outside location table")
            location_keys[index].append(key)
    ids = catalog.get("m_InternalIds", [])
    prefixes = catalog.get("m_InternalIdPrefixes", [])
    providers = catalog.get("m_ProviderIds", [])
    types = catalog.get("m_resourceTypes", [])
    result = []
    for index in range(count):
        internal, provider, dependency, dependency_hash, data_offset, primary, resource_type = struct.unpack_from("<7i", entries, 4 + index * 28)
        if not (0 <= internal < len(ids) and 0 <= provider < len(providers)
                and 0 <= primary < bucket_count and 0 <= resource_type < len(types)):
            raise ValueError("Addressables location contains an out-of-range index")
        if dependency < -1 or dependency >= bucket_count:
            raise ValueError("Addressables dependency points outside key table")
        result.append({"index": index, "internal_id": _expanded_id(prefixes, ids[internal]),
                       "provider": providers[provider], "resource_type": types[resource_type],
                       "primary_key": bucket_rows[primary][0], "keys": location_keys[index],
                       "dependency_key": None if dependency == -1 else bucket_rows[dependency][0],
                       "dependency_location_indices": [] if dependency == -1 else bucket_rows[dependency][1]})
    return result


def _yaml_references(path: Path) -> tuple[list[str], list[str]]:
    if path.suffix.lower() not in YAML_EXTENSIONS:
        return [], []
    refs: set[str] = set()
    scripts: set[str] = set()
    with path.open("rb") as stream:
        if not stream.read(6).startswith(b"%YAML"):
            return [], []
        stream.seek(0)
        for line in stream:
            if b"guid:" not in line:
                continue
            text = line.decode("utf-8", errors="replace")
            refs.update(x.lower() for x in REFERENCE.findall(text))
            scripts.update(x.lower() for x in SCRIPT_REFERENCE.findall(text))
    refs.discard("0" * 32)
    scripts.discard("0" * 32)
    return sorted(refs), sorted(scripts)


def build_asset_map(project_path: Path, catalog_path: Path | None = None) -> dict:
    """Index exported identities without inventing original GUIDs or bindings.

    Path matches are evidence for later review, not rewritten runtime addresses.
    Multiple exports from one source (notably FBX files) remain explicit.
    """
    project_path = _exported_project(project_path.resolve(strict=True))
    assets_dir = project_path / "Assets"
    if not assets_dir.is_dir():
        raise ValueError(f"AssetRipper did not export an Assets directory: {project_path}")
    _assert_no_symlinks(assets_dir)
    rows = []
    guid_paths: dict[str, list[str]] = defaultdict(list)
    exact: dict[str, list[dict]] = defaultdict(list)
    stems: dict[str, list[dict]] = defaultdict(list)
    names: dict[str, list[dict]] = defaultdict(list)
    for meta in sorted(assets_dir.rglob("*.meta")):
        asset = Path(str(meta)[:-5])
        if not asset.is_file():
            continue
        match = GUID.search(meta.read_text(encoding="utf-8", errors="replace"))
        if not match:
            continue
        relative = asset.relative_to(project_path).as_posix()
        refs, scripts = _yaml_references(asset)
        row = {"path": relative, "guid": match.group(1).lower(), "size": asset.stat().st_size,
               "code_quarantined": _is_code(Path(relative)), "shader_quarantined": _is_compiled_shader(asset),
               "references": refs, "script_references": scripts}
        rows.append(row)
        guid_paths[row["guid"]].append(relative)
        exact[_canonical(relative)].append(row)
        stems[str(Path(_canonical(relative)).with_suffix(""))].append(row)
        names[asset.stem.casefold()].append(row)
    addresses = []
    locations = []
    catalog_hash = None
    if catalog_path and catalog_path.is_file():
        raw = catalog_path.read_bytes()
        catalog_hash = hashlib.sha256(raw).hexdigest()
        catalog = json.loads(raw)
        locations = decode_catalog_locations(catalog)
        prefixes = catalog.get("m_InternalIdPrefixes", [])
        for internal_id in catalog.get("m_InternalIds", []):
            if not isinstance(internal_id, str):
                continue
            # ContentCatalogData expands an optional numeric prefix, e.g. 0#path.
            internal_id = _expanded_id(prefixes, internal_id)
            if internal_id.lower().endswith(".bundle"):
                continue
            normalized = _canonical(internal_id)
            candidates = exact.get(normalized, [])
            kind = "exact_path"
            if not candidates and "/" in internal_id:
                candidates = stems.get(str(Path(normalized).with_suffix("")), [])
                kind = "same_stem"
            elif not candidates and "/" not in internal_id:
                candidates = [x for x in names.get(internal_id.casefold(), []) if x["path"].lower().endswith(".unity")]
                kind = "scene_name"
            addresses.append({"internal_id": internal_id, "match": kind if candidates else "unmatched",
                              "ambiguous": len(candidates) > 1,
                              "candidates": [{"path": x["path"], "guid": x["guid"]} for x in candidates]})
    known = set(guid_paths)
    unresolved = sorted({guid for row in rows for guid in row["references"] if guid not in known})
    return {"schema_version": SCHEMA_VERSION, "project_path": str(project_path), "assets": rows, "catalog_sha256": catalog_hash,
            "catalog_identities": addresses,
            "catalog_locations": locations,
            "duplicate_guids": {guid: paths for guid, paths in guid_paths.items() if len(paths) > 1},
            "unresolved_references": unresolved,
            "summary": {"assets": len(rows), "code_quarantined": sum(row["code_quarantined"] for row in rows),
                        "shaders_quarantined": sum(row["shader_quarantined"] for row in rows),
                        "extensions": dict(sorted(Counter(Path(row["path"]).suffix.lower() for row in rows).items())),
                        "catalog_identities": len(addresses), "matched_identities": sum(bool(x["candidates"]) for x in addresses),
                        "catalog_locations": len(locations),
                        "ambiguous_identities": sum(x["ambiguous"] for x in addresses),
                        "unresolved_reference_guids": len(unresolved)}}


def extract_assets(input_path: Path, work_dir: Path) -> dict:
    """Run a full-app export in fresh quarantine; publish a pointer on success."""
    app = _resolve_app(input_path)
    work = _safe_work_dir(work_dir, app)
    executable = work / "tools/assetripper/AssetRipper.GUI.Free"
    if not executable.is_file():
        raise FileNotFoundError(f"AssetRipper is missing at {executable}; run the bootstrap command first")
    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8]
    run = work / "assets/runs" / run_id
    run.mkdir(parents=True, exist_ok=False)
    project = run / "project"
    manifest = _manifest(app)
    _json_write(run / "input-manifest.json", {"files": manifest})
    tool = _tool_lock()
    tool["executable_sha256"] = sha256_file(executable)
    report = {"schema_version": SCHEMA_VERSION, "status": "running", "run_id": run_id,
              "input_path": str(app), "project_path": str(project), "log_path": str(run / "assetripper.log"),
              "asset_map_path": str(run / "asset-map.json"), "settings": dict(EXPORT_SETTINGS), "tool": tool,
              "input_manifest_path": str(run / "input-manifest.json"), "input_fingerprint": fingerprint_manifest(manifest),
              "warnings": ["Generated script bodies are placeholders and remain quarantined.",
                           "Compiled shader data is preserved; shader source has not been reconstructed.",
                           "Catalog path matches require review before generating Addressables bindings."]}
    _json_write(run / "report.json", report)
    process = console = None
    try:
        process, base, console = _start_ripper(executable.resolve(), run)
        _request(base, "/Settings/Update", EXPORT_SETTINGS)
        _request(base, "/LoadFolder", {"Path": str(app)})
        if project.exists():
            raise RuntimeError("Fresh export destination unexpectedly exists; refusing destructive export")
        _request(base, "/Export/UnityProject", {"Path": str(project), "CreateSubfolder": "false"})
        return finalize_asset_export(run, work)
    except BaseException as exc:
        report.update(status="failed", error=str(exc))
        _json_write(run / "report.json", report)
        raise
    finally:
        if process is not None:
            _stop_ripper(process, console)


def finalize_asset_export(run_dir: Path, work_dir: Path) -> dict:
    """Retry indexing a completed retained export without running extraction."""
    work = work_dir.expanduser().resolve(strict=True)
    run = run_dir.expanduser().resolve(strict=True)
    if (work / "assets/runs") not in run.parents:
        raise ValueError("The export run must be inside work/assets/runs")
    report = json.loads((run / "report.json").read_text())
    log = run / "assetripper.log"
    log_text = log.read_text(encoding="utf-8", errors="replace")
    if "Finished post-export" not in log_text:
        raise RuntimeError(f"AssetRipper did not report a completed export; see {log}")
    app = _validate_export_identity(report)
    project = Path(report["project_path"]).resolve(strict=True)
    if run not in project.parents:
        raise ValueError("The export project must be inside its run directory")
    catalog = app / "Contents/Resources/Data/StreamingAssets/aa/catalog.json"
    mapping = build_asset_map(project, catalog)
    if not mapping["assets"]:
        raise RuntimeError("AssetRipper reported completion but produced no indexed assets")
    _json_write(run / "asset-map.json", mapping)
    report.pop("error", None)
    report.update(status="exported", project_path=mapping["project_path"],
                  asset_map_path=str(run / "asset-map.json"), log_path=str(log), summary=mapping["summary"])
    _json_write(run / "report.json", report)
    _json_write(work / "assets/latest-assets.json", report)
    return report


def _owned_directory(path: Path) -> None:
    if path.is_symlink():
        raise ValueError(f"Refusing to replace symlink: {path}")
    if not path.exists():
        return
    marker = path / MARKER
    try:
        owned = json.loads(marker.read_text())["owner"] == OWNER
    except (OSError, ValueError, KeyError):
        owned = False
    if not owned:
        raise ValueError(f"Refusing to replace unowned directory: {path}")


def _replace_owned_directories(replacements: list[tuple[Path, Path]]) -> list[Path]:
    """Swap same-filesystem prepared copies; roll back all targets on error."""
    for destination, _ in replacements:
        _owned_directory(destination)
    backups = []
    installed = []
    try:
        for destination, staged in replacements:
            if destination.exists():
                backup = destination.with_name("." + destination.name + ".backup-" + uuid.uuid4().hex)
                destination.rename(backup)
                backups.append((destination, backup))
            staged.rename(destination)
            installed.append(destination)
    except BaseException:
        for destination in reversed(installed):
            shutil.rmtree(destination)
        for destination, backup in reversed(backups):
            backup.rename(destination)
        raise
    # Preserve prior copies for manual recovery; never recursively erase assets.
    return [backup for _, backup in backups]


def prepare_assets(repo_root: Path, work_dir: Path) -> dict:
    """Promote data and authored StreamingAssets while excluding generated code."""
    repo = repo_root.expanduser().resolve(strict=True)
    work = work_dir.expanduser().resolve(strict=True)
    report = json.loads((work / "assets/latest-assets.json").read_text())
    if report.get("status") != "exported":
        raise ValueError("The latest asset export did not complete")
    _validate_export_identity(report)
    project = _exported_project(Path(report["project_path"]).resolve(strict=True))
    if work not in project.parents:
        raise ValueError("The export project must be inside the work directory")
    source = project / "Assets"
    _assert_no_symlinks(source)
    mapping = json.loads(Path(report["asset_map_path"]).read_text())
    quarantined_guids = {row["guid"] for row in mapping["assets"] if row["code_quarantined"]}
    shader_guids = {row["guid"] for row in mapping["assets"] if row.get("shader_quarantined")}
    dangling = sorted({guid for row in mapping["assets"] if not row["code_quarantined"]
                       for guid in row["script_references"] if guid in quarantined_guids})
    assets = repo / "Assets"
    if assets.is_symlink():
        raise ValueError("Refusing a symlinked Assets directory")
    assets.mkdir(exist_ok=True)
    targets = [(assets / "Recovered", source), (assets / "StreamingAssets", source / "StreamingAssets")]
    for destination, _ in targets:
        _owned_directory(destination)
    staging = assets / (".lucid-assets-" + uuid.uuid4().hex)
    staging.mkdir()
    replacements = []
    audio_reports = {}
    copied = skipped_code = skipped_shader = 0
    try:
        for destination, source_dir in targets:
            stage = staging / destination.name
            stage.mkdir()
            if source_dir.exists():
                for path in sorted(source_dir.rglob("*")):
                    if not path.is_file():
                        continue
                    relative = path.relative_to(source_dir)
                    if destination.name == "Recovered" and relative.parts[0] in {"StreamingAssets", "StreamingAssets.meta"}:
                        continue
                    if destination.name == "Recovered":
                        if _is_code(relative):
                            skipped_code += 1
                            continue
                        if _is_compiled_shader(path):
                            skipped_shader += 1
                            continue
                    output = stage / relative
                    output.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(path, output)
                    copied += 1
            audio_reports[destination.name] = normalize_audio(stage)
            _json_write(stage / MARKER, {"owner": OWNER, "run_id": report["run_id"]})
            replacements.append((destination, stage))
        backups = _replace_owned_directories(replacements)
        if backups:
            backup_dir = work / "assets/backups" / uuid.uuid4().hex
            backup_dir.mkdir(parents=True)
            retained = []
            for backup in backups:
                retained_path = backup_dir / backup.name
                shutil.move(str(backup), str(retained_path))
                retained.append(retained_path)
            backups = retained
        result = {"schema_version": SCHEMA_VERSION, "status": "prepared", "run_id": report["run_id"],
                  "recovered_path": str(assets / "Recovered"), "streaming_assets_path": str(assets / "StreamingAssets"),
                  "copied_files": copied, "skipped_code_files": skipped_code, "skipped_shader_files": skipped_shader,
                  "input_fingerprint": report["input_fingerprint"], "tool": report["tool"], "audio_normalization": audio_reports,
                  "backup_paths": [str(path) for path in backups], "quarantined_script_guids": dangling,
                  "quarantined_shader_guids": sorted(shader_guids),
                  "warnings": ["Missing game MonoScripts are expected until maintained implementations are restored.",
                               "Compiled shaders remain in quarantine until their source or compatible replacement is restored.",
                               "Preparation preserves exported GUIDs; it does not repair package script references or recreate gameplay."]}
        _json_write(work / "assets/latest-prepare.json", result)
        return result
    finally:
        shutil.rmtree(staging, ignore_errors=True)

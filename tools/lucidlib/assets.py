"""Export supplied Unity assets into quarantine, then promote data only.

AssetRipper's headless HTTP export deletes a nonempty destination. This module
never gives it an existing destination and never points it at the repository.
Generated scripts describe serialization; they are not recovered gameplay.
"""

from __future__ import annotations

from collections import Counter, defaultdict
import base64
import ctypes
import ctypes.util
import errno
import functools
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import struct
import time
from urllib import error, parse, request
import uuid

from .audio import normalize_audio
from .bootstrap import (ToolError, executable as native_executable, host_lock,
                        managed_path, tool_work_dir, tree_seal, validate_work_dir,
                        verified_install_seal, verified_tool_path)
from . import inputvariants
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
# Unity 2022.3.54f1 PluginImporter output after disabling every platform.
# UnityFS files remain raw StreamingAssets data; only importer metadata changes.
_DISABLED_BUNDLE_PLUGIN_META = """fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 1
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Editor: 0
        Exclude Linux64: 0
        Exclude OSXUniversal: 0
        Exclude Win: 0
        Exclude Win64: 0
  - first:
      Any:
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
  - first:
      Standalone: Linux64
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 0
      settings:
        CPU: x86_64
  - first:
      Standalone: Win
    second:
      enabled: 0
      settings:
        CPU: x86
  - first:
      Standalone: Win64
    second:
      enabled: 0
      settings:
        CPU: x86_64
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData:
  assetBundleName:
  assetBundleVariant:
"""


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


def _pinned_tools() -> list[dict]:
    """AssetRipper identities for every pinned host; this host's build comes first."""
    lock = json.loads((Path(__file__).resolve().parents[1] / "tool-lock.json").read_text())
    pinned = [lock["artifacts"]["assetripper"]]
    pinned += [host["artifacts"]["assetripper"] for host in lock.get("hosts", {}).values()
               if "assetripper" in host["artifacts"]]
    try:
        pinned.insert(0, host_lock(lock)["artifacts"]["assetripper"])
    except ToolError:
        pass
    return [{"name": "AssetRipper", "version": tool["version"], "archive_sha256": tool["sha256"]} for tool in pinned]


def _tool_lock() -> dict:
    return _pinned_tools()[0]


def _validate_export_identity(report: dict) -> Path:
    expected = report.get("input_fingerprint")
    if not expected or not report.get("tool"):
        raise ValueError("Asset export lacks captured input/tool provenance; extract assets again")
    app = _resolve_app(Path(report["input_path"]))
    current = fingerprint_manifest(_manifest(app))
    if current != expected:
        raise ValueError("Supplied game content differs from the asset export; extract assets again")
    # An export from any pinned host build is equivalent; a different version is not.
    matches = any(all(report["tool"].get(key) == value for key, value in tool.items()) for tool in _pinned_tools())
    if not matches or report.get("settings") != EXPORT_SETTINGS:
        raise ValueError("Asset export tool version/settings differ from this pipeline; extract assets again")
    return app


def _physical_path(path: Path) -> Path:
    """Reject symbolic-link ancestors for repository paths and file writes."""
    path = Path(path).expanduser()
    if ".." in path.parts:
        raise ValueError("Asset paths cannot contain parent traversal")
    path = Path(os.path.abspath(os.fspath(path)))
    if any(component.is_symlink() for component in (path, *path.parents)):
        raise ValueError(f"Refusing symbolic link in asset path: {path}")
    return path


def _cache_path(work: Path, path: Path) -> Path:
    """Check captured and generated descendants without resolving away links."""
    path = _physical_path(path)
    try:
        relative = path.relative_to(work)
    except ValueError as exc:
        raise ValueError("Asset path must be inside the work directory") from exc
    return managed_path(work, *relative.parts)


def _json_write(path: Path, value: dict) -> None:
    path = _physical_path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    stream = temporary.open("x", encoding="utf-8")
    try:
        with stream:
            stream.write(json.dumps(value, indent=2, sort_keys=True) + "\n")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


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
    work = _physical_path(work_dir)
    protected = protected.resolve()
    if work == protected or work in protected.parents or protected in work.parents:
        raise ValueError("The work directory must be separate from the supplied game bundle")
    work = validate_work_dir(work)
    work.mkdir(parents=True, exist_ok=True)
    return work


def _assert_no_symlinks(root: Path) -> None:
    root = _physical_path(root)
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


def _validate_streaming_bundle(path: Path) -> None:
    """Recognize the supplied UnityFS v8 header without changing bundle data."""
    with path.open("rb") as stream:
        header = stream.read(512)
    if not header.startswith(b"UnityFS\0"):
        raise ValueError(f"Unsupported StreamingAssets .bundle format: {path}")
    if len(header) < 12 or struct.unpack_from(">I", header, 8)[0] != 8:
        raise ValueError(f"Unsupported or truncated UnityFS bundle version: {path}")
    offset = 12
    for _ in range(2):
        end = header.find(b"\0", offset)
        if end < offset + 1 or end - offset > 128:
            raise ValueError(f"Invalid UnityFS bundle version string: {path}")
        try:
            header[offset:end].decode("ascii")
        except UnicodeDecodeError as exc:
            raise ValueError(f"Invalid UnityFS bundle version string: {path}") from exc
        offset = end + 1
    if offset + 20 > len(header):
        raise ValueError(f"Truncated UnityFS bundle header: {path}")
    size, compressed_size, uncompressed_size, _ = struct.unpack_from(">QIII", header, offset)
    if size != path.stat().st_size or not compressed_size or not uncompressed_size or compressed_size > size - offset - 20:
        raise ValueError(f"Invalid UnityFS bundle size or block-info bounds: {path}")


def _bundle_meta_guid(path: Path) -> str | None:
    if path.is_symlink():
        raise ValueError(f"Refusing symlink in StreamingAssets bundle metadata: {path}")
    if not path.exists():
        return None
    if not path.is_file() or path.stat().st_size > 1024 * 1024:
        raise ValueError(f"Invalid StreamingAssets bundle metadata: {path}")
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        raise ValueError(f"Invalid StreamingAssets bundle metadata: {path}") from exc
    matches = list(GUID.finditer(text))
    if not re.search(r"^fileFormatVersion: 2\s*$", text, re.MULTILINE) or len(re.findall(r"^guid:", text, re.MULTILINE)) != 1 or len(matches) != 1 or matches[0][1] == "0" * 32:
        raise ValueError(f"Invalid StreamingAssets bundle GUID metadata: {path}")
    return matches[0][1]


def _normalize_streaming_bundles(stage: Path, previous: Path) -> dict:
    """Disable native-plugin loading for recognized data bundles in staging."""
    result = {"policy": "unityfs-v8-disable-plugin-platforms-v1", "bundle_count": 0,
              "normalized_plugin_importers": 0, "rewritten_meta_files": 0,
              "preserved_export_guids": 0, "preserved_prepared_guids": 0, "generated_guids": 0}
    guids = {}
    # Prior Unity-created metas can be reused, but never follow a generated symlink.
    if previous.exists():
        _assert_no_symlinks(previous)
    for bundle in sorted(stage.rglob("*")):
        if bundle.suffix.lower() != ".bundle":
            continue
        if not bundle.is_file() or bundle.is_symlink():
            raise ValueError(f"Invalid StreamingAssets bundle path: {bundle}")
        _validate_streaming_bundle(bundle)
        relative = bundle.relative_to(stage)
        meta = bundle.with_name(bundle.name + ".meta")
        exported_guid = _bundle_meta_guid(meta)
        prior_guid = _bundle_meta_guid(previous / relative.parent / (relative.name + ".meta"))
        if exported_guid and prior_guid and exported_guid.lower() != prior_guid.lower():
            raise ValueError(f"Conflicting StreamingAssets bundle GUIDs: {relative}")
        if exported_guid:
            guid = exported_guid
            result["preserved_export_guids"] += 1
        elif prior_guid:
            guid = prior_guid
            result["preserved_prepared_guids"] += 1
        else:
            identity = "ProjectLucid/StreamingAssets/" + relative.as_posix()
            guid = hashlib.sha256(identity.encode("utf-8")).hexdigest()[:32]
            result["generated_guids"] += 1
        if guid.lower() in guids:
            raise ValueError(f"Duplicate StreamingAssets bundle GUID: {relative} and {guids[guid.lower()]}")
        guids[guid.lower()] = relative
        normalized = _DISABLED_BUNDLE_PLUGIN_META.format(guid=guid).encode("utf-8")
        if not meta.exists() or meta.read_bytes() != normalized:
            meta.write_bytes(normalized)
            result["rewritten_meta_files"] += 1
        result["bundle_count"] += 1
        result["normalized_plugin_importers"] += 1
    return result


def _exported_project(stage: Path) -> Path:
    stage = _physical_path(stage)
    for candidate in [stage / "ExportedProject", stage]:
        if _physical_path(candidate / "Assets").is_dir():
            return candidate
    raise ValueError(f"AssetRipper did not export an Assets directory below: {stage}")


def _request(base: str, endpoint: str, fields: dict | None = None, timeout: int = 3600) -> bytes:
    data = None if fields is None else parse.urlencode(fields).encode("utf-8")
    req = request.Request(base + endpoint, data=data)
    # AssetRipper redirects after a completed command; urllib follows it.
    # Never route the local control plane through an environment HTTP proxy.
    with request.build_opener(request.ProxyHandler({})).open(req, timeout=timeout) as response:
        return response.read()


def _runtime_ripper(install: Path, runtime: Path, seal: dict) -> None:
    """Copy the verified AssetRipper closure into a fresh, empty per-run directory.

    AssetRipper writes temp/<random> beside its executable (its base directory),
    not under the working directory, so it must never run from the sealed install.
    The copy is checked against the seal recorded from the pinned archive, so a
    change to the install after verification cannot reach the launched closure.
    """
    directories = []
    for path in sorted(install.rglob("*")):
        info = path.lstat()
        target = runtime / path.relative_to(install)
        if stat.S_ISDIR(info.st_mode):
            target.mkdir(mode=0o700)
            directories.append((target, stat.S_IMODE(info.st_mode)))
        elif stat.S_ISREG(info.st_mode) and info.st_nlink == 1:
            with path.open("rb") as source, target.open("xb") as output:
                shutil.copyfileobj(source, output)
            target.chmod(stat.S_IMODE(info.st_mode))
        else:
            raise ToolError(f"AssetRipper installation contains a link or special file: {path}")
    # Apply directory modes last so read-only directories can still be filled.
    for target, mode in reversed(directories):
        target.chmod(mode)
    if tree_seal(runtime) != seal:
        raise ToolError("AssetRipper runtime copy differs from the pinned verified closure")


def _discard_runtime_ripper(runtime: Path) -> dict | None:
    """Remove the per-run copy and its tool-owned output; describe it if it must stay."""
    try:
        # The copy is ours; make pinned read-only directories removable first.
        for directory, _, _ in os.walk(runtime):
            if not os.path.islink(directory):
                os.chmod(directory, 0o700)
        shutil.rmtree(runtime)
    except OSError as exc:
        return {"path": str(runtime), "error": str(exc)}
    return None


def _record_retained_runtime(run: Path, work: Path, retained: dict, result: dict | None,
                             failure: BaseException | None) -> None:
    """Record a kept runtime copy in the run report without masking an extraction failure."""
    if result is not None:
        result["retained_runtime_copy"] = retained
    try:
        report_path = _cache_path(work, run / "report.json")
        report = json.loads(report_path.read_text())
        report["retained_runtime_copy"] = retained
        _json_write(report_path, report)
    except Exception as exc:
        if failure is None:
            raise
        # The extraction error is already propagating; report the kept copy beside it.
        print(f"AssetRipper runtime copy kept at {retained['path']} ({retained['error']}); "
              f"recording it in the run report also failed: {exc}", file=sys.stderr)


def _stale_runtime_copies(tools: Path) -> list[str]:
    """List runtime copies an earlier, interrupted extraction left; never remove them here.

    A copy can belong to a live extraction in another process, so it is only reported.
    """
    return sorted(str(path) for path in tools.glob(".run-assetripper-*"))


def _start_ripper(executable: Path, run_dir: Path) -> tuple[subprocess.Popen, str, object]:
    run_dir = _physical_path(run_dir)
    log = _physical_path(run_dir / "assetripper.log")
    console_path = _physical_path(run_dir / "assetripper-console.log")
    console = console_path.open("xb")
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
    project_path = _exported_project(_physical_path(project_path).resolve(strict=True))
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
    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8]
    run = managed_path(work, "assets", "runs", run_id)
    # Reject retained publication links before creating any extraction output.
    managed_path(work, "assets", "latest-assets.json")
    executable = native_executable(tool_work_dir(work) / "tools/assetripper/AssetRipper.GUI.Free")
    if not executable.is_file():
        raise FileNotFoundError(f"AssetRipper is missing at {executable}; run the bootstrap command first")
    executable = verified_tool_path(work, "assetripper")
    manifest = _manifest(app)
    fingerprint = fingerprint_manifest(manifest)
    # Admit only a declared variant qualified for the prepare/binding chain, before
    # any run directory or export exists; resolve_bindings repeats the code check.
    variant = inputvariants.admit(fingerprint["critical_files"], inputvariants.ASSET_PREPARATION)
    run.mkdir(parents=True, exist_ok=False)
    project = _cache_path(work, run / "project")
    _json_write(_cache_path(work, run / "input-manifest.json"), {"files": manifest})
    tool = _tool_lock()
    tool["executable_sha256"] = sha256_file(executable)
    report = {"schema_version": SCHEMA_VERSION, "status": "running", "run_id": run_id,
              "input_path": str(app), "project_path": str(project), "log_path": str(run / "assetripper.log"),
              "asset_map_path": str(run / "asset-map.json"), "settings": dict(EXPORT_SETTINGS), "tool": tool,
              "input_manifest_path": str(run / "input-manifest.json"), "input_fingerprint": fingerprint,
              "input_variant": variant, "warnings": ["Generated script bodies are placeholders and remain quarantined.",
                           "Compiled shader data is preserved; shader source has not been reconstructed.",
                           "Catalog path matches require review before generating Addressables bindings."]}
    install = managed_path(tool_work_dir(work), "tools", "assetripper")
    runtime = managed_path(tool_work_dir(work), "tools", f".run-assetripper-{run_id}")
    stale = _stale_runtime_copies(install.parent)
    if stale:
        report["stale_runtime_copies"] = stale
    _json_write(_cache_path(work, run / "report.json"), report)
    process = console = result = failure = None
    owned_runtime = False
    try:
        runtime.mkdir(mode=0o700, exist_ok=False)
        owned_runtime = True
        _runtime_ripper(install, runtime, verified_install_seal(install))
        # Run the copy so AssetRipper's base-directory output stays in this run's tree.
        process, base, console = _start_ripper(runtime / executable.relative_to(install), run)
        _request(base, "/Settings/Update", EXPORT_SETTINGS)
        _request(base, "/LoadFolder", {"Path": str(app)})
        project = _cache_path(work, project)
        if project.exists():
            raise RuntimeError("Fresh export destination unexpectedly exists; refusing destructive export")
        _request(base, "/Export/UnityProject", {"Path": str(project), "CreateSubfolder": "false"})
        # The pinned closure must still match at the publication boundary itself, after
        # indexing, so a change during a long index is caught before anything is written.
        result = finalize_asset_export(run, work, before_publish=lambda: verified_tool_path(work, "assetripper"))
    except BaseException as exc:
        failure = exc
        report.update(status="failed", error=str(exc))
        _json_write(_cache_path(work, run / "report.json"), report)
        raise
    finally:
        try:
            if process is not None:
                _stop_ripper(process, console)
        finally:
            if owned_runtime:
                retained = _discard_runtime_ripper(runtime)
                if retained is not None:
                    _record_retained_runtime(run, work, retained, result, failure)
    return result


IL2CPP_BACKEND_LINE = "Files use the 'IL2Cpp' scripting backend."
UNKNOWN_BACKEND_LINE = "Switching to the 'Unknown' scripting backend."


def finalize_asset_export(run_dir: Path, work_dir: Path, before_publish=None) -> dict:
    """Index a completed export and publish it; before_publish runs just before the writes."""
    work = validate_work_dir(work_dir.expanduser())
    run = _cache_path(work, run_dir)
    if managed_path(work, "assets", "runs") not in run.parents:
        raise ValueError("The export run must be inside work/assets/runs")
    report_path = _cache_path(work, run / "report.json")
    map_path = _cache_path(work, run / "asset-map.json")
    latest = managed_path(work, "assets", "latest-assets.json")
    log = _cache_path(work, run / "assetripper.log")
    report = json.loads(report_path.read_text())
    project = _cache_path(work, Path(report["project_path"]))
    if run not in project.parents:
        raise ValueError("The export project must be inside its run directory")
    log_text = log.read_text(encoding="utf-8", errors="replace")
    if "Finished post-export" not in log_text:
        raise RuntimeError(f"AssetRipper did not report a completed export; see {log}")
    # A metadata decode failure makes AssetRipper fall back to its Unknown scripting
    # backend and still finish, without the IL2CPP type evidence the export needs.
    if UNKNOWN_BACKEND_LINE in log_text or IL2CPP_BACKEND_LINE not in log_text:
        raise RuntimeError(f"AssetRipper did not export with the IL2CPP scripting backend; see {log}")
    app = _validate_export_identity(report)
    _safe_work_dir(work, app)
    catalog = app / "Contents/Resources/Data/StreamingAssets/aa/catalog.json"
    mapping = build_asset_map(project, catalog)
    if not mapping["assets"]:
        raise RuntimeError("AssetRipper reported completion but produced no indexed assets")
    if before_publish is not None:
        before_publish()
    _json_write(_cache_path(work, map_path), mapping)
    report.pop("error", None)
    report.update(status="exported", project_path=mapping["project_path"],
                  asset_map_path=str(map_path), log_path=str(log), summary=mapping["summary"])
    _json_write(_cache_path(work, report_path), report)
    _json_write(_cache_path(work, latest), report)
    return report


def _owned_directory(path: Path) -> None:
    path = _physical_path(path)
    if not path.exists():
        return
    marker = _physical_path(path / MARKER)
    try:
        owned = json.loads(marker.read_text())["owner"] == OWNER
    except (OSError, ValueError, KeyError):
        owned = False
    if not owned:
        raise ValueError(f"Refusing to replace unowned directory: {path}")


def _replace_owned_directories(replacements: list[tuple[Path, Path]],
                               plan: list[tuple[Path, Path, Path | None]]) -> None:
    """Swap same-filesystem prepared copies; the caller owns the plan and rollback."""
    for destination, staged in replacements:
        _owned_directory(destination)
        _assert_no_symlinks(staged)
    # Each swap is recorded before its renames, so an interruption between a rename
    # and its bookkeeping (#460) still leaves rollback knowing what to reconcile.
    for destination, staged in replacements:
        backup = None
        if destination.exists():
            backup = _physical_path(destination.with_name("." + destination.name + ".backup-" + uuid.uuid4().hex))
            if backup.exists():
                raise ValueError(f"Asset backup destination already exists: {backup}")
        plan.append((destination, staged, backup))
        if backup is not None:
            destination.rename(backup)
        staged.rename(destination)


def _rollback_swaps(plan: list[tuple[Path, Path, Path | None]], error: BaseException) -> None:
    """Undo recorded swaps from the paths' actual state, newest first.

    A staged tree that is gone was installed at its destination; a backup that
    exists holds the prior tree. A swap that cannot be undone is noted on the
    original exception (or printed before Python 3.11) with the affected paths."""
    for destination, staged, backup in reversed(plan):
        try:
            if not staged.exists() and destination.exists():
                shutil.rmtree(destination)
            if backup is not None and backup.exists():
                backup.rename(destination)
        except Exception as failure:
            if backup is None:
                note = f"Could not remove the newly installed tree at {destination}: {failure}"
            else:
                note = f"Could not restore {destination} ({failure}); its prior tree remains at {backup}"
            if hasattr(error, "add_note"):
                error.add_note(note)
            else:
                print(note, file=sys.stderr)


def _clone_file(source: Path, output: Path) -> bool:
    """Copy-on-write clone (macOS clonefile(2)); False when it is unavailable.

    The clone keeps mode, times and xattrs like copy2 and shares blocks until
    either file changes, so same-volume prepare costs no space or writes."""
    if sys.platform != "darwin":
        return False
    try:
        return _libc().clonefile(os.fsencode(source), os.fsencode(output), 0) == 0
    except (OSError, AttributeError):
        return False


@functools.lru_cache(maxsize=None)
def _libc() -> ctypes.CDLL:
    # Prepare clones tens of thousands of files; look libc up once.
    return ctypes.CDLL(ctypes.util.find_library("c") or "libc.dylib", use_errno=True)


def _copy_file(source: Path, output: Path) -> None:
    """Clone when the volume allows it, otherwise copy exactly as before."""
    if not _clone_file(source, output):
        shutil.copy2(source, output)


def _relocate_tree(source: Path, target: Path) -> bool:
    """Move a directory tree to an absent target.

    Renames when both paths share a filesystem. The work cache may sit on
    another volume from Assets; then the tree is copied in full first, a
    partial copy is removed, and True is returned so the caller records the
    complete copy before deleting the source itself. A partial copy that
    cannot be fully removed is renamed aside to a ".failed-partial-" sibling,
    so the target path never holds a partial tree."""
    try:
        os.rename(source, target)
        return False
    except OSError as exc:
        if exc.errno != errno.EXDEV:
            raise
    try:
        shutil.copytree(source, target, symlinks=True)
    except BaseException:
        shutil.rmtree(target, ignore_errors=True)
        if os.path.lexists(target):
            os.rename(target, target.with_name("." + target.name + ".failed-partial-" + uuid.uuid4().hex))
        raise
    return True


def _relocate_recorded(source: Path, target: Path, locations: dict[Path, Path], key: Path) -> bool:
    """_relocate_tree that keeps locations[key] on the tree's real location.

    An interrupt can land after the rename but before _relocate_tree returns,
    so after any exception the record follows whichever path actually holds
    the tree."""
    try:
        return _relocate_tree(source, target)
    except BaseException as error:
        if not os.path.lexists(source) and os.path.lexists(target):
            locations[key] = target
        elif os.path.lexists(source) and os.path.lexists(target):
            # A cross-volume copy keeps the source until the copy is adopted, so the
            # source stays authoritative and the unadopted copy is removed.
            try:
                shutil.rmtree(target)
            except OSError as cleanup:
                note = f"An unadopted copy remains at {target}: {cleanup}"
                if hasattr(error, "add_note"):
                    error.add_note(note)
                else:
                    print(note, file=sys.stderr)
        raise


def _restore_file(path: Path, prior: bytes | None) -> None:
    """Put back a receipt's prior bytes, or its absence, atomically."""
    if prior is None:
        path.unlink(missing_ok=True)
        return
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(prior)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def _restore_swapped_directories(swapped: list[tuple[Path, Path | None]], locations: dict[Path, Path],
                                 backup_dir: Path) -> None:
    """Reinstate the prior owned copies after a post-swap failure.

    locations maps each destination to where its complete prior copy lives
    now. Each new tree is set aside, not erased, until its prior copy is back,
    so a failed restore leaves that destination on the new tree. The first
    failure stops the restore and names every retained copy for manual
    recovery."""
    for destination, backup in reversed(swapped):
        aside = None
        try:
            if destination.exists():
                aside = destination.with_name("." + destination.name + ".failed-" + uuid.uuid4().hex)
                os.rename(destination, aside)
            if backup is not None:
                source = locations[destination]
                copied = _relocate_recorded(source, destination, locations, destination)
                del locations[destination]
                if copied:
                    # The prior tree is active again; a leftover source copy is surplus.
                    shutil.rmtree(source, ignore_errors=True)
                if backup != source:
                    # Cross-volume retention copied the tree but failed while deleting this sibling.
                    shutil.rmtree(backup, ignore_errors=True)
            if aside is not None:
                shutil.rmtree(aside)
        except BaseException as exc:
            if locations.get(destination) == destination:
                # Interrupted after the prior tree was renamed back: it is active again.
                del locations[destination]
            if aside is not None and aside.exists() and not os.path.lexists(destination):
                os.rename(aside, destination)
            current = destination if aside is None or not aside.exists() else aside
            # Name only copies that really exist, plus any partial copy set aside.
            retained = ", ".join(str(path) for path in locations.values() if os.path.lexists(path)) or "none"
            partial = sorted(destination.parent.glob("." + destination.name + ".failed-partial-*"))
            message = (f"Could not restore prior prepared assets at {destination}; newly prepared tree: {current}; "
                       f"retained prior copies: {retained}")
            if partial:
                message += "; partial restore copies set aside: " + ", ".join(str(path) for path in partial)
            if not isinstance(exc, Exception):
                # Let interrupts and exits propagate as themselves once recovery is reported.
                print(message, file=sys.stderr)
                raise
            raise RuntimeError(message) from exc
    if backup_dir.is_dir() and not any(backup_dir.iterdir()):
        backup_dir.rmdir()


def _binding_release(input_fingerprint: dict) -> bool:
    """Whether these critical code hashes match the qualified binding release."""
    from .scriptbindings import BINDING_INPUT
    critical = (input_fingerprint or {}).get("critical_files", {})
    return (critical.get("Contents/Frameworks/GameAssembly.dylib") == BINDING_INPUT["binary_sha256"] and
            critical.get("Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat") == BINDING_INPUT["metadata_sha256"])


def _render_pipeline(repo: Path, project: Path, mapping: dict, bindings: list, input_fingerprint: dict) -> dict:
    """Check the selected render pipeline and map its shader slots to package shaders.

    For the release the binding rules were verified on, the committed
    ProjectSettings' pipeline and its whole closure must be mapped and rebound
    before anything is staged. Other inputs (and fixtures) have no pipeline rules.
    """
    from .rendererbindings import PROFILES, check_pipeline_selection, pass_material_shader_gaps, package_shader_map
    result = {"status": "not_applicable", "shader_map": {}}
    if _binding_release(input_fingerprint):
        result.update(check_pipeline_selection(repo, mapping, bindings, project))
    if any((row.get("assembly"), row.get("full_name")) in PROFILES for row in bindings):
        shaders = package_shader_map(repo, project, mapping)
        result.update(shader_map=shaders["map"], package_shader_count=shaders["package_shader_count"],
                      mapped_exported_shaders=len(shaders["map"]),
                      pass_material_shader_gaps=pass_material_shader_gaps(project, mapping, shaders["map"]))
    return result


def prepare_assets(repo_root: Path, work_dir: Path) -> dict:
    """Promote data and authored StreamingAssets while excluding generated code."""
    repo = _physical_path(repo_root).resolve(strict=True)
    work = validate_work_dir(work_dir.expanduser())
    latest = managed_path(work, "assets", "latest-assets.json")
    prepared = managed_path(work, "assets", "latest-prepare.json")
    # Cache publication and retention must be safe before replacing any assets.
    backup_dir = managed_path(work, "assets", "backups", uuid.uuid4().hex)
    report = json.loads(latest.read_text())
    if report.get("status") != "exported":
        raise ValueError("The latest asset export did not complete")
    app = _validate_export_identity(report)
    _safe_work_dir(work, app)
    project = _exported_project(_cache_path(work, Path(report["project_path"])))
    source = project / "Assets"
    _assert_no_symlinks(source)
    mapping = json.loads(_cache_path(work, Path(report["asset_map_path"])).read_text())
    assets = _physical_path(repo / "Assets")
    if assets == app or assets in app.parents or app in assets.parents:
        raise ValueError("Prepared assets must be separate from the supplied game bundle")
    targets = [(assets / "Recovered", source), (assets / "StreamingAssets", source / "StreamingAssets")]
    for destination, _ in targets:
        _owned_directory(destination)
    from .scriptbindings import resolve_bindings, rewrite_prepared_script_pointers
    script_bindings = resolve_bindings(repo, work, project, mapping, input_fingerprint=report["input_fingerprint"])
    bindings = script_bindings["bindings"]
    bound_guids = {row["exported_guid"] for row in bindings}
    render_pipeline = _render_pipeline(repo, project, mapping, bindings, report["input_fingerprint"])
    shader_map = render_pipeline.pop("shader_map")
    from .materialbindings import maintained_shader_overrides, rewrite_material_shader_pointer
    binding_release = _binding_release(report["input_fingerprint"])
    material_overrides = maintained_shader_overrides(repo, project, mapping) if binding_release else {}
    mapped_assets = {row["path"]: row for row in mapping["assets"]}
    material_path_counts = Counter(row["path"] for row in mapping["assets"])
    material_guid_counts = Counter(row["guid"] for row in mapping["assets"])
    quarantined_guids = {row["guid"] for row in mapping["assets"] if row["code_quarantined"]}
    shader_guids = {row["guid"] for row in mapping["assets"] if row.get("shader_quarantined")}
    dangling = sorted({guid for row in mapping["assets"] if not row["code_quarantined"]
                       for guid in row["script_references"] if guid in quarantined_guids and guid not in bound_guids})
    assets.mkdir(exist_ok=True)
    staging = _physical_path(assets / (".lucid-assets-" + uuid.uuid4().hex))
    staging.mkdir()
    replacements = []
    audio_reports = {}
    bundle_report = {}
    copied = skipped_code = skipped_shader = 0
    rebound_assets = []
    material_assets = []
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
                    output = _physical_path(stage / relative)
                    output.parent.mkdir(parents=True, exist_ok=True)
                    _copy_file(path, output)
                    row = mapped_assets.get("Assets/" + relative.as_posix()) if destination.name == "Recovered" else None
                    if row and bound_guids.intersection(row.get("script_references", [])):
                        meta_guids = GUID.findall(path.with_name(path.name + ".meta").read_text(encoding="utf-8"))
                        if meta_guids != [row["guid"]]:
                            raise ValueError("Rebound asset metadata differs from its asset map: " + row["path"])
                        shader_log = []
                        rewritten, count = rewrite_prepared_script_pointers(output.read_bytes(), bindings, shader_map, shader_log)
                        if not count:
                            raise ValueError("Mapped script reference is absent from exported asset: " + row["path"])
                        output.write_bytes(rewritten)
                        rebound = {"path": row["path"], "guid": row["guid"], "pointers_rewritten": count,
                                   "exported_sha256": sha256_file(path), "prepared_sha256": sha256_file(output)}
                        if shader_log:
                            rebound["shader_slots"] = shader_log
                        rebound_assets.append(rebound)
                    # Ordinary Materials have no MonoScript; their ShaderImporter
                    # kind normalization is independent of approved script bindings.
                    if row and path.suffix.lower() == ".mat" and material_overrides:
                        if material_path_counts[row["path"]] != 1 or material_guid_counts[row["guid"]] != 1:
                            raise ValueError("Ambiguous Material asset-map identity: " + row["path"])
                        material_meta = path.with_name(path.name + ".meta").read_text(encoding="utf-8")
                        meta_guids = GUID.findall(material_meta)
                        if meta_guids != [row["guid"]] or len(re.findall(r"^guid\b.*$", material_meta, re.MULTILINE)) != 1:
                            raise ValueError("Material metadata differs from its asset map: " + row["path"])
                        rewritten, change = rewrite_material_shader_pointer(output.read_bytes(), material_overrides)
                        if change is not None:
                            output.write_bytes(rewritten)
                            material_assets.append({"path": row["path"], "guid": row["guid"], "shader": change,
                                                    "exported_sha256": sha256_file(path), "prepared_sha256": sha256_file(output)})
                    copied += 1
            audio_reports[destination.name] = normalize_audio(stage)
            if destination.name == "StreamingAssets":
                bundle_report = _normalize_streaming_bundles(stage, destination)
            _json_write(stage / MARKER, {"owner": OWNER, "run_id": report["run_id"]})
            replacements.append((destination, stage))
        # Keep the prior receipt's bytes, or its absence, for rollback.
        receipt = _cache_path(work, prepared)
        prior_receipt = receipt.read_bytes() if receipt.exists() else None
        plan: list[tuple[Path, Path, Path | None]] = []
        locations = None
        # Retention and the receipt commit with the swap: on any failure the prior
        # trees go back in place, so they still match the prior receipt.
        try:
            _replace_owned_directories(replacements, plan)
            # Avoid inlined-comprehension trace points that bypass recovery on older Python builds.
            swapped = []
            for destination, _, backup in plan:
                swapped.append((destination, backup))
            complete_locations = {}
            for destination, backup in swapped:
                if backup is not None:
                    complete_locations[destination] = backup
            locations = complete_locations
            backups = []
            if locations:
                backup_dir = _cache_path(work, backup_dir)
                backup_dir.mkdir(parents=True)
                for destination, backup in swapped:
                    if backup is None:
                        continue
                    retained_path = _cache_path(work, backup_dir / backup.name)
                    copied_across_volumes = _relocate_recorded(backup, retained_path, locations, destination)
                    # Record the complete retained copy before deleting a cross-volume source.
                    locations[destination] = retained_path
                    if copied_across_volumes:
                        shutil.rmtree(backup)
                    backups.append(retained_path)
            result = {"schema_version": SCHEMA_VERSION, "status": "prepared", "run_id": report["run_id"],
                      "recovered_path": str(assets / "Recovered"), "streaming_assets_path": str(assets / "StreamingAssets"),
                      "copied_files": copied, "skipped_code_files": skipped_code, "skipped_shader_files": skipped_shader,
                      "input_fingerprint": report["input_fingerprint"], "tool": report["tool"], "audio_normalization": audio_reports,
                      "streaming_bundle_normalization": bundle_report,
                      "material_shader_normalization": {"status": "complete" if binding_release else "not_applicable",
                                                        "assets": material_assets},
                      "backup_paths": [str(path) for path in backups], "quarantined_script_guids": dangling,
                      "script_bindings": {**script_bindings, "references_modified": bool(rebound_assets), "assets": rebound_assets},
                      "render_pipeline": dict(render_pipeline, unresolved_shader_slots=sorted(
                          {(entry["slot"], entry["exported_guid"]) for asset in rebound_assets
                           for entry in asset.get("shader_slots", []) if entry["package_guid"] is None})),
                      "quarantined_shader_guids": sorted(shader_guids),
                      "warnings": ["Missing game MonoScripts are expected until maintained implementations are restored.",
                                   "Compiled shaders remain in quarantine until their source or compatible replacement is restored.",
                                   "Preparation preserves asset GUIDs and restores approved, layout-verified script pointers and qualified Material shader import kinds; gameplay remains unresolved."]}
            _json_write(receipt, result)
        except BaseException as error:
            if locations is None:
                _rollback_swaps(plan, error)
            else:
                _restore_swapped_directories(swapped, locations, backup_dir)
            # The receipt may already be published when a failure follows its
            # rename; with the prior trees back, put back their prior receipt.
            _restore_file(receipt, prior_receipt)
            raise
        return result
    finally:
        shutil.rmtree(staging, ignore_errors=True)

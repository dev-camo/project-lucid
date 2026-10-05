"""Read-only inspection of a user-supplied macOS Sonic Dream Team bundle."""

import hashlib
import json
import plistlib
import re
from pathlib import Path
from urllib.parse import urlsplit

from .macho import inspect_macho
from .metadata import parse_metadata


_RUNTIME_PATH = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_json(path):
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            return json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError("Cannot read valid JSON: %s" % path) from exc


def _locate_app(root):
    if root.suffix == ".app" and root.is_dir():
        return root
    apps = sorted(p for p in root.glob("*.app") if p.is_dir())
    if len(apps) != 1:
        raise ValueError("Input must be a .app bundle or contain exactly one .app bundle")
    return apps[0]


def _manifest(root):
    rows = []
    resolved_root = root.resolve()
    for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix()):
        if path.is_symlink():
            target = path.resolve()
            try:
                target.relative_to(resolved_root)
            except ValueError as exc:
                raise ValueError("Input symlink points outside input: %s" % path) from exc
            if not target.exists():
                raise ValueError("Broken input symlink: %s" % path)
            if target.is_dir():
                rows.append({"path": path.relative_to(root).as_posix(), "kind": "directory_symlink",
                             "target": str(path.readlink())})
                continue
        if not path.is_file():
            continue
        row = {"path": path.relative_to(root).as_posix(), "size": path.stat().st_size,
               "sha256": sha256_file(path)}
        if path.is_symlink():
            row["kind"] = "file_symlink"
            row["target"] = str(path.readlink())
        rows.append(row)
    return rows


def inspect_catalog(catalog_path: Path, settings_path: Path, root: Path) -> dict:
    """Compare all catalog bundle locations with the supplied local files.

    Compressed prefix IDs are expanded before checking. Remote locations are
    reported without network access; unsupported local location formats are
    explicit, so they cannot accidentally count as complete.
    """
    catalog = _read_json(catalog_path)
    settings = _read_json(settings_path)
    if not isinstance(catalog, dict) or not isinstance(settings, dict):
        raise ValueError("Addressables catalog/settings must be JSON objects")
    ids = catalog.get("m_InternalIds")
    prefixes = catalog.get("m_InternalIdPrefixes", [])
    if not isinstance(ids, list) or not all(isinstance(v, str) for v in ids):
        raise ValueError("Addressables catalog m_InternalIds must be a string array")
    if not isinstance(prefixes, list) or not all(isinstance(v, str) for v in prefixes):
        raise ValueError("Addressables catalog m_InternalIdPrefixes must be a string array")
    expanded = []
    for value in ids:
        match = re.match(r"^(\d+)#", value)
        if match:
            index = int(match.group(1))
            if index >= len(prefixes):
                raise ValueError("Addressables internal ID has an invalid prefix index")
            value = prefixes[index] + value[match.end():]
        expanded.append(value)
    aa = catalog_path.parent
    local = set()
    missing = set()
    remote = set()
    unmapped = set()
    for value in expanded:
        parsed = urlsplit(value)
        if not parsed.path.endswith(".bundle"):
            continue
        if parsed.scheme in ("http", "https"):
            remote.add(value)
            continue
        if value.startswith(_RUNTIME_PATH + "/"):
            relative = value[len(_RUNTIME_PATH) + 1:]
        elif not parsed.scheme and not value.startswith(("/", "{")):
            relative = value
        else:
            unmapped.add(value)
            continue
        path = aa / relative
        try:
            path.resolve().relative_to(aa.resolve())
        except ValueError as exc:
            raise ValueError("Addressables bundle location escapes its directory") from exc
        relative_to_root = path.relative_to(root).as_posix()
        local.add(relative_to_root)
        if not path.is_file():
            missing.add(relative_to_root)
    all_bundles = {p.relative_to(root).as_posix() for p in aa.rglob("*.bundle") if p.is_file()}
    return {
        "version": settings.get("m_AddressablesVersion"), "build_target": settings.get("m_buildTarget"),
        "catalog_path": catalog_path.relative_to(root).as_posix(),
        "settings_path": settings_path.relative_to(root).as_posix(),
        "catalog_sha256": sha256_file(catalog_path), "internal_ids": expanded,
        "resource_types": catalog.get("m_resourceTypes", []), "provider_ids": catalog.get("m_ProviderIds", []),
        "bundle_count": len(all_bundles), "catalog_bundle_count": len(local) + len(remote) + len(unmapped),
        "local_bundle_count": len(local), "missing_bundles": sorted(missing),
        "unreferenced_bundles": sorted(all_bundles - local),
        "remote_ids": sorted(remote), "unmapped_bundle_ids": sorted(unmapped),
        "complete": not (missing or remote or unmapped),
    }


def inspect_state_machines(directory: Path, root: Path, metadata: dict) -> dict:
    files = []
    all_classes = set()
    for path in sorted(directory.rglob("*.json")):
        document = _read_json(path)
        if not isinstance(document, dict) or not isinstance(document.get("FSMs"), list):
            raise ValueError("Invalid state machine document: %s" % path)
        machines = []
        classes = set()
        for machine in document["FSMs"]:
            if not isinstance(machine, dict):
                raise ValueError("Invalid state machine entry: %s" % path)
            states = machine.get("States", [])
            transitions = machine.get("Transitions", [])
            if not isinstance(states, list) or not isinstance(transitions, list):
                raise ValueError("Invalid state/transition arrays: %s" % path)
            for item in states + transitions:
                if not isinstance(item, dict) or not isinstance(item.get("Class"), str):
                    raise ValueError("State/transition is missing its Class: %s" % path)
                classes.add(item["Class"])
            machines.append({"name": machine.get("Name"), "default_state": machine.get("DefaultState"),
                             "state_count": len(states), "transition_count": len(transitions)})
        all_classes.update(classes)
        files.append({"path": path.relative_to(root).as_posix(), "sha256": sha256_file(path),
                      "machine_count": len(machines), "machines": machines,
                      "includes": document.get("Includes", []), "referenced_classes": sorted(classes)})
    available = {}
    for row in metadata["types"]:
        for name in {row["name"].split("`", 1)[0], row["full_name"].split("`", 1)[0]}:
            available.setdefault(name, []).append(row["id"])
    bindings = {name: sorted(set(available.get(name, []))) for name in sorted(all_classes)}
    return {"file_count": len(files), "machine_count": sum(r["machine_count"] for r in files),
            "files": files, "referenced_classes": sorted(all_classes), "class_bindings": bindings,
            "unresolved_classes": sorted(name for name, values in bindings.items() if not values)}


def inspect_bundle(input_path: Path) -> dict:
    """Return a deterministic, JSON-safe inventory; never write into input.

    Accept a macOS .app directory or its containing input directory. Missing
    required files and unsupported/corrupt metadata or Mach-O structures raise
    ValueError. Missing catalog bundles are inventory findings, represented by
    addressables.complete=False and the exact missing paths.
    """
    root = Path(input_path).absolute()
    if not root.is_dir():
        raise ValueError("Input directory does not exist: %s" % root)
    app = _locate_app(root)
    contents = app / "Contents"
    plist_path = contents / "Info.plist"
    try:
        with plist_path.open("rb") as stream:
            plist = plistlib.load(stream)
    except (OSError, plistlib.InvalidFileException, ValueError) as exc:
        raise ValueError("Cannot read app Info.plist: %s" % plist_path) from exc
    if not isinstance(plist, dict):
        raise ValueError("App Info.plist must be a dictionary")
    executable = plist.get("CFBundleExecutable")
    if not isinstance(executable, str) or not executable or Path(executable).name != executable:
        raise ValueError("Invalid CFBundleExecutable in app Info.plist")
    unity_info = plist.get("CFBundleGetInfoString", "")
    unity_match = re.search(r"Unity Player version ([0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+)\s*\(([0-9a-f]+)\)", unity_info)
    if not unity_match:
        raise ValueError("Cannot determine Unity version from app Info.plist")
    data = contents / "Resources" / "Data"
    metadata_path = data / "il2cpp_data" / "Metadata" / "global-metadata.dat"
    game_assembly = contents / "Frameworks" / "GameAssembly.dylib"
    launcher = contents / "MacOS" / executable
    streaming = data / "StreamingAssets"
    required = (metadata_path, game_assembly, launcher, streaming / "aa" / "catalog.json",
                streaming / "aa" / "settings.json")
    for path in required:
        if not path.is_file():
            raise ValueError("Required game bundle file is missing: %s" % path.relative_to(root))
    manifest = _manifest(root)
    metadata = parse_metadata(metadata_path)
    native = {"game_assembly": inspect_macho(game_assembly), "launcher": inspect_macho(launcher)}
    addressables = inspect_catalog(streaming / "aa" / "catalog.json", streaming / "aa" / "settings.json", root)
    state_machines = inspect_state_machines(streaming / "FSM", root, metadata)
    warnings = []
    if not addressables["complete"]:
        warnings.append("Addressables content is incomplete or has nonlocal bundle locations")
    if state_machines["unresolved_classes"]:
        warnings.append("Some state machine classes do not directly match metadata type names")
    if any(a["encrypted"] for file in native.values() for a in file["architectures"]):
        warnings.append("A native input architecture has an active encryption load command")
    return {
        "schema_version": 1, "input": {"root": str(root), "app_relative_path": app.relative_to(root).as_posix()},
        "identity": {"bundle_identifier": plist.get("CFBundleIdentifier"), "name": plist.get("CFBundleName"),
                     "version": plist.get("CFBundleShortVersionString"), "build": plist.get("CFBundleVersion"),
                     "platforms": plist.get("CFBundleSupportedPlatforms", []),
                     "unity_version": unity_match.group(1), "unity_revision": unity_match.group(2)},
        "files": manifest, "file_count": len(manifest),
        "total_bytes": sum(row.get("size", 0) for row in manifest),
        "il2cpp": metadata, "native": native, "addressables": addressables,
        "state_machines": state_machines, "source_paths": metadata["source_paths"],
        "package_evidence": metadata["package_evidence"], "warnings": warnings,
    }

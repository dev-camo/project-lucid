"""Check maintained script/folder metadata headers, not Unity import settings.

Supported minimal records contain only fileFormatVersion/guid (and folderAsset
for folders). Expanded records add the expected importer mapping and may retain
the historical timeCreated/licenseType fields. Those values, importer child
settings and other asset kinds remain outside this check's qualification.
"""

import os
from pathlib import Path
import re
import stat


_HEADER = re.compile(r"([A-Za-z_][A-Za-z0-9_]*):[ \t]*(.*)")
_GUID = re.compile(r"[0-9a-fA-F]{32}")
_HISTORICAL_FIELDS = {"timeCreated", "licenseType"}
_CACHE_DIRS = {"obj", "bin", "__pycache__"}
_PREPARED_DIRS = {"Recovered", "StreamingAssets"}
_PREPARED_METAS = {directory + ".meta" for directory in _PREPARED_DIRS}


def _header_shape(meta, folder):
    before = meta.lstat()
    if not stat.S_ISREG(before.st_mode):
        raise ValueError("metadata must be a physical regular file")
    text = meta.read_text(encoding="utf-8-sig")
    after = meta.lstat()
    if any(getattr(before, key) != getattr(after, key) for key in (
            "st_dev", "st_ino", "st_mode", "st_size", "st_mtime_ns", "st_ctime_ns")):
        raise ValueError("metadata changed while being read")
    fields = {}
    current = None
    expected = "DefaultImporter" if folder else "MonoImporter"
    for number, line in enumerate(text.splitlines(), 1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[0].isspace():
            if current != expected or fields[current] != "":
                raise ValueError("line {}: indented content under {}, outside the expected {} mapping".format(
                    number, current or "no top-level field", expected))
            # Settings are opaque here; Unity independently qualifies their schema.
            continue
        match = _HEADER.fullmatch(line)
        if not match:
            raise ValueError("line {}: malformed top-level metadata field".format(number))
        current, value = match.groups()
        if current in fields:
            raise ValueError("line {}: duplicate top-level field {}".format(number, current))
        fields[current] = value.strip()
    if fields.get("fileFormatVersion") != "2":
        raise ValueError("expected one fileFormatVersion: 2 field")
    if not _GUID.fullmatch(fields.get("guid", "")):
        raise ValueError("expected one 32-hex-digit guid field")
    base = {"fileFormatVersion", "guid"}
    if folder:
        base.add("folderAsset")
        if fields.get("folderAsset") != "yes":
            raise ValueError("folder metadata requires folderAsset: yes")
    extra = set(fields) - base
    if not extra:
        return "minimal"
    if expected not in extra or extra - {expected} - _HISTORICAL_FIELDS:
        raise ValueError("expected {} mapping or exact minimal record; unexpected top-level fields: {}".format(
            expected, ", ".join(sorted(extra - {expected} - _HISTORICAL_FIELDS)) or ", ".join(sorted(extra))))
    if fields[expected] not in ("", "{}"):
        raise ValueError("{} must be a mapping header, not a scalar".format(expected))
    return "expanded"


def validate_script_metadata(repo_root):
    """Return scoped header/pair checks for physical maintained Assets/Packages.

    Skip prepared content and hidden/build-cache directories, as maintained-source
    fingerprinting does, plus Unity's documented ignored paths. Never follow
    symlinks. This read-only check does not establish GUID uniqueness, bindings,
    or successful Unity import.
    """
    root = Path(repo_root).expanduser()
    errors = []
    counts = {"script_files": 0, "script_expanded": 0, "script_minimal": 0,
              "folder_expanded": 0, "folder_minimal": 0, "other_metadata_not_checked": 0}
    result = {"generated_by": "ProjectLucid.validate_script_metadata", "scope": "maintained-script-and-folder-headers",
              "unity_import": "NOT_RUN", "counts": counts, "errors": errors}

    def error(path, message):
        errors.append("Maintained script metadata {}: {}".format(path, message))

    if ".." in root.parts:
        error(root, "parent traversal is not a physical project path")
        return {**result, "status": "incomplete"}
    root = Path(os.path.abspath(root))
    if any(path.is_symlink() for path in (root, *root.parents)) or not root.is_dir():
        error(root, "project root must be physical with no symbolic-link ancestors")
        return {**result, "status": "incomplete"}
    files, folders, skipped = set(), set(), set()
    for scope in (root / "Assets", root / "Packages"):
        if scope.is_symlink():
            error(scope, "symbolic-link scope is not maintained source")
            continue
        if not scope.is_dir():
            error(scope, "maintained scope must be a physical directory")
            continue
        def walk_error(fault):
            error(fault.filename, str(fault))
        for parent, directories, names in os.walk(scope, followlinks=False, onerror=walk_error):
            parent = Path(parent)
            kept = []
            for name in sorted(directories):
                path = parent / name
                if path.is_symlink():
                    error(path, "symbolic-link directory is not maintained source")
                    continue
                # Unity 2022.3 Package layout documents ignored tilde folders.
                if (name.startswith(".") or name in _CACHE_DIRS or name.endswith("~")
                        or scope == root / "Assets" and name == "cvs"
                        or parent == root / "Assets" and name in _PREPARED_DIRS):
                    skipped.add(path)
                    continue
                kept.append(name)
                folders.add(path)
            directories[:] = kept
            for name in sorted(names):
                path = parent / name
                if path.is_symlink():
                    error(path, "symbolic-link file is not maintained source")
                    continue
                asset_name = name[:-5] if name.lower().endswith(".meta") else name
                # Unity 2022.3 Special folder names documents hidden Assets.
                if scope == root / "Assets" and (asset_name.startswith(".") or asset_name.endswith("~") or asset_name == "cvs"):
                    continue
                if parent == root / "Assets" and name in _PREPARED_METAS:
                    continue
                try:
                    info = path.lstat()
                    if stat.S_ISREG(info.st_mode):
                        files.add(path)
                    elif name.lower().endswith((".cs", ".cs.meta")):
                        error(path, "script or metadata must be a physical regular file")
                except OSError as fault:
                    error(path, str(fault))
    scripts = {path for path in files if path.suffix.lower() == ".cs"}
    counts["script_files"] = len(scripts)
    for script in sorted(scripts):
        meta = script.with_name(script.name + ".meta")
        if meta not in files:
            error(meta, "missing physical script metadata pair")
    for meta in sorted(path for path in files if path.suffix.lower() == ".meta"):
        asset = meta.with_name(meta.name[:-5])
        if asset in skipped:
            continue
        folder = asset in folders
        if not folder and asset.suffix.lower() != ".cs":
            counts["other_metadata_not_checked"] += 1
            continue
        if not folder and asset not in scripts:
            error(meta, "script metadata has no physical C# file pair")
            continue
        try:
            shape = _header_shape(meta, folder)
            counts[("folder_" if folder else "script_") + shape] += 1
        except (ValueError, OSError) as fault:
            error(meta, str(fault))
    return {**result, "status": "incomplete" if errors else "complete"}

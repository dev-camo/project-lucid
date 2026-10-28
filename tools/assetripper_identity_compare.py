#!/usr/bin/env python3
"""Inertness evidence for the patched AssetRipper (#552): compare exports, read-only.

  compare PATCHED_PROJECT BASELINE_PROJECT   gate (a): equal apart from the known random fields
  repeat  PROJECT_1 PROJECT_2                gate (b): export_identity.json equal apart from GUID values
  check   PATCHED_PROJECT                    gate (c): anchors, meta GUIDs and scene fileIDs agree

Each PROJECT is the export's "project" directory (ExportedProject and AuxiliaryFiles).
The GUID substitution in `compare` is path-aligned and used only here, never by the pipeline.
"""

from __future__ import annotations

from collections import Counter
import filecmp
import json
from pathlib import Path
import re
import struct
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from tools.lucidlib import exportidentity  # noqa: E402

TOKEN = re.compile(rb"[0-9a-f]{32}")
META_LINES = re.compile(rb"^(guid|timeCreated): .*$", re.MULTILINE)
TIME_LINE = re.compile(rb"^timeCreated: .*$", re.MULTILINE)
META_GUID = re.compile(rb"^guid: ([0-9a-f]{32})", re.MULTILINE)
IDENTITY = "AuxiliaryFiles/export_identity.json"


def files(root: Path) -> dict[str, Path]:
    return {path.relative_to(root).as_posix(): path for path in sorted(root.rglob("*")) if path.is_file()}


def mvid_range(data: bytes) -> tuple[int, int] | None:
    """File offsets of the module MVID of a .NET assembly (PE, CLI header, metadata root, #GUID heap and
    the Module table's Mvid index), or None if the file does not parse. Reads bytes only."""
    try:
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0":
            return None
        sections, optional_size = struct.unpack_from("<H", data, pe + 6)[0], struct.unpack_from("<H", data, pe + 20)[0]
        optional = pe + 24
        magic = struct.unpack_from("<H", data, optional)[0]
        directories = optional + (96 if magic == 0x10B else 112)
        cli_rva = struct.unpack_from("<I", data, directories + 14 * 8)[0]
        table = optional + optional_size
        # (virtual size, virtual address, raw size, raw pointer) of each section
        parts = [struct.unpack_from("<IIII", data, table + 40 * i + 8) for i in range(sections)]

        def offset(rva: int) -> int:
            for _size, virtual, raw_size, raw in parts:
                # Only the section's raw data is in the file; a virtual-only tail has no bytes to read.
                if virtual <= rva < virtual + raw_size:
                    return raw + rva - virtual
            raise ValueError(rva)

        metadata_rva = struct.unpack_from("<I", data, offset(cli_rva) + 8)[0]
        root = offset(metadata_rva)
        if data[root:root + 4] != b"BSJB":
            return None
        version = struct.unpack_from("<I", data, root + 12)[0]
        cursor = root + 16 + version + 2
        streams = {}
        count = struct.unpack_from("<H", data, cursor)[0]
        cursor += 2
        for _ in range(count):
            start, size = struct.unpack_from("<II", data, cursor)
            end = data.index(b"\0", cursor + 8)
            streams[data[cursor + 8:end].decode("ascii")] = (root + start, size)
            cursor = root + ((end - root + 4) & ~3)  # names are NUL padded to 4 bytes from the root
        tables, _ = streams.get("#~") or streams["#-"]
        heaps = data[tables + 6]
        valid = struct.unpack_from("<Q", data, tables + 8)[0]
        if not valid & 1:
            return None
        row = tables + 24 + 4 * bin(valid).count("1")
        name_width, guid_width = (4 if heaps & 1 else 2), (4 if heaps & 2 else 2)
        index = int.from_bytes(data[row + 2 + name_width:row + 2 + name_width + guid_width], "little")
        guid_heap, guid_size = streams["#GUID"]
        if index == 0 or index * 16 > guid_size:
            return None
        return guid_heap + (index - 1) * 16, guid_heap + index * 16
    except (struct.error, ValueError, KeyError, IndexError):
        return None


def same_apart_from_mvid(a: bytes, b: bytes) -> bool:
    """Equal-length assemblies that differ only inside their module MVID field (identical inputs also return True)."""
    where = mvid_range(a)
    if len(a) != len(b) or where is None or where != mvid_range(b):
        return False
    start, end = where
    return a[:start] + a[end:] == b[:start] + b[end:]


def compare(patched: Path, baseline: Path) -> dict:
    new, old = files(patched), files(baseline)
    result = Counter()
    other = []
    extra, missing = sorted(set(new) - set(old)), sorted(set(old) - set(new))
    # Path-aligned map from this export's GUIDs to the baseline's, from the .meta files.
    mapping, reverse = {}, {}
    for name in sorted(set(new) & set(old)):
        if name.endswith(".meta"):
            a, b = META_GUID.search(new[name].read_bytes()), META_GUID.search(old[name].read_bytes())
            if a is None or b is None:
                other.append(f"{name}: no guid line")
                continue
            if name.endswith(".cs.meta"):
                continue  # script GUIDs are deterministic: never normalized, compared exactly below
            if mapping.setdefault(a[1], b[1]) != b[1] or reverse.setdefault(b[1], a[1]) != a[1]:
                other.append(f"{name}: guid map is not one to one")
    substitute = lambda data: TOKEN.sub(lambda m: mapping.get(m[0], m[0]), data)
    result["guids_total"] = len(mapping)
    result["guids_same"] = sum(1 for a, b in mapping.items() if a == b)
    result["guids_fresh"] = result["guids_total"] - result["guids_same"]
    for name in sorted(set(new) & set(old)):
        a, b = new[name], old[name]
        if a.stat().st_size == b.stat().st_size and filecmp.cmp(a, b, shallow=False):
            result["byte_identical"] += 1
            continue
        da, db = a.read_bytes(), b.read_bytes()
        if name.endswith(".meta"):
            ignored = TIME_LINE if name.endswith(".cs.meta") else META_LINES
            if ignored.sub(b"", da) == ignored.sub(b"", db):
                result["meta_differ_only_in_guid_or_timeCreated"] += 1
                continue
        elif substitute(da) == db:
            result["identical_after_guid_substitution"] += 1
            continue
        elif name.endswith("/Main.mixer") and TOKEN.sub(b"G", da) == TOKEN.sub(b"G", db):
            # One-to-one over every pair, unchanged ones included, so a fresh GUID cannot merge into a kept one.
            pairs = set(zip(TOKEN.findall(da), TOKEN.findall(db)))
            if len({x for x, _ in pairs}) == len({y for _, y in pairs}) == len(pairs):
                result["mixer_files"] += 1
                result["mixer_guid_pairs"] += sum(1 for x, y in pairs if x != y)
                continue
        elif name.startswith("AuxiliaryFiles/GameAssemblies/") and name.endswith(".dll") and same_apart_from_mvid(da, db):
            result["gameassembly_mvid_only"] += 1
            continue
        other.append(name)
    result["files_patched"], result["files_baseline"] = len(new), len(old)
    return {"counts": dict(sorted(result.items())), "extra_in_patched": extra, "missing_in_patched": missing,
            "other_differences": other[:50], "other_difference_count": len(other),
            "ok": extra == [IDENTITY] and not missing and not other}


def repeat(first: Path, second: Path) -> dict:
    def stripped(path: Path):
        document = json.loads((path / IDENTITY).read_text(encoding="utf-8"))
        guids = [entry["guid"] for entry in document["collections"]]
        for entry in document["collections"]:
            entry["guid"] = None if entry["guid"] is None else "<guid>"
        return document, guids

    a, guids_a = stripped(first)
    b, guids_b = stripped(second)
    return {"collections": len(a["collections"]), "members": sum(len(c["members"]) for c in a["collections"]),
            "identical_apart_from_guids": a == b, "guids_equal_between_runs": sum(x == y for x, y in zip(guids_a, guids_b)),
            "ok": a == b}


def main(argv: list[str]) -> int:
    if len(argv) < 3 or argv[1] not in ("compare", "repeat", "check"):
        print(__doc__)
        return 2
    paths = [Path(item) for item in argv[2:]]
    if argv[1] == "compare":
        result = compare(*paths)
    elif argv[1] == "repeat":
        result = repeat(*paths)
    else:
        result = exportidentity.check_identity(paths[0] / "ExportedProject",
                                               exportidentity.load_identity(paths[0] / IDENTITY))
    print(json.dumps(result, indent=1))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))

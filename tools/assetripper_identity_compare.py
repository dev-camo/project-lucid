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
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from tools.lucidlib import exportidentity  # noqa: E402

TOKEN = re.compile(rb"[0-9a-f]{32}")
META_LINES = re.compile(rb"^(guid|timeCreated): .*$", re.MULTILINE)
META_GUID = re.compile(rb"^guid: ([0-9a-f]{32})", re.MULTILINE)
IDENTITY = "AuxiliaryFiles/export_identity.json"


def files(root: Path) -> dict[str, Path]:
    return {path.relative_to(root).as_posix(): path for path in sorted(root.rglob("*")) if path.is_file()}


def window(a: bytes, b: bytes) -> int | None:
    """Span of the differing byte range of two equal-length blobs (a PE MVID is 16 bytes)."""
    if len(a) != len(b):
        return None
    changed = [i for i in range(len(a)) if a[i] != b[i]]
    return changed[-1] - changed[0] + 1 if changed else 0


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
            if META_LINES.sub(b"", da) == META_LINES.sub(b"", db):
                result["meta_differ_only_in_guid_or_timeCreated"] += 1
                continue
        elif substitute(da) == db:
            result["identical_after_guid_substitution"] += 1
            continue
        elif name.endswith("/Main.mixer") and TOKEN.sub(b"G", da) == TOKEN.sub(b"G", db):
            pairs = set(zip(TOKEN.findall(da), TOKEN.findall(db)))
            pairs = {pair for pair in pairs if pair[0] != pair[1]}
            if len({x for x, _ in pairs}) == len({y for _, y in pairs}) == len(pairs):
                result["mixer_files"] += 1
                result["mixer_guid_pairs"] += len(pairs)
                continue
        elif name.startswith("AuxiliaryFiles/GameAssemblies/") and name.endswith(".dll") and window(da, db) is not None \
                and 0 < window(da, db) <= 16:
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

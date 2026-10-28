#!/usr/bin/env python3
"""Static check of the identity record against the input, without AssetRipper (#552). Read-only.

  scenes --app APP --identity export_identity.json
  all    --app APP --identity export_identity.json     the same for every collection

For every SceneExportCollection, each member that came from a serialized file must be in that file's own
object table (PathID present, same ClassID). The serialized files are read from the input: bundle nodes
below StreamingAssets/aa (tools/lucidlib/unityfs.py) and the plain files below Contents/Resources/Data.
"""

from __future__ import annotations

from collections import Counter
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from tools.lucidlib import exportidentity, unityfs  # noqa: E402


def index_bundles(data: Path) -> dict[str, tuple[Path, str]]:
    """Lower-case node name -> (bundle, node name) for every serialized-file node of every bundle."""
    found = {}
    for bundle in sorted((data / "StreamingAssets").rglob("*.bundle")):
        with unityfs.Bundle(bundle) as opened:
            for name in opened.nodes:
                if not name.endswith((".resS", ".resource")):
                    if name.lower() in found:
                        raise unityfs.UnityFsError(f"node {name} is in two bundles")
                    found[name.lower()] = (bundle, name)
    return found


def object_table_of(name: str, data: Path, index: dict) -> dict[int, int]:
    if name.lower() in index:
        bundle, node = index[name.lower()]
        with unityfs.Bundle(bundle) as opened:
            offset, _ = opened.nodes[node]
            return unityfs.object_table(lambda start, length: opened.read(offset + start, length))
    plain = data / name
    if plain.is_file():
        with plain.open("rb") as stream:
            def read(start, length):
                stream.seek(start)
                return stream.read(length)
            return unityfs.object_table(read)
    raise unityfs.UnityFsError(f"serialized file {name} is neither a bundle node nor a file of the input")


def check_scenes(app: Path, collections: list[dict]) -> dict:
    """Scene collections only."""
    return check_members(app, collections, exportidentity.SCENE_TYPES)


def check_members(app: Path, collections: list[dict], types=None) -> dict:
    """Every serialized member of the collections of `types` (all collections when None)."""
    data = Path(app) / "Contents" / "Resources" / "Data"
    index = index_bundles(data)
    counts, failures, tables, where = Counter(), [], {}, Counter()
    for entry in collections:
        if types is not None and entry["type"] not in types:
            continue
        counts["collections"] += 1
        files = {member[0] for member in entry["members"] if member[4]}
        for name in sorted(files):
            if name not in tables:
                tables[name] = object_table_of(name, data, index)
                where["bundle_node" if name.lower() in index else "plain_file"] += 1
        for name, path_id, class_id, _, serialized, _ in entry["members"]:
            if not serialized:
                continue
            counts["members_checked"] += 1
            found = tables[name].get(path_id)
            if found is None:
                failures.append(f"{entry['paths']} {name} PathID {path_id} is not in the object table")
            elif found != class_id:
                failures.append(f"{entry['paths']} {name} PathID {path_id} has ClassID {found}, record says {class_id}")
    counts["serialized_files"] = len(tables)
    return {"counts": dict(sorted(counts.items())), "files_read_from": dict(where), "failures": failures[:20],
            "failure_count": len(failures), "ok": not failures and counts["collections"] > 0}


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["scenes", "all"])
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--identity", type=Path, required=True)
    arguments = parser.parse_args()
    check = check_scenes if arguments.command == "scenes" else check_members
    outcome = check(arguments.app, exportidentity.load_identity(arguments.identity))
    print(json.dumps(outcome, indent=1))
    sys.exit(0 if outcome["ok"] else 1)

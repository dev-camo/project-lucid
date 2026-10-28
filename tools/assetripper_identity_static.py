#!/usr/bin/env python3
"""Static check of the identity record against the input, without AssetRipper (#552). Read-only.

  scenes --app APP --identity export_identity.json
  all    --app APP --identity export_identity.json     the same for every collection
  table  --app APP --table identity-table.txt          every anchor of the committed table is in its file's
                                                       object table (scenes counted separately)
  container --app APP --table identity-table.txt --asset-map asset-map.json
                                                       class-142 m_Container keys against PathIDs, the table
                                                       and the catalog's exact_path identities

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
from tools.lucidlib import exportidentity, identitymap, unityfs  # noqa: E402


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
    # A name from a record or a table must stay below the Data directory.
    plain = data / name
    if name and not Path(name).is_absolute() and ".." not in Path(name).parts and plain.is_file():
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


def check_table(app: Path, table: dict) -> dict:
    """Every anchor (file, PathID, ClassID) of the table's C rows is in that file's object table.

    A scene's LightingData row is anchored on the scene's own object with ClassID 1120 (the generated asset has
    no original object), so only its PathID is checked, against the scene file."""
    data = Path(app) / "Contents" / "Resources" / "Data"
    index = index_bundles(data)
    counts, failures, tables, where = Counter(), [], {}, Counter()
    for (name, path_id, class_id), row in sorted(table["collections"].items()):
        if name not in tables:
            tables[name] = object_table_of(name, data, index)
            where["bundle_node" if name.lower() in index else "plain_file"] += 1
        found = tables[name].get(path_id)
        scene = any(path.endswith(".unity") for path in row["paths"])
        counts["rows"] += 1
        counts["scene_rows"] += scene
        if class_id == identitymap.LIGHTING_DATA_ASSET:
            counts["lighting_rows"] += 1
            ok = found is not None
        else:
            ok = found == class_id
        if not ok:
            failures.append(f"{row['paths'][0]} {name} PathID {path_id} ClassID {class_id}: object table has {found}")
        elif scene:
            counts["scene_rows_found"] += 1
            counts["scene_rows_found_in_bundle_nodes" if name.lower() in index else "scene_rows_found_in_plain_files"] += 1
    counts["serialized_files"] = len(tables)
    return {"counts": dict(sorted(counts.items())), "files_read_from": dict(where), "failures": failures[:20],
            "failure_count": len(failures), "ok": not failures and counts["rows"] > 0}


def bundle_containers(data: Path) -> tuple[dict, Counter, list]:
    """Every class-142 m_Container of every bundle: ({lower-case key: [(node, PathID, ClassID)]}, counts, failures).

    Each entry's PathID (a PPtr with fileID 0) must be in the object table of the node that holds the AssetBundle."""
    by_key, counts, failures = {}, Counter(), []
    for bundle in sorted((data / "StreamingAssets").rglob("*.bundle")):
        with unityfs.Bundle(bundle) as opened:
            counts["bundles"] += 1
            for node, (offset, _) in opened.nodes.items():
                if node.endswith((".resS", ".resource")):
                    continue
                table = unityfs.objects(lambda start, length: opened.read(offset + start, length))
                for path_id, (class_id, start, size) in table.items():
                    if class_id != unityfs.ASSET_BUNDLE:
                        continue
                    counts["asset_bundle_objects"] += 1
                    for key, file_id, target in unityfs.asset_bundle_container(opened.read(offset + start, size)):
                        counts["container_entries"] += 1
                        if file_id != 0:
                            counts["container_entries_in_other_files"] += 1
                        elif target == 0:  # a null PPtr: a scene's entry (its objects are in the .sharedAssets file) or an empty one
                            counts["container_entries_null_in_scene_bundles" if node.endswith(".sharedAssets")
                                   else "container_entries_null_elsewhere"] += 1
                        elif target not in table:
                            failures.append(f"{node} container key {key!r}: PathID {target} is not in the object table")
                        else:
                            counts["container_entries_found"] += 1
                            by_key.setdefault(key.lower(), []).append((node, target, table[target][0]))
    return by_key, counts, failures


# The catalog exact_path identities whose original object the baseline names differently (see check_container).
# The baseline project names an object by its m_Name and adds a collision suffix to the later one of two with the
# same path: the unsuffixed baseline file belongs to another object than the one the original address names.
# 1: the FBX-derived prefab of the same name took scan_beam.prefab, the authored prefab is scan_beam_0.prefab.
# 2: the Cream_3D timeline in the Tails_3D bundle (m_Name of the Tails_3D timeline) took the unsuffixed name.
BASELINE_NAMING_EXCEPTIONS = {
    "Assets/Data/Art/Characters/Tails/miles_electric/cutscene_miles_electric_scan_beam.prefab",
    "Assets/Data/Art/Cutscenes/Cutscenes/Zone2_Boss/CS_Zone2_Boss_E_Cream_3D.playable"}


def check_container(app: Path, table: dict, asset_map: dict) -> dict:
    """The original container paths against the table and the catalog's `exact_path` identities.

    internal_id is the original address of an asset; the candidate is the baseline file the asset map gives it.
    Per identity: (1) some bundle's container has that key; (2) when a container object for the key is an anchor
    of the table, the table's paths for it contain the candidate ("confirmed"). When the key's objects include a
    class-114 object that no row anchors (the timeline of a ScriptableObjectGroup, whose anchor is the group's
    lowest PathID), the candidate's own row must be anchored in a CAB that has the key ("group_in_same_cab").
    Anything else is a disagreement; BASELINE_NAMING_EXCEPTIONS lists the ones that are baseline facts."""
    data = Path(app) / "Contents" / "Resources" / "Data"
    by_key, counts, failures = bundle_containers(data)
    rows = {(name.lower(), path_id, class_id): row for (name, path_id, class_id), row in table["collections"].items()}
    by_path = {path: key for key, row in table["collections"].items() for path in row["paths"]}
    exact = [item for item in asset_map["catalog_identities"] if item["match"] == "exact_path"]
    outcome, problems, excepted = Counter(), [], []
    for item in exact:
        counts["exact_path_identities"] += 1
        candidate = item["candidates"][0]["path"] if len(item["candidates"]) == 1 and not item["ambiguous"] else None
        found = by_key.get(item["internal_id"].lower())
        if candidate is None:
            outcome["not_one_candidate"] += 1
            problems.append(f"{item['internal_id']}: {len(item['candidates'])} candidates")
            continue
        if not found:
            outcome["no_container_key"] += 1
            problems.append(f"{item['internal_id']}: no bundle container has this key")
            continue
        outcome["container_key_found"] += 1
        anchored = [rows[(node.lower(), path_id, class_id)] for node, path_id, class_id in found
                    if (node.lower(), path_id, class_id) in rows]
        loose = any(class_id == 114 and (node.lower(), path_id, class_id) not in rows for node, path_id, class_id in found)
        own = by_path.get(candidate)
        if any(candidate in row["paths"] for row in anchored):
            outcome["confirmed"] += 1
        elif loose and own is not None and own[0].lower() in {node.lower() for node, _, _ in found}:
            outcome["group_in_same_cab"] += 1
        elif item["internal_id"] in BASELINE_NAMING_EXCEPTIONS:
            outcome["baseline_naming_exception"] += 1
            excepted.append(f"{item['internal_id']}: its object is the table row {[row['paths'][:2] for row in anchored]}")
        else:
            outcome["disagrees"] += 1
            problems.append(f"{item['internal_id']}: container objects {[(n, p, c) for n, p, c in found][:3]} "
                            f"are rows {[row['paths'][:2] for row in anchored][:2]}, candidate {candidate}")
    counts.update(outcome)
    bad = len(failures) + len(problems)
    return {"counts": dict(sorted(counts.items())), "failures": (failures + problems)[:20], "failure_count": bad,
            "baseline_naming_exceptions": excepted,
            "ok": not bad and counts["exact_path_identities"] > 0
            and len(excepted) == outcome["baseline_naming_exception"]}


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["scenes", "all", "table", "container"])
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--identity", type=Path)
    parser.add_argument("--table", type=Path)
    parser.add_argument("--asset-map", type=Path)
    arguments = parser.parse_args()
    if arguments.command in ("table", "container"):
        table = identitymap.parse_table(arguments.table.read_bytes().decode("utf-8"))
        if arguments.command == "table":
            outcome = check_table(arguments.app, table)
        else:
            outcome = check_container(arguments.app, table, json.loads(arguments.asset_map.read_text(encoding="utf-8")))
    else:
        check = check_scenes if arguments.command == "scenes" else check_members
        outcome = check(arguments.app, exportidentity.load_identity(arguments.identity))
    print(json.dumps(outcome, indent=1))
    sys.exit(0 if outcome["ok"] else 1)

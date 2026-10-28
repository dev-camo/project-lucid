"""Check AssetRipper's export_identity.json (written by tools/assetripper/lucid-identity.patch) against an export.

The patched AssetRipper records, for every collection that writes files, its export GUID,
the asset paths and each member's original serialized file, PathID, ClassID and local
fileID. This module is pure stdlib and only reads: it proves the record agrees with the
files actually written, so the record can serve as identity evidence.
"""

from __future__ import annotations

from collections import Counter
import json
from pathlib import Path
import re

ANCHOR = re.compile(rb"--- !u!(\d+) &(-?\d+)(?: stripped)?\r?$")
META_GUID = re.compile(rb"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)
SCENE_TYPES = ("SceneExportCollection",)
YAML_SUFFIXES = (".unity", ".prefab")  # always YAML documents: an unreadable one is a failure, not a binary export


class IdentityError(ValueError):
    """The identity file is malformed."""


def load_identity(path: Path) -> list[dict]:
    """Parse and structurally validate the identity file; returns its collections."""
    document = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(document, dict) or document.get("format") != 1 or not isinstance(document.get("collections"), list):
        raise IdentityError("Unsupported export identity format")
    orders = set()
    for entry in document["collections"]:
        if not isinstance(entry, dict) or not isinstance(entry.get("order"), int) or entry["order"] in orders \
                or not isinstance(entry.get("type"), str) or not isinstance(entry.get("paths"), list) \
                or not all(isinstance(item, str) for item in entry["paths"]) or not isinstance(entry.get("members"), list) \
                or not (entry.get("guid") is None or re.fullmatch(r"[0-9a-f]{32}", str(entry["guid"]))):
            raise IdentityError(f"Malformed identity collection: {str(entry)[:120]}")
        orders.add(entry["order"])
        for member in entry["members"]:
            if not (isinstance(member, list) and len(member) == 6 and isinstance(member[0], str)
                    and all(isinstance(item, int) and not isinstance(item, bool) for item in member[1:3])
                    and (member[3] is None or (isinstance(member[3], int) and not isinstance(member[3], bool)))
                    and isinstance(member[4], bool) and isinstance(member[5], bool)):
                raise IdentityError(f"Malformed identity member: {str(member)[:120]}")
    return document["collections"]


def yaml_anchors(path: Path) -> list[tuple[int, int]] | None:
    """The (classID, fileID) of each "--- !u!<class> &<id>" document, or None for a non-YAML file."""
    anchors = []
    with Path(path).open("rb") as stream:
        if not stream.read(5) == b"%YAML":
            return None
        stream.seek(0)
        for line in stream:
            if line.startswith(b"--- !u!"):
                match = ANCHOR.fullmatch(line.rstrip(b"\n"))
                if match is None:
                    raise IdentityError(f"Unreadable YAML document header in {path}")
                anchors.append((int(match[1]), int(match[2])))
    return anchors


def check_identity(project: Path, collections: list[dict], limit: int = 20) -> dict:
    """Compare the identity record with the files below ExportedProject.

    Checks per collection path: the file and its .meta exist, the .meta guid is the
    recorded GUID, YAML anchors equal the members' (ClassID, fileID), and scene members
    that came from a serialized file have fileID == PathID. Every .meta below Assets must
    be claimed by exactly one collection. Returns counts plus up to `limit` failures per kind."""
    project = Path(project)
    counts, failures = Counter(), {}

    def fail(kind: str, detail: str) -> None:
        counts[kind] += 1
        failures.setdefault(kind, [])
        if len(failures[kind]) < limit:
            failures[kind].append(detail)

    claimed = Counter()
    for entry in collections:
        counts["collections"] += 1
        counts["members"] += len(entry["members"])
        expected = Counter((member[2], member[3]) for member in entry["members"] if member[5])
        if entry["type"] in SCENE_TYPES:
            for file, path_id, class_id, file_id, serialized, written in entry["members"]:
                if serialized:
                    counts["scene_serialized_members"] += 1
                    if file_id != path_id:
                        fail("scene_fileid_differs_from_pathid", f"{entry['paths']} {file} {path_id} -> {file_id}")
        for relative in entry["paths"]:
            counts["paths"] += 1
            claimed[relative] += 1
            asset, meta = project / relative, project / (relative + ".meta")
            if not asset.is_file():
                fail("missing_file", relative)
                continue
            if not meta.is_file():
                fail("missing_meta", relative)
            else:
                found = META_GUID.search(meta.read_bytes())
                if entry["guid"] is None:
                    # Script collections write one deterministic GUID per script, not one per collection.
                    counts["metas_with_unrecorded_guid"] += 1
                elif found is None or found[1].decode() != entry["guid"]:
                    fail("meta_guid_differs", relative)
            anchors = yaml_anchors(asset)
            if anchors is None and (entry["type"] in SCENE_TYPES or relative.endswith(YAML_SUFFIXES)):
                fail("yaml_unreadable", relative)
                continue
            if anchors is None:
                counts["non_yaml_files"] += 1
                continue
            counts["yaml_files"] += 1
            counts["yaml_anchors"] += len(anchors)
            if Counter(anchors) != expected:
                fail("anchors_differ_from_members", f"{relative} anchors={len(anchors)} members={sum(expected.values())}")
    for meta in sorted((project / "Assets").rglob("*.meta")):
        relative = meta.relative_to(project).as_posix()[:-5]
        if claimed.get(relative) != 1:
            fail("meta_not_claimed_exactly_once", f"{relative} claimed {claimed.get(relative, 0)} times")
        counts["metas"] += 1
    for relative, times in claimed.items():
        if times > 1:
            fail("path_claimed_twice", relative)
    return {"counts": dict(sorted(counts.items())), "failures": failures, "ok": not failures}

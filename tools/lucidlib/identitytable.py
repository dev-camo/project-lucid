"""Build the committed identity table from a patched witness export and the baseline export (issue #552).

Used when building the table; pure stdlib. The table (format in tools/lucidlib/identitymap.py) maps each
exported collection, keyed by the original object it is anchored on (serialized file, PathID,
ClassID), to the baseline GUID, paths, timeCreated and file hashes. The witness export W says which
original object each of its files came from; the baseline export (the immutable reference run) says
what the GUIDs and bytes must be. `build_table` joins them by path, which is sound only because the
caller has first proved the two exports are byte-equal after the path-aligned GUID substitution
(tools/assetripper_identity_compare.py `compare`): the path is used to align, never as identity of
the table. Anything unexplained raises IdentityMapError; nothing is guessed.

Collections whose baseline files are content-identical and cannot be told apart by any reference
graph are listed as W rows ("order-witnessed"): for them the alignment rests on AssetRipper's
export order, so they are reported separately and never count as provenance evidence.
"""

from __future__ import annotations

from collections import Counter
import hashlib
from pathlib import Path

from . import exportidentity, identitymap
from .identitymap import IdentityMapError

TOKEN_MASK = b"<G>"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _meta_fields(path: Path) -> tuple[str, int]:
    data = Path(path).read_bytes()
    guids, times = identitymap.META_GUID.findall(data), identitymap.TIME_CREATED.findall(data)
    if len(guids) != 1 or len(times) != 1:
        raise IdentityMapError("baseline_meta", f"{path}: {len(guids)} guid and {len(times)} timeCreated lines")
    return guids[0].decode(), int(times[0].split(b": ")[1])


def _files(root: Path) -> dict[str, Path]:
    return {relative: path for path, relative in identitymap._files(Path(root))}


def build_table(witness: Path, collections: list[dict], baseline: Path, header: dict) -> tuple[dict, dict]:
    """The table for `header`, plus a report of what it contains.

    `witness` and `baseline` are the two ExportedProject directories; `collections` is the witness's
    export_identity.json. Refuses an unknown collection type, an anchor that is not unique, a
    collection whose files carry different baseline GUIDs, and a baseline GUID that two rows claim."""
    witness, baseline = Path(witness), Path(baseline)
    validation = identitymap.validate_identity(witness, collections)
    table = {"header": dict(header), "collections": {}, "scripts": {}, "mixer": {}, "witnessed": [],
             "unresolved": set(), "files": {}}
    types, owned, report = Counter(), set(), {"validation": validation}
    scenes = identitymap.scene_anchors(collections)
    for entry in collections:
        types[entry["type"]] += 1
        member = identitymap.anchor(witness, entry, scenes)  # refuses a type without a rule
        kind = identitymap.ANCHOR_RULES[entry["type"]][0]
        if kind in ("none", "unowned"):
            if entry["paths"] or (kind == "none" and entry["guid"] is not None):
                raise IdentityMapError("identity_unexpected_files", f"{entry['type']} {entry['order']} owns files")
            continue
        if not entry["paths"]:
            raise IdentityMapError("identity_missing", f"{entry['type']} collection {entry['order']} has no file")
        rows = []
        for relative in sorted(entry["paths"]):
            asset = baseline / relative
            if not asset.is_file() or not (baseline / (relative + ".meta")).is_file():
                raise IdentityMapError("baseline_file_missing", relative)
            guid, time = _meta_fields(baseline / (relative + ".meta"))
            rows.append((relative, guid, time, sha256_file(asset), sha256_file(baseline / (relative + ".meta"))))
            owned |= {relative, relative + ".meta"}
        if kind == "script":
            for relative, guid, time, sha, meta in rows:
                if relative in table["scripts"]:
                    raise IdentityMapError("identity_duplicate", f"script {relative} recorded twice")
                table["scripts"][relative] = {"guid": guid, "time_created": time, "sha256": sha, "meta_sha256": meta}
            continue
        if len({row[1] for row in rows}) != 1:
            raise IdentityMapError("baseline_guid_split", f"{rows[0][0]}: files of one collection carry "
                                   f"{len({row[1] for row in rows})} baseline GUIDs")
        key = (member[0], member[1], member[2])
        if key in table["collections"]:
            raise IdentityMapError("identity_duplicate", f"anchor {key} is claimed twice ({rows[0][0]})")
        table["collections"][key] = {
            "guid": rows[0][1], "digest": identitymap.member_digest(entry["members"]),
            "paths": [row[0] for row in rows], "time_created": [row[2] for row in rows],
            "sha256": [row[3] for row in rows], "meta_sha256": [row[4] for row in rows]}
    for relative, path in _files(baseline).items():
        if relative not in owned:
            table["files"][relative] = sha256_file(path)
    report["collection_types"] = dict(sorted(types.items()))
    report["anchor_rules"] = {name: list(identitymap.ANCHOR_RULES[name]) for name in sorted(types)}
    report["mixer"] = _mixer_rows(witness, baseline, table)
    table["unresolved"] = unresolved_references(baseline, table)
    classes = indistinguishable_classes(baseline, table, collections)
    table["witnessed"] = classes["classes"]
    report["witnessed"] = classes["report"]
    return table, report


def _mixer_rows(witness: Path, baseline: Path, table: dict) -> dict:
    """M rows: the baseline token at every bare-token position of each .mixer file; and how the witness's
    tokens map to them (the value mapping must be a function and injective, or identitymap refuses)."""
    report = {}
    owned = {path for row in table["collections"].values() for path in row["paths"]}
    for relative in sorted(path for path in owned if path.endswith(".mixer")):
        base = [text for _, _, kind, text in identitymap.tokens((baseline / relative).read_bytes(), relative)
                if kind == "other"]
        fresh = [text for _, _, kind, text in identitymap.tokens((witness / relative).read_bytes(), relative)
                 if kind == "other"]
        identitymap._mixer_edits(relative, (witness / relative).read_bytes(), base)  # consistent and injective
        table["mixer"][relative] = base
        mapping = dict(zip(fresh, base))
        report[relative] = {"positions": len(base), "distinct_baseline_values": len(set(base)),
                            "distinct_witness_values": len(set(fresh)),
                            "positions_equal_to_baseline": sum(a == b for a, b in zip(fresh, base)),
                            "values_equal_to_baseline": sum(a == b for a, b in mapping.items()),
                            "consistent_and_injective": True}
    return report


def unresolved_references(baseline: Path, table: dict) -> set[str]:
    """Every baseline reference GUID (reference or build-settings scene position) that no row owns."""
    known = identitymap.baseline_guids(table)
    found = set()
    for path, relative in identitymap._files(baseline):
        data = path.read_bytes()
        if b"\0" in data:
            continue
        found |= {text for _, _, kind, text in identitymap.tokens(data, relative) if kind in ("reference", "scene")}
    return found - known


# ---------------------------------------------------------------- order-witnessed classes

def _content_label(baseline: Path, row: dict, kind: str) -> tuple[bytes, list[str]]:
    """A digest of a collection's files with every GUID position masked (and the meta's guid and
    timeCreated lines removed), plus the GUIDs it references in file order."""
    digest, outs = hashlib.sha256(), []
    digest.update(kind.encode() + b"\0")
    for relative in row["paths"]:
        data = (baseline / relative).read_bytes()
        if b"\0" in data:
            digest.update(hashlib.sha256(data).digest())
        else:
            found = identitymap.tokens(data, relative)
            outs += [text for _, _, label, text in found if label == "reference"]
            masked, last = [], 0
            for start, end, label, _ in found:
                if label in ("reference", "other"):
                    masked += [data[last:start], TOKEN_MASK]
                    last = end
            masked.append(data[last:])
            digest.update(b"".join(masked))
        meta = (baseline / (relative + ".meta")).read_bytes()
        meta = identitymap.TIME_CREATED.sub(b"", identitymap.META_GUID.sub(b"", meta))
        digest.update(b"\1" + meta)
        digest.update(b"\2")
    return digest.digest(), outs


def indistinguishable_classes(baseline: Path, table: dict, collections: list[dict]) -> dict:
    """Colour refinement (no paths, no names, no PathIDs) over the baseline's collections: the start colour
    is the masked content, each round adds the colours of the collections a collection references (in
    file order) and of those referencing it. Collections that stay in one class cannot be told apart by
    content or by reference graph; they are the order-witnessed ones. Returns {"classes": sorted GUID
    lists of size >= 2, "report": counts}."""
    kinds = {}
    for entry in collections:
        kinds[tuple(sorted(entry["paths"]))] = entry["type"]
    order = list(table["collections"].values())
    index = {row["guid"]: number for number, row in enumerate(order)}
    labels, outs = [], []
    for row in order:
        label, out = _content_label(baseline, row, kinds[tuple(row["paths"])])
        labels.append(label)
        outs.append(out)
    inward = [[] for _ in order]
    for number, out in enumerate(outs):
        for target in out:
            if target in index:
                inward[index[target]].append(number)
    colours = _intern(labels)
    rounds = 0
    while True:
        rounds += 1
        signature = [(colours[number],
                      tuple(colours[index[t]] if t in index else t for t in outs[number]),
                      tuple(sorted(colours[source] for source in inward[number])))
                     for number in range(len(order))]
        refined = _intern(signature)
        if len(set(refined)) == len(set(colours)):
            break
        colours = refined
    groups = {}
    for number, colour in enumerate(colours):
        groups.setdefault(colour, []).append(order[number]["guid"])
    classes = sorted(sorted(members) for members in groups.values() if len(members) > 1)
    sizes = Counter(len(members) for members in classes)
    return {"classes": classes, "report": {"collections": len(order), "rounds": rounds, "classes": len(classes),
                                            "members": sum(len(members) for members in classes),
                                            "class_sizes": dict(sorted(sizes.items()))}}


def _intern(values: list) -> list[int]:
    seen = {}
    return [seen.setdefault(value, len(seen)) for value in values]


# ---------------------------------------------------------------- end gate against the baseline files

def compare_stage_with_baseline(stage: Path, baseline: Path) -> dict:
    """Independent of the table: every file of the relabelled stage has the same sha256 as the file of the
    same path in the baseline project, and the sets of paths are equal."""
    new, old = _files(stage), _files(baseline)
    if set(new) != set(old):
        extra, missing = sorted(set(new) - set(old)), sorted(set(old) - set(new))
        raise IdentityMapError("stage_file_set_differs", f"extra {extra[:3]}, missing {missing[:3]}")
    different = [name for name in sorted(new) if sha256_file(new[name]) != sha256_file(old[name])]
    if different:
        raise IdentityMapError("stage_differs_from_baseline", f"{len(different)} file(s), first {different[0]}")
    return {"files": len(new)}

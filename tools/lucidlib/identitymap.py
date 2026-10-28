"""Turn a fresh AssetRipper export into the baseline project, using an identity table (issue #552).

AssetRipper gives every non-script collection a random GUID on each export. The patched
build records each collection's original objects in export_identity.json
(tools/lucidlib/exportidentity.py). A committed identity table maps each collection, keyed
by the original object it is anchored on (serialized file, PathID, ClassID), to the baseline
GUID. This module is pure stdlib. Its steps are separate functions and each refuses anything
doubtful with IdentityMapError:

1. validate_identity   the record agrees with the exported files
2. lookup              every fresh collection maps to exactly one table row, one to one and onto
3. check_collisions    no fresh GUID can be confused with another identifier
4. relabel             rewrite only reference positions in a staged copy
5. verify_output       every output file equals the baseline hash; no fresh GUID is left

Table format (UTF-8, LF, one object per line, tab separated, lines sorted as strings, no duplicates):

    lucid-identity-table<TAB>1<TAB>input=<sha256><TAB>assetripper=<40 hex><TAB>patch=<sha256>
    C<TAB>file<TAB>pathid<TAB>classid<TAB>guid<TAB>member_digest<TAB>paths<TAB>timeCreated<TAB>sha256<TAB>metasha256
    F<TAB>path<TAB>sha256
    M<TAB>path<TAB>ordinal(6 digits)<TAB>guid
    S<TAB>path<TAB>guid<TAB>timeCreated<TAB>sha256<TAB>metasha256
    U<TAB>guid
    W<TAB>guid,guid[,guid...]

C is one exported collection; paths, timeCreated, sha256 and metasha256 hold one `|` separated
item per path, in sorted path order. S is a script (verified, never remapped). F is a file no
collection owns (ProjectSettings and the like). M is the baseline value for the nth bare GUID
of an AudioMixer file. U is a baseline reference GUID that resolves to nothing. W lists
content-identical collections whose GUIDs were attributed by witnessed export order; they are
accepted, reported separately and never count as provenance.
"""

from __future__ import annotations

from collections import Counter
import hashlib
from pathlib import Path
import re
import shutil

from . import exportidentity

SCHEMA = 1
HEADER = "lucid-identity-table"
GUID = re.compile(r"[0-9a-f]{32}")
SHA = re.compile(r"[0-9a-f]{64}")
TOKEN = re.compile(rb"(?<![0-9A-Za-z])[0-9a-fA-F]{32}(?![0-9A-Za-z])")
META_GUID = re.compile(rb"^guid: ([0-9a-f]{32})[ \t]*\r?$", re.MULTILINE)
REFERENCE = re.compile(rb"\{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}")
ASSET_GUID = re.compile(rb"m_AssetGUID: ['\"]?([0-9a-f]{32})")
TIME_CREATED = re.compile(rb"^timeCreated: -?\d+(?=[ \t]*\r?$)", re.MULTILINE)
DOCUMENT = re.compile(rb"^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?$", re.MULTILINE)
GAME_OBJECT, TRANSFORM, RECT_TRANSFORM = 1, 4, 224

# Anchor rule per collection type. ("single",): the only written serialized member.
# ("class", n): the only written serialized member of ClassID n. ("prefab_root",): the root
# GameObject. ("first",): the written serialized member with the lowest PathID (a scene is one
# serialized file). ("script",) is verified by path. ("none",) owns no files and no GUID.
# A type not listed here is refused: its rule must be chosen on evidence.
ANCHOR_RULES = {
    "AssetExportCollection": ("single",),
    "MaterialExportCollection": ("single",),
    "ShaderExportCollection": ("single",),
    "TextAssetExportCollection": ("single",),
    "FontAssetExportCollection": ("single",),
    "AudioExportCollection": ("single",),
    "VideoClipExportCollection": ("single",),
    "TextureExportCollection": ("class", 28),
    "AudioMixerExportCollection": ("class", 241),
    "AnimatorControllerExportCollection": ("class", 91),
    "PrefabExportCollection": ("prefab_root",),
    "SceneExportCollection": ("first",),
    "ScriptExportCollection": ("script",),
    "SceneAssetExportCollection": ("none",),
    "ManagerExportCollection": ("none",),
    "EditorBuildSettingsExportCollection": ("none",),
}


class IdentityMapError(ValueError):
    """Something doubtful: missing, duplicate, ambiguous or colliding identity, or a byte mismatch."""

    def __init__(self, kind: str, detail: str):
        super().__init__(f"{kind}: {detail}")
        self.kind = kind


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def member_digest(members: list) -> str:
    """sha256 over the ordered (serialized file, PathID, ClassID, fileID) of every member."""
    text = "".join(f"{file}\t{path_id}\t{class_id}\t{'-' if file_id is None else file_id}\n"
                   for file, path_id, class_id, file_id, *_ in members)
    return sha256_bytes(text.encode("utf-8"))


# ---------------------------------------------------------------- table

def _fields(line: str, kind: str, count: int) -> list[str]:
    fields = line.split("\t")
    if len(fields) != count + 1 or fields[0] != kind:
        raise IdentityMapError("table_malformed", f"bad {kind} line: {line[:160]}")
    return fields[1:]


def _check(pattern: re.Pattern, value: str, line: str) -> str:
    if not pattern.fullmatch(value):
        raise IdentityMapError("table_malformed", f"bad value {value[:80]!r} in: {line[:160]}")
    return value


def _number(value: str, line: str) -> int:
    if not re.fullmatch(r"-?\d+", value):
        raise IdentityMapError("table_malformed", f"bad number {value[:40]!r} in: {line[:160]}")
    return int(value)


def parse_table(text: str) -> dict:
    """Parse and structurally validate a table; rejects unsorted, duplicate and malformed lines."""
    if "\r" in text or not text.endswith("\n"):
        raise IdentityMapError("table_malformed", "the table must use LF line ends and end with a newline")
    lines = text[:-1].split("\n")
    head = lines[0].split("\t")
    if len(head) != 5 or head[0] != HEADER or head[1] != str(SCHEMA):
        raise IdentityMapError("table_malformed", f"unsupported header: {lines[0][:160]}")
    header = {"schema": SCHEMA}
    for name, pattern, field in (("input", SHA, head[2]), ("assetripper", re.compile(r"[0-9a-f]{40}"), head[3]),
                                 ("patch", SHA, head[4])):
        if not field.startswith(name + "=") or not pattern.fullmatch(field[len(name) + 1:]):
            raise IdentityMapError("table_malformed", f"bad header field: {field[:80]}")
        header[name] = field[len(name) + 1:]
    body = lines[1:]
    if body != sorted(body) or len(set(body)) != len(body):
        raise IdentityMapError("table_malformed", "table lines are not sorted and unique")
    table = {"header": header, "collections": {}, "scripts": {}, "mixer": {}, "witnessed": [],
             "unresolved": set(), "files": {}}
    guids = Counter()
    for line in body:
        kind = line[:1]
        if kind == "C":
            file, path_id, class_id, guid, digest, paths, times, shas, metas = _fields(line, "C", 9)
            key = (file, _number(path_id, line), _number(class_id, line))
            row = {"guid": _check(GUID, guid, line), "digest": _check(SHA, digest, line), "paths": paths.split("|"),
                   "time_created": [_number(item, line) for item in times.split("|")],
                   "sha256": [_check(SHA, item, line) for item in shas.split("|")],
                   "meta_sha256": [_check(SHA, item, line) for item in metas.split("|")]}
            if not file or key in table["collections"] or not all(row["paths"]) \
                    or not len(row["paths"]) == len(row["time_created"]) == len(row["sha256"]) == len(row["meta_sha256"]) \
                    or row["paths"] != sorted(row["paths"]):
                raise IdentityMapError("table_malformed", f"inconsistent or duplicate collection: {line[:160]}")
            table["collections"][key] = row
            guids[row["guid"]] += 1
        elif kind == "S":
            path, guid, time, sha, meta = _fields(line, "S", 5)
            if not path or path in table["scripts"]:
                raise IdentityMapError("table_malformed", f"duplicate script: {line[:160]}")
            table["scripts"][path] = {"guid": _check(GUID, guid, line), "time_created": _number(time, line),
                                      "sha256": _check(SHA, sha, line), "meta_sha256": _check(SHA, meta, line)}
            guids[guid] += 1
        elif kind == "F":
            path, sha = _fields(line, "F", 2)
            table["files"][path] = _check(SHA, sha, line)
        elif kind == "M":
            path, ordinal, guid = _fields(line, "M", 3)
            values = table["mixer"].setdefault(path, [])
            if ordinal != f"{len(values):06d}":
                raise IdentityMapError("table_malformed", f"mixer ordinals must be consecutive from 0: {line[:160]}")
            values.append(_check(GUID, guid, line))
        elif kind == "U":
            table["unresolved"].add(_check(GUID, _fields(line, "U", 1)[0], line))
        elif kind == "W":
            members = _fields(line, "W", 1)[0].split(",")
            if len(members) < 2 or members != sorted(members) or len(set(members)) != len(members):
                raise IdentityMapError("table_malformed", f"bad witnessed class: {line[:160]}")
            table["witnessed"].append([_check(GUID, item, line) for item in members])
        else:
            raise IdentityMapError("table_malformed", f"unknown line kind: {line[:160]}")
    repeated = sorted(guid for guid, times in guids.items() if times > 1)
    if repeated:
        raise IdentityMapError("table_malformed", f"baseline GUID used by more than one row: {repeated[0]}")
    known = set(guids)
    for members in table["witnessed"]:
        if not set(members) <= known:
            raise IdentityMapError("table_malformed", "a witnessed class names a GUID that has no row")
    shared = {item for members in table["witnessed"] for item in members}
    if len(shared) != sum(len(members) for members in table["witnessed"]):
        raise IdentityMapError("table_malformed", "a GUID is in two witnessed classes")
    return table


def format_table(table: dict) -> str:
    """The canonical text of a table (inverse of parse_table); used by the builder and by tests."""
    header = table["header"]
    lines = []
    for (file, path_id, class_id), row in table["collections"].items():
        lines.append("\t".join(["C", file, str(path_id), str(class_id), row["guid"], row["digest"],
                                "|".join(row["paths"]), "|".join(map(str, row["time_created"])),
                                "|".join(row["sha256"]), "|".join(row["meta_sha256"])]))
    for path, row in table["scripts"].items():
        lines.append("\t".join(["S", path, row["guid"], str(row["time_created"]), row["sha256"], row["meta_sha256"]]))
    lines += [f"F\t{path}\t{sha}" for path, sha in table["files"].items()]
    lines += [f"M\t{path}\t{ordinal:06d}\t{guid}" for path, values in table["mixer"].items()
              for ordinal, guid in enumerate(values)]
    lines += [f"U\t{guid}" for guid in table["unresolved"]]
    lines += ["W\t" + ",".join(sorted(members)) for members in table["witnessed"]]
    for line in lines:
        if "\n" in line or "\r" in line or line.count("\t") != len(line.split("\t")) - 1:
            raise IdentityMapError("table_malformed", f"unprintable row: {line[:80]!r}")
    return "\t".join([HEADER, str(header["schema"]), f"input={header['input']}", f"assetripper={header['assetripper']}",
                      f"patch={header['patch']}"]) + "\n" + "".join(line + "\n" for line in sorted(lines))


def require_header(table: dict, input_sha256: str, assetripper: str, patch_sha256: str) -> None:
    """Refuse a table made for another input, AssetRipper commit or patch."""
    expected = {"input": input_sha256, "assetripper": assetripper, "patch": patch_sha256}
    for name, value in expected.items():
        if table["header"][name] != value:
            raise IdentityMapError("table_header_mismatch", f"{name}: table {table['header'][name]} != {value}")


def baseline_guids(table: dict) -> set[str]:
    return {row["guid"] for row in table["collections"].values()} | {row["guid"] for row in table["scripts"].values()}


# ---------------------------------------------------------------- 1. validate

def validate_identity(project: Path, collections: list[dict]) -> dict:
    """Step 1. Every .meta claimed exactly once, YAML anchors equal recorded fileIDs, scene fileID ==
    PathID, .meta GUID equals the recorded GUID (all of exportidentity.check_identity), and no GUID is
    recorded for two collections. Returns the checker's counts; raises with the first failure of each kind."""
    result = exportidentity.check_identity(project, collections, limit=3)
    if not result["ok"]:
        kind, details = next(iter(sorted(result["failures"].items())))
        raise IdentityMapError(f"identity_{kind}",
                               f"{result['counts'].get(kind, len(details))} case(s), first: {details[0]}; "
                               f"all kinds: {sorted(result['failures'])}")
    seen = {}
    for entry in collections:
        if entry["guid"] is not None:
            if entry["guid"] in seen:
                raise IdentityMapError("identity_guid_shared", f"{entry['guid']} is the GUID of collections "
                                       f"{seen[entry['guid']]} and {entry['order']}")
            seen[entry["guid"]] = entry["order"]
    return result["counts"]


# ---------------------------------------------------------------- 2. lookup

def _written(entry: dict) -> list[list]:
    return [member for member in entry["members"] if member[4] and member[5]]


def _prefab_root(project: Path, entry: dict) -> tuple:
    """The root GameObject: the one whose Transform or RectTransform has no father."""
    if len(entry["paths"]) != 1:
        raise IdentityMapError("anchor_ambiguous", f"prefab collection {entry['order']} has {len(entry['paths'])} files")
    data = (project / entry["paths"][0]).read_bytes()
    marks = list(DOCUMENT.finditer(data))
    roots = set()
    for index, mark in enumerate(marks):
        if int(mark[1]) not in (TRANSFORM, RECT_TRANSFORM) or mark[0].rstrip().endswith(b"stripped"):
            continue
        body = data[mark.end():marks[index + 1].start() if index + 1 < len(marks) else len(data)]
        father, owner = re.search(rb"m_Father: \{fileID: (-?\d+)\}", body), re.search(rb"m_GameObject: \{fileID: (-?\d+)\}", body)
        if father is None or owner is None:
            raise IdentityMapError("anchor_ambiguous", f"unreadable transform in {entry['paths'][0]}")
        if int(father[1]) == 0:
            roots.add(int(owner[1]))
    members = [member for member in _written(entry) if member[2] == GAME_OBJECT and member[3] in roots]
    if len(roots) != 1 or len(members) != 1:
        raise IdentityMapError("anchor_ambiguous", f"{entry['paths'][0]}: {len(roots)} root GameObject(s), "
                               f"{len(members)} matching member(s)")
    return members[0]


def anchor(project: Path, entry: dict):
    """The anchor member of a collection, or None for collections that own no files. Refuses a type
    with no rule and a collection whose rule does not select exactly one member."""
    rule = ANCHOR_RULES.get(entry["type"])
    if rule is None:
        raise IdentityMapError("unknown_collection_type", f"{entry['type']} (collection {entry['order']}) has no anchor rule")
    if rule[0] in ("none", "script"):
        return None
    written = _written(entry)
    if rule[0] == "single":
        chosen = written
    elif rule[0] == "class":
        chosen = [member for member in written if member[2] == rule[1]]
    elif rule[0] == "first":
        chosen = sorted(written, key=lambda member: (member[1], member[0]))[:1]
    else:
        return _prefab_root(project, entry)
    if len(chosen) != 1:
        raise IdentityMapError("anchor_ambiguous" if chosen else "anchor_missing",
                               f"{entry['type']} collection {entry['order']} ({entry['paths'][:1]}): "
                               f"rule {rule} selects {len(chosen)} of {len(written)} written serialized members")
    return chosen[0]


def lookup(project: Path, collections: list[dict], table: dict) -> dict:
    """Step 2. Map each fresh collection to its table row by anchor triple; require equal member_digest
    and equal paths; the fresh -> baseline GUID map is total, injective and onto the table. Scripts are
    verified against the table and never remapped.

    Returns {"guids": fresh -> baseline, "scripts": set of script GUIDs, "rows": fresh -> row,
    "order_witnessed": sorted baseline GUIDs attributed by witnessed order (never provenance)}."""
    guids, rows, used, script_paths = {}, {}, {}, []
    for entry in collections:
        member = anchor(project, entry)
        kind = ANCHOR_RULES[entry["type"]][0]
        if kind == "none":
            if entry["paths"] or entry["guid"] is not None:
                raise IdentityMapError("identity_unexpected_files", f"{entry['type']} collection {entry['order']} owns files or a GUID")
            continue
        if kind == "script":
            script_paths += entry["paths"]
            continue
        if entry["guid"] is None or not entry["paths"]:
            raise IdentityMapError("identity_missing", f"{entry['type']} collection {entry['order']} has no GUID or no file")
        triple = (member[0], member[1], member[2])
        if triple in used:
            raise IdentityMapError("identity_duplicate", f"anchor {triple} is claimed by collections {used[triple]} and {entry['order']}")
        used[triple] = entry["order"]
        row = table["collections"].get(triple)
        if row is None:
            raise IdentityMapError("identity_missing", f"no table row for anchor {triple} ({entry['paths'][0]})")
        if member_digest(entry["members"]) != row["digest"]:
            raise IdentityMapError("member_digest_mismatch", f"anchor {triple} ({entry['paths'][0]})")
        if sorted(entry["paths"]) != row["paths"]:
            raise IdentityMapError("path_mismatch", f"anchor {triple}: export {sorted(entry['paths'])} != table {row['paths']}")
        if entry["guid"] in guids:
            raise IdentityMapError("identity_guid_shared", f"fresh GUID {entry['guid']} appears twice")
        guids[entry["guid"]] = row["guid"]
        rows[entry["guid"]] = row
    unmatched = sorted(set(table["collections"]) - set(used))
    if unmatched:
        raise IdentityMapError("table_row_unmatched", f"{len(unmatched)} table row(s) match no export collection, first {unmatched[0]}")
    if len(set(guids.values())) != len(guids):
        raise IdentityMapError("identity_not_injective", "two collections map to one baseline GUID")
    if sorted(script_paths) != sorted(table["scripts"]) or len(set(script_paths)) != len(script_paths):
        raise IdentityMapError("script_set_mismatch", f"export has {len(script_paths)} script paths, table has {len(table['scripts'])}")
    for path, row in table["scripts"].items():
        found = META_GUID.search((project / (path + ".meta")).read_bytes())
        if found is None or found[1].decode() != row["guid"]:
            raise IdentityMapError("script_guid_differs", f"{path}: export {found and found[1].decode()} != table {row['guid']}")
    witnessed = sorted(item for members in table["witnessed"] for item in members)
    return {"guids": guids, "rows": rows, "scripts": {row["guid"] for row in table["scripts"].values()},
            "order_witnessed": witnessed}


# ---------------------------------------------------------------- positions

def tokens(data: bytes, meta: bool) -> list[tuple[int, int, str, str]]:
    """Every 32-hex token as (start, end, kind, lowercase text). Kinds: "meta" (a .meta `guid:` line),
    "reference" (`{fileID: N, guid: G, type: T}`), "asset" (an m_AssetGUID value), "other" (anything
    else, including upper case spellings and tokens inside binary data). Only meta and reference
    positions are ever rewritten."""
    positions = {}
    patterns = [(REFERENCE, "reference"), (ASSET_GUID, "asset")] + ([(META_GUID, "meta")] if meta else [])
    for pattern, kind in patterns:
        for found in pattern.finditer(data):
            positions[found.start(1)] = kind
    return [(found.start(), found.end(), positions.get(found.start(), "other"), found[0].decode().lower())
            for found in TOKEN.finditer(data)]


def _is_text(data: bytes) -> bool:
    return b"\0" not in data


def _files(project: Path):
    for path in sorted(project.rglob("*")):
        if path.is_file():
            yield path, path.relative_to(project).as_posix()


# ---------------------------------------------------------------- 3. collisions

def check_collisions(project: Path, plan: dict, table: dict, catalog_keys) -> dict:
    """Step 3. No fresh GUID may equal a baseline GUID, a catalog key, an unresolved reference GUID, an
    m_AssetGUID value, or any 32-hex string outside a reference position. Also, a reference that
    resolves to nothing must not equal a baseline GUID, or it would bind after relabelling.
    Returns counts of the scanned positions."""
    fresh = set(plan["guids"])
    baseline = baseline_guids(table) | set(plan["guids"].values())
    catalog = {key.lower() for key in catalog_keys}
    references, assets, others, counts = set(), {}, {}, Counter()
    mixers = set(table["mixer"])
    for path, relative in _files(project):
        for _, _, kind, text in tokens(path.read_bytes(), relative.endswith(".meta")):
            counts[kind] += 1
            if kind == "asset":
                assets.setdefault(text, relative)
            elif kind == "other":
                others.setdefault(text, relative)
            else:
                references.add(text)
    for name, found in (("baseline", fresh & baseline), ("catalog_key", fresh & catalog),
                        ("unresolved_reference", fresh & table["unresolved"]),
                        ("asset_guid", fresh & set(assets)), ("outside_reference_position", fresh & set(others))):
        if found:
            guid = sorted(found)[0]
            where = {"asset_guid": assets, "outside_reference_position": others}.get(name, {}).get(guid, "")
            raise IdentityMapError(f"collision_{name}", f"fresh GUID {guid} ({len(found)} such) {where}")
    unresolved = (references - fresh - plan["scripts"]) | table["unresolved"]
    if unresolved & baseline:
        raise IdentityMapError("collision_unresolved_reference_is_baseline", sorted(unresolved & baseline)[0])
    return dict(sorted(counts.items()))


# ---------------------------------------------------------------- 4. relabel

def relabel(project: Path, stage: Path, plan: dict, table: dict) -> dict:
    """Step 4. Copy the project to `stage` (which must not exist) and rewrite only: `guid:` lines of
    .meta files and `{fileID, guid, type}` references (fresh -> baseline), `timeCreated` of every .meta
    (baseline value), and the AudioMixer values (nth bare GUID -> baseline nth; each must occur exactly
    once). Binary files and m_AssetGUID strings are never touched."""
    project, stage = Path(project), Path(stage)
    if stage.exists():
        raise IdentityMapError("stage_exists", str(stage))
    times = {}
    for row in plan["rows"].values():
        times.update({path + ".meta": time for path, time in zip(row["paths"], row["time_created"])})
    times.update({path + ".meta": row["time_created"] for path, row in table["scripts"].items()})
    mapping, counts = plan["guids"], Counter()
    shutil.copytree(project, stage)
    try:
        return _relabel_stage(stage, plan, table, times, mapping, counts)
    except BaseException:
        shutil.rmtree(stage, ignore_errors=True)  # a refused stage must not look like a result
        raise


def _relabel_stage(stage: Path, plan: dict, table: dict, times: dict, mapping: dict, counts: Counter) -> dict:
    mixers_done = set()
    for path, relative in _files(stage):
        data = path.read_bytes()
        if not _is_text(data):
            continue
        meta = relative.endswith(".meta")
        edits = []
        for start, end, kind, text in tokens(data, meta):
            if kind in ("meta", "reference") and text in mapping:
                edits.append((start, end, mapping[text].encode(), kind))
            elif kind == "meta" and text not in plan["scripts"]:
                raise IdentityMapError("meta_guid_unmapped", f"{relative}: {text}")
        if relative in table["mixer"]:
            others = [(start, end, text) for start, end, kind, text in tokens(data, False) if kind == "other"]
            baseline = table["mixer"][relative]
            texts = [text for *_, text in others]
            if len(others) != len(baseline) or len(set(texts)) != len(texts) \
                    or any(data.lower().count(text.encode()) != 1 for text in texts):
                raise IdentityMapError("mixer_value_not_exactly_once",
                                       f"{relative}: {len(others)} bare values, {len(set(texts))} distinct, table has {len(baseline)}")
            edits += [(start, end, value.encode(), "mixer") for (start, end, _), value in zip(others, baseline)]
            mixers_done.add(relative)
        if meta:
            if relative not in times:
                raise IdentityMapError("meta_unowned", relative)
            found = TIME_CREATED.findall(data)
            if len(found) != 1:
                raise IdentityMapError("meta_time_created", f"{relative} has {len(found)} timeCreated lines")
            edits.append((TIME_CREATED.search(data).start() + len(b"timeCreated: "), TIME_CREATED.search(data).end(),
                          str(times[relative]).encode(), "time"))
        if not edits:
            continue
        for start, end, value, kind in sorted(edits, reverse=True):
            data = data[:start] + value + data[end:]
            counts[kind] += 1
        path.write_bytes(data)
    if mixers_done != set(table["mixer"]):
        raise IdentityMapError("mixer_file_missing", f"{sorted(set(table['mixer']) - mixers_done)[0]}")
    return dict(sorted(counts.items()))


# ---------------------------------------------------------------- 5. end gate

def _asset_guid_values(project: Path) -> Counter:
    values = Counter()
    for path, relative in _files(project):
        for _, _, kind, text in tokens(path.read_bytes(), relative.endswith(".meta")):
            if kind == "asset":
                values[(relative, text)] += 1
    return values


def expected_hashes(table: dict) -> dict:
    """path -> sha256 for every file the baseline project holds."""
    hashes = dict(table["files"])
    for key, row in table["collections"].items():
        for path, sha, meta in zip(row["paths"], row["sha256"], row["meta_sha256"]):
            hashes[path], hashes[path + ".meta"] = sha, meta
    for path, row in table["scripts"].items():
        hashes[path], hashes[path + ".meta"] = row["sha256"], row["meta_sha256"]
    return hashes


def verify_output(source: Path, stage: Path, plan: dict, table: dict) -> dict:
    """Step 5. The sha256 of every file in `stage` equals the table, and no file is missing or extra; no
    fresh GUID remains in any file; the m_AssetGUID values are identical before (`source`) and after.
    Raises with the first failure in full, content mismatches first."""
    expected, actual = expected_hashes(table), {}
    for path, relative in _files(Path(stage)):
        actual[relative] = sha256_bytes(path.read_bytes())
    for relative in sorted(set(expected) & set(actual)):
        if expected[relative] != actual[relative]:
            raise IdentityMapError("hash_mismatch", f"{relative}: expected {expected[relative]}, got {actual[relative]}")
    for relative in sorted(set(expected) - set(actual)):
        raise IdentityMapError("file_missing", relative)
    for relative in sorted(set(actual) - set(expected)):
        raise IdentityMapError("file_extra", relative)
    fresh = set(plan["guids"])
    for path, relative in _files(Path(stage)):
        for _, _, _, text in tokens(path.read_bytes(), relative.endswith(".meta")):
            if text in fresh:
                raise IdentityMapError("fresh_guid_left", f"{relative}: {text}")
    before, after = _asset_guid_values(Path(source)), _asset_guid_values(Path(stage))
    if before != after:
        changed = sorted(set(before.items()) ^ set(after.items()))
        raise IdentityMapError("asset_guid_changed", f"{len(changed)} m_AssetGUID difference(s), first {changed[0]}")
    return {"files": len(actual), "asset_guid_values": sum(after.values())}


# ---------------------------------------------------------------- all steps

def build_baseline_project(project: Path, identity: Path, table_path: Path, stage: Path, catalog_keys) -> dict:
    """Run steps 1 to 5 on an exported project and write the baseline project to `stage`."""
    collections = exportidentity.load_identity(identity)
    table = parse_table(Path(table_path).read_text(encoding="utf-8"))
    counts = validate_identity(project, collections)
    plan = lookup(project, collections, table)
    check_collisions(project, plan, table, catalog_keys)
    edits = relabel(project, stage, plan, table)
    gate = verify_output(project, stage, plan, table)
    return {"identity": counts, "edits": edits, "gate": gate, "order_witnessed": plan["order_witnessed"]}

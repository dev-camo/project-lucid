"""Synthetic fixtures for the export identity relabel step; no AssetRipper output is read."""

import copy
import json
from pathlib import Path
import shutil
import tempfile
import unittest

from tools.lucidlib import exportidentity, identitymap
from tools.lucidlib.identitymap import IdentityMapError

HEADER = {"schema": 1, "input": "a" * 64, "assetripper": "b" * 40, "patch": "c" * 64}


def g(label):
    return (label * 32)[:32]


# One value per kind, for the baseline project (B*) and the fresh export (F*).
BASE = {"mat": g("1"), "tex": g("2"), "pre": g("3"), "sce": g("4"), "mix": g("5"), "scr": g("6")}
FRESH = {"mat": g("a"), "tex": g("b"), "pre": g("c"), "sce": g("d"), "mix": g("e"), "scr": g("6")}
MIXER_BASE, MIXER_FRESH = [g("7"), g("8")], [g("f"), g("9")]
ASSET_GUID = g("0")
ZERO, STABLE = g("0"), "0123456789abcdef" * 2  # mixer tokens that AssetRipper does not regenerate
AMY = "Assets/Data/Art/Characters/Amy/amy_hammer/mesh/amy_hammer|amy_hammerAction.anim"  # a real baseline path
BUILD_SETTINGS = "ProjectSettings/EditorBuildSettings.asset"
CATALOG = g("3").replace("3", "4", 1)
UNRESOLVED = g("1").replace("1", "e", 3)


def yaml(*docs):
    return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "".join(f"--- !u!{c} &{f}\n{body}\n" for c, f, body in docs)


def meta(guid, time):
    return f"fileFormatVersion: 2\nguid: {guid}\ntimeCreated: {time}\n"


def project_files(ids, mixer, time):
    """The files of a project whose collections carry the GUIDs `ids`."""
    return {
        "Assets/Material/a.mat": yaml((21, 2100000, f"m_Tex: {{fileID: 2800000, guid: {ids['tex']}, type: 3}}\n"
                                                    f"m_AssetGUID: {ASSET_GUID}")),
        "Assets/Material/a.mat.meta": meta(ids["mat"], time),
        "Assets/Texture/t.png": b"\x89PNG\0\0" + b"\x00" * 8,
        "Assets/Texture/t.png.meta": meta(ids["tex"], time),
        "Assets/Prefab/p.prefab": yaml(
            (1, 100, "m_Name: root"), (4, 200, "m_GameObject: {fileID: 100}\nm_Father: {fileID: 0}"),
            (1, 101, "m_Name: child"), (4, 201, f"m_GameObject: {{fileID: 101}}\nm_Father: {{fileID: 200}}\n"
                                                f"m_Mat: {{fileID: 2100000, guid: {ids['mat']}, type: 2}}\n"
                                                f"m_Script: {{fileID: 11500000, guid: {ids['scr']}, type: 3}}\n"
                                                f"m_Gone: {{fileID: 5, guid: {UNRESOLVED}, type: 3}}")),
        "Assets/Prefab/p.prefab.meta": meta(ids["pre"], time),
        "Assets/Scenes/s.unity": yaml((1, 11, f"m_Prefab: {{fileID: 100, guid: {ids['pre']}, type: 3}}")),
        "Assets/Scenes/s.unity.meta": meta(ids["sce"], time),
        "Assets/Scripts/x.cs": "class X {}\n",
        "Assets/Scripts/x.cs.meta": meta(ids["scr"], time),
        "Assets/Audio/Main.mixer": yaml((241, 2410000, f"group: {mixer[0]}\nsnap: {mixer[1]}")),
        "Assets/Audio/Main.mixer.meta": meta(ids["mix"], time),
        "ProjectSettings/ProjectSettings.asset": "settings: 1\n",
    }


def write(root, files):
    for relative, content in files.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content if isinstance(content, bytes) else content.encode())


def collections(ids):
    def entry(order, kind, guid, paths, members):
        return {"order": order, "type": kind, "guid": guid, "paths": paths, "members": members}
    return [
        entry(0, "MaterialExportCollection", ids["mat"], ["Assets/Material/a.mat"],
              [["cab-m", 7, 21, 2100000, True, True]]),
        entry(1, "TextureExportCollection", ids["tex"], ["Assets/Texture/t.png"],
              [["cab-t", 3, 28, 2800000, True, True]]),
        entry(2, "PrefabExportCollection", ids["pre"], ["Assets/Prefab/p.prefab"],
              [["Generated Prefabs", 9, 1001, 100100000, False, False], ["cab-p", 10, 1, 100, True, True],
               ["cab-p", 11, 4, 200, True, True], ["cab-p", 12, 1, 101, True, True],
               ["cab-p", 13, 4, 201, True, True]]),
        entry(3, "SceneExportCollection", ids["sce"], ["Assets/Scenes/s.unity"], [["cab-s", 11, 1, 11, True, True]]),
        entry(4, "AudioMixerExportCollection", ids["mix"], ["Assets/Audio/Main.mixer"],
              [["cab-x", 5, 241, 2410000, True, True]]),
        entry(5, "ScriptExportCollection", None, ["Assets/Scripts/x.cs"], [["cab-c", 2, 115, 11500000, True, True]]),
        entry(6, "SceneAssetExportCollection", None, [], [["cab-s", 1, 1032, None, True, False]]),
    ]


def build_settings(scene):
    return yaml((1045, 1, "EditorBuildSettings:\n  serializedVersion: 2\n  m_Scenes:\n  - enabled: 1\n"
                          f"    path: Assets/Scenes/s.unity\n    guid: {scene}\n  m_configObjects: {{}}"))


def mixer_file(tokens):
    return yaml((241, 2410000, "".join(f"t{index}: {token}\n" for index, token in enumerate(tokens))))


def build_table(root, records, mixer=None):
    """What the (later) builder will produce from the baseline project, written here from its files."""
    sha = lambda relative: identitymap.sha256_bytes((root / relative).read_bytes())
    time = lambda relative: int(
        identitymap.TIME_CREATED.search((root / (relative + ".meta")).read_bytes())[0].split()[1])
    table = {"header": dict(HEADER), "collections": {}, "scripts": {},
             "mixer": {"Assets/Audio/Main.mixer": list(MIXER_BASE if mixer is None else mixer)},
             "witnessed": [], "unresolved": {UNRESOLVED}, "files": {}}
    owned = set()
    for entry in records:
        for relative in entry["paths"]:
            owned |= {relative, relative + ".meta"}
        kind = identitymap.ANCHOR_RULES[entry["type"]][0]
        if kind == "script":
            for relative in entry["paths"]:
                table["scripts"][relative] = {"guid": BASE["scr"], "time_created": time(relative),
                                              "sha256": sha(relative), "meta_sha256": sha(relative + ".meta")}
        elif kind != "none":
            member = identitymap.anchor(root, entry)
            table["collections"][(member[0], member[1], member[2])] = {
                "guid": entry["guid"], "digest": identitymap.member_digest(entry["members"]),
                "paths": sorted(entry["paths"]), "time_created": [time(p) for p in sorted(entry["paths"])],
                "sha256": [sha(p) for p in sorted(entry["paths"])],
                "meta_sha256": [sha(p + ".meta") for p in sorted(entry["paths"])]}
    table["files"] = {path.relative_to(root).as_posix(): sha(path.relative_to(root).as_posix())
                      for path in root.rglob("*") if path.is_file() and path.relative_to(root).as_posix() not in owned}
    return table


class Fixture(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="test-identitymap-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.baseline_dir, self.fresh_dir, self.stage = self.tmp / "baseline", self.tmp / "fresh", self.tmp / "stage"
        write(self.baseline_dir, project_files(BASE, MIXER_BASE, 1111))
        write(self.fresh_dir, project_files(FRESH, MIXER_FRESH, 2222))
        self.records = collections(FRESH)
        self.table = build_table(self.baseline_dir, collections(BASE))

    def lookup(self):
        return identitymap.lookup(self.fresh_dir, self.records, self.table)

    def run_all(self, catalog=()):
        plan = self.lookup()
        identitymap.check_collisions(self.fresh_dir, plan, self.table, catalog)
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        return plan, identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table)

    def refused(self, kind, call):
        with self.assertRaises(IdentityMapError) as caught:
            call()
        self.assertEqual(kind, caught.exception.kind, str(caught.exception))
        return str(caught.exception)

    def set_mixer(self, fresh, base):
        """Give the mixer these bare tokens in the fresh export and in the baseline, and remake the table."""
        write(self.fresh_dir, {"Assets/Audio/Main.mixer": mixer_file(fresh)})
        write(self.baseline_dir, {"Assets/Audio/Main.mixer": mixer_file(base)})
        self.table = build_table(self.baseline_dir, collections(BASE), mixer=base)

    def add_build_settings(self):
        """Add the build settings, naming the scene by GUID, to both projects and remake the table."""
        write(self.fresh_dir, {BUILD_SETTINGS: build_settings(FRESH["sce"])})
        write(self.baseline_dir, {BUILD_SETTINGS: build_settings(BASE["sce"])})
        self.table = build_table(self.baseline_dir, collections(BASE))

    def fresh_file(self, relative, old, new):
        path = self.fresh_dir / relative
        data = path.read_bytes()
        self.assertIn(old.encode(), data)
        path.write_bytes(data.replace(old.encode(), new.encode()))


class TableTests(Fixture):
    def test_table_text_round_trips_and_is_sorted(self):
        text = identitymap.format_table(self.table)
        parsed = identitymap.parse_table(text)
        self.assertEqual(text, identitymap.format_table(parsed))
        self.assertEqual(self.table, parsed)
        lines = text.split("\n")[1:-1]
        self.assertEqual(sorted(lines), lines)

    def test_malformed_tables_are_refused(self):
        text = identitymap.format_table(self.table)
        lines = text.split("\n")
        bad = {"unsorted": "\n".join([lines[0]] + lines[1:-1][::-1]) + "\n", "no newline": text[:-1],
               "crlf": text.replace("\n", "\r\n"), "duplicate line": "\n".join(lines[:2] + lines[1:]),
               "wrong schema": text.replace("\t1\t", "\t2\t", 1), "unknown kind": text + "Z\tx\n",
               "short guid": text.replace(BASE["mat"], BASE["mat"][:30]),
               "repeated baseline guid": text.replace(BASE["tex"], BASE["mat"])}
        for label, broken in bad.items():
            with self.subTest(label):
                with self.assertRaises(IdentityMapError):
                    identitymap.parse_table(broken)

    def resorted(self, text, extra=(), drop=()):
        """The table text with lines added or dropped and the body sorted again (so only that change is wrong)."""
        head, *body = text[:-1].split("\n")
        return head + "\n" + "".join(line + "\n" for line in sorted([x for x in body if x not in drop] + list(extra)))

    def test_unsorted_table_without_mixer_rows_is_refused(self):
        self.table["mixer"] = {}  # the mixer ordinals would otherwise refuse a reversed table first
        lines = identitymap.format_table(self.table).split("\n")
        self.assertGreater(len(lines[1:-1]), 3)
        self.assertIn("not sorted", self.refused("table_malformed", lambda: identitymap.parse_table(
            "\n".join([lines[0]] + lines[1:-1][::-1]) + "\n")))

    def awkward_table(self):
        """A table whose paths hold `|`, TAB, `%` and every escape sequence as literal text."""
        paths = sorted([AMY, "Assets/tab\there/a%b.png", "Assets/plain.png", "Assets/pct%7Cliteral.png"])
        self.table["collections"][("cab|odd\t%", 1, 28)] = {
            "guid": g("9"), "digest": "d" * 64, "paths": paths, "time_created": [1, 2, 3, 4],
            "sha256": ["e" * 64] * 4, "meta_sha256": ["f" * 64] * 4}
        self.table["files"]["ProjectSettings/new\nline\rx|y%.asset"] = "b" * 64
        self.table["mixer"]["Assets/Audio/Mix|er.mixer"] = [g("7"), g("7")]
        self.table["scripts"]["Assets/Scripts/p|q%.cs"] = {"guid": g("8"), "time_created": 5, "sha256": "1" * 64,
                                                            "meta_sha256": "2" * 64}

    def test_real_baseline_path_with_a_pipe_round_trips(self):
        self.awkward_table()
        text = identitymap.format_table(self.table)
        self.assertEqual(self.table, identitymap.parse_table(text))
        self.assertEqual(text, identitymap.format_table(identitymap.parse_table(text)))
        self.assertIn("amy_hammer%7Camy_hammerAction.anim", text)
        self.assertNotIn("amy_hammer|amy_hammerAction", text)
        self.assertIn("a%25b.png", text)
        self.assertIn("tab%09here", text)
        self.assertIn("pct%257Cliteral", text)  # a literal `%7C` in a path is not mistaken for an escaped pipe
        for line in text.split("\n")[1:-1]:  # one table line per object, in the column count of its kind
            self.assertEqual({"C": 10, "F": 3, "M": 4, "S": 6, "U": 2, "W": 2}[line[0]], len(line.split("\t")), line)

    def test_unwritable_fields_are_refused_when_formatting(self):
        for label, change in {
                "tab in a guid": lambda t: t["collections"][("cab-t", 3, 28)].__setitem__("guid", "a\tb"),
                "newline in a sha": lambda t: t["files"].__setitem__("x", "a\nb"),
                "short guid": lambda t: t["collections"][("cab-t", 3, 28)].__setitem__("guid", "abc"),
                "tab in the header": lambda t: t["header"].__setitem__("input", "a\tb"),
                "number as text": lambda t: t["scripts"]["Assets/Scripts/x.cs"].__setitem__(
                    "time_created", "1\t2")}.items():
            with self.subTest(label):
                self.setUp()
                change(self.table)
                message = self.refused("table_malformed", lambda: identitymap.format_table(self.table))
                if label != "short guid":
                    self.assertIn("cannot be written", message)  # the field guard, not the later re-parse

    def test_non_canonical_escapes_are_refused(self):
        self.awkward_table()
        text = identitymap.format_table(self.table)
        for label, old, new in (("lower case escape", "amy_hammer%7Camy", "amy_hammer%7camy"),
                                ("unknown escape", "a%25b.png", "a%41b.png"),
                                ("lone percent", "a%25b.png", "a%b.png"),
                                ("raw pipe in a file path", "new%0Aline%0Dx%7Cy%25", "new%0Aline%0Dx|y%25")):
            with self.subTest(label):
                self.assertEqual(1, text.count(old))
                self.refused("table_malformed", lambda: identitymap.parse_table(self.resorted(text.replace(old, new))))

    def test_duplicate_and_overlapping_paths_are_refused(self):
        text = identitymap.format_table(self.table)
        script = "Assets/Scripts/x.cs"
        cases = {"F path twice": ("F\tProjectSettings/ProjectSettings.asset\t" + "9" * 64,),
                 "F path equal to a C path": ("F\tAssets/Material/a.mat\t" + "9" * 64,),
                 "F path equal to a C meta": ("F\tAssets/Texture/t.png.meta\t" + "9" * 64,),
                 "F path equal to a script": (f"F\t{script}\t" + "9" * 64,),
                 "F path equal to a script meta": (f"F\t{script}.meta\t" + "9" * 64,)}
        for label, extra in cases.items():
            with self.subTest(label):
                self.assertIn("more than one row", self.refused(
                    "table_malformed", lambda: identitymap.parse_table(self.resorted(text, extra))))

    def test_non_canonical_integers_are_refused(self):
        text = identitymap.format_table(self.table)
        mat_sha = self.table["collections"][("cab-m", 7, 21)]["sha256"][0]
        cases = {"leading zeros in a PathID": ("\t7\t21\t", "\t007\t21\t"),
                 "plus sign": ("\t7\t21\t", "\t+7\t21\t"),
                 "minus zero": ("\t3\t28\t", "\t-0\t28\t"),
                 "leading zeros in a ClassID": ("\t3\t28\t", "\t3\t028\t"),
                 "leading zeros in a time": (f"\t1111\t{mat_sha}\t", f"\t01111\t{mat_sha}\t"),
                 "space": ("\t7\t21\t", "\t7 \t21\t")}
        for label, (old, new) in cases.items():
            with self.subTest(label):
                self.assertEqual(1, text.count(old), old)
                self.assertIn("non-canonical", self.refused("table_malformed", lambda: identitymap.parse_table(
                    self.resorted(text.replace(old, new)))))
        script_time = f"{BASE['scr']}\t1111\t"
        self.assertEqual(1, text.count(script_time))
        self.refused("table_malformed", lambda: identitymap.parse_table(
            self.resorted(text.replace(script_time, f"{BASE['scr']}\t1111.0\t"))))

    def test_witnessed_classes_must_name_rows_and_not_overlap(self):
        text = identitymap.format_table(self.table)
        unknown = f"W\t{','.join(sorted([BASE['mat'], g('9')]))}"
        self.assertIn("no row", self.refused("table_malformed", lambda: identitymap.parse_table(
            self.resorted(text, [unknown]))))
        first, second = sorted([BASE["mat"], BASE["tex"]]), sorted([BASE["tex"], BASE["pre"]])
        self.assertIn("two witnessed", self.refused("table_malformed", lambda: identitymap.parse_table(
            self.resorted(text, [f"W\t{','.join(first)}", f"W\t{','.join(second)}"]))))
        one = self.resorted(text, [f"W\t{','.join(first)}"])  # a valid class is accepted
        self.assertEqual([first], identitymap.parse_table(one)["witnessed"])

    def test_header_must_match_input_commit_and_patch(self):
        identitymap.require_header(self.table, "a" * 64, "b" * 40, "c" * 64)
        for args in (("f" * 64, "b" * 40, "c" * 64), ("a" * 64, "f" * 40, "c" * 64), ("a" * 64, "b" * 40, "f" * 64)):
            self.refused("table_header_mismatch", lambda: identitymap.require_header(self.table, *args))

    def test_member_digest_depends_on_order_and_every_field(self):
        members = self.records[2]["members"]
        digest = identitymap.member_digest(members)
        self.assertNotEqual(digest, identitymap.member_digest(members[::-1]))
        changed = copy.deepcopy(members)
        changed[1][3] = 101
        self.assertNotEqual(digest, identitymap.member_digest(changed))


class ValidateTests(Fixture):
    def test_valid_record_passes(self):
        self.assertEqual(7, identitymap.validate_identity(self.fresh_dir, self.records)["collections"])

    def test_each_inconsistency_is_refused(self):
        cases = {
            "identity_anchors_differ_from_members": lambda: self.records[0]["members"][0].__setitem__(3, 2100001),
            "identity_meta_not_claimed_exactly_once": lambda: self.records.pop(1),
            "identity_scene_fileid_differs_from_pathid": lambda: self.records[3]["members"][0].__setitem__(1, 12),
            "identity_guid_shared": lambda: self.records[1].__setitem__("guid", FRESH["mat"]),
        }
        for kind, change in cases.items():
            with self.subTest(kind):
                self.setUp()
                change()
                if kind == "identity_guid_shared":
                    self.fresh_file("Assets/Texture/t.png.meta", FRESH["tex"], FRESH["mat"])
                self.refused(kind, lambda: identitymap.validate_identity(self.fresh_dir, self.records))


class LookupTests(Fixture):
    def test_every_collection_maps_to_its_baseline_guid(self):
        plan = self.lookup()
        self.assertEqual({FRESH[k]: BASE[k] for k in ("mat", "tex", "pre", "sce", "mix")}, plan["guids"])
        self.assertEqual({BASE["scr"]}, plan["scripts"])
        self.assertEqual([], plan["order_witnessed"])

    def test_prefab_anchor_is_the_root_gameobject_not_the_first_one(self):
        self.assertEqual(["cab-p", 10, 1], identitymap.anchor(self.fresh_dir, self.records[2])[:3])
        self.fresh_file("Assets/Prefab/p.prefab", "m_Father: {fileID: 200}", "m_Father: {fileID: 0}")
        self.refused("anchor_ambiguous", self.lookup)

    def test_scene_anchor_is_refused_when_members_come_from_two_serialized_files(self):
        self.assertEqual(["cab-s", 11, 1], identitymap.anchor(self.fresh_dir, self.records[3])[:3])
        self.records[3]["members"].append(["cab-s2", 2, 1, 2, True, True])  # a lower PathID, in another file
        self.assertIn("2 serialized files", self.refused("anchor_ambiguous", self.lookup))
        self.records[3]["members"][1][4] = False  # not written: it is not a candidate
        self.assertEqual(["cab-s", 11, 1], identitymap.anchor(self.fresh_dir, self.records[3])[:3])

    def test_scene_anchor_is_the_lowest_pathid_of_one_file(self):
        self.records[3]["members"].append(["cab-s", 5, 1, 5, True, True])
        self.assertEqual(["cab-s", 5, 1], identitymap.anchor(self.fresh_dir, self.records[3])[:3])

    def test_two_table_rows_with_one_baseline_guid_are_refused(self):
        # parse_table refuses this table; lookup is public and builds its own map, so it checks again.
        self.table["collections"][("cab-t", 3, 28)]["guid"] = BASE["mat"]
        self.refused("identity_not_injective", self.lookup)

    def test_missing_identity_is_refused(self):
        del self.table["collections"][("cab-t", 3, 28)]
        self.refused("identity_missing", self.lookup)

    def test_duplicate_identity_is_refused(self):
        clone = copy.deepcopy(self.records[1])
        clone.update(order=9, guid=g("9"), paths=["Assets/Texture/u.png"])
        self.records.append(clone)
        self.refused("identity_duplicate", self.lookup)

    def test_ambiguous_identity_is_refused(self):
        self.records[1]["members"].append(["cab-t", 4, 28, 2800001, True, True])
        self.refused("anchor_ambiguous", self.lookup)

    def test_collection_without_an_original_member_is_refused(self):
        self.records[1]["members"][0][4] = False
        self.refused("anchor_missing", self.lookup)

    def test_member_digest_mismatch_is_refused(self):
        self.records[2]["members"].append(["cab-p", 14, 4, 202, True, False])
        self.refused("member_digest_mismatch", self.lookup)

    def test_unknown_collection_type_is_refused(self):
        self.records[0]["type"] = "NewKindExportCollection"
        self.refused("unknown_collection_type", self.lookup)

    def test_table_row_without_a_collection_is_refused(self):
        self.table["collections"][("cab-z", 1, 28)] = dict(self.table["collections"][("cab-t", 3, 28)], guid=g("9"))
        self.refused("table_row_unmatched", self.lookup)

    def test_two_collections_with_one_fresh_guid_are_refused(self):
        self.records[3]["guid"] = FRESH["mat"]
        self.refused("identity_guid_shared", self.lookup)

    def test_paths_must_equal_the_table(self):
        self.table["collections"][("cab-t", 3, 28)]["paths"] = ["Assets/Texture/other.png"]
        self.refused("path_mismatch", self.lookup)

    def test_script_guid_must_already_equal_the_table(self):
        self.table["scripts"]["Assets/Scripts/x.cs"]["guid"] = g("9")
        self.refused("script_guid_differs", self.lookup)

    def test_script_set_must_equal_the_table(self):
        self.table["scripts"]["Assets/Scripts/y.cs"] = dict(self.table["scripts"]["Assets/Scripts/x.cs"])
        self.refused("script_set_mismatch", self.lookup)

    def test_collections_that_own_no_files_must_own_no_guid(self):
        self.records[6]["guid"] = g("9")
        self.refused("identity_unexpected_files", self.lookup)

    def test_order_witnessed_classes_are_reported_separately(self):
        self.table["witnessed"] = [sorted([BASE["mat"], BASE["tex"]])]
        self.assertEqual(sorted([BASE["mat"], BASE["tex"]]), self.lookup()["order_witnessed"])


class CollisionTests(Fixture):
    def collide(self, fresh_key, value):
        """Change the fresh GUID of one collection to `value`, consistently in record and files."""
        old = FRESH[fresh_key]
        for index, entry in enumerate(self.records):
            if entry["guid"] == old:
                entry["guid"] = value
        for path in self.fresh_dir.rglob("*"):
            if path.is_file() and b"\0" not in path.read_bytes():
                path.write_bytes(path.read_bytes().replace(old.encode(), value.encode()))

    def check(self, catalog=()):
        plan = self.lookup()
        return identitymap.check_collisions(self.fresh_dir, plan, self.table, catalog)

    def test_clean_export_passes(self):
        counts = self.check()
        self.assertEqual({"asset": 1, "meta": 6, "other": 2, "reference": 5}, counts)

    def test_fresh_equal_to_a_baseline_guid(self):
        self.collide("mat", BASE["scr"])
        self.records[0]["guid"] = BASE["scr"]
        self.refused("collision_baseline", self.check)

    def test_fresh_equal_to_a_catalog_key(self):
        self.refused("collision_catalog_key", lambda: self.check([FRESH["pre"].upper()]))

    def test_fresh_equal_to_an_unresolved_reference(self):
        self.table["unresolved"].add(FRESH["tex"])
        self.refused("collision_unresolved_reference", self.check)

    def test_fresh_equal_to_an_asset_guid_value(self):
        self.fresh_file("Assets/Material/a.mat", ASSET_GUID, FRESH["sce"])
        self.refused("collision_asset_guid", self.check)

    def test_fresh_in_any_other_position(self):
        self.fresh_file("ProjectSettings/ProjectSettings.asset", "settings: 1", f"settings: {FRESH['mat']}")
        self.assertIn("ProjectSettings", self.refused("collision_outside_reference_position", self.check))

    def test_fresh_in_a_binary_file_or_upper_case_or_odd_reference_shape_is_refused(self):
        for label, name, edit in (("binary", "Assets/Texture/t.png",
                                   lambda p: p.write_bytes(b"\0" + FRESH["mat"].encode())),
                                  ("upper case", "Assets/Scripts/x.cs", lambda p: p.write_text(FRESH["mat"].upper())),
                                  ("reference shape", "Assets/Scripts/x.cs",
                                   lambda p: p.write_text(f"{{guid: {FRESH['mat']}}}"))):
            with self.subTest(label):
                self.setUp()
                edit(self.fresh_dir / name)
                self.refused("collision_outside_reference_position", self.check)

    def test_build_settings_scene_entry_is_a_reference_position(self):
        self.add_build_settings()
        data = (self.fresh_dir / BUILD_SETTINGS).read_bytes()
        self.assertEqual([("scene", FRESH["sce"])], [item[2:] for item in identitymap.tokens(data, BUILD_SETTINGS)])
        counts = self.check()
        self.assertEqual({"asset": 1, "meta": 6, "other": 2, "reference": 5, "scene": 1}, counts)

    def test_a_scene_collision_in_the_build_settings_is_still_a_collision(self):
        self.add_build_settings()
        self.table["unresolved"].add(FRESH["sce"])
        self.refused("collision_unresolved_reference", self.check)

    def test_bare_guid_anywhere_else_is_not_a_scene_entry(self):
        self.add_build_settings()
        listed = "  m_Scenes:\n  - enabled: 1\n    path: Assets/Scenes/s.unity\n    guid: " + FRESH["mat"]
        settings = (self.fresh_dir / BUILD_SETTINGS).read_text()
        cases = {  # label: (file, text)
            "settings, outside the list": (BUILD_SETTINGS, settings.replace(
                "  m_configObjects: {}", f"  m_configObjects: {{}}\n  m_Other:\n    guid: {FRESH['mat']}")),
            "settings, key not indented as a list entry": (BUILD_SETTINGS, settings.replace(
                f"    guid: {FRESH['sce']}", f"  guid: {FRESH['mat']}")),
            "settings, first key of a list entry": (BUILD_SETTINGS, settings.replace(
                f"  - enabled: 1\n    path: Assets/Scenes/s.unity\n    guid: {FRESH['sce']}",
                f"  - guid: {FRESH['mat']}")),
            "settings, in a comment": (BUILD_SETTINGS, settings + f"# guid: {FRESH['mat']}\n"),
            "another file with the same list": ("ProjectSettings/Other.asset", listed + "\n"),
            "a script": ("Assets/Scripts/x.cs", listed + "\n"),
            "settings copy": ("ProjectSettings/EditorBuildSettings.asset.bak", listed + "\n")}
        for label, (name, text) in cases.items():
            with self.subTest(label):
                self.setUp()
                self.add_build_settings()
                (self.fresh_dir / name).write_text(text)
                kinds = {item[2] for item in identitymap.tokens((self.fresh_dir / name).read_bytes(), name)
                         if item[3] == FRESH["mat"]}
                self.assertEqual({"other"}, kinds)
                self.refused("collision_outside_reference_position", self.check)

    def test_unresolved_reference_that_equals_a_baseline_guid(self):
        self.fresh_file("Assets/Prefab/p.prefab", UNRESOLVED, BASE["tex"])
        self.table["unresolved"] = set()
        self.refused("collision_unresolved_reference_is_baseline", self.check)


class RelabelTests(Fixture):
    def test_relabelled_stage_is_byte_identical_to_the_baseline(self):
        plan, gate = self.run_all()
        self.assertEqual(identitymap.expected_hashes(self.table).keys(), {
            p.relative_to(self.stage).as_posix() for p in self.stage.rglob("*") if p.is_file()})
        for path in self.baseline_dir.rglob("*"):
            if path.is_file():
                self.assertEqual(path.read_bytes(),
                                 (self.stage / path.relative_to(self.baseline_dir)).read_bytes(), path)
        self.assertEqual({"files": 13, "asset_guid_values": 1}, gate)

    def test_only_permitted_tokens_change(self):
        self.run_all()
        permitted = {FRESH[k]: BASE[k] for k in ("mat", "tex", "pre", "sce", "mix")}
        permitted.update(zip(MIXER_FRESH, MIXER_BASE))
        for path in self.fresh_dir.rglob("*"):
            if not path.is_file():
                continue
            before, after = path.read_bytes(), (self.stage / path.relative_to(self.fresh_dir)).read_bytes()
            for old, new in permitted.items():
                before = before.replace(old.encode(), new.encode())
            before = identitymap.TIME_CREATED.sub(b"timeCreated: 1111", before)
            self.assertEqual(before, after, path)
        self.assertEqual(b"\x89PNG\0\0" + b"\x00" * 8, (self.stage / "Assets/Texture/t.png").read_bytes())
        self.assertIn(ASSET_GUID, (self.stage / "Assets/Material/a.mat").read_text())

    def test_script_guid_and_unresolved_reference_are_left_alone(self):
        self.run_all()
        text = (self.stage / "Assets/Prefab/p.prefab").read_text()
        self.assertIn(f"guid: {BASE['scr']}, type: 3", text)
        self.assertIn(f"guid: {UNRESOLVED}, type: 3", text)

    def test_stage_must_not_exist_and_a_refusal_removes_it(self):
        plan = self.lookup()
        self.stage.mkdir()
        self.refused("stage_exists", lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table))
        shutil.rmtree(self.stage)
        self.table["mixer"]["Assets/Audio/Main.mixer"].append(g("9"))
        self.refused("mixer_count_mismatch", lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table))
        self.assertFalse(self.stage.exists())

    def relabel_mixer(self, fresh, base):
        self.set_mixer(fresh, base)
        plan = self.lookup()
        return lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)

    def test_mixer_positions_map_with_repeats_and_stable_values(self):
        fresh = [g("f"), g("9"), g("f"), ZERO, STABLE, g("9"), ZERO]
        base = [g("7"), g("8"), g("7"), ZERO, STABLE, g("8"), ZERO]
        self.set_mixer(fresh, base)
        plan, gate = self.run_all()
        self.assertEqual((self.baseline_dir / "Assets/Audio/Main.mixer").read_bytes(),
                         (self.stage / "Assets/Audio/Main.mixer").read_bytes())
        self.assertEqual({"files": 13, "asset_guid_values": 1}, gate)

    def test_mixer_token_count_must_equal_the_table(self):
        for label, fresh in {"extra": [g("f"), g("9"), g("e")], "fewer": [g("f")]}.items():
            with self.subTest(label):
                self.setUp()
                call = self.relabel_mixer([g("f"), g("9")], [g("7"), g("8")])  # the table has two positions
                write(self.fresh_dir, {"Assets/Audio/Main.mixer": mixer_file(fresh)})
                self.assertIn("table has 2", self.refused("mixer_count_mismatch", call))
                self.assertFalse(self.stage.exists())

    def test_mixer_one_fresh_value_must_not_map_to_two_baseline_values(self):
        call = self.relabel_mixer([g("f"), g("f")], [g("7"), g("8")])
        self.assertIn("position 1", self.refused("mixer_not_consistent", call))
        self.assertFalse(self.stage.exists())

    def test_mixer_two_fresh_values_must_not_map_to_one_baseline_value(self):
        call = self.relabel_mixer([g("f"), g("9")], [g("7"), g("7")])
        self.assertIn("position 1", self.refused("mixer_not_injective", call))
        self.assertFalse(self.stage.exists())

    def test_mixer_stable_value_may_not_stand_for_a_regenerated_one(self):
        # fresh 7 is kept (stable) at one position and replaced by 8 at another: not a function
        self.refused("mixer_not_consistent", self.relabel_mixer([g("7"), g("7")], [g("7"), g("8")]))
        # a regenerated value landing on a stable value's name is the same fault seen the other way
        self.refused("mixer_not_injective", self.relabel_mixer([g("7"), g("6")], [g("7"), g("7")]))

    def test_build_settings_scene_entry_is_relabelled(self):
        self.add_build_settings()
        plan, gate = self.run_all()
        self.assertEqual((self.baseline_dir / BUILD_SETTINGS).read_bytes(), (self.stage / BUILD_SETTINGS).read_bytes())
        self.assertIn(f"guid: {BASE['sce']}", (self.stage / BUILD_SETTINGS).read_text())
        self.assertEqual({"files": 14, "asset_guid_values": 1}, gate)

    def test_a_bare_guid_next_to_the_scene_entry_is_never_rewritten(self):
        self.add_build_settings()
        self.fresh_file(BUILD_SETTINGS, "  m_configObjects: {}",
                        f"  m_configObjects: {{}}\n  m_Other:\n    guid: {FRESH['mat']}")
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        text = (self.stage / BUILD_SETTINGS).read_text()
        self.assertIn(f"    guid: {BASE['sce']}\n", text)
        self.assertIn(f"  m_Other:\n    guid: {FRESH['mat']}", text)
        self.refused("hash_mismatch", lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))

    def test_asset_guid_values_are_never_rewritten_even_when_they_equal_a_fresh_guid(self):
        self.fresh_file("Assets/Material/a.mat", f"m_AssetGUID: {ASSET_GUID}", f"m_AssetGUID: {FRESH['tex']}")
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        text = (self.stage / "Assets/Material/a.mat").read_text()
        self.assertIn(f"m_AssetGUID: {FRESH['tex']}", text)  # untouched
        self.assertIn(f"guid: {BASE['tex']}, type: 3", text)  # the reference on the line above was relabelled

    def test_meta_that_no_row_owns_is_refused(self):
        plan = self.lookup()
        write(self.fresh_dir, {"Assets/Extra/z.cs.meta": "fileFormatVersion: 2\ntimeCreated: 5\n"})
        self.refused("meta_unowned", lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table))
        self.assertFalse(self.stage.exists())

    def test_meta_needs_exactly_one_time_created(self):
        plan = self.lookup()
        self.fresh_file("Assets/Material/a.mat.meta", "timeCreated: 2222", "timeCreated: 2222\ntimeCreated: 2223")
        self.refused("meta_time_created", lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table))

    def test_mixer_file_in_the_table_must_exist(self):
        self.table["mixer"]["Assets/Audio/Gone.mixer"] = [g("9")]
        plan = self.lookup()
        self.refused("mixer_file_missing", lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table))

    def test_meta_with_an_unknown_guid_is_refused(self):
        plan = self.lookup()
        self.fresh_file("Assets/Scripts/x.cs.meta", BASE["scr"], g("9"))  # changed after the lookup passed
        self.refused("meta_guid_unmapped", lambda: identitymap.relabel(self.fresh_dir, self.stage, plan, self.table))


class EndGateTests(Fixture):
    def test_hash_mismatch_is_reported_with_both_hashes(self):
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        (self.stage / "Assets/Prefab/p.prefab").write_text("tampered")
        message = self.refused("hash_mismatch",
                               lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))
        self.assertIn("Assets/Prefab/p.prefab", message)
        self.assertIn(identitymap.expected_hashes(self.table)["Assets/Prefab/p.prefab"], message)

    def test_content_mismatch_is_reported_before_a_missing_file(self):
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        (self.stage / "Assets/Material/a.mat.meta").unlink()
        (self.stage / "Assets/Texture/t.png").write_bytes(b"x")
        self.refused("hash_mismatch", lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))
        (self.stage / "Assets/Texture/t.png").write_bytes((self.baseline_dir / "Assets/Texture/t.png").read_bytes())
        self.refused("file_missing", lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))

    def test_extra_file_is_refused(self):
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        (self.stage / "Assets/extra.txt").write_text("x")
        self.refused("file_extra", lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))

    def test_leftover_fresh_guid_is_refused(self):
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        # Make the table agree with a file that still holds a fresh GUID, so only that check can fire.
        path = self.stage / "ProjectSettings/ProjectSettings.asset"
        path.write_text(f"settings: {FRESH['mat']}\n")
        self.table["files"]["ProjectSettings/ProjectSettings.asset"] = identitymap.sha256_bytes(path.read_bytes())
        self.refused("fresh_guid_left", lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))

    def test_changed_asset_guid_value_is_refused(self):
        plan = self.lookup()
        identitymap.relabel(self.fresh_dir, self.stage, plan, self.table)
        path = self.stage / "Assets/Material/a.mat"
        path.write_bytes(path.read_bytes().replace(ASSET_GUID.encode(), g("9").encode()))
        row = self.table["collections"][("cab-m", 7, 21)]
        row["sha256"] = [identitymap.sha256_bytes(path.read_bytes())]
        self.refused("asset_guid_changed",
                     lambda: identitymap.verify_output(self.fresh_dir, self.stage, plan, self.table))


class WholeTests(Fixture):
    def test_build_baseline_project_from_files(self):
        result = self.build()
        self.assertEqual({"files": 13, "asset_guid_values": 1}, result["gate"])
        self.assertEqual(exportidentity.check_identity(self.fresh_dir, self.records)["counts"], result["identity"])

    def build(self, header=(HEADER["input"], HEADER["assetripper"], HEADER["patch"])):
        (self.tmp / "export_identity.json").write_text(json.dumps({"format": 1, "collections": self.records}))
        (self.tmp / "table.txt").write_text(identitymap.format_table(self.table), encoding="utf-8")
        return identitymap.build_baseline_project(self.fresh_dir, self.tmp / "export_identity.json",
                                                  self.tmp / "table.txt", self.stage, [CATALOG], *header)

    def test_table_for_another_input_commit_or_patch_is_refused_before_anything_is_read(self):
        for index, other in enumerate(("f" * 64, "f" * 40, "f" * 64)):
            with self.subTest(("input", "assetripper", "patch")[index]):
                header = [HEADER["input"], HEADER["assetripper"], HEADER["patch"]]
                header[index] = other
                self.refused("table_header_mismatch", lambda: self.build(header))
                self.assertFalse(self.stage.exists())

    def test_a_refused_end_gate_leaves_no_stage(self):
        self.table["files"]["ProjectSettings/ProjectSettings.asset"] = "0" * 64
        self.refused("hash_mismatch", self.build)
        self.assertFalse(self.stage.exists())
        self.table["files"]["ProjectSettings/ProjectSettings.asset"] = identitymap.sha256_bytes(b"settings: 1\n")
        self.build()  # the same call succeeds once the table is right, so the stage was removed, not blocking
        self.assertTrue(self.stage.exists())


if __name__ == "__main__":
    unittest.main()

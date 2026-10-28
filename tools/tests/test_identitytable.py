"""Synthetic fixtures for the identity table builder; no AssetRipper output is read."""

import copy
import json
from pathlib import Path
import shutil
import struct
import tempfile
import unittest

import test_identitymap as fixtures
from tools import assetripper_identity_static as static
from tools.lucidlib import identitymap, identitytable, unityfs
from tools.lucidlib.identitymap import IdentityMapError


class BuilderTests(fixtures.Fixture):
    """Fixture: `baseline_dir` is the baseline, `fresh_dir` the witness with random GUIDs."""

    def build(self, records=None):
        return identitytable.build_table(self.fresh_dir, self.records if records is None else records,
                                         self.baseline_dir, fixtures.HEADER)

    def refused(self, kind, call=None):
        return super().refused(kind, call or self.build)

    def test_builds_exactly_the_table_written_out_by_hand(self):
        table, report = self.build()
        self.assertEqual(self.table, table)
        self.assertEqual(identitymap.format_table(self.table), identitymap.format_table(table))
        self.assertEqual({"AudioMixerExportCollection": 1, "MaterialExportCollection": 1, "PrefabExportCollection": 1,
                          "SceneAssetExportCollection": 1, "SceneExportCollection": 1, "ScriptExportCollection": 1,
                          "TextureExportCollection": 1}, report["collection_types"])
        self.assertEqual(["prefab_root"], report["anchor_rules"]["PrefabExportCollection"][:1])

    def test_table_is_independent_of_the_witness_guids_and_record_order(self):
        first = identitymap.format_table(self.build()[0])
        other = {"mat": fixtures.g("9"), "tex": fixtures.g("8"), "pre": fixtures.g("7"), "sce": fixtures.g("5"),
                 "mix": fixtures.g("4"), "scr": fixtures.BASE["scr"]}
        fixtures.write(self.fresh_dir, fixtures.project_files(other, fixtures.MIXER_FRESH, 3333))
        records = fixtures.collections(other)
        records.reverse()
        self.assertEqual(first, identitymap.format_table(self.build(records)[0]))

    def test_round_trip_reproduces_every_baseline_file(self):
        table, _ = self.build()
        text = identitymap.format_table(table)
        (self.tmp / "table.txt").write_text(text, encoding="utf-8")
        (self.tmp / "identity.json").write_text(json.dumps({"format": 1, "collections": self.records}))
        result = identitymap.build_baseline_project(
            self.fresh_dir, self.tmp / "identity.json", self.tmp / "table.txt", self.stage, [],
            fixtures.HEADER["input"], fixtures.HEADER["assetripper"], fixtures.HEADER["patch"])
        self.assertEqual(len(identitytable._files(self.baseline_dir)), result["gate"]["files"])
        self.assertEqual({"files": result["gate"]["files"]},
                         identitytable.compare_stage_with_baseline(self.stage, self.baseline_dir))

    def test_stage_that_differs_from_the_baseline_is_refused(self):
        shutil.copytree(self.baseline_dir, self.stage)
        (self.stage / "Assets/Material/a.mat").write_bytes(b"changed")
        self.refused("stage_differs_from_baseline", lambda: identitytable.compare_stage_with_baseline(
            self.stage, self.baseline_dir))
        (self.stage / "Assets/extra").write_bytes(b"")
        self.refused("stage_file_set_differs", lambda: identitytable.compare_stage_with_baseline(
            self.stage, self.baseline_dir))

    def test_unknown_collection_type_is_refused(self):
        self.records[0]["type"] = "NewExportCollection"
        self.refused("unknown_collection_type")

    def test_collection_missing_from_the_baseline_is_refused(self):
        (self.baseline_dir / "Assets/Texture/t.png.meta").unlink()
        self.refused("baseline_file_missing")

    def test_two_paths_of_one_collection_with_different_baseline_guids_are_refused(self):
        self.records[0]["paths"] = ["Assets/Material/a.mat", "Assets/Texture/t.png"]
        del self.records[1]  # the texture's file now belongs to the material's collection
        fixtures.write(self.fresh_dir, {"Assets/Texture/t.png.meta": fixtures.meta(fixtures.FRESH["mat"], 2222)})
        self.refused("baseline_guid_split")

    def test_duplicate_anchor_is_refused(self):
        self.records[1]["members"] = [list(self.records[0]["members"][0])]
        self.records[1]["members"][0][2] = 21
        # Same (file, PathID, ClassID) as the material; the texture file's anchors then differ from the record too,
        # so the record is refused already by validation: either refusal keeps a double claim out of the table.
        with self.assertRaises(IdentityMapError):
            self.build()

    def test_anchor_that_is_not_unique_is_refused(self):
        self.records[0]["members"].append(["cab-m", 8, 21, 2100001, True, True])
        with self.assertRaises(IdentityMapError):
            self.build()

    def test_mixer_values_must_map_consistently(self):
        fixtures.write(self.fresh_dir, {"Assets/Audio/Main.mixer": fixtures.mixer_file([fixtures.g("f"), fixtures.g("f")])})
        self.refused("mixer_not_consistent")

    def test_mixer_report_counts_stable_values(self):
        fixtures.write(self.fresh_dir, {"Assets/Audio/Main.mixer": fixtures.mixer_file([fixtures.g("f"), fixtures.g("8")])})
        _, report = self.build()
        mixer = report["mixer"]["Assets/Audio/Main.mixer"]
        self.assertEqual((2, 2, 2, 1, 1), (mixer["positions"], mixer["distinct_baseline_values"],
                                           mixer["distinct_witness_values"], mixer["positions_equal_to_baseline"],
                                           mixer["values_equal_to_baseline"]))

    def test_unresolved_references_are_the_ones_no_row_owns(self):
        table, _ = self.build()
        self.assertEqual({fixtures.UNRESOLVED}, table["unresolved"])

    def test_scene_list_guid_counts_as_a_reference(self):
        self.add_build_settings()
        fixtures.write(self.baseline_dir, {fixtures.BUILD_SETTINGS: fixtures.build_settings(fixtures.g("c"))})
        fixtures.write(self.fresh_dir, {fixtures.BUILD_SETTINGS: fixtures.build_settings(fixtures.g("c"))})
        self.assertEqual({fixtures.UNRESOLVED, fixtures.g("c")}, self.build()[0]["unresolved"])


class RealTypeTests(fixtures.Fixture):
    """Collection types of the 1.10.1 export that the first fixtures do not have."""

    LIGHTING = "Assets/Scenes/LightingData.asset"

    def lighting(self, scene_guid, project_ids):
        return fixtures.yaml((1120, 112000000, f"LightingDataAsset:\n  m_Scene: {{fileID: 102900000, guid: {scene_guid}, type: 3}}"))

    def add_lighting(self):
        """A LightingDataAsset collection (one generated member) in the baseline and the witness."""
        base_id, fresh_id = fixtures.g("1").replace("1", "2", 2), fixtures.g("a").replace("a", "b", 2)
        fixtures.write(self.baseline_dir, {self.LIGHTING: self.lighting(fixtures.BASE["sce"], None),
                                           self.LIGHTING + ".meta": fixtures.meta(base_id, 1111)})
        fixtures.write(self.fresh_dir, {self.LIGHTING: self.lighting(fixtures.FRESH["sce"], None),
                                        self.LIGHTING + ".meta": fixtures.meta(fresh_id, 2222)})
        self.records.append({"order": 9, "type": "AssetExportCollection`1", "guid": fresh_id, "paths": [self.LIGHTING],
                             "members": [["Generated Lighting Data Assets", 1, 1120, 112000000, False, True]]})
        self.table, _ = identitytable.build_table(self.fresh_dir, self.records, self.baseline_dir, fixtures.HEADER)
        return base_id, fresh_id

    def test_lighting_data_is_anchored_on_its_scene(self):
        base_id, fresh_id = self.add_lighting()
        self.assertEqual(base_id, self.table["collections"][("cab-s", 11, 1120)]["guid"])
        plan, _ = self.run_all()
        self.assertEqual(base_id, plan["guids"][fresh_id])

    def test_lighting_data_that_cannot_be_tied_to_one_scene_is_refused(self):
        self.add_lighting()
        scenes = identitymap.scene_anchors(self.records)
        record = self.records[-1]
        self.assertEqual(["cab-s", 11, 1120], identitymap.anchor(self.fresh_dir, record, scenes)[:3])
        self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record))  # no scenes known
        self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record, {}))  # scene not exported
        path = self.fresh_dir / self.LIGHTING
        path.write_bytes(path.read_bytes().replace(fixtures.FRESH["sce"].encode(), fixtures.g("9").encode()))
        self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record, scenes))  # unknown scene
        path.write_bytes(path.read_bytes() + path.read_bytes())  # two m_Scene lines
        self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record, scenes))

    def test_generated_member_is_refused_unless_it_is_a_lone_lighting_data_asset(self):
        self.add_lighting()
        scenes = identitymap.scene_anchors(self.records)
        for change in (lambda m: m[0].__setitem__(2, 114),                        # another class
                       lambda m: m.append(["cab-s", 5, 114, 5, True, False])):     # a second member
            record = copy.deepcopy(self.records[-1])
            change(record["members"])
            self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record, scenes))
        record = copy.deepcopy(self.records[0])
        record["type"] = "AssetExportCollection`1"
        record["members"][0][4] = False  # a material-like collection without an original member stays refused
        self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record, scenes))

    def test_unowned_collection_has_a_random_guid_that_must_not_reach_the_output(self):
        guid = "ab" * 16
        self.records.append({"order": 9, "type": "ManagerExportCollection", "guid": guid, "paths": [],
                             "members": [["globalgamemanagers", 1, 129, 1, True, True]]})
        plan, _ = self.run_all()
        self.assertEqual({guid}, plan["unowned"])
        self.assertNotIn(guid, plan["guids"])
        self.assertEqual(self.table, identitytable.build_table(self.fresh_dir, self.records, self.baseline_dir,
                                                               fixtures.HEADER)[0])
        shutil.rmtree(self.stage)
        self.fresh_file("Assets/Prefab/p.prefab", "m_Gone: {fileID: 5, guid: " + fixtures.UNRESOLVED,
                        "m_Gone: {fileID: 5, guid: " + guid)
        self.refused("hash_mismatch", self.run_all)  # it is not relabelled, so the file differs from the baseline

    def test_unowned_collection_that_owns_files_is_refused(self):
        self.records.append({"order": 9, "type": "EditorBuildSettingsExportCollection", "guid": fixtures.g("9"),
                             "paths": ["Assets/x"], "members": []})
        self.refused("identity_unexpected_files", self.lookup)

    def test_texture_rule_accepts_a_texture_or_a_cubemap_but_not_both(self):
        record = self.records[1]
        record["members"][0][2] = 89
        self.assertEqual(89, identitymap.anchor(self.fresh_dir, record)[2])
        record["members"].append(["cab-t", 4, 28, 2800001, True, True])
        self.refused("anchor_ambiguous", lambda: identitymap.anchor(self.fresh_dir, record))
        record["members"][:] = [["cab-t", 4, 21, 2100000, True, True]]
        self.refused("anchor_missing", lambda: identitymap.anchor(self.fresh_dir, record))

    def test_font_anchor_is_the_font_member(self):
        record = {"order": 9, "type": "FontAssetExportCollection", "guid": fixtures.g("9"), "paths": ["Assets/f.ttf"],
                  "members": [["resources.assets", 27, 21, 2100000, True, True], ["resources.assets", 52, 28, 2800000, True, True],
                              ["resources.assets", 102, 128, 12800000, True, True]]}
        self.assertEqual(["resources.assets", 102, 128], identitymap.anchor(self.fresh_dir, record)[:3])

    def test_every_rule_names_a_known_kind(self):
        for name, rule in identitymap.ANCHOR_RULES.items():
            self.assertIn(rule[0], {"single", "asset", "class", "classes", "first", "prefab_root", "script", "none",
                                    "unowned"}, name)


class ClassTests(unittest.TestCase):
    """Order-witnessed classes: collections no content or reference graph can tell apart."""

    def project(self, twins_referenced_differently=False):
        root = Path(tempfile.mkdtemp(prefix="test-identitytable-"))
        self.addCleanup(shutil.rmtree, root, ignore_errors=True)
        files, table = {}, {"header": dict(fixtures.HEADER), "collections": {}, "scripts": {}, "mixer": {},
                            "witnessed": [], "unresolved": set(), "files": {}}
        entries = []

        def add(name, guid, body, key):
            files[f"Assets/{name}.asset"] = body
            files[f"Assets/{name}.asset.meta"] = fixtures.meta(guid, 5)
            table["collections"][key] = {"guid": guid, "digest": "d" * 64, "paths": [f"Assets/{name}.asset"],
                                         "time_created": [5], "sha256": ["e" * 64], "meta_sha256": ["f" * 64]}
            entries.append({"paths": [f"Assets/{name}.asset"], "type": "AssetExportCollection"})

        def doc(extra=""):
            return fixtures.yaml((114, 11400000, "m_Name: n\n" + extra))

        twin_a, twin_b, solo = fixtures.g("a"), fixtures.g("b"), fixtures.g("c")
        add("twin_a", twin_a, doc(), ("cab-1", 1, 114))
        add("twin_b", twin_b, doc(), ("cab-2", 1, 114))
        add("solo", solo, doc("m_Other: 1\n"), ("cab-3", 1, 114))
        parent_a, parent_b = fixtures.g("d"), fixtures.g("e")
        ref = lambda guid: f"m_Ref: {{fileID: 11400000, guid: {guid}, type: 2}}\n"
        add("parent_a", parent_a, doc(ref(twin_a)), ("cab-4", 1, 114))
        add("parent_b", parent_b, doc(ref(twin_b) if not twins_referenced_differently else ref(twin_b) + ref(solo)),
            ("cab-5", 1, 114))
        fixtures.write(root, files)
        return root, table, entries

    def classes(self, **kwargs):
        root, table, entries = self.project(**kwargs)
        return identitytable.indistinguishable_classes(root, table, entries)

    def test_twins_and_their_parents_are_the_classes(self):
        result = self.classes()
        self.assertEqual([[fixtures.g("a"), fixtures.g("b")], [fixtures.g("d"), fixtures.g("e")]], result["classes"])
        self.assertEqual({"collections": 5, "classes": 2, "members": 4, "class_sizes": {2: 2}},
                         {key: value for key, value in result["report"].items() if key != "rounds"})

    def test_a_different_referrer_tells_the_twins_apart(self):
        # parent_b also references the solo asset, so the parents differ, and so (by their in-edges) do the twins.
        self.assertEqual([], self.classes(twins_referenced_differently=True)["classes"])

    def test_classes_ignore_names_pathids_and_serialized_files(self):
        root, table, entries = self.project()
        keys = list(table["collections"])
        self.assertEqual(len(keys), len({key[0] for key in keys}))  # every collection is in its own serialized file
        self.assertTrue(identitytable.indistinguishable_classes(root, table, entries)["classes"])


class UnityFsTests(unittest.TestCase):
    """A hand-built SerializedFile (version 22) inside an uncompressed UnityFS bundle."""

    TYPES = (1, 4, 114)

    def serialized(self, objects, tree=False):
        meta = b"2022.3.54f1\0" + struct.pack("<iB", 5, int(tree)) + struct.pack("<i", len(self.TYPES))
        for class_id in self.TYPES:
            meta += struct.pack("<iBh", class_id, 0, -1) + (b"\0" * 16 if class_id == 114 else b"") + b"\0" * 16
            if tree:
                meta += struct.pack("<ii", 1, 4) + b"\0" * 32 + b"abcd" + struct.pack("<i", 0)  # nodes, strings, dependencies
        meta += struct.pack("<i", len(objects))
        for path_id, type_index in objects:
            meta += b"\0" * (-(48 + len(meta)) % 4) + struct.pack("<qQIi", path_id, 0, 8, type_index)
        header = struct.pack(">IIII", 0, 0, 22, 0) + b"\0\0\0\0" + struct.pack(">IQQQ", len(meta), 0, 0, 0)
        return header + meta

    def bundle(self, path, nodes, compressed=False):
        data = b"".join(blob for _, blob in nodes)
        block_info = b"\0" * 16 + struct.pack(">i", 1) + struct.pack(">IIH", len(data), len(data), 0)
        offset, entries = 0, b""
        for name, blob in nodes:
            entries += struct.pack(">qqI", offset, len(blob), 4) + name.encode() + b"\0"
            offset += len(blob)
        block_info += struct.pack(">i", len(nodes)) + entries
        head = b"UnityFS\0" + struct.pack(">I", 8) + b"5.x.x\0" + b"2022.3.54f1\0"
        size = len(head) + 20
        padded = (size + 15) & ~15
        total = padded + len(block_info) + len(data)
        path.write_bytes(head + struct.pack(">QIII", total, len(block_info), len(block_info), 0)
                         + b"\0" * (padded - size) + block_info + data)

    def test_object_table_of_a_serialized_file(self):
        for tree in (False, True):
            blob = self.serialized([(5, 0), (6, 2), (7, 1)], tree)
            self.assertEqual({5: 1, 6: 114, 7: 4}, unityfs.object_table(lambda o, n: blob[o:o + n]))

    def test_duplicate_path_ids_and_unknown_versions_are_refused(self):
        blob = self.serialized([(5, 0), (5, 1)])
        with self.assertRaises(unityfs.UnityFsError):
            unityfs.object_table(lambda o, n: blob[o:o + n])
        blob = bytearray(self.serialized([(5, 0)]))
        blob[8:12] = struct.pack(">I", 99)
        with self.assertRaises(unityfs.UnityFsError):
            unityfs.object_table(lambda o, n: bytes(blob[o:o + n]))

    def test_bundle_nodes_and_reads(self):
        tmp = Path(tempfile.mkdtemp(prefix="test-unityfs-"))
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        blob = self.serialized([(5, 0)])
        self.bundle(tmp / "a.bundle", [("CAB-aa", blob), ("CAB-aa.resS", b"xyz")])
        with unityfs.Bundle(tmp / "a.bundle") as opened:
            self.assertEqual({"CAB-aa": (0, len(blob)), "CAB-aa.resS": (len(blob), 3)}, opened.nodes)
            self.assertEqual({5: 1}, unityfs.object_table(lambda o, n: opened.read(o, n)))
            self.assertEqual(b"xyz", opened.read(len(blob), 3))
            with self.assertRaises(unityfs.UnityFsError):
                opened.read(len(blob), 4)
        (tmp / "b.bundle").write_bytes(b"UnityWeb\0")
        with self.assertRaises(unityfs.UnityFsError):
            unityfs.Bundle(tmp / "b.bundle")

    def app(self, tmp):
        data = tmp / "Contents/Resources/Data"
        (data / "StreamingAssets/aa/StandaloneOSX").mkdir(parents=True)
        self.bundle(data / "StreamingAssets/aa/StandaloneOSX/s.bundle",
                    [("CAB-0a0a", self.serialized([(10, 0), (11, 2)])), ("CAB-0a0a.resS", b"")])
        (data / "level0").write_bytes(self.serialized([(1, 1)]))
        return tmp

    def scene(self, members):
        return [{"order": 0, "type": "SceneExportCollection", "guid": None, "paths": [], "members": members},
                {"order": 1, "type": "MaterialExportCollection", "guid": None, "paths": [],
                 "members": [["cab-missing", 1, 21, 1, True, True]]}]

    def test_scene_members_must_be_in_the_object_table_of_their_file(self):
        tmp = Path(tempfile.mkdtemp(prefix="test-unityfs-app-"))
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        app = self.app(tmp)
        good = static.check_scenes(app, self.scene([["cab-0a0a", 10, 1, 10, True, True], ["cab-0a0a", 11, 114, 11, True, True],
                                                     ["level0", 1, 4, 1, True, True], ["Generated", 9, 1032, None, False, False]]))
        self.assertTrue(good["ok"], good)
        self.assertEqual({"members_checked": 3, "collections": 1, "serialized_files": 2}, good["counts"])
        self.assertEqual({"bundle_node": 1, "plain_file": 1}, good["files_read_from"])
        bad = static.check_scenes(app, self.scene([["cab-0a0a", 12, 1, 12, True, True]]))
        self.assertFalse(bad["ok"])
        self.assertIn("not in the object table", bad["failures"][0])
        bad = static.check_scenes(app, self.scene([["cab-0a0a", 10, 114, 10, True, True]]))
        self.assertIn("has ClassID 1", bad["failures"][0])
        with self.assertRaises(unityfs.UnityFsError):
            static.check_scenes(app, self.scene([["cab-ffff", 1, 1, 1, True, True]]))

    def test_all_collections_are_checked_when_no_type_is_given(self):
        tmp = Path(tempfile.mkdtemp(prefix="test-unityfs-app-"))
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        app = self.app(tmp)
        material = {"order": 2, "type": "MaterialExportCollection", "guid": None, "paths": [],
                    "members": [["cab-0a0a", 11, 114, 11, True, True], ["Generated", 3, 21, 3, False, True]]}
        result = static.check_members(app, [material])
        self.assertTrue(result["ok"], result)
        self.assertEqual({"collections": 1, "members_checked": 1, "serialized_files": 1}, result["counts"])
        material["members"][0][2] = 4
        self.assertFalse(static.check_members(app, [material])["ok"])
        self.assertFalse(static.check_scenes(app, [material])["ok"])  # no scene collection at all is not a pass


if __name__ == "__main__":
    unittest.main()

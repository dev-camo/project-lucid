"""Synthetic fixtures for the export identity checker; no AssetRipper output is read."""

import json
from pathlib import Path
import shutil
import tempfile
import unittest

from tools.lucidlib import exportidentity

GUID = "0123456789abcdef0123456789abcdef"
OTHER = "fedcba9876543210fedcba9876543210"


def yaml(*anchors):
    return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "".join(
        f"--- !u!{class_id} &{file_id}\nObject:\n  m_Name: x\n" for class_id, file_id in anchors)


class ExportIdentityTests(unittest.TestCase):
    def setUp(self):
        self.fresh()

    def fresh(self):
        self.root = Path(tempfile.mkdtemp(prefix="test-exportidentity-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.write("Assets/Material/a.mat", yaml((21, 2100000)), GUID)
        self.write("Assets/Scenes/s.unity", yaml((1, 11), (4, 12)), OTHER)
        self.write("Assets/Texture/t.png", "PNG", "11111111111111111111111111111111")
        self.collections = [
            {"order": 0, "type": "MaterialExportCollection", "guid": GUID, "paths": ["Assets/Material/a.mat"],
             "members": [["cab-1", 7, 21, 2100000, True, True]]},
            {"order": 1, "type": "SceneExportCollection", "guid": OTHER, "paths": ["Assets/Scenes/s.unity"],
             "members": [["cab-2", 11, 1, 11, True, True], ["cab-2", 12, 4, 12, True, True]],},
            {"order": 2, "type": "TextureExportCollection", "guid": "11111111111111111111111111111111",
             "paths": ["Assets/Texture/t.png"], "members": [["cab-3", 1, 28, 2800000, True, True]]},
        ]

    def write(self, relative, text, guid):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)
        (self.root / (relative + ".meta")).write_text(f"fileFormatVersion: 2\nguid: {guid}\ntimeCreated: 1\n")

    def check(self):
        return exportidentity.check_identity(self.root, self.collections)

    def test_consistent_export_passes_and_counts_are_exact(self):
        result = self.check()
        self.assertTrue(result["ok"], result["failures"])
        self.assertEqual({"collections": 3, "members": 4, "metas": 3, "non_yaml_files": 1, "paths": 3,
                          "scene_serialized_members": 2, "yaml_anchors": 3, "yaml_files": 2}, result["counts"])

    def test_each_inconsistency_is_reported_under_its_own_kind(self):
        cases = {
            "anchors_differ_from_members": lambda: self.collections[0]["members"][0].__setitem__(3, 2100002),
            "scene_fileid_differs_from_pathid": lambda: self.collections[1]["members"][0].__setitem__(3, 5),
            "meta_guid_differs": lambda: self.collections[0].__setitem__("guid", OTHER),
            "missing_file": lambda: (self.root / "Assets/Texture/t.png").unlink(),
            "missing_meta": lambda: (self.root / "Assets/Texture/t.png.meta").unlink(),
            "meta_not_claimed_exactly_once": lambda: self.collections.pop(),
        }
        for kind, change in cases.items():
            with self.subTest(kind):
                self.fresh()
                change()
                result = self.check()
                self.assertFalse(result["ok"])
                self.assertIn(kind, result["failures"])

    def test_generated_scene_members_are_exempt_from_the_pathid_rule(self):
        self.write("Assets/Scenes/s.unity", yaml((1, 11), (4, 12), (4, 5)), OTHER)
        self.collections[1]["members"].append(["Processed", 99, 4, 5, False, True])
        result = self.check()
        self.assertTrue(result["ok"], result["failures"])
        self.assertEqual(2, result["counts"]["scene_serialized_members"])

    def test_generated_wrapper_members_that_are_not_written_have_no_anchor(self):
        self.collections[0]["members"].append(["Generated Prefabs", 3238, 1001, 100100000, False, False])
        self.assertTrue(self.check()["ok"])
        self.collections[0]["members"][-1][5] = True
        self.assertIn("anchors_differ_from_members", self.check()["failures"])

    def test_script_collections_without_a_collection_guid_still_need_their_metas(self):
        self.collections[2]["guid"] = None
        result = self.check()
        self.assertTrue(result["ok"], result["failures"])
        self.assertEqual(1, result["counts"]["metas_with_unrecorded_guid"])

    def test_a_path_claimed_by_two_collections_is_rejected(self):
        self.collections.append(dict(self.collections[0], order=3))
        result = self.check()
        self.assertIn("path_claimed_twice", result["failures"])
        self.assertIn("meta_not_claimed_exactly_once", result["failures"])

    def test_unreadable_yaml_for_a_scene_or_prefab_is_a_failure_but_binary_exports_stay_exempt(self):
        # Codex finding: an empty or damaged scene/prefab skipped the anchor comparison and passed.
        (self.root / "Assets/Scenes/s.unity").write_text("")
        self.assertIn("yaml_unreadable", self.check()["failures"])
        self.fresh()
        self.write("Assets/P/p.prefab", "garbage", OTHER)
        self.collections[0].update(paths=["Assets/P/p.prefab"], type="PrefabExportCollection", guid=OTHER)
        self.collections[1]["paths"] = []
        self.assertIn("yaml_unreadable", self.check()["failures"])
        self.fresh()
        self.assertEqual(1, self.check()["counts"]["non_yaml_files"])

    def test_every_yaml_suffix_of_the_baseline_export_requires_yaml_but_binary_suffixes_stay_exempt(self):
        # Review: the baseline export's .asset/.anim/.mat/... files all start with %YAML; only they are required.
        self.write("Assets/A/a.asset", "", OTHER)
        self.collections[1].update(paths=["Assets/A/a.asset"], type="ScriptableObjectExportCollection")
        self.assertIn("yaml_unreadable", self.check()["failures"])
        self.fresh()
        (self.root / "Assets/Material/a.mat").write_text("damaged")
        self.assertIn("yaml_unreadable", self.check()["failures"])
        self.fresh()
        (self.root / "Assets/Texture/t.png").write_bytes(b"\x89PNG\r\n")
        result = self.check()
        self.assertNotIn("yaml_unreadable", result["failures"])
        self.assertEqual(1, result["counts"]["non_yaml_files"])

    def test_yaml_anchors_reads_stripped_documents_and_ignores_binary_files(self):
        path = self.root / "p.prefab"
        path.write_text("%YAML 1.1\n--- !u!1 &-5\nA:\n--- !u!4 &9 stripped\nB:\n")
        self.assertEqual([(1, -5), (4, 9)], exportidentity.yaml_anchors(path))
        self.assertIsNone(exportidentity.yaml_anchors(self.root / "Assets/Texture/t.png"))
        path.write_text("%YAML 1.1\n--- !u!1 &x\n")
        with self.assertRaises(exportidentity.IdentityError):
            exportidentity.yaml_anchors(path)

    def test_load_identity_validates_structure(self):
        path = self.root / "export_identity.json"
        path.write_text(json.dumps({"format": 1, "collections": self.collections}))
        self.assertEqual(self.collections, exportidentity.load_identity(path))
        bad = {"wrong format": {"format": 2, "collections": []},
               "duplicate order": {"format": 1, "collections": [self.collections[0], self.collections[0]]},
               "bad guid": {"format": 1, "collections": [dict(self.collections[0], guid="XYZ")]},
               "short member": {"format": 1, "collections": [dict(self.collections[0], members=[["a", 1, 2]])]},
               "five-field member": {"format": 1, "collections": [dict(self.collections[0], members=[["a", 1, 2, 3, True]])]},
               "bool pathid": {"format": 1, "collections": [dict(self.collections[0], members=[["a", True, 2, 3, True, True]])]}}
        for label, document in bad.items():
            with self.subTest(label):
                path.write_text(json.dumps(document))
                with self.assertRaises(exportidentity.IdentityError):
                    exportidentity.load_identity(path)


if __name__ == "__main__":
    unittest.main()

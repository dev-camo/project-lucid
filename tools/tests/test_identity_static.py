"""The static readers and checks of the input (class-142 containers, table anchors), on hand-built bundles."""

import contextlib
import io
from pathlib import Path
import shutil
import struct
import tempfile
import unittest
from unittest import mock

import test_identitytable as base
from tools import assetripper_identity_static as static
from tools.lucidlib import unityfs

TYPES = (1, 74, 114, 142)  # type index -> ClassID


def pstring(text: str) -> bytes:
    raw = text.encode()
    return struct.pack("<i", len(raw)) + raw + b"\0" * (-len(raw) % 4)


def asset_bundle(entries) -> bytes:
    """A class-142 object: m_Name, an empty preload table, and the m_Container (key, fileID, pathID) entries."""
    out = pstring("bundle") + struct.pack("<i", 0) + struct.pack("<i", len(entries))
    for key, file_id, path_id in entries:
        out += pstring(key) + struct.pack("<iiiq", 0, 1, file_id, path_id)
    return out


def serialized(objects) -> bytes:
    """Version 22 SerializedFile; objects are (pathID, type index, data)."""
    meta = b"2022.3.54f1\0" + struct.pack("<iB", 5, 0) + struct.pack("<i", len(TYPES))
    for class_id in TYPES:
        meta += struct.pack("<iBh", class_id, 0, -1) + (b"\0" * 16 if class_id == 114 else b"") + b"\0" * 16
    meta += struct.pack("<i", len(objects))
    data, table_at = b"", len(meta)
    starts = []
    for _, _, blob in objects:
        data += b"\0" * (-len(data) % 8)
        starts.append(len(data))
        data += blob
    table = b""
    for (path_id, type_index, blob), start in zip(objects, starts):
        table += b"\0" * (-(48 + len(meta) + len(table)) % 4) + struct.pack("<qQIi", path_id, start, len(blob), type_index)
    meta += table
    data_offset = (48 + len(meta) + 15) & ~15
    header = struct.pack(">IIII", 0, 0, 22, 0) + b"\0\0\0\0" + struct.pack(">IQQQ", len(meta), 0, data_offset, 0)
    return header + meta + b"\0" * (data_offset - 48 - len(meta)) + data


class Readers(unittest.TestCase):
    def test_asset_bundle_container_entries(self):
        blob = asset_bundle([("Assets/a.prefab", 0, 42), ("Assets/b.asset", 0, -7), ("scene", 0, 0)])
        self.assertEqual([("Assets/a.prefab", 0, 42), ("Assets/b.asset", 0, -7), ("scene", 0, 0)],
                         unityfs.asset_bundle_container(blob))

    def test_truncated_or_inconsistent_data_is_an_unityfs_error_not_a_struct_error(self):
        blob = asset_bundle([("Assets/a.prefab", 0, 42)])
        for cut in (3, 12, 20, len(blob) - 1):
            with self.assertRaises(unityfs.UnityFsError, msg=cut):
                unityfs.asset_bundle_container(blob[:cut])
        file = serialized([(1, 3, blob)])
        for cut in (10, 30, 60, len(file) // 3):
            with self.assertRaises(unityfs.UnityFsError, msg=cut):
                unityfs.objects(lambda o, n, cut=cut: file[o:min(o + n, cut)])

    def test_objects_have_absolute_offsets_and_sizes(self):
        blob = asset_bundle([("k", 0, 1)])
        file = serialized([(5, 0, b"12345678"), (9, 3, blob)])
        found = unityfs.objects(lambda o, n: file[o:o + n])
        self.assertEqual({5: 1, 9: 142}, {key: value[0] for key, value in found.items()})
        _, start, size = found[9]
        self.assertEqual(blob, file[start:start + size])
        self.assertEqual({5: 1, 9: 142}, unityfs.object_table(lambda o, n: file[o:o + n]))

    def test_a_truncated_bundle_header_is_an_unityfs_error(self):
        tmp = Path(tempfile.mkdtemp(prefix="test-unityfs-trunc-"))
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        (tmp / "t.bundle").write_bytes(b"UnityFS\0" + struct.pack(">I", 8) + b"5.x.x\0")
        with self.assertRaises(unityfs.UnityFsError):
            unityfs.Bundle(tmp / "t.bundle")

    def test_block_info_at_the_end_of_a_version_7_or_8_bundle_is_refused(self):
        tmp = Path(tempfile.mkdtemp(prefix="test-unityfs-end-"))
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        helper = base.UnityFsTests("test_bundle_nodes_and_reads")
        helper.bundle(tmp / "e.bundle", [("CAB-aa", serialized([(1, 0, b"x")]))])
        raw = bytearray((tmp / "e.bundle").read_bytes())
        flags_at = raw.index(b"\0", raw.index(b"\0", 12) + 1) + 1 + 16
        raw[flags_at:flags_at + 4] = struct.pack(">I", struct.unpack_from(">I", raw, flags_at)[0] | 0x80)
        (tmp / "e.bundle").write_bytes(raw)
        with self.assertRaises(unityfs.UnityFsError) as caught:
            unityfs.Bundle(tmp / "e.bundle")
        self.assertIn("not supported", str(caught.exception))


class App(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="test-identity-static-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.data = self.tmp / "app" / "Contents/Resources/Data"
        (self.data / "StreamingAssets/aa").mkdir(parents=True)
        self.helper = base.UnityFsTests("test_bundle_nodes_and_reads")
        self.app = self.tmp / "app"

    def bundle(self, name, node, objects, extra=()):
        self.helper.bundle(self.data / "StreamingAssets/aa" / name, [(node, serialized(objects))] + list(extra))

    def test_table_anchors_must_be_in_the_object_table_of_their_file(self):
        self.bundle("a.bundle", "CAB-0a0a", [(10, 0, b"x"), (11, 2, b"y")])
        (self.data / "level0").write_bytes(serialized([(1, 0, b"z")]))
        row = lambda *paths: {"paths": list(paths)}  # noqa: E731
        table = {"collections": {("cab-0a0a", 10, 1): row("Assets/a.prefab"), ("cab-0a0a", 11, 114): row("Assets/b.asset"),
                                 ("level0", 1, 1120): row("Assets/LightingData.asset"),
                                 ("level0", 1, 1): row("Assets/s.unity")}}
        good = static.check_table(self.app, table)
        self.assertTrue(good["ok"], good)
        self.assertEqual({"rows": 4, "scene_rows": 1, "scene_rows_found": 1, "scene_rows_found_in_plain_files": 1,
                          "lighting_rows": 1, "serialized_files": 2}, good["counts"])
        for key, why in ((("cab-0a0a", 12, 1), "absent"), (("cab-0a0a", 10, 114), "wrong class")):
            bad = static.check_table(self.app, {"collections": {key: row("Assets/x.asset")}})
            self.assertFalse(bad["ok"], why)
            self.assertEqual(1, bad["failure_count"], why)
        absent_scene = static.check_table(self.app, {"collections": {("level0", 2, 1120): row("Assets/LightingData.asset")}})
        self.assertFalse(absent_scene["ok"])  # a lighting row still needs its scene object to exist

    def test_a_name_that_leaves_the_data_directory_is_not_a_plain_file(self):
        (self.tmp / "outside").write_bytes(serialized([(1, 0, b"z")]))
        for name in ("../../../../outside", str(self.tmp / "outside")):
            with self.assertRaises(unityfs.UnityFsError, msg=name):
                static.object_table_of(name, self.data, {})
        (self.data / "level0").write_bytes(serialized([(1, 0, b"z")]))
        self.assertEqual({1: 1}, static.object_table_of("level0", self.data, {}))

    def containers(self):
        """cab1: a prefab and a timeline (class 114, not an anchor) with a clip; cab2: a prefab; scenes use PathID 0."""
        self.bundle("one.bundle", "CAB-0001", [
            (1, 3, asset_bundle([("Assets/P/a.prefab", 0, 10), ("Assets/P/t.playable", 0, 20), ("Assets/P/t.playable", 0, 21),
                                 ("Assets/P/missing.asset", 0, 99), ("Assets/P/ext.asset", 1, 5)])),
            (10, 0, b"p"), (20, 2, b"timeline"), (21, 1, b"clip")])
        self.bundle("two.bundle", "CAB-0002", [(1, 3, asset_bundle([("Assets/P/c.prefab", 0, 30)])), (30, 0, b"p")])
        self.bundle("scene.bundle", "CAB-0003", [(1, 3, asset_bundle([("scene_one", 0, 0)]))],
                    extra=[("CAB-0003.sharedAssets", serialized([(1, 0, b"s")]))])

    def test_bundle_containers_map_keys_to_objects_and_count_every_kind_of_entry(self):
        self.containers()
        by_key, counts, failures = static.bundle_containers(self.data)
        self.assertEqual([("CAB-0001", 20, 114), ("CAB-0001", 21, 74)], by_key["assets/p/t.playable"])
        self.assertEqual({"bundles": 3, "asset_bundle_objects": 3, "container_entries": 7, "container_entries_found": 4,
                          "container_entries_in_other_files": 1, "container_entries_null_elsewhere": 1}, dict(counts))
        self.assertEqual(1, len(failures))
        self.assertIn("PathID 99 is not in the object table", failures[0])

    def check(self, table_rows, identities):
        self.containers()
        (self.data / "StreamingAssets/aa/one.bundle").unlink()
        self.bundle("one.bundle", "CAB-0001", [
            (1, 3, asset_bundle([("Assets/P/a.prefab", 0, 10), ("Assets/P/t.playable", 0, 20), ("Assets/P/t.playable", 0, 21)])),
            (10, 0, b"p"), (20, 2, b"timeline"), (21, 1, b"clip")])
        asset_map = {"catalog_identities": [
            {"internal_id": key, "match": "exact_path", "ambiguous": False, "candidates": [{"path": path, "guid": "g"}]}
            for key, path in identities]}
        table = {"collections": {key: {"paths": paths} for key, paths in table_rows.items()}}
        return static.check_container(self.app, table, asset_map)

    ROWS = {("cab-0001", 10, 1): ["Assets/P/a.prefab"], ("cab-0001", 21, 74): ["Assets/P/clip.anim"],
            ("cab-0001", 5, 114): ["Assets/P/t.playable"], ("cab-0002", 30, 1): ["Assets/P/c.prefab"]}

    def test_container_keys_are_confirmed_by_the_table_or_the_groups_cab(self):
        result = self.check(self.ROWS, [("Assets/P/a.prefab", "Assets/P/a.prefab"), ("Assets/P/c.prefab", "Assets/P/c.prefab"),
                                        ("Assets/P/t.playable", "Assets/P/t.playable")])
        self.assertTrue(result["ok"], result)
        self.assertEqual({"confirmed": 2, "group_in_same_cab": 1}, {key: result["counts"][key] for key in (
            "confirmed", "group_in_same_cab")})
        self.assertEqual(3, result["counts"]["exact_path_identities"])

    def test_a_key_no_bundle_has_is_a_failure(self):
        result = self.check(self.ROWS, [("Assets/P/absent.prefab", "Assets/P/a.prefab")])
        self.assertFalse(result["ok"])
        self.assertEqual(1, result["counts"]["no_container_key"])

    def test_a_table_row_that_gives_the_object_another_path_is_a_failure(self):
        rows = dict(self.ROWS)
        rows[("cab-0001", 10, 1)] = ["Assets/P/a_0.prefab"]
        result = self.check(rows, [("Assets/P/a.prefab", "Assets/P/a.prefab")])
        self.assertFalse(result["ok"])
        self.assertEqual(1, result["counts"]["disagrees"])

    def test_a_timeline_whose_row_is_in_another_cab_is_a_failure_unless_it_is_a_documented_exception(self):
        rows = dict(self.ROWS)
        rows[("cab-0002", 5, 114)] = rows.pop(("cab-0001", 5, 114))
        self.assertFalse(self.check(rows, [("Assets/P/t.playable", "Assets/P/t.playable")])["ok"])
        with mock.patch.object(static, "BASELINE_NAMING_EXCEPTIONS", {"Assets/P/t.playable"}):
            result = self.check(rows, [("Assets/P/t.playable", "Assets/P/t.playable")])
        self.assertTrue(result["ok"], result)
        self.assertEqual(1, result["counts"]["baseline_naming_exception"])

    def test_an_identity_without_exactly_one_candidate_is_a_failure(self):
        self.containers()
        asset_map = {"catalog_identities": [{"internal_id": "Assets/P/a.prefab", "match": "exact_path", "ambiguous": True,
                                             "candidates": [{"path": "a", "guid": "g"}, {"path": "b", "guid": "h"}]}]}
        result = static.check_container(self.app, {"collections": {}}, asset_map)
        self.assertFalse(result["ok"])
        self.assertEqual(1, result["counts"]["not_one_candidate"])


if __name__ == "__main__":
    unittest.main()

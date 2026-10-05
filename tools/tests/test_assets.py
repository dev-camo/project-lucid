"""Safety and identity tests using tiny synthetic exports, not game assets."""

import json
import base64
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import sys
import struct
import hashlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lucidlib import assets


def write_asset(root, path, guid, contents="data"):
    target = root / "Assets" / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(contents)
    target.with_name(target.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n")


def unityfs_bundle(payload=b"synthetic data", version=8):
    header = b"UnityFS\0" + struct.pack(">I", version) + b"5.x.x\0" + b"2022.3.54f1\0"
    return header + struct.pack(">QIII", len(header) + 20 + len(payload), 1, 1, 579) + payload


def write_bundle(project, path, data=None, guid=None):
    target = project / "Assets/StreamingAssets" / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(unityfs_bundle() if data is None else data)
    if guid is not None:
        target.with_name(target.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\nDefaultImporter:\n  externalObjects: {}\n")
    return target


class AssetTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.work = self.root / "work"
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.work.mkdir()

    def tearDown(self):
        self.temporary.cleanup()

    def make_export(self):
        app = self.root / "Game.app"
        data = app / "Contents/Resources/Data"
        data.mkdir(parents=True, exist_ok=True)
        (data / "globalgamemanagers").write_bytes(b"Unity")
        project = self.work / "assets/runs/test/project"
        game_guid, scene_guid = "a" * 32, "b" * 32
        write_asset(project, "Scripts/Game/Foo.cs", game_guid, "class Foo {}")
        write_asset(project, "Scenes/demo.unity", scene_guid,
                    "%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + game_guid + ", type: 3}\n")
        streaming = project / "Assets/StreamingAssets"
        streaming.mkdir()
        (streaming / "FSM.json").write_text('{"FSMs": []}')
        mapping = assets.build_asset_map(project)
        map_path = project.parent / "asset-map.json"
        assets._json_write(map_path, mapping)
        assets._json_write(self.work / "assets/latest-assets.json",
                           {"status": "exported", "run_id": "test", "project_path": str(project), "asset_map_path": str(map_path),
                            "input_path": str(app), "input_fingerprint": assets.fingerprint_manifest(assets._manifest(app)),
                            "tool": assets._tool_lock(), "settings": assets.EXPORT_SETTINGS})
        return project

    def test_prepare_rejects_changed_input_without_replacing_assets(self):
        self.make_export()
        assets.prepare_assets(self.repo, self.work)
        marker = self.repo / "Assets/Recovered/keep.txt"
        marker.write_text("old")
        (self.root / "Game.app/Contents/Resources/Data/globalgamemanagers").write_bytes(b"Unita")
        with self.assertRaisesRegex(ValueError, "differs"):
            assets.prepare_assets(self.repo, self.work)
        self.assertEqual(marker.read_text(), "old")

    def test_prepare_rejects_different_tool_settings(self):
        self.make_export()
        pointer = self.work / "assets/latest-assets.json"
        report = json.loads(pointer.read_text())
        report["settings"] = dict(report["settings"], ShaderExportMode="Dummy")
        assets._json_write(pointer, report)
        with self.assertRaisesRegex(ValueError, "settings differ"):
            assets.prepare_assets(self.repo, self.work)
        self.assertFalse((self.repo / "Assets").exists())

    def test_prepare_normalizes_copied_pcm_but_preserves_raw_export(self):
        project = self.make_export()
        wav = project / "Assets/Audio/test.wav"
        wav.parent.mkdir()
        header = b"RIFF" + struct.pack("<I", 0) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, 8000, 16000, 2, 16) + b"data" + struct.pack("<I", 0)
        raw = header + b"\x01\x00\x02\x00"
        wav.write_bytes(raw)
        result = assets.prepare_assets(self.repo, self.work)
        prepared = (self.repo / "Assets/Recovered/Audio/test.wav").read_bytes()
        self.assertEqual(wav.read_bytes(), raw)
        self.assertEqual(prepared[44:], raw[44:])
        self.assertEqual(struct.unpack_from("<I", prepared, 40)[0], 4)
        self.assertEqual(result["audio_normalization"]["Recovered"]["repaired_pcm_headers"], 1)

    def test_prepare_bundle_bytes_export_guids_and_disabled_importer_shape(self):
        project = self.make_export()
        first = write_bundle(project, "aa/first/zone1.bundle", unityfs_bundle(b"first"), "1" * 32)
        second = write_bundle(project, "aa/second/zone1.BUNDLE", unityfs_bundle(b"second"), "2" * 32)
        raw = {path: path.read_bytes() for path in [first, second]}
        raw_metas = {path: path.with_name(path.name + ".meta").read_bytes() for path in [first, second]}
        input_before = assets.fingerprint_manifest(assets._manifest(self.root / "Game.app"))
        result = assets.prepare_assets(self.repo, self.work)
        normalization = result["streaming_bundle_normalization"]
        self.assertEqual(normalization["bundle_count"], 2)
        self.assertEqual(normalization["normalized_plugin_importers"], 2)
        self.assertEqual(normalization["preserved_export_guids"], 2)
        self.assertEqual(normalization["generated_guids"], 0)
        for path, expected_guid in [(first, "1" * 32), (second, "2" * 32)]:
            output = self.repo / path.relative_to(project)
            self.assertEqual(output.read_bytes(), raw[path])
            self.assertEqual(path.read_bytes(), raw[path])
            self.assertEqual(path.with_name(path.name + ".meta").read_bytes(), raw_metas[path])
            text = output.with_name(output.name + ".meta").read_text()
            self.assertEqual(assets.GUID.search(text)[1], expected_guid)
            # Hash of actual Unity 2022.3.54f1 API-produced all-disabled meta,
            # with its original probe GUID restored and trailing blanks removed.
            canonical = text.replace(expected_guid, "d8156e626c814a8fa2e6b2ddc6a7cc64")
            self.assertEqual(hashlib.sha256(canonical.encode()).hexdigest(),
                             "43e46dde270d69ca0f7e153e95cb2abc082317431108c560883b4fc757dda50f")
            self.assertNotIn("enabled: 1", text)
        self.assertEqual(assets.fingerprint_manifest(assets._manifest(self.root / "Game.app")), input_before)

    def test_prepare_missing_bundle_meta_is_deterministic_and_reuses_owned_guid(self):
        project = self.make_export()
        source = write_bundle(project, "aa/zone1.bundle")
        first = assets.prepare_assets(self.repo, self.work)
        output_meta = self.repo / "Assets/StreamingAssets/aa/zone1.bundle.meta"
        initial = output_meta.read_bytes()
        self.assertEqual(first["streaming_bundle_normalization"]["generated_guids"], 1)
        other_repo = self.root / "other-repo"
        other_repo.mkdir()
        assets.prepare_assets(other_repo, self.work)
        self.assertEqual((other_repo / "Assets/StreamingAssets/aa/zone1.bundle.meta").read_bytes(), initial)
        # Reuse a GUID Unity assigned before this normalizer existed.
        output_meta.write_text("fileFormatVersion: 2\nguid: " + "3" * 32 + "\nDefaultImporter:\n  externalObjects: {}\n")
        second = assets.prepare_assets(self.repo, self.work)
        self.assertEqual(assets.GUID.search(output_meta.read_text())[1], "3" * 32)
        self.assertEqual(second["streaming_bundle_normalization"]["preserved_prepared_guids"], 1)
        self.assertEqual(second["streaming_bundle_normalization"]["generated_guids"], 0)
        self.assertFalse(source.with_name(source.name + ".meta").exists())
        normalized_meta = output_meta.read_bytes()
        third = assets.prepare_assets(self.repo, self.work)
        self.assertEqual(third["streaming_bundle_normalization"]["preserved_prepared_guids"], 1)
        self.assertEqual(output_meta.read_bytes(), normalized_meta)
        self.assertEqual(third["streaming_bundle_normalization"]["rewritten_meta_files"], 1)

    def test_unsupported_bundle_headers_abort_staging_without_replacing_owned_assets(self):
        project = self.make_export()
        write_bundle(project, "aa/a.bundle", guid="1" * 32)
        assets.prepare_assets(self.repo, self.work)
        recovered = self.repo / "Assets/Recovered/keep.txt"
        recovered.write_bytes(b"previous recovered")
        streaming = self.repo / "Assets/StreamingAssets/keep.txt"
        streaming.write_bytes(b"previous streaming")
        previous_meta = (self.repo / "Assets/StreamingAssets/aa/a.bundle.meta").read_bytes()
        pointer = self.work / "assets/latest-prepare.json"
        previous_pointer = pointer.read_bytes()
        valid = unityfs_bundle()
        size_offset = valid.index(b"2022.3.54f1\0") + len(b"2022.3.54f1\0")
        bad_size = bytearray(valid)
        struct.pack_into(">Q", bad_size, size_offset, len(valid) + 1)
        bad_bounds = bytearray(valid)
        struct.pack_into(">I", bad_bounds, size_offset + 8, len(valid))
        cases = {"native": b"\xcf\xfa\xed\xfe" + bytes(64), "other-unity": b"UnityRaw\0" + bytes(64),
                 "future-version": unityfs_bundle(version=9), "truncated": b"UnityFS\0\0",
                 "truncated-header": valid[:size_offset + 19], "wrong-size": bytes(bad_size),
                 "bad-block-bounds": bytes(bad_bounds), "bad-string": b"UnityFS\0" + struct.pack(">I", 8) + b"x" * 500}
        for label, data in cases.items():
            with self.subTest(label=label):
                source = write_bundle(project, "aa/z.bundle", data)
                with self.assertRaises(ValueError):
                    assets.prepare_assets(self.repo, self.work)
                self.assertEqual(recovered.read_bytes(), b"previous recovered")
                self.assertEqual(streaming.read_bytes(), b"previous streaming")
                self.assertEqual((self.repo / "Assets/StreamingAssets/aa/a.bundle.meta").read_bytes(), previous_meta)
                self.assertEqual(pointer.read_bytes(), previous_pointer)
                self.assertEqual(source.read_bytes(), data)
                self.assertFalse(source.with_name(source.name + ".meta").exists())
                self.assertEqual(list((self.repo / "Assets").glob(".lucid-assets-*")), [])

    def test_bundle_metadata_conflicts_and_duplicate_guids_fail_before_promotion(self):
        project = self.make_export()
        source = write_bundle(project, "aa/a.bundle", guid="1" * 32)
        assets.prepare_assets(self.repo, self.work)
        output_meta = self.repo / "Assets/StreamingAssets/aa/a.bundle.meta"
        previous = output_meta.read_bytes()
        meta = source.with_name(source.name + ".meta")
        for value in ["fileFormatVersion: 2\nguid: " + "2" * 32 + "\n", "fileFormatVersion: 2\n",
                      "fileFormatVersion: 2\nguid: " + "0" * 32 + "\n",
                      "fileFormatVersion: 2\nguid: " + "1" * 32 + "\nguid: invalid\n",
                      "fileFormatVersion: 2\nguid: " + "1" * 32 + "\nguid: " + "1" * 32 + "\n"]:
            with self.subTest(meta=value):
                meta.write_text(value)
                with self.assertRaisesRegex(ValueError, "GUID"):
                    assets.prepare_assets(self.repo, self.work)
                self.assertEqual(output_meta.read_bytes(), previous)
        meta.write_text("fileFormatVersion: 2\nguid: " + "1" * 32 + "\n")
        write_bundle(project, "aa/b.bundle", guid="1" * 32)
        with self.assertRaisesRegex(ValueError, "Duplicate.*GUID"):
            assets.prepare_assets(self.repo, self.work)
        self.assertEqual(output_meta.read_bytes(), previous)
        self.assertFalse((self.repo / "Assets/StreamingAssets/aa/b.bundle").exists())

    def test_previous_bundle_meta_symlink_is_rejected_without_following_it(self):
        project = self.make_export()
        write_bundle(project, "aa/a.bundle")
        assets.prepare_assets(self.repo, self.work)
        outside = self.root / "outside.meta"
        outside.write_text("fileFormatVersion: 2\nguid: " + "4" * 32 + "\n")
        meta = self.repo / "Assets/StreamingAssets/aa/a.bundle.meta"
        meta.unlink()
        meta.symlink_to(outside)
        with self.assertRaisesRegex(ValueError, "symlink"):
            assets.prepare_assets(self.repo, self.work)
        self.assertTrue(meta.is_symlink())
        self.assertEqual(assets.GUID.search(outside.read_text())[1], "4" * 32)

    def test_fingerprint_ignores_input_location_and_detects_same_size_change(self):
        left, right = self.root / "left.app", self.root / "right.app"
        for app in [left, right]:
            (app / "Contents").mkdir(parents=True)
            (app / "Contents/data").write_bytes(b"data")
        first = assets.fingerprint_manifest(assets._manifest(left))
        self.assertEqual(first, assets.fingerprint_manifest(assets._manifest(right)))
        (right / "Contents/data").write_bytes(b"diff")
        self.assertNotEqual(first, assets.fingerprint_manifest(assets._manifest(right)))

    def test_map_preserves_guids_and_marks_ambiguous_source_matches(self):
        project = self.work / "export"
        write_asset(project, "Data/hero.prefab", "1" * 32)
        write_asset(project, "Data/hero.asset", "2" * 32)
        write_asset(project, "Scenes/demo.unity", "3" * 32)
        catalog = self.work / "catalog.json"
        catalog.write_text(json.dumps({"m_InternalIds": ["Assets/Data/hero.fbx", "demo", "missing", "local.bundle"]}))
        mapping = assets.build_asset_map(project, catalog)
        identities = mapping["catalog_identities"]
        self.assertEqual(len(identities), 3)
        self.assertEqual(identities[0]["match"], "same_stem")
        self.assertTrue(identities[0]["ambiguous"])
        self.assertEqual(identities[1]["candidates"][0]["guid"], "3" * 32)
        self.assertEqual(identities[2]["match"], "unmatched")

    def test_packed_catalog_retains_address_keys_and_dependencies(self):
        key_data = bytearray(struct.pack("<i", 2))
        offsets = []
        for key in ["demo", "original-asset-guid"]:
            offsets.append(len(key_data))
            raw = key.encode("ascii")
            key_data.extend(b"\0" + struct.pack("<i", len(raw)) + raw)
        buckets = struct.pack("<i", 2) + b"".join(struct.pack("<iii", offset, 1, 0) for offset in offsets)
        entries = struct.pack("<i7i", 1, 0, 0, -1, 0, -1, 0, 0)
        catalog = {"m_BucketDataString": base64.b64encode(buckets).decode(),
                   "m_KeyDataString": base64.b64encode(key_data).decode(),
                   "m_EntryDataString": base64.b64encode(entries).decode(),
                   "m_InternalIds": ["0#demo"], "m_InternalIdPrefixes": ["scene/"],
                   "m_ProviderIds": ["SceneProvider"], "m_resourceTypes": [{"m_ClassName": "SceneInstance"}]}
        locations = assets.decode_catalog_locations(catalog)
        self.assertEqual(locations[0]["keys"], ["demo", "original-asset-guid"])
        self.assertEqual(locations[0]["internal_id"], "scene/demo")
        self.assertEqual(locations[0]["dependency_location_indices"], [])
        catalog["m_EntryDataString"] = base64.b64encode(entries[:-1]).decode()
        with self.assertRaisesRegex(ValueError, "length"):
            assets.decode_catalog_locations(catalog)

    def test_nested_project_export_is_detected(self):
        stage = self.work / "stage"
        project = stage / "ExportedProject"
        write_asset(project, "example.asset", "1" * 32)
        mapping = assets.build_asset_map(stage)
        self.assertEqual(Path(mapping["project_path"]), project.resolve())

    def test_compiled_shader_and_its_meta_remain_quarantined(self):
        project = self.make_export()
        write_asset(project, "Shader/HL_Char.asset", "c" * 32,
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!48 &1\nShader:\n  m_Name: HL_Char\n")
        mapping = assets.build_asset_map(project)
        assets._json_write(project.parent / "asset-map.json", mapping)
        result = assets.prepare_assets(self.repo, self.work)
        self.assertFalse((self.repo / "Assets/Recovered/Shader/HL_Char.asset").exists())
        self.assertFalse((self.repo / "Assets/Recovered/Shader/HL_Char.asset.meta").exists())
        self.assertEqual(result["quarantined_shader_guids"], ["c" * 32])

    def test_prepare_excludes_code_preserves_scene_and_separates_streaming(self):
        self.make_export()
        result = assets.prepare_assets(self.repo, self.work)
        recovered = self.repo / "Assets/Recovered"
        self.assertTrue((recovered / "Scenes/demo.unity.meta").exists())
        self.assertFalse((recovered / "Scripts").exists())
        self.assertFalse((recovered / "StreamingAssets").exists())
        self.assertTrue((self.repo / "Assets/StreamingAssets/FSM.json").exists())
        self.assertEqual(result["quarantined_script_guids"], ["a" * 32])
        self.assertEqual(result["skipped_code_files"], 2)

    def test_prepare_rerun_retains_previous_owned_assets_in_cache(self):
        self.make_export()
        assets.prepare_assets(self.repo, self.work)
        old = self.repo / "Assets/Recovered/old.txt"
        old.write_text("previous")
        result = assets.prepare_assets(self.repo, self.work)
        self.assertFalse(old.exists())
        retained = [Path(path) for path in result["backup_paths"]]
        self.assertEqual(len(retained), 2)
        self.assertTrue(any((path / "old.txt").is_file() for path in retained))
        self.assertTrue(all(self.work.resolve() in path.parents for path in retained))

    def test_prepare_refuses_unowned_destination(self):
        self.make_export()
        destination = self.repo / "Assets/Recovered"
        destination.mkdir(parents=True)
        (destination / "manual.txt").write_text("keep")
        with self.assertRaisesRegex(ValueError, "unowned"):
            assets.prepare_assets(self.repo, self.work)
        self.assertEqual((destination / "manual.txt").read_text(), "keep")

    def test_symlink_in_export_is_rejected(self):
        project = self.make_export()
        (project / "Assets/outside").symlink_to(self.repo, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlink"):
            assets.prepare_assets(self.repo, self.work)

    def test_failed_second_swap_rolls_back_both_owned_directories(self):
        replacements = []
        for name in ["first", "second"]:
            old, new = self.root / name, self.root / ("new" + name)
            old.mkdir()
            new.mkdir()
            assets._json_write(old / assets.MARKER, {"owner": assets.OWNER})
            (old / "value").write_text("old")
            (new / "value").write_text("new")
            replacements.append((old, new))
        original = Path.rename

        def rename(path, destination):
            if path.name == "newsecond":
                raise OSError("synthetic swap failure")
            return original(path, destination)

        with mock.patch.object(Path, "rename", rename):
            with self.assertRaisesRegex(OSError, "synthetic"):
                assets._replace_owned_directories(replacements)
        self.assertEqual((self.root / "first/value").read_text(), "old")
        self.assertEqual((self.root / "second/value").read_text(), "old")

    def test_work_directory_cannot_overlap_input(self):
        app = self.root / "Game.app"
        app.mkdir()
        for work in [self.root, app, app / "cache"]:
            with self.assertRaisesRegex(ValueError, "separate"):
                assets._safe_work_dir(work, app)

    def test_failed_export_keeps_previous_success_pointer(self):
        app = self.root / "Game.app"
        data = app / "Contents/Resources/Data"
        data.mkdir(parents=True)
        (data / "globalgamemanagers").write_bytes(b"Unity")
        tool = self.work / "tools/assetripper/AssetRipper.GUI.Free"
        tool.parent.mkdir(parents=True)
        tool.write_text("synthetic")
        pointer = self.work / "assets/latest-assets.json"
        assets._json_write(pointer, {"status": "exported", "run_id": "previous"})
        console = mock.Mock()
        process = mock.Mock()
        with mock.patch.object(assets, "_start_ripper", return_value=(process, "http://127.0.0.1:1", console)), \
             mock.patch.object(assets, "_request", side_effect=RuntimeError("synthetic export failure")), \
             mock.patch.object(assets, "_stop_ripper"):
            with self.assertRaisesRegex(RuntimeError, "synthetic"):
                assets.extract_assets(app, self.work)
        self.assertEqual(json.loads(pointer.read_text())["run_id"], "previous")
        reports = list((self.work / "assets/runs").glob("*/report.json"))
        self.assertEqual(json.loads(reports[0].read_text())["status"], "failed")


if __name__ == "__main__":
    unittest.main()

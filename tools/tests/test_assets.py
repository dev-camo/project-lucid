"""Safety and identity tests using tiny synthetic exports, not game assets."""

import json
import base64
import contextlib
import errno
import io
import inspect
import os
from pathlib import Path
import shutil
import stat
import tempfile
import unittest
from unittest import mock
import sys
import struct
import hashlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lucidlib import assets, bootstrap
from lucidlib.bootstrap import executable


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
        self.root = Path(self.temporary.name).resolve()
        self.work = self.root / "work"
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.work.mkdir()
        self.cache_root = mock.patch.object(bootstrap, "CACHE_ROOT", self.work)
        self.cache_root.start()

    def tearDown(self):
        self.cache_root.stop()
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

    def test_prepare_copies_through_the_clone_helper_and_falls_back_to_copy2(self):
        project = self.make_export()
        (project / "Assets/Data").mkdir()
        payload = b"clone me " * 100
        source = project / "Assets/Data/blob.bin"
        source.write_bytes(payload)
        source.with_name("blob.bin.meta").write_text("fileFormatVersion: 2\nguid: " + "c" * 32 + "\n")
        os.utime(source, (1_000_000_000, 1_000_000_000))
        attempts = []
        def refuse(source_path, output):
            attempts.append(Path(source_path).name)
            return False
        with mock.patch.object(assets, "_clone_file", side_effect=refuse), \
             mock.patch.object(assets.shutil, "copy2", wraps=shutil.copy2) as fallback:
            assets.prepare_assets(self.repo, self.work)
        prepared = self.repo / "Assets/Recovered/Data/blob.bin"
        self.assertIn("blob.bin", attempts)
        self.assertGreaterEqual(fallback.call_count, len(attempts))
        self.assertEqual(payload, prepared.read_bytes())
        self.assertEqual(1_000_000_000, int(prepared.stat().st_mtime))

    def test_clone_failure_is_reported_not_raised(self):
        source = self.root / "source.bin"
        source.write_bytes(b"data")
        with mock.patch.object(assets.sys, "platform", "darwin"), \
             mock.patch.object(assets, "_libc", side_effect=OSError("no libc")):
            self.assertFalse(assets._clone_file(source, self.root / "out.bin"))
        self.assertFalse((self.root / "out.bin").exists())
        with mock.patch.object(assets.sys, "platform", "linux"):
            self.assertFalse(assets._clone_file(source, self.root / "out.bin"))

    @unittest.skipUnless(sys.platform == "darwin", "clonefile(2) is macOS only")
    def test_clone_file_produces_identical_independent_copy_on_darwin(self):
        source = self.root / "source.bin"
        source.write_bytes(bytes(range(256)) * 64)
        source.chmod(0o640)
        os.utime(source, (1_000_000_000, 1_000_000_000))
        output = self.root / "cloned.bin"
        if not assets._clone_file(source, output):
            self.skipTest("this volume does not support clonefile")
        self.assertEqual(source.read_bytes(), output.read_bytes())
        self.assertEqual(source.stat().st_mtime, output.stat().st_mtime)
        self.assertEqual(stat.S_IMODE(source.stat().st_mode), stat.S_IMODE(output.stat().st_mode))
        self.assertNotEqual(source.stat().st_ino, output.stat().st_ino)
        output.write_bytes(b"changed")
        self.assertEqual(bytes(range(256)) * 64, source.read_bytes())

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

    def test_serialized_data_under_plugins_survives_and_object_references_to_code_are_reported(self):
        # #572: MeshCombineStudio's InstantiatePrefab.prefab sits under an authored Plugins folder and ten zone scenes
        # reference it; it is data and keeps its GUID. A file inside a .bundle container stays quarantined, and a
        # non-script reference to quarantined code is reported.
        project = self.make_export()
        prefab_guid, native_guid, scene_guid = "d" * 32, "e" * 32, "f" * 32
        write_asset(project, "Plugins/External/MeshCombineStudio/Sources/InstantiatePrefab.prefab", prefab_guid,
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &1\nGameObject:\n  m_Name: InstantiatePrefab\n")
        write_asset(project, "Plugins/Native.bundle/Contents/Info.plist", native_guid, "<plist/>")
        write_asset(project, "Scenes/zone.unity", scene_guid,
                    "%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n  instantiatePrefab: {fileID: 1, guid: " + prefab_guid +
                    ", type: 3}\n  plugin: {fileID: 1, guid: " + native_guid + ", type: 3}\n")
        mapping = assets.build_asset_map(project)
        assets._json_write(project.parent / "asset-map.json", mapping)
        result = assets.prepare_assets(self.repo, self.work)
        recovered = self.repo / "Assets/Recovered"
        prefab = recovered / "Plugins/External/MeshCombineStudio/Sources/InstantiatePrefab.prefab"
        self.assertTrue(prefab.exists())
        self.assertIn("guid: " + prefab_guid, prefab.with_name(prefab.name + ".meta").read_text())
        self.assertFalse((recovered / "Plugins/Native.bundle").exists())
        self.assertEqual([native_guid], result["quarantined_object_reference_guids"])
        self.assertEqual(result["skipped_code_files"], 4)
        rows = {row["path"]: row for row in mapping["assets"]}
        self.assertFalse(rows["Assets/Plugins/External/MeshCombineStudio/Sources/InstantiatePrefab.prefab"]["code_quarantined"])
        self.assertTrue(rows["Assets/Plugins/Native.bundle/Contents/Info.plist"]["code_quarantined"])

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

    def test_prepare_rerun_with_backups_records_the_copied_file_count(self):
        # The scene, its meta and FSM.json are copied; the code pair is skipped.
        self.make_export()
        first = assets.prepare_assets(self.repo, self.work)
        self.assertEqual(first["backup_paths"], [])
        result = assets.prepare_assets(self.repo, self.work)
        self.assertEqual(len(result["backup_paths"]), 2)
        receipt = json.loads((self.work / "assets/latest-prepare.json").read_text())
        for recorded in (first, result, receipt):
            self.assertIs(type(recorded["copied_files"]), int)
            self.assertEqual(recorded["copied_files"], 3)

    def test_verified_script_binding_preserves_asset_identity_and_input_bytes(self):
        project = self.make_export()
        text = ("%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n"
                "  m_Script: {fileID: 11500000, guid: " + "a" * 32 + ", type: 3}\n"
                "  m_guid: " + "d" * 32 + "\n  m_Name: Starting point\n")
        write_asset(project, "Data/start.asset", "e" * 32, text)
        assets._json_write(project.parent / "asset-map.json", assets.build_asset_map(project))
        binding = {"exported_guid": "a" * 32, "exported_file_id": 11500000,
                   "target_guid": "c" * 32, "target_file_id": 11500000}
        proof = {"status": "verified_layout", "bindings": [binding], "references_modified": False}
        raw = project / "Assets/Data/start.asset"
        original_meta = raw.with_name(raw.name + ".meta").read_bytes()
        input_before = assets.fingerprint_manifest(assets._manifest(self.root / "Game.app"))
        with mock.patch("lucidlib.scriptbindings.resolve_bindings", return_value=proof):
            result = assets.prepare_assets(self.repo, self.work)
        output = self.repo / "Assets/Recovered/Data/start.asset"
        self.assertEqual(text.replace("guid: " + "a" * 32, "guid: " + "c" * 32), output.read_text())
        self.assertEqual(original_meta, output.with_name(output.name + ".meta").read_bytes())
        self.assertEqual(text, raw.read_text())
        self.assertEqual(input_before, assets.fingerprint_manifest(assets._manifest(self.root / "Game.app")))
        self.assertEqual([], result["quarantined_script_guids"])
        recorded = next(a for a in result["script_bindings"]["assets"] if a["path"] == "Assets/Data/start.asset")
        self.assertEqual("e" * 32, recorded["guid"])
        self.assertEqual(1, recorded["pointers_rewritten"])
        self.assertEqual(hashlib.sha256(text.encode()).hexdigest(), recorded["exported_sha256"])
        self.assertEqual(hashlib.sha256(output.read_bytes()).hexdigest(), recorded["prepared_sha256"])

        prior = output.read_bytes()
        pointer = self.work / "assets/latest-prepare.json"
        prior_pointer = pointer.read_bytes()
        for failure in ("metadata", "pointer"):
            with self.subTest(failure=failure):
                raw.write_text(text.replace("fileID: 11500000", "fileID: 1") if failure == "pointer" else text)
                raw.with_name(raw.name + ".meta").write_bytes(
                    original_meta.replace(b"e" * 32, b"f" * 32) if failure == "metadata" else original_meta)
                with mock.patch("lucidlib.scriptbindings.resolve_bindings", return_value=proof):
                    with self.assertRaisesRegex(ValueError, "metadata|pointer"):
                        assets.prepare_assets(self.repo, self.work)
                self.assertEqual(prior, output.read_bytes())
                self.assertEqual(prior_pointer, pointer.read_bytes())
                self.assertEqual(list((self.repo / "Assets").glob(".lucid-assets-*")), [])

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
                # Rollback moved to the single caller-owned scope, #479.
                plan = []
                try:
                    assets._replace_owned_directories(replacements, plan)
                except BaseException as error:
                    assets._rollback_swaps(plan, error)
                    raise
        self.assertEqual((self.root / "first/value").read_text(), "old")
        self.assertEqual((self.root / "second/value").read_text(), "old")

    def test_interruption_after_any_completed_swap_rename_restores_both_owned_directories(self):
        # Two existing destinations make four renames; interrupt right after each one (#460).
        for completed in range(1, 5):
            with self.subTest(completed=completed):
                root = self.root / ("interrupt-" + str(completed))
                replacements = []
                for name in ["Recovered", "StreamingAssets"]:
                    old, new = root / name, root / ("staged-" + name)
                    old.mkdir(parents=True)
                    new.mkdir()
                    assets._json_write(old / assets.MARKER, {"owner": assets.OWNER})
                    (old / "value").write_text("old")
                    (new / "value").write_text("new")
                    replacements.append((old, new))
                original, count = Path.rename, 0

                def rename(path, destination):
                    nonlocal count
                    result = original(path, destination)
                    count += 1
                    if count == completed:
                        raise KeyboardInterrupt("interrupted after rename " + str(completed))
                    return result

                with mock.patch.object(Path, "rename", rename), self.assertRaises(KeyboardInterrupt):
                    # Rollback moved to the single caller-owned scope, #479.
                    plan = []
                    try:
                        assets._replace_owned_directories(replacements, plan)
                    except BaseException as error:
                        assets._rollback_swaps(plan, error)
                        raise
                for name in ["Recovered", "StreamingAssets"]:
                    self.assertEqual("old", (root / name / "value").read_text())
                self.assertEqual([], [p.name for p in root.iterdir() if ".backup-" in p.name])

    def rollback_notes(self, error):
        """Notes the rollback attached, or printed where exception notes are unavailable."""
        return list(getattr(error, "__notes__", [])) or self.printed.getvalue().splitlines()

    def test_failed_first_install_removal_names_the_installed_tree_not_a_backup(self):
        destination, staged = self.root / "Recovered", self.root / "staged"
        staged.mkdir()
        original = Path.rename

        def rename(path, target):
            result = original(path, target)
            if path.name == "staged":
                raise KeyboardInterrupt("interrupted after install")
            return result

        self.printed = io.StringIO()
        with mock.patch.object(Path, "rename", rename), \
                mock.patch.object(assets.shutil, "rmtree", side_effect=OSError("synthetic removal failure")), \
                mock.patch("sys.stderr", self.printed), self.assertRaises(KeyboardInterrupt) as raised:
            # Rollback moved to the single caller-owned scope, #479.
            plan = []
            try:
                assets._replace_owned_directories([(destination, staged)], plan)
            except BaseException as error:
                assets._rollback_swaps(plan, error)
                raise
        self.assertEqual(["Could not remove the newly installed tree at {}: synthetic removal failure".format(
            destination)], self.rollback_notes(raised.exception))

    def test_failed_swap_restoration_keeps_the_original_error_and_names_the_retained_tree(self):
        old, new = self.root / "Recovered", self.root / "staged"
        old.mkdir()
        new.mkdir()
        assets._json_write(old / assets.MARKER, {"owner": assets.OWNER})
        original = Path.rename

        def rename(path, destination):
            if path.name == "staged":
                raise KeyboardInterrupt("interrupted swap")
            if ".backup-" in path.name:
                raise OSError("synthetic restore failure")
            return original(path, destination)

        self.printed = io.StringIO()
        with mock.patch.object(Path, "rename", rename), mock.patch("sys.stderr", self.printed), \
                self.assertRaises(KeyboardInterrupt) as raised:
            # Rollback moved to the single caller-owned scope, #479.
            plan = []
            try:
                assets._replace_owned_directories([(old, new)], plan)
            except BaseException as error:
                assets._rollback_swaps(plan, error)
                raise
        [backup] = [p for p in self.root.iterdir() if ".backup-" in p.name]
        self.assertEqual(["Could not restore {} (synthetic restore failure); its prior tree remains at {}".format(
            old, backup)], self.rollback_notes(raised.exception))

    def test_interrupted_reprepare_swap_keeps_prior_trees_and_receipt_in_agreement(self):
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        receipt = self.work / "assets/latest-prepare.json"
        prior_tree, prior_receipt = self.assets_snapshot(), receipt.read_bytes()
        self.change_export_scene(project)
        original = Path.rename
        # Recovered and StreamingAssets both exist, so the swap makes four renames into Assets.
        for completed in range(1, 5):
            with self.subTest(completed=completed):
                count = 0

                def rename(path, destination):
                    nonlocal count
                    result = original(path, destination)
                    if Path(destination).parent == self.repo / "Assets":
                        count += 1
                        if count == completed:
                            raise KeyboardInterrupt("interrupted after swap rename " + str(completed))
                    return result

                with mock.patch.object(Path, "rename", rename), self.assertRaises(KeyboardInterrupt):
                    assets.prepare_assets(self.repo, self.work)
                self.assertEqual(prior_tree, self.assets_snapshot())
                self.assertEqual(prior_receipt, receipt.read_bytes())

    def test_interrupt_after_the_swap_returns_restores_prior_trees_and_receipt(self):
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        receipt = self.work / "assets/latest-prepare.json"
        prior_tree, prior_receipt = self.assets_snapshot(), receipt.read_bytes()
        self.change_export_scene(project)
        original = assets._replace_owned_directories

        def replace_then_interrupt(*args, **kwargs):
            original(*args, **kwargs)
            raise KeyboardInterrupt("interrupted after both swaps")

        with mock.patch.object(assets, "_replace_owned_directories", replace_then_interrupt), \
                self.assertRaises(KeyboardInterrupt):
            assets.prepare_assets(self.repo, self.work)
        self.assertEqual(prior_tree, self.assets_snapshot())
        self.assertEqual(prior_receipt, receipt.read_bytes())
        self.assertEqual([], list((self.repo / "Assets").glob(".Recovered.backup-*")))
        self.assertEqual([], list((self.repo / "Assets").glob(".StreamingAssets.backup-*")))

    def test_interrupt_at_each_line_between_the_last_rename_and_publication_restores_prior_state(self):
        # Discover executable handoff boundaries rather than pinning source line numbers (#479).
        source, first_line = inspect.getsourcelines(assets.prepare_assets)
        swap_line = first_line + next(i for i, line in enumerate(source)
                                     if "_replace_owned_directories(replacements" in line)
        publication_line = first_line + next(i + 1 for i, line in enumerate(source) if "if locations:" in line)
        prepare_code, swap_code = assets.prepare_assets.__code__, assets._replace_owned_directories.__code__

        def handoff_event(frame, event):
            return ((frame.f_code is prepare_code and event == "line" and
                     swap_line <= frame.f_lineno <= publication_line) or
                    (frame.f_code is swap_code and event == "return"))

        events = []

        def discover(frame, event, arg):
            if handoff_event(frame, event):
                events.append((frame.f_code.co_name, event, frame.f_lineno))
            return discover

        self.make_export()
        assets.prepare_assets(self.repo, self.work)
        prior_trace = sys.gettrace()
        try:
            sys.settrace(discover)
            assets.prepare_assets(self.repo, self.work)
        finally:
            sys.settrace(prior_trace)
        self.assertGreaterEqual(len(events), 3)
        self.assertTrue(any(event == "return" for _, event, _ in events))

        for index, boundary in enumerate(events):
            with self.subTest(index=index, boundary=boundary), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary).resolve()
                with mock.patch.multiple(self, root=root, repo=root / "repo", work=root / "work"), \
                        mock.patch.object(bootstrap, "CACHE_ROOT", root / "work"):
                    self.repo.mkdir()
                    self.work.mkdir()
                    project = self.make_export()
                    assets.prepare_assets(self.repo, self.work)
                    receipt = self.work / "assets/latest-prepare.json"
                    prior_tree, prior_receipt = self.assets_snapshot(), receipt.read_bytes()
                    self.change_export_scene(project)
                    seen = []

                    def interrupt(frame, event, arg):
                        if handoff_event(frame, event):
                            seen.append((frame.f_code.co_name, event, frame.f_lineno))
                            if len(seen) == index + 1:
                                raise KeyboardInterrupt("interrupted at handoff event " + str(index))
                        return interrupt

                    prior_trace = sys.gettrace()
                    try:
                        sys.settrace(interrupt)
                        with self.assertRaises(KeyboardInterrupt):
                            assets.prepare_assets(self.repo, self.work)
                    finally:
                        sys.settrace(prior_trace)
                    self.assertEqual(boundary, seen[-1])
                    self.assertEqual(prior_tree, self.assets_snapshot())
                    self.assertEqual(prior_receipt, receipt.read_bytes())
                    self.assertEqual([], list((self.repo / "Assets").glob(".Recovered.backup-*")))
                    self.assertEqual([], list((self.repo / "Assets").glob(".StreamingAssets.backup-*")))

    def assets_snapshot(self):
        root = self.repo / "Assets"
        return {path.relative_to(root).as_posix(): path.read_bytes() if path.is_file() else None
                for path in sorted(root.rglob("*"))}

    def change_export_scene(self, project):
        scene = project / "Assets/Scenes/demo.unity"
        scene.write_text(scene.read_text() + "# second export\n")
        assets._json_write(project.parent / "asset-map.json", assets.build_asset_map(project))

    def cross_volume_work_cache(self):
        """Fail renames into or out of the work cache as a separate volume does."""
        original_rename = assets.os.rename
        work = str(self.work)

        def rename(source, target):
            if any(str(path).startswith(work + "/") for path in (source, target)):
                raise OSError(errno.EXDEV, "Cross-device link", str(source))
            return original_rename(source, target)
        return mock.patch.object(assets.os, "rename", rename)

    def test_failed_backup_retention_or_receipt_write_restores_prior_prepared_trees(self):
        project = self.make_export()
        maintained = self.repo / "Assets/Scripts/Maintained.cs"
        maintained.parent.mkdir(parents=True)
        maintained.write_text("class Maintained {}")
        assets.prepare_assets(self.repo, self.work)
        receipt = self.work / "assets/latest-prepare.json"
        backups_root = self.work / "assets/backups"
        prior_tree, prior_receipt = self.assets_snapshot(), receipt.read_bytes()
        self.change_export_scene(project)
        original_relocate, original_write = assets._relocate_tree, assets._json_write
        original_copytree, original_rmtree = assets.shutil.copytree, assets.shutil.rmtree

        def failing_retention(fail_at):
            # Count only moves of a prior tree into the retained backups directory.
            calls = []

            def relocate(source, target):
                if Path(target).parent.parent == backups_root:
                    calls.append(target)
                    if len(calls) == fail_at:
                        raise OSError("injected backup retention failure")
                return original_relocate(source, target)
            return mock.patch.object(assets, "_relocate_tree", relocate)

        def failing_receipt_write(path, value):
            if Path(path).name == "latest-prepare.json":
                raise OSError("injected receipt write failure")
            return original_write(path, value)

        def partial_copy():
            fired = []

            def copytree(source, target, *args, **options):
                # Nested directories recurse through the patched name with copy_once bound.
                if fired:
                    return original_copytree(source, target, *args, **options)
                fired.append(target)

                def copy_once(src, dst):
                    if Path(target).exists() and any(path.is_file() for path in Path(target).rglob("*")):
                        raise OSError("injected cross-volume copy failure")
                    return assets.shutil.copy2(src, dst)
                return original_copytree(source, target, copy_function=copy_once, **options)
            return mock.patch.object(assets.shutil, "copytree", copytree)

        def partial_source_delete():
            fired = []

            def rmtree(path, ignore_errors=False):
                path = Path(path)
                if not fired and path.parent == self.repo / "Assets" and ".backup-" in path.name:
                    fired.append(path)
                    next(item for item in path.rglob("*") if item.is_file()).unlink()
                    raise OSError("injected cross-volume source delete failure")
                return original_rmtree(path, ignore_errors=ignore_errors)
            return mock.patch.object(assets.shutil, "rmtree", rmtree)

        faults = {"first backup move": (failing_retention(1),), "second backup move": (failing_retention(2),),
                  "receipt write": (mock.patch.object(assets, "_json_write", failing_receipt_write),),
                  "cross-volume partial copy": (self.cross_volume_work_cache(), partial_copy()),
                  "cross-volume partial source delete": (self.cross_volume_work_cache(), partial_source_delete())}
        for name, patches in faults.items():
            with self.subTest(fault=name):
                with contextlib.ExitStack() as stack:
                    for patch in patches:
                        stack.enter_context(patch)
                    with self.assertRaisesRegex(OSError, "injected"):
                        assets.prepare_assets(self.repo, self.work)
                # Snapshot equality also rules out staging, backup and set-aside siblings.
                self.assertEqual(prior_tree, self.assets_snapshot())
                self.assertEqual(prior_receipt, receipt.read_bytes())
                self.assertEqual([], list(backups_root.iterdir()) if backups_root.exists() else [])

        with self.cross_volume_work_cache():
            result = assets.prepare_assets(self.repo, self.work)
        self.assertIn("# second export", (self.repo / "Assets/Recovered/Scenes/demo.unity").read_text())
        recorded = json.loads(receipt.read_text())
        self.assertEqual(result, recorded)
        self.assertEqual("test", recorded["run_id"])
        retained = sorted(path for directory in backups_root.iterdir() for path in directory.iterdir())
        self.assertEqual(sorted(str(path) for path in retained), sorted(recorded["backup_paths"]))
        by_name = {path.name.split(".backup-")[0].lstrip("."): path for path in retained}
        self.assertEqual({"Recovered", "StreamingAssets"}, set(by_name))
        self.assertEqual(prior_tree["Recovered/Scenes/demo.unity"],
                         (by_name["Recovered"] / "Scenes/demo.unity").read_bytes())
        self.assertEqual(prior_tree["StreamingAssets/FSM.json"], (by_name["StreamingAssets"] / "FSM.json").read_bytes())
        self.assertEqual([], [path for path in (self.repo / "Assets").iterdir() if path.name.startswith(".")])
        self.assertEqual("class Maintained {}", maintained.read_text())

    def failed_restore_fixture(self, restore_error):
        """Prepare twice; the second receipt write fails and so does restoring into Assets."""
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        self.change_export_scene(project)
        original_relocate, original_write = assets._relocate_tree, assets._json_write

        def failing_receipt_write(path, value):
            if Path(path).name == "latest-prepare.json":
                raise OSError("injected receipt write failure")
            return original_write(path, value)

        def relocate(source, target):
            if Path(target).parent == self.repo / "Assets":
                raise restore_error
            return original_relocate(source, target)

        stack = contextlib.ExitStack()
        stack.enter_context(mock.patch.object(assets, "_json_write", failing_receipt_write))
        stack.enter_context(mock.patch.object(assets, "_relocate_tree", relocate))
        return stack

    def assert_failed_restore_kept_new_trees_and_backups(self, message):
        active = self.repo / "Assets"
        # Restore runs in reverse; StreamingAssets fails first and keeps its new tree, Recovered is never reached.
        self.assertIn("# second export", (active / "Recovered/Scenes/demo.unity").read_text())
        self.assertEqual('{"FSMs": []}', (active / "StreamingAssets/FSM.json").read_text())
        self.assertTrue((active / "StreamingAssets" / assets.MARKER).is_file())
        self.assertEqual([], [path.name for path in active.iterdir() if path.name.startswith(".")])
        self.assertIn(f"newly prepared tree: {active / 'StreamingAssets'};", message)
        retained = [path for directory in (self.work / "assets/backups").iterdir() for path in directory.iterdir()]
        self.assertEqual(2, len(retained))
        self.assertTrue(all(str(path) in message for path in retained))
        self.assertTrue(all((path / assets.MARKER).is_file() for path in retained))
        self.assertNotIn("# second export", next(path for path in retained if "Recovered" in path.name)
                         .joinpath("Scenes/demo.unity").read_text())

    def test_failed_restore_after_receipt_failure_retains_prior_copies_and_names_them(self):
        with self.failed_restore_fixture(OSError("injected restore failure")):
            with self.assertRaisesRegex(RuntimeError, "retained prior copies") as caught:
                assets.prepare_assets(self.repo, self.work)
        self.assertRegex(str(caught.exception.__cause__), "injected restore failure")
        self.assertRegex(str(caught.exception.__cause__.__context__), "injected receipt write failure")
        self.assert_failed_restore_kept_new_trees_and_backups(str(caught.exception))

    def test_interrupted_restore_propagates_the_interrupt_after_reporting_retained_copies(self):
        stderr = io.StringIO()
        with self.failed_restore_fixture(KeyboardInterrupt()), mock.patch.object(assets.sys, "stderr", stderr):
            with self.assertRaises(KeyboardInterrupt) as caught:
                assets.prepare_assets(self.repo, self.work)
        self.assertRegex(str(caught.exception.__context__), "injected receipt write failure")
        self.assertIn("retained prior copies", stderr.getvalue())
        self.assert_failed_restore_kept_new_trees_and_backups(stderr.getvalue())

    def test_interrupt_after_the_receipt_rename_restores_the_prior_receipt_or_its_absence(self):
        # #297: os.replace has published the new receipt when the interrupt lands.
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        receipt = self.work / "assets/latest-prepare.json"
        original_replace = assets.os.replace

        def interrupted_publication():
            fired = []

            def replace(source, target):
                result = original_replace(source, target)
                if not fired and Path(target).name == "latest-prepare.json":
                    fired.append(target)
                    raise KeyboardInterrupt()
                return result
            return mock.patch.object(assets.os, "replace", replace)

        for prior_absent in (False, True):
            with self.subTest(prior_absent=prior_absent):
                if prior_absent:
                    receipt.unlink()
                prior_tree = self.assets_snapshot()
                prior_receipt = None if prior_absent else receipt.read_bytes()
                self.change_export_scene(project)
                with interrupted_publication(), self.assertRaises(KeyboardInterrupt):
                    assets.prepare_assets(self.repo, self.work)
                self.assertEqual(prior_tree, self.assets_snapshot())
                self.assertEqual(prior_receipt, receipt.read_bytes() if receipt.exists() else None)
                self.assertEqual([], [path.name for path in receipt.parent.iterdir() if path.name.endswith(".tmp")])

    def test_unremovable_partial_cross_volume_restore_is_set_aside_and_the_new_tree_stays_live(self):
        # #297: copying a prior tree back from a separate volume fails and its
        # partial target survives the best-effort removal.
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        self.change_export_scene(project)
        active = self.repo / "Assets"
        original_write, original_copytree, original_rmtree = assets._json_write, assets.shutil.copytree, assets.shutil.rmtree

        def failing_receipt_write(path, value):
            if Path(path).name == "latest-prepare.json":
                raise OSError("injected receipt write failure")
            return original_write(path, value)

        def copytree(source, target, *args, **options):
            if Path(target).parent == active:
                Path(target).mkdir()
                (Path(target) / "partial").write_text("partial")
                raise OSError("injected restore copy failure")
            return original_copytree(source, target, *args, **options)

        def rmtree(path, ignore_errors=False, **options):
            if Path(path).parent == active and (Path(path) / "partial").exists() and ignore_errors:
                return None  # the best-effort removal leaves the partial tree
            return original_rmtree(path, ignore_errors=ignore_errors, **options)

        with self.cross_volume_work_cache(), \
                mock.patch.object(assets, "_json_write", failing_receipt_write), \
                mock.patch.object(assets.shutil, "copytree", copytree), \
                mock.patch.object(assets.shutil, "rmtree", rmtree), \
                self.assertRaisesRegex(RuntimeError, "Could not restore") as caught:
            assets.prepare_assets(self.repo, self.work)
        message = str(caught.exception)
        # Restore runs in reverse, so StreamingAssets fails first: its live path
        # holds the complete new tree again, never the partial prior copy.
        streaming = active / "StreamingAssets"
        self.assertFalse((streaming / "partial").exists())
        self.assertTrue((streaming / assets.MARKER).is_file())
        self.assertEqual('{"FSMs": []}', (streaming / "FSM.json").read_text())
        self.assertIn(f"newly prepared tree: {streaming};", message)
        partial = list(active.glob(".StreamingAssets.failed-partial-*"))
        self.assertEqual(1, len(partial))
        self.assertEqual("partial", (partial[0] / "partial").read_text())
        self.assertIn(str(partial[0]), message)
        retained = [path for directory in (self.work / "assets/backups").iterdir() for path in directory.iterdir()]
        self.assertEqual(2, len(retained))
        self.assertTrue(all(str(path) in message for path in retained))

    def test_interrupt_after_a_retention_rename_restores_from_the_retained_copy(self):
        # #297: the prior tree has moved into the backups directory when the
        # interrupt lands, so the rollback must follow it there.
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        receipt = self.work / "assets/latest-prepare.json"
        backups_root = self.work / "assets/backups"
        prior_tree, prior_receipt = self.assets_snapshot(), receipt.read_bytes()
        self.change_export_scene(project)
        original_relocate = assets._relocate_tree
        moved = []

        def relocate(source, target):
            result = original_relocate(source, target)
            if not moved and Path(target).parent.parent == backups_root:
                moved.append(target)
                raise KeyboardInterrupt()
            return result

        with mock.patch.object(assets, "_relocate_tree", relocate), self.assertRaises(KeyboardInterrupt):
            assets.prepare_assets(self.repo, self.work)
        self.assertEqual(1, len(moved))
        self.assertEqual(prior_tree, self.assets_snapshot())
        self.assertEqual(prior_receipt, receipt.read_bytes())
        self.assertEqual([], list(backups_root.iterdir()))

    def test_interrupt_after_a_restore_rename_reports_only_existing_retained_copies(self):
        # #297: the prior tree is back in Assets when the interrupt lands, so the
        # report must not name its consumed retained path.
        project = self.make_export()
        assets.prepare_assets(self.repo, self.work)
        prior_tree = self.assets_snapshot()
        self.change_export_scene(project)
        active = self.repo / "Assets"
        original_relocate, original_write = assets._relocate_tree, assets._json_write
        moved = []

        def failing_receipt_write(path, value):
            if Path(path).name == "latest-prepare.json":
                raise OSError("injected receipt write failure")
            return original_write(path, value)

        def relocate(source, target):
            result = original_relocate(source, target)
            if not moved and Path(target).parent == active:
                moved.append(source)
                raise KeyboardInterrupt()
            return result

        stderr = io.StringIO()
        with mock.patch.object(assets, "_json_write", failing_receipt_write), \
                mock.patch.object(assets, "_relocate_tree", relocate), \
                mock.patch.object(assets.sys, "stderr", stderr), self.assertRaises(KeyboardInterrupt):
            assets.prepare_assets(self.repo, self.work)
        message = stderr.getvalue()
        self.assertEqual(1, len(moved))
        self.assertFalse(moved[0].exists())
        self.assertNotIn(str(moved[0]), message)
        # StreamingAssets is restored first and is its prior tree again; the new
        # tree is set aside and named. Recovered is never reached.
        streaming = {key: value for key, value in prior_tree.items() if key.startswith("StreamingAssets")}
        self.assertEqual(streaming, {key: value for key, value in self.assets_snapshot().items() if key.startswith("StreamingAssets")})
        aside = list(active.glob(".StreamingAssets.failed-*"))
        self.assertEqual(1, len(aside))
        self.assertIn(f"newly prepared tree: {aside[0]};", message)
        retained = [path for directory in (self.work / "assets/backups").iterdir() for path in directory.iterdir()]
        self.assertEqual(1, len(retained))
        self.assertIn(f"retained prior copies: {retained[0]}", message)

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
        critical = {"Contents/Frameworks/GameAssembly.dylib": b"synthetic code",
                    "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat": b"synthetic metadata",
                    "Contents/Info.plist": b"synthetic plist"}
        for relative, content in critical.items():
            (app / relative).parent.mkdir(parents=True, exist_ok=True)
            (app / relative).write_bytes(content)
        digest = {relative: hashlib.sha256(content).hexdigest() for relative, content in critical.items()}
        # Admit the synthetic bundle as its own declared variant for this test only.
        synthetic = {"binary_sha256": digest["Contents/Frameworks/GameAssembly.dylib"],
                     "metadata_sha256": digest["Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"],
                     "info_plist_sha256": digest["Contents/Info.plist"], "x86_64_slice_sha256": None,
                     "qualified": frozenset((assets.inputvariants.ASSET_PREPARATION,))}
        tool = executable(self.work / "tools/assetripper/AssetRipper.GUI.Free")
        tool.parent.mkdir(parents=True)
        tool.write_text("synthetic")
        pointer = self.work / "assets/latest-assets.json"
        assets._json_write(pointer, {"status": "exported", "run_id": "previous"})
        console = mock.Mock()
        process = mock.Mock()
        # Stand in for the closure verified_tool_path records from the pinned archive.
        with mock.patch.dict(assets.inputvariants.VARIANTS, {"synthetic": synthetic}), \
             mock.patch.dict(bootstrap._INSTALL_SEALS, {tool.parent: ({}, bootstrap.tree_seal(tool.parent))}), \
             mock.patch.object(assets, "verified_tool_path", return_value=tool), \
             mock.patch.object(assets, "_start_ripper", return_value=(process, "http://127.0.0.1:1", console)), \
             mock.patch.object(assets, "_request", side_effect=RuntimeError("synthetic export failure")), \
             mock.patch.object(assets, "_stop_ripper"):
            with self.assertRaisesRegex(RuntimeError, "synthetic"):
                assets.extract_assets(app, self.work)
        self.assertEqual(json.loads(pointer.read_text())["run_id"], "previous")
        reports = list((self.work / "assets/runs").glob("*/report.json"))
        self.assertEqual(json.loads(reports[0].read_text())["status"], "failed")
        self.assertEqual(json.loads(reports[0].read_text())["input_variant"], "synthetic")


if __name__ == "__main__":
    unittest.main()


class RelocationRecordTests(unittest.TestCase):
    def test_an_interrupt_after_a_cross_volume_copy_removes_the_unadopted_copy(self):
        # Both trees exist when an interrupt lands after the copy but before the source
        # is deleted: the source stays authoritative and the copy must not be stranded.
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source, target = root / "source", root / "backups" / "target"
            (source / "a").mkdir(parents=True)
            (source / "a" / "file.bin").write_bytes(b"x" * 64)

            def copy_then_interrupt(src, dst):
                shutil.copytree(src, dst)
                raise KeyboardInterrupt

            locations = {source: source}
            with mock.patch.object(assets, "_relocate_tree", side_effect=copy_then_interrupt):
                with self.assertRaises(KeyboardInterrupt):
                    assets._relocate_recorded(source, target, locations, source)
            self.assertEqual(locations[source], source)
            self.assertTrue((source / "a" / "file.bin").is_file())
            self.assertFalse(target.exists())

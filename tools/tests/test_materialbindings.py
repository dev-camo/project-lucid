"""Material import-kind controls use synthetic exports and preserved input bytes."""

import hashlib
import importlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import assets, bootstrap
from lucidlib.scriptbindings import BINDING_INPUT


SHADER_GUID = "a" * 32
MATERIAL_GUID = "b" * 32
OTHER_GUID = "c" * 32
PREFIX = b"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data.encode() if isinstance(data, str) else data)
    return path


def native_meta(guid, file_id):
    return ("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: " + str(file_id) + "\n")


def material(guid=SHADER_GUID, kind=2, file_id=4800000):
    return (PREFIX + b"--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n"
            b"  m_Name: Example\n  m_Shader: {fileID: " + str(file_id).encode() + b", guid: " + guid.encode()
            + b", type: " + str(kind).encode() + b"}\n  m_Colors: []\n")


class MaterialFixture(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.repo, self.export = self.root / "repo", self.root / "export"
        self.repo.mkdir()
        self.export.mkdir()
        self.mapping = {"assets": []}
        self.shader()

    def shader(self, guid=SHADER_GUID, suffix="", name="Example/Sky"):
        original = "Assets/Shader/Compiled" + suffix + ".asset"
        maintained = "Assets/Shaders/Recovered/Restored" + suffix + ".shader"
        write(self.export / original, PREFIX + b"--- !u!48 &4800000\nShader:\n  m_Name: " + name.encode() + b"\n  m_ParsedForm:\n    m_Name: ignored nested name\n")
        write(self.export / (original + ".meta"), native_meta(guid, 4800000))
        write(self.repo / maintained, 'Shader "' + name + '" { SubShader {} }\n')
        write(self.repo / (maintained + ".meta"), "fileFormatVersion: 2\nguid: " + guid + "\nShaderImporter:\n  externalObjects: {}\n")
        self.mapping["assets"].append({"guid": guid, "path": original, "code_quarantined": False, "shader_quarantined": True})
        return original, maintained

    def qualifier(self):
        return importlib.import_module("lucidlib.materialbindings").maintained_shader_overrides(self.repo, self.export, self.mapping)

    def rewrite(self, data, qualified=None):
        module = importlib.import_module("lucidlib.materialbindings")
        return module.rewrite_material_shader_pointer(data, self.qualifier() if qualified is None else qualified)


class OverrideIdentityTests(MaterialFixture):
    def test_exact_original_and_maintained_identity_qualifies(self):
        qualified = self.qualifier()
        self.assertEqual(set(qualified), {SHADER_GUID})
        self.assertEqual(qualified[SHADER_GUID]["name"], "Example/Sky")
        self.assertEqual(qualified[SHADER_GUID]["file_id"], 4800000)

    def test_same_shader_name_for_distinct_original_guids_is_not_ambiguous(self):
        self.shader(OTHER_GUID, "Copy")
        self.assertEqual(set(self.qualifier()), {SHADER_GUID, OTHER_GUID})

    def test_only_recovered_shader_sources_are_candidates(self):
        write(self.repo / "Assets/Other/Unrelated.shader", b"not a recovered shader")
        self.assertEqual(set(self.qualifier()), {SHADER_GUID})
        directory = self.repo / "Assets/Shaders/Recovered"
        for path in directory.iterdir():
            path.unlink()
        self.assertEqual(self.qualifier(), {})

    def test_original_map_rows_must_be_unique_quarantined_noncode_identities(self):
        original = dict(self.mapping["assets"][0])
        cases = [[], [original, original], [dict(original, code_quarantined=True)],
                 [dict(original, shader_quarantined=False)], [dict(original, guid=OTHER_GUID)],
                 [dict(original, path="../outside.asset")], [dict(original, path="/outside.asset")]]
        for rows in cases:
            with self.subTest(rows=rows), mock.patch.dict(self.mapping, assets=rows):
                with self.assertRaises(ValueError): self.qualifier()

    def test_windows_drive_path_cannot_be_original_evidence_even_if_it_exists_on_posix(self):
        original = self.export / self.mapping["assets"][0]["path"]
        write(self.export / "C:/outside.asset", original.read_bytes())
        write(self.export / "C:/outside.asset.meta", original.with_suffix(".asset.meta").read_bytes())
        self.mapping["assets"][0]["path"] = "C:/outside.asset"
        with self.assertRaises(ValueError): self.qualifier()

    def test_original_shader_header_name_and_metadata_are_all_required(self):
        original = self.export / self.mapping["assets"][0]["path"]
        source, meta = original.read_bytes(), original.with_suffix(".asset.meta")
        original_meta = meta.read_bytes()
        bad_sources = [source.replace(b"!u!48", b"!u!21"), source.replace(b"&4800000", b"&4800001"),
                       source.replace(b"Shader:\n", b"Material:\n"), source + b"--- !u!48 &4800000\nShader:\n",
                       source.replace(b"  m_Name: Example/Sky", b"  m_Name: Changed"),
                       source.replace(b"  m_ParsedForm:", b"  m_Name: Example/Sky\n  m_ParsedForm:")]
        for description in [b'  m_Description: "hello\n  m_Name: Example/Sky\n  world"\n',
                            b"  m_Description: {\n  m_Name: Example/Sky\n  }\n"]:
            bad_sources.append(PREFIX + b"--- !u!48 &4800000\nShader:\n" + description + b"  m_ParsedForm:\n    m_Name: ignored nested name\n")
        for data in bad_sources:
            with self.subTest(data=data):
                original.write_bytes(data)
                with self.assertRaises(ValueError): self.qualifier()
        original.write_bytes(source)
        for data in [original_meta.replace(SHADER_GUID.encode(), OTHER_GUID.encode()),
                     original_meta.replace(b"4800000", b"4800001"), original_meta.replace(b"NativeFormatImporter", b"ShaderImporter"),
                     original_meta + b"guid: " + SHADER_GUID.encode() + b"\n", original_meta + b"  mainObjectFileID: 4800000\n",
                     original_meta + b"guid: INVALID\n", original_meta + b"  mainObjectFileID: INVALID\n",
                     original_meta.replace(b"  mainObjectFileID: 4800000", b"OtherMetadata:\n  mainObjectFileID: 4800000")]:
            with self.subTest(meta=data):
                meta.write_bytes(data)
                with self.assertRaises(ValueError): self.qualifier()

    def test_maintained_shader_requires_one_declaration_and_one_shader_importer_guid(self):
        source = self.repo / "Assets/Shaders/Recovered/Restored.shader"
        meta = source.with_suffix(".shader.meta")
        original, original_meta = source.read_bytes(), meta.read_bytes()
        for data in [original.replace(b"Example/Sky", b"Other"), b"// no Shader declaration\n",
                     original + original, original + b'\nShader "Other" {}\n', b'\xffShader "Example/Sky" {}',
                     b'/*\nShader "Example/Sky"\n*/\n', b'/*\nShader "Example/Sky"\n']:
            with self.subTest(source=data):
                source.write_bytes(data)
                with self.assertRaises(ValueError): self.qualifier()
        source.write_bytes(original)
        for data in [original_meta.replace(SHADER_GUID.encode(), OTHER_GUID.encode()),
                     original_meta.replace(b"ShaderImporter", b"NativeFormatImporter"),
                     original_meta + b"guid: " + SHADER_GUID.encode() + b"\n", original_meta + b"ShaderImporter:\n",
                     original_meta + b"guid: INVALID\n", original_meta + b"ShaderImporter :\n"]:
            with self.subTest(meta=data):
                meta.write_bytes(data)
                with self.assertRaises(ValueError): self.qualifier()

    def test_source_or_metadata_missing_is_not_an_override(self):
        source = self.repo / "Assets/Shaders/Recovered/Restored.shader"
        for path in [source, source.with_suffix(".shader.meta"), self.export / "Assets/Shader/Compiled.asset",
                     self.export / "Assets/Shader/Compiled.asset.meta"]:
            with self.subTest(path=path):
                data = path.read_bytes()
                path.unlink()
                with self.assertRaises(ValueError): self.qualifier()
                path.write_bytes(data)

    def test_shader_declarations_in_comments_are_not_source_identity(self):
        source = self.repo / "Assets/Shaders/Recovered/Restored.shader"
        source.write_text('/*\nShader "Other/Comment"\n*/\nShader "Example/Sky" { SubShader {} }\n')
        self.assertEqual(set(self.qualifier()), {SHADER_GUID})

    def test_metadata_identity_fields_inside_scalar_or_flow_data_do_not_qualify(self):
        original = self.export / "Assets/Shader/Compiled.asset.meta"
        maintained = self.repo / "Assets/Shaders/Recovered/Restored.shader.meta"
        for path in [original, maintained]:
            raw = path.read_bytes()
            guid_line = b"guid: " + SHADER_GUID.encode() + b"\n"
            for fake_guid in [b'note: "hello\n' + guid_line + b'world"\n',
                              b"note: {\n" + guid_line + b"}\n"]:
                with self.subTest(path=path, fake=fake_guid):
                    path.write_bytes(raw.replace(guid_line, fake_guid))
                    with self.assertRaises(ValueError): self.qualifier()
            path.write_bytes(raw)
        raw = maintained.read_bytes()
        maintained.write_bytes(raw.replace(b"ShaderImporter:\n", b'note: "hello\nShaderImporter:\n  world"\n'))
        with self.assertRaises(ValueError): self.qualifier()

    def test_single_line_quoted_metadata_values_and_comments_are_preserved(self):
        original = self.export / "Assets/Shader/Compiled.asset.meta"
        original.write_bytes(original.read_bytes() + '  userData: "Unicode 測試; {} [] # data" # comment\n'.encode())
        self.assertEqual(set(self.qualifier()), {SHADER_GUID})

    def test_quoted_duplicate_metadata_identities_are_rejected(self):
        maintained = self.repo / "Assets/Shaders/Recovered/Restored.shader.meta"
        original = self.export / "Assets/Shader/Compiled.asset.meta"
        cases = [(maintained, b'"guid": ' + OTHER_GUID.encode() + b"\n"),
                 (maintained, b"'guid': " + OTHER_GUID.encode() + b"\n"),
                 (maintained, b'"ShaderImporter": {}\n'),
                 (original, b'"guid": ' + OTHER_GUID.encode() + b"\n"),
                 (original, b'  "mainObjectFileID": 4800001\n'),
                 (original, b"  'mainObjectFileID': 4800001\n")]
        for path, duplicate in cases:
            raw = path.read_bytes()
            with self.subTest(path=path, duplicate=duplicate):
                try:
                    path.write_bytes(raw + duplicate)
                    with self.assertRaises(ValueError):
                        self.qualifier()
                finally:
                    path.write_bytes(raw)

    def test_escaped_quoted_and_explicit_metadata_keys_are_unsupported(self):
        maintained = self.repo / "Assets/Shaders/Recovered/Restored.shader.meta"
        original = self.export / "Assets/Shader/Compiled.asset.meta"
        cases = [(maintained, b'"\\u0067uid": ' + OTHER_GUID.encode() + b"\n"),
                 (maintained, b'"Shader\\u0049mporter": {}\n'),
                 (original, b'  "mainObjectFile\\u0049D": 4800001\n'),
                 (original, b'"NativeFormatImporter": {}\n'),
                 (maintained, b"? guid\n: " + OTHER_GUID.encode() + b"\n"),
                 (maintained, b'? "ShaderImporter"\n: {}\n'),
                 (original, b"  ? mainObjectFileID\n  : 4800001\n"),
                 (original, b"?\n: " + OTHER_GUID.encode() + b"\n")]
        for path, duplicate in cases:
            raw = path.read_bytes()
            with self.subTest(path=path, duplicate=duplicate):
                try:
                    path.write_bytes(raw + duplicate)
                    with self.assertRaises(ValueError):
                        self.qualifier()
                finally:
                    path.write_bytes(raw)

    def test_duplicate_maintained_guid_is_rejected_even_with_same_name(self):
        self.shader(SHADER_GUID, "Duplicate")
        self.mapping["assets"].pop()
        with self.assertRaises(ValueError): self.qualifier()

    def test_symlinked_source_metadata_export_and_parent_are_rejected(self):
        original = self.export / self.mapping["assets"][0]["path"]
        source = self.repo / "Assets/Shaders/Recovered/Restored.shader"
        for path in [source, source.with_suffix(".shader.meta"), original, original.with_suffix(".asset.meta")]:
            with self.subTest(path=path):
                data = path.read_bytes()
                saved = write(self.root / "outside", data)
                path.unlink()
                path.symlink_to(saved)
                with self.assertRaises(ValueError): self.qualifier()
                path.unlink()
                path.write_bytes(data)
        directory = source.parent
        destination = self.root / "moved"
        directory.rename(destination)
        directory.symlink_to(destination, target_is_directory=True)
        with self.assertRaises(ValueError): self.qualifier()


class MaterialPointerTests(MaterialFixture):
    def test_only_kind_digit_changes_and_second_rewrite_is_idempotent(self):
        data = material()
        prepared, change = self.rewrite(data)
        expected = data.replace(b"type: 2}", b"type: 3}")
        self.assertEqual(prepared, expected)
        self.assertEqual(change["guid"], SHADER_GUID)
        self.assertEqual(change["file_id"], 4800000)
        self.assertEqual((change["old_type"], change["prepared_type"]), (2, 3))
        self.assertEqual(sum(a != b for a, b in zip(data, prepared)), 1)
        self.assertEqual(self.rewrite(prepared), (prepared, None))

    def test_crlf_is_preserved_and_mixed_line_endings_are_rejected_when_selected(self):
        data = material().replace(b"\n", b"\r\n")
        self.assertEqual(self.rewrite(data)[0], data.replace(b"type: 2}", b"type: 3}"))
        with self.assertRaises(ValueError): self.rewrite(data.replace(b"Material:\r\n", b"Material:\n"))

    def test_selected_pointer_does_not_rewrite_textures_or_repeated_guids_in_other_fields(self):
        other = ("  m_Description: 'Unicode 測試; type: 2'\n  # ordinary comment\n"
                 "  m_Texture: {fileID: 2800000, guid: " + OTHER_GUID + ", type: 2}\n"
                 "  m_OtherReference: {fileID: 4800000, guid: " + SHADER_GUID + ", type: 2}\n").encode()
        data = material().replace(b"  m_Colors: []\n", other + b"  m_Colors: []\n")
        expected = data.replace(b"  m_Shader: {fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 2}",
                                b"  m_Shader: {fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 3}")
        actual, change = self.rewrite(data)
        self.assertEqual(actual, expected)
        self.assertEqual(sum(a != b for a, b in zip(data, actual)), 1)
        self.assertEqual(change["guid"], SHADER_GUID)

    def test_unknown_null_local_and_nonmaterial_bytes_are_unchanged(self):
        for data in [material(OTHER_GUID), material().replace(b"guid: " + SHADER_GUID.encode() + b", type: 2", b"guid: " + b"0" * 32 + b", type: 2"),
                     material().replace(b"{fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 2}", b"{fileID: 0}"),
                     material().replace(b"!u!21", b"!u!114").replace(b"Material:", b"MonoBehaviour:"),
                     b"binary\x00" + SHADER_GUID.encode(), b"not yaml"]:
            with self.subTest(data=data): self.assertEqual(self.rewrite(data), (data, None))
        self.assertEqual(self.rewrite(material(), {}), (material(), None))

    def test_same_guid_elsewhere_in_material_is_never_globally_rewritten(self):
        data = material(OTHER_GUID).replace(b"  m_Colors: []", b"  m_Description: " + SHADER_GUID.encode() + b" type: 2\n  m_Colors: []")
        self.assertEqual(self.rewrite(data), (data, None))

    def test_shader_looking_lines_inside_multiline_scalars_never_get_rewritten(self):
        field = b"  m_Shader: {fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 2}\n"
        header = PREFIX + b"--- !u!21 &2100000\nMaterial:\n  m_Name: Example\n"
        unknown = b"  m_Shader: {fileID: 4800000, guid: " + OTHER_GUID.encode() + b", type: 2}\n"
        for quote in [b'"', b"'"]:
            decoy = header + b"  m_Description: " + quote + b"hello\n" + field + b"  world" + quote + b"\n"
            for data in [decoy, decoy + unknown]:
                with self.subTest(data=data):
                    with self.assertRaises(ValueError): self.rewrite(data)
        flow = header + b'  stringTagMap: {tag: "hello\n' + field + b'  world"}\n'
        with self.assertRaises(ValueError): self.rewrite(flow)
        block = header + b"  m_Description: |\n    " + field.lstrip()
        self.assertEqual(self.rewrite(block), (block, None))
        with self.assertRaises(ValueError): self.rewrite(block + field)

    def test_tagged_or_anchored_quoted_scalar_decoys_are_unsupported(self):
        field = b"  m_Shader: {fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 2}\n"
        header = PREFIX + b"--- !u!21 &2100000\nMaterial:\n  m_Name: Example\n"
        for properties in [b"&anchor ", b"!!str ", b"!<tag:yaml.org,2002:str> ", b"&anchor !!str "]:
            for quote in [b'"', b"'"]:
                data = header + b"  m_Description: " + properties + quote + b"hello\n" + field + b"  world" + quote + b"\n"
                with self.subTest(data=data):
                    with self.assertRaises(ValueError): self.rewrite(data)

    def test_shader_looking_fields_in_multiline_flow_containers_are_unsupported(self):
        field = b"  m_Shader: {fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 2}\n"
        header = PREFIX + b"--- !u!21 &2100000\nMaterial:\n  m_Name: Example\n"
        for flow in [b"  m_Description: {\n" + field + b"  }\n",
                     b"  m_Description: [\n  {\n" + field + b"  }]\n"]:
            with self.subTest(flow=flow):
                with self.assertRaises(ValueError): self.rewrite(header + flow)

    def test_malformed_or_ambiguous_selected_pointer_fails_closed(self):
        data = material()
        pointer = b"  m_Shader: {fileID: 4800000, guid: " + SHADER_GUID.encode() + b", type: 2}"
        bad = [data.replace(b"4800000", b"4800001"), data.replace(b"type: 2", b"type: 1"),
               data.replace(b"type: 2", b"type: 22"), data.replace(b", type: 2", b""),
               data.replace(b"type: 2}", b"type: 2, extra: 0}"), data.replace(b"type: 2}", b"type: 2} trailing"),
               data.replace(b"guid: ", b"guid: \""), data.replace(b"  m_Shader:", b"  m_Shader :"),
               data.replace(b"  m_Shader:", b'  "m_Shader":'),
               data.replace(pointer, pointer + b"\n" + pointer), data.replace(b"&2100000", b"&2100001"),
               data.replace(b"Material:\n", b"MonoBehaviour:\n"), data.replace(b"%YAML 1.1\n", b""),
               data.replace(b"--- !u!21", b"---!u!21"),
               data + b"--- !u!21 &2100000\nMaterial:\n", data.replace(b"m_Colors: []", b"m_Colors: []\r"),
               data.replace(b"m_Name: Example", b"m_Name: Ex\x00ample")]
        for selected in bad:
            with self.subTest(data=selected):
                with self.assertRaises(ValueError): self.rewrite(selected)


class MaterialPreparationTests(MaterialFixture):
    def setUp(self):
        super().setUp()
        self.work = self.root / "work"
        self.work.mkdir()
        self.cache = mock.patch.object(bootstrap, "CACHE_ROOT", self.work)
        self.cache.start()
        self.addCleanup(self.cache.stop)
        self.project = self.work / "assets/runs/test/project"
        self.project.parent.mkdir(parents=True)
        self.export.rename(self.project)
        self.export = self.project
        self.app = self.root / "Game.app"
        self.app.mkdir()
        self.matpath = self.project / "Assets/Material/ordinary.mat"
        write(self.matpath, material())
        write(self.matpath.with_suffix(".mat.meta"), native_meta(MATERIAL_GUID, 2100000))
        write(self.project / "Assets/StreamingAssets/FSM.json", b"{}")
        mapping = assets.build_asset_map(self.project)
        self.map_path = self.project.parent / "asset-map.json"
        assets._json_write(self.map_path, mapping)
        self.fingerprint = {"critical_files": {
            "Contents/Frameworks/GameAssembly.dylib": BINDING_INPUT["binary_sha256"],
            "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat": BINDING_INPUT["metadata_sha256"]}}
        self.latest = self.work / "assets/latest-assets.json"
        self.publish_export()

    def publish_export(self):
        assets._json_write(self.latest, {"status": "exported", "run_id": "test", "project_path": str(self.project),
            "asset_map_path": str(self.map_path), "input_path": str(self.app), "input_fingerprint": self.fingerprint,
            "tool": {"name": "fixture"}})

    def prepare(self, bindings=()):
        # Synthetic fixtures cannot impersonate original binaries or real layout inventories.
        # Patch those trust boundaries; the qualifier, copy loop, receipt and rollback remain real.
        with mock.patch.object(assets, "_validate_export_identity", return_value=self.app), \
                mock.patch("lucidlib.scriptbindings.resolve_bindings", return_value={"bindings": list(bindings)}), \
                mock.patch.object(assets, "_render_pipeline", return_value={"status": "not_applicable", "shader_map": {}}):
            return assets.prepare_assets(self.repo, self.work)

    def snapshot(self):
        return {path.relative_to(self.repo).as_posix(): path.read_bytes() for path in (self.repo / "Assets").rglob("*") if path.is_file()}

    def test_ordinary_material_without_scripts_is_normalized_and_receipted(self):
        raw = self.matpath.read_bytes()
        exported = {path.relative_to(self.project).as_posix(): path.read_bytes()
                    for path in self.project.rglob("*") if path.is_file()}
        maintained = {path.relative_to(self.repo).as_posix(): path.read_bytes()
                      for path in (self.repo / "Assets/Shaders").rglob("*") if path.is_file()}
        result = self.prepare()
        prepared = self.repo / "Assets/Recovered/Material/ordinary.mat"
        self.assertEqual(prepared.read_bytes(), raw.replace(b"type: 2}", b"type: 3}"))
        self.assertEqual(self.matpath.read_bytes(), raw)
        self.assertEqual({path.relative_to(self.project).as_posix(): path.read_bytes()
                          for path in self.project.rglob("*") if path.is_file()}, exported)
        self.assertEqual({path.relative_to(self.repo).as_posix(): path.read_bytes()
                          for path in (self.repo / "Assets/Shaders").rglob("*") if path.is_file()}, maintained)
        self.assertEqual(prepared.with_suffix(".mat.meta").read_bytes(), self.matpath.with_suffix(".mat.meta").read_bytes())
        self.assertEqual(result["script_bindings"]["assets"], [])
        normalization = result["material_shader_normalization"]
        self.assertEqual(normalization["status"], "complete")
        self.assertEqual(len(normalization["assets"]), 1)
        row = normalization["assets"][0]
        self.assertEqual(row["path"], "Assets/Material/ordinary.mat")
        self.assertEqual(row["guid"], MATERIAL_GUID)
        self.assertEqual(row["exported_sha256"], hashlib.sha256(raw).hexdigest())
        self.assertEqual(row["prepared_sha256"], hashlib.sha256(prepared.read_bytes()).hexdigest())
        self.assertEqual(row["shader"]["guid"], SHADER_GUID)

    def test_unknown_input_never_activates_maintained_overrides(self):
        write(self.repo / "Assets/Shaders/Recovered/Restored.shader", b"invalid even if inspected")
        critical = dict(self.fingerprint["critical_files"])
        for key in critical:
            with self.subTest(key=key):
                self.fingerprint["critical_files"] = dict(critical, **{key: "unknown"})
                self.publish_export()
                result = self.prepare()
                self.assertEqual((self.repo / "Assets/Recovered/Material/ordinary.mat").read_bytes(), self.matpath.read_bytes())
                self.assertEqual(result["material_shader_normalization"], {"status": "not_applicable", "assets": []})

    def test_material_and_script_rewrites_keep_separate_rows_counts_and_hashes(self):
        owner = (PREFIX + b"--- !u!114 &11400000\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: "
                 + OTHER_GUID.encode() + b", type: 3}\n  m_Name: Owner\n")
        write(self.project / "Assets/Scripts/Example.cs", b"class Example {}\n")
        write(self.project / "Assets/Scripts/Example.cs.meta", "fileFormatVersion: 2\nguid: " + OTHER_GUID + "\n")
        write(self.project / "Assets/Data/owner.asset", owner)
        write(self.project / "Assets/Data/owner.asset.meta", native_meta("e" * 32, 11400000))
        assets._json_write(self.map_path, assets.build_asset_map(self.project))
        binding = {"exported_guid": OTHER_GUID, "exported_file_id": 11500000,
                   "target_guid": "d" * 32, "target_file_id": 11500000}
        result = self.prepare([binding])
        script = owner.replace(OTHER_GUID.encode(), b"d" * 32)
        self.assertEqual((self.repo / "Assets/Recovered/Data/owner.asset").read_bytes(), script)
        self.assertEqual((self.project / "Assets/Data/owner.asset").read_bytes(), owner)
        self.assertTrue(result["script_bindings"]["references_modified"])
        self.assertEqual(result["script_bindings"]["assets"], [{"path": "Assets/Data/owner.asset", "guid": "e" * 32,
            "pointers_rewritten": 1, "exported_sha256": hashlib.sha256(owner).hexdigest(),
            "prepared_sha256": hashlib.sha256(script).hexdigest()}])
        self.assertEqual([row["path"] for row in result["material_shader_normalization"]["assets"]], ["Assets/Material/ordinary.mat"])
        self.assertFalse((self.repo / "Assets/Recovered/Scripts/Example.cs").exists())

    def test_duplicate_material_map_identity_is_rejected_before_publication(self):
        mapping = json.loads(self.map_path.read_text())
        row = next(row for row in mapping["assets"] if row["guid"] == MATERIAL_GUID)
        mapping["assets"].append(dict(row))
        assets._json_write(self.map_path, mapping)
        with self.assertRaises(ValueError): self.prepare()
        self.assertFalse((self.repo / "Assets/Recovered").exists())

    def test_nonmaterial_and_unknown_shader_material_are_copied_unchanged(self):
        for data in [material().replace(b"!u!21", b"!u!114").replace(b"Material:", b"MonoBehaviour:"), material(OTHER_GUID)]:
            with self.subTest(data=data):
                self.matpath.write_bytes(data)
                result = self.prepare()
                self.assertEqual((self.repo / "Assets/Recovered/Material/ordinary.mat").read_bytes(), data)
                self.assertEqual(result["material_shader_normalization"]["assets"], [])

    def test_selected_bad_reference_or_material_metadata_preserves_previous_tree_and_receipt(self):
        self.prepare()
        receipt = self.work / "assets/latest-prepare.json"
        old_receipt, prior = receipt.read_bytes(), self.snapshot()
        original = self.matpath.read_bytes()
        meta = self.matpath.with_suffix(".mat.meta")
        original_meta = meta.read_bytes()
        for field in ["pointer", "metadata", "ambiguous metadata"]:
            with self.subTest(field=field):
                self.matpath.write_bytes(original.replace(b"4800000", b"4800001") if field == "pointer" else original)
                meta.write_bytes(original_meta.replace(MATERIAL_GUID.encode(), OTHER_GUID.encode()) if field == "metadata" else original_meta)
                if field == "ambiguous metadata":
                    meta.write_bytes(original_meta + b"guid: INVALID\n")
                with self.assertRaises(ValueError): self.prepare()
                self.assertEqual(receipt.read_bytes(), old_receipt)
                self.assertEqual(self.snapshot(), prior)

    def test_unsupported_material_metadata_keys_or_scalar_guid_preserve_prior_publication(self):
        self.prepare()
        receipt = self.work / "assets/latest-prepare.json"
        old_receipt, prior = receipt.read_bytes(), self.snapshot()
        meta = self.matpath.with_suffix(".mat.meta")
        raw = meta.read_bytes()
        guid_line = b"guid: " + MATERIAL_GUID.encode() + b"\n"
        cases = [raw + b'"guid": ' + OTHER_GUID.encode() + b"\n",
                 raw + b"'guid': " + OTHER_GUID.encode() + b"\n",
                 raw + b'"\\u0067uid": ' + OTHER_GUID.encode() + b"\n",
                 raw + b"? guid\n: " + OTHER_GUID.encode() + b"\n",
                 raw.replace(guid_line, b'userData: "hello\n' + guid_line + b'world"\n'),
                 raw.replace(guid_line, b"userData: {\n" + guid_line + b"}\n")]
        for data in cases:
            with self.subTest(metadata=data):
                try:
                    meta.write_bytes(data)
                    with self.assertRaises(ValueError):
                        self.prepare()
                    self.assertEqual(receipt.read_bytes(), old_receipt)
                    self.assertEqual(self.snapshot(), prior)
                    self.assertEqual(meta.read_bytes(), data)
                finally:
                    meta.write_bytes(raw)

    def test_quoted_material_metadata_values_and_comments_are_copied_byte_for_byte(self):
        meta = self.matpath.with_suffix(".mat.meta")
        raw = meta.read_bytes() + '  userData: "Unicode 測試; {} [] # guid:" # comment\n'.encode()
        meta.write_bytes(raw)
        self.prepare()
        prepared = self.repo / "Assets/Recovered/Material/ordinary.mat.meta"
        self.assertEqual(prepared.read_bytes(), raw)
        self.assertEqual(meta.read_bytes(), raw)

    def test_receipt_publication_failure_rolls_back_normalized_tree_and_receipt(self):
        self.prepare()
        receipt = self.work / "assets/latest-prepare.json"
        old_receipt, prior = receipt.read_bytes(), self.snapshot()
        self.matpath.write_bytes(material().replace(b"m_Name: Example", b"m_Name: Updated"))
        original_write = assets._json_write
        def fail_receipt(path, value):
            if path == receipt: raise OSError("receipt publication failed")
            return original_write(path, value)
        with mock.patch.object(assets, "_json_write", side_effect=fail_receipt):
            with self.assertRaisesRegex(OSError, "receipt publication failed"): self.prepare()
        self.assertEqual(receipt.read_bytes(), old_receipt)
        self.assertEqual(self.snapshot(), prior)

    def test_later_invalid_material_discards_earlier_valid_staging_and_keeps_prior_receipt(self):
        self.prepare()
        receipt = self.work / "assets/latest-prepare.json"
        old_receipt, prior = receipt.read_bytes(), self.snapshot()
        write(self.project / "Assets/Material/z-later.mat", material(file_id=4800001))
        write(self.project / "Assets/Material/z-later.mat.meta", native_meta("e" * 32, 2100000))
        assets._json_write(self.map_path, assets.build_asset_map(self.project))
        with self.assertRaises(ValueError): self.prepare()
        self.assertEqual(receipt.read_bytes(), old_receipt)
        self.assertEqual(self.snapshot(), prior)
        self.assertEqual(list((self.repo / "Assets").glob(".lucid-assets-*")), [])


if __name__ == "__main__":
    unittest.main()

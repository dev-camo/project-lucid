import copy
import hashlib
import json
import re
import tempfile
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import scriptbindings
from lucidlib.scriptbindings import rewrite_prepared_script_pointers
from lucidlib.verification import artifact_fingerprint
from tools.tests import test_scriptbindings as resolver_tests

# Literal original uihighlight_default_0 Image document; only the YAML envelope
# is supplied here. Values and identity come from the immutable release export.
AUTHORED = b"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &114703437598541787
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1709254077376921}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3cf5a44414476512e00c3e7a2569a919, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 009495c444868ba409e37d6a8dede145, type: 2}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

# Literal first rejected original Image from CS_Zone4_Boss_B_3D.
BUILTIN_AUTHORED = b"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &114880969609912730
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1996317296757491}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3cf5a44414476512e00c3e7a2569a919, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {fileID: 0}
  m_Color: {r: 0.4339623, g: 0.4339623, b: 0.4339623, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

BINDING = {
    "assembly": "UnityEngine.UI", "full_name": "UnityEngine.UI.Image",
    "package": "com.unity.ugui", "exported_file_id": 11500000,
    "exported_guid": "3cf5a44414476512e00c3e7a2569a919",
    "target_file_id": 11500000, "target_guid": "fe87c0e1cc204ed48ad3b37840f39efc",
}


class ImageBindingTests(unittest.TestCase):
    def rewrite(self, content):
        before = bytes(content)
        binding = copy.deepcopy(BINDING)
        try:
            return rewrite_prepared_script_pointers(content, [binding])
        finally:
            self.assertEqual(before, content)
            self.assertEqual(BINDING, binding)

    def test_original_authored_image_changes_only_its_script_pointer(self):
        result, count = self.rewrite(AUTHORED)
        original = ("  m_Script: {fileID: 11500000, guid: " +
                    BINDING["exported_guid"] + ", type: 3}").encode()
        target = original.replace(BINDING["exported_guid"].encode(), BINDING["target_guid"].encode())
        self.assertEqual(1, count)
        self.assertEqual(AUTHORED.replace(original, target), result)

    def test_original_builtin_sprite_document_changes_only_script_pointer(self):
        result, count = self.rewrite(BUILTIN_AUTHORED)
        self.assertEqual(1, count)
        self.assertEqual(BUILTIN_AUTHORED.replace(BINDING["exported_guid"].encode(),
                                                BINDING["target_guid"].encode()), result)

    def test_exact_authored_builtin_tuples_preserve_bytes(self):
        # Exact original field/local-ID tuples; installed resource class IDs are
        # Sprite213 for these four and Material21 for10754, requiring native qualification.
        for field, local_id in (("m_Sprite", 10907), ("m_Sprite", 10911),
                                ("m_Sprite", 10913), ("m_Sprite", 10917),
                                ("m_Material", 10754)):
            with self.subTest(field=field, local_id=local_id):
                pointer = ("{fileID: " + str(local_id) + ", guid: 0000000000000000f000000000000000, type: 0}").encode()
                content = re.sub(rb"(?m)^  " + field.encode() + rb": .*?$",
                                 b"  " + field.encode() + b": " + pointer, AUTHORED)
                result, count = self.rewrite(content)
                self.assertEqual(1, count)
                self.assertEqual(content.replace(BINDING["exported_guid"].encode(),
                                                BINDING["target_guid"].encode()), result)

    def test_builtin_pointer_identity_type_and_field_mismatches_fail_closed(self):
        sprite = b"{fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}"
        invalid = (
            ("m_Sprite", sprite.replace(b"10907", b"10908")),
            ("m_Sprite", sprite.replace(b"10907", b"-10907")),
            ("m_Sprite", sprite.replace(b"10907", b"10754")),
            ("m_Sprite", sprite.replace(b"type: 0", b"type: 2")),
            ("m_Sprite", sprite.replace(b"f000000000000000", b"e000000000000000").replace(b"type: 0", b"type: 2")),
            ("m_Sprite", sprite.replace(b"type: 0", b"type: 3")),
            ("m_Sprite", sprite.replace(b"f000000000000000", b"e000000000000000")),
            ("m_Sprite", sprite.replace(b"0000000000000000f000000000000000", b"009495c444868ba409e37d6a8dede145")),
            ("m_Material", sprite),
            ("m_Material", sprite.replace(b"10907", b"10755")),
        )
        for field, pointer in invalid:
            with self.subTest(field=field, pointer=pointer):
                content = re.sub(rb"(?m)^  " + field.encode() + rb": .*?$",
                                 b"  " + field.encode() + b": " + pointer, AUTHORED)
                with self.assertRaises(ValueError):
                    self.rewrite(content)

    def test_disabled_image_and_typed_scalar_values_are_preserved(self):
        content = AUTHORED.replace(b"m_Enabled: 1", b"m_Enabled: 0")
        content = content.replace(b"m_Maskable: 1", b"m_Maskable: 0")
        content = content.replace(b"m_UseSpriteMesh: 0", b"m_UseSpriteMesh: 1")
        content = content.replace(b"m_FillAmount: 1", b"m_FillAmount: 0.25")
        content = content.replace(b"m_PixelsPerUnitMultiplier: 1", b"m_PixelsPerUnitMultiplier: 2")
        result, count = self.rewrite(content)
        self.assertEqual(1, count)
        self.assertEqual(content.replace(BINDING["exported_guid"].encode(), BINDING["target_guid"].encode()), result)

    def test_representable_values_are_not_confused_with_lifecycle_normalization(self):
        # Original OnAfterDeserialize 06000162@27D1C20 and Editor OnValidate
        # normalize these fields;
        # this parser-only control proves byte preservation, not a roundtrip.
        for old, new in ((b"m_FillOrigin: 0", b"m_FillOrigin: -1"),
                         (b"m_FillAmount: 1", b"m_FillAmount: -0.25"),
                         (b"m_FillAmount: 1", b"m_FillAmount: 2"),
                         (b"m_PixelsPerUnitMultiplier: 1", b"m_PixelsPerUnitMultiplier: -1")):
            with self.subTest(new=new):
                content = AUTHORED.replace(old, new)
                result, count = self.rewrite(content)
                self.assertEqual(1, count)
                self.assertEqual(content.replace(BINDING["exported_guid"].encode(),
                                                BINDING["target_guid"].encode()), result)

    def test_nonnull_material_and_signed_object_id_are_preserved(self):
        # Material identity is authored in CS_Zone4_Boss_A_2D. Negative Sprite
        # local ID is a syntax control only; asset resolution is native proof.
        content = AUTHORED.replace(b"m_Material: {fileID: 0}",
                                  b"m_Material: {fileID: 2100000, guid: fd67a78bfddedd942bef68ee6c7ef874, type: 2}")
        content = content.replace(b"fileID: 21300000", b"fileID: -21300000")
        content = content.replace(b"&114703437598541787", b"&-114703437598541787")
        content = content.replace(b"m_GameObject: {fileID: 1709254077376921}", b"m_GameObject: {fileID: -1709254077376921}")
        result, count = self.rewrite(content)
        self.assertEqual(1, count)
        self.assertEqual(content.replace(BINDING["exported_guid"].encode(),
                                        BINDING["target_guid"].encode()), result)

    def test_complete_shape_and_typed_payload_fail_closed(self):
        mutations = (
            (b"&114703437598541787", b"&9223372036854775808"),
            (b"&114703437598541787", b"&0"),
            (b"&114703437598541787", "&١".encode()),
            (b"  m_Color: {r: 1, g: 1, b: 1, a: 1}\n", b""),
            (b"  m_Color:", b"  extra: 1\n  m_Color:"),
            (b"  m_Maskable: 1", b"  m_Maskable: 1\n  m_Maskable: 1"),
            (b"  m_RaycastTarget: 0\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}",
             b"  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_RaycastTarget: 0"),
            (b"m_Enabled: 1", b"m_Enabled: 2"),
            (b"m_Maskable: 1", b"m_Maskable: true"),
            (b"m_Type: 0", b"m_Type: 4"),
            (b"m_FillMethod: 4", b"m_FillMethod: 5"),
            (b"m_FillOrigin: 0", b"m_FillOrigin: 2147483648"),
            (b"m_FillAmount: 1", b"m_FillAmount: .nan"),
            (b"m_FillAmount: 1", b"m_FillAmount: 1e100"),
            (b"m_Color: {r: 1, g: 1, b: 1, a: 1}", b"m_Color: {r: 1, g: 1, b: 1}"),
            (b"m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}", b"m_RaycastPadding: {x: 0, y: 0, z: 0, w: .inf}"),
            (b"m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}", b"m_RaycastPadding: {y: 0, x: 0, z: 0, w: 0}"),
            (b"m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}", b"m_RaycastPadding: {x: 0, x: 0, z: 0, w: 0}"),
            (b"m_Color: {r: 1, g: 1, b: 1, a: 1}", b"m_Color: {a: 1, r: 1, g: 1, b: 1}"),
            (b"m_Color: {r: 1, g: 1, b: 1, a: 1}", b"m_Color: {r: 1, r: 1, b: 1, a: 1}"),
            (b"m_Material: {fileID: 0}", b"m_Material: {fileID: 0, guid: bad, type: 2}"),
            (b"fileID: 21300000", b"fileID: 9223372036854775808"),
            (b"guid: 009495c444868ba409e37d6a8dede145, type: 2", b"guid: 009495c444868ba409e37d6a8dede145, type: 4"),
            (b"guid: 009495c444868ba409e37d6a8dede145", b"guid: 00000000000000000000000000000000"),
            (b"m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []",
             b"m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: [{m_MethodName: Invoke}]"),
            (b"      m_Calls: []", b"      m_Calls: []\n      m_Calls: []"),
            (b"      m_Calls: []", b"      unknown: []"),
            (b"    m_PersistentCalls:", b"    arbitrary:"),
            (b"m_OnCullStateChanged:", b"m_OnCullStateChanged: {}"),
            (b"m_OnCullStateChanged:\n", b"m_OnCullStateChanged:\n  - value: 1\n"),
        )
        for old, new in mutations:
            with self.subTest(old=old, new=new):
                self.assertIn(old, AUTHORED)
                changed = AUTHORED.replace(old, new, 1)
                with self.assertRaises(ValueError):
                    self.rewrite(changed)

    def test_unselected_documents_are_untouched(self):
        unrelated = b"--- !u!1 &1\nGameObject:\n  m_Name: Unrelated\n"
        result, count = self.rewrite(AUTHORED + unrelated)
        self.assertEqual(1, count)
        self.assertTrue(result.endswith(unrelated))

    def test_duplicate_image_document_identity_is_rejected(self):
        duplicate = AUTHORED[AUTHORED.index(b"--- !u!"):]
        with self.assertRaises(ValueError):
            self.rewrite(AUTHORED + duplicate)

    def test_crlf_and_null_sprite_preserve_all_other_bytes(self):
        content = AUTHORED.replace(b"{fileID: 21300000, guid: 009495c444868ba409e37d6a8dede145, type: 2}",
                                   b"{fileID: 0}").replace(b"\n", b"\r\n")
        result, count = self.rewrite(content)
        self.assertEqual(1, count)
        self.assertEqual(content.replace(BINDING["exported_guid"].encode(), BINDING["target_guid"].encode()), result)


class ImageAdmissionTests(unittest.TestCase):
    def fixture(self, directory):
        matches = [rule for rule in scriptbindings.RULES if rule["full_name"] == "UnityEngine.UI.Image"]
        self.assertEqual(1, len(matches), "one separately qualified Image rule")
        rule = matches[0]
        fixture = resolver_tests.UguiScriptBindingTests.fixture(self, directory)
        root, work, export, mapping, comparison, path, summary, package = fixture
        hashes = {}
        for relative in rule["runtime_sources"]:
            source = package / relative.removeprefix("Packages/com.unity.ugui/")
            if not source.exists():
                source.parent.mkdir(parents=True, exist_ok=True)
                source.write_text("// Image admission fixture " + relative + "\n")
            hashes[relative] = hashlib.sha256(source.read_bytes()).hexdigest()
        guard = patch.dict(rule["runtime_sources"], hashes, clear=True)
        guard.start()
        self.addCleanup(guard.stop)
        source = package / rule["maintained_path"].removeprefix("Packages/com.unity.ugui/")
        Path(str(source) + ".meta").write_text("fileFormatVersion: 2\nguid: " + BINDING["target_guid"] + "\n")
        original = export / rule["export_path"]
        original.parent.mkdir(parents=True, exist_ok=True)
        original.write_text("// Image original declaration fixture\n")
        Path(str(original) + ".meta").write_text("fileFormatVersion: 2\nguid: " + BINDING["exported_guid"] + "\n")
        mapping["assets"].append({"path": rule["export_path"], "guid": BINDING["exported_guid"], "code_quarantined": True})
        comparison["candidates"].append({
            "assembly": rule["assembly"], "full_name": rule["full_name"], "status": "compatible_layout", "issues": [],
            "original_token": "0x02000024", "loaded_scripts": [{
                "path": rule["maintained_path"], "guid": BINDING["target_guid"], "file_id": 11500000,
                "class_resolved": True, "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                "source_path": str(source), "package_name": "com.unity.ugui", "package_version": "1.0.0",
                "package_builtin": True, "package_resolved_path": str(package)}]})
        comparison["source_fingerprint"] = summary["source_fingerprint"] = artifact_fingerprint(root)
        path.write_text(json.dumps(comparison))
        return fixture, rule

    def resolve(self, fixture):
        root, work, export, mapping, comparison, path, summary, package = fixture
        with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
            return scriptbindings.resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)

    def test_image_joins_verified_bindings_without_changing_existing_three_type_rules(self):
        self.assertEqual(["UnityEngine.UI.LayoutElement", "UnityEngine.UI.ContentSizeFitter", "UnityEngine.UI.AspectRatioFitter"],
                         [rule["full_name"] for rule in scriptbindings.PACKAGE_RULES])
        with tempfile.TemporaryDirectory() as directory:
            fixture, rule = self.fixture(directory)
            before = artifact_fingerprint(fixture[0])
            result = self.resolve(fixture)
            images = [row for row in result["bindings"] if row["full_name"] == rule["full_name"]]
            self.assertEqual(1, len(images))
            self.assertEqual(BINDING["exported_guid"], images[0]["exported_guid"])
            self.assertEqual(BINDING["target_guid"], images[0]["target_guid"])
            self.assertEqual(11500000, images[0]["target_file_id"])
            self.assertFalse(result["references_modified"])
            self.assertEqual(before, artifact_fingerprint(fixture[0]))

    def test_each_inherited_or_helper_source_mutation_rejects_before_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            fixture, rule = self.fixture(directory)
            package = fixture[-1]
            for relative in rule["runtime_sources"]:
                source = package / relative.removeprefix("Packages/com.unity.ugui/")
                raw = source.read_bytes()
                try:
                    source.write_bytes(raw + b"// mutated\n")
                    with self.subTest(relative=relative), patch("lucidlib.scriptbindings.run_layout_inventory") as inventory:
                        with self.assertRaises(ValueError):
                            scriptbindings.resolve_bindings(*fixture[:4], input_fingerprint=self.input_fingerprint)
                        inventory.assert_not_called()
                finally:
                    source.write_bytes(raw)

    def test_image_layout_and_loaded_identity_mismatches_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            fixture, rule = self.fixture(directory)
            comparison, path = fixture[4:6]
            original = copy.deepcopy(comparison)
            mutations = [
                lambda c: c.update(status="blocked"),
                lambda c: c.update(issues=[{"code": "different_field"}]),
                lambda c: c["loaded_scripts"][0].update(class_resolved=False),
                lambda c: c["loaded_scripts"][0].update(guid="0" * 32),
                lambda c: c["loaded_scripts"][0].update(file_id=0),
                lambda c: c["loaded_scripts"][0].update(source_sha256="0" * 64),
                lambda c: c["loaded_scripts"][0].update(package_builtin=False),
                lambda c: c["loaded_scripts"][0].update(package_version="2.0.0"),
                lambda c: c["loaded_scripts"][0].update(package_resolved_path="/unrelated/package"),
                lambda c: c["loaded_scripts"].append(copy.deepcopy(c["loaded_scripts"][0])),
            ]
            for index, mutate in enumerate(mutations):
                value = copy.deepcopy(original)
                mutate(value["candidates"][-1])
                path.write_text(json.dumps(value))
                with self.subTest(index=index), self.assertRaises(ValueError):
                    self.resolve(fixture)
            path.write_text(json.dumps(original))


if __name__ == "__main__":
    unittest.main()

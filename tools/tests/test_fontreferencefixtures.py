"""Portable coverage of the actual converter-produced, test-owned import inputs."""
import base64
import copy
import hashlib
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.fontreferencefixtures import build_import_fixture
from lucidlib import fontreferences


class FontReferenceImportFixtureTests(unittest.TestCase):
    def test_lf_and_crlf_fixtures_are_actual_converter_outputs_and_exact_inverses(self):
        guid = "0123456789abcdef0123456789abcdef"
        fixture = build_import_fixture(guid)
        before = copy.deepcopy(fixture)
        self.assertEqual(1, fixture["schema_version"])
        self.assertEqual("ProjectLucid.fontreferences.import-fixture", fixture["generated_by"])
        self.assertEqual(guid, fixture["carrier_script_guid"])
        self.assertEqual([format(index + 1, "032x") for index in range(4)], fixture["keys"])
        self.assertEqual(["LF", "CRLF"], [variant["name"] for variant in fixture["variants"]])
        for variant in fixture["variants"]:
            with self.subTest(variant=variant["name"]):
                original = base64.b64decode(variant["original_base64"], validate=True)
                converted = base64.b64decode(variant["converted_base64"], validate=True)
                self.assertEqual(hashlib.sha256(original).hexdigest(), variant["original_sha256"])
                self.assertEqual(hashlib.sha256(converted).hexdigest(), variant["converted_sha256"])
                actual = fontreferences.convert_pinned_owner(original, variant["original_sha256"], fixture["keys"])
                self.assertEqual(actual["converted"], converted)
                self.assertEqual(original, fontreferences.reverse_pinned_owner(actual))
                self.assertEqual(4, variant["fragment_count"])
                self.assertFalse(variant["script_references_modified"])
                self.assertFalse(variant["player_serialized_profile_verified"])
                self.assertFalse(variant["runtime_verified"])
                self.assertIn(("m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}").encode(), original)
                self.assertNotIn(b"m_SubObjectName: ", original)
                self.assertNotIn(b"m_SubObjectType: ", original)
                if variant["name"] == "LF": self.assertNotIn(b"\r", original + converted)
                else: self.assertNotIn(b"\n", (original + converted).replace(b"\r\n", b""))
        self.assertEqual(before, fixture)
        self.assertEqual(fixture, build_import_fixture(guid))

    def test_invalid_carrier_identity_is_rejected_before_fixture_creation(self):
        for guid in (None, b"0" * 32, "0" * 31, "A" * 32, "z" * 32, "0" * 32 + "\n"):
            with self.subTest(guid=guid):
                with self.assertRaises(ValueError):
                    build_import_fixture(guid)


if __name__ == "__main__":
    unittest.main()

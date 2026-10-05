import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.verification import artifact_fingerprint, test_verdict, verified_receipt


class VerificationTests(unittest.TestCase):
    def xml(self, path, names, result="Passed"):
        cases = ''.join('<test-case fullname="' + name + '" result="Passed" />' for name in names)
        path.write_text('<test-run result="' + result + '" failed="0">' + cases + '</test-run>')

    def test_identity_hashes_content_separately_from_prepared_assets(self):
        with tempfile.TemporaryDirectory(prefix="Lucid project ") as temporary:
            root = Path(temporary)
            (root / "Assets/Recovered").mkdir(parents=True)
            (root / "Assets/Source.cs").write_text("maintained code")
            asset = root / "Assets/Recovered/scene.unity"
            asset.write_text("first asset")
            source = artifact_fingerprint(root)
            prepared = artifact_fingerprint(root, True)
            asset.write_text("changed asset")
            self.assertEqual(source, artifact_fingerprint(root))
            self.assertNotEqual(prepared, artifact_fingerprint(root, True))
            (root / "Assets/Source.cs").write_text("changed code")
            self.assertNotEqual(source, artifact_fingerprint(root))

    def test_identity_rejects_symlink_instead_of_hashing_outside_project(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "Assets").mkdir()
            (root / "outside.cs").write_text("outside")
            (root / "Assets/Link.cs").symlink_to(root / "outside.cs")
            with self.assertRaisesRegex(ValueError, "symlink"):
                artifact_fingerprint(root)

    def test_passing_unrelated_suite_does_not_prove_required_game_behavior(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            self.xml(path, ["Unrelated.RequiredTest"])
            result = test_verdict(path, ["Game.OriginalStartup"])
            self.assertEqual("incomplete", result["status"])
            self.assertEqual(["Game.OriginalStartup"], result["missing_required_tests"])
            self.xml(path, ["Game.OriginalStartup"])
            self.assertEqual("complete", test_verdict(path, ["Game.OriginalStartup"])["status"])

    def test_malformed_or_entity_xml_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for value in ('<test-run', '<!DOCTYPE test-run [<!ENTITY x "data">]><test-run />'):
                path.write_text(value)
                with self.assertRaises(ValueError):
                    test_verdict(path, [])

    def test_changed_content_invalidates_generated_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            (work / "reports").mkdir()
            receipt = {"generated_by": "ProjectLucid.run_tests", "status": "complete",
                       "unity_version": "2022.3.54f1", "source_fingerprint": "older",
                       "prepared_asset_fingerprint": "same"}
            (work / "reports/editmode-verification.json").write_text(json.dumps(receipt))
            with self.assertRaisesRegex(ValueError, "stale"):
                verified_receipt(work, "editmode", {"source_fingerprint": "newer", "prepared_asset_fingerprint": "same"})


if __name__ == "__main__":
    unittest.main()

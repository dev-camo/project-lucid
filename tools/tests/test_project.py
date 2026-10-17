import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.project import build_project, validate_project
from lucidlib import project, verification
from lucidlib.assets import EXPORT_SETTINGS, fingerprint_manifest, _tool_lock
from lucidlib.inspection import _manifest


class ProjectTests(unittest.TestCase):
    def fixture(self, root):
        work = root / "cache"
        (work / "reports").mkdir(parents=True)
        (work / "reports" / "inspection.json").write_text(json.dumps({
            "addressables": {"complete": True, "catalog_sha256": "a" * 64, "missing_bundles": []}}))
        app = root / "Fixture.app"
        player = app / "Contents/Resources/Data/globalgamemanagers"
        player.parent.mkdir(parents=True)
        player.write_bytes(b"original player data")
        export = work / "export"
        (export / "Assets").mkdir(parents=True)
        (work / "assets").mkdir()
        mapping = work / "assets" / "asset-map.json"
        mapping.write_text(json.dumps({"assets": [{"guid": "b" * 32}], "duplicate_guids": {},
                                       "catalog_sha256": "a" * 64}))
        (work / "assets" / "latest-assets.json").write_text(json.dumps({
            "status": "exported", "project_path": str(export), "asset_map_path": str(mapping),
            "input_path": str(app), "input_fingerprint": fingerprint_manifest(_manifest(app)),
            "tool": _tool_lock(), "settings": EXPORT_SETTINGS}))
        return work, player, mapping

    @staticmethod
    def isolation(work, mode, names):
        """Supplied isolation lines for each name, as a native run writes them; returns the receipt's isolation entry.

        Each side effect for this mode is reported once, by the first name in a
        fixture allowed to cause it, with exactly its changes; an allowlisted
        test leaks exactly its listed changes; every other line is clean.
        """
        nonce = mode + "-synthetic-nonce"
        path = work / "reports" / (mode + "-isolation.jsonl")
        pending = dict(verification.ISOLATION_SIDE_EFFECTS)
        lines = []
        for name in names:
            changes = list(verification.ISOLATION_ALLOWLIST[name]["changes"]) if name in verification.ISOLATION_ALLOWLIST else []
            effect = next((key for key, entry in pending.items()
                           if any(name.startswith(fixture + ".") for fixture in entry["fixtures"].get(mode, ()))), None)
            if not changes and effect is not None:
                changes = list(pending.pop(effect)["changes"])
            lines.append(json.dumps({"nonce": nonce, "test": name, "status": "leak" if changes else "clean",
                                     "changes": changes}) + "\n")
        path.write_text("".join(lines))
        return {"nonce": nonce, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}

    def test_release_build_never_runs_editor_when_game_unresolved(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with patch("lucidlib.project.subprocess.run") as run:
                result = build_project(root, root / "cache", "macos")
            self.assertEqual("blocked", result["status"])
            run.assert_not_called()
            self.assertFalse((root / "Builds").exists())

    def test_extraction_result_cannot_establish_game_readiness(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work, _, _ = self.fixture(root)
            self.assertEqual("complete", validate_project(root, work, "extraction")["status"])
            self.assertEqual("failed", validate_project(root, work, "release")["status"])

    def test_contradictory_xml_blocks_real_release_validation_and_build(self):
        # Complete-shaped inert evidence isolates the XML guard; no Editor runs.
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _, _ = self.fixture(root)
            (root / "Assets/Recovered").mkdir(parents=True)
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + verification.UNITY_VERSION)
            (root / "Packages").mkdir()
            (root / "Packages/packages-lock.json").write_text("{}")
            identity = verification.current_identity(root)
            audit = {"status": "complete", "unresolved_references": 0,
                     "unity_version": verification.UNITY_VERSION, **identity}
            (work / "reports/unity-reference-audit.json").write_text(json.dumps(audit))
            from xml.sax.saxutils import quoteattr
            for mode, names in (("editmode", verification.EDITMODE_TESTS), ("playmode", verification.PLAYMODE_TESTS)):
                cases = ''.join('<test-case fullname=' + quoteattr(name) + ' result="Passed"/>' for name in names)
                xml = work / "reports" / (mode + ".xml")
                xml.write_text('<test-run result="Passed" failed="0">' + cases + '</test-run>')
                receipt = {"generated_by": "ProjectLucid.run_tests", "status": "complete",
                           "unity_version": verification.UNITY_VERSION, **identity,
                           "verdict": {"xml_sha256": hashlib.sha256(xml.read_bytes()).hexdigest()},
                           "isolation": self.isolation(work, mode, names)}
                (work / "reports" / (mode + "-verification.json")).write_text(json.dumps(receipt))
            self.assertEqual("complete", validate_project(root, work)["status"])
            xml = work / "reports/playmode.xml"
            valid = xml.read_text()
            for contradiction in ('<test-case fullname="Other.Test" result="Failed"/>',
                                  '<test-suite result="Failed"/>',
                                  '<test-case fullname=' + quoteattr(verification.PLAYMODE_TESTS[0]) + ' result="Skipped"/>'):
                with self.subTest(contradiction=contradiction):
                    xml.write_text(valid.replace('</test-run>', contradiction + '</test-run>'))
                    receipt_path = work / "reports/playmode-verification.json"
                    receipt = json.loads(receipt_path.read_text())
                    receipt["verdict"]["xml_sha256"] = hashlib.sha256(xml.read_bytes()).hexdigest()
                    receipt_path.write_text(json.dumps(receipt))
                    result = validate_project(root, work)
                    self.assertEqual("failed", result["status"])
                    self.assertTrue(any("playmode" in error and "incomplete" in error for error in result["errors"]))
                    with patch("lucidlib.project.find_editor") as editor, patch("lucidlib.project.subprocess.run") as run:
                        built = build_project(root, work, "macos")
                    self.assertEqual("blocked", built["status"])
                    editor.assert_not_called()
                    run.assert_not_called()
                    self.assertFalse((root / "Builds").exists())
                    self.assertFalse((work / "reports/release-validation.json").exists())

    def test_wrong_script_importer_blocks_real_release_and_build(self):
        # Supplied inert receipts isolate the metadata gate; no Editor executes.
        from xml.sax.saxutils import quoteattr
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _, _ = self.fixture(root)
            (root / "Assets/Recovered").mkdir(parents=True)
            (root / "Assets/Test.cs").write_text("// inert source; never compiled\n")
            (root / "Assets/Test.cs.meta").write_text(
                "fileFormatVersion: 2\nguid: " + "1" * 32 + "\nMonImporter:\n  executionOrder: 0\n")
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + verification.UNITY_VERSION)
            (root / "Packages").mkdir()
            (root / "Packages/packages-lock.json").write_text("{}")
            identity = verification.current_identity(root)
            (work / "reports/unity-reference-audit.json").write_text(json.dumps(
                {"status": "complete", "unresolved_references": 0,
                 "unity_version": verification.UNITY_VERSION, **identity}))
            for mode, names in (("editmode", verification.EDITMODE_TESTS), ("playmode", verification.PLAYMODE_TESTS)):
                xml = work / "reports" / (mode + ".xml")
                xml.write_text('<test-run result="Passed" failed="0">' + ''.join(
                    '<test-case fullname=' + quoteattr(name) + ' result="Passed"/>' for name in names) + '</test-run>')
                (work / "reports" / (mode + "-verification.json")).write_text(json.dumps(
                    {"generated_by": "ProjectLucid.run_tests", "status": "complete",
                     "unity_version": verification.UNITY_VERSION, **identity,
                     "verdict": {"xml_sha256": hashlib.sha256(xml.read_bytes()).hexdigest()},
                     "isolation": self.isolation(work, mode, names)}))
            validation = validate_project(root, work)
            self.assertEqual("failed", validation["status"])
            self.assertTrue(any("script metadata" in error and "MonImporter" in error for error in validation["errors"]))
            with patch.object(project, "find_editor", side_effect=AssertionError("Editor lookup forbidden")) as editor, patch.object(
                    project.subprocess, "run", side_effect=AssertionError("process launch forbidden")) as run:
                built = build_project(root, work, "macos")
            self.assertEqual("blocked", built["status"])
            editor.assert_not_called()
            run.assert_not_called()
            self.assertFalse((root / "Builds").exists())
            self.assertFalse((work / "reports/release-validation.json").exists())
            # A corrected header and refreshed supplied evidence pass this guard.
            meta = root / "Assets/Test.cs.meta"
            meta.write_text(meta.read_text().replace("MonImporter:", "MonoImporter:"))
            identity = verification.current_identity(root)
            for report_name in ("unity-reference-audit.json", "editmode-verification.json", "playmode-verification.json"):
                path = work / "reports" / report_name
                supplied = json.loads(path.read_text())
                supplied.update(identity)
                path.write_text(json.dumps(supplied))
            corrected = validate_project(root, work)
            self.assertEqual("complete", corrected["status"], corrected["errors"])
            self.assertEqual(1, corrected["automated_checks"]["script_metadata"]["counts"]["script_expanded"])

    def test_validation_rejects_input_changed_after_export(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work, player, _ = self.fixture(root)
            player.write_bytes(b"modified player data")
            result = validate_project(root, work, "extraction")
            self.assertEqual("failed", result["status"])
            self.assertTrue(any("differs" in error for error in result["errors"]))

    def test_duplicate_guids_cannot_pass_extraction_validation(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work, _, mapping = self.fixture(root)
            data = json.loads(mapping.read_text())
            data["duplicate_guids"] = {"b" * 32: ["first.asset", "second.asset"]}
            mapping.write_text(json.dumps(data))
            result = validate_project(root, work, "extraction")
            self.assertEqual("failed", result["status"])
            self.assertIn("Export contains duplicate asset GUIDs", result["errors"])


if __name__ == "__main__":
    unittest.main()

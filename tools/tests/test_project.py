import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sdtlib.project import build_project, validate_project
from sdtlib.assets import EXPORT_SETTINGS, fingerprint_manifest, _tool_lock
from sdtlib.inspection import _manifest


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
        (root / "reconstruction-status.json").write_text(json.dumps({
            "release_ready": False, "behaviors": {"movement": "unresolved"}}))
        return work, player, mapping

    def test_release_build_never_runs_editor_when_game_unresolved(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "reconstruction-status.json").write_text(json.dumps({
                "release_ready": False, "behaviors": {"movement": "unresolved"}}))
            with patch("sdtlib.project.subprocess.run") as run:
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

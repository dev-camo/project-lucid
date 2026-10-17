from pathlib import Path
from types import SimpleNamespace
import json
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap, project


class BuildDestinationTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="lucid build spaces ")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.work = self.root / ".cache/project-lucid"
        self.stage = self.root / "output/linux/.stage-fixture"
        self.stage.mkdir(parents=True)
        for obj, name, value in ((bootstrap, "REPO_ROOT", self.root), (bootstrap, "CACHE_ROOT", self.work),
                                 (project, "validate_project", lambda *a: {"status": "complete"}),
                                 (project, "find_editor", lambda: self.root / "synthetic-editor")):
            patcher = patch.object(obj, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def test_fresh_destination_is_passed_as_one_buildplayer_argument(self):
        destination = self.stage / "ProjectLucid.x86_64"
        def build(command, **kwargs):
            self.assertEqual(str(destination), command[command.index("-lucidOutput") + 1])
            self.assertIn("ProjectLucid.Editor.LucidBuild.Build", command)
            self.record(command, "linux", destination, "")
            destination.write_bytes(b"synthetic BuildPlayer result")
            return SimpleNamespace(returncode=0)
        with patch.object(project.subprocess, "run", side_effect=build):
            result = project.build_project(self.root, self.work, "linux", destination=destination)
        self.assertEqual("complete", result["status"])
        self.assertEqual(str(destination), result["output_dir"])

    @staticmethod
    def record(command, target, destination, architecture):
        # Mirrors the JsonUtility BuildRecord LucidBuild writes after BuildPlayer succeeds.
        Path(command[command.index("-lucidBuildRecord") + 1]).write_text(json.dumps(
            {"target": target, "output": str(destination), "architecture": architecture}))

    def macos_build(self, architecture, mutate=None):
        destination = self.root / "output/macos/.stage-fixture/ProjectLucid.app"
        destination.parent.mkdir(parents=True)
        def build(command, **kwargs):
            if mutate:
                mutate()
            if architecture is not None:
                self.record(command, "macos", destination, architecture)
            destination.mkdir()
            return SimpleNamespace(returncode=0)
        with patch.object(project.subprocess, "run", side_effect=build):
            return project.build_project(self.root, self.work, "macos", destination=destination)

    def test_macos_build_reports_the_universal_architecture_it_used(self):
        result = self.macos_build("x64ARM64")
        self.assertEqual("complete", result["status"])
        self.assertEqual("x64ARM64", result["architecture"])
        self.assertEqual([], result["errors"])

    def test_macos_build_with_another_or_unreported_architecture_fails(self):
        for architecture in ("x64", "ARM64", "", None):
            with self.subTest(architecture=architecture):
                result = self.macos_build(architecture)
                self.assertEqual("failed", result["status"])
                self.assertTrue(result["errors"])
                shutil.rmtree(self.root / "output/macos")

    def test_stale_build_record_is_not_reused(self):
        stale = self.work / "reports/build-macos.json"
        stale.parent.mkdir(parents=True, exist_ok=True)
        destination = self.root / "output/macos/.stage-fixture/ProjectLucid.app"
        stale.write_text(json.dumps({"target": "macos", "output": str(destination), "architecture": "x64ARM64"}))
        result = self.macos_build(None)
        self.assertEqual("failed", result["status"])
        self.assertIsNone(result["architecture"])

    def test_build_that_rewrites_project_settings_is_rejected(self):
        # The pre-fix builder ran PlayerSettings.SetArchitecture(Standalone, 2) after
        # the release receipt was checked, which serialises this map into the
        # fingerprinted ProjectSettings.asset.
        settings = self.root / "ProjectSettings/ProjectSettings.asset"
        settings.parent.mkdir()
        settings.write_text("  platformArchitecture: {}\n")
        result = self.macos_build("x64ARM64", mutate=lambda: settings.write_text(
            "  platformArchitecture:\n    Standalone: 2\n"))
        self.assertEqual("failed", result["status"])
        self.assertIn("The build changed maintained source after release validation", result["errors"])

    def test_existing_payload_and_nonempty_stage_are_never_overwritten(self):
        destination = self.stage / "ProjectLucid.x86_64"
        for path in (destination, self.stage / "previous-data.bin"):
            path.write_bytes(b"retain previous data")
            with patch.object(project.subprocess, "run") as launch, self.assertRaises(ValueError):
                project.build_project(self.root, self.work, "linux", destination=destination)
            launch.assert_not_called()
            self.assertEqual(b"retain previous data", path.read_bytes())
            path.unlink()

    def test_protected_directories_wrong_names_and_links_are_rejected(self):
        protected = [self.root / name / "ProjectLucid.x86_64" for name in
                     ("input", "Assets", "Packages", "ProjectSettings", "tools", ".cache", "Library", "Temp")]
        link = self.root / "linked-stage"
        link.symlink_to(self.stage, target_is_directory=True)
        for destination in protected + [self.stage / "wrong-name", link / "ProjectLucid.x86_64"]:
            with self.subTest(destination=destination), patch.object(project.subprocess, "run") as launch, \
                    self.assertRaises(ValueError):
                project.build_project(self.root, self.work, "linux", destination=destination)
            launch.assert_not_called()

    def test_custom_destination_does_not_bypass_the_full_release_guard(self):
        with patch.object(project, "validate_project", return_value={"status": "failed", "errors": ["missing full gameplay"]}), \
                patch.object(project.subprocess, "run") as launch:
            result = project.build_project(self.root, self.work, "linux", destination=self.stage / "ProjectLucid.x86_64")
        self.assertEqual("blocked", result["status"])
        launch.assert_not_called()
        self.assertEqual([], list(self.stage.iterdir()))


if __name__ == "__main__":
    unittest.main()

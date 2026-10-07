from pathlib import Path
from types import SimpleNamespace
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
            destination.write_bytes(b"synthetic BuildPlayer result")
            return SimpleNamespace(returncode=0)
        with patch.object(project.subprocess, "run", side_effect=build):
            result = project.build_project(self.root, self.work, "linux", destination=destination)
        self.assertEqual("complete", result["status"])
        self.assertEqual(str(destination), result["output_dir"])

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

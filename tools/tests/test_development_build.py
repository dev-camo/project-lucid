"""Development builds (owner decision D1): labelled, non-release, never accepted by port or release."""
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from lucidlib import bootstrap, port, project, verification
import test_project

TOOLS = Path(__file__).resolve().parents[1]


class DevelopmentBuildTests(unittest.TestCase):
    def ready(self, root):
        """A prepared project with valid input and asset identity but no audit and no test receipts."""
        work, _, mapping = test_project.ProjectTests().fixture(root)
        (root / "Assets/Recovered").mkdir(parents=True)
        (root / "ProjectSettings").mkdir()
        (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + verification.UNITY_VERSION)
        (root / "Packages").mkdir()
        (root / "Packages/packages-lock.json").write_text("{}")
        return work, mapping

    def build(self, root, work, target="linux", **options):
        def editor(command, **kwargs):
            if "-lucidOutput" not in command:  # the marker's commit lookup also goes through subprocess.run
                return SimpleNamespace(stdout="0123abc\n", returncode=0)
            self.command = command
            output = Path(command[command.index("-lucidOutput") + 1])
            record = Path(command[command.index("-lucidBuildRecord") + 1])
            output.mkdir(parents=True)
            record.write_text(json.dumps({"target": target, "output": str(output), "architecture": ""}))
            return SimpleNamespace(returncode=0)
        with patch.object(bootstrap, "REPO_ROOT", root), patch.object(bootstrap, "CACHE_ROOT", work), \
                patch.object(project, "find_editor", return_value=root / "editor"), \
                patch.object(project.subprocess, "run", side_effect=editor) as run:
            result = project.build_project(root, work, target, **options)
        return result, run

    def test_development_stage_skips_only_the_audit_and_test_gates(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _ = self.ready(root)
            self.assertEqual("complete", project.validate_project(root, work, "development")["status"])
            release = project.validate_project(root, work, "release")
            self.assertEqual("failed", release["status"])
            self.assertTrue(any("playmode" in error for error in release["errors"]))

    def test_development_stage_keeps_input_prepare_and_asset_identity_checks(self):
        def catalog(mapping, **changes):
            mapping.write_text(json.dumps({"assets": [{"guid": "b" * 32}], "duplicate_guids": {},
                                           "catalog_sha256": "a" * 64, **changes}))
        cases = {
            "prepare state": lambda root, work, mapping: shutil.rmtree(root / "Assets/Recovered"),
            "project version": lambda root, work, mapping: (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 1"),
            "dependency lock": lambda root, work, mapping: (root / "Packages/packages-lock.json").unlink(),
            "duplicate guids": lambda root, work, mapping: catalog(mapping, duplicate_guids={"x": 2}),
            "catalog": lambda root, work, mapping: catalog(mapping, catalog_sha256="c" * 64),
            "input inspection": lambda root, work, mapping: (work / "reports/inspection.json").unlink(),
        }
        for name, damage in cases.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary).resolve()
                work, mapping = self.ready(root)
                damage(root, work, mapping)
                result, run = self.build(root, work, development=True)
                self.assertEqual("blocked", result["status"])
                run.assert_not_called()
                self.assertFalse((root / "Builds").exists())

    def test_development_build_output_marker_and_editor_arguments(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _ = self.ready(root)
            result, _ = self.build(root, work, development=True)
            folder = root / "Builds/Development/linux"
            self.assertEqual("complete", result["status"], result["errors"])
            self.assertEqual(str(folder / "ProjectLucid-DEVELOPMENT.x86_64"), result["output_dir"])
            self.assertIn("-lucidDevelopment", self.command)
            receipt = json.loads((work / "reports/development-validation.json").read_text())
            self.assertEqual("development", receipt["stage"])
            self.assertFalse((work / "reports/release-validation.json").exists())
            marker = (folder / project.DEVELOPMENT_MARKER).read_text()
            self.assertIn("NOT A RELEASE", marker)
            self.assertIn("commit: ", marker)
            for gate in verification.PLAYMODE_TESTS[:6]:
                self.assertIn(gate, marker)
            self.assertIn("EditMode verification receipt: no test result was checked", marker)
            self.assertIn("PlayMode verification receipt: no test result was checked", marker)
            self.assertNotIn("Release gates not met", marker)

    def test_a_blocked_development_attempt_clears_the_previous_build(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _ = self.ready(root)
            folder = root / "Builds/Development/linux"
            folder.mkdir(parents=True)
            (folder / project.DEVELOPMENT_MARKER).write_text("older attempt")
            (folder / "ProjectLucid-DEVELOPMENT.x86_64").mkdir()
            shutil.rmtree(root / "Assets/Recovered")
            result, run = self.build(root, work, development=True)
            self.assertEqual("blocked", result["status"])
            run.assert_not_called()
            self.assertFalse(folder.exists())

    def test_release_build_still_refuses_while_the_gates_are_unwritten(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _ = self.ready(root)
            result, run = self.build(root, work)
            self.assertEqual("blocked", result["status"])
            run.assert_not_called()
            self.assertFalse((root / "Builds").exists())
            self.assertNotIn("-lucidDevelopment", getattr(self, "command", []))

    def test_port_and_release_refuse_development_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work, _ = self.ready(root)
            result, _ = self.build(root, work, development=True)
            built = Path(result["output_dir"])
            with self.assertRaises(ValueError):
                project.build_project(root, work, "linux", destination=built)
            with self.assertRaises(ValueError):
                project.build_project(root, work, "linux", destination=root / "x", development=True)
            # port's promotion check refuses a staged payload that carries the marker.
            stage = root / "output/linux/.stage"
            stage.mkdir(parents=True)
            (stage / "ProjectLucid.x86_64").write_bytes(b"player")
            (stage / "ProjectLucid_Data/Managed").mkdir(parents=True)
            (stage / "ProjectLucid_Data/globalgamemanagers").write_bytes(b"data")
            (stage / "ProjectLucid_Data/Managed/Game.Runtime.dll").write_bytes(b"dll")
            port._payload(stage, "linux")  # negative control: the same payload without the marker is accepted
            (stage / project.DEVELOPMENT_MARKER).write_text("x")
            with self.assertRaisesRegex(ValueError, "development build"):
                port._payload(stage, "linux")
            self.assertFalse(project.is_development_output(root / "output/clean/ProjectLucid.x86_64"))
            self.assertTrue(project.is_development_output(built.parent))
            self.assertTrue(project.is_development_output(built))
            self.assertTrue(project.is_development_output(built / "inner"))

    def test_editor_script_and_cli_wiring(self):
        source = (TOOLS.parent / "Assets/Editor/LucidBuild.cs").read_text(encoding="utf-8")
        self.assertIn("BuildOptions.Development", source)
        self.assertIn('development ? "development" : "release"', source)
        sys.path.insert(0, str(TOOLS))
        import lucid
        parser = lucid.parser()
        self.assertTrue(parser.parse_args(["build", "--target", "macos", "--development"]).development)
        self.assertFalse(parser.parse_args(["build", "--target", "macos"]).development)
        with self.assertRaises(SystemExit), patch("sys.stderr"):
            parser.parse_args(["validate", "--development"])


if __name__ == "__main__":
    unittest.main()

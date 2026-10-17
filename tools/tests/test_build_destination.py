from pathlib import Path
from types import SimpleNamespace
import json
import plistlib
import re
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap, project

X86_64, ARM64 = 0x01000007, 0x0100000C


def universal(cpus):
    """A fat Mach-O whose slices are bare 64-bit headers with no load commands."""
    thin = [struct.pack("<IiiIIIII", 0xFEEDFACF, cpu, 0, 2, 0, 0, 0, 0) for cpu in cpus]
    offset = 8 + 20 * len(cpus)
    rows = b"".join(struct.pack(">iiIII", cpu, 0, offset + 32 * i, 32, 0) for i, cpu in enumerate(cpus))
    return struct.pack(">II", 0xCAFEBABE, len(cpus)) + rows + b"".join(thin)


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
        # Same field set LucidBuild.BuildRecord serialises; see test_build_record_matches_lucidbuild.
        values = {"target": target, "output": str(destination), "architecture": architecture}
        Path(command[command.index("-lucidBuildRecord") + 1]).write_text(
            json.dumps({field: values[field] for field in project.BUILD_RECORD_FIELDS}))

    @staticmethod
    def player(app, slices=(X86_64, ARM64), frameworks=("UnityPlayer.dylib",), executable=None):
        contents = app / "Contents"
        (contents / "MacOS").mkdir(parents=True)
        (contents / "Frameworks").mkdir()
        (contents / "Info.plist").write_bytes(plistlib.dumps({"CFBundleExecutable": "ProjectLucid"}))
        (contents / "MacOS/ProjectLucid").write_bytes(executable or universal(slices))
        for name in frameworks:
            (contents / "Frameworks" / name).write_bytes(universal(slices))

    def macos_build(self, architecture="x64ARM64", mutate=None, **player):
        stage = Path(tempfile.mkdtemp(prefix=".stage-", dir=self.root / "output"))
        destination = stage / "ProjectLucid.app"
        def build(command, **kwargs):
            if mutate:
                mutate()
            if architecture is not None:
                self.record(command, "macos", destination, architecture)
            self.player(destination, **player)
            return SimpleNamespace(returncode=0)
        with patch.object(project.subprocess, "run", side_effect=build):
            return project.build_project(self.root, self.work, "macos", destination=destination)

    def test_build_record_matches_lucidbuild(self):
        source = (Path(__file__).resolve().parents[2] / "Assets/Editor/LucidBuild.cs").read_text(encoding="utf-8")
        body = re.search(r"class BuildRecord\s*\{(.*?)\}", source, re.S).group(1)
        self.assertEqual(list(project.BUILD_RECORD_FIELDS), re.findall(r"public string (\w+);", body))

    def test_macos_build_reports_the_universal_slices_it_contains(self):
        result = self.macos_build(frameworks=("UnityPlayer.dylib", "GameAssembly.dylib"))
        self.assertEqual([], result["errors"])
        self.assertEqual("complete", result["status"])
        self.assertEqual("x64ARM64", result["architecture_setting"])
        self.assertEqual({"MacOS/ProjectLucid": ["arm64", "x86_64"],
                          "Frameworks/UnityPlayer.dylib": ["arm64", "x86_64"],
                          "Frameworks/GameAssembly.dylib": ["arm64", "x86_64"]}, result["slices"])

    def test_macos_build_without_both_slices_fails(self):
        for slices in ((X86_64,), (ARM64,)):
            with self.subTest(slices=slices):
                result = self.macos_build(slices=slices)
                self.assertEqual("failed", result["status"])
                self.assertIn("The macOS build binary MacOS/ProjectLucid has slices " +
                              ("x86_64" if slices == (X86_64,) else "arm64") + ", not arm64, x86_64",
                              result["errors"])
                self.assertNotIn("The build record does not identify this target and destination", result["errors"])

    def test_macos_build_without_a_readable_executable_fails(self):
        result = self.macos_build(executable=b"not Mach-O")
        self.assertEqual("failed", result["status"])
        self.assertEqual(1, len(result["errors"]))
        self.assertTrue(result["errors"][0].startswith("The macOS build binary MacOS/ProjectLucid is not readable Mach-O"))

    def test_missing_or_malformed_build_record_fails(self):
        for architecture in (None, 7):
            with self.subTest(architecture=architecture):
                result = self.macos_build(architecture)
                self.assertEqual("failed", result["status"])
                self.assertEqual(["The build record does not identify this target and destination"], result["errors"])

    def test_stale_build_record_is_not_reused(self):
        stale = self.work / "reports/build-macos.json"
        stale.parent.mkdir(parents=True, exist_ok=True)
        stale.write_text(json.dumps({"target": "macos", "output": "stale", "architecture": "x64ARM64"}))
        result = self.macos_build(None)
        self.assertEqual("failed", result["status"])
        self.assertFalse(stale.exists())
        self.assertIsNone(result["architecture_setting"])

    def test_build_that_rewrites_project_settings_is_rejected(self):
        # A build step that serialises a platformArchitecture entry into the
        # fingerprinted ProjectSettings.asset invalidates the release receipt.
        settings = self.root / "ProjectSettings/ProjectSettings.asset"
        settings.parent.mkdir()
        settings.write_text("  platformArchitecture: {}\n")
        result = self.macos_build(mutate=lambda: settings.write_text(
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

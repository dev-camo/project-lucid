"""Inert admission regressions; these fixtures do not qualify real executables."""

import hashlib
import json
import os
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock

from tools.lucidlib import bootstrap, native
from tools.tests import test_native_recovery as integration


class NativeIntegrationPreflightTests(unittest.TestCase):
    subject = integration

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="native-preflight-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.work = self.root / ".cache/project-lucid"
        self.work.mkdir(parents=True)
        self.original = self.root / "input/SonicDreamTeam.app/Contents/Frameworks/GameAssembly.dylib"
        self.metadata = self.root / "input/SonicDreamTeam.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"
        self.original.parent.mkdir(parents=True)
        self.metadata.parent.mkdir(parents=True)
        self.metadata.write_bytes(b"inert metadata fixture; never decoded")
        self.x64 = struct.pack("<IiiIIIII", 0xfeedfacf, 0x01000007, 3, 6, 0, 0, 0, 0) + b"inert x64"
        arm64 = struct.pack("<IiiIIIII", 0xfeedfacf, 0x0100000c, 0, 6, 0, 0, 0, 0) + b"inert arm64"
        fat = struct.pack(">II", 0xcafebabe, 2)
        fat += struct.pack(">iiIII", 0x01000007, 3, 48, len(self.x64), 0)
        fat += struct.pack(">iiIII", 0x0100000c, 0, 48 + len(self.x64), len(arm64), 0)
        self.original.write_bytes(fat + self.x64 + arm64)
        self.binary = self.work / "recovery/inputs" / (self.digest(self.original) + ".x86_64.dylib")
        self.binary.parent.mkdir(parents=True)
        self.binary.write_bytes(self.x64)
        self.control = self.root / "control"
        self.control.mkdir()
        self.project = self.control / "NativeRecovery.csproj"
        self.manifest = self.control / "SourcePin.props"
        for path in (self.project, self.manifest, self.control / "Program.cs"):
            path.write_text("inert control fixture; never built\n")
        self.dotnet = self.work / "tools/dotnet" / bootstrap.host_lock()["artifacts"]["dotnet"]["entrypoint"]
        self.dotnet.parent.mkdir(parents=True)
        self.dotnet.write_bytes(b"inert host fixture; never executed")
        (self.dotnet.parent / "host-dependency").write_bytes(b"inert host dependency")
        self.output = self.work / "native-recovery/builds/0123456789abcdef0123456789abcdef/build/bin/Release/net10.0"
        self.output.mkdir(parents=True)
        self.harness = self.output / native._HARNESS
        self.harness.write_bytes(b"inert harness fixture; never executed")
        (self.output / "dependency.dll").write_bytes(b"inert harness dependency")
        (self.output / "ProjectLucid.NativeRecovery.deps.json").write_text('{"inert": true}\n')
        self.pointer = self.work / "native-recovery/latest-build.json"
        patches = [mock.patch.object(bootstrap, "CACHE_ROOT", self.work),
                   mock.patch.object(bootstrap, "HOST_CACHE_ROOT", None),
                   mock.patch.object(bootstrap, "DIAGNOSTICS_ROOT", None),
                   mock.patch.object(bootstrap, "_INSTALL_SEALS", {}),
                   mock.patch.object(bootstrap, "_BUILD_SEALS", {}),
                   mock.patch.object(native, "_PROJECT", self.project),
                   mock.patch.object(native, "_MANIFEST", self.manifest),
                   mock.patch.object(self.subject, "ROOT", self.root),
                   mock.patch.object(self.subject, "CACHE", self.work),
                   mock.patch.object(self.subject, "METADATA", self.metadata),
                   mock.patch.object(self.subject, "DOTNET", self.dotnet, create=True)]
        for patch in patches:
            patch.start()
            self.addCleanup(patch.stop)
        # These synthetic seals represent a trusted controller in a unit test.
        # They are not archive verification, SDK approval or executable evidence.
        artifact = bootstrap.host_lock()["artifacts"]["dotnet"]
        identity = {key: artifact[key] for key in ("version", "sha256", "sha512") if key in artifact}
        bootstrap._INSTALL_SEALS[self.dotnet.parent] = (identity, bootstrap.tree_seal(self.dotnet.parent))
        identity = native._lock_identity(bootstrap.load_lock())
        identity.update(cpp2il_source_closure_sha256=hashlib.sha256(b"inert source closure").hexdigest(),
                        harness_sources={p.name: self.digest(p) for p in (self.project, self.manifest, self.control / "Program.cs")},
                        dotnet_host_sha256=self.digest(self.dotnet))
        self.fingerprint = hashlib.sha256(json.dumps(identity, sort_keys=True).encode()).hexdigest()
        self.report = {"status": "ready", "fingerprint": self.fingerprint, "path": str(self.harness),
                       "output_dir": str(self.output), "file_hashes": native._file_hashes(self.output, self.work), **identity}
        self.pointer.write_text(json.dumps(self.report))
        bootstrap.seal_built_tool(self.output, {"fingerprint": self.fingerprint})
        self.run = mock.patch.object(integration.subprocess, "run").start()
        self.addCleanup(mock.patch.stopall)
        self.download = mock.patch.object(bootstrap, "download_artifact", side_effect=AssertionError("no download allowed")).start()
        self.builder = mock.patch.object(native, "build_native_harness", side_effect=AssertionError("no build allowed")).start()

    @staticmethod
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def setup_rejected(self):
        with self.assertRaises((RuntimeError, ValueError, OSError)):
            self.subject.NativeRecoveryIntegrationTests.setUpClass()
        self.run.assert_not_called()
        self.download.assert_not_called()
        self.builder.assert_not_called()

    def admit(self):
        self.subject.NativeRecoveryIntegrationTests.setUpClass()
        self.run.assert_not_called()
        return self.subject.NativeRecoveryIntegrationTests()

    def test_changed_pointer_is_rejected_before_process(self):
        rogue = self.output / "arbitrary.dll"
        rogue.write_bytes(b"unsealed arbitrary fixture")
        self.report["path"] = str(rogue)
        self.pointer.write_text(json.dumps(self.report))
        self.setup_rejected()

    def test_changed_host_is_rejected_before_process(self):
        self.dotnet.write_bytes(b"changed host")
        self.setup_rejected()

    def test_changed_harness_is_rejected_before_process(self):
        self.harness.write_bytes(b"changed harness")
        self.setup_rejected()

    def test_changed_dependency_is_rejected_before_process(self):
        (self.output / "dependency.dll").write_bytes(b"changed dependency")
        self.setup_rejected()

    def test_changed_analysis_slice_is_rejected_before_process(self):
        self.binary.write_bytes(b"x" * len(self.x64))
        self.setup_rejected()

    def test_missing_process_seals_fail_instead_of_skipping_or_bootstrapping(self):
        for name in ("host", "harness"):
            with self.subTest(name=name):
                table = bootstrap._INSTALL_SEALS if name == "host" else bootstrap._BUILD_SEALS
                recorded = dict(table)
                table.clear()
                try:
                    self.setup_rejected()
                finally:
                    table.update(recorded)

    def _assert_current_native_lock_drift_rejected(self, field, changed):
        lock = bootstrap.load_lock()
        owned_lock = self.root / "tool-lock.json"
        owned_lock.write_text(json.dumps(lock))
        with mock.patch.object(bootstrap, "LOCK_FILE", owned_lock):
            instance = self.admit()
            instance.setUp()
            lock["artifacts"]["cpp2il-source"][field] = changed
            owned_lock.write_text(json.dumps(lock))
            # The maintained validator and the actual before-launch route must
            # both refuse drift; an admitted old build cannot waive the pin.
            with self.assertRaises(bootstrap.ToolError):
                native._lock_identity(bootstrap.load_lock())
            self.run.return_value = mock.Mock(stdout="", stderr="", returncode=0)
            with self.assertRaises(bootstrap.ToolError):
                instance.invoke()
            self.run.assert_not_called()
            self.download.assert_not_called()
            self.builder.assert_not_called()

    def test_current_cpp2il_pin_drift_is_rejected_before_process(self):
        self._assert_current_native_lock_drift_rejected("version", "changed inert source pin")

    def test_current_cpp2il_archive_drift_is_rejected_before_process(self):
        self._assert_current_native_lock_drift_rejected("sha256", "0" * 64)

    def test_stale_host_identity_cannot_trigger_archive_reconstitution(self):
        bootstrap._INSTALL_SEALS[self.dotnet.parent] = ({"version": "different"}, bootstrap.tree_seal(self.dotnet.parent))
        self.setup_rejected()

    def test_changed_host_dependency_and_unpinned_harness_source_are_rejected(self):
        (self.dotnet.parent / "host-dependency").write_bytes(b"changed")
        self.setup_rejected()
        artifact = bootstrap.host_lock()["artifacts"]["dotnet"]
        identity = {key: artifact[key] for key in ("version", "sha256", "sha512") if key in artifact}
        bootstrap._INSTALL_SEALS[self.dotnet.parent] = (identity, bootstrap.tree_seal(self.dotnet.parent))
        (self.control / "New.cs").write_bytes(b"unreviewed source")
        self.setup_rejected()

    def test_pointer_input_and_closure_changes_after_admission_stop_invoke(self):
        instance = self.admit()
        instance.setUp()
        paths = [self.pointer, self.original, self.metadata, self.binary, self.harness,
                 self.output / "dependency.dll", self.dotnet, self.dotnet.parent / "host-dependency",
                 self.project, self.manifest, self.control / "Program.cs", None]
        for path in paths:
            # Earlier cases deliberately create a new admitted test instance;
            # select its ARM64 file rather than a previous instance's fixture.
            path = instance.arm64 if path is None else path
            with self.subTest(path=path.name):
                previous = path.read_bytes()
                path.write_bytes(previous + b"changed")
                with self.assertRaises((RuntimeError, ValueError, OSError)):
                    instance.invoke()
                self.run.assert_not_called()
                path.write_bytes(previous)
                # Recreate the small synthetic admission after deliberately
                # restoring fixture bytes; filesystem identity also changes.
                if path in (self.pointer, self.original, self.metadata, self.binary, instance.arm64):
                    instance = self.admit()
                    instance.setUp()

    def test_physical_aliases_and_borrowed_mutation_paths_are_rejected(self):
        for path in (self.pointer, self.binary, self.metadata, self.harness):
            with self.subTest(path=path.name):
                moved = path.with_name(path.name + ".physical")
                path.rename(moved)
                path.symlink_to(moved)
                self.setup_rejected()
                path.unlink()
                moved.rename(path)
        instance = self.admit()
        instance.setUp()
        borrowed = self.work / "borrowed.dat"
        borrowed.write_bytes(b"borrowed inert fixture")
        with self.assertRaisesRegex(bootstrap.ToolError, "fresh directory"):
            instance.invoke(metadata=borrowed)
        self.run.assert_not_called()

    def test_owned_mutated_input_is_allowed_only_after_original_admission(self):
        instance = self.admit()
        instance.setUp()
        mutated = instance.directory / "corrupt-metadata.dat"
        mutated.write_bytes(b"inert corrupt fixture")
        self.run.return_value = mock.Mock(stdout="", stderr="", returncode=2)
        instance.invoke(metadata=mutated)
        self.run.assert_called_once()
        args, kwargs = self.run.call_args
        self.assertEqual(args[0][:2], [str(self.dotnet), str(self.harness)])
        self.assertEqual(kwargs["cwd"], instance.directory)
        self.assertEqual(kwargs["stdin"], integration.subprocess.DEVNULL)

    def test_environment_rejects_ambient_runtime_and_loader_channels(self):
        with mock.patch.dict(os.environ, {"DOTNET_STARTUP_HOOKS": "borrowed", "LD_PRELOAD": "borrowed",
                                         "DYLD_INSERT_LIBRARIES": "borrowed", "MSBuildSDKsPath": "borrowed",
                                         "NUGET_PACKAGES": "borrowed", "PATH": "borrowed"}):
            instance = self.admit()
        env = instance.environment
        for key in ("DOTNET_STARTUP_HOOKS", "LD_PRELOAD", "DYLD_INSERT_LIBRARIES", "MSBuildSDKsPath"):
            self.assertNotIn(key, env)
        self.assertEqual(env["PATH"], os.defpath)
        self.assertEqual(env["NUGET_PACKAGES"], str(instance.architecture_dir / "nuget-packages"))
        self.assertEqual(env["DOTNET_ROOT"], str(self.dotnet.parent))

    def test_default_discovery_does_not_opt_in_to_native_execution(self):
        self.assertNotEqual(os.environ.get("LUCID_NATIVE_INTEGRATION"), "1")
        self.assertTrue(integration.NativeRecoveryIntegrationTests.__unittest_skip__)


if __name__ == "__main__":
    unittest.main()

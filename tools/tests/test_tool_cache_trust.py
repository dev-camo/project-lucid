"""Inert archive and byte fixtures; no supplied executable is ever launched."""

from contextlib import ExitStack
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import tarfile
import tempfile
import unittest
from unittest import mock

from tools.lucidlib import bootstrap, recovery, native, codeprogress, playerschema, runtimecontracts
from types import SimpleNamespace


class ToolCacheTrustTests(unittest.TestCase):
    def setUp(self):
        bootstrap.CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.directory = Path(tempfile.mkdtemp(prefix="test-tool-trust-", dir=bootstrap.CACHE_ROOT))
        self.work = self.directory / "work"
        self.work.mkdir()
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.addCleanup(shutil.rmtree, self.directory)
        self.spawn = self.stack.enter_context(mock.patch.object(bootstrap.subprocess, "Popen", side_effect=AssertionError("No fixture executable may run")))
        self.network = self.stack.enter_context(mock.patch("urllib.request.urlopen", side_effect=AssertionError("Offline fixtures only")))
        self.stack.enter_context(mock.patch.object(bootstrap, "host_name", return_value="fixture-host"))
        self.lock = {"platform": "fixture-host", "rid": "fixture-rid", "artifacts": {}}

    def archive(self, name, entries, entrypoint):
        payload = io.BytesIO()
        with tarfile.open(fileobj=payload, mode="w") as archive:
            for filename, data in entries.items():
                member = tarfile.TarInfo(filename)
                member.size = len(data)
                member.mode = 0o644
                archive.addfile(member, io.BytesIO(data))
        data = payload.getvalue()
        artifact = {"version": "fixture-pin", "filename": name + ".tar", "url": "https://example.invalid/unused",
                    "format": "tar", "entrypoint": entrypoint, "sha256": hashlib.sha256(data).hexdigest()}
        downloads = self.work / "downloads"
        downloads.mkdir(exist_ok=True)
        (downloads / artifact["filename"]).write_bytes(data)
        self.lock["artifacts"][name] = artifact
        return artifact

    def install_fixture(self):
        self.archive("dotnet", {"dotnet": b"inert verified host", "shared/runtime.dll": b"inert verified dependency"}, "dotnet")
        return bootstrap._install("dotnet", self.lock, self.work)

    def test_sdk_runtime_empty_metadata_directory_is_reserved_before_sealing(self):
        sdk = self.install_fixture()
        metadata = sdk / "metadata"
        self.assertTrue(metadata.is_dir())
        self.assertEqual([], list(metadata.iterdir()))
        expected = bootstrap.tree_seal(sdk)
        metadata.mkdir(exist_ok=True)  # Inert stand-in for the observed MSBuild mkdir.
        self.assertEqual(expected, bootstrap.tree_seal(sdk))
        self.assertEqual(sdk, bootstrap._install("dotnet", self.lock, self.work))
        (metadata / "substituted-task.dll").write_bytes(b"inert untrusted SDK dependency")
        with self.assertRaisesRegex(bootstrap.ToolError, "closure changed"):
            bootstrap._install("dotnet", self.lock, self.work)
        self.spawn.assert_not_called()

    def test_matching_public_marker_cannot_authorize_fake_sdk(self):
        artifact = self.archive("dotnet", {"dotnet": b"verified host", "runtime.dll": b"verified runtime"}, "dotnet")
        cached = self.work / "tools/dotnet"
        cached.mkdir(parents=True)
        (cached / "dotnet").write_bytes(b"inert contributor payload")
        (cached / "runtime.dll").write_bytes(b"inert substituted dependency")
        bootstrap.write_json(cached / "lucid-install.json", {key: artifact[key] for key in ("version", "sha256")})
        with self.assertRaisesRegex(bootstrap.ToolError, "closure differs"):
            bootstrap._install("dotnet", self.lock, self.work)
        self.spawn.assert_not_called()

    def test_archive_verified_install_can_be_revalidated_without_marker_authority(self):
        destination = self.install_fixture()
        bootstrap._INSTALL_SEALS.pop(destination)
        self.assertEqual(destination, bootstrap._install("dotnet", self.lock, self.work))
        self.network.assert_not_called()
        self.spawn.assert_not_called()

    def test_modified_added_removed_and_symlinked_dependencies_reject_before_execution(self):
        mutations = {
            "modified": lambda root: (root / "shared/runtime.dll").write_bytes(b"substituted DLL"),
            "added": lambda root: (root / "extra-plugin.dll").write_bytes(b"extra plugin"),
            "removed": lambda root: (root / "shared/runtime.dll").unlink(),
            "symlink": lambda root: ((root / "shared/runtime.dll").unlink(), (root / "shared/runtime.dll").symlink_to(root / "dotnet")),
            "hardlink": lambda root: os.link(root / "dotnet", root / "extra.dll"),
            "mode": lambda root: (root / "dotnet").chmod(0o755),
        }
        for name, mutate in mutations.items():
            with self.subTest(mutation=name):
                destination = self.install_fixture()
                mutate(destination)
                with self.assertRaises(bootstrap.ToolError):
                    bootstrap._install("dotnet", self.lock, self.work)
                bootstrap._INSTALL_SEALS.pop(destination, None)
                shutil.rmtree(destination)
        self.spawn.assert_not_called()

    def cpp2il_fixture(self):
        self.archive("dotnet", {"dotnet": b"verified SDK"}, "dotnet")
        self.archive("cpp2il-source", {"Cpp2IL/Cpp2IL.csproj": b"verified source"}, "Cpp2IL/Cpp2IL.csproj")
        self.addCleanup(self.release_leases)

    def release_leases(self):
        for path in list(bootstrap._CPP2IL_LEASES):
            if self.directory in path.parents:
                bootstrap._release_lease(bootstrap._CPP2IL_LEASES.pop(path))
                bootstrap._BUILD_SEALS.pop(path, None)

    def forget_process_state(self):
        """Mimic a new process while this one keeps holding its lease as a live owner."""
        held = []
        for path in list(bootstrap._CPP2IL_LEASES):
            if self.directory in path.parents:
                held.append(bootstrap._CPP2IL_LEASES.pop(path))
                bootstrap._BUILD_SEALS.pop(path, None)
        self.addCleanup(lambda: [bootstrap._release_lease(stream) for stream in held])
        return held

    @staticmethod
    def fixture_build(command, *args, **kwargs):
        stage = Path(command[command.index("-o") + 1])
        stage.mkdir(parents=True)
        bootstrap.executable(stage / "Cpp2IL").write_bytes(b"inert generated executable")
        (stage / "Dependency.dll").write_bytes(b"inert generated dependency")
        return {"returncode": 0, "timed_out": False, "log": "fixture"}

    def temporaries(self):
        tools = self.work / "tools"
        return sorted(path.name for path in [*tools.glob(".cpp2il-source-*"), *(tools / "cpp2il-builds").glob(".build-*")])

    def test_forged_legacy_cpp2il_output_is_never_selected(self):
        self.cpp2il_fixture()
        legacy = self.work / "tools/cpp2il"
        legacy.mkdir(parents=True)
        bootstrap.executable(legacy / "Cpp2IL").write_bytes(b"inert forged Cpp2IL")
        bootstrap.write_json(legacy / "lucid-build.json", {"commit": "fixture-pin", "sdk": "fixture-pin", "runtime": "fixture-rid", "build_schema": 2})
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build) as runner:
            result = bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertEqual(1, runner.call_count)
        self.assertIn(self.work / "tools/cpp2il-builds", Path(result["path"]).parents)
        self.assertEqual(b"inert generated executable", Path(result["path"]).read_bytes())
        self.assertEqual(b"inert forged Cpp2IL", bootstrap.executable(legacy / "Cpp2IL").read_bytes())
        self.assertEqual([], self.temporaries())
        self.spawn.assert_not_called()

    def test_symlinked_cpp2il_builds_root_is_rejected_before_build(self):
        self.cpp2il_fixture()
        target = self.work / "elsewhere"
        target.mkdir()
        (self.work / "tools").mkdir(exist_ok=True)
        (self.work / "tools/cpp2il-builds").symlink_to(target)
        with mock.patch.object(bootstrap, "run_logged") as runner, self.assertRaises((bootstrap.ToolError, ValueError)):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        runner.assert_not_called()
        self.assertEqual([], list(target.iterdir()))

    def test_generated_cpp2il_requires_complete_process_owned_seal(self):
        self.cpp2il_fixture()
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build) as runner:
            first = bootstrap._ensure_cpp2il(self.lock, self.work)
            self.assertEqual(first["path"], bootstrap._ensure_cpp2il(self.lock, self.work)["path"])
            self.assertEqual(1, runner.call_count)
            Path(first["path"]).with_name("Dependency.dll").write_bytes(b"substituted dependency")
            with self.assertRaisesRegex(bootstrap.ToolError, "changed during this process"):
                bootstrap._ensure_cpp2il(self.lock, self.work)
            self.assertEqual(1, runner.call_count)
        self.spawn.assert_not_called()

    def test_failed_publication_keeps_a_live_build_and_leaves_no_temporaries(self):
        self.cpp2il_fixture()
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            live = Path(bootstrap._ensure_cpp2il(self.lock, self.work)["path"])
        self.forget_process_state()
        original_rename = Path.rename
        def failing_rename(path, target):
            if path.name.startswith(".build-"):
                raise OSError("injected publication failure")
            return original_rename(path, target)
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build), \
                mock.patch.object(Path, "rename", failing_rename), self.assertRaisesRegex(OSError, "injected publication failure"):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertEqual(b"inert generated executable", live.read_bytes())
        builds = sorted(path.name for path in (self.work / "tools/cpp2il-builds").iterdir())
        self.assertEqual([live.parent.name, live.parent.name + ".lock"], builds)
        self.assertEqual([], self.temporaries())
        self.spawn.assert_not_called()

    def test_cleanup_failure_does_not_override_a_sealed_build(self):
        self.cpp2il_fixture()
        original_rmtree = shutil.rmtree
        def failing_rmtree(path, *args, **kwargs):
            if Path(path).name.startswith(".cpp2il-source-"):
                raise OSError("injected cleanup failure")
            return original_rmtree(path, *args, **kwargs)
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build), \
                mock.patch.object(bootstrap.shutil, "rmtree", failing_rmtree):
            result = bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertEqual("ready", result["status"])
        self.assertTrue(any("injected cleanup failure" in warning for warning in result["cleanup_warnings"]))
        self.assertEqual(result["path"], bootstrap._ensure_cpp2il(self.lock, self.work)["path"])
        self.assertEqual(1, len(self.temporaries()))
        # The next process prunes the leftover temporary while building its own copy.
        self.release_leases()
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertEqual([], self.temporaries())
        self.spawn.assert_not_called()

    def test_forged_inspector_marker_rejected_before_build_or_load(self):
        sdk = self.work / "tools/dotnet/dotnet"
        sdk.parent.mkdir(parents=True)
        sdk.write_bytes(b"inert fake SDK")
        helper = self.work / "tools/il-inspector"
        dll = helper / "bin/Release/net10.0/RecoveryInspector.dll"
        dll.parent.mkdir(parents=True)
        dll.write_bytes(b"inert forged reader")
        bootstrap.write_json(helper / "lucid-inspector.json", {"source_sha256": hashlib.sha256(recovery._INSPECTOR_SOURCE.encode()).hexdigest()})
        with mock.patch.object(recovery, "run_logged") as runner, self.assertRaisesRegex(bootstrap.ToolError, "Unverified cached managed-output inspector"):
            recovery._inspect_managed_output(self.work, self.work, "fixture")
        runner.assert_not_called()
        self.spawn.assert_not_called()

    def test_self_consistent_native_build_pointer_cannot_create_origin(self):
        output = self.work / "native-recovery/build"
        output.mkdir(parents=True)
        dll = output / native._HARNESS
        dll.write_bytes(b"inert forged harness")
        (output / "Cpp2IL.Core.dll").write_bytes(b"inert substituted dependency")
        pointer = self.work / "native-recovery/latest-build.json"
        bootstrap.write_json(pointer, {"status": "ready", "fingerprint": "public-fingerprint", "path": str(dll), "output_dir": str(output),
                                       "file_hashes": native._file_hashes(output, self.work)})
        self.assertIsNone(native._cached_build(pointer, self.work, "public-fingerprint"))
        self.spawn.assert_not_called()

    def test_host_scope_never_consumes_source_tools_packages_bundles_or_markers(self):
        for name in ("tools/dotnet", "tools/cpp2il", "tools/il-inspector", "native-recovery", "nuget-packages", "dotnet-bundles", "dotnet-home"):
            path = self.work / name
            path.mkdir(parents=True)
            (path / "inert-poison").write_bytes(b"contributor state")
        artifact = self.archive("dotnet", {"dotnet": b"verified host", "runtime.dll": b"verified dependency"}, "dotnet")
        with bootstrap.isolated_host_cache(self.directory) as host:
            # Seed only a pinned byte fixture in the NEW controller directory.
            (host / "downloads").mkdir()
            shutil.copyfile(self.work / "downloads" / artifact["filename"], host / "downloads" / artifact["filename"])
            tool = bootstrap._install("dotnet", self.lock, self.work)
            self.assertEqual(host / "tools/dotnet", tool)
            self.assertEqual(b"verified host", (tool / "dotnet").read_bytes())
            with mock.patch.dict(os.environ, {"HOME": str(self.work), "DOTNET_STARTUP_HOOKS": "inert-poison", "MSBuildSDKsPath": str(self.work), "NUGET_PACKAGES": str(self.work)}):
                env = bootstrap.dotnet_environment(self.work, tool)
            for key in ("HOME", "DOTNET_CLI_HOME", "NUGET_PACKAGES", "DOTNET_BUNDLE_EXTRACT_BASE_DIR", "XDG_CONFIG_HOME", "DOTNET_ROOT"):
                self.assertIn(host, Path(env[key]).parents)
            self.assertNotIn("DOTNET_STARTUP_HOOKS", env)
            self.assertNotIn("MSBuildSDKsPath", env)
            self.assertFalse(list(host.rglob("inert-poison")))
        self.assertFalse(host.exists())
        self.assertIsNone(bootstrap.HOST_CACHE_ROOT)
        self.spawn.assert_not_called()

    def test_reader_paths_use_control_project_and_host_tree_despite_source_ancestor_config(self):
        for name in ("Directory.Build.props", "Directory.Build.targets", "NuGet.Config", "global.json"):
            (self.work / name).write_text("inert contributor build configuration")
        run = self.work / "code-progress/runs/nonce"
        with bootstrap.isolated_host_cache(self.directory) as host:
            for name in ("code-progress", "player-schema", "theme-contracts"):
                project, work, build = bootstrap.reader_build_paths(self.directory, self.work, run, name)
                self.assertEqual(bootstrap.LOCK_FILE.parent / name, project)
                self.assertEqual(host, work)
                self.assertIn(host, build.parents)
                self.assertNotIn(self.work, project.parents)
        self.spawn.assert_not_called()

    def test_actual_reader_builders_select_only_host_executable_control_projects_and_host_outputs(self):
        run = self.work / "runs/fixture"
        run.mkdir(parents=True)
        for name in ("Directory.Build.props", "Directory.Build.targets", "NuGet.Config", "global.json"):
            (self.work / name).write_text("inert contributor config")
        with bootstrap.isolated_host_cache(self.directory) as host:
            sdk = host / "tools/dotnet/dotnet"
            sdk.parent.mkdir(parents=True)
            sdk.write_bytes(b"inert verified SDK stand-in")
            dependency = host / "native-dependency"
            dependency.mkdir()
            (dependency / "Cpp2IL.Core.dll").write_bytes(b"inert verified Core stand-in")
            engine = [{"name": "cecil", "path": str(self.directory / "trusted-engine/Unity.Cecil.dll")}]
            calls = []
            def invoke(command, **kwargs):
                calls.append((command, kwargs))
                self.assertEqual(str(sdk), command[0])
                self.assertEqual(host, kwargs["cwd"])
                self.assertIn(host, Path(kwargs["env"]["HOME"]).parents)
                if command[1] == "--version":
                    return SimpleNamespace(returncode=0, stdout=native._SDK)
                self.assertIn(bootstrap.LOCK_FILE.parent, Path(command[2]).parents)
                if "-o" in command:
                    output = Path(command[command.index("-o") + 1])
                    assembly = "ProjectLucid.CodeProgress.dll"
                else:
                    output = Path(next(part.split("=", 1)[1] for part in command if part.startswith("-p:OutputPath=")))
                    assembly = "ProjectLucid.PlayerSchema.dll" if "PlayerSchema.csproj" in command[2] else "ProjectLucid.ThemeContracts.dll"
                self.assertIn(host, output.parents)
                output.mkdir(parents=True, exist_ok=True)
                (output / assembly).write_bytes(b"inert generated reader")
                return SimpleNamespace(returncode=0)
            pinned = {"artifacts": {"dotnet": {"version": native._SDK, "sha512": "0" * 128}}}
            for module in (codeprogress, playerschema, runtimecontracts):
                with self.subTest(reader=module.__name__), ExitStack() as patches:
                    patches.enter_context(mock.patch.object(module, "verified_tool_path", return_value=sdk))
                    patches.enter_context(mock.patch.object(module, "host_lock", return_value=pinned))
                    patches.enter_context(mock.patch.object(module.subprocess, "run", side_effect=invoke))
                    patches.enter_context(mock.patch.object(native, "build_native_harness", return_value={"status": "ready", "path": str(dependency / native._HARNESS)}))
                    patches.enter_context(mock.patch.object(playerschema.playercode, "_pe_identity", return_value={"mvid": "fixture-mvid"}))
                    reader = module._build_reader(self.work, self.work, run, engine)
                    self.assertEqual(str(sdk), reader["host"]["path"])
            self.assertEqual(6, len(calls))
        self.spawn.assert_not_called()

    def test_private_failure_logs_survive_host_cache_disposal_without_symlink_reads(self):
        with bootstrap.isolated_host_cache(self.directory, diagnostics=True) as host:
            log = host / "reports/native-build.log"
            log.parent.mkdir()
            log.write_bytes(b"inert private failure log")
            report = bootstrap.retain_failure("failure.json", {"status": "failed"}, [log])
            diagnostics = Path(report).parent
            self.assertNotIn(host, diagnostics.parents)
            outside = self.directory / "outside-log"
            outside.write_bytes(b"must not be copied")
            redirect = host / "reports/redirect.log"
            redirect.symlink_to(outside)
            with self.assertRaises(ValueError):
                bootstrap.retain_failure("redirect.json", {"status": "failed"}, [redirect])
        self.assertFalse(host.exists())
        self.assertTrue(Path(report).is_file())
        self.assertEqual(0o700, diagnostics.stat().st_mode & 0o777)
        self.assertEqual(0o600, (diagnostics / "failure-0.log").stat().st_mode & 0o777)
        self.assertEqual(b"inert private failure log", (diagnostics / "failure-0.log").read_bytes())
        self.assertIsNone(bootstrap.DIAGNOSTICS_ROOT)

    def test_host_scope_rejects_symlinked_parent(self):
        link = self.directory / "redirect"
        link.symlink_to(self.work, target_is_directory=True)
        with self.assertRaisesRegex(bootstrap.ToolError, "symbolic link"):
            with bootstrap.isolated_host_cache(link):
                self.fail("No cache may be created through a link")
        self.assertIsNone(bootstrap.HOST_CACHE_ROOT)


if __name__ == "__main__":
    unittest.main()


class Cpp2ILFreshProcessTests(unittest.TestCase):
    """Real Python processes share one work tree; each mocks only the inert build."""

    archive = ToolCacheTrustTests.archive
    CHILD = """
import json, sys
from pathlib import Path
from unittest import mock
sys.path.insert(0, sys.argv[1])
from tools.lucidlib import bootstrap
work, lock, label = Path(sys.argv[2]), json.loads(Path(sys.argv[3]).read_text()), sys.argv[4]
def build(command, *args, **kwargs):
    stage = Path(command[command.index("-o") + 1])
    stage.mkdir(parents=True)
    bootstrap.executable(stage / "Cpp2IL").write_bytes(b"inert executable from " + label.encode())
    return {"returncode": 0, "timed_out": False, "log": "fixture"}
with mock.patch.object(bootstrap, "host_name", return_value="fixture-host"), \\
        mock.patch("urllib.request.urlopen", side_effect=AssertionError("offline")), \\
        mock.patch.object(bootstrap.subprocess, "Popen", side_effect=AssertionError("no executable may run")), \\
        mock.patch.object(bootstrap, "run_logged", side_effect=build) as runner:
    result = bootstrap._ensure_cpp2il(lock, work)
    again = bootstrap._ensure_cpp2il(lock, work)
print(json.dumps({"status": result["status"], "path": result["path"], "same": again["path"] == result["path"],
                  "builds": runner.call_count, "bytes": Path(result["path"]).read_text()}), flush=True)
if len(sys.argv) > 5:
    sys.stdin.readline()
    print(json.dumps({"still": Path(result["path"]).read_text()}), flush=True)
"""

    def setUp(self):
        bootstrap.CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.directory = Path(tempfile.mkdtemp(prefix="test-cpp2il-process-", dir=bootstrap.CACHE_ROOT))
        self.addCleanup(shutil.rmtree, self.directory)
        self.work = self.directory / "work"
        self.work.mkdir()
        self.lock = {"platform": "fixture-host", "rid": "fixture-rid", "artifacts": {}}
        self.archive("dotnet", {"dotnet": b"verified SDK"}, "dotnet")
        self.archive("cpp2il-source", {"Cpp2IL/Cpp2IL.csproj": b"verified source"}, "Cpp2IL/Cpp2IL.csproj")
        self.lock_file = self.directory / "lock.json"
        self.lock_file.write_text(json.dumps(self.lock))

    def command(self, label, *hold):
        import sys
        return [sys.executable, "-c", self.CHILD, str(bootstrap.REPO_ROOT), str(self.work), str(self.lock_file), label, *hold]

    def run_process(self, label):
        import subprocess
        process = subprocess.run(self.command(label), capture_output=True, text=True, timeout=120)
        self.assertEqual(0, process.returncode, process.stderr)
        return json.loads(process.stdout.strip().splitlines()[-1])

    def check(self, result, label):
        self.assertEqual("ready", result["status"])
        self.assertTrue(result["same"])
        self.assertEqual(1, result["builds"])
        self.assertEqual("inert executable from " + label, result["bytes"])

    def test_second_process_builds_its_own_copy_instead_of_refusing(self):
        first = self.run_process("bootstrap")
        second = self.run_process("recover-code")
        self.check(first, "bootstrap")
        self.check(second, "recover-code")
        self.assertNotEqual(first["path"], second["path"])
        # The first owner has exited, so the second process pruned its build.
        self.assertFalse(Path(first["path"]).exists())
        leftovers = [path.name for path in (self.work / "tools").rglob(".*") if "cpp2il" in path.name]
        self.assertEqual([], leftovers)

    def test_live_consumer_keeps_its_selected_executable_while_another_process_builds(self):
        import subprocess
        holder = subprocess.Popen(self.command("holder", "hold"), stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.PIPE, text=True)
        try:
            held = json.loads(holder.stdout.readline())
            self.check(held, "holder")
            other = self.run_process("other")
            self.check(other, "other")
            self.assertNotEqual(held["path"], other["path"])
            holder.stdin.write("go\n")
            holder.stdin.flush()
            self.assertEqual({"still": "inert executable from holder"}, json.loads(holder.stdout.readline()))
            self.assertEqual(0, holder.wait(timeout=60), holder.stderr.read())
        finally:
            if holder.poll() is None:
                holder.kill()
            holder.wait()
            for stream in (holder.stdin, holder.stdout, holder.stderr):
                stream.close()

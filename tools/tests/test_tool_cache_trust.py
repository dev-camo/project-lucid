"""Inert archive and byte fixtures; no supplied executable is ever launched."""

from contextlib import ExitStack, contextmanager
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import stat
import tarfile
import tempfile
import threading
import unittest
import zipfile
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
        cached = bootstrap.install_path(self.work, "dotnet", self.lock)
        cached.mkdir(parents=True)
        (cached / "dotnet").write_bytes(b"inert contributor payload")
        (cached / "runtime.dll").write_bytes(b"inert substituted dependency")
        bootstrap.write_json(cached / "lucid-install.json", {key: artifact[key] for key in ("version", "sha256")})
        with self.assertRaisesRegex(bootstrap.ToolError, "closure differs"):
            bootstrap._install("dotnet", self.lock, self.work)
        self.spawn.assert_not_called()

    def older_pin_install(self):
        """Install the fixture SDK under a first pin, then move the lock to a second pin."""
        first = self.install_fixture()
        self.assertEqual(first, bootstrap.install_path(self.work, "dotnet", self.lock))
        before = bootstrap.tree_seal(first)
        self.archive("dotnet", {"dotnet": b"newer verified host", "shared/runtime.dll": b"newer verified dependency"}, "dotnet")
        self.lock["artifacts"]["dotnet"]["version"] = "fixture-pin-2"
        return first, before

    def test_new_pin_installs_beside_an_older_pin_without_touching_it(self):
        first, before = self.older_pin_install()
        second = bootstrap._install("dotnet", self.lock, self.work)
        self.assertNotEqual(first, second)
        self.assertEqual(first.parent, second.parent)
        self.assertEqual(b"newer verified host", (second / "dotnet").read_bytes())
        self.assertEqual(before, bootstrap.tree_seal(first))
        self.spawn.assert_not_called()

    def test_unversioned_layout_of_older_checkouts_is_neither_trusted_nor_blocking(self):
        legacy = self.work / "tools/dotnet"
        legacy.mkdir(parents=True)
        (legacy / "dotnet").write_bytes(b"inert legacy payload")
        self.archive("dotnet", {"dotnet": b"verified host"}, "dotnet")
        installed = bootstrap._install("dotnet", self.lock, self.work)
        self.assertEqual(b"verified host", (installed / "dotnet").read_bytes())
        self.assertEqual(b"inert legacy payload", (legacy / "dotnet").read_bytes())

    def test_bootstrap_removes_only_superseded_installs_of_the_same_tool(self):
        first, _ = self.older_pin_install()
        legacy = self.work / "tools/dotnet"
        legacy.mkdir()
        (legacy / "dotnet").write_bytes(b"inert legacy payload")
        unrelated = self.work / "tools/dotnet-builds"
        unrelated.mkdir()
        outside = self.directory / "outside"
        outside.mkdir()
        (outside / "keep").write_bytes(b"must survive")
        link = self.work / "tools/dotnet-fixture-pin-0000000000aa"
        link.symlink_to(outside, target_is_directory=True)
        # A link nested inside a superseded tree is removed as an entry, never followed.
        (first / "nested-link").symlink_to(outside, target_is_directory=True)
        with mock.patch.object(bootstrap, "load_lock", return_value=self.lock):
            result = bootstrap.bootstrap(self.work, ["dotnet"])
        self.assertEqual("ready", result["status"], result)
        current = bootstrap.install_path(self.work, "dotnet", self.lock)
        self.assertTrue(current.is_dir())
        self.assertFalse(first.exists())
        self.assertFalse(legacy.exists())
        self.assertTrue(unrelated.is_dir())
        self.assertTrue(link.is_symlink())
        self.assertEqual(b"must survive", (outside / "keep").read_bytes())

    def test_pruning_a_tool_keeps_installs_of_tools_whose_names_extend_it(self):
        # "assetripper-source-<pin>-<digest>" and "cpp2il-source-..." fit the "<name>-<version>-<digest>" shape.
        kept = {}
        for short, long in (("assetripper", "assetripper-source"), ("cpp2il", "cpp2il-source")):
            self.archive(short, {"tool": short.encode()}, "tool")
            self.archive(long, {"tool": long.encode()}, "tool")
            kept[long] = bootstrap.install_path(self.work, long, self.lock)
            current = bootstrap.install_path(self.work, short, self.lock)
            kept[long].mkdir(parents=True)
            current.mkdir()
            stale_short = self.work / f"tools/{short}-older-pin-0123456789ab"
            stale_long = self.work / f"tools/{long}-older-pin-0123456789ab"
            stale_short.mkdir()
            stale_long.mkdir()
            self.assertEqual([], bootstrap._prune_superseded(short, self.lock, self.work))
            self.assertTrue(current.is_dir())
            self.assertTrue(kept[long].is_dir(), long)
            self.assertTrue(stale_long.is_dir(), long)  # not the shorter tool's to prune
            self.assertFalse(stale_short.exists(), short)
            # The longer tool still prunes its own superseded pins.
            self.assertEqual([], bootstrap._prune_superseded(long, self.lock, self.work))
            self.assertTrue(kept[long].is_dir())
            self.assertFalse(stale_long.exists(), long)
            self.assertTrue(current.is_dir())

    def test_failed_pruning_is_reported_without_failing_the_bootstrap(self):
        first, _ = self.older_pin_install()
        with mock.patch.object(bootstrap, "load_lock", return_value=self.lock), \
                mock.patch.object(bootstrap, "_remove_unpacked_tree", side_effect=PermissionError("denied")):
            result = bootstrap.bootstrap(self.work, ["dotnet"])
        self.assertEqual("ready", result["status"], result)
        self.assertIn(str(first), result["tools"]["dotnet"]["cleanup_warnings"][0])
        self.assertTrue(first.is_dir())

    def test_archive_verified_install_can_be_revalidated_without_marker_authority(self):
        destination = self.install_fixture()
        bootstrap._INSTALL_SEALS.pop(destination)
        self.assertEqual(destination, bootstrap._install("dotnet", self.lock, self.work))
        self.network.assert_not_called()
        self.spawn.assert_not_called()

    def test_modified_added_removed_and_symlinked_dependencies_reject_before_execution(self):
        # Within one process the seal pop stands in for the second process of the issue.
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

    @contextmanager
    def umask(self, value):
        previous = os.umask(value)
        try:
            yield
        finally:
            os.umask(previous)

    def mode_archive(self, name, kind):
        """A tar or zip with explicit 0750, read-only 0555 (holding a file) and group-writable 0775 directories, and an implicit parent."""
        payload = io.BytesIO()
        files = {"dotnet": b"inert host", "implicit/deep/file.dll": b"inert dependency", "ro/inner.dll": b"inert read-only child"}
        if kind == "tar":
            with tarfile.open(fileobj=payload, mode="w") as archive:
                for directory, mode in (("explicit", 0o750), ("ro", 0o555), ("loose", 0o775)):
                    info = tarfile.TarInfo(directory)
                    info.type, info.mode = tarfile.DIRTYPE, mode
                    archive.addfile(info)
                for filename, data in files.items():
                    info = tarfile.TarInfo(filename)
                    info.size, info.mode = len(data), 0o644
                    archive.addfile(info, io.BytesIO(data))
        else:
            with zipfile.ZipFile(payload, "w") as archive:
                for directory, mode in (("explicit/", 0o750), ("ro/", 0o555), ("loose/", 0o775)):
                    info = zipfile.ZipInfo(directory)
                    info.external_attr = (stat.S_IFDIR | mode) << 16
                    archive.writestr(info, b"")
                for filename, data in files.items():
                    info = zipfile.ZipInfo(filename)
                    info.external_attr = (stat.S_IFREG | 0o644) << 16
                    archive.writestr(info, data)
        data = payload.getvalue()
        artifact = {"version": "fixture-pin", "filename": name + "." + kind, "url": "https://example.invalid/unused",
                    "format": kind, "entrypoint": "dotnet", "sha256": hashlib.sha256(data).hexdigest()}
        downloads = self.work / "downloads"
        downloads.mkdir(exist_ok=True)
        (downloads / artifact["filename"]).write_bytes(data)
        self.lock["artifacts"][name] = artifact

    def install_under(self, mask, name="dotnet"):
        with self.umask(mask):
            return bootstrap._install(name, self.lock, self.work)

    def discard(self, destination):
        bootstrap._INSTALL_SEALS.pop(destination, None)
        if destination.exists():
            bootstrap._remove_unpacked_tree(destination)

    def installed_under(self, mask, name="dotnet"):
        """Install and register removal first, so every failure path still cleans up."""
        destination = self.install_under(mask, name)
        self.addCleanup(self.discard, destination)
        return destination

    @unittest.skipUnless(hasattr(os, "umask") and os.name == "posix", "POSIX umask semantics required")
    def test_install_reuse_is_independent_of_the_umask_of_either_process(self):
        for kind in ("tar", "zip"):
            for first, second in ((0o022, 0o077), (0o077, 0o022)):
                with self.subTest(kind=kind, first=oct(first), second=oct(second)):
                    self.mode_archive("dotnet", kind)
                    destination = self.installed_under(first)
                    seal = bootstrap.tree_seal(destination)
                    self.assertEqual(0o750, seal["explicit"]["mode"])
                    self.assertEqual(0o555, seal["ro"]["mode"])
                    self.assertEqual(0o755, seal["loose"]["mode"])  # group write from the archive is not kept
                    self.assertEqual(0o755, seal["implicit"]["mode"])
                    self.assertEqual(0o755, seal["implicit/deep"]["mode"])
                    self.assertEqual(0o755, seal["metadata"]["mode"])
                    self.assertEqual(0o644, seal["lucid-install.json"]["mode"])
                    bootstrap._INSTALL_SEALS.pop(destination)  # a later process starts without the seal
                    self.assertEqual(destination, self.install_under(second))
                    self.assertEqual(seal, bootstrap.tree_seal(destination))
                    self.discard(destination)  # next subtest starts from a fresh destination

    @unittest.skipUnless(hasattr(os, "umask") and os.name == "posix", "POSIX umask semantics required")
    def test_injected_license_and_dumper_configuration_modes_ignore_the_umask(self):
        for name in ("assetripper", "dumper"):
            files = {"dotnet": b"inert host"}
            if name == "dumper":
                files["config.json"] = b'{"RequireAnyKey": true}'
            self.archive(name, files, "dotnet")
            (self.work / "downloads" / "license.txt").write_bytes(b"inert licence")
            self.lock["artifacts"][name + "-license"] = {
                "version": "l", "filename": "license.txt", "url": "https://example.invalid/unused",
                "sha256": hashlib.sha256(b"inert licence").hexdigest()}
            with self.subTest(tool=name):
                destination = self.install_under(0o077, name)
                seal = bootstrap.tree_seal(destination)
                self.assertEqual(0o644, seal["LICENSE.txt"]["mode"])
                if name == "dumper":
                    self.assertEqual(0o644, seal["config.json"]["mode"])
                bootstrap._INSTALL_SEALS.pop(destination)
                self.assertEqual(destination, self.install_under(0o022, name))

    @unittest.skipUnless(hasattr(os, "umask") and os.name == "posix", "POSIX umask semantics required")
    def test_umask_independent_install_still_rejects_every_mutation_under_both_masks(self):
        # Within one process the seal pop stands in for the second process of the issue.
        mutations = {
            "bytes": lambda root: (root / "implicit/deep/file.dll").write_bytes(b"substituted"),
            "file mode": lambda root: (root / "dotnet").chmod(0o700),
            "directory mode": lambda root: (root / "explicit").chmod(0o700),
            "extra file": lambda root: (root / "extra.dll").write_bytes(b"extra"),
            "symlink": lambda root: ((root / "implicit/deep/file.dll").unlink(), (root / "implicit/deep/file.dll").symlink_to(root / "dotnet")),
        }
        for mask in (0o022, 0o077):
            for name, mutate in mutations.items():
                with self.subTest(mask=oct(mask), mutation=name):
                    self.mode_archive("dotnet", "tar")
                    destination = self.installed_under(mask)
                    other = 0o022 if mask == 0o077 else 0o077
                    bootstrap._INSTALL_SEALS.pop(destination)
                    self.assertEqual(destination, self.install_under(other))  # unmutated control reuses
                    mutate(destination)
                    bootstrap._INSTALL_SEALS.pop(destination)
                    expected = "link or special file" if name == "symlink" else "differs from the pinned archive"
                    with self.assertRaisesRegex(bootstrap.ToolError, expected):
                        self.install_under(other)
                    self.discard(destination)

    def cpp2il_fixture(self):
        self.archive("dotnet", {"dotnet": b"verified SDK"}, "dotnet")
        self.archive("cpp2il-source", {"Cpp2IL/Cpp2IL.csproj": b"verified source"}, "Cpp2IL/Cpp2IL.csproj")
        self.addCleanup(self.release_leases)

    def release_leases(self):
        for path in list(bootstrap._BUILD_LEASES):
            if self.directory in path.parents:
                bootstrap._release_lease(bootstrap._BUILD_LEASES.pop(path))
                bootstrap._BUILD_SEALS.pop(path, None)

    def forget_process_state(self):
        """Mimic a new process while this one keeps holding its lease as a live owner."""
        held = []
        for path in list(bootstrap._BUILD_LEASES):
            if self.directory in path.parents:
                held.append(bootstrap._BUILD_LEASES.pop(path))
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

    def assert_junction_rejected_before_pruning(self, junction):
        # Path.is_symlink() misses NTFS junctions; their reparse attribute is reported instead.
        self.cpp2il_fixture()
        builds = self.work / "tools/cpp2il-builds"
        builds.mkdir(parents=True)
        (builds / "victim").mkdir()
        (builds / "victim.lock").write_bytes(b"")
        original_lstat = os.lstat
        def junction_lstat(path, *args, **kwargs):
            info = original_lstat(path, *args, **kwargs)
            if Path(path) == junction:
                return mock.Mock(st_mode=info.st_mode, st_file_attributes=0x400)
            return info
        with mock.patch.object(bootstrap.os, "lstat", junction_lstat), \
                mock.patch.object(bootstrap, "run_logged") as runner, \
                self.assertRaises((bootstrap.ToolError, ValueError)):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        runner.assert_not_called()
        self.assertTrue((builds / "victim").is_dir())

    def test_junction_cpp2il_builds_root_is_rejected_before_pruning(self):
        self.assert_junction_rejected_before_pruning(self.work / "tools/cpp2il-builds")

    def test_junction_ancestor_of_cpp2il_builds_is_rejected_before_pruning(self):
        self.assert_junction_rejected_before_pruning(self.work / "tools")

    def test_junction_above_the_approved_cache_root_is_rejected_before_pruning(self):
        self.assert_junction_rejected_before_pruning(bootstrap.CACHE_ROOT.parent)

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

    def test_untrusted_lease_files_are_skipped_without_following(self):
        self.cpp2il_fixture()
        builds = self.work / "tools/cpp2il-builds"
        builds.mkdir(parents=True)
        outside = self.directory / "outside-target"
        (builds / "dangling.lock").symlink_to(outside)
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            result = bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertEqual("ready", result["status"])
        self.assertFalse(outside.exists())
        self.assertTrue(any("dangling.lock" in warning for warning in result["cleanup_warnings"]))
        self.spawn.assert_not_called()

    @unittest.skipUnless(hasattr(os, "mkfifo"), "FIFOs are Unix-only")
    def test_fifo_lease_files_are_skipped_without_blocking(self):
        self.cpp2il_fixture()
        builds = self.work / "tools/cpp2il-builds"
        builds.mkdir(parents=True)
        os.mkfifo(builds / "pipe.lock")
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            result = bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertEqual("ready", result["status"])
        self.assertTrue(any("pipe.lock" in warning for warning in result["cleanup_warnings"]))
        self.spawn.assert_not_called()

    def test_failed_stale_build_removal_keeps_its_lease_for_a_later_retry(self):
        self.cpp2il_fixture()
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            stale = Path(bootstrap._ensure_cpp2il(self.lock, self.work)["path"]).parent
        self.release_leases()
        original_rmtree = shutil.rmtree
        def failing_rmtree(path, *args, **kwargs):
            if Path(path) == stale:
                raise OSError("injected stale removal failure")
            return original_rmtree(path, *args, **kwargs)
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build), \
                mock.patch.object(bootstrap.shutil, "rmtree", failing_rmtree):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertTrue(stale.exists())
        self.assertTrue(stale.with_suffix(".lock").exists())
        self.release_leases()
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertFalse(stale.exists())
        self.assertFalse(stale.with_suffix(".lock").exists())
        self.spawn.assert_not_called()

    def test_failed_seal_with_failed_cleanup_keeps_the_lease_for_a_later_retry(self):
        self.cpp2il_fixture()
        original_rmtree = shutil.rmtree
        def failing_rmtree(path, *args, **kwargs):
            if Path(path).parent.name == "cpp2il-builds" and not Path(path).name.startswith("."):
                raise OSError("injected published cleanup failure")
            return original_rmtree(path, *args, **kwargs)
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build), \
                mock.patch.object(bootstrap, "seal_built_tool", side_effect=bootstrap.ToolError("injected seal failure")), \
                mock.patch.object(bootstrap.shutil, "rmtree", failing_rmtree), \
                self.assertRaisesRegex(bootstrap.ToolError, "injected seal failure"):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        builds = self.work / "tools/cpp2il-builds"
        orphans = [path for path in builds.iterdir() if path.is_dir() and not path.name.startswith(".")]
        self.assertEqual(1, len(orphans))
        self.assertTrue(orphans[0].with_suffix(".lock").exists())
        with mock.patch.object(bootstrap, "run_logged", side_effect=self.fixture_build):
            bootstrap._ensure_cpp2il(self.lock, self.work)
        self.assertFalse(orphans[0].exists())
        self.assertFalse(orphans[0].with_suffix(".lock").exists())
        self.spawn.assert_not_called()

    def inspector_fixture(self, stack=None):
        """A verified inert SDK install and a recorder standing in for dotnet build/run."""
        stack = stack or self.stack
        self.install_fixture()
        stack.enter_context(mock.patch.object(bootstrap, "load_lock", return_value=self.lock))
        self.addCleanup(self.release_leases)
        self.executed = []
        self.inspector_builds = 0

        def fake_dotnet(command, *args, **kwargs):
            if command[1] == "build":
                stage = Path(command[2]).parent
                self.assertEqual(stage, kwargs["cwd"])
                self.assertEqual(recovery._INSPECTOR_SOURCE, (stage / "Program.cs").read_text(encoding="utf-8"))
                self.inspector_builds += 1
                output = stage / "bin/Release/net10.0"
                output.mkdir(parents=True)
                (output / "RecoveryInspector.dll").write_bytes(b"inert generated reader %d" % self.inspector_builds)
                (output / "RecoveryInspector.deps.json").write_bytes(b"inert generated dependency closure")
                return {"returncode": 0, "timed_out": False, "log": "fixture"}
            # Record the exact reader handed to the host instead of running it.
            self.executed.append((command[1], Path(command[1]).read_bytes()))
            bootstrap.write_json(Path(command[3]), {"assemblies": [{"name": "Fixture", "file": "Fixture.dll", "types": 1, "methods": 1, "fields": 0}],
                                                    "methods": [{"assembly": "Fixture", "token": "0x06000001", "name": "M", "declaring_type": "T",
                                                                 "has_body": True, "il_length": 1, "il_prefix": "2A", "il_sha256": "00"}]})
            return {"returncode": 0, "timed_out": False, "log": "fixture"}
        return stack.enter_context(mock.patch.object(recovery, "run_logged", side_effect=fake_dotnet))

    def inspector_builds_dir(self):
        return self.work / "tools/il-inspector-builds"

    def test_separate_processes_sharing_a_cache_each_get_a_verified_inspector(self):
        self.inspector_fixture()
        first = recovery._inspect_managed_output(self.work, self.work, "first-process")
        self.assertEqual(1, self.inspector_builds)
        # Same process: the sealed build is reused, not rebuilt.
        recovery._inspect_managed_output(self.work, self.work, "first-process-again")
        self.assertEqual(1, self.inspector_builds)
        self.assertEqual(self.executed[0], self.executed[1])
        # A second process that starts while the first is still alive builds its own copy.
        first_owner = [bootstrap._BUILD_LEASES.pop(path) for path in list(bootstrap._BUILD_LEASES) if self.directory in path.parents]
        self.addCleanup(lambda: [bootstrap._release_lease(stream) for stream in first_owner if not stream.closed])
        [bootstrap._BUILD_SEALS.pop(path) for path in list(bootstrap._BUILD_SEALS) if self.directory in path.parents]
        second = recovery._inspect_managed_output(self.work, self.work, "second-process")
        # A third, after both earlier owners exited, prunes theirs and builds a third.
        self.release_leases()
        for stream in first_owner:
            bootstrap._release_lease(stream)
        [bootstrap._BUILD_SEALS.pop(path) for path in list(bootstrap._BUILD_SEALS) if self.directory in path.parents]
        third = recovery._inspect_managed_output(self.work, self.work, "third-process")
        for result in (first, second, third):
            self.assertEqual({"trivial_or_default_body": 1}, result["counts"])
            self.assertTrue(Path(result["method_inventory"]).is_file())
        self.assertEqual(3, self.inspector_builds)
        readers = [Path(path) for path, _ in self.executed]
        self.assertEqual([b"inert generated reader 1"] * 2 + [b"inert generated reader 2", b"inert generated reader 3"],
                         [data for _, data in self.executed])
        self.assertEqual(3, len(set(readers)))
        for reader in readers:
            self.assertEqual(self.inspector_builds_dir(), reader.parents[3].parent)
        self.assertEqual([readers[-1].parents[3].name, readers[-1].parents[3].name + ".lock"],
                         sorted(path.name for path in self.inspector_builds_dir().iterdir()))
        self.spawn.assert_not_called()

    def test_separate_recover_code_processes_both_report_a_managed_inventory(self):
        self.inspector_fixture()
        app = self.directory / "fixture.app"
        emitted = self.work / "emitted"
        emitted.mkdir()
        metadata = {"image_count": 1, "type_count": 1, "method_count": 1, "field_count": 0,
                    "version": 31, "images": [{"name": "Fixture.dll"}]}
        attempt = {"tool": "cpp2il", "status": "ready", "output_dir": str(emitted),
                   "assembly_files": [str(emitted / "Fixture.dll")], "process": {"log": "fixture"}}
        reports = []
        for process in ("bootstrap-then-recover", "next-recover"):
            with mock.patch.object(recovery, "_resolve_input", return_value=(app, app / "binary", app / "metadata", "2022.3.54f1")), \
                    mock.patch.object(recovery, "parse_metadata", return_value=metadata), \
                    mock.patch.object(recovery, "sha256_file", return_value="fixture-hash"), \
                    mock.patch.object(recovery, "_thin_binary", return_value=(app / "binary", {"name": "arm64"})), \
                    mock.patch.object(recovery, "_cpp2il", return_value=dict(attempt)):
                reports.append(recovery.recover_code(app, self.work))
            # Process exit: its lease and seals end with it.
            self.release_leases()
            [bootstrap._BUILD_SEALS.pop(path) for path in list(bootstrap._BUILD_SEALS) if self.directory in path.parents]
        for report in reports:
            self.assertEqual("ready", report["status"])
            self.assertTrue(Path(report["quality"]["method_inventory"]).is_file())
            self.assertEqual(1, report["counts"]["output_methods"])
            self.assertFalse([item for item in report["limitations"] if "inspection could not finish" in item])
        self.assertEqual(2, self.inspector_builds)
        self.spawn.assert_not_called()

    def test_interrupted_or_foreign_inspector_output_is_rebuilt_without_executing_it(self):
        self.inspector_fixture()
        source_hash = hashlib.sha256(recovery._INSPECTOR_SOURCE.encode()).hexdigest()
        # A legacy shared tree carrying a matching public marker and a forged reader.
        legacy = self.work / "tools/il-inspector"
        (legacy / "bin/Release/net10.0").mkdir(parents=True)
        (legacy / "bin/Release/net10.0/RecoveryInspector.dll").write_bytes(b"inert forged legacy reader")
        bootstrap.write_json(legacy / "lucid-inspector.json", {"source_sha256": source_hash})
        builds = self.inspector_builds_dir()
        # An interrupted build stage and a foreign build whose owner has exited.
        (builds / ".build-interrupted/obj").mkdir(parents=True)
        foreign = builds / "foreign"
        (foreign / "bin/Release/net10.0").mkdir(parents=True)
        (foreign / "bin/Release/net10.0/RecoveryInspector.dll").write_bytes(b"inert foreign reader")
        bootstrap.write_json(foreign / "lucid-inspector.json", {"source_sha256": source_hash})
        (builds / "foreign.lock").write_bytes(b"")
        result = recovery._inspect_managed_output(self.work, self.work, "fixture")
        self.assertTrue(Path(result["method_inventory"]).is_file())
        self.assertEqual(1, self.inspector_builds)
        self.assertEqual([b"inert generated reader 1"], [data for _, data in self.executed])
        self.assertIn(builds, Path(self.executed[0][0]).parents)
        self.assertFalse(foreign.exists())
        self.assertFalse((builds / ".build-interrupted").exists())
        self.assertEqual(b"inert forged legacy reader", (legacy / "bin/Release/net10.0/RecoveryInspector.dll").read_bytes())
        self.spawn.assert_not_called()

    def test_failed_inspector_build_leaves_no_executable_tree(self):
        runner = self.inspector_fixture()
        def failing_build(command, *args, **kwargs):
            (Path(kwargs["cwd"]) / "bin/Release/net10.0").mkdir(parents=True)
            (Path(kwargs["cwd"]) / "bin/Release/net10.0/RecoveryInspector.dll").write_bytes(b"inert partial reader")
            return {"returncode": 1, "timed_out": False, "log": "fixture"}
        runner.side_effect = failing_build
        with self.assertRaisesRegex(bootstrap.ToolError, "Could not build the managed-output inspector"):
            recovery._inspect_managed_output(self.work, self.work, "fixture")
        self.assertEqual([], list(self.inspector_builds_dir().iterdir()))
        self.assertEqual(1, runner.call_count)
        self.spawn.assert_not_called()

    def assert_coordinator_lock_refused(self, message, error=bootstrap.ToolError):
        """The inspector coordinator lock is refused promptly, before any build or lock wait."""
        runner = self.inspector_fixture()
        outcome = {}

        def inspect():
            try:
                recovery._inspect_managed_output(self.work, self.work, "fixture")
            except BaseException as exc:  # recorded for the main thread
                outcome["error"] = exc
        worker = threading.Thread(target=inspect, daemon=True)
        worker.start()
        worker.join(10)
        self.assertFalse(worker.is_alive(), "opening the inspector coordinator lock blocked")
        self.assertIs(error, type(outcome.get("error")))
        self.assertRegex(str(outcome["error"]), message)
        runner.assert_not_called()
        self.assertFalse(self.inspector_builds_dir().exists())
        self.spawn.assert_not_called()

    @unittest.skipUnless(hasattr(os, "mkfifo") and os.name != "nt", "FIFOs are Unix-only")
    def test_fifo_inspector_coordinator_lock_fails_without_blocking(self):
        lock = self.work / "tools/.il-inspector.lock"
        lock.parent.mkdir(parents=True)
        os.mkfifo(lock)
        self.assert_coordinator_lock_refused("Inspector coordinator lock must be a regular file")
        self.assertTrue(stat.S_ISFIFO(os.lstat(lock).st_mode))

    @unittest.skipIf(os.name == "nt", "symlink creation needs privileges on Windows")
    def test_symlinked_inspector_coordinator_lock_is_refused_without_following(self):
        lock = self.work / "tools/.il-inspector.lock"
        lock.parent.mkdir(parents=True)
        outside = self.directory / "outside-target"
        lock.symlink_to(outside)
        # managed_path already refuses the link before it is opened.
        self.assert_coordinator_lock_refused("Generated path cannot contain a symbolic link", ValueError)
        # The opener refuses it too, so a link swapped in after path validation still fails.
        with self.assertRaisesRegex(bootstrap.ToolError, "Inspector coordinator lock must not be a link"):
            bootstrap.open_coordinator_lock(lock, "Inspector coordinator lock")
        self.assertFalse(outside.exists())
        self.assertTrue(lock.is_symlink())

    def test_multiply_linked_inspector_coordinator_lock_is_refused(self):
        lock = self.work / "tools/.il-inspector.lock"
        lock.parent.mkdir(parents=True)
        outside = self.directory / "outside-target"
        outside.write_bytes(b"")
        os.link(outside, lock)
        self.assert_coordinator_lock_refused("Inspector coordinator lock must be a regular file")
        self.assertEqual(b"", outside.read_bytes())

    def test_modified_inspector_or_dependency_closure_is_never_executed(self):
        reader = "bin/Release/net10.0/RecoveryInspector.dll"
        # Within one process the seal pop stands in for the second process of the issue.
        mutations = {
            "reader": lambda build, sdk: (build / reader).write_bytes(b"inert substituted reader"),
            "dependency": lambda build, sdk: (build / "bin/Release/net10.0/RecoveryInspector.deps.json").write_bytes(b"{}"),
            "added": lambda build, sdk: (build / "bin/Release/net10.0/Injected.dll").write_bytes(b"inert plugin"),
            "symlink": lambda build, sdk: ((build / reader).unlink(), (build / reader).symlink_to(sdk / "dotnet")),
            "mode": lambda build, sdk: (build / reader).chmod(0o755),
            "sdk": lambda build, sdk: (sdk / "shared/runtime.dll").write_bytes(b"inert substituted SDK dependency"),
        }
        for name, mutate in mutations.items():
            with self.subTest(mutation=name), ExitStack() as stack:
                stack.callback(self.reset_inspector_fixture)
                runner = self.inspector_fixture(stack)
                recovery._inspect_managed_output(self.work, self.work, "sealed")
                calls = runner.call_count
                build = Path(self.executed[0][0]).parents[3]
                mutate(build, bootstrap.install_path(self.work, "dotnet", self.lock))
                with self.assertRaisesRegex(bootstrap.ToolError, "changed|link or special file"):
                    recovery._inspect_managed_output(self.work, self.work, "after-change")
                self.assertEqual(calls, runner.call_count)
                self.assertEqual(1, len(self.executed))
        self.spawn.assert_not_called()

    def reset_inspector_fixture(self):
        self.release_leases()
        bootstrap._INSTALL_SEALS.pop(bootstrap.install_path(self.work, "dotnet", self.lock), None)
        shutil.rmtree(self.work / "tools")

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
            self.assertEqual(bootstrap.install_path(host, "dotnet", self.lock), tool)
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
            sdk = host / "tools/dotnet-fixture/dotnet"  # verified_tool_path is mocked below, so any pinned-looking location serves
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
                    patches.enter_context(mock.patch.object(module, "run_owned", side_effect=invoke))
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


if __name__ == "__main__":
    unittest.main()

"""Host selection for pinned recovery tools; no downloads are performed."""

import errno
import os
import stat
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest import mock

from tools.lucidlib import assets, bootstrap, native
from tools.lucidlib.bootstrap import CACHE_ROOT, ToolError, exclusive_lock, host_lock, load_lock


class HostLockTests(unittest.TestCase):
    def setUp(self):
        self.lock = load_lock()

    def _on(self, system, machine):
        return mock.patch.multiple("platform", system=mock.Mock(return_value=system),
                                   machine=mock.Mock(return_value=machine))

    def test_default_host_uses_top_level_artifacts(self):
        with self._on("Darwin", "arm64"):
            pinned = host_lock(self.lock)
        self.assertEqual("osx-arm64", pinned["rid"])
        self.assertIs(self.lock["artifacts"], pinned["artifacts"])

    def test_windows_overrides_only_native_artifacts(self):
        with self._on("Windows", "AMD64"):
            pinned = host_lock(self.lock)
        self.assertEqual("win-x64", pinned["rid"])
        overrides = self.lock["hosts"]["windows-amd64"]["artifacts"]
        self.assertEqual({"assetripper", "dotnet", "runtime6"}, set(overrides))
        for name, artifact in pinned["artifacts"].items():
            expected = overrides.get(name, self.lock["artifacts"][name])
            self.assertEqual(expected, artifact)
        for name in overrides:
            self.assertTrue(pinned["artifacts"][name]["entrypoint"].endswith(".exe"))
            self.assertEqual(self.lock["artifacts"][name]["version"], pinned["artifacts"][name]["version"])

    def test_every_pinned_artifact_has_a_checksum(self):
        for host in [self.lock, *self.lock["hosts"].values()]:
            for name, artifact in host["artifacts"].items():
                with self.subTest(name=name, url=artifact["url"]):
                    algorithm = "sha512" if "sha512" in artifact else "sha256"
                    self.assertEqual(64 if algorithm == "sha256" else 128, len(artifact[algorithm]))

    def test_unsupported_host_names_supported_hosts(self):
        with self._on("Linux", "aarch64"), self.assertRaisesRegex(ToolError, "linux-x86_64"):
            host_lock(self.lock)

    def test_unsupported_host_fails_before_download(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="test-hosts-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, work)
        with self._on("Linux", "aarch64"), mock.patch.object(bootstrap, "download_artifact") as download:
            with self.assertRaises(ToolError):
                bootstrap.bootstrap(work, ["assetripper"])
        download.assert_not_called()

    @unittest.skipUnless(hasattr(os, "mkfifo"), "needs FIFOs")
    def test_tool_lock_fifo_fails_promptly_without_downloading(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="test-hosts-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, work)
        (work / "tools").mkdir()
        os.mkfifo(work / "tools" / ".assetripper.lock")
        script = ("import sys; from pathlib import Path; sys.path.insert(0, sys.argv[1]);"
                  "from tools.lucidlib import bootstrap;"
                  "bootstrap.bootstrap(Path(sys.argv[2]), ['assetripper'])")
        root = Path(__file__).resolve().parents[2]
        try:
            result = subprocess.run([sys.executable, "-c", script, str(root), str(work)],
                                    capture_output=True, timeout=30, stdin=subprocess.DEVNULL)
        except subprocess.TimeoutExpired:
            self.fail("bootstrap blocked on a FIFO lock")
        self.assertNotEqual(0, result.returncode)
        self.assertIn(b"regular file", result.stderr)
        self.assertFalse((work / "downloads").exists())

    @unittest.skipUnless(hasattr(os, "link"), "needs hardlinks")
    def test_run_logged_refuses_a_hardlinked_log(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="test-hosts-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, work)
        (work / "reports").mkdir()
        victim = work / "victim.txt"
        victim.write_bytes(b"keep me")
        os.link(victim, work / "reports" / "run.log")
        with self.assertRaisesRegex(ToolError, "regular file"):
            bootstrap.run_logged([sys.executable, "-c", "pass"], work, "run.log")
        self.assertEqual(b"keep me", victim.read_bytes())

    def test_run_logged_still_overwrites_a_regular_log(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="test-hosts-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, work)
        (work / "reports").mkdir()
        (work / "reports" / "run.log").write_text("stale stale stale " * 20)
        bootstrap.run_logged([sys.executable, "-c", "print('fresh')"], work, "run.log")
        text = (work / "reports" / "run.log").read_text()
        self.assertIn("fresh", text)
        self.assertNotIn("stale", text)

    @unittest.skipUnless(hasattr(os, "fchmod"), "needs POSIX modes")
    def test_run_logged_tightens_a_permissive_reused_log(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="test-hosts-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, work)
        (work / "reports").mkdir()
        log = work / "reports" / "run.log"
        log.write_text("old")
        os.chmod(log, 0o644)
        bootstrap.run_logged([sys.executable, "-c", "pass"], work, "run.log")
        self.assertEqual(0o600, stat.S_IMODE(log.stat().st_mode))

    def test_linux_selects_native_tools_and_retains_shared_source_pins(self):
        with self._on("Linux", "x86_64"):
            pinned = host_lock(self.lock)
        self.assertEqual("linux-x64", pinned["rid"])
        for name in ("assetripper", "dotnet", "runtime6"):
            self.assertIn("linux", pinned["artifacts"][name]["url"])
            self.assertEqual(self.lock["artifacts"][name]["version"], pinned["artifacts"][name]["version"])
        for name in ("cpp2il-source", "dumper", "assetripper-license", "dumper-license"):
            self.assertEqual(self.lock["artifacts"][name], pinned["artifacts"][name])

    def test_native_build_records_the_host_sdk_archive(self):
        with self._on("Linux", "x86_64"):
            identity = native._lock_identity(self.lock)
        self.assertEqual(self.lock["hosts"]["linux-x86_64"]["artifacts"]["dotnet"]["sha512"],
                         identity["sdk_archive_sha512"])
        self.assertNotEqual(self.lock["artifacts"]["dotnet"]["sha512"],
                            identity["sdk_archive_sha512"])

    def test_executable_names_follow_the_host(self):
        path = Path("tools/dotnet/dotnet")
        with mock.patch.object(bootstrap, "_windows_host", return_value=True):
            self.assertEqual("dotnet.exe", bootstrap.executable(path).name)
        with mock.patch.object(bootstrap, "_windows_host", return_value=False):
            self.assertEqual("dotnet", bootstrap.executable(path).name)


class ExportIdentityTests(unittest.TestCase):
    def _report(self, archive_sha256, settings=None):
        tool = {"name": "AssetRipper", "version": "2.0.0", "archive_sha256": archive_sha256}
        return {"input_path": "Game.app", "input_fingerprint": "fingerprint", "tool": tool,
                "settings": dict(assets.EXPORT_SETTINGS) if settings is None else settings}

    def _validate(self, report):
        with mock.patch.object(assets, "_resolve_app", return_value=Path("Game.app")), \
             mock.patch.object(assets, "_manifest", return_value=[]), \
             mock.patch.object(assets, "fingerprint_manifest", return_value="fingerprint"):
            return assets._validate_export_identity(report)

    def test_exports_from_either_pinned_host_build_are_accepted(self):
        lock = load_lock()
        for archive in [lock["artifacts"]["assetripper"]["sha256"],
                        *[host["artifacts"]["assetripper"]["sha256"] for host in lock["hosts"].values()]]:
            with self.subTest(archive=archive):
                self.assertEqual(Path("Game.app"), self._validate(self._report(archive)))

    def test_unknown_archive_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "tool version/settings differ"):
            self._validate(self._report("0" * 64))

    def test_changed_export_settings_are_rejected(self):
        settings = dict(assets.EXPORT_SETTINGS, ImageExportFormat="Bmp")
        with self.assertRaisesRegex(ValueError, "tool version/settings differ"):
            self._validate(self._report(load_lock()["artifacts"]["assetripper"]["sha256"], settings))

    def test_current_host_build_is_recorded(self):
        with mock.patch.multiple("platform", system=mock.Mock(return_value="Windows"),
                                 machine=mock.Mock(return_value="AMD64")):
            tool = assets._tool_lock()
        self.assertEqual(load_lock()["hosts"]["windows-amd64"]["artifacts"]["assetripper"]["sha256"],
                         tool["archive_sha256"])


class FakeMsvcrt:
    """Simulate msvcrt.locking with scripted outcomes for each lock attempt."""
    LK_LOCK, LK_UNLCK = 1, 0

    def __init__(self, outcomes):
        self.outcomes = list(outcomes)
        self.calls = []

    def locking(self, fd, mode, length):
        self.calls.append(mode)
        if mode == self.LK_LOCK and self.outcomes:
            outcome = self.outcomes.pop(0)
            if outcome is not None:
                raise OSError(outcome, "simulated")


class WindowsLockTests(unittest.TestCase):
    def _lock(self, fake):
        stream = mock.Mock(fileno=mock.Mock(return_value=3))
        return mock.patch.multiple(bootstrap, fcntl=None, msvcrt=fake), stream

    def test_contention_is_retried_until_acquired(self):
        fake = FakeMsvcrt([errno.EDEADLK, errno.EACCES, None])
        patch, stream = self._lock(fake)
        with patch, exclusive_lock(stream):
            self.assertEqual([fake.LK_LOCK] * 3, fake.calls)
        self.assertEqual([fake.LK_LOCK] * 3 + [fake.LK_UNLCK], fake.calls)

    def test_permanent_error_propagates_without_retry(self):
        fake = FakeMsvcrt([errno.EBADF, None])
        patch, stream = self._lock(fake)
        with patch, self.assertRaises(OSError) as raised:
            with exclusive_lock(stream):
                self.fail("lock should not be acquired")
        self.assertEqual(errno.EBADF, raised.exception.errno)
        self.assertEqual([fake.LK_LOCK], fake.calls)

    def test_lock_is_released_when_protected_operation_fails(self):
        fake = FakeMsvcrt([None])
        patch, stream = self._lock(fake)
        with patch, self.assertRaisesRegex(RuntimeError, "protected"):
            with exclusive_lock(stream):
                raise RuntimeError("protected operation failed")
        self.assertEqual([fake.LK_LOCK, fake.LK_UNLCK], fake.calls)


class ExclusiveLockTests(unittest.TestCase):
    def test_second_holder_waits_for_release(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="test-lock-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, work)
        path = work / ".tool.lock"
        events = []

        def contender():
            with path.open("a") as stream, exclusive_lock(stream):
                events.append("second")

        thread = threading.Thread(target=contender)
        try:
            with path.open("a") as stream, exclusive_lock(stream):
                thread.start()
                thread.join(timeout=0.5)
                events.append("first")
                self.assertTrue(thread.is_alive())
        finally:
            if thread.ident is not None:
                thread.join(timeout=15)
        self.assertFalse(thread.is_alive())
        self.assertEqual(["first", "second"], events)


class WorkLockTests(unittest.TestCase):
    """take_work_lock is held by the OS, so only a live owner blocks it (#568)."""

    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.path = Path(temp.name) / "locks" / "work.lock"

    def test_file_left_by_a_dead_owner_does_not_block(self):
        self.path.parent.mkdir()
        self.path.write_bytes(b"")
        bootstrap.release_work_lock(bootstrap.take_work_lock(self.path, "busy"))
        self.assertTrue(self.path.is_file())

    def test_legacy_lock_directory_stays_busy_until_removed_on_purpose(self):
        # An older command may still own it: it is neither removed nor bypassed, and the message says how to clear it.
        self.path.mkdir(parents=True)
        with self.assertRaisesRegex(ValueError, "^busy; a lock directory from an earlier tool version is at .*work.lock"):
            bootstrap.take_work_lock(self.path, "busy")
        self.assertTrue(self.path.is_dir())
        self.path.rmdir()
        bootstrap.release_work_lock(bootstrap.take_work_lock(self.path, "busy"))
        self.assertTrue(self.path.is_file())

    def test_non_empty_legacy_lock_directory_is_not_removed(self):
        self.path.mkdir(parents=True)
        (self.path / "owner").write_bytes(b"x")
        with self.assertRaises(ValueError):
            bootstrap.take_work_lock(self.path, "busy")
        self.assertTrue((self.path / "owner").exists())

    def test_held_lock_raises_the_busy_message(self):
        owner = bootstrap.take_work_lock(self.path, "busy")
        try:
            with self.assertRaisesRegex(ValueError, "^Another owner holds it$"):
                bootstrap.take_work_lock(self.path, "Another owner holds it")
        finally:
            bootstrap.release_work_lock(owner)

    def test_lock_can_be_taken_again_after_release(self):
        bootstrap.release_work_lock(bootstrap.take_work_lock(self.path, "busy"))
        bootstrap.release_work_lock(bootstrap.take_work_lock(self.path, "busy"))

    @unittest.skipUnless(os.name == "posix", "SIGKILL is POSIX")
    def test_killed_owner_leaves_a_file_that_does_not_block(self):
        code = ("import sys, time\nsys.path.insert(0, sys.argv[2])\nfrom pathlib import Path\nfrom tools.lucidlib import bootstrap\n"
                "owner = bootstrap.take_work_lock(Path(sys.argv[1]), 'busy')\nprint('held', flush=True)\ntime.sleep(60)\n")
        root = Path(__file__).resolve().parents[2]
        child = subprocess.Popen([sys.executable, "-I", "-c", code, str(self.path), str(root)], cwd=root,
                                 stdout=subprocess.PIPE)
        try:
            self.assertEqual(b"held\n", child.stdout.readline())
            with self.assertRaises(ValueError):
                bootstrap.take_work_lock(self.path, "busy")
        finally:
            child.kill()
            child.wait(timeout=10)
            child.stdout.close()
        self.assertTrue(self.path.is_file())
        bootstrap.release_work_lock(bootstrap.take_work_lock(self.path, "busy"))

    @unittest.skipUnless(os.name == "posix", "SIGKILL is POSIX; a Windows job kills the tree with its owner")
    def test_killed_owner_keeps_the_lock_while_its_command_runs(self):
        # The owned command runs in its own session and survives its owner's SIGKILL; until it stops,
        # nobody else may take the lock and start another command against the same work.
        code = ("import sys\nsys.path.insert(0, sys.argv[2])\nfrom pathlib import Path\nfrom tools.lucidlib import bootstrap\n"
                "owner = bootstrap.take_work_lock(Path(sys.argv[1]), 'busy')\n"
                "bootstrap.run_owned([sys.executable, '-I', '-c', 'import os, time; print(os.getpid(), flush=True); time.sleep(60)'],"
                " timeout=120)\n")
        root = Path(__file__).resolve().parents[2]
        owner = subprocess.Popen([sys.executable, "-I", "-c", code, str(self.path), str(root)], cwd=root,
                                 stdout=subprocess.PIPE)
        command = None
        try:
            command = int(owner.stdout.readline())
            owner.kill()
            owner.wait(timeout=10)
            with self.assertRaisesRegex(ValueError, "^busy$"):
                bootstrap.take_work_lock(self.path, "busy")
        finally:
            owner.kill()
            owner.wait(timeout=10)
            owner.stdout.close()
            if command is not None:
                os.kill(command, signal.SIGKILL)
                for _ in range(100):
                    try:
                        os.kill(command, 0)
                    except ProcessLookupError:
                        break
                    time.sleep(0.1)
        bootstrap.release_work_lock(bootstrap.take_work_lock(self.path, "busy"))


if __name__ == "__main__":
    unittest.main()

"""Host selection for pinned recovery tools; no downloads are performed."""

import errno
from pathlib import Path
import shutil
import tempfile
import threading
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


if __name__ == "__main__":
    unittest.main()

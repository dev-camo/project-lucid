"""Safety boundaries and conservative code-recovery reporting regressions."""

import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import signal
import stat
import subprocess
import sys
import tarfile
import tempfile
import threading
import time
import unittest
from unittest import mock
import zipfile

from tools.lucidlib.bootstrap import (CACHE_ROOT, REPO_ROOT, ToolError, bootstrap,
                                   download_artifact, managed_path, run_logged,
                                   unpack_archive, validate_work_dir)
from tools.lucidlib.recovery import _managed_files, classify_il, recover_code


class RecoverySafetyTests(unittest.TestCase):
    def setUp(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix="test-recovery-", dir=CACHE_ROOT))

    def tearDown(self):
        shutil.rmtree(self.work)

    def _tar(self, members):
        output = self.work / "fixture.tar"
        with tarfile.open(output, "w") as archive:
            for name, content, link in members:
                member = tarfile.TarInfo(name)
                if link is not None:
                    member.type = tarfile.SYMTYPE
                    member.linkname = link
                    archive.addfile(member)
                else:
                    member.size = len(content)
                    archive.addfile(member, io.BytesIO(content))
        return output

    def test_repo_and_input_cannot_be_work_directories(self):
        for path in (REPO_ROOT, REPO_ROOT / "input", REPO_ROOT / "Assets", self.work / ".." / ".." / "escape"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                validate_work_dir(path)

    def test_existing_symlink_cannot_redirect_generated_output(self):
        (self.work / "tools").symlink_to(REPO_ROOT / "input", target_is_directory=True)
        with self.assertRaises(ValueError):
            managed_path(self.work, "tools", "replacement.dll")
        with self.assertRaises(ValueError):
            validate_work_dir(self.work / "tools" / "nested")

    def test_selected_tool_names_are_validated_before_any_download(self):
        for selected in ([], ["unknown"], "../input"):
            with self.subTest(selected=selected), mock.patch("tools.lucidlib.bootstrap.download_artifact") as download:
                with self.assertRaises(ValueError):
                    bootstrap(self.work, selected)
                download.assert_not_called()

    def test_traversal_is_rejected_before_stripping_archive_root(self):
        archive = self._tar([("source/../escape.txt", b"bad", None)])
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "tar", "strip_components": 1})
        self.assertFalse(destination.exists())
        self.assertFalse((self.work / "escape.txt").exists())

    def test_tar_link_cannot_redirect_later_member(self):
        archive = self._tar([("tools/link", b"", str(REPO_ROOT / "input")),
                             ("tools/link/overwrite", b"bad", None)])
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "tar"})
        self.assertFalse(destination.exists())

    def test_zip_link_is_rejected(self):
        archive = self.work / "fixture.zip"
        with zipfile.ZipFile(archive, "w") as source:
            member = zipfile.ZipInfo("redirect")
            member.create_system = 3
            member.external_attr = (stat.S_IFLNK | 0o777) << 16
            source.writestr(member, "../../input")
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "zip"})
        self.assertFalse(destination.exists())

    def test_archive_unpacked_size_limit_is_cumulative(self):
        archive = self._tar([("one", b"1234", None), ("two", b"5678", None)])
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "tar", "max_unpacked_bytes": 6})
        self.assertFalse(destination.exists())

    def test_failed_download_checksum_never_promotes_artifact(self):
        artifact = {"filename": "tool.bin", "url": "https://example.invalid/pinned-tool",
                    "sha256": hashlib.sha256(b"expected").hexdigest()}
        with mock.patch("urllib.request.urlopen", return_value=io.BytesIO(b"unexpected")):
            with self.assertRaises(ToolError):
                download_artifact(artifact, self.work)
        self.assertFalse((self.work / "downloads/tool.bin").exists())
        self.assertEqual(list((self.work / "downloads").glob("*.part")), [])

    def test_verified_download_is_reused_without_network(self):
        payload = b"expected"
        artifact = {"filename": "tool.bin", "url": "https://example.invalid/pinned-tool",
                    "sha256": hashlib.sha256(payload).hexdigest()}
        with mock.patch("urllib.request.urlopen", return_value=io.BytesIO(payload)):
            downloaded = download_artifact(artifact, self.work)
        with mock.patch("urllib.request.urlopen") as network:
            self.assertEqual(download_artifact(artifact, self.work), downloaded)
            network.assert_not_called()

    def test_timeout_stops_process_and_retains_diagnostic(self):
        result = run_logged([sys.executable, "-c", "import time; time.sleep(60)"], self.work,
                            "timeout.log", timeout=1)
        self.assertTrue(result["timed_out"])
        self.assertNotEqual(result["returncode"], 0)
        self.assertIn("stopped the process", Path(result["log"]).read_text())

    # The leader starts the worker, waits until the worker reports ready (handler
    # installed, PID published atomically), then sleeps past the timeout and exits
    # on SIGTERM.
    _LEADER = ("import os, subprocess, sys, time; "
               "subprocess.Popen([sys.executable, '-c', sys.argv[1], sys.argv[2]]); "
               "end = time.monotonic() + 30\n"
               "while not os.path.exists(sys.argv[2]) and time.monotonic() < end: time.sleep(0.02)\n"
               "time.sleep(60)")

    @staticmethod
    def _process_state(pid):
        # The process-table state letter, or "" once the PID has been reaped.
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return ""
        if sys.platform.startswith("linux"):
            try:
                stat_line = Path(f"/proc/{pid}/stat").read_text()
            except OSError:
                return ""
            return stat_line.rsplit(")", 1)[1].split()[0]
        return subprocess.run(["ps", "-o", "stat=", "-p", str(pid)], capture_output=True,
                              text=True).stdout.strip()

    @classmethod
    def _process_stopped(cls, pid):
        # os.kill(pid, 0) also succeeds on a zombie awaiting a lazy reaper; a zombie
        # has stopped running.
        state = cls._process_state(pid)
        return not state or state.startswith("Z")

    def _run_leader_with_worker(self, worker, log_name):
        pid_file = self.work / "worker.pid"
        result = run_logged([sys.executable, "-c", self._LEADER, worker, str(pid_file)], self.work,
                            log_name, timeout=3)
        self.assertTrue(pid_file.exists(), "worker was not ready before the timeout")
        return result, int(pid_file.read_text())

    @staticmethod
    def _worker(on_sigterm):
        return ("import os, signal, sys, time\n" + on_sigterm +
                "tmp = sys.argv[1] + '.tmp'\nopen(tmp, 'w').write(str(os.getpid()))\n"
                "os.replace(tmp, sys.argv[1])\ntime.sleep(60)")

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_timeout_stops_descendant_that_outlives_the_leader(self):
        # The leader exits on SIGTERM; its child ignores SIGTERM and would survive a
        # group SIGTERM alone. The wrapper must still stop the whole group (#235).
        worker = self._worker("signal.signal(signal.SIGTERM, signal.SIG_IGN)\n")
        worker_pid = None
        try:
            result, worker_pid = self._run_leader_with_worker(worker, "descendant-timeout.log")
            self.assertTrue(result["timed_out"])
            self.assertNotEqual(result["returncode"], 0)
            self.assertIn("stopped the process", Path(result["log"]).read_text())
            deadline = time.monotonic() + 5
            while not self._process_stopped(worker_pid) and time.monotonic() < deadline:
                time.sleep(0.05)
            self.assertTrue(self._process_stopped(worker_pid),
                            "SIGTERM-ignoring descendant survived the timeout")
        finally:
            if worker_pid is not None:
                try:
                    os.kill(worker_pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_timeout_keeps_sigterm_grace_for_descendants_after_the_leader_exits(self):
        # The leader exits on SIGTERM at once; the worker needs about a second to
        # finish its own cleanup. The group SIGKILL must wait for the SIGTERM grace
        # window instead of following the leader's exit immediately.
        done = self.work / "worker.done"
        worker = self._worker(
            "def stop(*_):\n    time.sleep(1)\n    open(%r, 'w').write('done')\n    os._exit(0)\n"
            "signal.signal(signal.SIGTERM, stop)\n" % str(done))
        worker_pid = None
        try:
            result, worker_pid = self._run_leader_with_worker(worker, "descendant-grace.log")
            self.assertTrue(result["timed_out"])
            self.assertTrue(done.exists(), "descendant was killed during its SIGTERM cleanup")
            self.assertTrue(self._process_stopped(worker_pid))
        finally:
            if worker_pid is not None:
                try:
                    os.kill(worker_pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_timeout_keeps_the_leader_unreaped_while_the_group_is_killed(self):
        # The leader exits on SIGTERM and the worker ignores it, so the group needs
        # SIGKILL. Until then the leader must stay an unreaped zombie: its PID keeps
        # the group id from being reused by an unrelated group.
        worker = self._worker("signal.signal(signal.SIGTERM, signal.SIG_IGN)\n")
        real_killpg = os.killpg
        leader_states = []

        def observe(pgid, sig):
            if sig == signal.SIGKILL:
                leader_states.append(self._process_state(pgid))
            real_killpg(pgid, sig)

        worker_pid = None
        try:
            with mock.patch.object(os, "killpg", side_effect=observe):
                result, worker_pid = self._run_leader_with_worker(worker, "leader-unreaped.log")
            self.assertTrue(result["timed_out"])
            self.assertNotEqual(result["returncode"], 0)
            self.assertEqual(len(leader_states), 1, "the group was not sent SIGKILL once")
            self.assertTrue(leader_states[0].startswith("Z"),
                            f"leader was reaped before the group SIGKILL ({leader_states[0]!r})")
        finally:
            if worker_pid is not None:
                try:
                    os.kill(worker_pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_timeout_returns_only_after_the_killed_group_has_stopped(self):
        # The worker survives SIGTERM until SIGKILL. The SIGKILL takes effect half a
        # second after it is sent, standing in for a member that is slow to die, so the
        # wrapper must wait for the group instead of returning once the signal is sent.
        worker = self._worker("signal.signal(signal.SIGTERM, signal.SIG_IGN)\n")
        real_killpg = os.killpg
        timers = []

        def delay_kill(pgid, sig):
            if sig != signal.SIGKILL:
                return real_killpg(pgid, sig)
            timer = threading.Timer(0.5, real_killpg, (pgid, sig))
            timers.append(timer)
            timer.start()

        worker_pid = None
        try:
            with mock.patch.object(os, "killpg", side_effect=delay_kill):
                result, worker_pid = self._run_leader_with_worker(worker, "group-stopped.log")
            stopped = self._process_stopped(worker_pid)
            self.assertTrue(result["timed_out"])
            self.assertTrue(timers, "the group was not sent SIGKILL")
            self.assertTrue(stopped, "run_logged returned while a killed descendant still ran")
        finally:
            for timer in timers:
                timer.join()
            if worker_pid is not None:
                try:
                    os.kill(worker_pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    # Runs run_logged under a caller whose finally records, at cleanup time, the state of the
    # leader (pid published as soon as it starts) and of the worker.
    _INTERRUPT_DRIVER = (
        "import json, signal, subprocess, sys\n"
        "from pathlib import Path\n"
        "from tools.lucidlib import bootstrap\n"
        "signal.signal(signal.SIGINT, signal.default_int_handler)\n"
        "work, leader, worker, pid_file, marker, leader_file = sys.argv[1:7]\n"
        "real_popen = subprocess.Popen\n"
        "class Spy(real_popen):\n"
        "    def __init__(self, *args, **kwargs):\n"
        "        super().__init__(*args, **kwargs)\n"
        "        Path(leader_file).write_text(str(self.pid))\n"
        "subprocess.Popen = Spy\n"
        "def state(pid):\n"
        "    return subprocess.run(['ps', '-o', 'stat=', '-p', str(pid)],\n"
        "                          capture_output=True, text=True).stdout.strip()\n"
        "try:\n"
        "    bootstrap.run_logged([sys.executable, '-c', leader, worker, pid_file], Path(work),\n"
        "                         'interrupt.log', timeout=60)\n"
        "finally:\n"
        "    Path(marker).write_text(json.dumps({\n"
        "        'leader': state(Path(leader_file).read_text()),\n"
        "        'worker': state(Path(pid_file).read_text())}))\n")

    def _interrupt_group(self, worker, interrupts):
        pids = self.work / "pids"
        marker = self.work / "cleanup.marker"
        leader_file = self.work / "leader.pid"
        helper = subprocess.Popen(
            [sys.executable, "-c", self._INTERRUPT_DRIVER, str(self.work), self._LEADER, worker,
             str(pids), str(marker), str(leader_file)],
            cwd=REPO_ROOT, stderr=subprocess.PIPE, text=True)
        worker_pid = leader_pid = None
        try:
            deadline = time.monotonic() + 30
            while not pids.exists() and time.monotonic() < deadline:
                time.sleep(0.05)
            self.assertTrue(pids.exists(), "worker was not ready")
            worker_pid = int(pids.read_text())
            leader_pid = int(leader_file.read_text())
            for index in range(interrupts):
                if index:
                    time.sleep(0.5)
                helper.send_signal(signal.SIGINT)
            _, stderr = helper.communicate(timeout=60)
            self.assertNotEqual(helper.returncode, 0)
            self.assertIn("KeyboardInterrupt", stderr)
            self.assertTrue(marker.exists(), "caller cleanup did not run")
            at_cleanup = json.loads(marker.read_text())
            for name, state in at_cleanup.items():
                self.assertTrue(not state or state.startswith("Z"),
                                f"{name} was still running when caller cleanup started ({state!r})")
            self.assertTrue(self._process_stopped(leader_pid))
            self.assertTrue(self._process_stopped(worker_pid))
            self.assertIn("after an interruption",
                          (self.work / "reports" / "interrupt.log").read_text())
        finally:
            if helper.poll() is None:
                helper.kill()
                helper.communicate()
            if leader_pid is not None:
                try:
                    os.killpg(leader_pid, signal.SIGKILL)
                except (ProcessLookupError, PermissionError):
                    pass
            if worker_pid is not None:
                try:
                    os.kill(worker_pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_interrupt_stops_the_group_before_the_caller_cleans_up(self):
        # Ctrl-C during the wait must stop the tool and its descendant before the
        # caller's finally runs, then propagate as KeyboardInterrupt (#412).
        self._interrupt_group(self._worker(""), 1)

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_repeated_interrupt_does_not_cut_the_group_stop_short(self):
        # The worker ignores SIGTERM, so stopping takes the full SIGTERM grace. A second
        # Ctrl-C during it must not abandon the group before SIGKILL.
        self._interrupt_group(self._worker("signal.signal(signal.SIGTERM, signal.SIG_IGN)\n"), 2)

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_interrupt_raises_tool_error_when_the_group_outlives_the_sigkill_bound(self):
        # When the group cannot be stopped, callers must not see a plain interruption
        # and go on to remove its working trees; the interruption stays as the cause.
        with mock.patch("tools.lucidlib.bootstrap.group_has_live_members", return_value=True), \
                mock.patch("tools.lucidlib.bootstrap.TERM_GRACE_SECONDS", 0.2), \
                mock.patch("tools.lucidlib.bootstrap.KILL_WAIT_SECONDS", 0.2), \
                mock.patch.object(subprocess.Popen, "wait", side_effect=KeyboardInterrupt):
            with self.assertRaises(ToolError) as caught:
                run_logged([sys.executable, "-c", "import time; time.sleep(60)"], self.work,
                           "interrupt-bound.log", timeout=60)
        self.assertIsInstance(caught.exception.__cause__, KeyboardInterrupt)

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_timeout_raises_when_the_group_outlives_the_sigkill_bound(self):
        # A group that still reports live members after SIGKILL must not let callers
        # go on to remove its working trees.
        with mock.patch("tools.lucidlib.bootstrap.group_has_live_members", return_value=True), \
                mock.patch("tools.lucidlib.bootstrap.TERM_GRACE_SECONDS", 0.2), \
                mock.patch("tools.lucidlib.bootstrap.KILL_WAIT_SECONDS", 0.2):
            with self.assertRaisesRegex(ToolError, "still running 0.2 seconds after SIGKILL"):
                run_logged([sys.executable, "-c", "import time; time.sleep(60)"], self.work,
                           "kill-bound.log", timeout=1)

    def test_schema_fallback_cannot_satisfy_body_request(self):
        app = self.work.parent / "source-recovery-fixture.app"
        binary, metadata = app / "binary", app / "metadata"
        empty = {"image_count": 1, "type_count": 1, "method_count": 1, "field_count": 1,
                 "version": 31, "images": [{"name": "Game.Runtime.dll"}]}
        failed = {"tool": "cpp2il", "status": "failed", "assembly_files": []}
        fallback = {"tool": "dumper", "status": "ready", "output_dir": str(self.work / "fallback"),
                    "assembly_files": ["Game.Runtime.dll"], "process": {"log": "fallback.log"}}
        with mock.patch("tools.lucidlib.recovery._resolve_input", return_value=(app, binary, metadata, "2022.3.54f1")), \
             mock.patch("tools.lucidlib.recovery.parse_metadata", return_value=empty), \
             mock.patch("tools.lucidlib.recovery.sha256_file", return_value="source-hash"), \
             mock.patch("tools.lucidlib.recovery._thin_binary", return_value=(binary, {"name": "x86_64"})), \
             mock.patch("tools.lucidlib.recovery._cpp2il", return_value=failed), \
             mock.patch("tools.lucidlib.recovery._dumper", return_value=fallback):
            report = recover_code(app, self.work, mode="bodies")
        self.assertEqual(report["status"], "failed")
        self.assertFalse(report["playable_game"])
        self.assertEqual(report["verified_gameplay_methods"], 0)
        self.assertIn("declaration-only", report["limitations"][-1])

    def test_extensionless_generated_image_is_not_falsely_missing(self):
        app = self.work.parent / "source-recovery-fixture.app"
        binary, metadata = app / "binary", app / "metadata"
        fixture = {"image_count": 1, "type_count": 1, "method_count": 1, "field_count": 1,
                   "version": 31, "images": [{"name": "__Generated"}]}
        succeeded = {"tool": "cpp2il", "status": "ready", "output_dir": str(self.work),
                     "assembly_files": [str(self.work / "__Generated.dll")], "process": {"log": "schema.log"}}
        quality = {"assemblies": [{"types": 1, "methods": 1}], "counts": {"schema_placeholder": 1}}
        with mock.patch("tools.lucidlib.recovery._resolve_input", return_value=(app, binary, metadata, "2022.3.54f1")), \
             mock.patch("tools.lucidlib.recovery.parse_metadata", return_value=fixture), \
             mock.patch("tools.lucidlib.recovery.sha256_file", return_value="source-hash"), \
             mock.patch("tools.lucidlib.recovery._thin_binary", return_value=(binary, {"name": "x86_64"})), \
             mock.patch("tools.lucidlib.recovery._cpp2il", return_value=succeeded), \
             mock.patch("tools.lucidlib.recovery._inspect_managed_output", return_value=quality) as inspect:
            report = recover_code(app, self.work)
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["missing_assemblies"], [])
        self.assertTrue(inspect.call_args.kwargs["schemas"])

    def test_failed_managed_inspection_is_reported_as_a_missing_inventory(self):
        app = self.work.parent / "source-recovery-fixture.app"
        binary, metadata = app / "binary", app / "metadata"
        fixture = {"image_count": 1, "type_count": 1, "method_count": 1, "field_count": 1,
                   "version": 31, "images": [{"name": "Game"}]}
        succeeded = {"tool": "cpp2il", "status": "ready", "output_dir": str(self.work),
                     "assembly_files": [str(self.work / "Game.dll")], "process": {"log": "schema.log"}}
        with mock.patch("tools.lucidlib.recovery._resolve_input", return_value=(app, binary, metadata, "2022.3.54f1")), \
             mock.patch("tools.lucidlib.recovery.parse_metadata", return_value=fixture), \
             mock.patch("tools.lucidlib.recovery.sha256_file", return_value="source-hash"), \
             mock.patch("tools.lucidlib.recovery._thin_binary", return_value=(binary, {"name": "x86_64"})), \
             mock.patch("tools.lucidlib.recovery._cpp2il", return_value=succeeded), \
             mock.patch("tools.lucidlib.recovery._inspect_managed_output",
                        side_effect=ToolError("injected inspector failure")):
            report = recover_code(app, self.work)
        # Emitted declarations stay ready; the lost inventory is explicit, not silent.
        self.assertEqual(report["status"], "ready")
        self.assertEqual({"status": "failed", "error": "injected inspector failure", "method_inventory": None},
                         report["quality"])
        self.assertNotIn("output_methods", report["counts"])
        self.assertIn("Managed-output inspection could not finish: injected inspector failure", report["limitations"])

    def test_extensionless_managed_assembly_is_included_in_output_coverage(self):
        generated = self.work / "__Generated"
        generated.write_bytes(b"MZ" + b"\0" * 30)
        normal = self.work / "Game.Runtime.dll"
        normal.write_bytes(b"MZ" + b"\0" * 30)
        (self.work / "script.json").write_text('{"addresses": []}')
        self.assertEqual(_managed_files(self.work), sorted([generated, normal]))


class IlQualityTests(unittest.TestCase):
    def test_throwing_and_default_bodies_are_never_verified(self):
        cases = [b"\x14\x7a", b"\x72\1\0\0\x70\x73\1\0\0\x0a\x7a", b"\x16\x2a",
                 b"\x02\x28\1\0\0\x0a\x2a", b"\x12\0\xfe\x15\1\0\0\x02\x06\x2a"]
        for body in cases:
            with self.subTest(body=body):
                self.assertIn(classify_il(body), ("throwing_stub_or_trivial_throw", "trivial_or_default_body"))

    def test_nontrivial_il_remains_provisional(self):
        self.assertEqual(classify_il(b"\x02\x03\x58\x2a"), "provisional_il_body")
        self.assertEqual(classify_il(b"", has_body=False), "declaration_only")


if __name__ == "__main__":
    unittest.main()

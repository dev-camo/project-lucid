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
import types
import unittest
from unittest import mock
import zipfile

from tools.lucidlib.bootstrap import (CACHE_ROOT, REPO_ROOT, ToolError, _communicate, _release_tree, bootstrap,
                                   download_artifact, host_lock, install_dir_name, managed_path, run_logged, run_owned,
                                   unpack_archive, validate_work_dir)
from tools.lucidlib import recovery
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

    def test_dumper_runs_on_the_dotnet_artifact_with_major_roll_forward(self):
        tool = self.work / "tools" / "dumper" / "Il2CppDumper.dll"
        ready = {"tools": {"dumper": {"status": "ready", "path": str(tool), "version": "6.7.46"}}}
        seen = {}

        def fake_run(command, work_dir, log, timeout, env):
            seen.update(command=command, env=env)
            return {"returncode": 1, "timed_out": False, "log": log}

        with mock.patch.object(recovery, "bootstrap", return_value=ready) as boot, \
                mock.patch.object(recovery, "run_logged", side_effect=fake_run):
            result = recovery._dumper(self.work / "thin", self.work / "meta", self.work, "run")
        boot.assert_called_once_with(self.work, ["dumper"])
        # Pin-keyed install of the locked .NET 10 artifact (#611), not an unversioned tools/dotnet.
        root = self.work / "tools" / install_dir_name("dotnet", host_lock()["artifacts"]["dotnet"])
        self.assertEqual("failed", result["status"])
        self.assertEqual(str(root), seen["env"]["DOTNET_ROOT"])
        self.assertEqual("Major", seen["env"]["DOTNET_ROLL_FORWARD"])
        self.assertEqual("dotnet", Path(seen["command"][0]).stem)
        self.assertEqual(root, Path(seen["command"][0]).parent)
        self.assertEqual(str(tool), seen["command"][1])
        self.assertNotIn("runtime6", " ".join(seen["command"]) + str(seen["env"]))

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

    def _run_owned_leader(self, worker, *, leader=None, timeout=3):
        pid_file = self.work / "owned-worker.pid"
        command = [sys.executable, "-c", leader or self._LEADER, worker, str(pid_file)]
        try:
            return run_owned(command, timeout=timeout), pid_file
        finally:
            self.addCleanup(self._kill_recorded, pid_file)

    @staticmethod
    def _kill_recorded(pid_file):
        if pid_file.exists():
            try:
                os.kill(int(pid_file.read_text()), signal.SIGKILL)
            except ProcessLookupError:
                pass

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_owned_run_timeout_stops_the_tree_before_raising_the_original_error(self):
        # A SIGTERM-ignoring descendant must be stopped before the caller can release
        # its lock (#461); the caller still sees the original TimeoutExpired.
        worker = self._worker("signal.signal(signal.SIGTERM, signal.SIG_IGN)\n")
        with self.assertRaises(subprocess.TimeoutExpired):
            self._run_owned_leader(worker)
        pid_file = self.work / "owned-worker.pid"
        self.assertTrue(pid_file.exists(), "worker was not ready before the timeout")
        self.assertTrue(self._process_stopped(int(pid_file.read_text())),
                        "descendant was still running when the timeout propagated")

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_owned_run_stops_a_descendant_that_outlives_a_successful_leader(self):
        exits_when_ready = self._LEADER.replace("time.sleep(60)", "sys.exit(0)")
        result, pid_file = self._run_owned_leader(self._worker(""), leader=exits_when_ready, timeout=30)
        self.assertEqual(0, result.returncode)
        self.assertTrue(self._process_stopped(int(pid_file.read_text())),
                        "descendant was still running after the command returned")

    def test_owned_run_returns_like_subprocess_run(self):
        result = run_owned([sys.executable, "-c", "import sys; print('out'); sys.exit(3)"],
                                     timeout=30, capture_output=True, text=True)
        self.assertEqual((3, "out\n"), (result.returncode, result.stdout))

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

    def _unstoppable_group(self):
        return (mock.patch("tools.lucidlib.bootstrap.group_has_live_members", return_value=True),
                mock.patch("tools.lucidlib.bootstrap.TERM_GRACE_SECONDS", 0.2),
                mock.patch("tools.lucidlib.bootstrap.KILL_WAIT_SECONDS", 0.2))

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_owned_interrupt_raises_tool_error_when_the_group_outlives_the_sigkill_bound(self):
        # run_owned keeps the same contract: the final teardown barrier, which also fails
        # here, must not replace the chained ToolError; its failure is noted instead.
        live, grace, bound = self._unstoppable_group()
        with live, grace, bound, mock.patch.object(subprocess.Popen, "communicate", side_effect=KeyboardInterrupt):
            with self.assertRaises(ToolError) as caught:
                run_owned([sys.executable, "-c", "import time; time.sleep(60)"], timeout=60)
        self.assertIsInstance(caught.exception.__cause__, KeyboardInterrupt)
        self.assertEqual(1, len(caught.exception.__notes__))
        self.assertRegex(caught.exception.__notes__[0], r"^Final teardown also failed: .*after SIGKILL")

    @unittest.skipUnless(os.name == "posix", "process-group teardown is POSIX-only")
    def test_logged_interrupt_notes_the_failed_final_teardown(self):
        live, grace, bound = self._unstoppable_group()
        with live, grace, bound, mock.patch.object(subprocess.Popen, "wait", side_effect=KeyboardInterrupt):
            with self.assertRaises(ToolError) as caught:
                run_logged([sys.executable, "-c", "import time; time.sleep(60)"], self.work,
                           "interrupt-bound-note.log", timeout=60)
        self.assertIsInstance(caught.exception.__cause__, KeyboardInterrupt)
        self.assertEqual(1, len(caught.exception.__notes__))
        self.assertRegex(caught.exception.__notes__[0], r"^Final teardown also failed: .*after SIGKILL")

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



class _Interrupter:
    """An injected interruption owned by one test: once ready() holds it calls
    _thread.interrupt_main() count times. That is the Python-level SIGINT simulation
    through the installed handler, not a console CTRL_C_EVENT. It never fires if
    readiness times out or it is cancelled first; cancel() also joins the thread."""

    def __init__(self, ready, count=1, gap=0.1, timeout=30):
        import _thread
        self.cancelled = threading.Event()
        self.fired = 0
        self.timed_out = False

        def run():
            deadline = time.monotonic() + timeout
            while not ready():
                if self.cancelled.is_set():
                    return
                if time.monotonic() >= deadline:
                    self.timed_out = True
                    return
                time.sleep(0.02)
            for index in range(count):
                if index:
                    time.sleep(gap)
                if self.cancelled.is_set():
                    return
                _thread.interrupt_main()
                self.fired += 1
        self.thread = threading.Thread(target=run, daemon=True)
        self.thread.start()

    def cancel(self):
        self.cancelled.set()
        self.thread.join(10)


class _OwnedProcessCleanup:
    """Failure cleanup for the processes a Windows fixture recorded. Every recorded
    process is attempted, and the fixture's folder is removed only once every one is
    confirmed stopped: TerminateProcess is asynchronous, so only WAIT_OBJECT_0 confirms
    it, and OpenProcess failing with ERROR_INVALID_PARAMETER confirms the PID is gone.
    kernel32 and last_error are injectable so failed outcomes can be controlled."""

    _OPEN_ACCESS = 0x0001 | 0x00100000  # PROCESS_TERMINATE | SYNCHRONIZE
    _WAIT_OBJECT_0, _WAIT_TIMEOUT, _WAIT_FAILED = 0, 0x102, 0xFFFFFFFF
    _ERROR_INVALID_PARAMETER = 87

    def __init__(self, kernel32, last_error, wait_ms=10000):
        self.kernel32, self.last_error, self.wait_ms = kernel32, last_error, wait_ms

    def stop(self, pid):
        """None once the process is confirmed stopped, otherwise a diagnostic."""
        handle = self.kernel32.OpenProcess(self._OPEN_ACCESS, False, pid)
        if not handle:
            error = self.last_error()
            if error == self._ERROR_INVALID_PARAMETER:
                return None
            return f"process {pid}: OpenProcess failed with error {error}"
        try:
            terminate_error = None if self.kernel32.TerminateProcess(handle, 1) else self.last_error()
            result = self.kernel32.WaitForSingleObject(handle, self.wait_ms)
            if result == self._WAIT_OBJECT_0:
                return None
            if result == self._WAIT_TIMEOUT:
                outcome = "WAIT_TIMEOUT"
            elif result == self._WAIT_FAILED:
                outcome = f"WAIT_FAILED with error {self.last_error()}"
            else:
                outcome = f"wait result {result:#x}"
            if terminate_error is not None:
                outcome = f"TerminateProcess failed with error {terminate_error}, then {outcome}"
            return f"process {pid}: {outcome}"
        finally:
            self.kernel32.CloseHandle(handle)

    def release(self, folder, pids):
        """Stop every recorded process, then remove folder only if all are confirmed
        stopped. Returns {pid: diagnostic} for those that are not; folder then stays."""
        unconfirmed = {}
        for pid in pids:
            try:
                diagnostic = self.stop(pid)
            except Exception as error:  # keep attempting the remaining members
                diagnostic = f"process {pid}: {error!r}"
            if diagnostic is not None:
                unconfirmed[pid] = diagnostic
        if not unconfirmed:
            shutil.rmtree(folder, True)
        return unconfirmed


class OwnedProcessCleanupTests(unittest.TestCase):
    """Deterministic controls for _OwnedProcessCleanup with a scripted kernel32."""

    class _Kernel32:
        def __init__(self, opened=True, terminated=True, wait=0, open_raises=()):
            self.opened, self.terminated, self.wait = opened, terminated, wait
            self.open_raises, self.calls, self.closed = set(open_raises), [], []

        def OpenProcess(self, access, inherit, pid):
            self.calls.append(("open", pid))
            if pid in self.open_raises:
                raise OSError(f"injected OpenProcess fault for {pid}")
            return 1000 + pid if self.opened else 0

        def TerminateProcess(self, handle, code):
            self.calls.append(("terminate", handle))
            return 1 if self.terminated else 0

        def WaitForSingleObject(self, handle, milliseconds):
            self.calls.append(("wait", handle))
            return self.wait

        def CloseHandle(self, handle):
            self.closed.append(handle)
            return 1

    def setUp(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.folder = Path(tempfile.mkdtemp(prefix="test-owned-cleanup-", dir=CACHE_ROOT))
        self.addCleanup(shutil.rmtree, self.folder, True)
        (self.folder / "level0.pid").write_text("11")

    def _release(self, kernel32, pids=(11, 12), error=5):
        return _OwnedProcessCleanup(kernel32, lambda: error).release(self.folder, list(pids))

    def test_confirmed_stops_remove_the_folder(self):
        kernel32 = self._Kernel32()
        self.assertEqual({}, self._release(kernel32))
        self.assertFalse(self.folder.exists())
        self.assertEqual([1011, 1012], kernel32.closed)

    def test_a_vanished_process_counts_as_stopped(self):
        self.assertEqual({}, self._release(self._Kernel32(opened=False), error=87))
        self.assertFalse(self.folder.exists())

    def test_failed_termination_with_a_timed_out_wait_keeps_the_folder(self):
        kernel32 = self._Kernel32(terminated=False, wait=0x102)
        unconfirmed = self._release(kernel32)
        self.assertEqual({11: "process 11: TerminateProcess failed with error 5, then WAIT_TIMEOUT",
                          12: "process 12: TerminateProcess failed with error 5, then WAIT_TIMEOUT"}, unconfirmed)
        self.assertTrue((self.folder / "level0.pid").exists())
        self.assertEqual([1011, 1012], kernel32.closed)

    def test_failed_termination_with_a_failed_wait_keeps_the_folder(self):
        unconfirmed = self._release(self._Kernel32(terminated=False, wait=0xFFFFFFFF), pids=(11,))
        self.assertEqual({11: "process 11: TerminateProcess failed with error 5, then WAIT_FAILED with error 5"},
                         unconfirmed)
        self.assertTrue(self.folder.exists())

    def test_a_timed_out_wait_alone_is_not_a_confirmed_stop(self):
        self.assertEqual({11: "process 11: WAIT_TIMEOUT"}, self._release(self._Kernel32(wait=0x102), pids=(11,)))
        self.assertTrue(self.folder.exists())

    def test_an_unopenable_process_is_not_a_confirmed_stop(self):
        unconfirmed = self._release(self._Kernel32(opened=False), pids=(11,), error=5)
        self.assertEqual({11: "process 11: OpenProcess failed with error 5"}, unconfirmed)
        self.assertTrue(self.folder.exists())

    def test_one_failing_member_does_not_stop_the_others_being_attempted(self):
        kernel32 = self._Kernel32(open_raises={11})
        unconfirmed = self._release(kernel32, pids=(11, 12, 13))
        self.assertEqual([11], list(unconfirmed))
        self.assertIn("injected OpenProcess fault for 11", unconfirmed[11])
        self.assertEqual([("open", 11), ("open", 12), ("terminate", 1012), ("wait", 1012),
                          ("open", 13), ("terminate", 1013), ("wait", 1013)], kernel32.calls)
        self.assertTrue(self.folder.exists())


class ReleaseTreeProvenanceTests(unittest.TestCase):
    """A failed final teardown barrier never replaces the exception already propagating."""

    class _Leader:
        _lucid_job = None
        pid = 0

        def poll(self):
            return None

    def _release(self, pending):
        with mock.patch("tools.lucidlib.bootstrap._stop_tool", side_effect=ToolError("barrier could not stop")):
            _release_tree(self._Leader(), pending)

    def test_a_pending_tool_error_keeps_its_cause_and_notes_the_barrier(self):
        interruption = KeyboardInterrupt()
        pending = ToolError("stop could not finish")
        pending.__cause__ = interruption
        self._release(pending)  # returns: the caller re-raises pending unchanged
        self.assertIs(pending.__cause__, interruption)
        self.assertEqual(["Final teardown also failed: barrier could not stop"], pending.__notes__)

    def test_any_other_pending_exception_becomes_the_barrier_errors_cause(self):
        interruption = KeyboardInterrupt()
        with self.assertRaisesRegex(ToolError, "barrier could not stop") as caught:
            self._release(interruption)
        self.assertIs(caught.exception.__cause__, interruption)

    def test_the_note_survives_without_add_note(self):
        # Python 3.9 and 3.10 have no BaseException.add_note; the note still reaches __notes__.
        class LegacyToolError(ToolError):
            add_note = property(lambda self: (_ for _ in ()).throw(AttributeError("add_note")))

        interruption = KeyboardInterrupt()
        pending = LegacyToolError("stop could not finish")
        pending.__cause__ = interruption
        self.assertFalse(hasattr(pending, "add_note"))
        self._release(pending)
        self.assertIs(pending.__cause__, interruption)
        self.assertEqual(["Final teardown also failed: barrier could not stop"], pending.__notes__)

    def test_without_a_pending_exception_the_barrier_error_propagates(self):
        with self.assertRaisesRegex(ToolError, "barrier could not stop") as caught:
            self._release(None)
        self.assertIsNone(caught.exception.__cause__)


class ReaderCustodyTests(unittest.TestCase):
    """The Windows communicate reader is owned by cleanup exactly when it runs, and the
    final barrier fails, under the same pending policy, if it is still running after the
    bounded join. Real threads on inert processes; every probe thread is released and
    joined afterwards."""

    class _Process:
        _lucid_job = None
        pid = 0
        args = ["inert"]

        def __init__(self, release=None):
            self.release = release

        def poll(self):
            return 0

        def communicate(self):
            if self.release is not None:
                self.release.wait()
            return (b"", b"")

    def setUp(self):
        for patcher in (mock.patch("tools.lucidlib.bootstrap.group_has_live_members", return_value=False),
                        mock.patch("tools.lucidlib.bootstrap.KILL_WAIT_SECONDS", 0.05)):
            patcher.start()
            self.addCleanup(patcher.stop)

    def _windows_communicate(self, process):
        with mock.patch("tools.lucidlib.bootstrap.os", types.SimpleNamespace(name="nt")):
            return _communicate(process, 5)

    def _stuck(self):
        """An inert process whose published reader is held on an Event."""
        release = threading.Event()
        process = self._Process(release)
        reader = threading.Thread(target=release.wait, daemon=True)
        reader.start()
        process._lucid_reader = reader
        self.addCleanup(reader.join)
        self.addCleanup(release.set)
        return process

    def test_a_reader_that_fails_to_start_is_never_handed_to_cleanup(self):
        for failure in (RuntimeError("can't start new thread"), KeyboardInterrupt()):
            process = self._Process()
            with mock.patch.object(threading.Thread, "start", side_effect=failure):
                with self.assertRaises(type(failure)) as caught:
                    self._windows_communicate(process)
            self.assertIs(caught.exception, failure)
            self.assertFalse(hasattr(process, "_lucid_reader"))
            _release_tree(process, failure)  # nothing to join: the primary failure stands alone
            self.assertIsNone(failure.__cause__)
            self.assertFalse(hasattr(failure, "__notes__"))

    @unittest.skipUnless(hasattr(signal, "raise_signal"), "needs signal.raise_signal")
    def test_an_interrupt_during_startup_waits_until_the_started_reader_has_an_owner(self):
        release = threading.Event()
        process = self._Process(release)
        start = threading.Thread.start
        published = []

        def start_then_interrupt(thread):
            start(thread)
            self.addCleanup(thread.join)  # registered after release.set, so it runs first: release here too
            self.addCleanup(release.set)
            signal.raise_signal(signal.SIGINT)
            published.append(hasattr(process, "_lucid_reader"))

        self.addCleanup(release.set)
        with mock.patch.object(threading.Thread, "start", start_then_interrupt):
            with self.assertRaises(KeyboardInterrupt) as caught:
                self._windows_communicate(process)
        self.assertEqual([False], published)  # held across the gap, delivered after publication
        self.assertTrue(process._lucid_reader.is_alive())
        release.set()
        _release_tree(process, caught.exception)
        self.assertFalse(process._lucid_reader.is_alive())

    def test_a_reader_that_stops_lets_the_barrier_complete(self):
        process = self._Process()
        self.assertEqual((b"", b""), self._windows_communicate(process))
        _release_tree(process)
        self.assertFalse(process._lucid_reader.is_alive())

    def test_a_reader_outliving_the_join_fails_a_cleanup_only_barrier(self):
        process = self._stuck()
        with self.assertRaisesRegex(ToolError, "output reader .* still running") as caught:
            _release_tree(process)
        self.assertIsNone(caught.exception.__cause__)

    def test_a_reader_outliving_the_join_is_caused_by_a_pending_interrupt(self):
        process = self._stuck()
        interruption = KeyboardInterrupt()
        with self.assertRaisesRegex(ToolError, "output reader .* still running") as caught:
            _release_tree(process, interruption)
        self.assertIs(caught.exception.__cause__, interruption)

    def test_a_reader_outliving_the_join_is_noted_on_a_pending_tool_error(self):
        process = self._stuck()
        interruption = KeyboardInterrupt()
        pending = ToolError("stop could not finish")
        pending.__cause__ = interruption
        _release_tree(process, pending)  # returns: the caller re-raises pending unchanged
        self.assertIs(pending.__cause__, interruption)
        self.assertEqual(1, len(pending.__notes__))
        self.assertRegex(pending.__notes__[0], r"^Final teardown also failed: The output reader .* still running")

    def test_a_stop_failure_stays_primary_and_notes_the_stuck_reader(self):
        process = self._stuck()
        process.poll = lambda: None
        with mock.patch("tools.lucidlib.bootstrap._stop_tool", side_effect=ToolError("barrier could not stop")):
            with self.assertRaisesRegex(ToolError, "barrier could not stop") as caught:
                _release_tree(process)
        self.assertEqual(1, len(caught.exception.__notes__))
        self.assertRegex(caught.exception.__notes__[0], r"^The output reader also failed to stop: .* still running")

    def test_a_pending_tool_error_keeps_both_the_stop_and_reader_diagnostics(self):
        process = self._stuck()
        process.poll = lambda: None
        interruption = KeyboardInterrupt()
        pending = ToolError("stop could not finish")
        pending.__cause__ = interruption
        with mock.patch("tools.lucidlib.bootstrap._stop_tool", side_effect=ToolError("barrier could not stop")):
            _release_tree(process, pending)  # returns: the caller re-raises pending unchanged
        self.assertIs(pending.__cause__, interruption)
        self.assertEqual(2, len(pending.__notes__))
        self.assertEqual("Final teardown also failed: barrier could not stop", pending.__notes__[0])
        self.assertRegex(pending.__notes__[1], r"^The output reader also failed to stop: .* still running")

    def test_an_unexpected_stop_failure_propagates_after_every_cleanup_attempt(self):
        process = self._stuck()
        process.poll = lambda: None
        job = mock.Mock()
        job.active_processes.return_value = [1]
        job.stop.side_effect = OSError("terminate failed")
        process._lucid_job = job
        with self.assertRaisesRegex(OSError, "terminate failed") as caught:
            _release_tree(process)
        job.close.assert_called_once_with()
        self.assertRegex(caught.exception.__notes__[0], r"^The output reader also failed to stop")


@unittest.skipUnless(os.name == "nt", "Job Object teardown is Windows-only")
class WindowsJobTeardownTests(unittest.TestCase):
    """Real Windows process trees (#458): a leader starts a child that starts a
    grandchild, and the whole tree must have stopped before control returns to the
    caller's cleanup, while an unrelated sentinel process keeps running."""

    # argv: this code, level, folder, finish. Each level starts the next (up to level 2)
    # before publishing its PID; the leader waits for the grandchild, then sleeps or exits.
    _CHAIN = ("import os, subprocess, sys, time\n"
              "code, level, folder, finish = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]\n"
              "if level < 2:\n"
              "    subprocess.Popen([sys.executable, '-c', code, code, str(level + 1), folder, finish])\n"
              "tmp = os.path.join(folder, 'level%d.tmp' % level)\n"
              "open(tmp, 'w').write(str(os.getpid()))\n"
              "os.replace(tmp, os.path.join(folder, 'level%d.pid' % level))\n"
              "if level == 0:\n"
              "    end = time.monotonic() + 30\n"
              "    while not os.path.exists(os.path.join(folder, 'level2.pid')) and time.monotonic() < end:\n"
              "        time.sleep(0.02)\n"
              "    if finish == 'exit':\n"
              "        sys.exit(0)\n"
              "time.sleep(60)\n")
    _STILL_ACTIVE = 259

    def setUp(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix="test-job-", dir=CACHE_ROOT))
        # Registered first, so it runs last: after every other cleanup, it stops any
        # recorded tree member and removes the folder only once all are confirmed stopped.
        self.addCleanup(self._release_recorded)
        import ctypes
        from ctypes import wintypes
        self.kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel32.OpenProcess.restype = wintypes.HANDLE
        self.kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        self.kernel32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, wintypes.LPDWORD]
        self.kernel32.TerminateProcess.argtypes = [wintypes.HANDLE, wintypes.UINT]
        self.kernel32.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        self.kernel32.WaitForSingleObject.restype = wintypes.DWORD  # WAIT_FAILED is 0xFFFFFFFF, not -1
        self.kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
        self.wintypes = wintypes
        self.ctypes = ctypes
        self.owned = _OwnedProcessCleanup(self.kernel32, ctypes.get_last_error)
        self.tree = self.work / "tree"
        self.tree.mkdir()
        self.sentinel = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"])
        self.addCleanup(self._stop_sentinel)

    def _stop_sentinel(self):
        self.sentinel.kill()
        self.sentinel.wait(10)

    def _release_recorded(self):
        # Failure cleanup. Unconfirmed members keep the folder and their PID files, and
        # are reported here; unittest reports this separately from the test's own result.
        pids = [pid for pid in map(self._pid, range(3)) if pid is not None] if hasattr(self, "tree") else []
        unconfirmed = self.owned.release(self.work, pids) if hasattr(self, "owned") else {}
        if not hasattr(self, "owned"):
            shutil.rmtree(self.work, True)  # setUp failed before any process could start
        if unconfirmed:
            raise AssertionError(f"Recorded tree members were not confirmed stopped; keeping {self.work}: "
                                 + "; ".join(unconfirmed.values()))

    def _pid(self, level):
        path = self.tree / f"level{level}.pid"
        return int(path.read_text()) if path.exists() else None

    def _running(self, pid):
        # Never os.kill(pid, 0) here: on Windows it terminates the process.
        handle = self.kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return False
        try:
            code = self.wintypes.DWORD()
            self.assertTrue(self.kernel32.GetExitCodeProcess(handle, self.ctypes.byref(code)))
            return code.value == self._STILL_ACTIVE
        finally:
            self.kernel32.CloseHandle(handle)

    def _command(self, finish="sleep"):
        return [sys.executable, "-c", self._CHAIN, self._CHAIN, "0", str(self.tree), finish]

    def _grandchild_ready(self):
        return (self.tree / "level2.pid").exists()

    def _interrupter(self, **options):
        interrupter = _Interrupter(self._grandchild_ready, **options)
        self.addCleanup(interrupter.cancel)
        return interrupter

    def _assert_tree_stopped(self, levels=(0, 1, 2)):
        for level in levels:
            pid = self._pid(level)
            self.assertIsNotNone(pid, f"level {level} never started")
            self.assertFalse(self._running(pid), f"level {level} was still running")
        self.assertIsNone(self.sentinel.poll(), "the unrelated sentinel was stopped")

    def _assert_interrupted_outside(self, call, function):
        # A held interruption is delivered once the protected step has finished, so its
        # traceback does not pass through the function it arrived in. (assertRaises would
        # discard the traceback, so the interruption is caught here.)
        try:
            call()
        except KeyboardInterrupt as error:
            frames, tb = [], error.__traceback__
            while tb is not None:
                frames.append(tb.tb_frame.f_code.co_name)
                tb = tb.tb_next
            self.assertNotIn(function, frames, frames)
            return
        self.fail("no interruption reached the caller")

    def test_interrupter_never_fires_after_its_readiness_times_out(self):
        interrupter = _Interrupter(lambda: False, timeout=0.2)
        interrupter.thread.join(5)
        self.assertTrue(interrupter.timed_out)
        self.assertEqual(0, interrupter.fired)

    def test_logged_timeout_stops_the_whole_tree(self):
        result = run_logged(self._command(), self.work, "job-timeout.log", timeout=3)
        self.assertTrue(result["timed_out"])
        self.assertNotEqual(0, result["returncode"])
        self.assertIn("stopped the process", Path(result["log"]).read_text())
        self._assert_tree_stopped()

    def test_owned_timeout_stops_the_tree_before_raising(self):
        with self.assertRaises(subprocess.TimeoutExpired):
            run_owned(self._command(), timeout=3)
        self._assert_tree_stopped()

    def test_logged_run_stops_descendants_left_by_a_finished_leader(self):
        result = run_logged(self._command("exit"), self.work, "job-orphan.log", timeout=30)
        self.assertFalse(result["timed_out"])
        self.assertEqual(0, result["returncode"])
        self._assert_tree_stopped()

    def test_owned_run_stops_descendants_left_by_a_finished_leader(self):
        result = run_owned(self._command("exit"), timeout=30)
        self.assertEqual(0, result.returncode)
        self._assert_tree_stopped()

    def test_owned_run_keeps_its_output_and_exit_code(self):
        result = run_owned([sys.executable, "-c", "import sys; print('out'); sys.exit(3)"],
                           timeout=30, capture_output=True, text=True)
        self.assertEqual((3, "out"), (result.returncode, result.stdout.strip()))

    def test_interrupt_stops_the_tree_before_the_caller_continues(self):
        started = time.monotonic()
        interrupter = self._interrupter()
        with self.assertRaises(KeyboardInterrupt):
            run_logged(self._command(), self.work, "job-interrupt.log", timeout=60)
        interrupter.cancel()
        self.assertEqual(1, interrupter.fired)
        # The wait is sliced, so the interruption lands long before the timeout.
        self.assertLess(time.monotonic() - started, 30)
        self._assert_tree_stopped()
        self.assertIn("after an interruption", (self.work / "reports" / "job-interrupt.log").read_text())

    def test_owned_interrupt_stops_the_tree_before_the_caller_continues(self):
        interrupter = self._interrupter()
        with self.assertRaises(KeyboardInterrupt):
            run_owned(self._command(), timeout=60)
        interrupter.cancel()
        self.assertEqual(1, interrupter.fired)
        self._assert_tree_stopped()

    def test_repeated_interrupt_during_the_stop_is_ignored(self):
        # A second interruption while the job is being stopped must neither cut the stop
        # short nor surface afterwards: SIGINT is ignored for the duration of the stop.
        from tools.lucidlib import bootstrap
        import _thread
        real_stop = bootstrap._WindowsJob.stop
        repeats = []

        def stop_with_a_repeat(job, process):
            _thread.interrupt_main()
            repeats.append(True)
            real_stop(job, process)
        self._interrupter()
        with mock.patch.object(bootstrap._WindowsJob, "stop", stop_with_a_repeat):
            with self.assertRaises(KeyboardInterrupt):
                run_logged(self._command(), self.work, "job-repeat.log", timeout=60)
        time.sleep(0.2)  # a repeat that was not ignored would be raised by now
        self.assertEqual([True], repeats)
        self._assert_tree_stopped()

    def _interrupt_during_handoff(self):
        # The interruption arrives inside popen, after the tree is running in its job
        # and before the process reaches the caller.
        from tools.lucidlib import bootstrap
        import _thread
        real_resume = bootstrap._WindowsJob._resume
        calls = []

        def resume_then_interrupt(job, process):
            real_resume(job, process)
            deadline = time.monotonic() + 30
            while not self._grandchild_ready() and time.monotonic() < deadline:
                time.sleep(0.02)
            self.assertTrue(self._grandchild_ready())
            calls.append(process.pid)
            _thread.interrupt_main()
        return mock.patch.object(bootstrap._WindowsJob, "_resume", resume_then_interrupt), calls

    def test_logged_interrupt_during_the_handoff_reaches_cleanup_with_the_tree_owned(self):
        patch, calls = self._interrupt_during_handoff()
        with patch:
            self._assert_interrupted_outside(
                lambda: run_logged(self._command(), self.work, "job-handoff.log", timeout=60), "resume_then_interrupt")
        self.assertEqual(1, len(calls))
        self._assert_tree_stopped()
        self.assertIn("after an interruption", (self.work / "reports" / "job-handoff.log").read_text())

    def test_owned_interrupt_during_the_handoff_reaches_cleanup_with_the_tree_owned(self):
        patch, calls = self._interrupt_during_handoff()
        with patch:
            self._assert_interrupted_outside(lambda: run_owned(self._command(), timeout=60), "resume_then_interrupt")
        self.assertEqual(1, len(calls))
        self._assert_tree_stopped()

    def _interrupt_during_the_exit_query(self):
        # The first membership query is the normal-exit one; the interruption arrives
        # inside it, after the leader has finished and while descendants still run.
        from tools.lucidlib import bootstrap
        import _thread
        real_query, real_stop = bootstrap._WindowsJob.active_processes, bootstrap._WindowsJob.stop
        events = []

        def query(job):
            if not events:
                events.append("query")
                _thread.interrupt_main()
            return real_query(job)

        def stop(job, process):
            events.append("stop")
            real_stop(job, process)
        patches = (mock.patch.object(bootstrap._WindowsJob, "active_processes", query),
                   mock.patch.object(bootstrap._WindowsJob, "stop", stop))
        return patches, events

    def test_logged_interrupt_during_the_exit_query_is_delivered_after_the_stop(self):
        (query, stop), events = self._interrupt_during_the_exit_query()
        with query, stop:
            self._assert_interrupted_outside(
                lambda: run_logged(self._command("exit"), self.work, "job-exit-query.log", timeout=30), "query")
        self.assertEqual(["query", "stop"], events)
        self._assert_tree_stopped()

    def test_owned_interrupt_during_the_exit_query_is_delivered_after_the_stop(self):
        (query, stop), events = self._interrupt_during_the_exit_query()
        with query, stop:
            self._assert_interrupted_outside(lambda: run_owned(self._command("exit"), timeout=30), "query")
        self.assertEqual(["query", "stop"], events)
        self._assert_tree_stopped()

    def test_admission_failure_after_assignment_stops_the_job(self):
        # Assigned but not resumed: the job is stopped and waited for, not just the
        # immediate process killed.
        from tools.lucidlib import bootstrap
        leaders, stops = [], []
        real_stop = bootstrap._WindowsJob.stop

        def fail_resume(job, process):
            leaders.append(process.pid)
            raise ToolError("injected resume failure")

        def stop(job, process):
            stops.append(process.pid)
            real_stop(job, process)
        with mock.patch.object(bootstrap._WindowsJob, "_resume", fail_resume), \
                mock.patch.object(bootstrap._WindowsJob, "stop", stop):
            with self.assertRaisesRegex(ToolError, "injected resume failure"):
                run_owned(self._command(), timeout=30)
        self.assertEqual(leaders, stops)
        self.assertFalse(self._running(leaders[0]))
        self.assertIsNone(self.sentinel.poll())

    def test_tree_that_outlives_the_bound_raises_tool_error(self):
        from tools.lucidlib import bootstrap
        with mock.patch.object(bootstrap._WindowsJob, "active_processes", return_value=1), \
                mock.patch("tools.lucidlib.bootstrap.KILL_WAIT_SECONDS", 0.2):
            with self.assertRaisesRegex(ToolError, "still running 0.2 seconds after its job"):
                run_logged(self._command(), self.work, "job-bound.log", timeout=3)
        # Closing the job still kills the tree (KILL_ON_JOB_CLOSE).
        deadline = time.monotonic() + 5
        while any(self._running(self._pid(level)) for level in range(3)) and time.monotonic() < deadline:
            time.sleep(0.05)
        self._assert_tree_stopped()


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

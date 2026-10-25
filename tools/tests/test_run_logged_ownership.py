"""Real process-group lifetime regressions for the logged tool runner (#513)."""

import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock
import uuid

from tools.lucidlib import bootstrap


# Every generation publishes its identity only after installing its handler.
# The leader cannot exit until both descendants have actually started.
_TREE = r'''
import json, os, pathlib, signal, subprocess, sys, time
root, token, role, resistant, status = sys.argv[1:]
root = pathlib.Path(root)
if resistant == "True":
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
record = {"pid": os.getpid(), "pgid": os.getpgrp(), "sid": os.getsid(0), "token": token}
marker = root / (role + ".json")
temporary = marker.with_suffix(".tmp")
temporary.write_text(json.dumps(record))
temporary.replace(marker)
if role != "grandchild":
    next_role = "child" if role == "leader" else "grandchild"
    subprocess.Popen([sys.executable, __file__, str(root), token, next_role, resistant, status])
    deadline = time.monotonic() + 10
    while not (root / "grandchild.json").exists():
        if time.monotonic() >= deadline:
            raise RuntimeError("grandchild did not become ready")
        time.sleep(.01)
if role == "leader":
    print("leader exiting after descendants are ready", flush=True)
    sys.exit(int(status))
time.sleep(60)
'''


@unittest.skipUnless(os.name == "posix", "process-group ownership is POSIX-only; Windows is #458")
class RunLoggedOwnershipTests(unittest.TestCase):
    @staticmethod
    def _state(pid):
        if sys.platform.startswith("linux"):
            try:
                return Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()[0]
            except FileNotFoundError:
                return ""
        return subprocess.run(["ps", "-o", "stat=", "-p", str(pid)],
                              capture_output=True, text=True, check=False).stdout.strip()

    @classmethod
    def _stopped(cls, pid):
        # An orphan zombie awaiting the host reaper is no longer executing.
        state = cls._state(pid)
        return not state or state[0] in "ZX"

    def _cleanup_tree(self, root, token):
        records = [json.loads(path.read_text()) for path in root.glob("*.json")]
        leaders = [record for record in records
                   if record["token"] == token and record["pid"] == record["pgid"] == record["sid"]]
        if not leaders:
            self.assertFalse(records, "cannot establish ownership for fixture cleanup")
            return
        pgid = leaders[0]["pid"]
        for record in records:
            self.assertEqual((token, pgid, pgid),
                             (record["token"], record["pgid"], record["sid"]))
            pid = record["pid"]
            if self._stopped(pid):
                continue
            try:
                current = (os.getpgid(pid), os.getsid(pid))
            except ProcessLookupError:
                continue
            self.assertEqual((pgid, pgid), current, "refusing to signal an unrelated process")
            # A freshly validated live member keeps this owned group/session alive.
            try:
                os.killpg(pgid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            break
        deadline = time.monotonic() + 5
        while any(not self._stopped(record["pid"]) for record in records):
            self.assertLess(time.monotonic(), deadline, "fixture descendants failed to stop")
            time.sleep(.02)

    def _run_tree(self, status, resistant=False):
        with tempfile.TemporaryDirectory(prefix="lucid-run-logged-") as directory:
            root = Path(directory).resolve()
            token = uuid.uuid4().hex
            script = root / "tree.py"
            script.write_text(_TREE)
            command = [sys.executable, str(script), str(root), token, "leader", str(resistant), str(status)]
            signals = []
            real_killpg = os.killpg

            def observe(pgid, sig):
                if sig:
                    signals.append((pgid, sig))
                return real_killpg(pgid, sig)

            # This independently owned process must remain untouched by teardown.
            sentinel = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"],
                                        start_new_session=True)
            try:
                with mock.patch.object(bootstrap, "CACHE_ROOT", root), \
                     mock.patch.object(bootstrap, "TERM_GRACE_SECONDS", .2), \
                     mock.patch.object(os, "killpg", side_effect=observe):
                    result = bootstrap.run_logged(command, root / "work", "tool.log", timeout=15)
                records = [json.loads((root / (role + ".json")).read_text())
                           for role in ("leader", "child", "grandchild")]
                leader = records[0]["pid"]
                self.assertEqual(status, result["returncode"])
                self.assertFalse(result["timed_out"])
                self.assertEqual(command, result["command"])
                self.assertIn("leader exiting after descendants are ready", Path(result["log"]).read_text())
                self.assertIsNone(sentinel.poll(), "unrelated session was stopped")
                for record in records:
                    self.assertEqual((token, leader, leader),
                                     (record["token"], record["pgid"], record["sid"]))
                    self.assertTrue(self._stopped(record["pid"]),
                                    f"process {record['pid']} was still running when run_logged returned")
                graceful = [(leader, signal.SIGTERM)]
                escalated = graceful + [(leader, signal.SIGKILL)]
                if resistant:
                    self.assertEqual(escalated, signals)
                else:
                    # Scheduling can exhaust the grace even for default TERM handlers.
                    self.assertIn(signals, (graceful, escalated))
                self.assertLess(result["elapsed_seconds"], 10, "cleanup exceeded its bounded grace")
            finally:
                try:
                    self._cleanup_tree(root, token)
                finally:
                    # The Popen handle still owns this unreaped direct child.
                    if sentinel.poll() is None:
                        sentinel.terminate()
                    sentinel.wait(timeout=5)

    def test_successful_leader_stops_ready_child_and_grandchild_before_return(self):
        self._run_tree(0)

    def test_nonzero_leader_preserves_status_after_stopping_descendants(self):
        self._run_tree(7)

    def test_term_resistant_descendants_require_bounded_kill_escalation(self):
        self._run_tree(0, resistant=True)

"""Checkout failures are infrastructure evidence, never compiler results."""
from contextlib import redirect_stderr, redirect_stdout
import io
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).parent))
import direct_progress as direct
import main_progress as main


class CheckoutTests(unittest.TestCase):
    HEAD = "a" * 40  # Inert fixture identity, not compiler evidence.
    PHASES = ("init", "remote", "fetch", "checkout", "head")

    @staticmethod
    def phase(command):
        return "head" if "rev-parse" in command else next(
            phase for phase in CheckoutTests.PHASES if phase in command)

    def commands(self, calls, failing=None, exception=None, actual=None):
        def execute(command, **options):
            phase = self.phase(command)
            calls.append((phase, command, options))
            if phase == failing:
                raise exception(command)
            output = ((actual or self.HEAD) + "\n").encode() if phase == "head" else b""
            return subprocess.CompletedProcess(command, 0, output, b"")

        def legacy_head(command, **options):
            result = execute(command, **options)
            return result.stdout.decode() if options.get("text") else result.stdout

        return mock.patch.object(direct, "checkout_run", side_effect=execute), mock.patch.object(
            direct.subprocess, "check_output", side_effect=legacy_head)

    def test_every_failed_git_phase_retains_private_bounded_output_after_cleanup(self):
        for role in ("control", "source"):
            for phase in self.PHASES:
                with self.subTest(role=role, phase=phase), tempfile.TemporaryDirectory() as directory:
                    root = Path(directory); target = root / role; calls = []
                    out = b"discarded stdout prefix" + b"o" * 20000 + b"PRIVATE_STDOUT"
                    err = b"discarded stderr prefix" + b"e" * 20000 + b"PRIVATE_STDERR"
                    patches = self.commands(calls, phase, lambda cmd: subprocess.CalledProcessError(
                        128, cmd, output=out, stderr=err))
                    public_out, public_err = io.StringIO(), io.StringIO()
                    with patches[0], patches[1], redirect_stdout(public_out), redirect_stderr(public_err):
                        with self.assertRaises(subprocess.CalledProcessError) as caught:
                            direct.checkout(target, self.HEAD)
                    self.assertEqual(128, caught.exception.returncode)
                    self.assertEqual(list(self.PHASES[:self.PHASES.index(phase) + 1]), [c[0] for c in calls])
                    receipt = root / ("private-checkout-" + role + ".json")
                    self.assertTrue(receipt.is_file(), "Git diagnostics were discarded")
                    before = receipt.read_bytes(); data = json.loads(before)
                    self.assertEqual(0o600, receipt.stat().st_mode & 0o777)
                    self.assertEqual(role, data["checkout"])
                    self.assertEqual(self.HEAD, data["requested_head"])
                    self.assertEqual(phase, data["phase"])
                    self.assertEqual(calls[-1][1], data["command"])
                    self.assertEqual("CalledProcessError", data["error_type"])
                    self.assertEqual(128, data["returncode"])
                    self.assertEqual(out[-16384:].decode(), data["stdout_tail"])
                    self.assertEqual(err[-16384:].decode(), data["stderr_tail"])
                    self.assertLess(len(before), 200000)
                    self.assertEqual("", public_out.getvalue() + public_err.getvalue())
                    with mock.patch.object(direct, "job_root", return_value=root):
                        direct.cleanup_job()
                    self.assertFalse(target.exists())
                    self.assertEqual(before, receipt.read_bytes())

    def test_each_timed_out_git_phase_retains_output_and_stops_without_retry(self):
        for phase in self.PHASES:
            with self.subTest(phase=phase), tempfile.TemporaryDirectory() as directory:
                root = Path(directory); calls = []
                patches = self.commands(calls, phase, lambda cmd: subprocess.TimeoutExpired(
                    cmd, 300, output=b"partial output", stderr=b"connection stalled"))
                with patches[0], patches[1], self.assertRaises(subprocess.TimeoutExpired):
                    direct.checkout(root / "source", self.HEAD)
                data = json.loads((root / "private-checkout-source.json").read_bytes())
                self.assertEqual(phase, data["phase"])
                self.assertEqual("TimeoutExpired", data["error_type"])
                self.assertIsNone(data["returncode"])
                self.assertEqual(300, data["timeout_seconds"])
                self.assertEqual("partial output", data["stdout_tail"])
                self.assertEqual("connection stalled", data["stderr_tail"])
                self.assertEqual(self.PHASES.index(phase) + 1, len(calls))

    def test_success_bounds_each_git_command_and_checks_the_exact_head(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); calls = []; patches = self.commands(calls)
            with patches[0], patches[1]:
                direct.checkout(root / "source", self.HEAD)
            self.assertEqual(list(self.PHASES), [c[0] for c in calls])
            for _, _, options in calls:
                self.assertEqual(300, options["timeout"])
                self.assertTrue(options["check"])
            self.assertFalse(list(root.glob("private-checkout-*.json")))

    def test_a_different_head_still_fails_closed_and_records_the_difference(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); calls = []; actual = "b" * 40
            patches = self.commands(calls, actual=actual)
            with patches[0], patches[1], self.assertRaisesRegex(ValueError, "Fetched checkout differs"):
                direct.checkout(root / "source", self.HEAD)
            data = json.loads((root / "private-checkout-source.json").read_bytes())
            self.assertEqual("head", data["phase"])
            self.assertEqual(self.HEAD, data["requested_head"])
            self.assertEqual(actual, data["stdout_tail"].strip())

    def test_an_unlaunchable_git_command_retains_its_private_error(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); calls = []
            patches = self.commands(calls, "init", lambda _: FileNotFoundError("git executable missing"))
            with patches[0], patches[1], self.assertRaises(FileNotFoundError):
                direct.checkout(root / "source", self.HEAD)
            data = json.loads((root / "private-checkout-source.json").read_bytes())
            self.assertEqual("init", data["phase"])
            self.assertEqual("FileNotFoundError", data["error_type"])
            self.assertIn("git executable missing", data["error"])

    def test_checkout_error_remains_a_precondition_and_never_launches_the_compiler(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); calls = []
            patches = self.commands(calls, "fetch", lambda cmd: subprocess.CalledProcessError(
                128, cmd, stderr=b"PRIVATE_GIT_FAILURE"))
            output = root / "output"
            env = {"GITHUB_EVENT_NAME": "workflow_run", "GITHUB_OUTPUT": str(output)}
            with patches[0], patches[1], mock.patch.dict(direct.os.environ, env), \
                    mock.patch.object(main, "pr_staleness", return_value=None), \
                    mock.patch.object(direct, "require_workflow_context"), \
                    mock.patch.object(direct, "require_upstream_run", return_value={"conclusion": "success"}), \
                    mock.patch.object(main, "prepare_job", return_value=(root, root / "control", root / "source")), \
                    mock.patch.object(direct, "run_compiler") as compiler:
                with self.assertRaises(subprocess.CalledProcessError):
                    main.pr_compile_job(object(), {"control_commit": self.HEAD, "head": self.HEAD})
            self.assertEqual("outcome=precondition\nsubstituted=false\n", output.read_text())
            compiler.assert_not_called()
            with mock.patch.dict(direct.os.environ, env), \
                    mock.patch.object(direct, "require_workflow_context"), \
                    mock.patch.object(main, "pr_status") as status:
                main.pr_status_final(object(), {}, "failure", outcome="precondition")
            self.assertEqual("error", status.call_args.args[2])
            self.assertNotIn("PRIVATE_GIT_FAILURE", status.call_args.args[3])

    def test_real_git_exact_head_and_missing_object_retain_the_actual_failure(self):
        # The only remote substitution is an inert local repository; no network or game code runs.
        repository = Path(__file__).resolve().parents[2]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); remote = root / "remote"
            subprocess.run(["git", "clone", "-q", "--no-checkout", "--shared", str(repository), str(remote)], check=True)
            head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=remote, text=True).strip()
            missing = subprocess.check_output(["git", "hash-object", "--stdin"], input=b"checkout missing commit fixture").decode().strip()
            execute = direct.checkout_run

            def local_git(command, **options):
                if "fetch" in command:
                    self.assertIn("protocol.file.allow=never", command)
                    command = ["git", "-c", "protocol.file.allow=always", "fetch", "-q", "--depth=1", str(remote), command[-1]]
                return execute(command, **options)

            with mock.patch.object(direct, "checkout_run", side_effect=local_git):
                direct.checkout(root / "control", head)
                self.assertEqual(head, subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root / "control", text=True).strip())
                with self.assertRaises(subprocess.CalledProcessError) as caught:
                    direct.checkout(root / "source", missing)
            data = json.loads((root / "private-checkout-source.json").read_bytes())
            self.assertEqual("fetch", data["phase"])
            self.assertEqual(missing, data["requested_head"])
            self.assertEqual(caught.exception.returncode, data["returncode"])
            self.assertTrue(data["stderr_tail"])
            self.assertFalse((root / "private-checkout-control.json").exists())

    def test_an_existing_receipt_does_not_replace_the_original_git_exception(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); receipt = root / "private-checkout-source.json"
            receipt.write_bytes(b"existing diagnostic must survive")
            original = subprocess.CalledProcessError(128, ["git", "init"], stderr=b"original error")
            calls = []; patches = self.commands(calls, "init", lambda _: original)
            with patches[0], patches[1], self.assertRaises(subprocess.CalledProcessError) as caught:
                direct.checkout(root / "source", self.HEAD)
            self.assertIs(original, caught.exception)
            self.assertEqual(b"existing diagnostic must survive", receipt.read_bytes())
            self.assertIsInstance(caught.exception.__cause__, FileExistsError)

    def test_a_receipt_write_failure_keeps_the_git_exception_primary(self):
        with tempfile.TemporaryDirectory() as directory:
            original = subprocess.TimeoutExpired(["git", "fetch"], 300, stderr=b"partial error")
            storage = PermissionError("cannot write diagnostic")
            calls = []; patches = self.commands(calls, "fetch", lambda _: original)
            with patches[0], patches[1], mock.patch.object(direct, "retain_checkout_failure", side_effect=storage), \
                    self.assertRaises(subprocess.TimeoutExpired) as caught:
                direct.checkout(Path(directory) / "source", self.HEAD)
            self.assertIs(original, caught.exception)
            self.assertIs(storage, caught.exception.__cause__)

    def test_checkout_git_uses_the_owned_process_tree_facade(self):
        command = ["git", "fetch"]
        with tempfile.TemporaryDirectory() as directory:
            cwd = Path(directory) / "source"; cwd.mkdir()
            with mock.patch.object(direct, "run_compiler", return_value=b"output") as owned, \
                    mock.patch.object(direct.subprocess, "run", side_effect=AssertionError("unowned Git process")):
                result = direct.checkout_run(command, cwd=cwd, check=True,
                                             stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=300)
            self.assertEqual(0, result.returncode)
            self.assertEqual(b"output", result.stdout)
            self.assertEqual(b"", result.stderr)
            owned.assert_called_once()
            self.assertEqual(command, owned.call_args.args[0])
            self.assertEqual(cwd, owned.call_args.args[1])
            self.assertEqual(cwd.parent / "compiler-group.json", owned.call_args.args[3])
            self.assertEqual({"timeout": 300}, owned.call_args.kwargs)

    def test_timeout_stops_a_real_parent_and_term_ignoring_helper_before_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / "source"; source.mkdir()
            pid_file = root / "helper.pid"
            child = ("import os,signal,sys,time; from pathlib import Path; "
                     "signal.signal(signal.SIGTERM,signal.SIG_IGN); "
                     "Path(sys.argv[1]).write_text(str(os.getpid())); time.sleep(60)")
            parent = ("import subprocess,sys,time; subprocess.Popen([sys.executable,'-c',sys.argv[1],sys.argv[2]]); time.sleep(60)")
            command = [sys.executable, "-c", parent, child, str(pid_file)]
            helper_pid = None
            try:
                with self.assertRaises(subprocess.TimeoutExpired):
                    direct.checkout_run(command, cwd=source, check=True, stdout=subprocess.PIPE,
                                        stderr=subprocess.PIPE, timeout=1)
                self.assertTrue(pid_file.is_file(), "helper did not install its handler")
                helper_pid = int(pid_file.read_text())
                marker = json.loads((root / "compiler-group.json").read_bytes())
                self.assertFalse(direct.group_running(marker["group"]))
                state = subprocess.run(["ps", "-o", "stat=", "-p", str(helper_pid)], capture_output=True, text=True).stdout.strip()
                self.assertTrue(not state or state.startswith("Z"), "helper survived the checkout timeout")
                with mock.patch.object(direct, "job_root", return_value=root):
                    direct.cleanup_job()
                self.assertFalse(source.exists())
            finally:
                if pid_file.exists():
                    helper_pid = int(pid_file.read_text())
                    marker = json.loads((root / "compiler-group.json").read_bytes())
                    try:
                        if os.getpgid(helper_pid) == marker["group"]:
                            os.killpg(marker["group"], signal.SIGKILL)
                    except ProcessLookupError:
                        pass


if __name__ == "__main__":
    unittest.main()

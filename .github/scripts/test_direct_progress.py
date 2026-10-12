"""Mock HTTPS and filesystem regressions; fixture hashes are not compiler evidence."""
import base64
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import urllib.parse

sys.path.insert(0, str(Path(__file__).parent))
import direct_progress as direct

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
from lucidlib.verification import artifact_fingerprint


class HTTPSFixture:
    def __init__(self):
        self.head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        self.request = {"pr": 7, "head": self.head, "control_commit": self.head}
        self.pr = {"number": 7, "state": "open", "draft": False, "user": {"login": "dev-camo"},
                   "head": {"sha": self.head, "ref": "owner-progress", "repo": {"full_name": direct.REPOSITORY}},
                   "base": {"repo": {"full_name": direct.REPOSITORY}}}
        self.report = json.loads((ROOT / "docs/code-progress.json").read_bytes())
        # Deliberately mocked new receipt/source identity, never written to production files.
        self.report["compiler_receipt_sha256"] = "e" * 64
        self.report["source_fingerprint"] = artifact_fingerprint(ROOT)
        self.raw = (json.dumps(self.report, indent=2, sort_keys=True) + "\n").encode()
        self.producer = {"head": self.head, "image": direct.EDITOR, "control_commit": self.head,
                         "run_id": "456", "source_fingerprint": self.report["source_fingerprint"],
                         "report_sha256": hashlib.sha256(self.raw).hexdigest(),
                         "compiler_receipt_sha256": self.report["compiler_receipt_sha256"]}
        self.default = self.head
        self.reference = self.head
        self.calls = []
        self.on_call = None
        self.bot = {"login": direct.BOT, "type": "Bot", "id": 41898282}
        self.commit = {"author": {"login": direct.BOT}, "committer": {"login": direct.BOT},
                       "parents": [{"sha": "a" * 40}],
                       "files": [{"filename": "docs/code-progress.json", "status": "modified"}]}
        self.dispatch_response = True

    def urlopen(self, request, timeout):
        path = urllib.parse.urlsplit(request.full_url).path
        method = request.get_method()
        body = json.loads(request.data) if request.data else None
        call = {"path": path, "method": method, "body": body, "headers": dict(request.header_items())}
        self.calls.append(call)
        if self.on_call:
            self.on_call(call)
        repo = "/repos/" + direct.REPOSITORY
        if path == "/users/github-actions%5Bbot%5D":
            value = self.bot
        elif path == repo:
            value = {"full_name": direct.REPOSITORY, "default_branch": "main"}
        elif path == repo + "/commits/main":
            value = {"sha": self.default}
        elif path == repo + "/pulls/7":
            value = self.pr
        elif path == repo + "/commits/" + self.head:
            value = self.commit
        elif path == repo + "/git/commits/" + self.head:
            value = {"tree": {"sha": "f" * 40}}
        elif path == repo + "/git/blobs":
            value = {"sha": hashlib.sha1(base64.b64decode(body["content"])).hexdigest()}
        elif path == repo + "/git/trees":
            value = {"sha": "1" * 40}
        elif path == repo + "/git/commits":
            value = {"sha": "2" * 40}
        elif path == repo + "/git/refs/heads/owner-progress":
            if method == "PATCH":
                self.reference = body["sha"]
                self.pr["head"]["sha"] = body["sha"]
            value = {"object": {"sha": self.reference}}
        elif path.endswith("/dispatches"):
            run_id = 800 + len([c for c in self.calls if c["path"].endswith("/dispatches")])
            value = {"workflow_run_id": run_id, "html_url": f"https://github.com/{direct.REPOSITORY}/actions/runs/{run_id}"} if self.dispatch_response else None
        else:
            raise AssertionError("Unexpected HTTPS call: " + method + " " + path)
        return io.BytesIO(json.dumps(value).encode())


class DirectProgressTests(unittest.TestCase):
    def setUp(self):
        self.http = HTTPSFixture()
        self.network = mock.patch.object(direct.urllib.request, "urlopen", side_effect=self.http.urlopen)
        self.network.start()
        self.addCleanup(self.network.stop)
        self.environment = mock.patch.dict(os.environ, {"GH_TOKEN": "job-token", "GITHUB_REPOSITORY": direct.REPOSITORY,
            "GITHUB_RUN_ID": "456", "GITHUB_RUN_ATTEMPT": "1", "GITHUB_SHA": self.http.head})
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.api = direct.GitHub()

    def publish(self):
        return direct.publish(self.api, self.http.request, self.http.raw, self.http.producer, ROOT, ROOT)

    def test_publish_only_generated_files_with_bot_author_committer_and_explicit_required_checks(self):
        head, runs = self.publish()
        self.assertEqual("2" * 40, head)
        self.assertEqual(2, len(runs))
        created = next(c for c in self.http.calls if c["method"] == "POST" and c["path"].endswith("/git/commits"))
        identity = {"name": direct.BOT, "email": "41898282+github-actions[bot]@users.noreply.github.com"}
        self.assertEqual(identity, created["body"]["author"])
        self.assertEqual(identity, created["body"]["committer"])
        self.assertEqual([self.http.head], created["body"]["parents"])
        tree = next(c for c in self.http.calls if c["path"].endswith("/git/trees"))
        self.assertEqual({"README.md", "docs/code-progress.json"}, {r["path"] for r in tree["body"]["tree"]})
        patch = next(c for c in self.http.calls if c["method"] == "PATCH")
        self.assertEqual({"sha": head, "force": False}, patch["body"])
        dispatches = [c for c in self.http.calls if c["path"].endswith("/dispatches")]
        self.assertEqual(list(direct.CHECK_WORKFLOWS), [c["path"].split("/")[-2] for c in dispatches])
        self.assertTrue(all(c["body"] == {"ref": "owner-progress", "inputs": {"expected_head": head}} for c in dispatches))
        self.assertTrue(all(c["headers"]["Authorization"] == "Bearer job-token" for c in self.http.calls))

    def test_non_pr_check_completions_do_not_loop(self):
        for event in ("push", "workflow_dispatch"):
            with self.subTest(event=event):
                self.assertIsNone(direct.gate(self.api, {"workflow_run": {"event": event}}, self.http.head))
        self.assertFalse(self.http.calls)

    def test_owner_current_head_gate_and_stale_or_contributor_skips(self):
        event = {"workflow_run": {"name": "Code progress", "repository": {"full_name": direct.REPOSITORY},
                                  "event": "pull_request", "head_sha": self.http.head, "pull_requests": [{"number": 7}]}}
        self.assertEqual(self.http.request, direct.gate(self.api, event, self.http.head))
        for mutate in (lambda: self.http.pr["user"].update(login="KyleO6"),
                       lambda: self.http.pr["head"].update(sha="a" * 40),
                       lambda: self.http.pr["head"]["repo"].update(full_name="KyleO6/project-lucid"),
                       lambda: self.http.pr.update(draft=True), lambda: self.http.pr.update(state="closed")):
            saved = copy.deepcopy(self.http.pr)
            mutate()
            self.assertIsNone(direct.gate(self.api, event, self.http.head))
            self.http.pr = saved

    def test_stale_control_and_default_branch_publication_rejected(self):
        self.http.default = "a" * 40
        with self.assertRaisesRegex(ValueError, "Control changed"):
            self.publish()
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))
        self.http.default = self.http.head
        self.http.pr["head"]["ref"] = "main"
        with self.assertRaises(ValueError):
            self.publish()

    def test_forged_or_missing_compilation_outputs_rejected_before_write(self):
        for key in direct.PRODUCER_FIELDS:
            producer = dict(self.http.producer, **{key: "0" * 64})
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "protected real compiler"):
                direct.publish(self.api, self.http.request, self.http.raw, producer, ROOT, ROOT)
        with self.assertRaises(ValueError):
            direct.publish(self.api, self.http.request, self.http.raw, {}, ROOT, ROOT)
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_source_fingerprint_and_private_report_fields_rejected(self):
        for key, value in (("source_fingerprint", "a" * 64), ("private_inventory", ["input.app"])):
            report = dict(self.http.report, **{key: value})
            raw = json.dumps(report).encode()
            with self.subTest(key=key), self.assertRaises(ValueError):
                direct.publish(self.api, self.http.request, raw, self.http.producer, ROOT, ROOT)
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_original_input_and_reporting_tool_integrity_rejected(self):
        report = dict(self.http.report, input_binary_sha256="a" * 64)
        with self.assertRaisesRegex(ValueError, "Original code inputs"):
            direct.verify_report(json.dumps(report).encode(), self.http.producer, self.http.request, "456", ROOT)
        with mock.patch.object(direct.driver, "require_control_reporting_tools", side_effect=ValueError("tools differ")), self.assertRaises(ValueError):
            self.publish()
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_concurrent_head_and_ref_rejected_before_push(self):
        for change in ("head", "ref"):
            self.http.calls.clear()
            self.http.reference = self.http.head
            self.http.pr["head"]["sha"] = self.http.head
            def race(call):
                if call["method"] == "POST" and call["path"].endswith("/git/commits"):
                    if change == "head":
                        self.http.pr["head"]["sha"] = "a" * 40
                    else:
                        self.http.reference = "a" * 40
            self.http.on_call = race
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.publish()
            self.assertFalse(any(c["method"] == "PATCH" for c in self.http.calls))

    def test_uncertain_dispatch_does_not_retry_or_claim_checks_complete(self):
        self.http.dispatch_response = False
        with self.assertRaisesRegex(ValueError, "uncertain"):
            self.publish()
        self.assertEqual(1, len([c for c in self.http.calls if c["path"].endswith("/dispatches")]))
        self.assertEqual(1, len([c for c in self.http.calls if c["method"] == "PATCH"]))

    def test_head_changes_after_push_prevents_dispatch(self):
        def race(call):
            if call["method"] == "GET" and call["path"].endswith("/pulls/7") and self.http.reference == "2" * 40:
                self.http.pr["head"]["sha"] = "3" * 40
        self.http.on_call = race
        with self.assertRaises(ValueError):
            self.publish()
        self.assertFalse(any(c["path"].endswith("/dispatches") for c in self.http.calls))

    def test_bot_commit_requires_exact_identity_allowlist_and_fresh_outputs(self):
        validate, render, _ = direct.reporting_tools(ROOT)
        original_read = Path.read_bytes
        def read_fixture(path):
            return self.http.raw if path == ROOT / "docs/code-progress.json" else original_read(path)
        with mock.patch.object(direct, "reporting_tools", return_value=(validate, render, lambda _: ("", render(self.http.report).rstrip("\n"), ""))), \
             mock.patch.object(Path, "read_bytes", read_fixture):
            self.assertTrue(direct.fresh_bot_output(self.api, ROOT, ROOT, self.http.head))
        for field in ("author", "committer"):
            saved = self.http.commit[field]
            self.http.commit[field] = {"login": "dev-camo"}
            self.assertFalse(direct.fresh_bot_output(self.api, ROOT, ROOT, self.http.head))
            self.http.commit[field] = saved
        self.http.commit["files"].append({"filename": "Assets/Code.cs", "status": "modified"})
        self.assertFalse(direct.fresh_bot_output(self.api, ROOT, ROOT, self.http.head))

    def test_failed_owner_preflight_never_fetches_or_invokes_compiler(self):
        self.http.pr["user"]["login"] = "KyleO6"
        with mock.patch.object(direct, "checkout") as fetch, mock.patch.object(direct.subprocess, "run") as run, self.assertRaises(ValueError):
            direct.compile_job(self.api, self.http.request)
        fetch.assert_not_called()
        run.assert_not_called()

    def test_wrong_repository_and_rerun_context_rejected(self):
        for key, value in (("GITHUB_REPOSITORY", "dev-camo/other"), ("GITHUB_RUN_ATTEMPT", "2"), ("GITHUB_SHA", "a" * 40)):
            with self.subTest(key=key), mock.patch.dict(os.environ, {key: value}), self.assertRaises(ValueError):
                direct.require_workflow_context(self.http.request)

    def test_seal_binds_report_to_actual_compiler_step_outputs_and_rejects_tampering(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            # These are fixture checkouts, not compiler results or production evidence.
            for name in ("source", "control"):
                subprocess.run(["git", "clone", "-q", "--shared", str(ROOT), str(root / name)], check=True)
            (root / "request.json").write_text(json.dumps(self.http.request))
            (root / "code-progress.json").write_bytes(self.http.raw)
            with mock.patch.object(direct, "job_root", return_value=root), \
                 mock.patch.object(direct, "write_outputs") as outputs:
                direct.seal_job(self.api, self.http.request, self.http.producer)
                emitted = outputs.call_args.args[0]
                self.assertEqual(self.http.raw, base64.b64decode(emitted["report_base64"]))
                self.assertEqual(self.http.producer, json.loads(base64.b64decode(emitted["producer_base64"])))
                outputs.reset_mock()
                report = copy.deepcopy(self.http.report)
                report["compiler_receipt_sha256"] = "a" * 64
                (root / "code-progress.json").write_text(json.dumps(report))
                with self.assertRaisesRegex(ValueError, "protected real compiler"):
                    direct.seal_job(self.api, self.http.request, self.http.producer)
                outputs.assert_not_called()
                (root / "code-progress.json").write_bytes(self.http.raw)
                (root / "request.json").write_text(json.dumps(dict(self.http.request, head="a" * 40)))
                with self.assertRaisesRegex(ValueError, "Local source/control/request"):
                    direct.seal_job(self.api, self.http.request, self.http.producer)

    def test_check_dispatch_ref_race_and_foreign_api_paths_fail_closed(self):
        self.http.reference = "a" * 40
        with self.assertRaisesRegex(ValueError, "Head changed"):
            direct.dispatch_checks(self.api, self.http.request, self.http.head)
        self.assertFalse(any(c["method"] == "POST" for c in self.http.calls))
        with self.assertRaisesRegex(ValueError, "Foreign API"):
            self.api.request("/repos/dev-camo/other/git/refs/heads/main", "PATCH", {})

    def test_timeout_stops_wrapper_and_ignored_term_child_before_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            child_pid = root / "child.pid"
            child = "import os,signal,time; from pathlib import Path; signal.signal(signal.SIGTERM,signal.SIG_IGN); Path(" + repr(str(child_pid)) + ").write_text(str(os.getpid())); time.sleep(60)"
            wrapper = "import subprocess,time; subprocess.Popen([" + repr(sys.executable) + ",'-c'," + repr(child) + "],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); time.sleep(60)"
            marker = root / "compiler-group.json"
            with (root / "errors").open("wb") as errors, self.assertRaises(subprocess.TimeoutExpired):
                direct.run_compiler([sys.executable, "-c", wrapper], root, errors, marker, timeout=0.5)
            group = json.loads(marker.read_bytes())["group"]
            self.assertTrue(child_pid.exists(), "The actual child must have started before timeout")
            self.assertFalse(direct.group_running(group))
            direct.require_stopped_marker(marker)

    def test_active_compiler_marker_blocks_checkout_cleanup_and_next_compiler(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            (root / "source").mkdir()
            (root / "control").mkdir()
            marker = root / "compiler-group.json"
            marker.write_text(json.dumps({"group": os.getpgrp()}))
            with mock.patch.object(direct, "job_root", return_value=root), self.assertRaisesRegex(ValueError, "still active"):
                direct.cleanup_job()
            self.assertTrue((root / "source").exists())
            with self.assertRaisesRegex(ValueError, "still active"):
                direct.require_stopped_marker(marker)

    def test_bot_stale_report_is_rejected_instead_of_skipping_compilation(self):
        stale = json.dumps(dict(self.http.report, source_fingerprint="a" * 64)).encode()
        original_read = Path.read_bytes
        def read_fixture(path):
            return stale if path == ROOT / "docs/code-progress.json" else original_read(path)
        with mock.patch.object(Path, "read_bytes", read_fixture), self.assertRaisesRegex(ValueError, "stale"):
            direct.fresh_bot_output(self.api, ROOT, ROOT, self.http.head)

    def test_bounded_report_and_bot_identity_do_not_allow_publication(self):
        with self.assertRaisesRegex(ValueError, "exceeds bound"):
            direct.verify_report(b" " * (direct.MAX_REPORT_BYTES + 1), self.http.producer, self.http.request, "456", ROOT)
        for field, value in (("login", "dev-camo"), ("id", True), ("type", "User")):
            original = self.http.bot[field]
            self.http.bot[field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "bot identity"):
                self.publish()
            self.http.bot[field] = original
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_cleanup_removes_only_run_checkouts_without_following_symlinks(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            root = base / "jobs" / "direct-456-1"
            root.mkdir(parents=True)
            outside = base / "input-and-license"
            outside.mkdir()
            (outside / "keep").write_text("private fixture")
            (root / "source").symlink_to(outside, target_is_directory=True)
            (root / "control").mkdir()
            (root / "proof.json").write_text("small proof")
            with mock.patch.object(direct, "job_root", return_value=root):
                direct.cleanup_job()
            self.assertTrue((outside / "keep").exists())
            self.assertTrue((root / "proof.json").exists())
            self.assertFalse((root / "source").exists())
            self.assertFalse((root / "control").exists())
            linked = base / "linked"
            linked.symlink_to(root, target_is_directory=True)
            with mock.patch.object(direct, "job_root", return_value=linked), self.assertRaises(ValueError):
                direct.cleanup_job()

    def test_no_deleted_repository_or_app_reference_in_active_path(self):
        for name in ("scripts/direct_progress.py", "workflows/native-progress.yml"):
            text = (ROOT / ".github" / name).read_text()
            self.assertNotIn("project-lucid-build", text)
            self.assertNotIn("create-github-app-token", text)
            self.assertNotIn("LUCID_PROGRESS_APP", text)


if __name__ == "__main__":
    unittest.main()

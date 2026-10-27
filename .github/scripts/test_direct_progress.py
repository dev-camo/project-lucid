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
from lucidlib import codeprogress
from lucidlib.verification import artifact_fingerprint


class HTTPSFixture:
    def __init__(self):
        self.head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        self.request = {"pr": 7, "head": self.head, "control_commit": self.head,
                        "head_repository": direct.REPOSITORY, "head_ref": "owner-progress", "upstream_run": 123}
        self.pr = {"number": 7, "state": "open", "draft": False, "user": {"login": "dev-camo"},
                   "head": {"sha": self.head, "ref": "owner-progress", "repo": {"full_name": direct.REPOSITORY}},
                   "base": {"repo": {"full_name": direct.REPOSITORY}}}
        self.upstream = {"id": 123, "workflow_id": 9, "name": "Code progress", "event": "pull_request",
                         "repository": {"full_name": direct.REPOSITORY}, "head_sha": self.head,
                         "head_repository": {"full_name": direct.REPOSITORY}, "head_branch": "owner-progress",
                         "status": "completed", "conclusion": "success", "pull_requests": []}
        self.statuses = []
        self.fork_checks = []
        self.native_run = {"path": ".github/workflows/native-progress.yml", "head_sha": self.head,
                           "event": "workflow_run", "conclusion": "success"}
        self.native_jobs = [{"name": "Native compilation and evidence", "status": "completed",
                             "conclusion": "success"}]
        self.candidates = None
        self.bind_report_to_source(ROOT)
        self.default = self.head
        self.reference = self.head
        self.calls = []
        self.on_call = None
        self.bot = {"login": direct.BOT, "type": "Bot", "id": 41898282}
        self.commit = {"author": {"login": direct.BOT}, "committer": {"login": direct.BOT},
                       "parents": [{"sha": "a" * 40}],
                       "files": [{"filename": "docs/code-progress.json", "status": "modified"}]}
        self.dispatch_response = True

    def bind_report_to_source(self, source):
        self.report = json.loads((source / "docs/code-progress.json").read_bytes())
        # Deliberately mocked new receipt/source identity, never written to production files.
        self.report["compiler_receipt_sha256"] = "e" * 64
        self.report["source_fingerprint"] = artifact_fingerprint(source)
        # The published snapshot is main-owned and may predate this head's scope; model the next publication's scope identity.
        names, _, scope_sha256 = codeprogress._scope(source)
        self.report["scope_sha256"] = scope_sha256
        rows = {row["assembly"]: row for row in self.report["assemblies"]}
        for name in names:
            rows.setdefault(name, {"assembly": name, "measurement_status": "no_recovered_source", "unavailable_reason": None,
                                   **{key: 0 for key in self.report["totals"]}})
        self.report["assemblies"] = [rows[name] for name in names]
        self.raw = (json.dumps(self.report, indent=2, sort_keys=True) + "\n").encode()
        self.producer = {"head": self.head, "image": direct.EDITOR, "control_commit": self.head,
                         "run_id": "456", "source_fingerprint": self.report["source_fingerprint"],
                         "report_sha256": hashlib.sha256(self.raw).hexdigest(),
                         "compiler_receipt_sha256": self.report["compiler_receipt_sha256"]}

    def urlopen(self, request, timeout):
        url = urllib.parse.urlsplit(request.full_url)
        path = url.path
        method = request.get_method()
        body = json.loads(request.data) if request.data else None
        call = {"path": path, "query": urllib.parse.parse_qs(url.query), "method": method,
                "body": body, "headers": dict(request.header_items())}
        self.calls.append(call)
        if self.on_call:
            self.on_call(call)
        repo = "/repos/" + direct.REPOSITORY
        if path == "/users/github-actions%5Bbot%5D":
            value = self.bot
        elif path == repo:
            value = {"full_name": direct.REPOSITORY, "default_branch": "main"}
        elif path == repo + "/contents/docs/code-progress.json":
            value = {"encoding": "base64", "content": base64.b64encode(self.raw).decode()}
        elif path == repo + "/commits/main":
            value = {"sha": self.default}
        elif path == repo + "/actions/runs/123":
            value = self.upstream
        elif path == repo + "/actions/runs/456":
            value = self.native_run
        elif path == repo + "/actions/runs/456/jobs":
            value = {"jobs": self.native_jobs}
        elif path == repo + "/actions/workflows/code-progress.yml":
            value = {"id": 9}
        elif path.endswith("/runs") and "/actions/workflows/" in path:
            value = {"workflow_runs": self.fork_checks or [self.upstream]}
        elif path == repo + "/pulls":
            value = [self.pr] if self.candidates is None else self.candidates
        elif path == repo + "/commits/" + self.head + "/statuses":
            value = self.statuses
        elif path.startswith(repo + "/statuses/"):
            value = {}
        elif path == "/repos/KyleO6/project-lucid/git/refs/heads/owner-progress":
            value = {"object": {"sha": self.reference}}
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


    def test_non_pr_check_completions_do_not_loop(self):
        for event in ("push", "workflow_dispatch"):
            with self.subTest(event=event):
                self.assertIsNone(direct.gate(self.api, {"workflow_run": {"event": event}}, self.http.head))
        self.assertFalse(self.http.calls)

    def event(self):
        return {"workflow_run": copy.deepcopy(self.http.upstream)}

    def fork(self):
        self.http.pr["user"]["login"] = "KyleO6"
        self.http.pr["head"]["repo"]["full_name"] = "KyleO6/project-lucid"
        self.http.pr["maintainer_can_modify"] = True
        self.http.upstream["head_repository"]["full_name"] = "KyleO6/project-lucid"
        self.http.request["head_repository"] = "KyleO6/project-lucid"

    def test_bot_actor_without_protected_publication_proof_still_compiles(self):
        event = self.event()
        event["workflow_run"]["actor"] = {"login": direct.BOT}
        self.assertEqual(self.http.request, direct.gate(self.api, event, self.http.head))

    def test_fork_with_empty_pr_payload_runs_without_extra_contributor_policy(self):
        self.fork()
        event = self.event()
        self.assertEqual([], event["workflow_run"]["pull_requests"])
        self.assertEqual(self.http.request, direct.gate(self.api, event, self.http.head))
        discovery = next(call for call in self.http.calls if call["path"].endswith("/pulls"))
        self.assertEqual({"state": ["open"], "head": ["KyleO6:owner-progress"], "per_page": ["100"]},
                         discovery["query"])
        # The PR author's name/association is deliberately not an admission gate.
        self.http.pr["user"]["login"] = "first-time-author"
        self.http.pr["author_association"] = "NONE"
        self.assertEqual(self.http.request, direct.gate(self.api, event, self.http.head))

    def test_branch_discovery_rejects_ambiguous_stale_or_foreign_matches(self):
        self.fork()
        original = copy.deepcopy(self.http.pr)
        for key, value in (("sha", "a" * 40), ("ref", "other"),
                           ("repo", {"full_name": "KyleO6/other"})):
            candidate = copy.deepcopy(original)
            candidate["head"][key] = value
            self.http.candidates = [candidate]
            with self.subTest(key=key):
                self.assertIsNone(direct.gate(self.api, self.event(), self.http.head))
        self.http.candidates = [original, copy.deepcopy(original)]
        self.assertIsNone(direct.gate(self.api, self.event(), self.http.head))

    def test_branch_discovery_rejects_invalid_advertised_repository_or_ref(self):
        for key, value in (("head_repository", {"full_name": "invalid"}), ("head_branch", None)):
            event = self.event()
            event["workflow_run"][key] = value
            with self.subTest(key=key):
                self.assertIsNone(direct.gate(self.api, event, self.http.head))
        self.assertFalse(self.http.calls)

    def test_platform_run_must_be_completed_for_exact_head_(self):
        self.fork()
        for key, value in (("status", "waiting"), ("conclusion", "action_required"),
                           ("event", "push"), ("workflow_id", 10), ("head_sha", "a" * 40),
                           ("head_branch", "other"), ("head_repository", {"full_name": "other/repo"})):
            event = self.event()
            saved = copy.deepcopy(self.http.upstream)
            self.http.upstream[key] = value
            with self.subTest(key=key):
                self.assertIsNone(direct.gate(self.api, event, self.http.head))
            self.http.upstream = saved
        self.http.upstream["conclusion"] = "failure"
        self.assertEqual(self.http.request, direct.gate(self.api, self.event(), self.http.head))
        # Without a workflow_run event there is no admitted upstream run.
        self.assertIsNone(direct.gate(self.api, {}, self.http.head))


    def test_compiler_environment_excludes_controller_tokens_and_output_channels(self):
        with tempfile.TemporaryDirectory() as directory:
            # The custody record uses the owned canonical path, including on macOS.
            root = Path(directory).resolve()
            process = mock.Mock(pid=987, returncode=0)
            process.communicate.return_value = (b"fixture", None)
            with mock.patch.dict(os.environ, {"GITHUB_OUTPUT": "never-child", "LUCID_FORK_TOKEN": "never-child"}), \
                 mock.patch.object(direct.subprocess, "Popen", return_value=process) as spawn, \
                 mock.patch.object(direct, "group_running", return_value=False):
                self.assertEqual(b"fixture", direct.run_compiler(["fixture"], root, None, root / "marker"))
            environment = spawn.call_args.kwargs["env"]
            for key in ("GH_TOKEN", "GITHUB_OUTPUT", "REQUEST_BASE64", "LUCID_FORK_TOKEN"):
                self.assertNotIn(key, environment)


    def test_current_head_gate_and_stale_or_changed_identity_skips(self):
        event = self.event()
        self.assertEqual(self.http.request, direct.gate(self.api, event, self.http.head))
        for mutate in (lambda: self.http.pr["head"].update(sha="a" * 40),
                       lambda: self.http.pr["head"]["repo"].update(full_name="KyleO6/project-lucid"),
                       lambda: self.http.pr.update(draft=True), lambda: self.http.pr.update(state="closed")):
            saved = copy.deepcopy(self.http.pr)
            mutate()
            self.assertIsNone(direct.gate(self.api, event, self.http.head))
            self.http.pr = saved


    def test_wrong_repository_and_rerun_context_rejected(self):
        for key, value in (("GITHUB_REPOSITORY", "dev-camo/other"), ("GITHUB_RUN_ATTEMPT", "2"), ("GITHUB_SHA", "a" * 40)):
            with self.subTest(key=key), mock.patch.dict(os.environ, {key: value}), self.assertRaises(ValueError):
                direct.require_workflow_context(self.http.request)


    def test_foreign_api_paths_fail_closed(self):
        with self.assertRaisesRegex(ValueError, "Foreign API"):
            self.api.request("/repos/dev-camo/other/git/refs/heads/main", "PATCH", {})

    def test_timeout_stops_wrapper_and_ignored_term_child_before_cleanup(self):
        for new_session in (False, True):
            with self.subTest(new_session=new_session), tempfile.TemporaryDirectory() as directory:
                self.assert_timeout_cleans_tree(Path(directory).resolve(), new_session)

    def assert_timeout_cleans_tree(self, root, new_session):
            child_pid = root / "child.pid"
            child = "import os,signal,time; from pathlib import Path; signal.signal(signal.SIGTERM,signal.SIG_IGN); Path(" + repr(str(child_pid)) + ").write_text(str(os.getpid())); time.sleep(60)"
            wrapper = "import subprocess,time; subprocess.Popen([" + repr(sys.executable) + ",'-c'," + repr(child) + "],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=" + repr(new_session) + "); time.sleep(60)"
            marker = root / "compiler-group.json"
            with (root / "errors").open("wb") as errors, self.assertRaises(subprocess.TimeoutExpired):
                direct.run_compiler([sys.executable, "-c", wrapper], root, errors, marker, timeout=0.5)
            group = json.loads(marker.read_bytes())["group"]
            self.assertTrue(child_pid.exists(), "The actual child must have started before timeout")
            self.assertFalse(direct.group_running(group))
            self.assertFalse(direct.group_running(int(child_pid.read_text())))
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

    def test_no_deleted_repository_or_app_credential_in_contributor_controller(self):
        for name in ("scripts/direct_progress.py", "workflows/native-progress.yml"):
            text = (ROOT / ".github" / name).read_text()
            self.assertNotIn("project-lucid-build", text)
            if name == "workflows/native-progress.yml":
                text = text.split("  publish:", 1)[0]
            self.assertNotIn("create-github-app-token", text)
            self.assertNotIn("LUCID_PROGRESS_APP", text)


if __name__ == "__main__":
    unittest.main()

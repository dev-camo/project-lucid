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

    def test_platform_run_must_be_completed_for_exact_head_including_manual_dispatch(self):
        self.fork()
        for key, value in (("status", "waiting"), ("conclusion", "action_required"),
                           ("event", "push"), ("workflow_id", 10), ("head_sha", "a" * 40),
                           ("head_branch", "other"), ("head_repository", {"full_name": "other/repo"})):
            event = self.event()
            saved = copy.deepcopy(self.http.upstream)
            self.http.upstream[key] = value
            with self.subTest(key=key):
                self.assertIsNone(direct.gate(self.api, event, self.http.head))
                self.assertIsNone(direct.gate(self.api, {}, self.http.head, 7, self.http.head))
            self.http.upstream = saved
        self.http.upstream["conclusion"] = "failure"
        self.assertEqual(self.http.request, direct.gate(self.api, self.event(), self.http.head))
        self.assertEqual(self.http.request, direct.gate(self.api, {}, self.http.head, 7, self.http.head))

    def test_unapproved_platform_run_never_fetches_or_compiles(self):
        self.fork()
        self.http.upstream["status"] = "waiting"
        with mock.patch.object(direct, "checkout") as fetch, mock.patch.object(direct, "run_compiler") as run:
            with self.assertRaisesRegex(ValueError, "Completed exact-head"):
                direct.compile_job(self.api, self.http.request)
        fetch.assert_not_called()
        run.assert_not_called()

    def test_generated_loop_requires_base_bot_provenance_and_successful_trusted_compilation(self):
        status = {"context": direct.PUBLICATION_CONTEXT, "state": "success", "creator": {"login": direct.BOT},
                  "target_url": f"https://github.com/{direct.REPOSITORY}/actions/runs/456"}
        self.http.statuses = [status]
        self.assertIsNone(direct.gate(self.api, self.event(), self.http.head))
        for key, value in (("creator", {"login": "KyleO6"}), ("state", "failure"),
                           ("target_url", "https://github.com/KyleO6/project-lucid/actions/runs/456")):
            self.http.statuses = [dict(status, **{key: value})]
            with self.subTest(key=key):
                self.assertEqual(self.http.request, direct.gate(self.api, self.event(), self.http.head))
        self.http.statuses = [status]
        for key, value in (("path", ".github/workflows/other.yml"), ("head_sha", "a" * 40),
                           ("event", "pull_request")):
            saved = copy.deepcopy(self.http.native_run)
            self.http.native_run[key] = value
            with self.subTest(key=key):
                self.assertEqual(self.http.request, direct.gate(self.api, self.event(), self.http.head))
            self.http.native_run = saved
        for conclusion in (None, "failure", "cancelled"):
            self.http.native_run["conclusion"] = conclusion
            with self.subTest(publisher_conclusion=conclusion):
                self.assertIsNone(direct.gate(self.api, self.event(), self.http.head))
        for jobs in ([], [{"name": "Native compilation and evidence", "status": "completed",
                          "conclusion": "failure"}],
                     [{"name": "Native compilation and evidence", "status": "in_progress",
                       "conclusion": None}],
                     [{"name": "other", "status": "completed", "conclusion": "success"}]):
            self.http.native_jobs = jobs
            with self.subTest(jobs=jobs):
                self.assertEqual(self.http.request, direct.gate(self.api, self.event(), self.http.head))

    def test_fork_missing_publisher_access_fails_before_any_write(self):
        self.fork()
        for token, allow in (("", True), ("publisher-only-fixture", False)):
            self.http.pr["maintainer_can_modify"] = allow
            with self.subTest(token=bool(token)), mock.patch.dict(os.environ, {"LUCID_FORK_TOKEN": token}), \
                 mock.patch("sys.stdout", new_callable=io.StringIO):
                with self.assertRaisesRegex(PermissionError, "original contributor head retained"):
                    self.publish()
            self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def checks(self, head):
        return [dict(self.http.upstream, id=800, head_sha=head,
                     html_url=f"https://github.com/{direct.REPOSITORY}/actions/runs/800")]

    def test_fork_publish_preserves_original_parent_and_observes_actual_pr_checks(self):
        self.fork()
        head = "2" * 40
        self.http.fork_checks = self.checks(head)
        def push(api, request, source, created):
            self.assertEqual(head, created)
            self.assertEqual(self.http.head, request["head"])
            self.http.reference = created
            self.http.pr["head"]["sha"] = created
        with mock.patch.dict(os.environ, {"LUCID_FORK_TOKEN": "publisher-only-fixture"}), \
             mock.patch.object(direct, "push_fork", side_effect=push) as pushed:
            published, runs = self.publish()
        self.assertEqual(head, published)
        self.assertEqual(2, len(runs))
        pushed.assert_called_once()
        created = next(c for c in self.http.calls if c["path"].endswith("/git/commits") and c["method"] == "POST")
        self.assertEqual([self.http.head], created["body"]["parents"])
        self.assertEqual(direct.BOT, created["body"]["author"]["name"])
        self.assertFalse(any(c["path"].endswith("/dispatches") for c in self.http.calls))
        self.assertFalse(any(c["method"] == "PATCH" for c in self.http.calls))
        fork_reads = [c for c in self.http.calls if c["path"].startswith("/repos/KyleO6/")]
        self.assertTrue(fork_reads)
        self.assertTrue(all("Authorization" not in c["headers"] for c in fork_reads))

    def test_fork_checks_cannot_claim_default_branch_or_stale_head_runs(self):
        self.fork()
        self.http.fork_checks = self.checks("a" * 40)
        with self.assertRaisesRegex(ValueError, "checks not observed"):
            direct.wait_for_fork_checks(self.api, self.http.request, self.http.head, seconds=0)
        self.http.fork_checks = self.checks(self.http.head)
        self.http.fork_checks[0]["head_repository"]["full_name"] = direct.REPOSITORY
        with self.assertRaisesRegex(ValueError, "checks not observed"):
            direct.wait_for_fork_checks(self.api, self.http.request, self.http.head, seconds=0)
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_failed_fork_checks_are_observed_without_rewriting_their_results(self):
        self.fork()
        self.http.fork_checks = self.checks(self.http.head)
        self.http.fork_checks[0]["conclusion"] = "failure"
        before = copy.deepcopy(self.http.fork_checks)
        runs = direct.wait_for_fork_checks(self.api, self.http.request, self.http.head)
        self.assertEqual(2, len(runs))
        self.assertEqual(before, self.http.fork_checks)
        self.assertFalse(any(call["method"] != "GET" for call in self.http.calls))

    def test_fork_push_uses_only_hosted_credential_and_atomic_exact_parent_lease(self):
        self.fork()
        with mock.patch.dict(os.environ, {"LUCID_FORK_TOKEN": "publisher-only-fixture", "GITHUB_TOKEN": "never-child"}), \
             mock.patch.object(direct.subprocess, "run") as git:
            direct.push_fork(self.api, self.http.request, ROOT, "2" * 40)
        push = git.call_args_list[-1]
        command = push.args[0]
        self.assertIn("--force-with-lease=refs/heads/owner-progress:" + self.http.head, command)
        self.assertIn("https://github.com/KyleO6/project-lucid.git", command)
        self.assertIn("2" * 40 + ":refs/heads/owner-progress", command)
        self.assertNotIn("publisher-only-fixture", " ".join(command))
        self.assertEqual("publisher-only-fixture", push.kwargs["env"]["LUCID_FORK_TOKEN"])
        self.assertNotIn("GH_TOKEN", push.kwargs["env"])
        self.assertNotIn("GITHUB_TOKEN", push.kwargs["env"])

    def test_real_git_fork_push_preserves_concurrent_contributor_commit(self):
        self.fork()
        execute = subprocess.run
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            control, source, remote = root / "control", root / "source", root / "fork.git"
            execute(["git", "clone", "-q", "--shared", str(ROOT), str(control)], check=True)
            execute(["git", "clone", "-q", "--shared", str(control), str(source)], check=True)
            execute(["git", "clone", "-q", "--bare", str(control), str(remote)], check=True)
            ref = "refs/heads/owner-progress"
            execute(["git", "update-ref", ref, self.http.head], cwd=remote, check=True)
            (control / "README.md").write_text("Generated-only fixture\n")
            execute(["git", "add", "README.md"], cwd=control, check=True)
            execute(["git", "-c", "user.name=" + direct.BOT, "-c", "user.email=fixture@example.invalid",
                     "commit", "-qm", "Generated fixture"], cwd=control, check=True)
            created = direct.git_head(control)
            def local_git(command, **kwargs):
                command = [str(remote) if item == "https://github.com/KyleO6/project-lucid.git" else item
                           for item in command]
                return execute(command, **kwargs)
            with mock.patch.dict(os.environ, {"LUCID_FORK_TOKEN": "publisher-only-fixture"}), \
                 mock.patch.object(direct.subprocess, "run", side_effect=local_git):
                direct.push_fork(self.api, self.http.request, source, created)
                self.assertEqual(created, subprocess.check_output(["git", "rev-parse", ref], cwd=remote, text=True).strip())
                # A contributor advances the actual remote after our API read.
                (control / "README.md").write_text("Concurrent contributor fixture\n")
                execute(["git", "add", "README.md"], cwd=control, check=True)
                execute(["git", "-c", "user.name=Contributor", "-c", "user.email=fixture@example.invalid",
                         "commit", "-qm", "Concurrent fixture"], cwd=control, check=True)
                concurrent = direct.git_head(control)
                execute(["git", "push", "-q", str(remote), "HEAD:" + ref], cwd=control, check=True)
                with self.assertRaises(subprocess.CalledProcessError):
                    direct.push_fork(self.api, self.http.request, source, created)
                self.assertEqual(concurrent, subprocess.check_output(["git", "rev-parse", ref], cwd=remote, text=True).strip())

    def test_compiler_environment_excludes_controller_tokens_and_output_channels(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            process = mock.Mock(pid=987, returncode=0)
            process.communicate.return_value = (b"fixture", None)
            with mock.patch.dict(os.environ, {"GITHUB_OUTPUT": "never-child", "LUCID_FORK_TOKEN": "never-child"}), \
                 mock.patch.object(direct.subprocess, "Popen", return_value=process) as spawn, \
                 mock.patch.object(direct, "group_running", return_value=False):
                self.assertEqual(b"fixture", direct.run_compiler(["fixture"], root, None, root / "marker"))
            environment = spawn.call_args.kwargs["env"]
            for key in ("GH_TOKEN", "GITHUB_OUTPUT", "REQUEST_BASE64", "LUCID_FORK_TOKEN"):
                self.assertNotIn(key, environment)

    def test_forged_bot_report_cannot_skip_real_compilation(self):
        original = copy.deepcopy(self.http.report)
        for schema in (1, 2, 3):
            with self.subTest(schema=schema):
                forged = copy.deepcopy(original)
                forged["schema_version"] = schema
                row = next(row for row in forged["assemblies"]
                           if row.get("measurement_status", "measured") == "measured"
                           and row["not_implemented"] > 0)
                if schema == 1:
                    for assembly in forged["assemblies"]:
                        assembly.pop("measurement_status", None)
                        assembly.pop("unavailable_reason", None)
                for counts in (row, forged["totals"]):
                    counts["implemented_source_bodies"] += 1
                    counts["not_implemented"] -= 1
                forged["compiler_receipt_sha256"] = "0" * 64
                # Self-consistent counts still cannot prove compiler provenance.
                direct.reporting_tools(ROOT)[0](forged, ROOT)
                self.http.report = forged
                self.http.raw = (json.dumps(forged, indent=2, sort_keys=True) + "\n").encode()
                with self.assertRaisesRegex(ValueError, "Report differs from protected real compiler outputs"):
                    direct.verify_report(self.http.raw, self.http.producer, self.http.request, "456", ROOT, ROOT)
                self.http.calls.clear()
                event = self.event()
                event["workflow_run"]["actor"] = {"login": direct.BOT}
                with tempfile.TemporaryDirectory() as directory:
                    path = Path(directory) / "event.json"
                    output = Path(directory) / "output"
                    path.write_text(json.dumps(event))
                    with mock.patch.dict(os.environ, {"GITHUB_EVENT_PATH": str(path), "GITHUB_OUTPUT": str(output)}), \
                         mock.patch.object(sys, "argv", ["direct_progress", "gate"]), \
                         mock.patch.object(direct, "reporting_tools") as report_tools:
                        direct.main()
                        report_tools.assert_not_called()
                    self.assertIn("run=true\n", output.read_text())
                self.assertFalse(any(c["path"].endswith("/commits/" + self.http.head)
                                     or "/contents/" in c["path"] for c in self.http.calls))
        workflow=(ROOT/".github/workflows/native-progress.yml").read_text()
        self.assertIn("compile: ${{ steps.gate.outputs.run }}",workflow)
        self.assertNotIn("recheck",workflow)

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

    def test_pr_metadata_can_lag_only_while_published_ref_stays_exact(self):
        head="2"*40
        self.http.reference=head
        clock=[0]
        def catch_up(seconds):
            clock[0]+=seconds
            self.http.pr["head"]["sha"]=head
        direct.wait_for_published_head(self.api,self.http.request,"owner-progress",head,
                                      sleep=catch_up,monotonic=lambda:clock[0],seconds=10)
        self.assertEqual(2,clock[0])
        self.http.pr["head"]["sha"]=self.http.head
        self.http.reference="3"*40
        with self.assertRaisesRegex(ValueError,"published ref changed"):
            direct.wait_for_published_head(self.api,self.http.request,"owner-progress",head,
                                          sleep=catch_up,monotonic=lambda:clock[0])

    def test_pr_metadata_timeout_never_dispatches_or_repushes(self):
        self.http.reference="2"*40
        clock=[0]
        with self.assertRaisesRegex(ValueError,"did not catch up"):
            direct.wait_for_published_head(self.api,self.http.request,"owner-progress","2"*40,
                sleep=lambda seconds:clock.__setitem__(0,clock[0]+seconds),monotonic=lambda:clock[0],seconds=3)
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_fork_ref_and_pr_api_can_each_lag_after_atomic_push(self):
        self.fork()
        head = "2" * 40
        for first in ("ref", "pr"):
            self.http.reference = self.http.head
            self.http.pr["head"]["sha"] = self.http.head
            clock = [0]
            def catch_up(seconds):
                clock[0] += seconds
                if first == "ref" or clock[0] >= 4:
                    self.http.reference = head
                if first == "pr" or clock[0] >= 4:
                    self.http.pr["head"]["sha"] = head
            with self.subTest(first=first):
                direct.wait_for_published_head(self.api, self.http.request, "owner-progress", head,
                    sleep=catch_up, monotonic=lambda: clock[0], seconds=10)
                self.assertEqual(4, clock[0])
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))

    def test_fork_ref_api_staying_at_parent_times_out_without_writes(self):
        self.fork()
        self.http.pr["head"]["sha"] = "2" * 40
        clock = [0]
        with self.assertRaisesRegex(ValueError, "did not catch up"):
            direct.wait_for_published_head(self.api, self.http.request, "owner-progress", "2" * 40,
                sleep=lambda seconds: clock.__setitem__(0, clock[0] + seconds),
                monotonic=lambda: clock[0], seconds=3)
        self.assertFalse(any(c["method"] != "GET" for c in self.http.calls))


    def test_failed_pr_preflight_never_fetches_or_invokes_compiler(self):
        self.http.pr["draft"] = True
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

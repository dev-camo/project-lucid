"""Default-branch control for GitHub-approved PR runs on the Ubuntu runner.

The controller has read-only API access. A separate hosted job publishes only
validated generated outputs, dispatches owner checks and observes fork PR checks.
No PR Python is imported by the gate, seal or publisher.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import urllib.error

import native_compilation as driver
import process_registry

REPOSITORY = "dev-camo/project-lucid"
EDITOR = "native@sha256:88c95da13fdd8f9b17229b860f5ac72c7706586eb18b7fee581d206b45148dcc"
BOT = "github-actions[bot]"
SHA = re.compile(r"[0-9a-f]{40}\Z")
PRODUCER_FIELDS = {"head", "image", "control_commit", "run_id", "source_fingerprint",
                   "report_sha256", "compiler_receipt_sha256"}
MAX_REPORT_BYTES = 40 * 1024
CHECK_WORKFLOWS = ("code-progress.yml", "tool-tests.yml")
PUBLICATION_CONTEXT = "lucid/generated-progress"
REPO_NAME = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\Z")


class GitHub:
    def __init__(self, repository=REPOSITORY, token=None):
        if not REPO_NAME.fullmatch(repository):
            raise ValueError("Invalid API repository")
        self.repository = repository
        self.token = os.environ.get("GH_TOKEN", "") if token is None else token

    def request(self, path, method="GET", body=None):
        if not path.startswith("/repos/" + self.repository + "/") and path not in (
                "/repos/" + self.repository, "/users/github-actions%5Bbot%5D"):
            raise ValueError("Foreign API path rejected")
        headers = {"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2026-03-10"}
        if self.token:
            headers["Authorization"] = "Bearer " + self.token
        request = urllib.request.Request("https://api.github.com" + path,
            data=None if body is None else json.dumps(body).encode(), headers=headers, method=method)
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read(2 * 1024 * 1024 + 1)
        if len(raw) > 2 * 1024 * 1024:
            raise ValueError("API response exceeds bound")
        return json.loads(raw) if raw else None


def git_head(path):
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=path, text=True).strip()


def default_head(api):
    info = api.request("/repos/" + REPOSITORY)
    if info.get("full_name") != REPOSITORY or not isinstance(info.get("default_branch"), str):
        raise ValueError("Repository identity differs")
    branch = info["default_branch"]
    head = api.request(f"/repos/{REPOSITORY}/commits/" + urllib.parse.quote(branch, safe=""))["sha"]
    if not isinstance(head, str) or not SHA.fullmatch(head):
        raise ValueError("Invalid default-branch head")
    return branch, head


def validate_request(request):
    if (set(request) != {"pr", "head", "control_commit", "head_repository", "head_ref", "upstream_run"}
            or not isinstance(request["head_repository"], str) or not REPO_NAME.fullmatch(request["head_repository"])
            or not isinstance(request["head_ref"], str) or not request["head_ref"]
            or type(request["upstream_run"]) is not int or request["upstream_run"] <= 0
            or type(request["pr"]) is not int
            or request["pr"] <= 0 or not all(isinstance(request[key], str) and SHA.fullmatch(request[key])
                                            for key in ("head", "control_commit"))):
        raise ValueError("Exact PR, head and control required")


def require_current_pr(api, request):
    validate_request(request)
    pr = api.request(f"/repos/{REPOSITORY}/pulls/{request['pr']}")
    require_pr_metadata(pr, request)
    branch, head = default_head(api)
    if head != request["control_commit"] or (request["head_repository"] == REPOSITORY and pr["head"]["ref"] == branch):
        raise ValueError("Control changed or PR targets the default-branch ref")
    return pr


def require_pr_metadata(pr, request):
    if (pr.get("state") != "open" or pr.get("draft")
            or pr.get("head", {}).get("sha") != request["head"]
            or pr.get("head", {}).get("ref") != request["head_ref"]
            or (pr.get("head", {}).get("repo") or {}).get("full_name") != request["head_repository"]
            or (pr.get("base", {}).get("repo") or {}).get("full_name") != REPOSITORY):
        raise ValueError("Stale, draft, closed or foreign PR rejected")


def require_upstream_run(api, request):
    """A completed PR run has passed GitHub's configured contributor gate.

    A failing progress check is eligible: its stale generated report is precisely
    what native compilation repairs. Pending approval and cancelled runs are not.
    Manual dispatch cannot bypass that same platform gate.
    """
    run = api.request(f"/repos/{REPOSITORY}/actions/runs/{request['upstream_run']}")
    workflow = api.request(f"/repos/{REPOSITORY}/actions/workflows/code-progress.yml")
    if (run.get("event") != "pull_request" or run.get("status") != "completed"
            or run.get("conclusion") not in {"success", "failure"}
            or run.get("workflow_id") != workflow.get("id")
            or run.get("repository", {}).get("full_name") != REPOSITORY
            or run.get("head_repository", {}).get("full_name") != request["head_repository"]
            or run.get("head_branch") != request["head_ref"] or run.get("head_sha") != request["head"]):
        raise ValueError("Completed exact-head PR workflow required")
    return run


def require_workflow_context(request):
    if (os.environ["GITHUB_REPOSITORY"] != REPOSITORY or os.environ["GITHUB_RUN_ATTEMPT"] != "1"
            or os.environ["GITHUB_SHA"] != request["control_commit"]
            or not re.fullmatch(r"[1-9][0-9]*", os.environ["GITHUB_RUN_ID"])):
        raise ValueError("Workflow repository, control or first-attempt identity differs")


def gate(api, event, control_commit, manual_pr=0, manual_head=""):
    run = event.get("workflow_run")
    if run:
        if (run.get("repository", {}).get("full_name") != REPOSITORY or run.get("event") != "pull_request"
                or run.get("name") != "Code progress"):
            return None
        head = run.get("head_sha", "")
        if not isinstance(head, str) or not SHA.fullmatch(head):
            return None
        # Both workflow_run.pull_requests and commit-to-PR associations can be
        # empty for forks. Query the advertised fork branch, then bind the
        # returned PR to its exact repository/ref/head below.
        repository = (run.get("head_repository") or {}).get("full_name")
        branch = run.get("head_branch")
        if (not isinstance(repository, str) or not REPO_NAME.fullmatch(repository)
                or not isinstance(branch, str) or not branch):
            return None
        query = urllib.parse.urlencode({"state": "open", "head": repository.split("/")[0] + ":" + branch,
                                       "per_page": 100})
        candidates = api.request(f"/repos/{REPOSITORY}/pulls?" + query)
        candidates = [pr for pr in candidates if pr.get("state") == "open"
                      and pr.get("head", {}).get("sha") == head
                      and pr.get("head", {}).get("ref") == run.get("head_branch")
                      and (pr.get("head", {}).get("repo") or {}).get("full_name") ==
                          (run.get("head_repository") or {}).get("full_name")
                      and (pr.get("base", {}).get("repo") or {}).get("full_name") == REPOSITORY]
        if len(candidates) != 1:
            return None
        pr = candidates[0]
    else:
        if not SHA.fullmatch(manual_head) or manual_pr <= 0:
            return None
        head = manual_head
        pr = api.request(f"/repos/{REPOSITORY}/pulls/{manual_pr}")
        runs = api.request(f"/repos/{REPOSITORY}/actions/workflows/code-progress.yml/runs?event=pull_request&head_sha={head}&per_page=100")
        run = next((row for row in runs["workflow_runs"] if row.get("status") == "completed"
                    and row.get("conclusion") in {"success", "failure"}), {})
    request = {"pr": pr["number"], "head": head, "control_commit": control_commit,
               "head_repository": (pr.get("head", {}).get("repo") or {}).get("full_name"),
               "head_ref": pr.get("head", {}).get("ref"), "upstream_run": run.get("id")}
    try:
        require_current_pr(api, request)
        require_upstream_run(api, request)
        if published_head(api, request):
            return None
    except ValueError:
        return None
    return request


def published_head(api, request):
    """Only protected publisher provenance can suppress a generated-only loop."""
    statuses = api.request(f"/repos/{REPOSITORY}/commits/{request['head']}/statuses?per_page=100")
    for status in statuses:
        if (status.get("context") != PUBLICATION_CONTEXT or status.get("state") != "success"
                or status.get("creator", {}).get("login") != BOT):
            continue
        match = re.fullmatch(r"https://github.com/" + re.escape(REPOSITORY) + r"/actions/runs/([1-9][0-9]*)",
                             status.get("target_url", ""))
        if not match:
            continue
        run = api.request(f"/repos/{REPOSITORY}/actions/runs/{match[1]}")
        if (run.get("path") == ".github/workflows/native-progress.yml"
                and run.get("head_sha") == request["control_commit"]
                and run.get("event") in {"workflow_run", "workflow_dispatch"}):
            # The protected publisher issues this marker only after verifying
            # the sealed report. A later publication/check-observation failure
            # must not recompile its generated child, nor must pending publisher
            # completion race the child's check workflow.
            jobs = api.request(f"/repos/{REPOSITORY}/actions/runs/{match[1]}/jobs?per_page=100")
            if any(job.get("name") == "Native compilation and evidence"
                   and job.get("status") == "completed" and job.get("conclusion") == "success"
                   for job in jobs["jobs"]):
                return True
    return False


def reporting_tools(control):
    sys.path.insert(0, str(Path(control) / "tools"))
    from lucidlib.codeprogress import validate_public_counts, render_readme_section
    from lucidlib.progressreport import split_readme
    return validate_public_counts, render_readme_section, split_readme




def verify_report(raw, producer, request, run_id, control, source=None):
    validate_request(request)
    if len(raw) > MAX_REPORT_BYTES:
        raise ValueError("Public report exceeds bound")
    report = json.loads(raw)
    reporting_tools(control)[0](report, source)
    driver.require_approved_original(report, json.loads((control / "docs/code-progress.json").read_bytes()))
    expected = {"head": request["head"], "image": EDITOR, "control_commit": request["control_commit"],
                "run_id": str(run_id), "source_fingerprint": report["source_fingerprint"],
                "report_sha256": hashlib.sha256(raw).hexdigest(),
                "compiler_receipt_sha256": report["compiler_receipt_sha256"]}
    if set(producer) != PRODUCER_FIELDS or producer != expected:
        raise ValueError("Report differs from protected real compiler outputs")
    return report


CHECKOUT_TIMEOUT_SECONDS = 300
CHECKOUT_DIAGNOSTIC_BYTES = 16 * 1024


def checkout_run(command, *, cwd, check, stdout, stderr, timeout):
    """Use the controller's owned process tree and cleanup marker for Git too."""
    if stdout != subprocess.PIPE or stderr != subprocess.PIPE:
        raise ValueError('Checkout output must be captured privately')

    def stderr_tail(stream):
        stream.seek(0, os.SEEK_END)
        stream.seek(max(0, stream.tell() - CHECKOUT_DIAGNOSTIC_BYTES))
        return stream.read()

    with tempfile.TemporaryFile(dir=cwd.parent) as errors:
        try:
            output = run_compiler(command, cwd, errors, cwd.parent / 'compiler-group.json', timeout=timeout)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
            try:
                error.stderr = stderr_tail(errors)
            except OSError as capture_error:
                raise error from capture_error
            if check or isinstance(error, subprocess.TimeoutExpired):
                raise
            return subprocess.CompletedProcess(command, error.returncode, error.output, error.stderr)
        return subprocess.CompletedProcess(command, 0, output, stderr_tail(errors))


def retain_checkout_failure(path, sha, phase, command, error, output=None):
    """Keep Git failures outside disposable checkouts, without printing private output."""
    def tail(value):
        if isinstance(value, str):
            value = value.encode('utf-8', 'replace')
        return (value or b'')[-CHECKOUT_DIAGNOSTIC_BYTES:].decode('utf-8', 'replace')

    record = {'checkout': path.name, 'requested_head': sha, 'phase': phase,
              'command': command, 'error_type': type(error).__name__,
              'returncode': getattr(error, 'returncode', None),
              'timeout_seconds': getattr(error, 'timeout', None),
              'error': str(error)[-CHECKOUT_DIAGNOSTIC_BYTES:],
              'stdout_tail': tail(output if output is not None else getattr(error, 'output', None)),
              'stderr_tail': tail(getattr(error, 'stderr', None))}
    receipt = path.parent / ('private-checkout-' + path.name + '.json')
    descriptor = os.open(receipt, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, 'w', encoding='utf-8') as stream:
        json.dump(record, stream, sort_keys=True)
        stream.write('\n')


def checkout(path, sha):
    if path.exists() or path.is_symlink():
        raise ValueError("Fresh checkout required")
    path.mkdir()
    steps = (("init", ["git", "init", "-q"]),
             ("remote", ["git", "remote", "add", "origin", "https://github.com/" + REPOSITORY + ".git"]),
             ("fetch", ["git", "-c", "protocol.file.allow=never", "fetch", "-q", "--depth=1", "origin", sha]),
             ("checkout", ["git", "checkout", "-q", "--detach", "FETCH_HEAD"]),
             ("head", ["git", "rev-parse", "HEAD"]))
    for phase, command in steps:
        try:
            result = checkout_run(command, cwd=path, check=True, stdout=subprocess.PIPE,
                                  stderr=subprocess.PIPE, timeout=CHECKOUT_TIMEOUT_SECONDS)
        except Exception as error:
            try:
                retain_checkout_failure(path, sha, phase, command, error)
            except OSError as retention_error:
                raise error from retention_error
            raise
    if result.stdout.decode('utf-8', 'replace').strip() != sha:
        error = ValueError("Fetched checkout differs")
        try:
            retain_checkout_failure(path, sha, "head", command, error, result.stdout)
        except OSError as retention_error:
            raise error from retention_error
        raise error


def job_root():
    run, attempt = os.environ["GITHUB_RUN_ID"], os.environ["GITHUB_RUN_ATTEMPT"]
    if not re.fullmatch(r"[1-9][0-9]*", run) or attempt != "1":
        raise ValueError("Invalid native run identity")
    return Path("/home/lucid-runner/lucid-jobs") / ("direct-" + run + "-" + attempt)


def group_running(group):
    # Inspect only group IDs/states, never process commands or credentials.
    rows = subprocess.check_output(["ps", "-axo", "pgid=,stat="], text=True).splitlines()
    return any(int(fields[0]) == group and fields[1][0] not in "ZX"
               for row in rows if len(fields := row.split()) == 2)


def signal_group(group, sig):
    try:
        os.killpg(group, sig)
    except ProcessLookupError:
        pass


def freeze_compiler_groups(root):
    # Bootstrap intentionally creates nested sessions. Freeze the tree before
    # killing its parents so those compiler groups cannot escape by reparenting.
    signal_group(root, signal.SIGSTOP)
    known, groups = {root}, {root}
    while True:
        rows = subprocess.check_output(["ps", "-axo", "pid=,ppid=,pgid="], text=True).splitlines()
        changed = False
        for row in rows:
            fields = row.split()
            if len(fields) != 3:
                continue
            pid, parent, group = map(int, fields)
            if pid in known or parent in known or group == root:
                if pid not in known:
                    known.add(pid)
                    changed = True
                if group not in groups:
                    if group == os.getpgrp():
                        raise ValueError("Compiler escaped into controller group; refuse cleanup")
                    signal_group(group, signal.SIGSTOP)
                    groups.add(group)
                    changed = True
        if not changed:
            return sorted(groups)


def stop_compiler_group(process, marker):
    groups = freeze_compiler_groups(process.pid)
    try:
        for group in groups:
            signal_group(group, signal.SIGTERM)
            signal_group(group, signal.SIGCONT)
        try:
            process.communicate(timeout=15)
        except subprocess.TimeoutExpired:
            pass
        for group in groups:
            signal_group(group, signal.SIGKILL)
        process.communicate(timeout=10)
        deadline = time.monotonic() + 10
        while any(group_running(group) for group in groups):
            if time.monotonic() >= deadline:
                raise ValueError("Compiler group did not stop; preserve checkout and refuse next compilation")
            time.sleep(0.05)
    except BaseException as failure:
        # Retain discovered groups even on failed teardown. Lock acquisition is
        # bounded because a surviving descendant may still hold the registry.
        try:
            process_registry.merge_registry(marker, process.pid, groups, create=True)
        except (OSError, ValueError) as registry_error:
            raise failure from registry_error
        raise
    # A frozen descendant may hold the registry lock. Stop our verified tree
    # first, then merge its observations without overwriting registered groups.
    process_registry.merge_registry(marker, process.pid, groups, create=True)
    # Registered survivors are refusal evidence, never extra numeric signal targets.
    require_stopped_marker(marker)


def run_compiler(command, source, errors, marker, timeout=38 * 60, *, registry=None):
    # Job/API/output credentials belong to the controller, never Unity or child
    # processes. Contributor admission remains GitHub's repository policy.
    environment = {key: value for key, value in os.environ.items()
                   if key in {"PATH", "HOME", "LANG", "LC_ALL", "TMPDIR", "DISPLAY",
                              "LUCID_UNITY_EDITOR", "DOTNET_ROOT", "LD_LIBRARY_PATH"}}
    process_registry.clear_stopped_registry(marker, require_stopped_marker)
    options = dict(cwd=source, env=environment, stdout=subprocess.PIPE, stderr=errors)
    if registry is None:
        process = subprocess.Popen(command, start_new_session=True, **options)
        try:
            process_registry.merge_registry(marker, process.pid, [process.pid], create=True)
        except BaseException:
            stop_compiler_group(process, marker)
            raise
    else:
        process = process_registry.spawn_registered(command, [registry, (marker, None)], **options)
    try:
        output, _ = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        stop_compiler_group(process, marker)
        raise
    if group_running(process.pid):
        stop_compiler_group(process, marker)
    require_stopped_marker(marker)
    if process.returncode:
        raise subprocess.CalledProcessError(process.returncode, command, output=output)
    return output


def require_stopped_marker(marker):
    if not marker.exists():
        return
    if marker.is_symlink() or marker.parent.is_symlink():
        raise ValueError("Symlinked compiler marker rejected")
    value = json.loads(marker.read_bytes())
    groups = value.get("groups", [value.get("group")])
    if (not isinstance(groups, list) or not groups
            or any(type(group) is not int or group <= 0 or group_running(group) for group in groups)):
        raise ValueError("Compiler group still active; refuse compilation or cleanup")


def compile_job(api, request):
    require_workflow_context(request)
    require_current_pr(api, request)
    require_upstream_run(api, request)
    root = job_root()
    if root.parent.is_symlink():
        raise ValueError("Symlinked job parent rejected")
    for marker in root.parent.glob("direct-*/compiler-group.json"):
        require_stopped_marker(marker)
    root.mkdir(mode=0o700)
    control, source = root / "control", root / "source"
    checkout(control, request["control_commit"])
    checkout(source, request["head"])
    require_current_pr(api, request)
    driver.require_control_reporting_tools(source, control)
    sys.path.insert(0, str(control / "tools"))
    from lucidlib.verification import artifact_fingerprint
    command = ["python3", str(control / ".github/scripts/native_compilation.py"),
        "--source", str(source), "--control", str(control),
        "--input", "/srv/lucid-task5/input/SonicDreamTeam.app", "--work-dir", str(source / ".cache/project-lucid"),
        "--expected-head", request["head"], "--expected-source", artifact_fingerprint(source),
        "--image", EDITOR, "--control-commit", request["control_commit"], "--run-id", os.environ["GITHUB_RUN_ID"]]
    with (root / "private-driver-stderr.log").open("wb") as errors:
        output = run_compiler(command, source, errors, root / "compiler-group.json")
    producer = json.loads(output)
    raw = (source / "docs/code-progress.json").read_bytes()
    verify_report(raw, producer, request, os.environ["GITHUB_RUN_ID"], control, source)
    require_current_pr(api, request)
    (root / "request.json").write_text(json.dumps(request, sort_keys=True))
    (root / "code-progress.json").write_bytes(raw)
    write_outputs(producer)
    print("Real native compilation and progress generation validated")


def seal_job(api, request, producer):
    require_workflow_context(request)
    require_current_pr(api, request)
    require_upstream_run(api, request)
    root = job_root()
    control, source = root / "control", root / "source"
    if (json.loads((root / "request.json").read_bytes()) != request
            or git_head(control) != request["control_commit"] or git_head(source) != request["head"]):
        raise ValueError("Local source/control/request differs")
    raw = (root / "code-progress.json").read_bytes()
    verify_report(raw, producer, request, os.environ["GITHUB_RUN_ID"], control, source)
    driver.require_control_reporting_tools(source, control)
    write_outputs({"producer_base64": encode(producer), "report_base64": base64.b64encode(raw).decode()})
    print("Sanitized report sealed against protected compiler step outputs")


def cleanup_job():
    root = job_root()
    if root.is_symlink() or root.parent.is_symlink():
        raise ValueError("Refusing cleanup through symlinked job root")
    require_stopped_marker(root / "compiler-group.json")
    for name in ("source", "control"):
        path = root / name
        if path.is_symlink():
            path.unlink()
        elif path.exists():
            shutil.rmtree(path)


def dispatch_checks(api, request, head):
    if request["head_repository"] != REPOSITORY:
        return wait_for_fork_checks(api, request, head)
    current = dict(request, head=head)
    pr = require_current_pr(api, current)
    ref = "/repos/" + REPOSITORY + "/git/refs/heads/" + urllib.parse.quote(pr["head"]["ref"], safe="")
    runs = []
    for workflow in CHECK_WORKFLOWS:
        if api.request(ref)["object"]["sha"] != head:
            raise ValueError("Head changed before required-check dispatch")
        result = api.request(f"/repos/{REPOSITORY}/actions/workflows/{workflow}/dispatches", "POST",
                             {"ref": pr["head"]["ref"], "inputs": {"expected_head": head}})
        run_id = (result or {}).get("workflow_run_id")
        if (type(run_id) is not int or run_id <= 0
                or result.get("html_url") != f"https://github.com/{REPOSITORY}/actions/runs/{run_id}"):
            raise ValueError("Required-check dispatch uncertain; do not automatically retry")
        runs.append(result["html_url"])
    return runs


def wait_for_fork_checks(api, request, head, sleep=time.sleep, monotonic=time.monotonic, seconds=240):
    """Observe real PR checks triggered by the publisher's non-Actions token.

    Base workflow_dispatch cannot target a branch in another repository. Never
    dispatch on main and call those checks evidence for the contributor head.
    """
    deadline = monotonic() + seconds
    while True:
        require_current_pr(api, dict(request, head=head))
        found = []
        for workflow in CHECK_WORKFLOWS:
            runs = api.request(f"/repos/{REPOSITORY}/actions/workflows/{workflow}/runs?event=pull_request&head_sha={head}&per_page=100")
            matches = [row for row in runs["workflow_runs"] if row.get("head_sha") == head
                       and row.get("event") == "pull_request"
                       and row.get("head_branch") == request["head_ref"]
                       and (row.get("head_repository") or {}).get("full_name") == request["head_repository"]
                       and row.get("status") in {"queued", "in_progress", "completed"}
                       and row.get("conclusion") not in {"skipped", "cancelled", "action_required"}]
            if matches:
                run = matches[0]
                url = f"https://github.com/{REPOSITORY}/actions/runs/{run['id']}"
                if run.get("html_url") != url:
                    raise ValueError("Fork check URL differs")
                found.append(url)
        if len(found) == len(CHECK_WORKFLOWS):
            return found
        if monotonic() >= deadline:
            raise ValueError("Published head retained; exact-head PR checks not observed, no automatic retry")
        sleep(min(2, max(0, deadline - monotonic())))


def head_api(request):
    # Public ref reads need no credential. In particular the base job token is
    # never sent to a contributor-selected repository.
    return GitHub(request["head_repository"], token="")


def push_fork(api, request, source, created):
    if not os.environ.get("LUCID_FORK_TOKEN"):
        raise PermissionError("Fork writeback requires hosted publisher access")
    require_current_pr(api, request)
    # Fetch a commit made by trusted API code; do not run hooks or PR scripts.
    environment = {key: value for key, value in os.environ.items() if key in {"PATH", "HOME", "LANG", "LC_ALL"}}
    subprocess.run(["git", "-c", "core.hooksPath=/dev/null", "fetch", "--no-tags", "origin", created],
                   cwd=source, env=environment, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    helper = '!f() { printf "username=x-access-token\\npassword=%s\\n" "$LUCID_FORK_TOKEN"; }; f'
    environment = dict(environment)
    environment.update(LUCID_FORK_TOKEN=os.environ["LUCID_FORK_TOKEN"], GIT_TERMINAL_PROMPT="0")
    ref = "refs/heads/" + request["head_ref"]
    require_current_pr(api, request)
    # The lease is an atomic compare-and-swap. The new commit has this exact
    # head as its sole parent, so a contributor's concurrent commit is preserved.
    subprocess.run(["git", "-c", "core.hooksPath=/dev/null", "-c", "credential.helper=",
                    "-c", "credential.helper=" + helper, "push",
                    "--force-with-lease=" + ref + ":" + request["head"],
                    "https://github.com/" + request["head_repository"] + ".git", created + ":" + ref],
                   cwd=source, env=environment, check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def wait_for_published_head(api, request, branch, head, sleep=time.sleep,
                            monotonic=time.monotonic, seconds=240):
    """Bound API propagation after the atomic push; never repush or overwrite."""
    deadline=monotonic()+seconds
    ref_api = api if request["head_repository"] == REPOSITORY else head_api(request)
    ref=f"/repos/{request['head_repository']}/git/refs/heads/"+urllib.parse.quote(branch,safe="")
    while True:
        pr=api.request(f"/repos/{REPOSITORY}/pulls/{request['pr']}")
        actual=pr.get("head",{}).get("sha")
        if actual not in {request["head"],head} or pr.get("head",{}).get("ref") != branch:
            raise ValueError("Concurrent head or branch change after publication")
        require_pr_metadata(pr,dict(request, head=actual))
        reference = ref_api.request(ref)["object"]["sha"]
        if default_head(api)[1] != request["control_commit"] or reference not in {request["head"], head}:
            raise ValueError("Control or published ref changed during PR metadata wait")
        if monotonic() >= deadline:
            raise ValueError("PR metadata did not catch up; published ref retained, no automatic retry")
        if actual == head and reference == head:
            return
        sleep(min(2, max(0,deadline-monotonic())))


def publish(api, request, raw, producer, control, source):
    require_workflow_context(request)
    pr = require_current_pr(api, request)
    require_upstream_run(api, request)
    if git_head(control) != request["control_commit"] or git_head(source) != request["head"]:
        raise ValueError("Publisher source/control checkout differs")
    driver.require_control_reporting_tools(source, control)
    report = verify_report(raw, producer, request, os.environ["GITHUB_RUN_ID"], control, source)
    _, render, split = reporting_tools(control)
    readme = source / "README.md"
    if readme.is_symlink() or not readme.is_file() or readme.stat().st_size > 1024 * 1024:
        raise ValueError("Invalid README")
    before, _, after = split(readme.read_text())
    updated = (before + render(report).rstrip("\n") + after).encode()
    updated += b"" if updated.endswith(b"\n") else b"\n"
    payload = (json.dumps(report, indent=2, sort_keys=True) + "\n").encode()
    if readme.read_bytes() == updated and (source / "docs/code-progress.json").read_bytes() == payload:
        return request["head"], dispatch_checks(api, request, request["head"])
    fork = request["head_repository"] != REPOSITORY
    if fork and (pr.get("maintainer_can_modify") is not True or not os.environ.get("LUCID_FORK_TOKEN")):
        print("::error::Native compilation succeeded; generated progress was not published. "
              "Fork writeback requires Allow edits by maintainers and a fork-capable hosted publisher credential.")
        raise PermissionError("Fork writeback unavailable; original contributor head retained")
    user = api.request("/users/github-actions%5Bbot%5D")
    if user.get("login") != BOT or user.get("type") != "Bot" or type(user.get("id")) is not int or user["id"] <= 0:
        raise ValueError("GitHub Actions bot identity differs")
    identity = {"name": BOT, "email": f"{user['id']}+{BOT}@users.noreply.github.com"}
    head = request["head"]
    commit = api.request(f"/repos/{REPOSITORY}/git/commits/{head}")
    tree = []
    for path, data in (("README.md", updated), ("docs/code-progress.json", payload)):
        blob = api.request(f"/repos/{REPOSITORY}/git/blobs", "POST",
                           {"content": base64.b64encode(data).decode(), "encoding": "base64"})["sha"]
        tree.append({"path": path, "mode": "100644", "type": "blob", "sha": blob})
    tree_sha = api.request(f"/repos/{REPOSITORY}/git/trees", "POST", {"base_tree": commit["tree"]["sha"], "tree": tree})["sha"]
    created = api.request(f"/repos/{REPOSITORY}/git/commits", "POST", {
        "message": "Update verified Linux code progress", "tree": tree_sha, "parents": [head],
        "author": identity, "committer": identity})["sha"]
    if not isinstance(created, str) or not SHA.fullmatch(created):
        raise ValueError("Created commit identity differs")
    require_current_pr(api, request)
    ref = f"/repos/{request['head_repository']}/git/refs/heads/" + urllib.parse.quote(pr["head"]["ref"], safe="")
    ref_api = head_api(request) if fork else api
    if ref_api.request(ref)["object"]["sha"] != head:
        raise ValueError("Concurrent commit; refuse publication")
    if fork:
        # Base-repository bot provenance prevents the PAT/App-triggered PR check
        # from starting another native refresh of these generated-only changes.
        api.request(f"/repos/{REPOSITORY}/statuses/{created}", "POST", {
            "state": "success", "context": PUBLICATION_CONTEXT,
            "description": "Generated outputs from verified native compilation",
            "target_url": f"https://github.com/{REPOSITORY}/actions/runs/{os.environ['GITHUB_RUN_ID']}"})
        push_fork(api, request, source, created)
    else:
        api.request(ref, "PATCH", {"sha": created, "force": False})
    wait_for_published_head(api,request,pr["head"]["ref"],created)
    return created, dispatch_checks(api, request, created)


def encode(value):
    return base64.b64encode(json.dumps(value, separators=(",", ":")).encode()).decode()


def write_outputs(values):
    with open(os.environ["GITHUB_OUTPUT"], "a") as output:
        for key, value in values.items():
            text = str(value)
            if "\n" in text or "\r" in text:
                raise ValueError("Multiline output rejected")
            output.write(key + "=" + text + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("gate", "compile", "seal", "cleanup", "publish"))
    mode = parser.parse_args().mode
    api = GitHub()
    if mode == "cleanup":
        cleanup_job()
        return
    if mode == "gate":
        event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
        request = gate(api, event, os.environ["GITHUB_SHA"], int(os.environ.get("PR_NUMBER") or 0), os.environ.get("EXPECTED_HEAD", ""))
        if request:
            require_workflow_context(request)
        write_outputs({"run": "true" if request else "false", "head": request["head"] if request else "",
                       "request_base64": encode(request) if request else ""})
        return
    request = json.loads(base64.b64decode(os.environ["REQUEST_BASE64"], validate=True))
    require_workflow_context(request)
    if mode == "compile":
        compile_job(api, request)
    elif mode == "seal":
        seal_job(api, request, json.loads(os.environ["COMPILE_OUTPUTS"]))
    else:
        head, runs = publish(api, request, base64.b64decode(os.environ["REPORT_BASE64"], validate=True),
            json.loads(base64.b64decode(os.environ["PRODUCER_BASE64"], validate=True)), Path("control"), Path("source"))
        print("Verified output head: " + head)
        for url in runs:
            print("Required check run (completion not claimed): " + url)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        detail="HTTP " + str(error.code) if isinstance(error,urllib.error.HTTPError) else type(error).__name__
        print("Native progress failed ("+detail+"); inspect sanitized run state or private VM diagnostics", file=sys.stderr)
        sys.exit(1)

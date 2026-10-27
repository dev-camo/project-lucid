"""Shared default-branch helpers for the progress workflows (main_progress, compiler_precheck,
editor_sandbox, process_registry): the PR gate, request validation, checkout and compiler control.

The controller has read-only API access. No PR Python is imported by the gate.
"""
import base64
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

import native_compilation as driver
import process_registry

REPOSITORY = "dev-camo/project-lucid"
EDITOR = "native@sha256:88c95da13fdd8f9b17229b860f5ac72c7706586eb18b7fee581d206b45148dcc"
BOT = "github-actions[bot]"
SHA = re.compile(r"[0-9a-f]{40}\Z")
PRODUCER_FIELDS = {"head", "image", "control_commit", "run_id", "source_fingerprint",
                   "report_sha256", "compiler_receipt_sha256"}
MAX_REPORT_BYTES = 40 * 1024
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


def gate(api, event, control_commit):
    run = event.get("workflow_run")
    if not run:
        return None
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
    request = {"pr": pr["number"], "head": head, "control_commit": control_commit,
               "head_repository": (pr.get("head", {}).get("repo") or {}).get("full_name"),
               "head_ref": pr.get("head", {}).get("ref"), "upstream_run": run.get("id")}
    try:
        require_current_pr(api, request)
        require_upstream_run(api, request)
    except ValueError:
        return None
    return request


def reporting_tools(control):
    sys.path.insert(0, str(Path(control) / "tools"))
    from lucidlib.codeprogress import validate_public_counts, render_readme_section
    from lucidlib.progressreport import split_readme
    return validate_public_counts, render_readme_section, split_readme




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


def encode(value):
    return base64.b64encode(json.dumps(value, separators=(",", ":")).encode()).decode()


def write_outputs(values):
    with open(os.environ["GITHUB_OUTPUT"], "a") as output:
        for key, value in values.items():
            text = str(value)
            if "\n" in text or "\r" in text:
                raise ValueError("Multiline output rejected")
            output.write(key + "=" + text + "\n")

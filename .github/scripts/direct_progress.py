"""Default-branch control for trusted owner PRs on the native Ubuntu runner.

Compilation receives read-only credentials. A separate hosted job publishes only
validated generated outputs and explicitly dispatches the existing required checks.
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
import subprocess
import sys
import urllib.parse
import urllib.request

import native_compilation as driver

REPOSITORY = "dev-camo/project-lucid"
EDITOR = "native@sha256:88c95da13fdd8f9b17229b860f5ac72c7706586eb18b7fee581d206b45148dcc"
BOT = "github-actions[bot]"
SHA = re.compile(r"[0-9a-f]{40}\Z")
PRODUCER_FIELDS = {"head", "image", "control_commit", "run_id", "source_fingerprint",
                   "report_sha256", "compiler_receipt_sha256"}
MAX_REPORT_BYTES = 40 * 1024
CHECK_WORKFLOWS = ("code-progress.yml", "tool-tests.yml")


class GitHub:
    def request(self, path, method="GET", body=None):
        if not path.startswith("/repos/" + REPOSITORY + "/") and path not in (
                "/repos/" + REPOSITORY, "/users/github-actions%5Bbot%5D"):
            raise ValueError("Foreign API path rejected")
        headers = {"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2026-03-10"}
        if os.environ.get("GH_TOKEN"):
            headers["Authorization"] = "Bearer " + os.environ["GH_TOKEN"]
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
    if (set(request) != {"pr", "head", "control_commit"} or type(request["pr"]) is not int
            or request["pr"] <= 0 or not all(isinstance(request[key], str) and SHA.fullmatch(request[key])
                                            for key in ("head", "control_commit"))):
        raise ValueError("Exact PR, head and control required")


def require_current_owner(api, request):
    validate_request(request)
    pr = api.request(f"/repos/{REPOSITORY}/pulls/{request['pr']}")
    if (pr.get("state") != "open" or pr.get("draft") or pr.get("user", {}).get("login") != "dev-camo"
            or pr.get("head", {}).get("sha") != request["head"]
            or (pr.get("head", {}).get("repo") or {}).get("full_name") != REPOSITORY
            or (pr.get("base", {}).get("repo") or {}).get("full_name") != REPOSITORY):
        raise ValueError("Contributor, stale, draft, closed or foreign PR rejected")
    branch, head = default_head(api)
    if head != request["control_commit"] or pr["head"]["ref"] == branch:
        raise ValueError("Control changed or PR targets the default-branch ref")
    return pr


def require_workflow_context(request):
    if (os.environ["GITHUB_REPOSITORY"] != REPOSITORY or os.environ["GITHUB_RUN_ATTEMPT"] != "1"
            or os.environ["GITHUB_SHA"] != request["control_commit"]
            or not re.fullmatch(r"[1-9][0-9]*", os.environ["GITHUB_RUN_ID"])):
        raise ValueError("Workflow repository, control or first-attempt identity differs")


def gate(api, event, control_commit, manual_pr=0, manual_head=""):
    run = event.get("workflow_run")
    if run:
        if (run.get("repository", {}).get("full_name") != REPOSITORY or run.get("event") != "pull_request"
                or run.get("name") != "Code progress" or len(run.get("pull_requests", [])) != 1):
            return None
        number, head = run["pull_requests"][0]["number"], run["head_sha"]
    else:
        number, head = manual_pr, manual_head
    request = {"pr": number, "head": head, "control_commit": control_commit}
    validate_request(request)
    try:
        require_current_owner(api, request)
    except ValueError:
        return None
    return request


def reporting_tools(control):
    sys.path.insert(0, str(Path(control) / "tools"))
    from lucidlib.codeprogress import validate_public_counts, render_readme_section
    from lucidlib.progressreport import split_readme
    return validate_public_counts, render_readme_section, split_readme


def fresh_bot_output(api, source, control, head):
    commit = api.request(f"/repos/{REPOSITORY}/commits/{head}")
    files = commit.get("files", [])
    if (any((commit.get(field) or {}).get("login") != BOT for field in ("author", "committer"))
            or len(commit.get("parents", [])) != 1 or not files
            or any(row["filename"] not in {"README.md", "docs/code-progress.json"}
                   or row.get("status") != "modified" for row in files)):
        return False
    driver.require_control_reporting_tools(source, control)
    validate, render, split = reporting_tools(control)
    report = json.loads((source / "docs/code-progress.json").read_bytes())
    validate(report, source)
    driver.require_approved_original(report, json.loads((control / "docs/code-progress.json").read_bytes()))
    if split((source / "README.md").read_text())[1] + "\n" != render(report):
        raise ValueError("Bot README differs from fresh report")
    return True


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


def checkout(path, sha):
    if path.exists() or path.is_symlink():
        raise ValueError("Fresh checkout required")
    path.mkdir()
    for command in (["git", "init", "-q"],
                    ["git", "remote", "add", "origin", "https://github.com/" + REPOSITORY + ".git"],
                    ["git", "-c", "protocol.file.allow=never", "fetch", "-q", "--depth=1", "origin", sha],
                    ["git", "checkout", "-q", "--detach", "FETCH_HEAD"]):
        subprocess.run(command, cwd=path, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if git_head(path) != sha:
        raise ValueError("Fetched checkout differs")


def job_root():
    run, attempt = os.environ["GITHUB_RUN_ID"], os.environ["GITHUB_RUN_ATTEMPT"]
    if not re.fullmatch(r"[1-9][0-9]*", run) or attempt != "1":
        raise ValueError("Invalid native run identity")
    return Path("/home/lucid-runner/lucid-jobs") / ("direct-" + run + "-" + attempt)


def compile_job(api, request):
    require_workflow_context(request)
    require_current_owner(api, request)
    root = job_root()
    if root.parent.is_symlink():
        raise ValueError("Symlinked job parent rejected")
    root.mkdir(mode=0o700)
    control, source = root / "control", root / "source"
    checkout(control, request["control_commit"])
    checkout(source, request["head"])
    require_current_owner(api, request)
    driver.require_control_reporting_tools(source, control)
    sys.path.insert(0, str(control / "tools"))
    from lucidlib.verification import artifact_fingerprint
    command = ["xvfb-run", "-a", "python3", str(control / ".github/scripts/native_compilation.py"),
        "--source", str(source), "--control", str(control),
        "--input", "/srv/lucid-task5/input/SonicDreamTeam.app", "--work-dir", str(source / ".cache/project-lucid"),
        "--expected-head", request["head"], "--expected-source", artifact_fingerprint(source),
        "--image", EDITOR, "--control-commit", request["control_commit"], "--run-id", os.environ["GITHUB_RUN_ID"]]
    with (root / "private-driver-stderr.log").open("wb") as errors:
        result = subprocess.run(command, cwd=source, check=True, stdout=subprocess.PIPE, stderr=errors, timeout=38 * 60)
    producer = json.loads(result.stdout)
    raw = (source / "docs/code-progress.json").read_bytes()
    verify_report(raw, producer, request, os.environ["GITHUB_RUN_ID"], control, source)
    require_current_owner(api, request)
    (root / "request.json").write_text(json.dumps(request, sort_keys=True))
    (root / "code-progress.json").write_bytes(raw)
    write_outputs(producer)
    print("Real native compilation and progress generation validated")


def seal_job(api, request, producer):
    require_workflow_context(request)
    require_current_owner(api, request)
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
    for name in ("source", "control"):
        path = root / name
        if path.is_symlink():
            path.unlink()
        elif path.exists():
            shutil.rmtree(path)


def dispatch_checks(api, request, head):
    current = dict(request, head=head)
    pr = require_current_owner(api, current)
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


def publish(api, request, raw, producer, control, source):
    require_workflow_context(request)
    pr = require_current_owner(api, request)
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
    require_current_owner(api, request)
    ref = f"/repos/{REPOSITORY}/git/refs/heads/" + urllib.parse.quote(pr["head"]["ref"], safe="")
    if api.request(ref)["object"]["sha"] != head:
        raise ValueError("Concurrent commit; refuse publication")
    api.request(ref, "PATCH", {"sha": created, "force": False})
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
    parser.add_argument("mode", choices=("gate", "admit", "compile", "seal", "cleanup", "publish"))
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
    if mode == "admit":
        require_current_owner(api, request)
        skip = fresh_bot_output(api, Path("source"), Path("control"), request["head"])
        write_outputs({"compile": str(not skip).lower(), "recheck": str(skip).lower()})
    elif mode == "compile":
        compile_job(api, request)
    elif mode == "seal":
        seal_job(api, request, json.loads(os.environ["COMPILE_OUTPUTS"]))
    elif os.environ.get("RECHECK_ONLY") == "true":
        require_current_owner(api, request)
        if not fresh_bot_output(api, Path("source"), Path("control"), request["head"]):
            raise ValueError("Check-only recovery requires a fresh output-only bot commit")
        for url in dispatch_checks(api, request, request["head"]):
            print("Required checks requested: " + url)
    else:
        head, runs = publish(api, request, base64.b64decode(os.environ["REPORT_BASE64"], validate=True),
            json.loads(base64.b64decode(os.environ["PRODUCER_BASE64"], validate=True)), Path("control"), Path("source"))
        print("Verified output head: " + head)
        for url in runs:
            print("Required checks requested: " + url)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("Native progress failed; inspect sanitized run state or private VM diagnostics", file=sys.stderr)
        sys.exit(1)

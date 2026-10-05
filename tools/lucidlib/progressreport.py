"""Publish the generated code coverage table and check it without game input."""

import json
import os
from pathlib import Path
import subprocess
import uuid

from .verification import artifact_fingerprint

START = "<!-- LUCID_CODE_PROGRESS:START -->"
END = "<!-- LUCID_CODE_PROGRESS:END -->"
SNAPSHOT = "docs/code-progress.json"


def split_readme(text):
    if START not in text and END not in text:
        return text.rstrip() + "\n\n", "", ""
    if text.count(START) != 1 or text.count(END) != 1:
        raise ValueError("README code progress markers must occur exactly once")
    before, rest = text.split(START, 1)
    if END not in rest:
        raise ValueError("README code progress markers are out of order")
    section, after = rest.split(END, 1)
    return before, START + section + END, after


def _regular(path):
    if path.is_symlink() or path.parent.is_symlink():
        raise ValueError("Refusing a symlinked progress output")
    if path.exists() and not path.is_file():
        raise ValueError("Progress output must be a regular file")
    return path.read_bytes() if path.is_file() else None


def check_progress(repo_root):
    from .codeprogress import _json, render_readme_section, validate_public_counts
    root = Path(repo_root)
    _, report = _json(root / SNAPSHOT, 1024 * 1024)
    validate_public_counts(report, root)
    readme = _regular(root / "README.md")
    if readme is None:
        raise ValueError("README.md is missing")
    _, actual, _ = split_readme(readme.decode("utf-8"))
    if actual + "\n" != render_readme_section(report):
        raise ValueError("README code progress differs from the generated snapshot")
    return {"status": "complete", "source_fingerprint": report["source_fingerprint"],
            "totals": report["totals"]}


def _require_readme_index(root, original):
    process = subprocess.run(["git", "show", ":README.md"], cwd=root, capture_output=True)
    if process.returncode:
        raise ValueError("Stage README.md before generating commit progress")
    staged = split_readme(process.stdout.decode("utf-8"))
    working = split_readme(original.decode("utf-8"))
    if (staged[0], staged[2]) != (working[0], working[2]):
        raise ValueError("Stage README edits before generating commit progress")


def _replace(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name("." + path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def generate_progress(repo_root, work_dir, input_path, target="macos", stage=False):
    from .bootstrap import managed_path, validate_work_dir
    work = validate_work_dir(work_dir)
    lock = managed_path(work, "locks", "progress-publication.lock")
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        lock.mkdir()
    except FileExistsError as error:
        raise ValueError("Another progress publication owns the lock") from error
    try:
        return _generate_progress(repo_root, work, input_path, target, stage)
    finally:
        lock.rmdir()


def _generate_progress(repo_root, work_dir, input_path, target, stage):
    from .codeprogress import run_code_progress, render_readme_section, validate_public_counts
    from .progressgit import require_matching_source_index
    root = Path(repo_root).resolve()
    readme_path, snapshot_path = root / "README.md", root / SNAPSHOT
    original_readme = _regular(readme_path)
    if original_readme is None:
        raise ValueError("README.md is missing")
    original_snapshot = _regular(snapshot_path)
    before, _, after = split_readme(original_readme.decode("utf-8"))
    if stage:
        require_matching_source_index(root)
        _require_readme_index(root, original_readme)
    result = run_code_progress(root, work_dir, input_path, target)
    report = result["public"]
    validate_public_counts(report, root)
    if _regular(readme_path) != original_readme or _regular(snapshot_path) != original_snapshot:
        raise ValueError("Progress outputs changed during measurement")
    if stage:
        require_matching_source_index(root)
        _require_readme_index(root, original_readme)
    readme = (before + render_readme_section(report).rstrip("\n") + after).encode("utf-8")
    if not readme.endswith(b"\n"):
        readme += b"\n"
    snapshot = (json.dumps(report, indent=2, sort_keys=True) + "\n").encode("utf-8")
    try:
        _replace(snapshot_path, snapshot)
        _replace(readme_path, readme)
        if artifact_fingerprint(root) != report["source_fingerprint"]:
            raise ValueError("Source changed while publishing progress")
        check_progress(root)
        if stage:
            require_matching_source_index(root)
            _require_readme_index(root, original_readme)
            subprocess.run(["git", "add", "--", "README.md", SNAPSHOT], cwd=root, check=True)
            require_matching_source_index(root)
    except BaseException:
        if original_snapshot is None:
            snapshot_path.unlink(missing_ok=True)
        else:
            _replace(snapshot_path, original_snapshot)
        _replace(readme_path, original_readme)
        raise
    return {"status": "complete", "source_fingerprint": report["source_fingerprint"],
            "totals": report["totals"], "snapshot": SNAPSHOT, "staged": stage}

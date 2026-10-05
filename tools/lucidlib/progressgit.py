"""Keep a commit's progress evidence tied to the source being committed."""

import hashlib
import subprocess
from pathlib import Path, PurePosixPath

from .verification import artifact_fingerprint


def _source_path(path):
    parts = PurePosixPath(path).parts
    if len(parts) < 2 or parts[0] not in ("Assets", "Packages", "ProjectSettings", "tools"):
        return False
    if parts[0] == "Assets" and parts[1] in ("Recovered", "StreamingAssets", "Recovered.meta", "StreamingAssets.meta"):
        return False
    if any(part.startswith(".") or part in ("obj", "bin", "__pycache__") for part in parts[1:-1]):
        return False
    return parts[-1] != ".DS_Store" and not parts[-1].endswith((".pyc", ".pyo", ".tmp"))


def _index_rows(root):
    raw = subprocess.check_output(["git", "ls-files", "--stage", "-z", "--",
                                   "Assets", "Packages", "ProjectSettings", "tools"], cwd=root)
    rows = []
    for record in raw.split(b"\0"):
        if not record:
            continue
        info, name = record.split(b"\t", 1)
        path = name.decode("utf-8", "surrogateescape")
        if not _source_path(path):
            continue
        mode, oid, stage = info.decode("ascii").split()
        if stage != "0" or mode not in ("100644", "100755"):
            raise ValueError("Commit progress requires regular, unconflicted source: " + path)
        rows.append((path, oid))
    return sorted(rows)


def require_matching_source_index(repo_root):
    root = Path(repo_root).resolve()
    rows = _index_rows(root)
    digest = hashlib.sha256()
    hashes = {}
    process = subprocess.Popen(["git", "cat-file", "--batch"], cwd=root,
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    try:
        for path, oid in rows:
            if oid not in hashes:
                process.stdin.write((oid + "\n").encode("ascii"))
                process.stdin.flush()
                header = process.stdout.readline().decode("ascii").split()
                if len(header) != 3 or header[:2] != [oid, "blob"]:
                    raise ValueError("Source index object is unavailable")
                remaining = int(header[2])
                content = hashlib.sha256()
                while remaining:
                    data = process.stdout.read(min(remaining, 1024 * 1024))
                    if not data:
                        raise ValueError("Source index object is truncated")
                    content.update(data)
                    remaining -= len(data)
                if process.stdout.read(1) != b"\n":
                    raise ValueError("Invalid source index object boundary")
                hashes[oid] = content.hexdigest()
            digest.update((path + "\0" + hashes[oid] + "\n").encode("utf-8"))
    finally:
        process.stdin.close()
        process.stdout.close()
        if process.wait() != 0:
            raise ValueError("Git source index inspection failed")
    if _index_rows(root) != rows:
        raise ValueError("Source index changed while measuring commit progress")
    if digest.hexdigest() != artifact_fingerprint(root):
        raise ValueError("Stage all maintained source changes before generating commit progress; "
                         "remove ignored local source files that are absent from the index")

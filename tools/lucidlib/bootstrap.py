"""Download and build pinned external recovery tools in an isolated cache."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import platform
import shutil
import signal
import stat
import subprocess
import tarfile
import time
import urllib.request
import uuid
import zipfile

try:
    import fcntl
except ImportError:  # Inspection/validation must still import on Windows.
    fcntl = None


REPO_ROOT = Path(__file__).resolve().parents[2]
CACHE_ROOT = REPO_ROOT / ".cache" / "project-lucid"
LOCK_FILE = REPO_ROOT / "tools" / "tool-lock.json"


class ToolError(RuntimeError):
    """A pinned tool could not be downloaded, installed, or executed."""


def validate_work_dir(work_dir: Path) -> Path:
    """Allow generated cache directories only; reject source paths and symlinks."""
    requested = Path(os.path.abspath(os.fspath(work_dir)))
    try:
        requested.relative_to(CACHE_ROOT)
    except ValueError as exc:
        raise ValueError(f"Work directory must be {CACHE_ROOT} or a child directory") from exc
    current = REPO_ROOT
    for part in requested.relative_to(REPO_ROOT).parts:
        current /= part
        if current.is_symlink():
            raise ValueError(f"Work directory cannot contain a symbolic link: {current}")
        if current.exists() and not current.is_dir():
            raise ValueError(f"Work directory component is not a directory: {current}")
    return requested


def managed_path(work_dir: Path, *parts: str) -> Path:
    """Check descendants before writing so a cached symlink cannot redirect output."""
    root = validate_work_dir(work_dir)
    result = root.joinpath(*parts)
    try:
        relative = result.relative_to(root)
    except ValueError as exc:
        raise ValueError("Generated path escapes the work directory") from exc
    if ".." in relative.parts:
        raise ValueError("Generated path cannot contain parent traversal")
    current = root
    for part in relative.parts:
        current /= part
        if current.is_symlink():
            raise ValueError(f"Generated path cannot contain a symbolic link: {current}")
    return result


def write_json(path: Path, value: dict) -> None:
    """Write a report atomically. The caller must already validate its location."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    try:
        temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def load_lock() -> dict:
    lock = json.loads(LOCK_FILE.read_text(encoding="utf-8"))
    if lock.get("schema_version") != 1:
        raise ToolError("Unsupported tools/tool-lock.json schema")
    return lock


def _digest(path: Path, algorithm: str) -> str:
    result = hashlib.new(algorithm)
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def download_artifact(artifact: dict, work_dir: Path) -> Path:
    """Cache a hash-verified artifact; incomplete downloads are never promoted."""
    algorithm = "sha512" if "sha512" in artifact else "sha256"
    expected = artifact.get(algorithm, "")
    if len(expected) != hashlib.new(algorithm).digest_size * 2:
        raise ToolError("Artifact has no valid pinned checksum")
    filename = artifact["filename"]
    if Path(filename).name != filename:
        raise ToolError("Artifact filename must be a single path component")
    path = managed_path(work_dir, "downloads", filename)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_file() and _digest(path, algorithm) == expected:
        return path
    temporary = managed_path(work_dir, "downloads", f".{filename}.{uuid.uuid4().hex}.part")
    try:
        request = urllib.request.Request(artifact["url"], headers={"User-Agent": "ProjectLucid-Recovery/1.0"})
        checksum = hashlib.new(algorithm)
        with urllib.request.urlopen(request, timeout=60) as source, temporary.open("xb") as output:
            for block in iter(lambda: source.read(1024 * 1024), b""):
                checksum.update(block)
                output.write(block)
        if checksum.hexdigest() != expected:
            raise ToolError(f"Checksum mismatch for {filename}; downloaded artifact was not used")
        temporary.replace(path)
        return path
    except (OSError, urllib.error.URLError) as exc:
        raise ToolError(f"Could not download {filename}: {exc}") from exc
    finally:
        temporary.unlink(missing_ok=True)


def _member_path(name: str, strip_components: int) -> Path | None:
    raw = PurePosixPath(name)
    if raw.is_absolute() or ".." in raw.parts or "\\" in name or (raw.parts and ":" in raw.parts[0]):
        raise ToolError(f"Unsafe archive member: {name}")
    parts = raw.parts[strip_components:]
    return Path(*parts) if parts else None


def unpack_archive(archive: Path, destination: Path, artifact: dict) -> None:
    """Unpack regular files only into a fresh directory; reject links and traversal."""
    destination = validate_work_dir(destination)
    if destination.exists():
        raise ToolError("Archive destination must be a fresh directory")
    destination.mkdir(parents=True)
    strip = artifact.get("strip_components", 0)
    limit = artifact.get("max_unpacked_bytes", 1000000000)
    total = 0
    try:
        if artifact["format"] == "tar":
            with tarfile.open(archive, "r:*") as source:
                for member in source:
                    relative = _member_path(member.name, strip)
                    if member.issym() or member.islnk() or not (member.isdir() or member.isfile()):
                        raise ToolError(f"Archive links and special files are unsupported: {member.name}")
                    if relative is None:
                        continue
                    target = destination / relative
                    if member.isdir():
                        target.mkdir(parents=True, exist_ok=True)
                    else:
                        total += member.size
                        if total > limit:
                            raise ToolError("Archive exceeds the pinned unpacked size limit")
                        target.parent.mkdir(parents=True, exist_ok=True)
                        with source.extractfile(member) as input_stream, target.open("xb") as output:
                            shutil.copyfileobj(input_stream, output)
                        target.chmod(member.mode & 0o777)
        elif artifact["format"] == "zip":
            with zipfile.ZipFile(archive) as source:
                for member in source.infolist():
                    relative = _member_path(member.filename, strip)
                    mode = member.external_attr >> 16
                    if stat.S_ISLNK(mode):
                        raise ToolError(f"Archive symbolic link is unsupported: {member.filename}")
                    if relative is None:
                        continue
                    target = destination / relative
                    if member.is_dir():
                        target.mkdir(parents=True, exist_ok=True)
                    else:
                        total += member.file_size
                        if total > limit:
                            raise ToolError("Archive exceeds the pinned unpacked size limit")
                        target.parent.mkdir(parents=True, exist_ok=True)
                        with source.open(member) as input_stream, target.open("xb") as output:
                            shutil.copyfileobj(input_stream, output)
                        target.chmod((mode & 0o777) or 0o644)
        else:
            raise ToolError("Unsupported archive format")
    except Exception:
        shutil.rmtree(destination)
        raise


def run_logged(command: list[str], work_dir: Path, log_name: str, *, timeout: int = 900,
               env: dict | None = None, cwd: Path | None = None) -> dict:
    """Bound a child process and its descendants; keep evidence in the cache."""
    log = managed_path(work_dir, "reports", log_name)
    log.parent.mkdir(parents=True, exist_ok=True)
    location = validate_work_dir(cwd or work_dir)
    location.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    with log.open("w", encoding="utf-8") as stream:
        stream.write(json.dumps({"command": command, "cwd": str(location)}) + "\n")
        stream.flush()
        process = subprocess.Popen(command, cwd=location, env=env, stdin=subprocess.DEVNULL,
                                   stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        timed_out = False
        try:
            returncode = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            if os.name == "posix":
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
            else:
                process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                if os.name == "posix":
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                else:
                    process.kill()
                process.wait()
            returncode = process.returncode
            stream.write(f"\nRecovery wrapper stopped the process after {timeout} seconds.\n")
    return {"command": command, "log": str(log), "returncode": returncode,
            "timed_out": timed_out, "elapsed_seconds": round(time.monotonic() - started, 3)}


def dotnet_environment(work_dir: Path, dotnet_root: Path | None = None) -> dict:
    env = os.environ.copy()
    for key, path in [("DOTNET_CLI_HOME", managed_path(work_dir, "dotnet-home")),
                      ("NUGET_PACKAGES", managed_path(work_dir, "nuget-packages")),
                      ("DOTNET_BUNDLE_EXTRACT_BASE_DIR", managed_path(work_dir, "dotnet-bundles"))]:
        path.mkdir(parents=True, exist_ok=True)
        env[key] = str(path)
    if dotnet_root:
        env["DOTNET_ROOT"] = str(dotnet_root)
    env.update(DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    return env


def _install(name: str, lock: dict, work_dir: Path) -> Path:
    artifact = lock["artifacts"][name]
    destination = managed_path(work_dir, "tools", name)
    marker = managed_path(work_dir, "tools", name, "lucid-install.json")
    identity = {key: artifact[key] for key in ("version", "sha256", "sha512") if key in artifact}
    entry = managed_path(work_dir, "tools", name, artifact["entrypoint"])
    if marker.is_file() and entry.is_file():
        if json.loads(marker.read_text(encoding="utf-8")) == identity:
            return destination
    if destination.exists():
        raise ToolError(f"Unrecognized or incomplete cached tool directory: {destination}; move it aside and retry")
    archive = download_artifact(artifact, work_dir)
    stage = managed_path(work_dir, "tools", f".install-{name}-{uuid.uuid4().hex}")
    unpack_archive(archive, stage, artifact)
    try:
        if not (stage / artifact["entrypoint"]).is_file():
            raise ToolError(f"Pinned {name} archive did not contain its expected entrypoint")
        if name in ("assetripper", "dumper"):
            license_file = download_artifact(lock["artifacts"][f"{name}-license"], work_dir)
            shutil.copyfile(license_file, stage / "LICENSE.txt")
        if name == "dumper":
            configuration = json.loads((stage / "config.json").read_text(encoding="utf-8"))
            configuration["RequireAnyKey"] = False
            write_json(stage / "config.json", configuration)
        write_json(stage / "lucid-install.json", identity)
        stage.replace(destination)
    finally:
        if stage.exists():
            shutil.rmtree(stage)
    return destination


def _ensure_cpp2il(lock: dict, work_dir: Path) -> dict:
    dotnet_root = _install("dotnet", lock, work_dir)
    source = _install("cpp2il-source", lock, work_dir)
    output = managed_path(work_dir, "tools", "cpp2il")
    marker = managed_path(work_dir, "tools", "cpp2il", "lucid-build.json")
    identity = {"commit": lock["artifacts"]["cpp2il-source"]["version"],
                "sdk": lock["artifacts"]["dotnet"]["version"], "runtime": "osx-arm64",
                "build_schema": 2}
    executable = managed_path(work_dir, "tools", "cpp2il", "Cpp2IL")
    if marker.is_file() and executable.is_file() and json.loads(marker.read_text()) == identity:
        return {"status": "ready", "path": str(executable), "version": identity["commit"]}
    replacing_owned_build = False
    if output.exists():
        previous = json.loads(marker.read_text()) if marker.is_file() else {}
        replacing_owned_build = (previous.get("commit") == identity["commit"] and
                                 previous.get("sdk") == identity["sdk"])
        if not replacing_owned_build:
            raise ToolError(f"Incomplete cached Cpp2IL build: {output}; move it aside and retry")
    stage = managed_path(work_dir, "tools", f".build-cpp2il-{uuid.uuid4().hex}")
    try:
        process = run_logged([
            str(dotnet_root / "dotnet"), "publish", str(source / "Cpp2IL" / "Cpp2IL.csproj"),
            "-c", "Release", "-f", "net10.0", "-r", "osx-arm64", "--self-contained", "true",
            "-p:PublishSingleFile=true", "-p:PublishTrimmed=false", "-p:RestorePackagesWithLockFile=true",
            "-p:SourceRevisionId=" + identity["commit"],
            "-o", str(stage),
        ], work_dir, "cpp2il-build.log", timeout=1200,
            env=dotnet_environment(work_dir, dotnet_root), cwd=source)
        if process["returncode"] or process["timed_out"] or not (stage / "Cpp2IL").is_file():
            raise ToolError(f"Pinned Cpp2IL build failed; inspect {process['log']}")
        for filename in ("LICENSE", "nuget.config"):
            if (source / filename).is_file():
                shutil.copyfile(source / filename, stage / filename)
        write_json(stage / "lucid-build.json", identity)
        backup = None
        if replacing_owned_build:
            backup = managed_path(work_dir, "tools", f".superseded-cpp2il-{uuid.uuid4().hex}")
            output.replace(backup)
        try:
            stage.replace(output)
        except Exception:
            if backup is not None:
                backup.replace(output)
            raise
        if backup is not None:
            shutil.rmtree(backup)
        return {"status": "ready", "path": str(executable), "version": identity["commit"], "build": process}
    finally:
        if stage.exists():
            shutil.rmtree(stage)


def bootstrap(work_dir: Path, selected=None) -> dict:
    """Install selected pinned tools. Defaults to AssetRipper and Cpp2IL."""
    work_dir = validate_work_dir(work_dir)
    lock = load_lock()
    selection = [selected] if isinstance(selected, str) else list(["assetripper", "cpp2il"] if selected is None else selected)
    if selection == ["all"]:
        selection = ["assetripper", "cpp2il", "dumper"]
    allowed = {"assetripper", "cpp2il", "dumper", "dotnet", "runtime6"}
    if not selection or any(name not in allowed for name in selection):
        raise ValueError(f"Selected tools must be one of {', '.join(sorted(allowed))}")
    host = f"{platform.system().lower()}-{platform.machine().lower()}"
    if host != lock["platform"]:
        raise ToolError(f"This lock currently supports {lock['platform']}; detected {host}")
    work_dir.mkdir(parents=True, exist_ok=True)
    tools = managed_path(work_dir, "tools")
    tools.mkdir(parents=True, exist_ok=True)
    result = {"status": "ready", "host": host, "tools": {}}
    for name in dict.fromkeys(selection):
        lock_path = managed_path(work_dir, "tools", f".{name}.lock")
        with lock_path.open("a") as stream:
            fcntl.flock(stream, fcntl.LOCK_EX)
            try:
                if name == "cpp2il":
                    item = _ensure_cpp2il(lock, work_dir)
                else:
                    if name == "dumper":
                        _install("runtime6", lock, work_dir)
                    destination = _install(name, lock, work_dir)
                    artifact = lock["artifacts"][name]
                    item = {"status": "ready", "path": str(destination / artifact["entrypoint"]),
                            "version": artifact["version"]}
                result["tools"][name] = item
            except (ToolError, OSError, ValueError) as exc:
                result["status"] = "failed"
                result["tools"][name] = {"status": "failed", "error": str(exc)}
    write_json(managed_path(work_dir, "reports", "bootstrap.json"), result)
    return result

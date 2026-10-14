"""Download and build pinned external recovery tools in an isolated cache."""

from __future__ import annotations

from contextlib import contextmanager
import errno
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
import tempfile
import time
import urllib.request
import uuid
import zipfile

try:
    import fcntl
except ImportError:  # Inspection/validation must still import on Windows.
    fcntl = None
try:
    import msvcrt
except ImportError:
    msvcrt = None


REPO_ROOT = Path(__file__).resolve().parents[2]
CACHE_ROOT = REPO_ROOT / ".cache" / "project-lucid"
LOCK_FILE = REPO_ROOT / "tools" / "tool-lock.json"
HOST_CACHE_ROOT: Path | None = None
DIAGNOSTICS_ROOT: Path | None = None
_BUILD_SEALS: dict[Path, tuple[dict, dict]] = {}
_INSTALL_SEALS: dict[Path, tuple[dict, dict]] = {}


class ToolError(RuntimeError):
    """A pinned tool could not be downloaded, installed, or executed."""


def validate_work_dir(work_dir: Path) -> Path:
    """Allow generated cache directories only; reject source paths and symlinks."""
    requested = Path(os.path.abspath(os.fspath(work_dir)))
    roots = [CACHE_ROOT] + [root for root in (HOST_CACHE_ROOT, DIAGNOSTICS_ROOT) if root is not None]
    if not any(requested == root or root in requested.parents for root in roots):
        raise ValueError(f"Work directory must be {CACHE_ROOT} or a child directory")
    current = Path(requested.anchor)
    for part in requested.parts[1:]:
        current /= part
        if current.is_symlink():
            raise ValueError(f"Work directory cannot contain a symbolic link: {current}")
        if current.exists() and not current.is_dir():
            raise ValueError(f"Work directory component is not a directory: {current}")
    return requested


@contextmanager
def isolated_host_cache(parent: Path, *, diagnostics: bool = False):
    """Reserve a fresh controller-owned tool tree outside either checkout."""
    global HOST_CACHE_ROOT, DIAGNOSTICS_ROOT
    if HOST_CACHE_ROOT is not None:
        raise ToolError("A host cache scope is already active")
    parent = Path(parent)
    if any(path.is_symlink() for path in (parent, *parent.parents)):
        raise ToolError("Host cache parent cannot contain a symbolic link")
    with tempfile.TemporaryDirectory(prefix="host-tools-", dir=parent) as directory:
        HOST_CACHE_ROOT = Path(directory)
        try:
            if diagnostics:
                DIAGNOSTICS_ROOT = parent / "private-native-diagnostics"
                validate_work_dir(DIAGNOSTICS_ROOT)
                DIAGNOSTICS_ROOT.mkdir(mode=0o700, exist_ok=False)
            yield HOST_CACHE_ROOT
        finally:
            for seals in (_BUILD_SEALS, _INSTALL_SEALS):
                for path in list(seals):
                    if HOST_CACHE_ROOT in path.parents:
                        del seals[path]
            HOST_CACHE_ROOT = None
            DIAGNOSTICS_ROOT = None


def tool_work_dir(work_dir: Path) -> Path:
    """Keep executable dependencies separate from contributor data outputs."""
    if HOST_CACHE_ROOT is None:
        return Path(work_dir)
    validate_work_dir(work_dir)
    return validate_work_dir(HOST_CACHE_ROOT)


def reader_build_paths(root: Path, work: Path, run: Path, name: str) -> tuple[Path, Path, Path]:
    """Select control projects and fresh host outputs when running the CI driver."""
    host_work = tool_work_dir(work)
    if HOST_CACHE_ROOT is None:
        return root / "tools" / name, work, run
    project = LOCK_FILE.parent / name
    build = managed_path(host_work, "readers", name, run.name)
    build.mkdir(parents=True, exist_ok=False)
    return project, host_work, build


def tree_seal(directory: Path) -> dict:
    """Seal the complete file/directory closure, rejecting links and special files."""
    if directory.is_symlink() or not directory.is_dir():
        raise ToolError(f"Tool tree must be a physical directory: {directory}")
    result = {}
    for path in sorted(directory.rglob("*")):
        info = path.lstat()
        relative = path.relative_to(directory).as_posix()
        if stat.S_ISDIR(info.st_mode):
            result[relative] = {"mode": stat.S_IMODE(info.st_mode), "directory": True}
        elif stat.S_ISREG(info.st_mode) and info.st_nlink == 1:
            result[relative] = {"mode": stat.S_IMODE(info.st_mode), "sha256": _digest(path, "sha256")}
        else:
            raise ToolError(f"Tool tree contains a link or special file: {path}")
    return result


def verify_built_tool(directory: Path, identity: dict) -> bool:
    """Public build markers cannot create the process-owned execution seal."""
    recorded = _BUILD_SEALS.get(directory)
    return recorded is not None and recorded == (identity, tree_seal(directory))


def seal_built_tool(directory: Path, identity: dict) -> None:
    _BUILD_SEALS[directory] = (dict(identity), tree_seal(directory))


def retain_failure(name: str, report: dict, logs: list[Path]) -> str | None:
    """Retain only known diagnostics privately; never upload native logs or inputs."""
    if DIAGNOSTICS_ROOT is None:
        return None
    if Path(name).name != name or not name.endswith(".json"):
        raise ToolError("Diagnostic report name must be one JSON filename")
    copied = []
    for index, log in enumerate(logs):
        log = Path(log)
        root = next((root for root in (HOST_CACHE_ROOT, CACHE_ROOT) if root is not None
                     and root in log.parents), None)
        if root is None:
            raise ToolError("Diagnostic log is outside this run's owned tool/data trees")
        checked = managed_path(root, *log.relative_to(root).parts)
        if checked.exists():
            info = checked.lstat()
            if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
                raise ToolError("Diagnostic log must be one regular owned file")
            destination = managed_path(DIAGNOSTICS_ROOT, f"{name[:-5]}-{index}.log")
            with checked.open("rb") as source, destination.open("xb") as output:
                shutil.copyfileobj(source, output)
            destination.chmod(0o600)
            copied.append(destination.name)
    destination = managed_path(DIAGNOSTICS_ROOT, name)
    write_json(destination, {**report, "retained_log_files": copied})
    destination.chmod(0o600)
    return str(destination)


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


def host_name() -> str:
    return f"{platform.system().lower()}-{platform.machine().lower()}"


def host_lock(lock: dict | None = None) -> dict:
    """Resolve the pinned artifacts for this host; other hosts override only native tools."""
    lock = load_lock() if lock is None else lock
    host = host_name()
    if host == lock["platform"]:
        return {"platform": host, "rid": lock["rid"], "artifacts": lock["artifacts"]}
    variant = lock.get("hosts", {}).get(host)
    if variant is None:
        supported = ", ".join([lock["platform"], *lock.get("hosts", {})])
        raise ToolError(f"This lock currently supports {supported}; detected {host}")
    return {"platform": host, "rid": variant["rid"], "artifacts": {**lock["artifacts"], **variant["artifacts"]}}


def _windows_host() -> bool:
    return os.name == "nt"


def executable(path: Path) -> Path:
    """Name a native executable as the host expects it."""
    return path.with_name(path.name + ".exe") if _windows_host() else path


# msvcrt.locking reports a region still held elsewhere with these errors;
# LK_LOCK raises EDEADLOCK after its ten one-second attempts.
_LOCK_CONTENTION = {getattr(errno, "EDEADLOCK", errno.EDEADLK), errno.EACCES}


@contextmanager
def exclusive_lock(stream):
    """Hold a process-wide lock on an open file, waiting as long as necessary."""
    if fcntl is not None:
        fcntl.flock(stream, fcntl.LOCK_EX)
        try:
            yield
        finally:
            fcntl.flock(stream, fcntl.LOCK_UN)
        return
    if msvcrt is None:
        raise ToolError("This host has no supported file lock")
    stream.seek(0)
    while True:
        try:
            msvcrt.locking(stream.fileno(), msvcrt.LK_LOCK, 1)
            break
        except OSError as exc:
            if exc.errno not in _LOCK_CONTENTION:
                raise
    try:
        yield
    finally:
        stream.seek(0)
        msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)


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
    location = validate_work_dir(cwd or tool_work_dir(work_dir))
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
    work_dir = tool_work_dir(work_dir)
    env = os.environ.copy()
    if HOST_CACHE_ROOT is not None:
        env = {key: value for key, value in env.items()
               if key in {"PATH", "LANG", "LC_ALL", "LD_LIBRARY_PATH"}}
        home = managed_path(work_dir, "host-home")
        home.mkdir(parents=True, exist_ok=True)
        env.update(HOME=str(home), USERPROFILE=str(home), XDG_CONFIG_HOME=str(home / ".config"))
    for key, path in [("DOTNET_CLI_HOME", managed_path(work_dir, "dotnet-home")),
                      ("NUGET_PACKAGES", managed_path(work_dir, "nuget-packages")),
                      ("DOTNET_BUNDLE_EXTRACT_BASE_DIR", managed_path(work_dir, "dotnet-bundles"))]:
        path.mkdir(parents=True, exist_ok=True)
        env[key] = str(path)
    if dotnet_root:
        env["DOTNET_ROOT"] = str(dotnet_root)
    env.update(DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1",
               DOTNET_NOLOGO="1", DOTNET_MULTILEVEL_LOOKUP="0")
    return env


def _install(name: str, lock: dict, work_dir: Path) -> Path:
    work_dir = tool_work_dir(work_dir)
    artifact = host_lock(lock)["artifacts"][name]
    destination = managed_path(work_dir, "tools", name)
    identity = {key: artifact[key] for key in ("version", "sha256", "sha512") if key in artifact}
    managed_path(work_dir, "tools", name, artifact["entrypoint"])
    recorded = _INSTALL_SEALS.get(destination)
    if recorded is not None and recorded[0] == identity:
        if recorded[1] != tree_seal(destination):
            raise ToolError(f"Cached {name} executable/dependency closure changed")
        return destination
    # Reconstruct the expected tree from the pinned archive. A marker stored
    # alongside untrusted files supplies no integrity or provenance evidence.
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
        if name == "dotnet":
            # MSBuild initializes this empty SDK directory on first publish.
            # Reserve it before sealing; subsequent files/links/children still
            # change the exact verified closure and are rejected.
            (stage / "metadata").mkdir(mode=0o755, exist_ok=True)
        write_json(stage / "lucid-install.json", identity)
        expected = tree_seal(stage)
        if destination.exists():
            if tree_seal(destination) != expected:
                raise ToolError(f"Cached {name} executable/dependency closure differs from the pinned archive")
        else:
            stage.replace(destination)
        _INSTALL_SEALS[destination] = (identity, expected)
    finally:
        if stage.exists():
            shutil.rmtree(stage)
    return destination


def verified_tool_path(work_dir: Path, name: str) -> Path:
    """Verify an installed tool's entire closure before selecting its executable."""
    lock = load_lock()
    directory = _install(name, lock, work_dir)
    return directory / host_lock(lock)["artifacts"][name]["entrypoint"]


def _ensure_cpp2il(lock: dict, work_dir: Path) -> dict:
    work_dir = tool_work_dir(work_dir)
    dotnet_root = _install("dotnet", lock, work_dir)
    source = _install("cpp2il-source", lock, work_dir)
    output = managed_path(work_dir, "tools", "cpp2il")
    marker = managed_path(work_dir, "tools", "cpp2il", "lucid-build.json")
    rid = host_lock(lock)["rid"]
    identity = {"commit": lock["artifacts"]["cpp2il-source"]["version"],
                "sdk": lock["artifacts"]["dotnet"]["version"], "runtime": rid,
                "build_schema": 2}
    program = executable(managed_path(work_dir, "tools", "cpp2il", "Cpp2IL"))
    if verify_built_tool(output, identity):
        return {"status": "ready", "path": str(program), "version": identity["commit"]}
    if output in _BUILD_SEALS:
        raise ToolError(f"Sealed Cpp2IL build changed during this process: {output}")
    if output.is_symlink() or (output.exists() and not output.is_dir()):
        raise ToolError(f"Cpp2IL output must be a physical directory: {output}")
    # A new process holds no seal for an earlier build, and its public marker proves
    # nothing; rebuild from pinned source and replace that tree before any use.
    stage = managed_path(work_dir, "tools", f".build-cpp2il-{uuid.uuid4().hex}")
    build_source = managed_path(work_dir, "tools", f".cpp2il-source-{uuid.uuid4().hex}")
    unverified = managed_path(work_dir, "tools", f".unverified-cpp2il-{uuid.uuid4().hex}")
    try:
        shutil.copytree(source, build_source)
        process = run_logged([
            str(executable(dotnet_root / "dotnet")), "publish", str(build_source / "Cpp2IL" / "Cpp2IL.csproj"),
            "-c", "Release", "-f", "net10.0", "-r", rid, "--self-contained", "true",
            "-p:PublishSingleFile=true", "-p:PublishTrimmed=false", "-p:RestorePackagesWithLockFile=true",
            "-p:SourceRevisionId=" + identity["commit"],
            "-o", str(stage),
        ], work_dir, "cpp2il-build.log", timeout=1200,
            env=dotnet_environment(work_dir, dotnet_root), cwd=build_source)
        if process["returncode"] or process["timed_out"] or not executable(stage / "Cpp2IL").is_file():
            raise ToolError(f"Pinned Cpp2IL build failed; inspect {process['log']}")
        for filename in ("LICENSE", "nuget.config"):
            if (source / filename).is_file():
                shutil.copyfile(source / filename, stage / filename)
        write_json(stage / "lucid-build.json", identity)
        if output.exists():
            output.replace(unverified)
        stage.replace(output)
        seal_built_tool(output, identity)
        return {"status": "ready", "path": str(program), "version": identity["commit"], "build": process}
    finally:
        for path in (stage, build_source, unverified):
            if path.exists():
                shutil.rmtree(path)


def bootstrap(work_dir: Path, selected=None) -> dict:
    """Install selected pinned tools. Defaults to AssetRipper and Cpp2IL."""
    work_dir = tool_work_dir(work_dir)
    lock = load_lock()
    selection = [selected] if isinstance(selected, str) else list(["assetripper", "cpp2il"] if selected is None else selected)
    if selection == ["all"]:
        selection = ["assetripper", "cpp2il", "dumper"]
    allowed = {"assetripper", "cpp2il", "dumper", "dotnet", "runtime6"}
    if not selection or any(name not in allowed for name in selection):
        raise ValueError(f"Selected tools must be one of {', '.join(sorted(allowed))}")
    pinned = host_lock(lock)
    host = pinned["platform"]
    work_dir.mkdir(parents=True, exist_ok=True)
    tools = managed_path(work_dir, "tools")
    tools.mkdir(parents=True, exist_ok=True)
    result = {"status": "ready", "host": host, "tools": {}}
    for name in dict.fromkeys(selection):
        lock_path = managed_path(work_dir, "tools", f".{name}.lock")
        with lock_path.open("a") as stream, exclusive_lock(stream):
            try:
                if name == "cpp2il":
                    item = _ensure_cpp2il(lock, work_dir)
                else:
                    if name == "dumper":
                        _install("runtime6", lock, work_dir)
                    destination = _install(name, lock, work_dir)
                    artifact = pinned["artifacts"][name]
                    item = {"status": "ready", "path": str(destination / artifact["entrypoint"]),
                            "version": artifact["version"]}
                result["tools"][name] = item
            except (ToolError, OSError, ValueError) as exc:
                result["status"] = "failed"
                result["tools"][name] = {"status": "failed", "error": str(exc)}
    write_json(managed_path(work_dir, "reports", "bootstrap.json"), result)
    return result

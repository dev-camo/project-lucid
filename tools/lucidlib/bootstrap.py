"""Download and build pinned external recovery tools in an isolated cache."""

from __future__ import annotations

from contextlib import contextmanager
import errno
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import signal
import stat
import subprocess
import tarfile
import tempfile
import threading
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
_BUILD_LEASES: dict[Path, object] = {}


class ToolError(RuntimeError):
    """A pinned tool could not be downloaded, installed, or executed."""


def _is_link(path: Path) -> bool:
    """True for symbolic links and for Windows junctions and other reparse points."""
    try:
        info = os.lstat(path)
    except FileNotFoundError:
        return False
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, "st_file_attributes", 0) & reparse)


def validate_work_dir(work_dir: Path) -> Path:
    """Allow generated cache directories only; reject source paths and symlinks."""
    requested = Path(os.path.abspath(os.fspath(work_dir)))
    roots = [CACHE_ROOT] + [root for root in (HOST_CACHE_ROOT, DIAGNOSTICS_ROOT) if root is not None]
    matched = [root for root in roots if requested == root or root in requested.parents]
    if not matched:
        raise ValueError(f"Work directory must be {CACHE_ROOT} or a child directory")
    current = Path(requested.anchor)
    for part in requested.parts[1:]:
        current /= part
        # Windows junctions and other reparse points are links too, on every ancestor.
        if _is_link(current):
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
    if any(_is_link(path) for path in (parent, *parent.parents)):
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
            for path in list(_BUILD_LEASES):
                if HOST_CACHE_ROOT in path.parents:
                    _release_lease(_BUILD_LEASES.pop(path))
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
    if _is_link(directory) or not directory.is_dir():
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
        if _is_link(current):
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
        unlock(stream)


def unlock(stream) -> None:
    """Release a lock taken by exclusive_lock or try_lock on this host."""
    if fcntl is not None:
        fcntl.flock(stream, fcntl.LOCK_UN)
    elif msvcrt is not None:
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


IMPLICIT_DIRECTORY_MODE = 0o755
INJECTED_FILE_MODE = 0o644


def _archive_directory_mode(mode: int) -> int:
    """Pinned directory mode from an archive entry, never group- or other-writable.

    Upstream archives (the cpp2il-source tarball uses 0775) must not loosen the tool
    tree's permissions; 0755 is also what the implicit parents get, so caches sealed
    before explicit directory modes were pinned still match.
    """
    return mode & 0o755


def _make_directory(target: Path, destination: Path, modes: dict, mode: int | None) -> None:
    """Create target and its missing parents; record the mode each is pinned to afterwards.

    Directory modes are applied after extraction (see unpack_archive), so a read-only
    pinned directory cannot block writing its children, and none depends on the umask.
    """
    for directory in (target, *target.parents):
        if directory == destination:
            break
        modes.setdefault(directory, IMPLICIT_DIRECTORY_MODE)
    target.mkdir(parents=True, exist_ok=True)
    if mode is not None:
        modes[target] = mode


def _remove_unpacked_tree(directory: Path) -> None:
    """Remove a staged tree whose pinned directory modes may forbid deleting their children.

    Each directory is made owner-accessible before it is listed, so no pinned mode can
    hide children from the walk; any failure propagates instead of being skipped.
    """
    if directory.is_symlink() or not directory.is_dir():
        directory.unlink()
        return
    directory.chmod(0o700)
    for child in directory.iterdir():
        _remove_unpacked_tree(child)
    directory.rmdir()


def unpack_archive(archive: Path, destination: Path, artifact: dict) -> None:
    """Unpack regular files only into a fresh directory; reject links and traversal.

    Every file and directory mode is set explicitly, never inherited from the umask,
    because tree_seal compares modes between installs made by different processes.
    """
    destination = validate_work_dir(destination)
    if destination.exists():
        raise ToolError("Archive destination must be a fresh directory")
    destination.mkdir(parents=True)
    strip = artifact.get("strip_components", 0)
    limit = artifact.get("max_unpacked_bytes", 1000000000)
    total = 0
    modes: dict[Path, int] = {}
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
                        _make_directory(target, destination, modes, _archive_directory_mode(member.mode))
                    else:
                        total += member.size
                        if total > limit:
                            raise ToolError("Archive exceeds the pinned unpacked size limit")
                        _make_directory(target.parent, destination, modes, None)
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
                        _make_directory(target, destination, modes, _archive_directory_mode(mode) or IMPLICIT_DIRECTORY_MODE)
                    else:
                        total += member.file_size
                        if total > limit:
                            raise ToolError("Archive exceeds the pinned unpacked size limit")
                        _make_directory(target.parent, destination, modes, None)
                        with source.open(member) as input_stream, target.open("xb") as output:
                            shutil.copyfileobj(input_stream, output)
                        target.chmod((mode & 0o777) or 0o644)
        else:
            raise ToolError("Unsupported archive format")
        # Deepest first, as tarfile.extractall does, so a restrictive parent mode
        # never blocks changing its children.
        for directory in sorted(modes, key=lambda path: len(path.parts), reverse=True):
            directory.chmod(modes[directory])
    except Exception:
        _remove_unpacked_tree(destination)
        raise


TERM_GRACE_SECONDS = 5
KILL_WAIT_SECONDS = 5
# On Windows a blocking Popen wait defers Ctrl-C until it returns, and on Python 3.9 an
# interruption during a Popen timeout can wait without bound for the child: its handler
# passes the already expired, negative remaining time to WaitForSingleObject, which reads
# it as INFINITE. Windows waits therefore poll and sleep in slices, since time.sleep is
# interruptible there, and never pass a timeout to Popen.
WINDOWS_WAIT_SLICE_SECONDS = 0.2


class _WindowsJob:
    """A Job Object that owns a tool's whole process tree on Windows (#458).

    The tool starts suspended and runs only once it belongs to the job, so every
    descendant it starts belongs to the job too. Terminating the job stops the
    tree; closing it kills whatever is left (KILL_ON_JOB_CLOSE), so the tree also
    dies with this process."""

    _CREATE_SUSPENDED = 0x00000004
    _KILL_ON_JOB_CLOSE = 0x00002000
    _BASIC_ACCOUNTING = 1
    _EXTENDED_LIMITS = 9

    def __init__(self):
        import ctypes
        from ctypes import wintypes

        class Basic(ctypes.Structure):
            _fields_ = [("PerProcessUserTimeLimit", ctypes.c_int64), ("PerJobUserTimeLimit", ctypes.c_int64),
                        ("LimitFlags", wintypes.DWORD), ("MinimumWorkingSetSize", ctypes.c_size_t),
                        ("MaximumWorkingSetSize", ctypes.c_size_t), ("ActiveProcessLimit", wintypes.DWORD),
                        ("Affinity", ctypes.c_size_t), ("PriorityClass", wintypes.DWORD),
                        ("SchedulingClass", wintypes.DWORD)]

        class Extended(ctypes.Structure):
            _fields_ = [("BasicLimitInformation", Basic), ("IoInfo", ctypes.c_uint64 * 6),
                        ("ProcessMemoryLimit", ctypes.c_size_t), ("JobMemoryLimit", ctypes.c_size_t),
                        ("PeakProcessMemoryUsed", ctypes.c_size_t), ("PeakJobMemoryUsed", ctypes.c_size_t)]

        class Accounting(ctypes.Structure):
            _fields_ = [("TotalUserTime", ctypes.c_int64), ("TotalKernelTime", ctypes.c_int64),
                        ("ThisPeriodTotalUserTime", ctypes.c_int64), ("ThisPeriodTotalKernelTime", ctypes.c_int64),
                        ("TotalPageFaultCount", wintypes.DWORD), ("TotalProcesses", wintypes.DWORD),
                        ("ActiveProcesses", wintypes.DWORD), ("TotalTerminatedProcesses", wintypes.DWORD)]

        self._ctypes = ctypes
        self._Accounting = Accounting
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        self._kernel32 = kernel32
        self._ntdll = ctypes.WinDLL("ntdll")
        kernel32.CreateJobObjectW.restype = wintypes.HANDLE
        kernel32.CreateJobObjectW.argtypes = [wintypes.LPVOID, wintypes.LPCWSTR]
        kernel32.SetInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int, wintypes.LPVOID, wintypes.DWORD]
        kernel32.QueryInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int, wintypes.LPVOID,
                                                       wintypes.DWORD, wintypes.LPDWORD]
        kernel32.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
        kernel32.TerminateJobObject.argtypes = [wintypes.HANDLE, wintypes.UINT]
        kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
        self._ntdll.NtResumeProcess.argtypes = [wintypes.HANDLE]
        self._ntdll.NtResumeProcess.restype = ctypes.c_long
        self._handle = kernel32.CreateJobObjectW(None, None)
        if not self._handle:
            raise ToolError(f"Could not create a job object: {ctypes.WinError(ctypes.get_last_error())}")
        limits = Extended()
        limits.BasicLimitInformation.LimitFlags = self._KILL_ON_JOB_CLOSE
        if not kernel32.SetInformationJobObject(self._handle, self._EXTENDED_LIMITS, ctypes.byref(limits),
                                                ctypes.sizeof(limits)):
            error = ctypes.WinError(ctypes.get_last_error())
            self.close()
            raise ToolError(f"Could not configure the job object: {error}")

    @classmethod
    def popen(cls, command, **options) -> subprocess.Popen:
        """Start the command suspended, place it in a new job, then let it run.

        Called by _popen_tree with interruptions held, so only a real error can stop the
        admission part way. Before assignment the process is suspended outside the job and
        has started nothing; once assigned, the whole job is stopped and waited for."""
        job = cls()
        try:
            options["creationflags"] = options.get("creationflags", 0) | cls._CREATE_SUSPENDED
            process = subprocess.Popen(command, **options)
        except BaseException:
            job.close()
            raise
        assigned = False
        try:
            job._assign(process)
            assigned = True
            job._resume(process)
        except BaseException as admission:
            with _interrupts_deferred():
                try:
                    if assigned:
                        job.stop(process)
                    else:
                        process.kill()
                        process.wait()
                except ToolError as stop_error:
                    raise stop_error from admission
                finally:
                    job.close()
            raise
        process._lucid_job = job
        return process

    def _assign(self, process: subprocess.Popen) -> None:
        ctypes = self._ctypes
        if not self._kernel32.AssignProcessToJobObject(self._handle, int(process._handle)):
            raise ToolError(f"Could not place process {process.pid} in its job: "
                            f"{ctypes.WinError(ctypes.get_last_error())}")

    def _resume(self, process: subprocess.Popen) -> None:
        status = self._ntdll.NtResumeProcess(int(process._handle))
        if status != 0:
            raise ToolError(f"Could not resume process {process.pid}: NTSTATUS {status & 0xFFFFFFFF:#010x}")

    def active_processes(self) -> int:
        ctypes = self._ctypes
        accounting = self._Accounting()
        if not self._kernel32.QueryInformationJobObject(self._handle, self._BASIC_ACCOUNTING,
                                                        ctypes.byref(accounting), ctypes.sizeof(accounting), None):
            raise ToolError(f"Could not query the job object: {ctypes.WinError(ctypes.get_last_error())}")
        return accounting.ActiveProcesses

    def stop(self, process: subprocess.Popen) -> None:
        """Terminate every process in the job and return only once all have exited."""
        if self.active_processes() and not self._kernel32.TerminateJobObject(self._handle, 1):
            ctypes = self._ctypes
            raise ToolError(f"Could not terminate the job of process {process.pid}: "
                            f"{ctypes.WinError(ctypes.get_last_error())}")
        deadline = time.monotonic() + KILL_WAIT_SECONDS
        while self.active_processes():
            if time.monotonic() >= deadline:
                process.poll()
                raise ToolError(f"Process tree {process.pid} was still running {KILL_WAIT_SECONDS} "
                                "seconds after its job was terminated")
            time.sleep(0.05)
        process.wait()

    def close(self) -> None:
        if self._handle:
            self._kernel32.CloseHandle(self._handle)
            self._handle = None


def _popen_tree(command: list[str], owner: list, **options) -> subprocess.Popen:
    """Popen that owns the command's whole tree: its own POSIX session, or a Windows job.

    Interruptions are held from creation until the process is in owner, the caller's
    list, so an interruption during the handoff reaches the caller's cleanup with the
    process already in its custody."""
    with _interrupts_held():
        if os.name == "nt":
            process = _WindowsJob.popen(command, **options)
        else:
            # The tree inherits each held work lock, so the lock outlives a killed owner until the tree stops.
            held = tuple(stream.fileno() for stream in _HELD_WORK_LOCKS)
            options["pass_fds"] = tuple(options.get("pass_fds", ())) + held
            process = subprocess.Popen(command, start_new_session=True, **options)
        owner.append(process)
    return process


def _stop_descendants(process: subprocess.Popen) -> None:
    """After a leader exits normally, stop descendants it left running. The membership
    query, the decision and the stop are one step: an interruption during it is held
    and delivered once the tree has stopped."""
    with _interrupts_held():
        job = getattr(process, "_lucid_job", None)
        if job is not None:
            if job.active_processes():
                job.stop(process)
        elif os.name == "posix" and group_has_live_members(process.pid):
            # Live descendants retain the group id after its leader is reaped.
            stop_process_group(process)


def _add_note(error: BaseException, note: str) -> None:
    """BaseException.add_note, which Python 3.9 and 3.10 lack: there the note is kept in
    the same __notes__ list, which 3.11 and later also display in tracebacks."""
    if hasattr(error, "add_note"):
        error.add_note(note)
    else:
        error.__notes__ = [*getattr(error, "__notes__", ()), note]


def _release_tree(process: subprocess.Popen, pending: BaseException | None = None) -> None:
    """The barrier before the caller cleans up, on every path: nothing of the tree is
    still running afterwards, the Windows job handle is closed, and the output reader has
    stopped. A reader still running after the bounded join fails the barrier; a stop
    failure stays primary and notes the reader's.

    pending is the exception the caller is already propagating. A failed barrier never
    replaces it: a pending ToolError already reports that the tree could not be confirmed
    stopped, and keeps its own cause with the barrier's failure and that failure's notes
    added as notes; any other pending exception becomes the cause of the barrier's
    ToolError."""
    job = getattr(process, "_lucid_job", None)
    with _interrupts_deferred():
        failure = None
        try:
            try:
                if job is not None:
                    if job.active_processes():
                        job.stop(process)
                elif process.poll() is None or (os.name == "posix" and group_has_live_members(process.pid)):
                    _stop_tool(process)
            except ToolError as error:
                failure = error
            except BaseException as error:
                failure = error
                raise
        finally:
            if job is not None:
                job.close()
            reader = getattr(process, "_lucid_reader", None)
            if reader is not None:
                # The tree has stopped or the job is closed, so its pipes reach EOF.
                reader.join(KILL_WAIT_SECONDS)
                if reader.is_alive():
                    reader_error = ToolError(f"The output reader for process {process.pid} was still "
                                             f"running {KILL_WAIT_SECONDS} seconds after teardown")
                    if failure is None:
                        failure = reader_error
                    else:
                        _add_note(failure, f"The output reader also failed to stop: {reader_error}")
        if failure is not None:
            if isinstance(pending, ToolError):
                _add_note(pending, f"Final teardown also failed: {failure}")
                for note in getattr(failure, "__notes__", ()):  # e.g. a reader that also failed to stop
                    _add_note(pending, note)
                return
            if pending is None:
                raise failure
            raise failure from pending


def _wait(process: subprocess.Popen, timeout: float) -> int:
    """process.wait(timeout); on Windows a poll between interruptible sleeps."""
    if os.name != "nt":
        return process.wait(timeout=timeout)
    deadline = time.monotonic() + timeout
    while process.poll() is None:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise subprocess.TimeoutExpired(process.args, timeout)
        time.sleep(min(WINDOWS_WAIT_SLICE_SECONDS, remaining))
    return process.returncode


def _communicate(process: subprocess.Popen, timeout: float):
    """process.communicate(timeout). On Windows an untimed communicate runs on a reader
    thread while this thread sleeps in interruptible slices; the reader stays with the
    process, and _release_tree joins it once the tree has stopped."""
    if os.name != "nt":
        return process.communicate(timeout=timeout)
    result = {}

    def collect():
        try:
            result["output"] = process.communicate()
        except BaseException as error:
            result["error"] = error

    reader = threading.Thread(target=collect, name=f"communicate-{process.pid}", daemon=True)
    with _interrupts_held():
        # Published only once started, and with no gap in between: _release_tree joins
        # exactly the readers that run.
        reader.start()
        process._lucid_reader = reader
    deadline = time.monotonic() + timeout
    while reader.is_alive():
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise subprocess.TimeoutExpired(process.args, timeout)
        time.sleep(min(WINDOWS_WAIT_SLICE_SECONDS, remaining))
    if "error" in result:
        raise result["error"]
    return result["output"]


def group_has_live_members(pgid: int) -> bool:
    """Report whether any process in the group is still running; zombies count as stopped."""
    try:
        os.killpg(pgid, 0)
    except (ProcessLookupError, PermissionError):
        # Darwin reports EPERM for a group holding only unreaped zombies; a member we
        # may not signal cannot be stopped either way.
        return False
    proc = Path("/proc")
    if not (proc / "self" / "stat").exists():
        return True
    # Linux signals zombies without error, so the unreaped leader alone would keep the
    # probe succeeding. Count only members that are not zombies.
    for entry in proc.iterdir():
        if not entry.name.isdigit():
            continue
        try:
            fields = (entry / "stat").read_text().rsplit(")", 1)[1].split()
        except (OSError, IndexError):
            continue
        if len(fields) > 2 and fields[2] == str(pgid) and fields[0] not in {"Z", "X"}:
            return True
    return False


def stop_process_group(process: subprocess.Popen) -> None:
    """SIGTERM a session leader's group, SIGKILL what outlives the grace, then reap the leader."""
    # The group id cannot be reused by an unrelated group while it is being signalled (#235): on a
    # timeout or interrupt the leader is still unreaped, and after a normal exit (the leader already
    # reaped) its live members keep the id reserved. The leader is a member of its own group, so the
    # group probe also reports its exit.
    pgid = process.pid
    try:
        os.killpg(pgid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    grace_deadline = time.monotonic() + TERM_GRACE_SECONDS
    while group_has_live_members(pgid) and time.monotonic() < grace_deadline:
        time.sleep(0.05)
    if group_has_live_members(pgid):
        try:
            os.killpg(pgid, signal.SIGKILL)
        except (ProcessLookupError, PermissionError):
            pass
        # Callers remove the group's working trees next, so return only once it has stopped.
        kill_deadline = time.monotonic() + KILL_WAIT_SECONDS
        while group_has_live_members(pgid):
            if time.monotonic() >= kill_deadline:
                process.poll()
                raise ToolError(f"Process group {pgid} was still running {KILL_WAIT_SECONDS} "
                                "seconds after SIGKILL")
            time.sleep(0.05)
    process.wait()


@contextmanager
def _interrupts_held():
    """Hold SIGINT while a block runs and deliver it afterwards: the block completes, and
    an interruption that arrived during it then goes to the previous handler (a
    KeyboardInterrupt under Python's default; nothing if it was ignored)."""
    held = []
    try:
        previous = signal.signal(signal.SIGINT, lambda signum, frame: held.append(signum))
    except ValueError:  # not the main thread: signals cannot be changed here
        yield
        return
    try:
        yield
    finally:
        signal.signal(signal.SIGINT, previous)
    if held and callable(previous):
        previous(signal.SIGINT, None)


@contextmanager
def _interrupts_deferred():
    """Ignore SIGINT while a tool is being stopped, so a repeated Ctrl-C cannot cut the stop short."""
    try:
        previous = signal.signal(signal.SIGINT, signal.SIG_IGN)
    except ValueError:  # not the main thread: signals cannot be changed here
        yield
        return
    try:
        yield
    finally:
        signal.signal(signal.SIGINT, previous)


def _stop_tool(process: subprocess.Popen) -> None:
    """Stop a run_logged child and return only once it has stopped."""
    with _interrupts_deferred():
        if os.name == "posix":
            stop_process_group(process)
            return
        job = getattr(process, "_lucid_job", None)
        if job is not None:
            # Windows has no SIGTERM to offer a grace period: TerminateProcess is the
            # only stop, so the whole job is terminated at once (#458).
            job.stop(process)
            return
        # A process started without a job: only the immediate process can be stopped.
        process.terminate()
        try:
            process.wait(timeout=TERM_GRACE_SECONDS)
        except subprocess.TimeoutExpired:
            process.kill()
            try:
                process.wait(timeout=KILL_WAIT_SECONDS)
            except subprocess.TimeoutExpired:
                raise ToolError(f"Process {process.pid} was still running {KILL_WAIT_SECONDS} "
                                "seconds after kill") from None


def run_owned(command: list[str], *, timeout: float, capture_output: bool = False,
              text: bool = False, **options) -> subprocess.CompletedProcess:
    """subprocess.run that owns the whole process tree (#461).

    The command runs as its own session leader. A timeout or interruption stops the
    group before the original error propagates, and descendants that outlive a
    finished leader are stopped too, so callers never release a lock or remove a
    tree while part of the command still runs. On Windows the tree is a Job Object
    (#458)."""
    if capture_output:
        options["stdout"] = options["stderr"] = subprocess.PIPE
    owner = []
    pending = None
    try:
        try:
            process = _popen_tree(command, owner, text=text, **options)
            stdout, stderr = _communicate(process, timeout)
        except BaseException as interruption:
            if owner:
                try:
                    _stop_tool(owner[0])
                except ToolError as stop_error:
                    raise stop_error from interruption
            raise
        _stop_descendants(process)
    except BaseException as error:
        pending = error
        raise
    finally:
        if owner:
            _release_tree(owner[0], pending)
    return subprocess.CompletedProcess(command, process.returncode, stdout, stderr)


def _open_log(path: Path):
    """Open a log for truncating write; a link, hardlink or special file is refused untouched."""
    try:
        binary = _open_lease(path, create=True, exclusive=False, label="Log")
    except ToolError:
        raise
    except OSError as exc:
        raise ToolError(f"Could not open log {path}: {exc}") from exc
    try:
        if hasattr(os, "fchmod"):  # a reused log keeps its old mode otherwise
            os.fchmod(binary.fileno(), 0o600)
        binary.truncate(0)
        return io.TextIOWrapper(binary, encoding="utf-8")
    except BaseException:
        binary.close()
        raise


def run_logged(command: list[str], work_dir: Path, log_name: str, *, timeout: int = 900,
               env: dict | None = None, cwd: Path | None = None) -> dict:
    """Bound a child process and its descendants; keep evidence in the cache."""
    log = managed_path(work_dir, "reports", log_name)
    log.parent.mkdir(parents=True, exist_ok=True)
    location = validate_work_dir(cwd or tool_work_dir(work_dir))
    location.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    with _open_log(log) as stream:
        stream.write(json.dumps({"command": command, "cwd": str(location)}) + "\n")
        stream.flush()
        owner = []
        timed_out = False
        pending = None
        try:
            try:
                process = _popen_tree(command, owner, cwd=location, env=env, stdin=subprocess.DEVNULL,
                                      stdout=stream, stderr=subprocess.STDOUT)
                returncode = _wait(process, timeout)
            except subprocess.TimeoutExpired:
                timed_out = True
                _stop_tool(process)
                returncode = process.returncode
                stream.write(f"\nRecovery wrapper stopped the process after {timeout} seconds.\n")
            except BaseException as interruption:
                # Ctrl-C or any other abort: callers remove working trees and release locks
                # next, so the tool must have stopped first. SIGINT is ignored while stopping,
                # so a repeated Ctrl-C does not abandon the group. The original exception
                # propagates, except that a ToolError replaces it (chained as its cause) when
                # the tool cannot be stopped, so callers do not treat the tree as stopped.
                if owner:
                    try:
                        _stop_tool(owner[0])
                        stream.write("\nRecovery wrapper stopped the process after an interruption.\n")
                    except ToolError as stop_error:
                        raise stop_error from interruption
                raise
            else:
                _stop_descendants(process)
        except BaseException as error:
            pending = error
            raise
        finally:
            if owner:
                _release_tree(owner[0], pending)
    return {"command": command, "log": str(log), "returncode": returncode,
            "timed_out": timed_out, "elapsed_seconds": round(time.monotonic() - started, 3)}


# Tool-invoked `dotnet build`/`publish` must not leave MSBuild node-reuse, Roslyn
# (VBCSCompiler) or Razor servers running from a temporary cache afterwards.
# Output is unchanged. Every build call site appends these arguments.
NO_BUILD_SERVER_ARGS = ["--disable-build-servers", "-nodeReuse:false", "-p:UseSharedCompilation=false",
                        "-p:UseRazorBuildServer=false"]


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
               DOTNET_NOLOGO="1", DOTNET_MULTILEVEL_LOOKUP="0",
               DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
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
            (stage / "LICENSE.txt").chmod(INJECTED_FILE_MODE)
        if name == "dumper":
            configuration = json.loads((stage / "config.json").read_text(encoding="utf-8"))
            configuration["RequireAnyKey"] = False
            pinned_mode = stat.S_IMODE((stage / "config.json").stat().st_mode)
            write_json(stage / "config.json", configuration)
            (stage / "config.json").chmod(pinned_mode)  # write_json creates the file under the process umask; restore the pinned archive mode
        if name == "dotnet":
            # MSBuild initializes this empty SDK directory on first publish.
            # Reserve it before sealing; subsequent files/links/children still
            # change the exact verified closure and are rejected.
            reserved = stage / "metadata"
            existed = reserved.is_dir()
            reserved.mkdir(exist_ok=True)
            if not existed:
                reserved.chmod(IMPLICIT_DIRECTORY_MODE)  # mkdir's mode argument is masked by the umask
        write_json(stage / "lucid-install.json", identity)
        (stage / "lucid-install.json").chmod(INJECTED_FILE_MODE)
        expected = tree_seal(stage)
        published = False
        if not destination.exists():
            try:
                stage.replace(destination)
                published = True
            except OSError:
                # Installers under different locks (dotnet is installed under both .dotnet.lock
                # and .cpp2il.lock) can publish the same pinned tree first (#456). Theirs is
                # accepted only if it seals identically below.
                if not destination.is_dir():
                    raise
        if not published and tree_seal(destination) != expected:
            raise ToolError(
                f"Cached {name} executable/dependency closure differs from the pinned archive; "
                f"if it was installed by an older version of this tool, delete {destination} to reinstall")
        _INSTALL_SEALS[destination] = (identity, expected)
    finally:
        if stage.exists():
            _remove_unpacked_tree(stage)
    return destination


def verified_install_seal(directory: Path) -> dict:
    """Return the closure this process verified from the pinned archive for an install."""
    recorded = _INSTALL_SEALS.get(directory)
    if recorded is None:
        raise ToolError(f"No pinned installation was verified in this process: {directory}")
    return recorded[1]


def verified_tool_path(work_dir: Path, name: str) -> Path:
    """Verify an installed tool's entire closure before selecting its executable."""
    lock = load_lock()
    directory = _install(name, lock, work_dir)
    return directory / host_lock(lock)["artifacts"][name]["entrypoint"]


def try_lock(stream) -> bool:
    """Take an exclusive lock without waiting; False means another owner holds it."""
    if fcntl is not None:
        try:
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
            return True
        except OSError as exc:
            if exc.errno in (errno.EAGAIN, errno.EWOULDBLOCK, errno.EACCES):
                return False
            raise
    if msvcrt is None:
        raise ToolError("This host has no supported file lock")
    stream.seek(0)
    try:
        msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        return True
    except OSError as exc:
        if exc.errno in _LOCK_CONTENTION:
            return False
        raise


def _open_lease(path: Path, *, create: bool, exclusive: bool = True, label: str = "Build lease"):
    """Open a lease file without following links or blocking on special files.

    create makes a missing file; exclusive (with create) also refuses an existing one.
    """
    if _is_link(path):
        raise ToolError(f"{label} must not be a link: {path}")
    flags = os.O_RDWR | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_NONBLOCK", 0)
    if create:
        flags |= os.O_CREAT | (os.O_EXCL if exclusive else 0)
    descriptor = os.open(path, flags, 0o600)
    try:
        info = os.fstat(descriptor)
        if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
            raise ToolError(f"{label} must be a regular file: {path}")
        return os.fdopen(descriptor, "r+b")
    except BaseException:
        os.close(descriptor)
        raise


def open_coordinator_lock(path: Path, label: str):
    """Open (creating if missing) a shared coordinator lock file with the lease safeguards.

    A link, special file or multiply linked file fails here at once with ToolError,
    before any lock wait, instead of blocking the caller on a FIFO or device.
    """
    try:
        return _open_lease(path, create=True, exclusive=False, label=label)
    except ToolError:
        raise
    except OSError as exc:
        raise ToolError(f"Could not open {label.lower()} {path}: {exc}") from exc


# Work locks this process holds. On POSIX every owned command tree inherits them; a Windows job kills the tree
# with its owner instead.
_HELD_WORK_LOCKS = []


def take_work_lock(path: Path, busy: str):
    """Take a process-held work lock; the OS releases it once its owner and the commands it runs have all exited,
    so a leftover file is not a live owner."""
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_dir() and not path.is_symlink():
        # The earlier mkdir lock left only an empty directory, which cannot show whether its owner, an
        # older version of this command, is still running; it stays busy until removed on purpose.
        raise ValueError(busy + "; a lock directory from an earlier tool version is at " + str(path) +
                         " (remove it if no older command is running)")
    try:
        stream = open_coordinator_lock(path, "Work lock")
    except ToolError as error:
        raise ValueError(str(error)) from error
    if not try_lock(stream):
        stream.close()
        raise ValueError(busy)
    _HELD_WORK_LOCKS.append(stream)
    return stream


def release_work_lock(stream) -> None:
    """Release a work lock; the file stays so every owner locks the same inode."""
    _HELD_WORK_LOCKS.remove(stream)
    try:
        unlock(stream)
    finally:
        stream.close()


def _release_lease(stream) -> None:
    try:
        unlock(stream)
    finally:
        stream.close()


def _remove_tree(path: Path, warnings: list) -> None:
    """Best-effort cleanup; its failure is reported and never changes the build outcome."""
    try:
        if _is_link(path):
            # A junction is removed as a directory entry; its target is never traversed.
            try:
                path.unlink()
            except IsADirectoryError:
                os.rmdir(path)
        elif path.is_file():
            path.unlink()
        elif path.exists():
            shutil.rmtree(path)
    except OSError as exc:
        warnings.append(f"Could not remove {path}: {exc}")


def _prune_leased_builds(builds: Path, stale: list[Path], warnings: list) -> None:
    """Remove builds whose owning process has exited, plus stale build temporaries.

    Callers hold the tool's exclusive build lock, so no other build is in progress.
    A build whose owner lock is still held by a live process is never touched.
    """
    for lease in sorted(builds.glob("*.lock")):
        directory = lease.with_suffix("")
        if directory in _BUILD_LEASES:
            continue
        try:
            stream = _open_lease(lease, create=False)
        except (OSError, ToolError) as exc:
            warnings.append(f"Skipped untrusted build lease {lease}: {exc}")
            continue
        if not try_lock(stream):
            stream.close()
            continue
        # The owner has exited. Close the lease before deleting anything (Windows cannot
        # delete an open file), and keep the lease until its build is really gone so a
        # later pass can retry a failed removal.
        _release_lease(stream)
        _BUILD_SEALS.pop(directory, None)
        _remove_tree(directory, warnings)
        if not directory.exists() and not _is_link(directory):
            _remove_tree(lease, warnings)
    for path in [*builds.glob(".build-*"), *stale]:
        _remove_tree(path, warnings)


def leased_build(work_dir: Path, name: str, label: str, identity: dict, build, *,
                 scratch: str | None = None) -> tuple[Path, object, list]:
    """Select this process's sealed build of a generated tool, or build a fresh leased one.

    Each process uses only its own immutable build, kept under a lease for the
    process lifetime, so another process never replaces an executable in use and
    neither a public marker nor an earlier process's tree is ever trusted. Callers
    hold the tool's exclusive build lock. build(stage, build_id, warnings) creates
    and fills the stage; scratch is a glob, relative to tools/, matching its own
    temporaries. Returns (directory, build result or None when reused, warnings).
    """
    tools = managed_path(work_dir, "tools")
    builds = managed_path(work_dir, "tools", f"{name}-builds")
    for directory in list(_BUILD_LEASES):
        if builds not in directory.parents:
            continue
        if not verify_built_tool(directory, identity):
            raise ToolError(f"Sealed {label} build changed during this process: {directory}")
        return directory, None, []
    if _is_link(builds) or (builds.exists() and not builds.is_dir()):
        raise ToolError(f"{label} builds must be a physical directory: {builds}")
    builds.mkdir(parents=True, exist_ok=True)
    # Recheck the whole chain from the cache root once it exists, before anything is pruned.
    managed_path(work_dir, "tools", f"{name}-builds")
    warnings: list = []
    _prune_leased_builds(builds, sorted(tools.glob(scratch)) if scratch else [], warnings)
    build_id = uuid.uuid4().hex
    directory = builds / build_id
    lease = _open_lease(builds / f"{build_id}.lock", create=True)
    if not try_lock(lease):
        lease.close()
        raise ToolError(f"Fresh {label} build lease is unexpectedly held: {directory}")
    stage = builds / f".build-{build_id}"
    published = False
    try:
        result = build(stage, build_id, warnings)
        # A fresh name: publication never displaces another build.
        stage.rename(directory)
        seal_built_tool(directory, identity)
        _BUILD_LEASES[directory] = lease
        published = True
    finally:
        if not published:
            _remove_tree(directory, warnings)
            _release_lease(lease)
            # Keep the lease while the directory survives so a later prune retries it.
            if not directory.exists() and not _is_link(directory):
                _remove_tree(builds / f"{build_id}.lock", warnings)
        _remove_tree(stage, warnings)
    return directory, result, warnings


def _ensure_cpp2il(lock: dict, work_dir: Path) -> dict:
    work_dir = tool_work_dir(work_dir)
    dotnet_root = _install("dotnet", lock, work_dir)
    source = _install("cpp2il-source", lock, work_dir)
    rid = host_lock(lock)["rid"]
    identity = {"commit": lock["artifacts"]["cpp2il-source"]["version"],
                "sdk": lock["artifacts"]["dotnet"]["version"], "runtime": rid,
                "build_schema": 2}
    tools = managed_path(work_dir, "tools")

    def build(stage: Path, build_id: str, warnings: list) -> dict:
        build_source = tools / f".cpp2il-source-{build_id}"
        try:
            shutil.copytree(source, build_source)
            process = run_logged([
                str(executable(dotnet_root / "dotnet")), "publish", str(build_source / "Cpp2IL" / "Cpp2IL.csproj"),
                "-c", "Release", "-f", "net10.0", "-r", rid, "--self-contained", "true",
                "-p:PublishSingleFile=true", "-p:PublishTrimmed=false", "-p:RestorePackagesWithLockFile=true",
                "-p:SourceRevisionId=" + identity["commit"],
                "-o", str(stage), *NO_BUILD_SERVER_ARGS,
            ], work_dir, "cpp2il-build.log", timeout=1200,
                env=dotnet_environment(work_dir, dotnet_root), cwd=build_source)
            if process["returncode"] or process["timed_out"] or not executable(stage / "Cpp2IL").is_file():
                raise ToolError(f"Pinned Cpp2IL build failed; inspect {process['log']}")
            for filename in ("LICENSE", "nuget.config"):
                if (source / filename).is_file():
                    shutil.copyfile(source / filename, stage / filename)
            write_json(stage / "lucid-build.json", identity)
            return process
        finally:
            _remove_tree(build_source, warnings)

    directory, process, warnings = leased_build(work_dir, "cpp2il", "Cpp2IL", identity, build,
                                                scratch=".cpp2il-source-*")
    result = {"status": "ready", "path": str(executable(directory / "Cpp2IL")), "version": identity["commit"]}
    if process is not None:
        result["build"] = process
    if warnings:
        result["cleanup_warnings"] = warnings
    return result


ASSETRIPPER_SOURCE_DIR = REPO_ROOT / "tools" / "assetripper"
_ASSETRIPPER_BUILD_SCHEMA = 1
_MANIFEST_LINE = re.compile(r"([0-9a-f]{64})  ((?!/)(?:[^/\\\0\n]+/)*[^/\\\0\n]+)")


def _source_manifest(manifest: Path) -> dict[str, str]:
    """Parse "sha256  relative/path" lines; anything malformed, unsorted or duplicated is refused."""
    result = {}
    for number, line in enumerate(manifest.read_text(encoding="utf-8").splitlines(), 1):
        match = _MANIFEST_LINE.fullmatch(line)
        if match is None or ".." in match[2].split("/") or match[2] in result:
            raise ToolError(f"Invalid source manifest line {number}: {manifest.name}")
        result[match[2]] = match[1]
    if not result or list(result) != sorted(result):
        raise ToolError(f"Empty or unsorted source manifest: {manifest.name}")
    return result


def verify_source_manifest(source: Path, manifest: Path, *, allow_extra: bool = False,
                           skip: frozenset = frozenset()) -> str:
    """Require the tree to be exactly the manifest's files, byte for byte.

    The installer's own lucid-install.json is not source. With allow_extra (after a
    build) new files are tolerated, but every listed file must still match; skip names
    files a build step replaces on purpose. Returns the manifest's sha256."""
    expected = _source_manifest(manifest)
    actual = {path: record["sha256"] for path, record in tree_seal(source).items() if "sha256" in record}
    actual.pop("lucid-install.json", None)
    for path, digest in expected.items():
        if path not in skip and actual.get(path) != digest:
            raise ToolError(f"AssetRipper source differs from the pinned manifest: {path}")
    if not allow_extra:
        extra = sorted(set(actual) - set(expected))
        if extra:
            raise ToolError(f"Additional unpinned AssetRipper source: {extra[0]}")
    return _digest(manifest, "sha256")


def _assetripper_locks(rid: str) -> tuple[dict[str, Path], str]:
    """The committed packages.lock.json files for this rid, keyed by project-relative path."""
    root = ASSETRIPPER_SOURCE_DIR / "locks" / rid
    if _is_link(root) or not root.is_dir():
        raise ToolError(f"No committed AssetRipper package locks for {rid}")
    locks = {}
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        if _is_link(path) or (path.is_file() and path.name != "packages.lock.json") or \
                not (path.is_file() or path.is_dir()):
            raise ToolError(f"Unexpected entry in the AssetRipper package locks: {relative}")
        if path.is_file():
            locks[relative] = path
    if not locks:
        raise ToolError(f"No committed AssetRipper package locks for {rid}")
    digest = hashlib.sha256(json.dumps({key: _digest(path, "sha256") for key, path in locks.items()},
                                       sort_keys=True).encode()).hexdigest()
    return locks, digest


def _ensure_assetripper_source(lock: dict, work_dir: Path) -> dict:
    """Build AssetRipper from the pinned source; the prebuilt "assetripper" artifact is unaffected."""
    work_dir = tool_work_dir(work_dir)
    dotnet_root = _install("dotnet", lock, work_dir)
    source = _install("assetripper-source", lock, work_dir)
    pinned = host_lock(lock)
    rid = pinned["rid"]
    artifact = pinned["artifacts"]["assetripper-source"]
    manifest = ASSETRIPPER_SOURCE_DIR / "source-manifest.txt"
    nuget_config = ASSETRIPPER_SOURCE_DIR / "nuget.config"
    locks, locks_digest = _assetripper_locks(rid)
    identity = {"commit": artifact["version"], "archive_sha256": artifact["sha256"],
                "source_manifest_sha256": verify_source_manifest(source, manifest),
                "lock_files_sha256": locks_digest, "nuget_config_sha256": _digest(nuget_config, "sha256"),
                "sdk": pinned["artifacts"]["dotnet"]["version"], "runtime": rid,
                "publish_aot": False, "build_schema": _ASSETRIPPER_BUILD_SCHEMA}
    tools = managed_path(work_dir, "tools")

    def build(stage: Path, build_id: str, warnings: list) -> dict:
        build_source = tools / f".assetripper-source-{build_id}"
        try:
            shutil.copytree(source, build_source)
            verify_source_manifest(build_source, manifest)
            # Replace the source's feed configuration and add the locks; both are committed here.
            shutil.copyfile(nuget_config, build_source / "nuget.config")
            for relative, path in locks.items():
                if not (build_source / PurePosixPath(relative).parent).is_dir():
                    raise ToolError(f"AssetRipper package lock has no project: {relative}")
                shutil.copyfile(path, build_source / relative)
            process = run_logged([
                str(executable(dotnet_root / "dotnet")), "publish", str(build_source / artifact["entrypoint"]),
                "-c", "Release", "-r", rid, "--self-contained", "true", "-p:PublishAot=false",
                "-p:RestorePackagesWithLockFile=true", "-p:RestoreLockedMode=true", "-p:NuGetAudit=false",
                "-p:RestoreConfigFile=" + str(build_source / "nuget.config"),
                "-p:SourceRevisionId=" + identity["commit"],
                "-o", str(stage), *NO_BUILD_SERVER_ARGS,
            ], work_dir, "assetripper-source-build.log", timeout=1800,
                env=dotnet_environment(work_dir, dotnet_root), cwd=build_source)
            if process["returncode"] or process["timed_out"] or not executable(stage / "AssetRipper.GUI.Free").is_file():
                raise ToolError(f"Pinned AssetRipper build failed; inspect {process['log']}")
            # The build may add its own outputs, but no pinned source file may have changed.
            verify_source_manifest(build_source, manifest, allow_extra=True, skip=frozenset({"nuget.config"}))
            shutil.copyfile(source / "LICENSE.md", stage / "LICENSE.txt")
            write_json(stage / "lucid-build.json", identity)
            return process
        finally:
            _remove_tree(build_source, warnings)

    directory, process, warnings = leased_build(work_dir, "assetripper-source", "AssetRipper source", identity,
                                                build, scratch=".assetripper-source-*")
    result = {"status": "ready", "path": str(executable(directory / "AssetRipper.GUI.Free")),
              "version": identity["commit"]}
    if process is not None:
        result["build"] = process
    if warnings:
        result["cleanup_warnings"] = warnings
    return result


def bootstrap(work_dir: Path, selected=None) -> dict:
    """Install selected pinned tools. Defaults to AssetRipper and Cpp2IL."""
    work_dir = tool_work_dir(work_dir)
    lock = load_lock()
    selection = [selected] if isinstance(selected, str) else list(["assetripper", "cpp2il"] if selected is None else selected)
    if selection == ["all"]:
        selection = ["assetripper", "cpp2il", "dumper"]
    allowed = {"assetripper", "assetripper-source", "cpp2il", "dumper", "dotnet"}
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
        with open_coordinator_lock(lock_path, "Bootstrap tool lock") as stream, exclusive_lock(stream):
            try:
                if name == "cpp2il":
                    item = _ensure_cpp2il(lock, work_dir)
                elif name == "assetripper-source":
                    item = _ensure_assetripper_source(lock, work_dir)
                else:
                    if name == "dumper":
                        _install("dotnet", lock, work_dir)  # the net6 dumper runs on the pinned .NET 10
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

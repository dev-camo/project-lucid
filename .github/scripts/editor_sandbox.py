"""Confine Editor code; the trusted native driver stays outside this boundary."""
import contextlib
import os
from pathlib import Path
import shutil
import signal
import stat
import subprocess
import time
import uuid


EDITOR = Path('/opt/unity/Editor/Unity')
LICENSE_CLIENT = EDITOR.parent / 'Data/Resources/Licensing/Client/Unity.Licensing.Client'
RUNTIME_PATHS = ('/usr', '/bin', '/sbin', '/lib', '/lib64', '/opt/unity/Editor',
                 '/opt/dotnet', '/opt/lucid/compat-jammy')
SYSTEM_FILES = ('/etc/ld.so.cache', '/etc/ld.so.conf', '/etc/passwd', '/etc/group',
                '/etc/nsswitch.conf', '/etc/localtime', '/etc/machine-id',
                '/etc/os-release', '/etc/lsb-release')


def licensing_sockets(pipe):
    primary = Path('/tmp') / (pipe + '.sock')
    if not primary.exists() or not stat.S_ISSOCK(primary.lstat().st_mode):
        raise ValueError('Unity licensing broker unavailable; refuse unsandboxed Editor execution')
    notification = Path('/tmp') / (pipe + '-notifications.sock')
    sockets = [primary]
    if notification.exists() and stat.S_ISSOCK(notification.lstat().st_mode):
        sockets.append(notification)
    return sockets


def cached_license_files():
    directory = Path.home() / '.config/unity3d/Unity/licenses'
    files = sorted(directory.glob('*.xml'))
    if any(path.is_symlink() for path in (directory, *directory.parents, *files)):
        raise ValueError('Symlinked cached licensing input rejected')
    return [str(path) for path in files if path.is_file()]


@contextlib.contextmanager
def licensing_broker(root, *, registries=()):
    # Normal installed licensing client, with no activation/login credentials or
    # flags. Its process, account files and private diagnostics stay on the host.
    # A unique pipe avoids depending on or disturbing another Editor's client.
    pipe = 'Unity-LicenseClient-lucid-job-' + uuid.uuid4().hex
    environment = {key: value for key, value in os.environ.items()
                   if key in {'PATH', 'HOME', 'LANG', 'LD_LIBRARY_PATH'}}
    sockets = []
    command = [str(LICENSE_CLIENT), '--namedPipe', pipe, '--disable-auto-shutdown']
    files = cached_license_files()
    if files:
        # The installed CLI's --license option loads existing files. Only this
        # trusted host process sees them; they are never mounted for the Editor.
        command.extend(['--license', *files])
    with (root / 'private-licensing-client.log').open('xb') as log:
        options = dict(stdin=subprocess.DEVNULL, stdout=log, stderr=log,
                       env=environment, close_fds=True)
        if registries:
            import process_registry
            process = process_registry.spawn_registered(
                command, [*registries, (root / 'broker-group.json', None)], **options)
        else:
            process = subprocess.Popen(command, start_new_session=True, **options)
        try:
            deadline = time.monotonic() + 15
            while not sockets:
                if process.poll() is not None or time.monotonic() >= deadline:
                    raise ValueError('Task licensing broker did not start; refuse unsandboxed Editor execution')
                try:
                    sockets = licensing_sockets(pipe)
                except ValueError:
                    time.sleep(0.1)
            yield pipe, sockets
        finally:
            if registries:
                import direct_progress
                direct_progress.stop_compiler_group(process, root / 'broker-group.json')
            else:
                # Stop only this task's group, including licensing-client children.
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                    process.wait(timeout=5)
                except ProcessLookupError:
                    pass
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait(timeout=5)
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            for path in (Path('/tmp') / (pipe + '.sock'), Path('/tmp') / (pipe + '-notifications.sock')):
                if path.exists() and stat.S_ISSOCK(path.lstat().st_mode):
                    path.unlink()


def prepare_home(source, *, package_seed=None):
    home = source / '.cache/project-lucid/editor-home'
    if home.exists() or home.is_symlink():
        raise ValueError('Fresh sandbox Editor home required')
    # Only public package cache data is copied, never runner/account/license
    # configuration. UPM gets a writable private cache without network access.
    cache = Path.home() / '.config/unity3d/cache' if package_seed is None else Path(package_seed)
    if cache.exists():
        if any(path.is_symlink() for path in (cache, *cache.parents)) or any(
                path.is_symlink() for path in cache.rglob('*')):
            raise ValueError('Symlinked shared package cache rejected')
        shutil.copytree(cache, home / '.config/unity3d/cache')
    else:
        home.mkdir(parents=True)
    return home


def sandbox_command(source, home, command, sockets):
    source = Path(source).resolve(strict=True)
    home = Path(home).resolve(strict=True)
    if source not in home.parents:
        raise ValueError('Sandbox home must belong to the current source checkout')
    writable = [source / name for name in ('Library', 'Temp', 'Logs', 'UserSettings')]
    writable.extend([source / '.cache/project-lucid/player-code', home])
    for path in writable:
        if any(part.is_symlink() for part in (path, *path.parents)):
            raise ValueError('Symlinked Editor output directory rejected')
        path.mkdir(parents=True, exist_ok=True)
    # Source and host-executed cached tools are read-only. Only Editor outputs
    # and its private home are writable, preventing a later trusted driver call
    # from executing a replaced cached tool or following a planted output link.
    args = ['/usr/bin/bwrap', '--unshare-all', '--unshare-user', '--disable-userns', '--die-with-parent',
            '--new-session', '--cap-drop', 'ALL', '--clearenv', '--proc', '/proc',
            '--dev', '/dev', '--tmpfs', '/tmp']
    for path in RUNTIME_PATHS + SYSTEM_FILES:
        if Path(path).exists():
            args.extend(['--ro-bind', path, path])
    # Unity rejects a read-only project directory before compiling. A private
    # tmpfs root lets it create root markers without changing the host checkout
    # or planting host-executed MSBuild configuration. Existing entries stay RO.
    args.extend(['--tmpfs', str(source), '--chdir', str(source)])
    for entry in sorted(source.iterdir()):
        if entry.is_symlink():
            # Reproduce links in the sandbox; never follow a PR-controlled link
            # on the host while constructing a bind mount.
            args.extend(['--symlink', os.readlink(entry), str(entry)])
        elif entry.is_file() or entry.is_dir():
            args.extend(['--ro-bind', str(entry), str(entry)])
        else:
            raise ValueError('Unexpected project-root entry type')
    for path in writable:
        args.extend(['--bind', str(path), str(path)])
    for path in sockets:
        if not stat.S_ISSOCK(path.lstat().st_mode):
            raise ValueError('Only existing licensing sockets may cross the sandbox')
        args.extend(['--ro-bind', str(path), str(path)])
    environment = {'PATH': '/usr/bin:/bin:/opt/dotnet', 'HOME': str(home),
                   'LANG': 'C.UTF-8', 'TMPDIR': '/tmp', 'DOTNET_ROOT': '/opt/dotnet',
                   'LD_LIBRARY_PATH': '/opt/lucid/compat-jammy/usr/lib/x86_64-linux-gnu'}
    for key, value in environment.items():
        args.extend(['--setenv', key, value])
    return args + ['--'] + list(command)


class EditorProcess:
    SubprocessError = subprocess.SubprocessError
    TimeoutExpired = subprocess.TimeoutExpired
    CalledProcessError = subprocess.CalledProcessError

    def __init__(self, source, home, sockets, pipe):
        self.source, self.home, self.sockets, self.pipe = source, home, sockets, pipe

    def run(self, command, **kwargs):
        if Path(command[0]) != EDITOR or set(kwargs) != {'timeout', 'stdout', 'stderr'}:
            raise ValueError('Only the pinned Editor invocation may use the sandbox launcher')
        # The Editor adds "Unity-" when resolving /tmp/Unity-%s.sock.
        command = [command[0], '-nographics', '-licensingIpc', self.pipe.removeprefix('Unity-'), *command[1:]]
        return subprocess.run(sandbox_command(self.source, self.home, command, self.sockets),
                              stdin=subprocess.DEVNULL, close_fds=True, env={}, **kwargs)

@contextlib.contextmanager
def isolated_editor(playercode, source, *, package_seed=None):
    with licensing_broker(source.parent) as (pipe, sockets):
        home = prepare_home(source, package_seed=package_seed)
        original = playercode.subprocess
        # Replace this module's facade, not subprocess.run globally: trusted
        # bootstrap, receipt verification and progress generation stay on host.
        playercode.subprocess = EditorProcess(source, home, sockets, pipe)
        try:
            yield
        finally:
            playercode.subprocess = original

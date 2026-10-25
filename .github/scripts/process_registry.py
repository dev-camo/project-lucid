"""Register new precheck sessions before exec; records never authorize signaling."""
import json
import contextlib
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time


@contextlib.contextmanager
def locked_registry(marker):
    import fcntl
    marker = Path(marker)
    lock_path = marker.with_name(marker.name + '.lock')
    if any(path.is_symlink() for path in (marker, lock_path, *marker.parents)):
        raise ValueError('Linked custody registry rejected')
    with lock_path.open('a+b') as lock:
        deadline = time.monotonic() + 5
        while True:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                if time.monotonic() >= deadline:
                    raise ValueError('Custody registry lock remained unavailable')
                time.sleep(.01)
        yield marker


def clear_stopped_registry(marker, require_stopped):
    # Sequential commands may reuse a marker only after every prior owner stopped.
    with locked_registry(marker) as marker:
        require_stopped(marker)
        if marker.exists():
            marker.unlink()


def merge_registry(marker, root, groups, *, create=False):
    if type(root) is not int or root <= 0 or any(type(group) is not int or group <= 0 for group in groups):
        raise ValueError('Invalid owned process group')
    with locked_registry(marker) as marker:
        if marker.exists():
            value = json.loads(marker.read_bytes())
            if not isinstance(value, dict) or value.get('group') != root or type(value.get('group')) is not int:
                raise ValueError('Custody registry belongs to another root')
            previous = value.get('groups', [root])
            if (not isinstance(previous, list) or root not in previous
                    or any(type(group) is not int or group <= 0 for group in previous)):
                raise ValueError('Malformed custody registry')
        elif create:
            previous = [root]
        else:
            raise ValueError('Existing job custody registry required')
        result = {'group': root, 'groups': sorted(set(previous) | set(groups) | {root})}
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode='w', dir=marker.parent, prefix=marker.name + '.', delete=False) as stream:
                temporary = Path(stream.name)
                json.dump(result, stream, sort_keys=True)
                stream.write('\n')
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary, marker)
        finally:
            if temporary is not None and temporary.exists():
                temporary.unlink()
        return result


def spawn_registered(command, markers, **options):
    """The parent alone can release the gate after all durable registrations."""
    if 'start_new_session' in options or 'pass_fds' in options:
        raise ValueError('Registered launcher owns session and gate descriptors')
    reader, writer = os.pipe()
    process = None
    try:
        gate = [sys.executable, '-I', str(Path(__file__).resolve()), str(reader), '--', *command]
        process = subprocess.Popen(gate, start_new_session=True, pass_fds=(reader,), **options)
        os.close(reader)
        reader = None
        for marker, root in markers:
            merge_registry(marker, process.pid if root is None else root, [process.pid], create=root is None)
        if os.write(writer, b'G') != 1:
            raise OSError('Gate release was incomplete')
        return process
    except BaseException:
        # EOF means no service exec. A failed registration never licenses a later release.
        os.close(writer)
        writer = None
        if process is not None:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
        raise
    finally:
        if reader is not None:
            os.close(reader)
        if writer is not None:
            os.close(writer)


def gate_main(arguments):
    if len(arguments) < 3 or arguments[1] != '--':
        return 71
    descriptor = int(arguments[0])
    try:
        release = b''
        while len(release) < 2:
            block = os.read(descriptor, 2 - len(release))
            if not block:
                break
            release += block
    finally:
        os.close(descriptor)
    if release != b'G':
        return 71
    os.execvpe(arguments[2], arguments[2:], os.environ)


if __name__ == '__main__':
    raise SystemExit(gate_main(sys.argv[1:]))

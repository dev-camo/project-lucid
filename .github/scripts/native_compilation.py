"""Trusted-source Linux driver for a native VM or a prepared container.

No activation, credentials, runner registration or publication is performed.
The Editor runs headlessly inside the namespace sandbox. Controllers accept
reviewed main or an admitted PR workflow; compilation-only PRs receive no
original game inputs and generate no progress files.
"""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys

from editor_sandbox import isolated_editor


def git_head(root):
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()


def reporting_files(root, *, allow_missing_fixed=False):
    root = Path(root).resolve(strict=True)
    tools = root / 'tools'
    if any(path.is_symlink() for path in (tools, *tools.parents)):
        raise ValueError('Reporting code must be regular files')
    paths = []
    for parent, directories, files in os.walk(tools, followlinks=False):
        for name in [*directories, *files]:
            if (Path(parent) / name).is_symlink():
                raise ValueError('Reporting code must be regular files')
        directories[:] = [name for name in directories if name != '__pycache__']
        paths.extend(Path(parent) / name for name in files if not name.endswith('.pyc'))
    paths.extend(root / 'Assets/Editor' / name for name in
                 ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'))
    result = {}
    for path in paths:
        if any(part.is_symlink() for part in (path, *path.parents)):
            raise ValueError('Reporting code must be regular files')
        # Only the overlay may see a PR that deleted a fixed Editor reporting file.
        if allow_missing_fixed and path.parent == root / 'Assets/Editor' and (not path.exists() or path.is_dir()):
            continue
        if not path.is_file():
            raise ValueError('Reporting code must be regular files')
        result[str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


def require_control_reporting_tools(source, control):
    if reporting_files(source) != reporting_files(control):
        raise ValueError('Source reporting tools differ from trusted control; review/control update required')


FIXED_REPORTING_FILES = ('Assets/Editor/LucidPlayerCompilation.cs', 'Assets/Editor/LucidCleanPlayerCompilation.cs')


def _in_reporting_area(relative):
    return relative == 'tools' or relative.startswith('tools/') or relative in FIXED_REPORTING_FILES


def _clear_reporting_obstructions(source, relative):
    """Remove PR entries that block writing a trusted reporting file at relative."""
    parts = PurePosixPath(relative).parts
    for depth in range(1, len(parts) + 1):
        partial = '/'.join(parts[:depth])
        entry = source.joinpath(*parts[:depth])
        if entry.is_symlink():
            raise ValueError('Reporting code must be regular files')
        last = depth == len(parts)
        obstructs = entry.is_dir() if last else (entry.exists() and not entry.is_dir())
        if not obstructs:
            continue
        if not _in_reporting_area(partial):
            raise ValueError('Reporting path is obstructed outside the reporting area')
        if entry.is_dir():
            shutil.rmtree(entry)
        else:
            entry.unlink()


def _prune_empty_directories(directory):
    if directory.is_symlink() or not directory.is_dir():
        return
    for parent, directories, files in os.walk(directory, topdown=False):
        path = Path(parent)
        if path != directory and not path.is_symlink() and not any(path.iterdir()):
            path.rmdir()


def overlay_control_reporting_tools(source, control):
    """Replace a disposable PR checkout's reporting code with the trusted control's.

    PR compilation then builds the PR's game code without executing or measuring
    any PR reporting code. Returns the sorted replaced paths; a PR's reporting
    changes still need review and the post-merge main run, which uses them.
    """
    source, control = Path(source).resolve(strict=True), Path(control).resolve(strict=True)
    current = reporting_files(source, allow_missing_fixed=True)
    trusted = reporting_files(control)
    changed = sorted(path for path in set(current) | set(trusted) if current.get(path) != trusted.get(path))
    # Delete PR-only reporting files first, then directories they leave empty, so a
    # file/directory transition never blocks restoring a trusted path.
    for path in changed:
        if path not in trusted:
            (source / path).unlink()
    _prune_empty_directories(source / 'tools')
    for path in changed:
        if path in trusted:
            _clear_reporting_obstructions(source, path)
            target = source / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((control / path).read_bytes())
    require_control_reporting_tools(source, control)
    return changed


def require_execution_identity(image, engine):
    editor_hash = next(row['sha256'] for row in engine if row['name'] == 'editor')
    if image.startswith('native@'):
        if image != 'native@sha256:' + editor_hash:
            raise ValueError('Native execution identity differs from installed Editor')
    elif not re.fullmatch(r'ghcr.io/[a-z0-9_./-]+@sha256:[0-9a-f]{64}', image):
        raise ValueError('Immutable execution identity required')


def require_phase_success(name, result):
    expected = {'bootstrap': 'ready', 'player-code': 'compiled', 'progress': 'complete'}
    if result.get('status') != expected[name]:
        raise ValueError(name + ' failed; private diagnostics retained')


COMPILER_LINE = re.compile(r'error\s+CS\d+|\berror\b.*\.cs\(\d+', re.IGNORECASE)
LOG_TAIL_BYTES = 8 * 1024 * 1024
MAX_DIAGNOSTIC_LINES = 200
MAX_DIAGNOSTIC_LINE = 400


def compiler_error_excerpt(log):
    """Bounded compiler error lines from a Unity editor log (tail only, long lines cut)."""
    log = Path(log)
    with log.open('rb') as stream:
        stream.seek(0, os.SEEK_END)
        stream.seek(max(0, stream.tell() - LOG_TAIL_BYTES))
        text = stream.read().decode('utf-8', 'replace')
    lines, seen = [], set()
    for line in text.splitlines():
        line = line.strip()
        if COMPILER_LINE.search(line) and line not in seen:
            seen.add(line)
            lines.append(line[:MAX_DIAGNOSTIC_LINE])
            if len(lines) >= MAX_DIAGNOSTIC_LINES:
                break
    return lines


def retain_player_code_failure(bootstrap_module, work, result):
    """Keep compiler diagnostics only in the runner's private diagnostics directory.

    Retains the result JSON plus the compiler error lines of the player-code editor log.
    Nothing is printed, so the public Actions log stays free of private paths and logs.
    """
    excerpt = work / 'player-code-compiler-errors.log'
    lines = []
    log = result.get('log')
    if isinstance(log, str):
        log = Path(log)
        try:
            if work in log.parents and log.is_file() and not log.is_symlink():
                lines = compiler_error_excerpt(log)
        except OSError:
            lines = []
    excerpt.write_text('\n'.join(lines) + ('\n' if lines else ''), encoding='utf-8')
    report = {'phase': 'player-code', 'status': str(result.get('status')),
              'exit_code': result.get('exit_code'), 'errors': [str(e)[:MAX_DIAGNOSTIC_LINE] for e in result.get('errors', [])[:10]],
              'compiler_error_line_count': len(lines)}
    bootstrap_module.retain_failure('driver-player-code.json', report, [excerpt])


def require_approved_original(report, approved):
    for key in ('input_binary_sha256', 'input_metadata_sha256'):
        if report.get(key) != approved.get(key) or not re.fullmatch('[0-9a-f]{64}', approved.get(key, '')):
            raise ValueError('Original code inputs differ from trusted control approval')


@contextlib.contextmanager
def source_working_directory(source):
    """Resolve relative Unity PDB documents against the compiled checkout."""
    previous = Path.cwd()
    os.chdir(source)
    try:
        yield
    finally:
        os.chdir(previous)


@contextlib.contextmanager
def project_cache_scope(bootstrap_module, source, work):
    """Reserve fresh data outputs and separate tools from contributor checkout."""
    original = bootstrap_module.CACHE_ROOT
    bootstrap_module.CACHE_ROOT = source / '.cache/project-lucid'
    try:
        work = bootstrap_module.validate_work_dir(work)
        if work != bootstrap_module.CACHE_ROOT:
            raise ValueError('Driver requires the exact project data cache')
        if work.exists():
            raise ValueError('Fresh project data cache required; contributor cache is not trusted')
        work.mkdir(parents=True, mode=0o700, exist_ok=False)
        with bootstrap_module.isolated_host_cache(source.parent, diagnostics=True):
            yield
    finally:
        bootstrap_module.CACHE_ROOT = original


def run(source, control, input_path, work, expected_head, expected_source, image,
        control_commit, run_id, *, compile_only=False):
    source = source.resolve(strict=True)
    control = control.resolve(strict=True)
    if compile_only and input_path is not None:
        raise ValueError('Compilation-only mode cannot receive original game inputs')
    if not compile_only:
        if input_path is None:
            raise ValueError('Progress generation requires original game inputs')
        input_path = input_path.resolve(strict=True)
    work = Path(os.path.abspath(work))
    require_control_reporting_tools(source, control)
    approved = None if compile_only else json.loads((control / 'docs/code-progress.json').read_bytes())
    sys.path.insert(0, str(control / 'tools'))
    from lucidlib import bootstrap as bootstrap_module
    from lucidlib.bootstrap import bootstrap, host_name
    from lucidlib.verification import artifact_fingerprint, find_editor
    from lucidlib import playercode
    from lucidlib.playercode import _engine_identity
    from lucidlib.playerschema import _player_evidence
    from lucidlib.progressreport import generate_progress, check_progress
    if host_name() != 'linux-x86_64':
        raise ValueError('Driver requires Linux x86_64')
    if git_head(control) != control_commit:
        raise ValueError('Control checkout differs from approved producer commit')
    require_execution_identity(image, _engine_identity(find_editor()))

    def unchanged():
        if git_head(source) != expected_head or artifact_fingerprint(source) != expected_source:
            raise ValueError('Source head or fingerprint changed')
        require_control_reporting_tools(source, control)

    unchanged()
    def compile_player():
        with isolated_editor(playercode, source):
            return playercode.run_player_code(source, work, 'linux')

    with project_cache_scope(bootstrap_module, source, work), source_working_directory(source):
        if compile_only:
            unchanged()
            result = compile_player()
            try:
                require_phase_success('player-code', result)
            except ValueError:
                retain_player_code_failure(bootstrap_module, work, result)
                raise
            unchanged()
            receipt = _player_evidence(source, work, 'linux', expected_source,
                                       _engine_identity(find_editor()))
            unchanged()
            return {'head': expected_head, 'image': image, 'control_commit': control_commit,
                    'run_id': str(run_id), 'source_fingerprint': expected_source,
                    'compiler_receipt_sha256': receipt['receipt_sha256']}
        logs = work / 'private-driver-logs'
        logs.mkdir(parents=True, exist_ok=True)
        phases = [('bootstrap', lambda: bootstrap(work, selected=['cpp2il'])),
                  ('player-code', compile_player),
                  ('progress', lambda: generate_progress(source, work, input_path, 'linux'))]
        for name, phase in phases:
            unchanged()
            path = logs / (name + '.log')
            try:
                with path.open('w') as log:
                    with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                        result = phase()
                        log.write(json.dumps(result, sort_keys=True) + '\n')
                require_phase_success(name, result)
            except Exception as error:
                bootstrap_module.retain_failure('driver-' + name + '.json',
                                               {'phase': name, 'error_type': type(error).__name__}, [path])
                raise
        unchanged()
        check_progress(source)
        raw = (source / 'docs/code-progress.json').read_bytes()
        report = json.loads(raw)
        require_approved_original(report, approved)
        # Revalidate actual sealed DLL/PDB/compiler evidence, not a self-reported hash.
        receipt = _player_evidence(source, work, 'linux', expected_source,
                                   _engine_identity(find_editor()))
        measurement = json.loads((work / 'code-progress/latest-linux.json').read_bytes())
        if (measurement.get('status') != 'ready' or measurement['public'] != report
                or receipt['receipt_sha256'] != report['compiler_receipt_sha256']):
            raise ValueError('Generated report differs from genuine compiler measurement')
        unchanged()
        return {'head': expected_head, 'image': image, 'control_commit': control_commit,
                'run_id': str(run_id), 'source_fingerprint': expected_source,
                'report_sha256': hashlib.sha256(raw).hexdigest(),
                'compiler_receipt_sha256': receipt['receipt_sha256']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--compile-only', action='store_true')
    parser.add_argument('--input', type=Path)
    for name in ('source', 'control', 'work-dir'):
        parser.add_argument('--' + name, type=Path, required=True)
    for name in ('expected-head', 'expected-source', 'image', 'control-commit', 'run-id'):
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    if not all(re.fullmatch('[0-9a-f]{40}', value) for value in
               (args.expected_head, args.control_commit)) or not re.fullmatch('[0-9a-f]{64}', args.expected_source):
        raise ValueError('Invalid source/control identity')
    record = run(args.source, args.control, args.input, args.work_dir,
                 args.expected_head, args.expected_source, args.image,
                 args.control_commit, args.run_id, compile_only=args.compile_only)
    print(json.dumps(record, sort_keys=True))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)

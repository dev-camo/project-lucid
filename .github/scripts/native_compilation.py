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
from pathlib import Path
import re
import subprocess
import sys

from editor_sandbox import isolated_editor


def git_head(root):
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()


def reporting_files(root):
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
        if any(part.is_symlink() for part in (path, *path.parents)) or not path.is_file():
            raise ValueError('Reporting code must be regular files')
        result[str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


def require_control_reporting_tools(source, control):
    if reporting_files(source) != reporting_files(control):
        raise ValueError('Source reporting tools differ from trusted control; review/control update required')


def overlay_control_reporting_tools(source, control):
    """Replace a disposable PR checkout's reporting code with the trusted control's.

    PR compilation then builds the PR's game code without executing or measuring
    any PR reporting code. Returns the sorted replaced paths; a PR's reporting
    changes still need review and the post-merge main run, which uses them.
    """
    source, control = Path(source).resolve(strict=True), Path(control).resolve(strict=True)
    current, trusted = reporting_files(source), reporting_files(control)
    changed = sorted(path for path in set(current) | set(trusted) if current.get(path) != trusted.get(path))
    for path in changed:
        target = source / path
        if any(part.is_symlink() for part in (target, *target.parents)):
            raise ValueError('Reporting code must be regular files')
        if path in trusted:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((control / path).read_bytes())
        else:
            target.unlink()
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
            require_phase_success('player-code', result)
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

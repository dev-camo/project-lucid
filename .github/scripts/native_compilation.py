"""Trusted-source Linux driver for a native VM or a prepared container.

No activation, credentials, runner registration or publication is performed.
Run with xvfb-run -a if no display server is available. The calling controller
requires a completed PR workflow admitted by GitHub's contributor approval policy.
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


def git_head(root):
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()


def reporting_files(root):
    paths = [p for p in (root / 'tools').rglob('*') if p.is_file()
             and '__pycache__' not in p.parts and p.suffix != '.pyc']
    paths.extend(root / 'Assets/Editor' / name for name in
                 ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'))
    result = {}
    for path in paths:
        if path.is_symlink():
            raise ValueError('Reporting code must be regular files')
        result[str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


def require_control_reporting_tools(source, control):
    if reporting_files(source) != reporting_files(control):
        raise ValueError('Source reporting tools differ from trusted control; review/control update required')


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
    """Keep existing cache/symlink guards bound to the compiled project.

    Implementations and the pinned download lock still come from control. Only
    the owned output root follows source, matching the Editor's context checks.
    """
    original = bootstrap_module.REPO_ROOT, bootstrap_module.CACHE_ROOT
    bootstrap_module.REPO_ROOT = source
    bootstrap_module.CACHE_ROOT = source / '.cache/project-lucid'
    try:
        bootstrap_module.validate_work_dir(work)
        yield
    finally:
        bootstrap_module.REPO_ROOT, bootstrap_module.CACHE_ROOT = original


def run(source, control, input_path, work, expected_head, expected_source, image,
        control_commit, run_id):
    source = source.resolve(strict=True)
    control = control.resolve(strict=True)
    input_path = input_path.resolve(strict=True)
    work = work.resolve()
    require_control_reporting_tools(source, control)
    approved = json.loads((control / 'docs/code-progress.json').read_bytes())
    sys.path.insert(0, str(control / 'tools'))
    from lucidlib import bootstrap as bootstrap_module
    from lucidlib.bootstrap import bootstrap, host_name
    from lucidlib.verification import artifact_fingerprint, find_editor
    from lucidlib.playercode import run_player_code, _engine_identity
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
    with project_cache_scope(bootstrap_module, source, work), source_working_directory(source):
        logs = work / 'private-driver-logs'
        logs.mkdir(parents=True, exist_ok=True)
        phases = [('bootstrap', lambda: bootstrap(work, selected=['cpp2il'])),
                  ('player-code', lambda: run_player_code(source, work, 'linux')),
                  ('progress', lambda: generate_progress(source, work, input_path, 'linux'))]
        for name, phase in phases:
            unchanged()
            with (logs / (name + '.log')).open('w') as log:
                with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                    result = phase()
                    log.write(json.dumps(result, sort_keys=True) + '\n')
            require_phase_success(name, result)
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
    for name in ('source', 'control', 'input', 'work-dir'):
        parser.add_argument('--' + name, type=Path, required=True)
    for name in ('expected-head', 'expected-source', 'image', 'control-commit', 'run-id'):
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    if not all(re.fullmatch('[0-9a-f]{40}', value) for value in
               (args.expected_head, args.control_commit)) or not re.fullmatch('[0-9a-f]{64}', args.expected_source):
        raise ValueError('Invalid source/control identity')
    record = run(args.source, args.control, args.input, args.work_dir,
                 args.expected_head, args.expected_source, args.image,
                 args.control_commit, args.run_id)
    print(json.dumps(record, sort_keys=True))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)

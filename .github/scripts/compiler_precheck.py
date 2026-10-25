"""Optional syntax precheck; it never replaces the native success receipt."""
import json
import argparse
import contextlib
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import uuid
import stat

# This file is invoked with -I from the trusted control checkout, never PR Python.
sys.path.insert(0, str(Path(__file__).resolve().parent))
import direct_progress as shared
import compiler_witness as witness
import editor_sandbox as sandbox
import process_registry


PRECHECK_SECONDS = 10 * 60
PR_ADDITIONAL_SECONDS = 12 * 60  # Includes the existing bounded process-tree teardown.
EDITOR_DATA = Path('/opt/unity/Editor/Data')
PINNED_TOOLS = {
    'NetCoreRuntime/dotnet': '54a3016f80e65c3505d072874c39a325b777838b8e1895d83ef11f906388f8be',
    'DotNetSdkRoslyn/csc.dll': '27bd8a1717a8a5cb4586d9ce0cdde40bf536c7f744ae9526311e546475055f0f',
    'DotNetSdkRoslyn/Microsoft.CodeAnalysis.dll': '219ae7abe830711204f659b407f20916e0101dfe204c68aa31eb4674b0a14b72',
    'DotNetSdkRoslyn/Microsoft.CodeAnalysis.CSharp.dll': '24ccb925ce05205eb3bc870366fc0ce10168fd7c34c2127ef500aa6463ace422',
}
TOOL_CLOSURES = ('NetCoreRuntime', 'DotNetSdkRoslyn', 'MonoBleedingEdge', 'MonoEmbedRuntime',
                 'Tools/BuildPipeline', 'Tools/Unity.SourceGenerators', 'Tools/ilpp',
                 'Resources/PackageManager', 'bee_backend', 'Tools/netcorerun')


def tree_identity(root, *, allow_internal_links=False):
    """Measure exact dependency bytes/paths/modes, never a package version claim."""
    root = Path(root)
    if root.is_symlink() or not root.exists():
        raise ValueError('Measured input root must exist without indirection')
    paths = [root]
    if root.is_dir():
        for directory, directories, files in os.walk(root, followlinks=False, onerror=witness._raise_walk_error):
            paths.extend(Path(directory) / name for name in directories + files)
    rows = {}
    for path in sorted(paths):
        relative = '.' if path == root else path.relative_to(root).as_posix()
        info = path.lstat()
        if stat.S_ISLNK(info.st_mode):
            target = path.resolve(strict=True)
            if not allow_internal_links or (target != root and root not in target.parents):
                raise ValueError('Unmeasured or disallowed dependency link')
            rows[relative] = ('link', os.readlink(path), target.relative_to(root).as_posix())
        elif stat.S_ISDIR(info.st_mode):
            rows[relative] = ('directory', info.st_mode & 0o111)
        elif stat.S_ISREG(info.st_mode):
            digest = hashlib.sha256()
            with path.open('rb') as stream:
                for block in iter(lambda: stream.read(512 * 1024), b''):
                    digest.update(block)
            after = path.stat()
            if (info.st_ino, info.st_size, info.st_mtime_ns) != (after.st_ino, after.st_size, after.st_mtime_ns):
                raise ValueError('Dependency changed while measured')
            rows[relative] = ('file', info.st_mode & 0o111, info.st_size, digest.hexdigest())
        else:
            raise ValueError('Unsupported dependency entry')
    for row in rows.values():
        if row[0] == 'link' and row[2] not in rows:
            raise ValueError('Linked dependency target is outside measured closure')
    return rows


def installed_tool_closure():
    require_parser_tools()
    return {name: tree_identity(EDITOR_DATA / name, allow_internal_links=True) for name in TOOL_CLOSURES}


def identity_digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode('utf-8')).hexdigest()


def stopped_precheck(state):
    for name in ('compiler-group.json', 'capture-group.json', 'broker-group.json'):
        shared.require_stopped_marker(state / name)


def retain_diagnostics(source, state, output):
    try:
        with (source.parent / 'private-syntax-precheck.log').open('ab') as retained:
            retained.write(b'Optional syntax precheck result: ' + output[:65536] + b'\n')
            for name in ('private-precheck.log', 'private-editor-console.log',
                         'control-project/Logs/capture.log', 'private-parser-build.log',
                         'private-parser-result.json', 'private-parser-stderr.log'):
                path = state / name
                if path.is_file() and not path.is_symlink():
                    with path.open('rb') as log:
                        log.seek(max(0, path.stat().st_size - 65536))
                        retained.write(name.encode('ascii') + b'\n' + log.read(65536) + b'\n')
    except OSError:
        # Optional diagnostics cannot replace the normal compiler's outcome.
        pass


@contextlib.contextmanager
def precheck_resources(source, control, identity, *, registry):
    """A single watchdog owns all optional preparation; normal compile stays separate."""
    state, answer, output = None, None, b''
    try:
        state = Path(tempfile.mkdtemp(prefix='syntax-precheck-', dir=source.parent))
    except OSError:
        pass
    if state is None:
        yield None
        return
    marker = state / 'compiler-group.json'
    command = [sys.executable, '-I', str(control / '.github/scripts/compiler_precheck.py'),
               '--source', str(source), '--control', str(control), '--state', str(state),
               '--job-marker', str(registry[0]), '--job-root', str(registry[1])]
    for key, value in identity.items():
        command.extend(['--' + key.replace('_', '-'), value])
    try:
        try:
            with (state / 'private-precheck.log').open('wb') as errors:
                output = shared.run_compiler(command, source, errors, marker,
                                             timeout=PRECHECK_SECONDS, registry=registry)
            if len(output) <= 64 * 1024:
                result = json.loads(output)
                if isinstance(result, dict):
                    status, diagnostics = result.get('status'), result.get('diagnostics')
                    package_identity = result.get('package_identity')
                    expected = dict(identity, status=status, diagnostics=diagnostics,
                                    package_identity=package_identity)
                    seed = state / 'package-seed'
                    if (result == expected and status in ('ready', 'syntax_error')
                            and isinstance(package_identity, str) and re.fullmatch('[0-9a-f]{64}', package_identity)
                            and isinstance(diagnostics, list) and len(diagnostics) <= 32
                            and bool(diagnostics) == (status == 'syntax_error')
                            and all(isinstance(value, str) and re.fullmatch(r'CS[0-9]{4}', value) for value in diagnostics)
                            and seed.is_dir() and not seed.is_symlink()):
                        answer = dict(syntax_error=status == 'syntax_error', package_seed=seed)
        except (subprocess.SubprocessError, OSError, ValueError):
            answer = None
        # Never catch custody failures as ordinary optional unavailability.
        stopped_precheck(state)
        retain_diagnostics(source, state, output)
        yield answer
    finally:
        stopped_precheck(state)
        try:
            # Trusted deletion starts no descendants and shares the driver's
            # registered group. subprocess.run kills and reaps it on timeout.
            subprocess.run([sys.executable, '-I', '-c',
                            'import shutil,sys; shutil.rmtree(sys.argv[1])', str(state)],
                           stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL, timeout=60, check=True)
        except (OSError, subprocess.SubprocessError):
            # All owned services already stopped. Keep bounded diagnostic/state
            # evidence for job cleanup without changing the normal outcome.
            pass


@contextlib.contextmanager
def child_resources(control, state, registries):
    """Only the watchdog child copies/hashes inputs or starts a licensing broker."""
    seed = state / 'package-seed'
    cache = Path.home() / '.config/unity3d/cache'
    before = tree_identity(cache) if cache.exists() else None
    if before is None:
        seed.mkdir()
    else:
        shutil.copytree(cache, seed)
        if tree_identity(cache) != before or tree_identity(seed) != before:
            raise ValueError('Public package cache changed while copied')
    tools, packages = installed_tool_closure(), tree_identity(seed)
    value = {'package_seed': str(seed), 'package_identity': identity_digest(packages),
             'tool_identity': identity_digest(tools), 'registries': registries}
    (state / 'tool-closure.json').write_text(json.dumps(tools, sort_keys=True))
    with sandbox.licensing_broker(state, registries=registries) as (pipe, sockets):
        value.update(pipe=pipe, sockets=[str(path) for path in sockets])
        yield value
    # The producer emits its receipt only after this context has verified all inputs.
    if tree_identity(seed) != packages or installed_tool_closure() != tools:
        raise ValueError('Witness package or tool closure changed during use')


def require_parser_tools():
    for relative, expected in PINNED_TOOLS.items():
        path = EDITOR_DATA / relative
        if (any(part.is_symlink() for part in (path, *path.parents)) or not path.is_file()
                or hashlib.sha256(path.read_bytes()).hexdigest() != expected):
            raise ValueError('Optional pinned parser tool is unavailable')


def copy_control_inputs(control, project, inventory):
    project.mkdir()
    for relative in inventory:
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(control / relative, target)
    # No Library, Temp, home, compiler-server or Bee state is copied from another run.
    for name in ('Library', 'Temp', 'Logs', 'UserSettings', '.cache'):
        if (project / name).exists():
            raise ValueError('Fresh control compilation state required')


def capture_plan(control, state, inventory, resources):
    project, capture = state / 'control-project', state / 'capture'
    copy_control_inputs(control, project, inventory)
    capture.mkdir()
    launch = {'schema': 1, 'nonce': uuid.uuid4().hex, 'control_root': str(project),
              'target': 'StandaloneLinux64', 'runtime': str(EDITOR_DATA / 'NetCoreRuntime/dotnet'),
              'compiler': str(EDITOR_DATA / 'DotNetSdkRoslyn/csc.dll')}
    witness._write_capture(capture / 'launch.json', launch)
    launcher = state / 'dotnet-wrapper'
    launcher.write_text('#!/usr/bin/python3 -I\nimport runpy, sys\n'
        'sys.path.insert(0, "/witness-tools")\n'
        'sys.argv = ["compiler_witness.py", "--capture", "/capture", "--runtime", '
        '"/witness-runtime/dotnet", "--", *sys.argv[1:]]\n'
        'runpy.run_path("/witness-tools/compiler_witness.py", run_name="__main__")\n')
    launcher.chmod(0o700)
    pipe, sockets = resources['pipe'], [Path(path) for path in resources['sockets']]
    seed = Path(resources['package_seed'])
    if identity_digest(tree_identity(seed)) != resources['package_identity']:
        raise ValueError('Private package seed changed before capture')
    home = sandbox.prepare_home(project, package_seed=seed)
    if identity_digest(tree_identity(home / '.config/unity3d/cache')) != resources['package_identity']:
        raise ValueError('Control home differs from the admitted package seed')
    editor = [str(sandbox.EDITOR), '-nographics', '-licensingIpc', pipe.removeprefix('Unity-'),
              '-batchmode', '-quit', '-projectPath', str(project), '-buildTarget', 'Linux64',
              '-standaloneBuildSubtarget', 'Player', '-logFile', str(project / 'Logs/capture.log')]
    command = sandbox.sandbox_command(project, home, editor, sockets)
    split = command.index('--')
    command[split:split] = ['--ro-bind', str(EDITOR_DATA / 'NetCoreRuntime'), '/witness-runtime',
        '--ro-bind', str(control / '.github/scripts'), '/witness-tools',
        '--bind', str(capture), '/capture',
        '--ro-bind', str(capture / 'launch.json'), '/capture/launch.json',
        '--ro-bind', str(launcher), launch['runtime'],
        '--setenv', 'BEE_CACHE_DIRECTORY', '/tmp/private-bee-cache']
    marker = state / 'capture-group.json'
    with (state / 'private-editor-console.log').open('wb') as log:
        process = process_registry.spawn_registered(
            command, [*resources['registries'], (marker, None)], cwd=project, env={},
            stdin=subprocess.DEVNULL, stdout=log, stderr=log)
        try:
            deadline = time.monotonic() + PRECHECK_SECONDS - 90
            while not (capture / 'seal.json').exists() and not (capture / 'error.json').exists():
                if process.poll() is not None or time.monotonic() >= deadline:
                    raise ValueError('Optional initial capture unavailable')
                time.sleep(0.1)
        finally:
            # Includes nested sessions; deletion waits for the existing stopped-group proof.
            shared.stop_compiler_group(process, marker)
            shared.require_stopped_marker(marker)
    plan = witness.validate_initial_capture(capture, launch)
    resolved = tree_identity(project / 'Library/PackageCache')
    (state / 'resolved-packages.json').write_text(json.dumps(resolved, sort_keys=True))
    plan['resolved_packages'] = resolved
    return project, plan


def parse_sources(control, source, state, project, plan, changed):
    runtime, roslyn = EDITOR_DATA / 'NetCoreRuntime', EDITOR_DATA / 'DotNetSdkRoslyn'
    output = state / 'parser'
    output.mkdir()
    framework = runtime / 'shared/Microsoft.NETCore.App/6.0.21'
    references = sorted(framework.glob('*.dll'))
    if not references:
        raise ValueError('Pinned parser framework unavailable')
    command = [str(runtime / 'dotnet'), 'exec', str(roslyn / 'csc.dll'), '/noconfig', '/nostdlib',
               '/target:exe', '/langversion:9.0', '/out:' + str(output / 'Parser.dll')]
    command += ['/reference:' + str(path) for path in references]
    command += ['/reference:' + str(roslyn / name) for name in ('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll')]
    command += [str(control / 'tools/compiler-witness/Program.cs')]
    with (state / 'private-parser-build.log').open('wb') as log:
        subprocess.run(command, cwd=control, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
                       timeout=60, check=True)
    for name in ('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll'):
        shutil.copy2(roslyn / name, output / name)
    (output / 'Parser.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net6.0', 'rollForward': 'Disable',
        'framework': {'name': 'Microsoft.NETCore.App', 'version': '6.0.21'}}}))
    request = {'baseDirectory': str(project), 'arguments': plan['argv'][2:],
               'responses': [{'argument': row['argument'], 'contentsBase64': row['contents_base64']}
                             for row in plan['responses']],
               'sources': [{'original': str(project / relative), 'candidate': str(source / relative)} for relative in changed]}
    path = state / 'parser-request.json'
    path.write_text(json.dumps(request))
    with (state / 'private-parser-result.json').open('wb') as result, (state / 'private-parser-stderr.log').open('wb') as errors:
        parsed = subprocess.run([str(runtime / 'dotnet'), str(output / 'Parser.dll'), str(path)],
                                cwd=project, stdin=subprocess.DEVNULL, stdout=result, stderr=errors, timeout=60)
    result_path = state / 'private-parser-result.json'
    if result_path.stat().st_size > 16 * 1024 * 1024:
        raise ValueError('Parser result exceeds bound')
    value = json.loads(result_path.read_bytes())
    if not isinstance(value, dict) or parsed.returncode != 10 or value.get('status') != 'syntax_error':
        return []
    expected_sources = {str(project / relative) for relative in plan['candidate_sources']}
    sources = value.get('sourceFiles')
    if (not isinstance(sources, list) or len(sources) != len(expected_sources) or set(sources) != expected_sources
            or value.get('languageVersion') != 'CSharp9' or value.get('documentationMode') != 'None'
            or value.get('defines') != plan['assembly'].get('Defines')):
        raise ValueError('Parsed options or source roster differ from the initial graph')
    errors = value.get('errors')
    changed_paths = {str(project / relative) for relative in changed}
    if (not isinstance(errors, list) or not 0 < len(errors) <= 32
            or any(not isinstance(row, dict) or row.get('path') not in changed_paths
                   or not isinstance(row.get('id'), str) or not re.fullmatch(r'CS[0-9]{4}', row['id'])
                   or type(row.get('line')) is not int or row['line'] < 1
                   or type(row.get('column')) is not int or row['column'] < 1 for row in errors)):
        raise ValueError('Parser diagnostics do not identify admitted source')
    return [row['id'] for row in errors]


def produce_precheck(source, control, state, identity, resources):
    sys.path.insert(0, str(control / 'tools'))
    from lucidlib.verification import artifact_fingerprint

    def unchanged():
        if (shared.git_head(source) != identity['head'] or shared.git_head(control) != identity['control_commit']
                or artifact_fingerprint(source) != identity['source_fingerprint']):
            raise ValueError('Optional witness source/control identity changed')
        shared.driver.require_control_reporting_tools(source, control)

    unchanged()
    inventory = witness._tracked_inventory(control)
    possible = [name for name in inventory if name.startswith('Assets/') and name.endswith('.cs')]
    changed = witness.admit_source_changes(source, control, possible)
    if not changed:
        return {'status': 'unavailable'}
    if identity_digest(installed_tool_closure()) != resources['tool_identity']:
        raise ValueError('Installed compiler closure changed before capture')
    project, plan = capture_plan(control, state, inventory, resources)
    if witness.admit_source_changes(source, control, plan['candidate_sources']) != changed:
        raise ValueError('Admitted source changed during capture')
    # The copied control sources must remain the exact baseline supplied to the parser.
    for relative in plan['candidate_sources']:
        if hashlib.sha256((project / relative).read_bytes()).hexdigest() != inventory[relative][1]:
            raise ValueError('Control source changed during capture')
    diagnostics = parse_sources(control, source, state, project, plan, changed)
    unchanged()
    if witness._tracked_inventory(control) != inventory:
        raise ValueError('Control inputs changed during witness')
    if (identity_digest(installed_tool_closure()) != resources['tool_identity']
            or identity_digest(tree_identity(Path(resources['package_seed']))) != resources['package_identity']
            or tree_identity(project / 'Library/PackageCache') != plan['resolved_packages']):
        raise ValueError('Compiler/package closure changed during witness')
    return dict(identity, status='syntax_error' if diagnostics else 'ready',
                diagnostics=diagnostics, package_identity=resources['package_identity'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'control', 'state'):
        parser.add_argument('--' + name, type=Path, required=True)
    for name in ('head', 'control-commit', 'source-fingerprint'):
        parser.add_argument('--' + name, required=True)
    parser.add_argument('--job-marker', type=Path, required=True)
    parser.add_argument('--job-root', type=int, required=True)
    args = parser.parse_args()
    identity = {'head': args.head, 'control_commit': args.control_commit,
                'source_fingerprint': args.source_fingerprint}
    stage = 'admission'
    try:
        source, control, state = (path.resolve(strict=True) for path in (args.source, args.control, args.state))
        inventory = witness._tracked_inventory(control)
        candidates = [name for name in inventory if name.startswith('Assets/') and name.endswith('.cs')]
        if not witness.admit_source_changes(source, control, candidates):
            result = {'status': 'unavailable'}
        else:
            registries = [(args.job_marker, args.job_root), (state / 'compiler-group.json', os.getpgrp())]
            stage = 'prepare-resources'
            with child_resources(control, state, registries) as resources:
                stage = 'capture-and-parse'
                result = produce_precheck(source, control, state, identity, resources)
                stage = 'verify-resources-and-custody'
    except (ValueError, OSError, subprocess.SubprocessError, TypeError, KeyError) as error:
        # This stream is private diagnostic evidence, never classification input.
        print(json.dumps({'stage': stage, 'error_type': type(error).__name__,
                          'message': str(error)[:2048]}, sort_keys=True), file=sys.stderr)
        result = {'status': 'unavailable'}
    print(json.dumps(result, sort_keys=True))


if __name__ == '__main__':
    main()

"""Optional, independently observed syntax failures; unavailable evidence is not failure."""
import hashlib
import base64
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import sys


COMPILATION_ROOTS = ('Assets', 'Packages', 'ProjectSettings')
GRAPH_DATA = 'ScriptCompilationBuildProgram.Data.ScriptCompilationData'
MAX_CAPTURE_BYTES = 32 * 1024 * 1024


def _raise_walk_error(error):
    raise error


def _relative_source(value):
    if not isinstance(value, str) or '\\' in value or '\x00' in value:
        raise ValueError('Unsupported witness source path')
    path = PurePosixPath(value)
    if path.is_absolute() or str(path) != value or '..' in path.parts or path.suffix != '.cs':
        raise ValueError('Unsupported witness source path')
    if not path.parts or path.parts[0] != 'Assets':
        raise ValueError('Witness source must belong to tracked Assets')
    return value


def _tracked_inventory(root):
    """Hash actual checkout bytes, rejecting links and untracked compilation inputs."""
    root = Path(root).resolve(strict=True)
    raw = subprocess.check_output(['git', 'ls-tree', '-rz', '--full-tree', 'HEAD'], cwd=root)
    rows = {}
    for entry in raw.split(b'\0'):
        if not entry:
            continue
        metadata, name = entry.split(b'\t', 1)
        mode, kind, _ = metadata.split()
        relative = name.decode('utf-8', errors='strict')
        parts = PurePosixPath(relative).parts
        if kind != b'blob' or mode not in (b'100644', b'100755') or '..' in parts:
            raise ValueError('Unsupported tracked witness input')
        path = root.joinpath(*parts)
        if any(parent.is_symlink() for parent in (path, *path.parents)):
            raise ValueError('Linked witness input rejected')
        if not path.is_file():
            raise ValueError('Tracked witness input is missing or not a file')
        info = path.stat()
        if not stat.S_ISREG(info.st_mode):
            raise ValueError('Witness inputs must be regular files')
        executable = bool(info.st_mode & 0o111)
        if executable != (mode == b'100755'):
            raise ValueError('Witness input mode differs from its tracked mode')
        digest = hashlib.sha256()
        with path.open('rb') as stream:
            for block in iter(lambda: stream.read(512 * 1024), b''):
                digest.update(block)
        rows[relative] = (mode, digest.hexdigest())
    for name in COMPILATION_ROOTS:
        base = root / name
        if base.is_symlink():
            raise ValueError('Linked compilation root rejected')
        for directory, directories, files in os.walk(base, followlinks=False, onerror=_raise_walk_error):
            for child in (*directories, *files):
                path = Path(directory) / child
                if path.is_symlink():
                    raise ValueError('Linked compilation input rejected')
            for child in files:
                relative = (Path(directory) / child).relative_to(root).as_posix()
                if relative not in rows:
                    raise ValueError('Untracked compilation input rejected')
    return rows


def admit_source_changes(source, control, candidate_sources):
    """Only existing captured Game.Runtime source contents may differ from control.

    The caller must establish that candidate_sources is the complete validated
    initial Game.Runtime roster. Rejection disables the optional witness only.
    """
    candidates = tuple(_relative_source(value) for value in candidate_sources)
    if not candidates or len(set(candidates)) != len(candidates):
        raise ValueError('Witness candidate roster is empty or duplicated')
    original, current = _tracked_inventory(control), _tracked_inventory(source)
    if original.keys() != current.keys() or not set(candidates).issubset(original):
        raise ValueError('Witness tracked input roster differs')
    if any(original[name][0] != current[name][0] for name in original):
        raise ValueError('Witness tracked input modes differ')
    changed = tuple(sorted(name for name in original if original[name][1] != current[name][1]))
    if not set(changed).issubset(candidates):
        raise ValueError('Witness configuration or other source inputs changed')
    return changed


def _capture_json(path):
    if path.is_symlink() or not path.is_file() or path.stat().st_size > MAX_CAPTURE_BYTES:
        raise ValueError('Capture file is not a bounded regular file')
    value = json.loads(path.read_bytes())
    if not isinstance(value, dict):
        raise ValueError('Capture must be an object')
    return value


def _verified_blob(value):
    if not isinstance(value, dict) or not isinstance(value.get('contents_base64'), str):
        raise ValueError('Capture bytes are missing')
    if len(value['contents_base64']) > MAX_CAPTURE_BYTES:
        raise ValueError('Capture bytes exceed bound')
    raw = base64.b64decode(value['contents_base64'], validate=True)
    if hashlib.sha256(raw).hexdigest() != value.get('sha256'):
        raise ValueError('Captured byte identity differs')
    return raw


def validate_planned_target(graph, dag, payload, folder, launch):
    """Link a planned target to its exact DAG writers; this does not prove phase.

    The producer must seal these bytes at the first intercepted compiler boundary.
    Never execute the Action string or fill missing inputs from a later graph.
    """
    if (not isinstance(folder, str) or not re.fullmatch(r'Library/Bee/artifacts/[0-9a-f]+E\.dag/', folder)
            or not isinstance(payload, bytes) or len(payload) > MAX_CAPTURE_BYTES):
        raise ValueError('Unqualified initial DAG location or payload')
    data = graph.get(GRAPH_DATA) if isinstance(graph, dict) else None
    if (not isinstance(data, dict) or data.get('BuildTarget') != launch['target']
            or data.get('OutputDirectory') != 'Library/ScriptAssemblies'
            or data.get('BuildPlayerDataOutput') != 'Library/BuildPlayerData/Editor'
            or data.get('CodegenAssemblies') is not None):
        raise ValueError('Initial plan is not an Editor graph')
    rows = data.get('Assemblies')
    if (not isinstance(rows, list) or not rows
            or any(not isinstance(row, dict) or not isinstance(row.get('Name'), str) for row in rows)
            or len({row['Name'] for row in rows}) != len(rows)):
        raise ValueError('Initial assembly identities are ambiguous')
    candidates = [row for row in rows if row['Name'] == 'Game.Runtime']
    if len(candidates) != 1 or candidates[0].get('LanguageVersion') != '9.0':
        raise ValueError('Initial candidate assembly is unavailable')
    assembly = candidates[0]
    if not isinstance(assembly.get('SourceFiles'), list) or not assembly['SourceFiles']:
        raise ValueError('Initial candidate sources are unavailable')
    sources = tuple(_relative_source(value) for value in assembly['SourceFiles'])
    if len(set(sources)) != len(sources):
        raise ValueError('Initial candidate sources are duplicated')
    nodes = dag.get('Nodes') if isinstance(dag, dict) else None
    if (not isinstance(nodes, list) or not nodes
            or any(not isinstance(node, dict) or not isinstance(node.get('Outputs', []), list)
                   or any(not isinstance(path, str) for path in node.get('Outputs', [])) for node in nodes)):
        raise ValueError('Initial DAG nodes are unavailable')
    compiler_nodes = [node for node in nodes if str(node.get('DisplayName', '')).startswith('Csc ')
                      or 'csc.dll' in str(node.get('Action', ''))]
    expected_compilers = {'Csc ' + row['Name'] for row in rows}
    if (len(compiler_nodes) != len(rows)
            or any(not isinstance(node.get('DisplayName'), str) for node in compiler_nodes)
            or {node.get('DisplayName') for node in compiler_nodes} != expected_compilers):
        raise ValueError('Initial compiler action roster differs from assembly graph')
    for node in compiler_nodes:
        name = node['DisplayName'][4:]
        if not re.fullmatch(r'[A-Za-z0-9_.-]+', name):
            raise ValueError('Unsupported assembly action name')
        response = folder + name + '.rsp'
        expected_action = ('"' + launch['runtime'] + '" exec "' + launch['compiler']
                           + '" /nostdlib /noconfig /shared "@' + response + '" "@' + response + '2"')
        if node.get('Action') != expected_action:
            raise ValueError('Every initial compiler must pass through the owned wrapper')
    rsp = folder + 'Game.Runtime.rsp'
    outputs = [folder + 'Game.Runtime' + suffix for suffix in ('.dll', '.pdb', '.ref.dll')]
    targets = [node for node in nodes if node.get('DisplayName') == 'Csc Game.Runtime']
    if len(targets) != 1:
        raise ValueError('Initial target command is missing or ambiguous')
    target = targets[0]
    named = dag.get('NamedNodes')
    start = named.get('ScriptAssemblies') if isinstance(named, dict) else None
    if type(start) is not int or not 0 <= start < len(nodes):
        raise ValueError('Selected ScriptAssemblies DAG root is unavailable')
    pending, reached, active = [(start, False)], set(), set()
    while pending:
        index, finished = pending.pop()
        if finished:
            active.remove(index)
            reached.add(index)
            continue
        if index in reached:
            continue
        if index in active:
            raise ValueError('Selected DAG dependencies contain a cycle')
        active.add(index)
        edges = nodes[index].get('ToBuildDependencies', [])
        if (not isinstance(edges, list)
                or any(type(edge) is not int or not 0 <= edge < len(nodes) for edge in edges)):
            raise ValueError('Selected DAG dependencies are malformed')
        pending.append((index, True))
        pending.extend((edge, False) for edge in edges)
    if not any(nodes[index] is target for index in reached):
        raise ValueError('Planned target is not required by ScriptAssemblies')
    # Compare the exact qualified template; shell tokenization is not execution authority.
    expected = ('"' + launch['runtime'] + '" exec "' + launch['compiler']
                + '" /nostdlib /noconfig /shared "@' + rsp + '" "@' + rsp + '2"')
    inputs = target.get('Inputs')
    if (target.get('Action') != expected or target.get('Outputs') != outputs
            or target.get('FrontendResponseFiles') != [rsp]
            or target.get('Env') != [{'Key': 'DOTNET_MULTILEVEL_LOOKUP', 'Value': '0'}]
            or not isinstance(inputs, list) or any(not isinstance(value, str) for value in inputs)
            or sorted(value for value in inputs if value.endswith('.cs')) != sorted(sources)
            or inputs.count(rsp) != 1):
        raise ValueError('Initial target command linkage differs')
    for output in outputs:
        writers = [node for node in nodes if output in node.get('Outputs', [])]
        if writers != [target]:
            raise ValueError('Initial target output has ambiguous writers')
    dependencies = target.get('ToBuildDependencies')
    used = target.get('ToUseDependencies')
    if (not isinstance(dependencies, list) or not isinstance(used, list)
            or any(type(index) is not int or not 0 <= index < len(nodes) for index in dependencies + used)):
        raise ValueError('Initial target dependencies are malformed')
    responses = []
    for path in (rsp, rsp + '2'):
        writers = [(index, node) for index, node in enumerate(nodes) if path in node.get('Outputs', [])]
        if len(writers) != 1:
            raise ValueError('Initial response writer is missing or ambiguous')
        index, writer = writers[0]
        offset, length = writer.get('PayloadOffset'), writer.get('PayloadLength')
        if (writer.get('ActionType') != 'WriteFile' or writer.get('Outputs') != [path]
                or writer.get('Inputs', []) != [] or writer.get('Action') is not None
                or type(offset) is not int or type(length) is not int
                or offset < 0 or length < 0 or offset + length > len(payload)
                or index not in dependencies or (path == rsp + '2' and index not in used)):
            raise ValueError('Initial response writer is not the qualified payload write')
        raw = payload[offset:offset + length]
        responses.append({'argument': '@' + path, 'sha256': hashlib.sha256(raw).hexdigest(),
                          'contents_base64': base64.b64encode(raw).decode('ascii')})
    return {'argv': ['exec', launch['compiler'], '/nostdlib', '/noconfig', '/shared', '@' + rsp, '@' + rsp + '2'],
            'responses': responses, 'candidate_sources': sources, 'assembly': assembly}


def validate_initial_capture(directory, launch):
    """Validate the sealed FIRST boundary, not a later actual-target invocation.

    Linux qualification established plan/file/actual-target equality. Production
    intercepts every compiler and forwards none; missing initial inputs stay unavailable.
    """
    directory = Path(directory)
    required = {'launch.json', 'first.json', 'plan.json', 'seal.json'}
    names = {path.name for path in directory.iterdir()}
    if directory.is_symlink() or not required <= names or names - required - {'capture.lock'}:
        raise ValueError('First-boundary capture is missing, partial, failed or duplicated')
    if any(path.is_symlink() or not path.is_file() for path in directory.iterdir()):
        raise ValueError('Capture state must be regular files')
    if _capture_json(directory / 'launch.json') != launch:
        raise ValueError('Capture launch identity differs')
    first = _capture_json(directory / 'first.json')
    plan = _capture_json(directory / 'plan.json')
    seal = _capture_json(directory / 'seal.json')
    for record in (first, plan, seal):
        if (type(record.get('schema')) is not int or record['schema'] != 1
                or type(record.get('sequence')) is not int or record['sequence'] != 0
                or record.get('nonce') != launch['nonce']):
            raise ValueError('Capture is not the launch-owned first boundary')
    if seal.get('plan_sha256') != hashlib.sha256((directory / 'plan.json').read_bytes()).hexdigest():
        raise ValueError('First-boundary seal differs')
    argv = first.get('argv')
    folder = plan.get('folder')
    if (first.get('cwd') != launch['control_root'] or not isinstance(argv, list) or len(argv) != 7
            or argv[:5] != ['exec', launch['compiler'], '/nostdlib', '/noconfig', '/shared']
            or not isinstance(folder, str) or not isinstance(argv[5], str)
            or not re.fullmatch(re.escape('@' + folder) + r'[A-Za-z0-9_.-]+\.rsp', argv[5])
            or argv[6] != argv[5] + '2'):
        raise ValueError('First compiler launch identity differs')
    graph = json.loads(_verified_blob(plan.get('graph')))
    result = validate_planned_target(graph, json.loads(_verified_blob(plan.get('dag'))),
                                     _verified_blob(plan.get('payload')), folder, launch)
    first_name = argv[5][len('@' + folder):-4]
    if first_name not in {row['Name'] for row in graph[GRAPH_DATA]['Assemblies']}:
        raise ValueError('First compiler does not belong to the initial graph')
    responses = plan.get('responses')
    if not isinstance(responses, list) or len(responses) != 2:
        raise ValueError('Actual first-boundary response files are missing')
    for actual, planned in zip(responses, result['responses']):
        if (not isinstance(actual, dict) or actual.get('argument') != planned['argument']
                or _verified_blob(actual) != _verified_blob(planned)):
            raise ValueError('Actual first-boundary response differs from its planned writer')
    return result


def _control_bytes(root, relative):
    path = PurePosixPath(relative)
    if path.is_absolute() or str(path) != relative or '..' in path.parts:
        raise ValueError('Capture path is not a canonical control-relative path')
    full = root.joinpath(*path.parts)
    if (any(part.is_symlink() for part in (full, *full.parents)) or not full.is_file()
            or full.stat().st_size > MAX_CAPTURE_BYTES):
        raise ValueError('Capture input is not a bounded regular control file')
    return full.read_bytes()


def _blob(raw):
    return {'sha256': hashlib.sha256(raw).hexdigest(),
            'contents_base64': base64.b64encode(raw).decode('ascii')}


def _write_capture(path, value):
    # Exclusive writes preserve first-attempt failure; partial files are unavailable.
    with path.open('xb') as stream:
        stream.write(json.dumps(value, sort_keys=True).encode('utf-8'))


def capture_compiler(directory, launch, argv):
    """Seal the first planned target and stop EVERY compiler without forwarding.

    Each intercepted launch acquires the shared lock before inspecting or writing
    capture state. A failed attempt is permanent; later launches cannot replace it.
    """
    import fcntl  # Linux producer; imported lazily so pure validation is portable.
    directory = Path(directory)
    with (directory / 'capture.lock').open('ab') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        if (directory / 'first.json').exists():
            return 70
        first = {'schema': 1, 'sequence': 0, 'nonce': launch['nonce'],
                 'cwd': str(Path.cwd()), 'argv': list(argv)}
        _write_capture(directory / 'first.json', first)
        try:
            if (first['cwd'] != launch['control_root'] or len(argv) != 7
                    or argv[:5] != ['exec', launch['compiler'], '/nostdlib', '/noconfig', '/shared']
                    or not isinstance(argv[5], str) or argv[6] != argv[5] + '2'):
                raise ValueError('Unqualified first compiler command')
            match = re.fullmatch(r'@(Library/Bee/artifacts/([0-9a-f]+E)\.dag/)[A-Za-z0-9_.-]+\.rsp', argv[5])
            if match is None:
                raise ValueError('Unqualified first compiler response location')
            folder, graph_id = match.groups()
            root = Path(launch['control_root'])
            graph = _control_bytes(root, 'Library/Bee/' + graph_id + '-inputdata.json')
            dag = _control_bytes(root, 'Library/Bee/' + graph_id + '.dag.json')
            payload_path = 'Library/Bee/' + graph_id + '.dag.payloads'
            if json.loads(dag).get('PayloadsFile') not in (payload_path, str(root / payload_path)):
                raise ValueError('DAG payload path differs from first graph')
            payload = _control_bytes(root, payload_path)
            responses = []
            for suffix in ('.rsp', '.rsp2'):
                path = folder + 'Game.Runtime' + suffix
                responses.append(dict(_blob(_control_bytes(root, path)), argument='@' + path))
            plan = {'schema': 1, 'sequence': 0, 'nonce': launch['nonce'], 'folder': folder,
                    'graph': _blob(graph), 'dag': _blob(dag), 'payload': _blob(payload),
                    'responses': responses}
            _write_capture(directory / 'plan.json', plan)
            _write_capture(directory / 'seal.json', {'schema': 1, 'sequence': 0, 'nonce': launch['nonce'],
                           'plan_sha256': hashlib.sha256((directory / 'plan.json').read_bytes()).hexdigest()})
            validate_initial_capture(directory, launch)
        except (ValueError, OSError, TypeError, KeyError) as error:
            _write_capture(directory / 'error.json', {'error_type': type(error).__name__})
        return 70


def capture_main(arguments):
    if len(arguments) < 5 or arguments[0] != '--capture' or arguments[2] != '--runtime' or arguments[4] != '--':
        return 70
    directory, runtime = Path(arguments[1]), arguments[3]
    launch = _capture_json(directory / 'launch.json')
    argv = arguments[5:]
    if any(Path(argument).name.lower() == 'csc.dll' for argument in argv):
        return capture_compiler(directory, launch, argv)
    # Only trusted control tools use this namespace; ordinary tool argv/CWD stay exact.
    os.execv(runtime, [runtime, *argv])


if __name__ == '__main__':
    raise SystemExit(capture_main(sys.argv[1:]))

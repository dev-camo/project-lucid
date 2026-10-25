"""Optional compiler-witness admission; fixture source is data, never executed."""
from pathlib import Path
import base64
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).parent))
import compiler_witness as witness


class AdmissionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.control = self.root / 'control'
        self.control.mkdir()
        self.files = {
            'Assets/Game/A.cs': b'class A {}\n',
            'Assets/Game/B.cs': b'class B {}\n',
            'Assets/Game/A.cs.meta': b'guid: inert-metadata\n',
            'Assets/Game/Game.Runtime.asmdef': b'{"name":"Game.Runtime"}\n',
            'Assets/Editor/CodeGen/Processor.cs': b'class Processor {}\n',
            'Packages/manifest.json': b'{"dependencies":{}}\n',
            'ProjectSettings/ProjectSettings.asset': b'inert settings\n',
        }
        for relative, content in self.files.items():
            path = self.control / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        subprocess.run(['git', 'init', '-q', str(self.control)], check=True)
        subprocess.run(['git', 'add', '.'], cwd=self.control, check=True)
        subprocess.run(['git', '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                        'commit', '-qm', 'Inert input inventory'], cwd=self.control, check=True)
        self.source = self.root / 'source'
        shutil.copytree(self.control, self.source)
        self.candidates = ('Assets/Game/A.cs', 'Assets/Game/B.cs')

    def admit(self, candidates=None):
        return witness.admit_source_changes(self.source, self.control,
                                            self.candidates if candidates is None else candidates)

    def test_only_existing_candidate_source_contents_may_change(self):
        self.assertEqual((), self.admit())
        (self.source / self.candidates[1]).write_bytes(b'class B { int Broken = ; }\n')
        self.assertEqual((self.candidates[1],), self.admit())
        (self.source / self.candidates[0]).write_bytes(b'class A { int Value; }\n')
        self.assertEqual(self.candidates, self.admit())

    def test_dependency_extension_config_metadata_and_roster_changes_are_unavailable(self):
        for relative in self.files:
            if relative in self.candidates:
                continue
            with self.subTest(relative=relative):
                path = self.source / relative
                path.write_bytes(self.files[relative] + b'changed\n')
                with self.assertRaises(ValueError):
                    self.admit()
                path.write_bytes(self.files[relative])
        for relative in ('Assets/Game/New.cs', 'Assets/Editor/New.cs', 'Assets/Game/csc.rsp'):
            with self.subTest(added=relative):
                path = self.source / relative
                path.write_bytes(b'new input\n')
                with self.assertRaises(ValueError):
                    self.admit()
                path.unlink()
        path = self.source / self.candidates[0]
        path.unlink()
        with self.assertRaises(ValueError):
            self.admit()

    def test_symlinks_modes_and_untracked_ignored_inputs_cannot_enter_the_plan(self):
        path = self.source / self.candidates[0]
        path.unlink()
        path.symlink_to(self.control / self.candidates[0])
        with self.assertRaises(ValueError):
            self.admit()
        path.unlink()
        path.write_bytes(self.files[self.candidates[0]])
        path.chmod(0o755)
        with self.assertRaises(ValueError):
            self.admit()
        path.chmod(0o644)
        # Ignored source is still an initial Unity input, irrespective of Git's ignore rules.
        (self.source / '.git/info/exclude').write_text('*.hidden.cs\n')
        extra = self.source / 'Assets/Game/extra.hidden.cs'
        extra.write_bytes(b'class Extra {}\n')
        with self.assertRaises(ValueError):
            self.admit()

    def test_candidate_roster_rejects_aliases_duplicates_and_nontracked_paths(self):
        for candidates in (('Assets/Game/../Game/A.cs',), ('/Assets/Game/A.cs',),
                           ('Assets/Game/A.cs', 'Assets/Game/A.cs'), ('Assets/Game/missing.cs',),
                           ('Assets/Game/A.cs.meta',), ('Assets/Game\\A.cs',)):
            with self.subTest(candidates=candidates), self.assertRaises(ValueError):
                self.admit(candidates)

    def test_committed_executable_mode_change_is_not_a_source_content_change(self):
        path = self.source / self.candidates[0]
        path.chmod(0o755)
        subprocess.run(['git', 'update-index', '--chmod=+x', self.candidates[0]], cwd=self.source, check=True)
        subprocess.run(['git', '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                        'commit', '-qm', 'Inert changed mode'], cwd=self.source, check=True)
        with self.assertRaises(ValueError):
            self.admit()

    def test_committed_candidate_content_change_is_admitted(self):
        (self.source / self.candidates[0]).write_bytes(b'class A { int Value = ; }\n')
        subprocess.run(['git', 'add', self.candidates[0]], cwd=self.source, check=True)
        subprocess.run(['git', '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                        'commit', '-qm', 'Inert changed source'], cwd=self.source, check=True)
        self.assertEqual((self.candidates[0],), self.admit())

    def test_untracked_input_traversal_errors_make_admission_unavailable(self):
        failure = PermissionError('inert unreadable compilation subtree')

        def failed_walk(*args, onerror=None, **kwargs):
            if onerror is not None:
                onerror(failure)
            return iter(())

        with mock.patch.object(witness.os, 'walk', side_effect=failed_walk):
            with self.assertRaises(PermissionError) as caught:
                self.admit()
        self.assertIs(failure, caught.exception)


class PlannedTargetTests(unittest.TestCase):
    """Validate planned inputs only; producer first-boundary authority is separate."""

    def setUp(self):
        self.launch = {'runtime': '/trusted/NetCoreRuntime/dotnet',
                       'compiler': '/trusted/DotNetSdkRoslyn/csc.dll', 'target': 'StandaloneLinux64'}
        self.folder = 'Library/Bee/artifacts/1234E.dag/'
        self.rsp = self.folder + 'Game.Runtime.rsp'
        self.sources = ['Assets/Game/A.cs', 'Assets/Game/B.cs']
        self.payload = b'/langversion:9.0\n'
        self.graph = {witness.GRAPH_DATA: {
            'BuildTarget': 'StandaloneLinux64', 'OutputDirectory': 'Library/ScriptAssemblies',
            'BuildPlayerDataOutput': 'Library/BuildPlayerData/Editor', 'CodegenAssemblies': None,
            'Assemblies': [{'Name': 'Game.Runtime', 'LanguageVersion': '9.0', 'SourceFiles': self.sources}]}}
        self.dag = {'NamedNodes': {'ScriptAssemblies': 3}, 'Nodes': [
            {'ActionType': 'WriteFile', 'Outputs': [self.rsp], 'PayloadOffset': 0, 'PayloadLength': len(self.payload)},
            {'ActionType': 'WriteFile', 'Outputs': [self.rsp + '2'], 'PayloadOffset': len(self.payload), 'PayloadLength': 0},
            {'DisplayName': 'Csc Game.Runtime',
             'Action': '"' + self.launch['runtime'] + '" exec "' + self.launch['compiler'] +
                       '" /nostdlib /noconfig /shared "@' + self.rsp + '" "@' + self.rsp + '2"',
             'Inputs': self.sources + [self.rsp],
             'Outputs': [self.folder + 'Game.Runtime' + suffix for suffix in ('.dll', '.pdb', '.ref.dll')],
             'ToBuildDependencies': [0, 1], 'ToUseDependencies': [1],
             'FrontendResponseFiles': [self.rsp],
             'Env': [{'Key': 'DOTNET_MULTILEVEL_LOOKUP', 'Value': '0'}]},
            {'ToBuildDependencies': [2]}]}

    def validate(self):
        return witness.validate_planned_target(self.graph, self.dag, self.payload, self.folder, self.launch)

    def test_planned_command_links_exact_sources_outputs_and_response_payloads(self):
        result = self.validate()
        self.assertEqual(tuple(self.sources), result['candidate_sources'])
        self.assertEqual(['exec', self.launch['compiler'], '/nostdlib', '/noconfig', '/shared',
                          '@' + self.rsp, '@' + self.rsp + '2'], result['argv'])
        self.assertEqual([self.payload, b''], [base64.b64decode(row['contents_base64']) for row in result['responses']])

    def test_target_missing_from_initial_plan_cannot_be_filled_by_later_input(self):
        self.dag['Nodes'][2] = {}
        with self.assertRaises(ValueError):
            self.validate()

    def test_orphan_target_or_unwrapped_other_compiler_is_unavailable(self):
        self.dag['Nodes'][3]['ToBuildDependencies'] = []
        with self.assertRaises(ValueError):
            self.validate()
        self.dag['Nodes'][3]['ToBuildDependencies'] = [2]
        self.dag['Nodes'].append({'DisplayName': 'Csc Other',
                                 'Action': '"/unwrapped/dotnet" exec "other/csc.dll" @other.rsp'})
        with self.assertRaises(ValueError):
            self.validate()

    def test_selected_root_cannot_use_a_to_use_only_cycle_or_malformed_edge(self):
        root = self.dag['Nodes'][3]
        for edges in ([], [True], [len(self.dag['Nodes'])], [3]):
            root['ToBuildDependencies'] = edges
            root['ToUseDependencies'] = [2]
            with self.subTest(edges=edges), self.assertRaises(ValueError):
                self.validate()
        root['ToBuildDependencies'] = [2]
        self.dag['Nodes'][2]['ToBuildDependencies'].append(3)
        with self.assertRaises(ValueError):
            self.validate()

    def test_dag_input_order_is_not_compiler_response_order(self):
        # Actual Bee DAG and graph source orders differ; exact membership is the DAG contract.
        self.dag['Nodes'][2]['Inputs'] = list(reversed(self.sources)) + [self.rsp]
        self.assertEqual(tuple(self.sources), self.validate()['candidate_sources'])

    def test_command_output_source_and_dependency_changes_are_unavailable(self):
        node = self.dag['Nodes'][2]
        for field, value in (
                ('Action', node['Action'] + ' && arbitrary-command'),
                ('Outputs', [self.folder + 'Other.dll']),
                ('Inputs', [self.sources[0], self.rsp]),
                ('ToBuildDependencies', [0]), ('ToUseDependencies', []),
                ('FrontendResponseFiles', [self.rsp + '2']), ('Env', [])):
            old = node[field]
            node[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.validate()
            node[field] = old

    def test_response_writer_must_be_unique_input_free_and_within_payload(self):
        node = self.dag['Nodes'][0]
        for field, value in (('PayloadOffset', -1), ('PayloadOffset', True),
                             ('PayloadLength', len(self.payload) + 1), ('PayloadLength', False),
                             ('ActionType', 'Run'), ('Inputs', ['generated-input']), ('Action', 'arbitrary')):
            old = dict(node)
            node[field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                self.validate()
            node.clear()
            node.update(old)
        self.dag['Nodes'].append(dict(node))
        with self.assertRaises(ValueError):
            self.validate()

    def test_malformed_unrelated_node_outputs_are_unavailable(self):
        for outputs in (None, 1, {}, ['valid', []]):
            self.dag['Nodes'].append({'Outputs': outputs})
            with self.subTest(outputs=outputs), self.assertRaises(ValueError):
                self.validate()
            self.dag['Nodes'].pop()

    def test_malformed_compiler_display_name_is_unavailable(self):
        for name in ([], {}):
            self.dag['Nodes'][2]['DisplayName'] = name
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.validate()
        self.dag['Nodes'][2]['DisplayName'] = 'Csc Game.Runtime'

    def test_path_or_command_syntax_in_assembly_names_remains_unavailable(self):
        for name in ('../Assembly-CSharp', 'Assembly/CSharp', 'Assembly\\CSharp', 'Assembly-CSharp;echo'):
            row = {'Name': name, 'LanguageVersion': '9.0', 'SourceFiles': ['Assets/Other.cs']}
            node = dict(self.dag['Nodes'][2], DisplayName='Csc ' + name)
            node['Action'] = node['Action'].replace('Game.Runtime', name)
            self.graph[witness.GRAPH_DATA]['Assemblies'].append(row)
            self.dag['Nodes'].append(node)
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.validate()
            self.graph[witness.GRAPH_DATA]['Assemblies'].pop()
            self.dag['Nodes'].pop()


class InitialCaptureTests(unittest.TestCase):
    # Linux barrier probe: seal planned inputs before ANY csc forwards, never select a later prefix.
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.capture = self.root / 'capture'
        self.capture.mkdir()
        self.control = self.root / 'control'
        self.control.mkdir()
        fixture = PlannedTargetTests()
        fixture.setUp()
        self.launch = dict(fixture.launch, schema=1, nonce='1' * 32, control_root=str(self.control))
        self.folder = fixture.folder
        self.graph, self.dag, self.payload = fixture.graph, fixture.dag, fixture.payload
        self.first = {'schema': 1, 'sequence': 0, 'nonce': self.launch['nonce'],
                      'cwd': str(self.control), 'argv': ['exec', self.launch['compiler'],
                      '/nostdlib', '/noconfig', '/shared', '@' + self.folder + 'Game.Runtime.rsp',
                      '@' + self.folder + 'Game.Runtime.rsp2']}
        self.plan = {'schema': 1, 'sequence': 0, 'nonce': self.launch['nonce'], 'folder': self.folder,
                     'graph': self.blob(json.dumps(self.graph).encode()),
                     'dag': self.blob(json.dumps(self.dag).encode()), 'payload': self.blob(self.payload),
                     'responses': [dict(self.blob(self.payload), argument='@' + fixture.rsp),
                                   dict(self.blob(b''), argument='@' + fixture.rsp + '2')]}
        (self.capture / 'launch.json').write_text(json.dumps(self.launch))
        self.write_records()

    @staticmethod
    def blob(raw):
        return {'sha256': hashlib.sha256(raw).hexdigest(), 'contents_base64': base64.b64encode(raw).decode()}

    def write_records(self):
        (self.capture / 'first.json').write_text(json.dumps(self.first))
        raw = json.dumps(self.plan).encode()
        (self.capture / 'plan.json').write_bytes(raw)
        (self.capture / 'seal.json').write_text(json.dumps({'schema': 1, 'sequence': 0,
            'nonce': self.launch['nonce'], 'plan_sha256': hashlib.sha256(raw).hexdigest()}))

    def validate(self):
        return witness.validate_initial_capture(self.capture, self.launch)

    def test_sealed_first_boundary_selects_its_own_planned_target_options(self):
        result = self.validate()
        self.assertEqual(self.plan['responses'], result['responses'])
        self.assertEqual(('Assets/Game/A.cs', 'Assets/Game/B.cs'), result['candidate_sources'])
        self.assertEqual('9.0', result['assembly']['LanguageVersion'])

    def test_first_other_assembly_does_not_supply_target_options_or_sources(self):
        self.graph[witness.GRAPH_DATA]['Assemblies'].insert(0,
            {'Name': 'Dependency', 'LanguageVersion': '7.3', 'SourceFiles': ['Assets/Other.cs']})
        node = dict(self.dag['Nodes'][2])
        node.update(DisplayName='Csc Dependency', Action=node['Action'].replace('Game.Runtime', 'Dependency'),
                    Inputs=['Assets/Other.cs'], Outputs=[self.folder + 'Dependency.dll'])
        self.dag['Nodes'].append(node)
        self.first['argv'] = [value.replace('Game.Runtime', 'Dependency') for value in self.first['argv']]
        self.plan['graph'] = self.blob(json.dumps(self.graph).encode())
        self.plan['dag'] = self.blob(json.dumps(self.dag).encode())
        self.write_records()
        result = self.validate()
        self.assertEqual('@' + self.folder + 'Game.Runtime.rsp', result['argv'][5])
        self.assertEqual(('Assets/Game/A.cs', 'Assets/Game/B.cs'), result['candidate_sources'])
        self.assertEqual('9.0', result['assembly']['LanguageVersion'])

    def test_identical_later_capture_cannot_replace_initial_target(self):
        original = dict(self.plan)
        self.plan['sequence'] = 1
        self.assertEqual(original['graph'], self.plan['graph'])
        self.assertEqual(original['responses'], self.plan['responses'])
        self.write_records()
        with self.assertRaises(ValueError):
            self.validate()

    def test_incremental_later_fill_cannot_complete_missing_initial_target(self):
        (self.capture / 'first.json').unlink()
        # A complete later plan and seal do not replace the absent first boundary.
        with self.assertRaises(ValueError):
            self.validate()
        self.write_records()
        self.first['sequence'] = 1
        self.write_records()
        with self.assertRaises(ValueError):
            self.validate()

    def test_missing_duplicate_partial_or_failed_capture_is_unavailable(self):
        for name in ('first.json', 'plan.json', 'seal.json'):
            raw = (self.capture / name).read_bytes()
            (self.capture / name).unlink()
            with self.subTest(missing=name), self.assertRaises(ValueError):
                self.validate()
            (self.capture / name).write_bytes(raw)
        for name in ('plan.partial', 'error.json', 'record-000001.json', 'candidate.json'):
            path = self.capture / name
            path.write_text('incomplete or later')
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.validate()
            path.unlink()

    def test_wrong_launch_graph_response_or_compiler_identity_is_unavailable(self):
        for field, value in (('nonce', '2' * 32), ('cwd', str(self.root)),
                             ('argv', ['exec', '/other/csc.dll']), ('sequence', 2)):
            old = self.first[field]
            self.first[field] = value
            self.write_records()
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.validate()
            self.first[field] = old
        self.plan['responses'][0] = dict(self.blob(b'changed'), argument=self.plan['responses'][0]['argument'])
        self.write_records()
        with self.assertRaises(ValueError):
            self.validate()
        self.plan['responses'][0] = dict(self.blob(self.payload), argument=self.plan['responses'][0]['argument'])
        self.graph[witness.GRAPH_DATA]['BuildTarget'] = 'StandaloneOSX'
        self.plan['graph'] = self.blob(json.dumps(self.graph).encode())
        self.write_records()
        with self.assertRaises(ValueError):
            self.validate()

    def test_capture_schema_and_sequence_require_integers_not_boolean_aliases(self):
        for record in (self.first, self.plan):
            for field in ('schema', 'sequence'):
                old = record[field]
                record[field] = bool(old)
                self.write_records()
                with self.subTest(field=field), self.assertRaises(ValueError):
                    self.validate()
                record[field] = old

    def test_malformed_assembly_names_are_unavailable_not_unhandled_type_errors(self):
        for value in ([], {}, None, True):
            with self.subTest(value=value):
                malformed = json.loads(json.dumps(self.graph))
                malformed[witness.GRAPH_DATA]['Assemblies'][0]['Name'] = value
                self.plan['graph'] = self.blob(json.dumps(malformed).encode())
                self.write_records()
                with self.assertRaises(ValueError):
                    self.validate()


class CaptureProducerTests(unittest.TestCase):
    """Real isolated Python wrapper processes; inert plan inputs are never compiled."""
    blob = staticmethod(InitialCaptureTests.blob)
    write_records = InitialCaptureTests.write_records
    validate = InitialCaptureTests.validate

    def setUp(self):
        InitialCaptureTests.setUp(self)
        for name in ('first.json', 'plan.json', 'seal.json'):
            (self.capture / name).unlink()
        self.dag_name = self.folder.split('/')[-2]
        self.dag['PayloadsFile'] = 'Library/Bee/' + self.dag_name + '.payloads'
        self.write_control_inputs()
        self.forwarded = self.root / 'unexpected-forward'
        self.alias = self.root / 'runtime-alias'
        self.alias.write_text('#!' + sys.executable + '\nfrom pathlib import Path\n'
                              + 'Path(' + repr(str(self.forwarded)) + ').write_text("forwarded")\n')
        self.alias.chmod(0o755)

    def write_control_inputs(self):
        files = {
            'Library/Bee/' + self.dag_name.removesuffix('.dag') + '-inputdata.json': json.dumps(self.graph).encode(),
            'Library/Bee/' + self.dag_name + '.json': json.dumps(self.dag).encode(),
            self.dag['PayloadsFile']: self.payload,
            self.folder + 'Game.Runtime.rsp': self.payload,
            self.folder + 'Game.Runtime.rsp2': b'',
        }
        for relative, raw in files.items():
            path = self.control / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)

    def command(self, arguments=None):
        return [sys.executable, '-I', str(Path(witness.__file__).resolve()),
                '--capture', str(self.capture), '--runtime', str(self.alias), '--',
                *(self.first['argv'] if arguments is None else arguments)]

    def invoke(self, arguments=None):
        return subprocess.run(self.command(arguments),
                              cwd=self.control, text=True, capture_output=True, timeout=10)

    def test_real_wrapper_seals_first_actual_inputs_and_never_forwards_any_csc(self):
        result = self.invoke()
        self.assertEqual(70, result.returncode, result.stderr)
        self.assertFalse(self.forwarded.exists())
        self.assertEqual(tuple(self.graph[witness.GRAPH_DATA]['Assemblies'][0]['SourceFiles']),
                         self.validate()['candidate_sources'])
        before = {path.name: path.read_bytes() for path in self.capture.iterdir()}
        (self.control / (self.folder + 'Game.Runtime.rsp')).write_bytes(b'later changed response')
        self.assertEqual(70, self.invoke().returncode)
        self.assertEqual(before, {path.name: path.read_bytes() for path in self.capture.iterdir()})
        self.assertFalse(self.forwarded.exists())

    def test_real_wrapper_accepts_qualified_unity_default_assembly_names(self):
        # The observed Linux fallback Editor graph contains these Unity default names.
        for name in ('Assembly-CSharp', 'Assembly-CSharp-Editor', 'Assembly-CSharp-firstpass'):
            self.graph[witness.GRAPH_DATA]['Assemblies'].append(
                {'Name': name, 'LanguageVersion': '9.0', 'SourceFiles': ['Assets/Other.cs']})
            node = dict(self.dag['Nodes'][2])
            node.update(DisplayName='Csc ' + name, Action=node['Action'].replace('Game.Runtime', name),
                        Inputs=['Assets/Other.cs'], Outputs=[self.folder + name + '.dll'])
            self.dag['Nodes'].append(node)
        self.first['argv'] = [value.replace('Game.Runtime', 'Assembly-CSharp-Editor')
                              for value in self.first['argv']]
        self.write_control_inputs()
        self.assertEqual(70, self.invoke().returncode)
        self.assertTrue((self.capture / 'seal.json').is_file())
        self.assertFalse((self.capture / 'error.json').exists())
        self.assertFalse(self.forwarded.exists())
        result = self.validate()
        self.assertEqual('@' + self.folder + 'Game.Runtime.rsp', result['argv'][5])
        self.assertEqual(('Assets/Game/A.cs', 'Assets/Game/B.cs'), result['candidate_sources'])

    def test_real_wrapper_failed_first_attempt_cannot_be_filled_by_later_files(self):
        missing = self.control / (self.folder + 'Game.Runtime.rsp2')
        missing.unlink()
        self.assertEqual(70, self.invoke().returncode)
        self.assertTrue((self.capture / 'first.json').is_file())
        self.assertTrue((self.capture / 'error.json').is_file())
        self.assertFalse((self.capture / 'seal.json').exists())
        before = {path.name: path.read_bytes() for path in self.capture.iterdir()}
        missing.write_bytes(b'')
        self.assertEqual(70, self.invoke().returncode)
        self.assertEqual(before, {path.name: path.read_bytes() for path in self.capture.iterdir()})
        self.assertFalse(self.forwarded.exists())
        with self.assertRaises(ValueError):
            self.validate()

    def test_noncompiler_tool_launch_keeps_forwarding_contract(self):
        self.assertEqual(0, self.invoke(['unrelated-trusted-tool']).returncode)
        self.assertEqual('forwarded', self.forwarded.read_text())
        self.assertFalse((self.capture / 'first.json').exists())

    def test_exact_absolute_dag_payload_path_uses_same_control_file(self):
        self.dag['PayloadsFile'] = str(self.control / self.dag['PayloadsFile'])
        self.write_control_inputs()
        self.assertEqual(70, self.invoke().returncode)
        self.assertEqual('9.0', self.validate()['assembly']['LanguageVersion'])
        self.assertFalse(self.forwarded.exists())

    def test_concurrent_compilers_share_one_first_attempt_and_never_forward(self):
        processes = []
        try:
            for _ in range(4):
                processes.append(subprocess.Popen(self.command(), cwd=self.control,
                                 stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True))
            for process in processes:
                _, stderr = process.communicate(timeout=10)
                self.assertEqual(70, process.returncode, stderr)
            self.assertFalse(self.forwarded.exists())
            self.assertEqual({'launch.json', 'capture.lock', 'first.json', 'plan.json', 'seal.json'},
                             {path.name for path in self.capture.iterdir()})
            self.assertEqual(0, json.loads((self.capture / 'first.json').read_bytes())['sequence'])
            self.assertEqual('9.0', self.validate()['assembly']['LanguageVersion'])
        finally:
            for process in processes:
                if process.poll() is None:
                    process.kill()
                process.communicate(timeout=10)

    def test_unqualified_csc_path_never_forwards_or_creates_positive_capture(self):
        argv = list(self.first['argv'])
        argv[1] = '/unqualified/csc.dll'
        self.assertEqual(70, self.invoke(argv).returncode)
        self.assertFalse(self.forwarded.exists())
        with self.assertRaises(ValueError):
            self.validate()


if __name__ == '__main__':
    unittest.main()

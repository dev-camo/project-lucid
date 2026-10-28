"""Optional witness budget and ownership, independent of normal compilation."""
import inspect
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock

import compiler_precheck as precheck
import process_registry


class OptionalPrecheckTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.source, self.control = self.root / 'source', self.root / 'control'
        self.source.mkdir()
        self.control.mkdir()
        self.identity = {'head': '1' * 40, 'control_commit': '2' * 40,
                         'source_fingerprint': '3' * 64}

    def receipt(self, positive=True):
        return dict(self.identity, status='syntax_error' if positive else 'ready',
                    diagnostics=['CS1525'] if positive else [], package_identity='4' * 64)

    def run_case(self, result=None, error=None, caller_failure=None):
        states, calls = [], []
        def execute(command, source, errors, marker, timeout, *, registry):
            self.assertEqual(self.source, source)
            self.assertEqual(precheck.PRECHECK_SECONDS, timeout)
            self.assertEqual((self.root / 'job.json', 100), registry)
            state = Path(command[command.index('--state') + 1])
            states.append(state)
            (state / 'package-seed').mkdir()
            (state / 'package-seed/inert').write_text('complete seed fixture')
            if error is not None:
                raise error
            return json.dumps(result).encode()
        with mock.patch.object(precheck.shared, 'run_compiler', side_effect=execute) as run, \
                mock.patch.object(precheck.shared, 'require_stopped_marker') as stopped:
            try:
                with precheck.precheck_resources(self.source, self.control, self.identity,
                                                registry=(self.root / 'job.json', 100)) as answer:
                    calls.append('normal compilation')
                    self.assertTrue(states[0].exists())
                    if answer is not None:
                        self.assertEqual(states[0] / 'package-seed', answer['package_seed'])
                        self.assertTrue(answer['package_seed'].exists())
                    if caller_failure is not None:
                        raise caller_failure
            except BaseException as caught:
                if caller_failure is None:
                    raise
                self.assertIs(caller_failure, caught)
            else:
                self.assertIsNone(caller_failure)
        self.assertEqual(['normal compilation'], calls)
        self.assertEqual(1, run.call_count)
        self.assertGreaterEqual(stopped.call_count, 2)
        self.assertEqual([], list(self.root.glob('syntax-precheck-*')))
        return answer

    def test_only_exact_trusted_positive_receipt_is_positive(self):
        good = self.receipt()
        self.assertTrue(self.run_case(good)['syntax_error'])
        self.assertFalse(self.run_case(self.receipt(False))['syntax_error'])
        for result in (None, [], {}, dict(good, status='other'), dict(good, head='5' * 40),
                       dict(good, diagnostics=[]), dict(good, diagnostics=['forged text']),
                       dict(good, package_identity='wrong'), dict(good, extra='unexpected')):
            with self.subTest(result=result):
                self.assertIsNone(self.run_case(result))

    def test_optional_exit_and_timeout_release_scratch_without_changing_normal_budget(self):
        for error in (subprocess.CalledProcessError(70, ['inert']),
                      subprocess.TimeoutExpired(['inert'], precheck.PRECHECK_SECONDS)):
            with self.subTest(error=type(error).__name__):
                self.assertIsNone(self.run_case(error=error))
        self.assertEqual(38 * 60, inspect.signature(precheck.shared.run_compiler).parameters['timeout'].default)

    def test_failed_process_custody_prevents_cleanup_and_normal_fallback(self):
        failure = ValueError('owned group still active')
        with mock.patch.object(precheck.shared, 'run_compiler', return_value=b'{}'), \
                mock.patch.object(precheck.shared, 'require_stopped_marker', side_effect=failure):
            with self.assertRaises(ValueError) as caught:
                with precheck.precheck_resources(self.source, self.control, self.identity,
                                                registry=(self.root / 'job.json', 100)):
                    self.fail('Surviving registered groups cannot fall back')
        self.assertIs(failure, caught.exception)
        self.assertEqual(1, len(list(self.root.glob('syntax-precheck-*'))))

    def test_nested_capture_custody_is_checked_before_optional_fallback(self):
        failure = ValueError('nested capture group still active')
        def execute(command, source, errors, marker, timeout, *, registry):
            (marker.parent / 'capture-group.json').write_text('{"group":123}')
            return b'{}'
        def stopped(marker):
            if marker.name == 'capture-group.json':
                raise failure
        with mock.patch.object(precheck.shared, 'run_compiler', side_effect=execute), \
                mock.patch.object(precheck.shared, 'require_stopped_marker', side_effect=stopped):
            with self.assertRaises(ValueError) as caught:
                with precheck.precheck_resources(self.source, self.control, self.identity,
                                                registry=(self.root / 'job.json', 100)):
                    self.fail('Nested capture must stop first')
        self.assertIs(failure, caught.exception)
        self.assertEqual(1, len(list(self.root.glob('syntax-precheck-*'))))

    def test_no_change_and_unavailable_preparation_preserve_exact_caller_failure(self):
        # Preparation is now wholly watchdog-owned; retain the exact caller
        # exception and single execution for unavailable and ready outcomes.
        for result in ({'status': 'unavailable'}, self.receipt(False)):
            for failure in (ValueError('normal receipt failure'), OSError('normal IO failure'),
                            subprocess.CalledProcessError(1, ['normal compiler'])):
                with self.subTest(result=result['status'], failure=type(failure).__name__):
                    self.run_case(result, caller_failure=failure)

    def test_parent_does_not_copy_or_hash_optional_inputs_outside_child_budget(self):
        with mock.patch.object(precheck, 'tree_identity', side_effect=AssertionError('unbounded parent hash')), \
                mock.patch.object(precheck.shutil, 'copytree', side_effect=AssertionError('unbounded parent copy')), \
                mock.patch.object(precheck, 'installed_tool_closure', side_effect=AssertionError('unbounded parent tools')):
            self.assertTrue(self.run_case(self.receipt())['syntax_error'])

    def test_optional_scratch_setup_failure_preserves_normal_caller_exception(self):
        failure = ValueError('normal failure')
        with mock.patch.object(precheck.tempfile, 'mkdtemp', side_effect=OSError('optional setup unavailable')):
            with self.assertRaises(ValueError) as caught:
                with precheck.precheck_resources(self.source, self.control, self.identity,
                                                registry=(self.root / 'job.json', 100)) as result:
                    self.assertIsNone(result)
                    raise failure
        self.assertIs(failure, caught.exception)

    def test_cleanup_has_its_own_bound_and_failure_cannot_replace_normal_result(self):
        with mock.patch.object(precheck.subprocess, 'run', side_effect=subprocess.TimeoutExpired(['cleanup'], 60)) as cleanup:
            # A failed optional deletion retains evidence; no caller outcome is rewritten.
            with mock.patch.object(precheck.shared, 'run_compiler', return_value=b'{}'), \
                    mock.patch.object(precheck.shared, 'require_stopped_marker'):
                with precheck.precheck_resources(self.source, self.control, self.identity,
                                                registry=(self.root / 'job.json', 100)) as result:
                    self.assertIsNone(result)
            self.assertEqual(60, cleanup.call_args.kwargs['timeout'])
            self.assertEqual(1, len(list(self.root.glob('syntax-precheck-*'))))

    def test_actual_slow_preparation_is_stopped_before_normal_fallback(self):
        script = self.control / '.github/scripts/compiler_precheck.py'
        script.parent.mkdir(parents=True)
        script.write_text('import sys,time\nfrom pathlib import Path\n'
                          'state=Path(sys.argv[sys.argv.index("--state")+1])\n'
                          '(state/"package-seed").mkdir()\n'
                          '(state/"preparation-started").write_text("ready")\n'
                          'time.sleep(30)\n')
        marker = self.root / 'job.json'
        process_registry.merge_registry(marker, os.getpgrp(), [], create=True)
        started = time.monotonic()
        with mock.patch.object(precheck, 'PRECHECK_SECONDS', .3):
            with precheck.precheck_resources(self.source, self.control, self.identity,
                                            registry=(marker, os.getpgrp())) as result:
                self.assertIsNone(result)
                state, = self.root.glob('syntax-precheck-*')
                self.assertEqual('ready', (state / 'preparation-started').read_text())
                # Normal work starts only after the registered optional process stopped.
                for group in json.loads(marker.read_bytes())['groups']:
                    if group != os.getpgrp():
                        self.assertFalse(precheck.shared.group_running(group))
        self.assertLess(time.monotonic() - started, 10)
        self.assertEqual([], list(self.root.glob('syntax-precheck-*')))

    def test_child_closure_change_suppresses_a_completed_positive_result(self):
        state = self.root / 'child'
        state.mkdir()
        output = io.StringIO()
        errors = io.StringIO()
        arguments = ['compiler_precheck.py', '--source', str(self.source), '--control', str(self.control),
                     '--state', str(state), '--job-marker', str(self.root / 'job.json'), '--job-root', '100']
        for name, value in self.identity.items():
            arguments += ['--' + name.replace('_', '-'), value]
        with mock.patch.object(sys, 'argv', arguments), \
                mock.patch.object(precheck.Path, 'home', return_value=self.root), \
                mock.patch.object(precheck.witness, '_tracked_inventory', return_value={}), \
                mock.patch.object(precheck.witness, 'admit_source_changes', return_value=('Assets/A.cs',)), \
                mock.patch.object(precheck, 'installed_tool_closure', side_effect=[{'tool': 'before'}, {'tool': 'changed'}]), \
                mock.patch.object(precheck.sandbox, 'licensing_broker', return_value=contextlib.nullcontext(('owned', []))), \
                mock.patch.object(precheck, 'produce_precheck', return_value=self.receipt()) as produce, \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            precheck.main()
        produce.assert_called_once()
        self.assertEqual({'status': 'unavailable'}, json.loads(output.getvalue()))
        diagnostic = json.loads(errors.getvalue())
        self.assertEqual('verify-resources-and-custody', diagnostic['stage'])
        self.assertEqual('ValueError', diagnostic['error_type'])
        self.assertEqual('Witness package or tool closure changed during use', diagnostic['message'])

    def test_real_producer_tool_measurements_enclose_use_and_broker_teardown(self):
        paths = mock.patch.object(sys, 'path', [str(Path(__file__).resolve().parents[2] / 'tools'), *sys.path])
        paths.start()
        self.addCleanup(paths.stop)
        from lucidlib import verification

        for mutation in (None, 'capture', 'broker teardown'):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory(dir=self.root) as directory:
                state = Path(directory)
                tool = state / 'installed-tool'
                tool.write_bytes(b'original tool bytes')
                relative = 'Assets/A.cs'
                project = state / 'project'
                original = project / relative
                original.parent.mkdir(parents=True)
                original.write_text('class A {}')
                (project / 'Library/PackageCache').mkdir(parents=True)
                inventory = {relative: ('100644', hashlib.sha256(original.read_bytes()).hexdigest())}
                events = []

                class Output(io.StringIO):
                    def write(self, value):
                        if value.startswith('{'):
                            events.append('publish')
                        return super().write(value)

                output, errors = Output(), io.StringIO()

                def measure():
                    events.append('measure tools')
                    return precheck.tree_identity(tool)

                @contextlib.contextmanager
                def broker(*args, **kwargs):
                    events.append('broker enter')
                    try:
                        yield 'owned', []
                    finally:
                        events.append('broker exit')
                        if mutation == 'broker teardown':
                            tool.write_bytes(b'changed tool bytes')

                def capture(*args):
                    events.append('capture')
                    if mutation == 'capture':
                        tool.write_bytes(b'changed tool bytes')
                    return project, {'candidate_sources': [relative],
                                     'resolved_packages': precheck.tree_identity(project / 'Library/PackageCache')}

                def parse(*args):
                    events.append('parse')
                    return ['CS1525']

                arguments = ['compiler_precheck.py', '--source', str(self.source), '--control', str(self.control),
                             '--state', str(state), '--job-marker', str(self.root / 'job.json'), '--job-root', '100']
                for name, value in self.identity.items():
                    arguments += ['--' + name.replace('_', '-'), value]
                with mock.patch.object(sys, 'argv', arguments), \
                        mock.patch.object(precheck.Path, 'home', return_value=self.root), \
                        mock.patch.object(precheck.witness, '_tracked_inventory', return_value=inventory), \
                        mock.patch.object(precheck, 'game_runtime_roster', return_value=[relative]), \
                        mock.patch.object(precheck.witness, 'admit_source_changes', return_value=(relative,)), \
                        mock.patch.object(precheck.shared, 'git_head', side_effect=lambda path: self.identity['head' if path == self.source else 'control_commit']), \
                        mock.patch.object(precheck.shared.driver, 'require_control_reporting_tools'), \
                        mock.patch.object(verification, 'artifact_fingerprint', return_value=self.identity['source_fingerprint']), \
                        mock.patch.object(precheck, 'installed_tool_closure', side_effect=measure), \
                        mock.patch.object(precheck.sandbox, 'licensing_broker', side_effect=broker), \
                        mock.patch.object(precheck, 'capture_plan', side_effect=capture), \
                        mock.patch.object(precheck, 'parse_sources', side_effect=parse), \
                        contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
                    precheck.main()
                self.assertLess(events.index('broker exit'), events.index('publish'))
                # Even mutation cases complete parsing; the enclosing postcheck
                # suppresses that positive only after the broker has stopped.
                self.assertEqual(['measure tools', 'broker enter', 'capture', 'parse',
                                  'broker exit', 'measure tools', 'publish'], events)
                if mutation:
                    self.assertEqual({'status': 'unavailable'}, json.loads(output.getvalue()))
                    diagnostic = json.loads(errors.getvalue())
                    self.assertEqual('ValueError', diagnostic['error_type'])
                    self.assertIn('closure changed', diagnostic['message'])
                else:
                    self.assertEqual(dict(self.identity, status='syntax_error', diagnostics=['CS1525'],
                                          package_identity=precheck.identity_digest(precheck.tree_identity(state / 'package-seed'))),
                                     json.loads(output.getvalue()))
                    self.assertEqual('', errors.getvalue())

    def test_non_runtime_change_is_unavailable_before_any_preparation(self):
        # An Editor or test source is outside the Game.Runtime roster, so no child
        # resource, broker, capture or Editor may be started for it.
        def write(root, editor_text):
            for relative, text in (('Assets/Scripts/RecoveredProof/Game.Runtime.asmdef', '{"name": "Game.Runtime"}'),
                                   ('Assets/Scripts/RecoveredProof/A.cs', 'class A {}'),
                                   ('Assets/Editor/E.cs', editor_text), ('Packages/manifest.json', '{}'),
                                   ('ProjectSettings/ProjectVersion.txt', 'x')):
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text)
            subprocess.run(['git', 'init', '-q'], cwd=root, check=True)
            subprocess.run(['git', '-c', 'user.name=t', '-c', 'user.email=t@t', 'add', '-A'], cwd=root, check=True)
            subprocess.run(['git', '-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-qm', 'x'], cwd=root, check=True)
        write(self.control, 'class E {}')
        write(self.source, 'class E { int changed; }')
        self.assertEqual(['Assets/Scripts/RecoveredProof/A.cs'],
                         precheck.game_runtime_roster(self.control, precheck.witness._tracked_inventory(self.control)))
        state = self.root / 'child'
        state.mkdir()
        output, errors = io.StringIO(), io.StringIO()
        arguments = ['compiler_precheck.py', '--source', str(self.source), '--control', str(self.control),
                     '--state', str(state), '--job-marker', str(self.root / 'job.json'), '--job-root', '100']
        for name, value in self.identity.items():
            arguments += ['--' + name.replace('_', '-'), value]
        with mock.patch.object(sys, 'argv', arguments), \
                mock.patch.object(precheck, 'child_resources', side_effect=AssertionError('prepared')) as prepared, \
                mock.patch.object(precheck, 'capture_plan', side_effect=AssertionError('launched')) as launched, \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            precheck.main()
        prepared.assert_not_called()
        launched.assert_not_called()
        self.assertEqual({'status': 'unavailable'}, json.loads(output.getvalue()))
        self.assertEqual('admission', json.loads(errors.getvalue())['stage'])

    def test_roster_follows_asmdef_asmref_ownership(self):
        files = {'Assets/A/Game.Runtime.asmdef': '{"name": "Game.Runtime"}',
                 'Assets/A/Game.Runtime.asmdef.meta': 'guid: ' + '1' * 32 + '\n',
                 'Assets/A/X.cs': '', 'Assets/A/Sub/Y.cs': '',
                 'Assets/A/Other/Other.asmdef': '{"name": "Other"}', 'Assets/A/Other/Z.cs': '',
                 'Assets/B/Game.Runtime.asmref': '{"reference": "GUID:' + '1' * 32 + '"}',
                 'Assets/B/W.cs': '', 'Assets/C/V.cs': '', 'Assets/A/Hidden~/H.cs': ''}
        for relative, text in files.items():
            path = self.control / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)
        self.assertEqual(['Assets/A/Sub/Y.cs', 'Assets/A/X.cs', 'Assets/B/W.cs'],
                         precheck.game_runtime_roster(self.control, files))

    def test_parent_timeout_diagnostic_is_bounded_private_and_neutral(self):
        failure = subprocess.TimeoutExpired(['inert-' + 'x' * 4096], precheck.PRECHECK_SECONDS)
        self.assertIsNone(self.run_case(error=failure))
        retained = (self.root / 'private-syntax-precheck.log').read_text()
        diagnostic = json.loads(retained.split('private-precheck.log\n', 1)[1].strip())
        self.assertEqual('optional-child', diagnostic['stage'])
        self.assertEqual('TimeoutExpired', diagnostic['error_type'])
        self.assertEqual(2048, len(diagnostic['message']))


class InputClosureTests(unittest.TestCase):
    def test_closure_detects_bytes_roster_and_executable_mode_changes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            path = root / 'tool'
            path.write_bytes(b'original')
            original = precheck.tree_identity(root)
            path.write_bytes(b'modified')
            self.assertNotEqual(original, precheck.tree_identity(root))
            path.write_bytes(b'original')
            path.chmod(0o755)
            self.assertNotEqual(original, precheck.tree_identity(root))
            path.chmod(0o644)
            (root / 'extra').write_bytes(b'new dependency')
            self.assertNotEqual(original, precheck.tree_identity(root))

    def test_package_seed_rejects_links_and_tool_links_must_stay_in_the_measured_tree(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve() / 'tree'
            root.mkdir()
            (root / 'actual').write_bytes(b'owned tool')
            (root / 'alias').symlink_to('actual')
            with self.assertRaises(ValueError):
                precheck.tree_identity(root)
            result = precheck.tree_identity(root, allow_internal_links=True)
            self.assertIn('actual', result)
            self.assertIn('alias', result)
            (root / 'alias').unlink()
            (root.parent / 'outside').write_bytes(b'foreign input')
            (root / 'alias').symlink_to('../outside')
            with self.assertRaises(ValueError):
                precheck.tree_identity(root, allow_internal_links=True)


if __name__ == '__main__':
    unittest.main()

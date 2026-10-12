"""Main publication and PR validation boundaries; all API/process results are inert."""
import base64
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from contextlib import ExitStack

sys.path.insert(0, str(Path(__file__).parent))
import main_progress as main


class API:
    def __init__(self):
        self.parent = 'a' * 40
        self.head = self.parent
        self.generated = 'b' * 40
        self.published = 'c' * 40
        self.calls = []
        self.refs = {'main': self.head}
        self.branch = None
        self.pr = None
        self.hook = None
        self.checks = [{'name': name, 'head_sha': self.generated, 'status': 'completed', 'conclusion': 'success',
                        'app': {'id': main.CHECK_APP}} for name in sorted(main.CHECKS)]
        self.identity = {'name': main.shared.BOT, 'email': '41898282+github-actions[bot]@users.noreply.github.com'}
        self.tree = None

    def request(self, path, method='GET', body=None):
        call = {'path': path, 'method': method, 'body': copy.deepcopy(body)}
        self.calls.append(call)
        if self.hook:
            self.hook(call)
        repo = '/repos/' + main.REPOSITORY
        if path == repo:
            return {'full_name': main.REPOSITORY, 'default_branch': 'main'}
        if path == repo + '/commits/main':
            return {'sha': self.head}
        if path == '/users/github-actions%5Bbot%5D':
            return {'login': main.shared.BOT, 'type': 'Bot', 'id': 41898282}
        if path == repo + '/git/commits/' + self.parent:
            return {'tree': {'sha': 'd' * 40}}
        if path == repo + '/git/blobs':
            return {'sha': 'e' * 40}
        if path == repo + '/git/trees':
            self.tree = body
            return {'sha': 'f' * 40}
        if path == repo + '/git/commits' and method == 'POST':
            return {'sha': self.generated}
        if path == repo + '/git/refs' and method == 'POST':
            self.branch = body['ref'][len('refs/heads/'):]
            self.refs[self.branch] = body['sha']
            return {'object': {'sha': body['sha']}}
        if '/git/refs/heads/' in path:
            import urllib.parse
            branch = urllib.parse.unquote(path.split('/git/refs/heads/', 1)[1])
            if method != 'GET':
                raise AssertionError('Never force/direct-update protected main or use a racy API ref deletion')
            return {'object': {'sha': self.refs[branch]}}
        if path == repo + '/pulls' and method == 'POST':
            self.pr = {'number': 12, 'state': 'open', 'user': {'login': main.shared.BOT, 'id': 41898282},
                       'head': {'sha': self.generated, 'ref': self.branch, 'repo': {'full_name': main.REPOSITORY}},
                       'base': {'sha': self.parent, 'ref': 'main', 'repo': {'full_name': main.REPOSITORY}}}
            return copy.deepcopy(self.pr)
        if path == repo + '/pulls/12':
            if method == 'PATCH':
                self.pr.update(body)
            return copy.deepcopy(self.pr)
        if path == repo + '/pulls/12/merge':
            if self.head != self.parent:
                raise ValueError('Protected strict checks reject a behind PR')
            if body != {'sha': self.generated, 'merge_method': 'rebase'}:
                raise AssertionError('Only exact-head rebase publication')
            self.head = self.published
            self.refs['main'] = self.published
            self.pr.update(state='closed', merged=True)
            return {'merged': True, 'sha': self.published}
        if path == repo + '/git/commits/' + self.published:
            return {'tree': {'sha': 'f' * 40}, 'parents': [{'sha': self.parent}],
                    'author': self.identity, 'committer': self.identity}
        if '/actions/workflows/' in path and path.endswith('/dispatches'):
            return None  # Normal GitHub 204; dispatch success is not test success.
        if path == repo + '/commits/' + self.generated + '/check-runs?per_page=100':
            return {'check_runs': copy.deepcopy(self.checks)}
        raise AssertionError('Unexpected API operation: ' + method + ' ' + path)


class MainProgressTests(unittest.TestCase):
    def setUp(self):
        self.api = API()
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name).resolve()
        self.source, self.control = self.base / 'source', self.base / 'control'
        for root in (self.source, self.control):
            (root / 'docs').mkdir(parents=True)
        self.report = {'source_fingerprint': '1' * 64, 'compiler_receipt_sha256': '2' * 64,
                       'input_binary_sha256': '3' * 64, 'input_metadata_sha256': '4' * 64}
        (self.control / 'docs/code-progress.json').write_text(json.dumps(self.report))
        (self.source / 'docs/code-progress.json').write_bytes(b'old snapshot')
        (self.source / 'README.md').write_text('human before\nOLD\nhuman after\n')
        self.raw = json.dumps(self.report).encode()
        self.request = {'head': self.api.parent, 'control_commit': self.api.parent}
        self.producer = {**self.request, 'image': main.shared.EDITOR, 'run_id': '456',
                         'source_fingerprint': self.report['source_fingerprint'],
                         'report_sha256': hashlib.sha256(self.raw).hexdigest(),
                         'compiler_receipt_sha256': self.report['compiler_receipt_sha256']}
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.stack.enter_context(mock.patch.dict(os.environ, {
            'GITHUB_REPOSITORY': main.REPOSITORY, 'GITHUB_REF': 'refs/heads/main', 'GITHUB_EVENT_NAME': 'push',
            'GITHUB_SHA': self.api.parent, 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_RUN_ID': '456', 'GH_TOKEN': 'inert-fixture'}))
        self.stack.enter_context(mock.patch.object(main.shared, 'git_head', return_value=self.api.parent))
        self.reporting = self.stack.enter_context(mock.patch.object(main.shared, 'reporting_tools', return_value=(
            self.validate, lambda report: 'NEW\n', lambda text: ('human before\n', 'OLD', '\nhuman after\n'))))
        self.control_check = self.stack.enter_context(mock.patch.object(main.shared.driver, 'require_control_reporting_tools'))
        self.cleanup = self.stack.enter_context(mock.patch.object(main, 'cleanup_check_ref'))

    def validate(self, report, source):
        if set(report) != set(self.report) or report['source_fingerprint'] != self.report['source_fingerprint']:
            raise ValueError('Synthetic public report shape or source differs')

    def event(self):
        return {'repository': {'full_name': main.REPOSITORY}, 'ref': 'refs/heads/main', 'after': self.api.parent}

    def publish(self, producer=None, request=None, raw=None):
        return main.publish(self.api, self.request if request is None else request,
                            self.raw if raw is None else raw, self.producer if producer is None else producer,
                            self.control, self.source)

    def mutations(self):
        return [call for call in self.api.calls if call['method'] != 'GET']

    def test_exact_main_gate_and_stale_main_skip(self):
        self.assertEqual(self.request, main.gate(self.api, self.event()))
        self.api.head = '9' * 40
        self.assertIsNone(main.gate(self.api, self.event()))
        self.assertFalse(self.mutations())

    def test_pr_workflow_run_nonmain_foreign_repo_and_rerun_reject_before_api(self):
        for key, value in (('GITHUB_EVENT_NAME', 'pull_request'), ('GITHUB_EVENT_NAME', 'workflow_run'),
                           ('GITHUB_REF', 'refs/heads/contributor'), ('GITHUB_REPOSITORY', 'other/repo'),
                           ('GITHUB_RUN_ATTEMPT', '2')):
            self.api.calls.clear()
            with self.subTest(key=key, value=value), mock.patch.dict(os.environ, {key: value}), self.assertRaises(ValueError):
                main.gate(self.api, self.event())
            self.assertFalse(self.api.calls)

    def test_event_payload_cannot_supply_a_fork_or_other_ref(self):
        for event in ({'repository': {'full_name': 'fork/project-lucid'}},
                      {**self.event(), 'ref': 'refs/heads/fork'}, {**self.event(), 'after': '5' * 40}):
            with self.assertRaises(ValueError):
                main.gate(self.api, event)
        self.assertFalse(self.mutations())

    def test_main_manual_dispatch_accepts_only_main_context(self):
        with mock.patch.dict(os.environ, {'GITHUB_EVENT_NAME': 'workflow_dispatch'}):
            self.assertEqual(self.request, main.gate(self.api, {'repository': {'full_name': main.REPOSITORY}}))

    def test_report_producer_tamper_and_oversize_reject_before_writes(self):
        for key in main.shared.PRODUCER_FIELDS:
            producer = dict(self.producer, **{key: 'tampered'})
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.publish(producer=producer)
        with self.assertRaises(ValueError):
            self.publish(raw=b' ' * (main.shared.MAX_REPORT_BYTES + 1))
        self.assertFalse(self.mutations())

    def test_pr_shaped_request_cannot_reach_main_publication(self):
        with self.assertRaises(ValueError):
            self.publish(request={**self.request, 'pr': 12})
        self.assertFalse(self.api.calls)

    def test_main_advance_before_publication_preserves_ref_and_starts_no_host_process(self):
        self.api.head = '9' * 40
        with mock.patch.object(main, 'prepare_job') as checkout:
            with self.assertRaises(ValueError):
                main.compile_job(self.api, self.request)
            with self.assertRaises(ValueError):
                self.publish()
        checkout.assert_not_called()
        self.assertFalse(self.mutations())

    def test_bot_generated_only_pr_and_real_checks_precede_protected_rebase(self):
        self.assertEqual(self.api.published, self.publish())
        tree = self.api.tree['tree']
        self.assertEqual(['README.md', 'docs/code-progress.json'], [entry['path'] for entry in tree])
        commit = next(call['body'] for call in self.api.calls if call['path'].endswith('/git/commits') and call['method'] == 'POST')
        self.assertEqual([self.api.parent], commit['parents'])
        self.assertEqual(self.api.identity, commit['author'])
        self.assertEqual(self.api.identity, commit['committer'])
        blobs = [base64.b64decode(call['body']['content']) for call in self.api.calls if call['path'].endswith('/git/blobs')]
        self.assertEqual(b'human before\nNEW\nhuman after\n', blobs[0])
        self.assertEqual(self.report, json.loads(blobs[1]))
        dispatches = [call['body'] for call in self.api.calls if call['path'].endswith('/dispatches')]
        self.assertEqual(4, len(dispatches))
        self.assertTrue(all(body['inputs']['expected_head'] == self.api.generated and body['ref'] == self.api.branch for body in dispatches[:2]))
        self.assertTrue(all(body['inputs']['expected_head'] == self.api.published and body['ref'] == 'main' for body in dispatches[2:]))
        merge_index = next(i for i, call in enumerate(self.api.calls) if call['path'].endswith('/merge'))
        check_index = next(i for i, call in enumerate(self.api.calls) if '/check-runs?' in call['path'])
        self.assertLess(check_index, merge_index)
        self.assertFalse(any(call['path'].endswith('/git/refs/heads/main') and call['method'] == 'PATCH' for call in self.api.calls))
        self.cleanup.assert_called_once_with(self.api, self.api.branch, self.api.generated, self.control)

    def test_cleanup_failure_cannot_mask_publication_failure(self):
        import urllib.error
        denied = urllib.error.HTTPError('https://example.invalid', 405, 'inert denial', {}, None)
        def fail_merge(call):
            if call['path'].endswith('/merge'):
                raise denied
        self.api.hook = fail_merge
        self.cleanup.side_effect = subprocess.CalledProcessError(128, ['git'])
        with mock.patch('sys.stderr', new_callable=io.StringIO) as errors:
            with self.assertRaises(urllib.error.HTTPError) as result:
                self.publish()
        self.assertIs(denied, result.exception)
        self.assertIn('Generated ref cleanup failed (CalledProcessError)', errors.getvalue())
        self.assertEqual('closed', self.api.pr['state'])

    def test_ref_cleanup_still_runs_when_pr_cleanup_fails(self):
        def fail_pr_cleanup(call):
            if call['method'] == 'PATCH':
                raise ValueError('inert PR cleanup failure')
        self.api.hook = fail_pr_cleanup
        self.api.checks = []
        original = main.wait_for_checks
        with mock.patch.object(main, 'wait_for_checks', side_effect=lambda *args: original(*args, seconds=0)), \
                mock.patch('sys.stderr', new_callable=io.StringIO), self.assertRaisesRegex(ValueError, 'checks did not finish'):
            self.publish()
        self.cleanup.assert_called_once_with(self.api, self.api.branch, self.api.generated, self.control)

    def test_incomplete_failed_wrong_app_and_wrong_sha_checks_cannot_publish(self):
        states = ([*self.api.checks[:-1]],
                  [{**check, 'conclusion': 'failure'} for check in self.api.checks],
                  [{**check, 'app': {'id': 999}} for check in self.api.checks],
                  [{**check, 'head_sha': self.api.parent} for check in self.api.checks])
        for checks in states:
            with self.subTest(checks=checks):
                api = API(); api.checks = checks
                clock = [0]
                original = main.wait_for_checks
                def wait(*args):
                    return original(*args, seconds=0, sleep=lambda _: None, monotonic=lambda: clock[0])
                with mock.patch.object(main, 'wait_for_checks', side_effect=wait), self.assertRaises(ValueError):
                    main.publish(api, self.request, self.raw, self.producer, self.control, self.source)
                self.assertEqual(api.parent, api.head)
                self.assertFalse(any(call['path'].endswith('/merge') for call in api.calls))
                self.assertEqual('closed', api.pr['state'])

    def test_newer_failed_check_cannot_be_hidden_by_older_success(self):
        check = self.api.checks[0]
        self.api.checks.insert(0, {**check, 'conclusion': 'failure'})
        with self.assertRaises(ValueError):
            self.publish()
        self.assertEqual(self.api.parent, self.api.head)

    def test_concurrent_main_commit_during_checks_is_preserved(self):
        def advance(call):
            if '/check-runs?' in call['path']:
                self.api.head = '9' * 40
                self.api.refs['main'] = self.api.head
        self.api.hook = advance
        with self.assertRaises(ValueError):
            self.publish()
        self.assertEqual('9' * 40, self.api.head)
        self.assertFalse(any(call['path'].endswith('/merge') for call in self.api.calls))

    def test_concurrent_main_commit_at_merge_is_rejected_by_strict_protection(self):
        def advance(call):
            if call['path'].endswith('/merge'):
                self.api.head = '9' * 40
        self.api.hook = advance
        with self.assertRaises(ValueError):
            self.publish()
        self.assertEqual('9' * 40, self.api.head)
        self.assertFalse(any(call['body'] == {'force': True} for call in self.api.calls))

    def test_compiler_command_has_original_inputs_only_for_main_measurement(self):
        main_command = main.compiler_command(self.control, self.source, self.api.parent, self.api.parent, '1' * 64)
        pr_command = main.compiler_command(self.control, self.source, self.api.parent, self.api.parent, '1' * 64, compile_only=True)
        self.assertIn('--input', main_command)
        self.assertIn('--compile-only', pr_command)
        self.assertNotIn('--input', pr_command)
        self.assertFalse(any('SonicDreamTeam' in argument for argument in pr_command))

    def test_pr_gate_requires_successful_platform_admitted_pr_workflow(self):
        with mock.patch.dict(os.environ, {'GITHUB_EVENT_NAME': 'workflow_run'}), mock.patch.object(main.shared, 'gate') as gate:
            for event in ({}, {'workflow_run': {'event': 'push', 'conclusion': 'success'}},
                          {'workflow_run': {'event': 'pull_request', 'conclusion': 'failure'}},
                          {'workflow_run': {'event': 'pull_request', 'conclusion': 'action_required'}}):
                self.assertIsNone(main.pr_gate(self.api, event))
            gate.assert_not_called()
            gate.return_value = {'head': self.api.parent, 'head_repository': 'fork/repo', 'head_ref': 'feature'}
            self.assertEqual(gate.return_value, main.pr_gate(self.api, {'workflow_run': {'event': 'pull_request', 'conclusion': 'success'}}))



class CleanupTests(unittest.TestCase):
    def test_exact_lease_deletion_and_changed_ref_preservation(self):
        api = API()
        branch = 'lucid/progress-checks-456-' + api.generated[:12]
        api.refs[branch] = api.generated
        with mock.patch.dict(os.environ, {'GH_TOKEN': 'inert-fixture'}), mock.patch.object(main.subprocess, 'run') as push:
            main.cleanup_check_ref(api, branch, api.generated, Path('control'))
            command = push.call_args.args[0]
            self.assertEqual(Path('control'), push.call_args.kwargs['cwd'])
            self.assertIn('--force-with-lease=refs/heads/' + branch + ':' + api.generated, command)
            self.assertIn(':refs/heads/' + branch, command)
            self.assertIn('core.hooksPath=/dev/null', command)
            push.reset_mock()
            api.refs[branch] = '9' * 40
            main.cleanup_check_ref(api, branch, api.generated, Path('control'))
            push.assert_not_called()
            with self.assertRaises(ValueError):
                main.cleanup_check_ref(api, 'main', api.generated, Path('control'))

    def test_offline_git_deletion_from_nonrepository_workspace_and_ref_race(self):
        real_run = subprocess.run
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            control, remote = root / 'control', root / 'remote.git'
            def git(*args, cwd=root, check=True):
                return real_run(['git', '-c', 'core.hooksPath=/dev/null', *args], cwd=cwd,
                                check=check, capture_output=True, text=True)
            git('init', '--bare', str(remote))
            git('init', str(control))
            git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                'commit', '--allow-empty', '-m', 'inert fixture', cwd=control)
            expected = git('rev-parse', 'HEAD', cwd=control).stdout.strip()
            api = API()
            branch = 'lucid/progress-checks-456-' + expected[:12]
            ref = 'refs/heads/' + branch
            api.refs[branch] = expected
            git('push', str(remote), 'HEAD:' + ref, cwd=control)
            git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                'commit', '--allow-empty', '-m', 'concurrent inert fixture', cwd=control)
            changed = git('rev-parse', 'HEAD', cwd=control).stdout.strip()
            git('push', str(remote), 'HEAD:refs/heads/fixture', cwd=control)
            racing = False
            def offline_push(command, **kwargs):
                self.assertEqual(control, kwargs['cwd'])
                command = list(command)
                index = command.index('https://github.com/' + main.REPOSITORY + '.git')
                command[index] = str(remote)
                if racing:
                    git('--git-dir=' + str(remote), 'update-ref', ref, changed)
                return real_run(command, **kwargs)
            with mock.patch.dict(os.environ, {'GH_TOKEN': 'inert-fixture', 'HOME': str(root)}), \
                    mock.patch.object(main.subprocess, 'run', side_effect=offline_push):
                main.cleanup_check_ref(api, branch, expected, control)
                self.assertNotEqual(0, git('--git-dir=' + str(remote), 'show-ref', '--verify', ref, check=False).returncode)
                git('--git-dir=' + str(remote), 'update-ref', ref, expected)
                racing = True
                with self.assertRaises(subprocess.CalledProcessError):
                    main.cleanup_check_ref(api, branch, expected, control)
            self.assertEqual(changed, git('--git-dir=' + str(remote), 'rev-parse', ref).stdout.strip())


class WorkflowTests(unittest.TestCase):
    def test_generation_main_only_pr_compilation_only_and_no_summary_or_fork_credential(self):
        root = Path(__file__).resolve().parents[2]
        native = (root / '.github/workflows/native-progress.yml').read_text()
        pr = (root / '.github/workflows/pr-compilation.yml').read_text()
        tests = (root / '.github/workflows/tool-tests.yml').read_text()
        self.assertIn('branches: [main]', native)
        self.assertIn('paths-ignore:', native)
        self.assertIn('docs/code-progress.json', native)
        self.assertNotIn('workflow_run:', native)
        self.assertNotIn('pull_request:', native)
        self.assertNotIn('LUCID_FORK_TOKEN', native)
        self.assertIn('main_progress.py pr-compile', pr)
        self.assertNotIn(' publish', pr)
        self.assertNotIn('secrets.', pr)
        self.assertIn('workflows: [Code progress]', pr)
        for text in (native, pr, tests):
            self.assertNotIn('GITHUB_STEP_SUMMARY', text)
        for text in (native, pr):
            self.assertIn('group: native-progress-${{ github.repository }}', text)
            self.assertIn('cancel-in-progress: false', text)
        self.assertIn('python -m unittest discover -s tools/tests', tests)
        self.assertIn('dotnet run --project ArtifactIdentity.csproj', tests)
        self.assertIn('pull-requests: write', native)
        self.assertIn('checks: read', native)


if __name__ == '__main__':
    unittest.main()

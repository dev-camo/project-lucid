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
        self.calls = []
        self.hook = None
        self.identity = {'name': main.shared.BOT, 'email': '41898282+github-actions[bot]@users.noreply.github.com'}
        self.tree = None
        self.commit = None

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
            self.commit = body
            return {'sha': self.generated}
        if path == repo + '/git/commits/' + self.generated:
            return {'tree': {'sha': self.commit['tree']}, 'parents': [{'sha': p} for p in self.commit['parents']],
                    'author': self.commit['author'], 'committer': self.commit['committer']}
        if path == repo + '/git/refs/heads/main' and method == 'PATCH':
            if body != {'sha': self.generated, 'force': False}:
                raise AssertionError('Only a non-force update of generated main is permitted')
            if self.head != self.parent:
                raise ValueError('Atomic non-fast-forward rejection')
            self.head = self.generated
            return {'object': {'sha': self.generated}}
        if method == 'POST' and path.startswith(repo + '/statuses/'):
            return {}
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
        (self.source / 'README.md').write_text('human before\n<!-- LUCID_CODE_PROGRESS:START -->OLD<!-- LUCID_CODE_PROGRESS:END -->\nhuman after\n')
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
            self.validate, lambda report: '<!-- LUCID_CODE_PROGRESS:START -->NEW<!-- LUCID_CODE_PROGRESS:END -->\n',
            lambda text: (text.split('<!-- LUCID_CODE_PROGRESS:START -->')[0], '', text.split('<!-- LUCID_CODE_PROGRESS:END -->')[1]))))
        self.control_check = self.stack.enter_context(mock.patch.object(main.shared.driver, 'require_control_reporting_tools'))
        self.clean = self.stack.enter_context(mock.patch.object(main, 'require_clean_checkout'))
        self.expected = self.stack.enter_context(mock.patch.object(main, 'expected_tree', return_value='f' * 40))
        self.stack.enter_context(mock.patch.object(main, 'git', return_value=('d' * 40 + '\n').encode()))

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

    def test_generated_only_child_fast_forward_and_bot_attribution(self):
        self.assertEqual(self.api.generated, self.publish())
        self.assertEqual(['README.md', 'docs/code-progress.json'], [entry['path'] for entry in self.api.tree['tree']])
        self.assertEqual([self.api.parent], self.api.commit['parents'])
        self.assertEqual(self.api.identity, self.api.commit['author'])
        self.assertEqual(self.api.identity, self.api.commit['committer'])
        blobs = [base64.b64decode(call['body']['content']) for call in self.api.calls if call['path'].endswith('/git/blobs')]
        self.assertEqual(b'human before\n<!-- LUCID_CODE_PROGRESS:START -->NEW<!-- LUCID_CODE_PROGRESS:END -->\nhuman after\n', blobs[0])
        self.assertEqual(self.report, json.loads(blobs[1]))
        patches = [call for call in self.api.calls if call['method'] == 'PATCH']
        self.assertEqual([{'path': '/repos/' + main.REPOSITORY + '/git/refs/heads/main', 'method': 'PATCH',
                           'body': {'sha': self.api.generated, 'force': False}}], patches)
        self.assertFalse(any('/pulls' in call['path'] or '/actions/' in call['path'] for call in self.api.calls))
        self.assertFalse(any(call['method'] == 'DELETE' for call in self.api.calls))

    def test_dirty_checkout_rejected_before_writes(self):
        self.clean.side_effect = ValueError('unexpected content')
        with self.assertRaises(ValueError):
            self.publish()
        self.assertFalse(self.mutations())

    def test_missing_duplicate_or_reversed_markers_rejected_before_writes(self):
        for content in ('human text', '<!-- LUCID_CODE_PROGRESS:START -->' * 2,
                        '<!-- LUCID_CODE_PROGRESS:END --><!-- LUCID_CODE_PROGRESS:START -->'):
            (self.source / 'README.md').write_text(content)
            # Real marker ordering validation is exercised by split_readme in
            # the offline tests; this synthetic split rejects reversed input.
            with mock.patch.object(main.shared, 'reporting_tools', return_value=(self.validate, lambda r: 'NEW', real_split)):
                with self.assertRaises(ValueError):
                    self.publish()
        self.assertFalse(self.mutations())

    def test_human_suffix_without_final_newline_preserved(self):
        readme = self.source / 'README.md'
        readme.write_bytes(readme.read_bytes().rstrip(b'\n'))
        self.publish()
        blob = next(base64.b64decode(c['body']['content']) for c in self.api.calls if c['path'].endswith('/git/blobs'))
        self.assertTrue(blob.endswith(b'human after'))

    def test_noop_makes_no_mutations(self):
        payload = main.generated_payload(self.api, self.request, self.raw, self.producer, self.control, self.source)
        for path, data in payload.items():
            (self.source / path).write_bytes(data)
        self.api.calls.clear()
        self.assertEqual(self.api.parent, self.publish())
        self.assertFalse(self.mutations())

    def test_server_tree_or_commit_tamper_never_updates_main(self):
        for kind in ('tree', 'parent', 'author', 'committer'):
            api = API()
            def tamper(call):
                if kind != 'tree' and call['path'].endswith('/git/commits/' + api.generated):
                    if kind == 'parent':
                        api.commit['parents'] = ['9' * 40]
                    else:
                        api.commit[kind] = {'name': 'other', 'email': 'other@example.invalid'}
            api.hook = tamper
            with self.subTest(kind=kind), mock.patch.object(main, 'expected_tree', return_value=('9' if kind == 'tree' else 'f') * 40):
                with self.assertRaises(ValueError):
                    main.publish(api, self.request, self.raw, self.producer, self.control, self.source)
            self.assertEqual(api.parent, api.head)
            self.assertFalse(any(c['method'] == 'PATCH' for c in api.calls))

    def test_main_advance_during_construction_discards_stale_measurement(self):
        def advance(call):
            if call['path'].endswith('/git/commits') and call['method'] == 'POST':
                self.api.head = '9' * 40
        self.api.hook = advance
        with self.assertRaisesRegex(ValueError, 'Main advanced'):
            self.publish()
        self.assertEqual('9' * 40, self.api.head)
        self.assertFalse(any(c['method'] == 'PATCH' for c in self.api.calls))

    def test_main_race_at_update_is_atomically_rejected_without_retry(self):
        def advance(call):
            if call['method'] == 'PATCH':
                self.api.head = '9' * 40
        self.api.hook = advance
        with self.assertRaisesRegex(ValueError, 'non-fast-forward'):
            self.publish()
        self.assertEqual('9' * 40, self.api.head)
        self.assertEqual(1, len([c for c in self.api.calls if c['method'] == 'PATCH']))

    def test_uncertain_update_never_retries(self):
        def uncertain(call):
            if call['method'] == 'PATCH':
                raise TimeoutError('inert transport uncertainty')
        self.api.hook = uncertain
        with self.assertRaises(TimeoutError):
            self.publish()
        self.assertEqual(1, len([c for c in self.api.calls if c['method'] == 'PATCH']))

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


    def pr_request(self):
        return {'pr': 7, 'head': 'c' * 40, 'control_commit': self.api.parent,
                'head_repository': 'fork/repo', 'head_ref': 'feature', 'upstream_run': 9}

    def statuses(self):
        return [call for call in self.api.calls if call['method'] == 'POST']

    def report_pr(self, result, current=True):
        request = self.pr_request()
        side_effect = None if current else ValueError('Stale, draft, closed or foreign PR rejected')
        with mock.patch.dict(os.environ, {'GITHUB_EVENT_NAME': 'workflow_run'}), \
                mock.patch.object(main.shared, 'require_current_pr', side_effect=side_effect):
            main.pr_status_final(self.api, request, result)
        return request

    def test_pr_compilation_failure_is_visible_on_the_exact_head(self):
        request = self.report_pr('failure')
        (call,) = self.statuses()
        self.assertEqual(f"/repos/{main.REPOSITORY}/statuses/{request['head']}", call['path'])
        self.assertEqual(('failure', main.PR_COMPILATION_CONTEXT), (call['body']['state'], call['body']['context']))
        self.assertTrue(call['body']['target_url'].endswith('/actions/runs/456'))

    def test_pr_compilation_success_only_for_a_completed_current_compile(self):
        request = self.report_pr('success')
        (call,) = self.statuses()
        self.assertEqual('success', call['body']['state'])
        self.assertEqual(f"/repos/{main.REPOSITORY}/statuses/{request['head']}", call['path'])

    def test_pr_compilation_skipped_cancelled_or_unknown_never_succeeds(self):
        for result in ('skipped', 'cancelled', '', 'unexpected'):
            with self.subTest(result=result):
                self.api.calls.clear()
                self.report_pr(result)
                (call,) = self.statuses()
                self.assertEqual('error', call['body']['state'])

    def test_pr_compilation_advanced_head_or_closed_pr_gets_no_success(self):
        request = self.report_pr('success', current=False)
        (call,) = self.statuses()
        self.assertEqual('error', call['body']['state'])
        self.assertEqual(f"/repos/{main.REPOSITORY}/statuses/{request['head']}", call['path'])
        self.assertEqual([], [c for c in self.statuses() if c['body']['state'] == 'success'])

    def test_pr_compilation_pending_status_targets_validated_head_only(self):
        request = self.pr_request()
        with mock.patch.dict(os.environ, {'GITHUB_EVENT_NAME': 'workflow_run'}), \
                mock.patch.object(main.shared, 'require_current_pr') as current:
            main.pr_status_pending(self.api, request)
        current.assert_called_once_with(self.api, request)
        (call,) = self.statuses()
        self.assertEqual('pending', call['body']['state'])
        self.assertEqual(f"/repos/{main.REPOSITORY}/statuses/{request['head']}", call['path'])

    def test_pr_compilation_status_rejects_unvalidated_request_and_foreign_control(self):
        with mock.patch.dict(os.environ, {'GITHUB_EVENT_NAME': 'workflow_run'}):
            with self.assertRaises(ValueError):
                main.pr_status(self.api, {'head': 'c' * 40}, 'success', 'x')
            request = {**self.pr_request(), 'control_commit': 'e' * 40}
            with self.assertRaises(ValueError):
                main.pr_status_final(self.api, request, 'success')
        self.assertEqual([], self.statuses())

    def test_pr_compilation_workflow_reports_with_isolated_write_token(self):
        text = (Path(__file__).resolve().parents[2] / '.github/workflows/pr-compilation.yml').read_text()
        jobs = {}
        for chunk in text.split('\njobs:\n', 1)[1].split('\n  ')[0:0] or []:
            pass
        sections = text.split('\njobs:\n', 1)[1].split('\n\n  ')
        for section in sections:
            name = section.strip().split(':', 1)[0]
            jobs[name] = section
        self.assertEqual({'gate', 'start', 'compile', 'report'}, set(jobs))
        self.assertNotIn('statuses: write', jobs['compile'])
        self.assertNotIn('statuses: write', jobs['gate'])
        self.assertIn('statuses: write', jobs['start'])
        self.assertIn('statuses: write', jobs['report'])
        self.assertIn('always()', jobs['report'])
        self.assertIn('needs.compile.result', jobs['report'])
        self.assertIn('runs-on: ubuntu-24.04', jobs['report'])
        self.assertIn('self-hosted', jobs['compile'])



sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
from lucidlib.progressreport import split_readme as real_split


class OfflineTreeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        (self.root / 'docs').mkdir()
        (self.root / 'README.md').write_bytes(b'Human prefix\n<!-- LUCID_CODE_PROGRESS:START -->old<!-- LUCID_CODE_PROGRESS:END -->human suffix')
        (self.root / 'docs/code-progress.json').write_text('{}\n')
        (self.root / 'human.txt').write_bytes(b'must stay identical\n')
        subprocess.run(['git', 'add', '.'], cwd=self.root, check=True)
        subprocess.run(['git', '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '-qm', 'inert fixture'], cwd=self.root, check=True)
        self.head = main.shared.git_head(self.root)

    def test_real_tree_replaces_only_two_regular_blobs_and_preserves_index(self):
        main.require_clean_checkout(self.root, self.head)
        index = main.git(self.root, 'write-tree')
        payload = {'README.md': b'Human prefix\n<!-- LUCID_CODE_PROGRESS:START -->new<!-- LUCID_CODE_PROGRESS:END -->human suffix',
                   'docs/code-progress.json': b'{"fixture":true}\n'}
        tree = main.expected_tree(self.root, self.head, payload)
        self.assertEqual(index, main.git(self.root, 'write-tree'))
        self.assertEqual(b'README.md\ndocs/code-progress.json\n', main.git(self.root, 'diff-tree', '--no-commit-id', '--name-only', '-r', self.head, tree))
        for path, data in payload.items():
            self.assertEqual(data, main.git(self.root, 'show', tree + ':' + path))
        self.assertEqual(b'must stay identical\n', main.git(self.root, 'show', tree + ':human.txt'))
        self.assertEqual(b'', main.git(self.root, 'status', '--porcelain'))
        with self.assertRaisesRegex(ValueError, 'Unexpected generated path'):
            main.expected_tree(self.root, self.head, {**payload, 'human.txt': b'attack'})

    def test_real_checkout_rejects_untracked_staged_and_human_edits(self):
        for kind in ('untracked', 'human', 'staged'):
            path = self.root / ('untracked' if kind == 'untracked' else 'human.txt')
            path.write_text('changed')
            if kind == 'staged':
                subprocess.run(['git', 'add', str(path)], cwd=self.root, check=True)
            with self.subTest(kind=kind), self.assertRaisesRegex(ValueError, 'unexpected path/content'):
                main.require_clean_checkout(self.root, self.head)
            if kind == 'untracked':
                path.unlink()
            else:
                subprocess.run(['git', 'reset', '-q', '--hard', self.head], cwd=self.root, check=True)

    def test_git_subprocess_never_inherits_token_or_workflow_channels(self):
        with mock.patch.dict(os.environ, {'GH_TOKEN': 'inert', 'LUCID_PROGRESS_APP_PRIVATE_KEY': 'inert', 'GITHUB_OUTPUT': 'inert'}), mock.patch.object(main.subprocess, 'check_output', return_value=b'') as run:
            main.git(self.root, 'status')
        env = run.call_args.kwargs['env']
        for name in ('GH_TOKEN', 'LUCID_PROGRESS_APP_PRIVATE_KEY', 'GITHUB_OUTPUT'):
            self.assertNotIn(name, env)


class WorkflowTests(unittest.TestCase):
    def native_concurrency(self, workflow):
        text = (Path(__file__).resolve().parents[2] / '.github/workflows' / workflow).read_text()
        self.assertEqual(1, text.count('\nconcurrency:\n'))
        block = text.split('\nconcurrency:\n', 1)[1].split('\njobs:', 1)[0]
        return dict(line.strip().split(': ', 1) for line in block.splitlines() if line.strip())

    def test_both_native_workflows_preserve_serialization_and_keep_pending_runs(self):
        for workflow in ('native-progress.yml', 'pr-compilation.yml'):
            with self.subTest(workflow=workflow):
                self.assertEqual({'group': 'native-progress-${{ github.repository }}', 'queue': 'max',
                                  'cancel-in-progress': 'false'}, self.native_concurrency(workflow))

    def test_pending_main_survives_later_pr_gate_arrival(self):
        # GitHub queue:single replaces the existing pending run, even with
        # cancel-in-progress:false. queue:max retains up to100 pending runs.
        # Both incoming workflow policies matter: an unconfigured PR gate
        # can replace a queued main measurement before its admission step.
        # Primary contract: docs.github.com/en/actions/how-tos/write-workflows/
        # choose-when-workflows-run/control-workflow-concurrency
        policies = {name: self.native_concurrency(name) for name in ('native-progress.yml', 'pr-compilation.yml')}
        def pending_after_arrivals(config):
            pending = []
            for workflow, run in (('native-progress.yml', 'new-main'), ('pr-compilation.yml', 'later-pr-gate')):
                if config[workflow].get('queue', 'single') == 'single':
                    pending.clear()
                pending.append(run)
            return pending
        self.assertEqual(['new-main', 'later-pr-gate'], pending_after_arrivals(policies))
        # Retain the observed failure vector as a control, using the default
        # PR policy. It would evict the pending automatic main measurement.
        default_pr = {name: dict(policy) for name, policy in policies.items()}
        default_pr['pr-compilation.yml'].pop('queue', None)
        self.assertEqual(['later-pr-gate'], pending_after_arrivals(default_pr))

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
        self.assertNotIn('pull-requests: write', native)
        self.assertNotIn('checks: read', native)
        self.assertNotIn('actions: write', native)
        self.assertNotIn('\n      contents: write', native)
        publish = native.split('  publish:', 1)[1]
        self.assertNotIn('secrets.', native.split('  publish:', 1)[0])
        self.assertIn('environment: lucid-progress-publish', publish)
        self.assertIn('runs-on: ubuntu-24.04', publish)
        self.assertIn('repositories: project-lucid', publish)
        self.assertIn('permission-contents: write', publish)
        self.assertIn('GH_TOKEN: ${{ steps.publisher.outputs.token }}', publish)
        self.assertLess(publish.index('main_progress.py validate'), publish.index('uses: actions/create-github-app-token@'))
        self.assertNotIn('skip-token-revoke', publish)
        self.assertNotIn('self-hosted', publish)


if __name__ == '__main__':
    unittest.main()

"""Inert fixtures only: no repository issue, token, network or Unity execution."""
import contextlib
import copy
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import urllib.error
import urllib.parse

import issue_closure as closure

MERGE = 'a' * 40
HEAD = 'b' * 40
GENERATED = 'c' * 40
PR = 12
ISSUE = 34
BEFORE = '2026-10-14T01:00:00Z'
MERGED_AT = '2026-10-14T02:00:00Z'
VERIFIED_AT = '2026-10-14T03:00:00Z'
ACCEPTED_AT = '2026-10-14T04:00:00Z'
LATER = '2026-10-14T05:00:00Z'
POLICY = {'verification_principals': [101], 'approval_principals': [202], 'excluded_principals': [303]}
PROFILES = {'verification': {'login': 'verification-user', 'id': 101, 'type': 'User'},
            'approval': {'login': 'approval-user', 'id': 202, 'type': 'User'}}
OUTSIDE_USER = {'login': 'outside-user', 'id': 303, 'type': 'User'}


def attested(value, identifier, date):
    return {'id': identifier, 'body': closure.MARKER + json.dumps(value),
            'user': copy.deepcopy(PROFILES[value['kind']]),
            'created_at': date, 'updated_at': date}


class FakeGitHub:
    def __init__(self):
        self.requests = []
        self.writes = []
        self.hook = None
        self.patch_error = None
        self.patch_applied_on_error = False
        self.permission = 'write'
        self.permission_user = None
        self.repo = {'full_name': closure.REPOSITORY, 'default_branch': 'main'}
        self.commit = {'sha': MERGE, 'author': {'login': 'source-user'},
                       'committer': {'login': 'source-user'}, 'parents': [{'sha': 'd' * 40}]}
        self.issue = {'number': ISSUE, 'title': 'Complete both criteria', 'body': 'Both acceptance criteria must pass.',
                      'state': 'open', 'updated_at': BEFORE}
        self.prs = {PR: {'number': PR, 'state': 'closed', 'merged': True,
                       'head': {'sha': HEAD}, 'merge_commit_sha': MERGE, 'merged_at': MERGED_AT,
                       'base': {'ref': 'main', 'repo': {'full_name': closure.REPOSITORY}},
                       'updated_at': ACCEPTED_AT}}
        common = {'issue': ISSUE, 'issue_scope_sha256': closure.scope_digest(self.issue),
                  'settled_sha': MERGE, 'pull_requests': [{'number': PR, 'head_sha': HEAD, 'merge_sha': MERGE}],
                  'scope': 'whole-issue', 'all_issue_criteria_verified': True}
        self.verification = dict(copy.deepcopy(common), kind='verification', independent=True, publication_verified=True,
                           criteria=[{'criterion': 'Both criteria', 'result': 'passed',
                                      'evidence': 'Authenticated independent receipt sha256:' + 'e' * 64}])
        self.acceptance = dict(copy.deepcopy(common), kind='approval', decision='close', verification_comment_id=201,
                               independent_verification_accepted=True)
        self.rows = []
        self.sync_records()
        self.workflows = {}
        self.runs = {}
        self.run_lists = {}
        self.jobs = {}
        self.checks = {}
        self.add_checks(MERGE)
        self.comparisons = {}

    def sync_records(self):
        self.rows = [attested(self.verification, 201, VERIFIED_AT), attested(self.acceptance, 202, ACCEPTED_AT)]

    def add_checks(self, sha, native=True):
        for index, (filename, (name, event, roster)) in enumerate(closure.WORKFLOWS.items(), 1):
            if not native and filename == 'native-progress.yml':
                continue
            self.workflows[filename] = {'id': index, 'name': name, 'path': '.github/workflows/' + filename,
                                        'state': 'active'}
            run_id = len(self.runs) + 1000
            run = {'id': run_id, 'name': name, 'workflow_id': index, 'path': '.github/workflows/' + filename,
                   'head_sha': sha, 'head_branch': 'main', 'event': event, 'run_attempt': 1,
                   'repository': {'full_name': closure.REPOSITORY},
                   'head_repository': {'full_name': closure.REPOSITORY},
                   'status': 'completed', 'conclusion': 'success', 'created_at': MERGED_AT}
            if filename == 'pr-compilation.yml':
                run['display_title'] = closure.compilation_title({'id': run_id - 1, 'attempt': 1, 'sha': sha})
            self.runs[run_id] = run
            self.run_lists[(sha, filename)] = [run]
            self.jobs[run_id] = []
            for name, conclusion in roster.items():
                check_id = len(self.checks) + 2000
                job = {'id': check_id, 'name': name, 'head_sha': sha, 'run_id': run_id, 'run_attempt': 1,
                       'status': 'completed', 'conclusion': conclusion,
                       'check_run_url': 'https://api.github.com' + closure.ROOT + '/check-runs/' + str(check_id)}
                self.jobs[run_id].append(job)
                self.checks[check_id] = {'name': name, 'head_sha': sha, 'app': {'id': 15368},
                                        'status': 'completed', 'conclusion': conclusion}

    def generated_child(self):
        self.commit = {'sha': GENERATED, 'author': {'login': 'github-actions[bot]', 'type': 'Bot'},
                       'committer': {'login': 'github-actions[bot]', 'type': 'Bot'},
                       'commit': {'message': 'Update verified main code progress'},
                       'parents': [{'sha': MERGE}],
                       'files': [{'filename': 'docs/code-progress.json', 'status': 'modified'}]}
        self.acceptance['settled_sha'] = GENERATED
        self.verification['settled_sha'] = GENERATED
        self.sync_records()
        self.add_checks(GENERATED, native=False)

    def request(self, path, method='GET', body=None):
        self.requests.append((path, method, copy.deepcopy(body)))
        if self.hook:
            self.hook(path, method, body)
        parsed = urllib.parse.urlsplit(path)
        route = parsed.path[len(closure.ROOT):]
        query = urllib.parse.parse_qs(parsed.query)
        require_repo = parsed.path == closure.ROOT or parsed.path.startswith(closure.ROOT + '/')
        if not require_repo:
            raise AssertionError('Foreign API in fake')
        if method != 'GET':
            if route != f'/issues/{ISSUE}' or method != 'PATCH' or body != {'state': 'closed', 'state_reason': 'completed'}:
                raise AssertionError('Unexpected mutation in fake')
            self.writes.append((path, method, copy.deepcopy(body)))
            if not self.patch_error or self.patch_applied_on_error:
                self.issue.update(state='closed', state_reason='completed', updated_at=LATER)
            if self.patch_error:
                raise self.patch_error
            return copy.deepcopy(self.issue)
        if route == '':
            result = self.repo
        elif route == '/commits/main':
            result = self.commit
        elif route.startswith('/collaborators/') and route.endswith('/permission'):
            login = route.split('/')[2]
            user = next((profile for profile in PROFILES.values() if profile['login'] == login),
                        {'login': login, 'id': 0, 'type': 'User'})
            result = {'permission': self.permission, 'user': self.permission_user or user}
        elif route == f'/issues/{ISSUE}':
            result = self.issue
        elif route == f'/issues/{PR}/comments':
            result = self.rows
        elif route.startswith('/pulls/'):
            result = self.prs[int(route.rsplit('/', 1)[1])]
        elif route.startswith('/commits/') and route.endswith('/pulls'):
            result = list(self.prs.values())
        elif route.startswith('/compare/'):
            base = route.split('/')[2].split('...')[0]
            result = self.comparisons.get(base, {'status': 'identical' if base == MERGE else 'ahead',
                      'base_commit': {'sha': base}, 'merge_base_commit': {'sha': base}})
        elif route.startswith('/actions/workflows/'):
            filename = route.split('/')[3]
            if route.endswith('/runs'):
                if 'head_sha' in query:
                    rows = self.run_lists.get((query['head_sha'][0], filename), [])
                else:
                    rows = [row for (_, workflow), batch in self.run_lists.items()
                            if workflow == filename for row in batch]
                    created = query.get('created', [''])[0].removeprefix('>=')
                    rows = [row for row in rows if row['created_at'] >= created]
                result = {'workflow_runs': rows}
            else:
                result = self.workflows[filename]
        elif route.startswith('/actions/runs/'):
            run_id = int(route.split('/')[3])
            result = {'jobs': self.jobs[run_id]} if route.endswith('/jobs') else self.runs[run_id]
        elif route.startswith('/check-runs/'):
            result = self.checks[int(route.rsplit('/', 1)[1])]
        else:
            raise AssertionError('Unexpected fake GET: ' + path)
        if 'page' in query:
            page = int(query['page'][0])
            key = 'workflow_runs' if isinstance(result, dict) and 'workflow_runs' in result else 'jobs'
            if isinstance(result, list):
                result = result[(page - 1) * 100:page * 100]
            else:
                result = {key: result[key][(page - 1) * 100:page * 100]}
        return copy.deepcopy(result)


class ClosureTests(unittest.TestCase):
    def setUp(self):
        configuration = mock.patch.dict(os.environ, {closure.AUTHORIZATION_ENV: json.dumps(POLICY)})
        configuration.start()
        self.addCleanup(configuration.stop)
        self.api = FakeGitHub()

    def qualify(self):
        return closure.qualify(self.api, PR, 202, self.api.commit['sha'])

    def held(self, reason=None):
        with self.assertRaisesRegex(closure.Blocked, reason or '.'):
            self.qualify()
        self.assertEqual([], self.api.writes)

    def test_complete_issue_closes_only_after_separate_acceptance_and_all_workflows(self):
        plan = self.qualify()
        self.assertEqual(4, len(plan['runs']))
        self.assertEqual([], self.api.writes)
        self.assertEqual('closed', closure.close(self.api, plan))
        self.assertEqual([(closure.ROOT + f'/issues/{ISSUE}', 'PATCH',
                           {'state': 'closed', 'state_reason': 'completed'})], self.api.writes)

    def test_generated_child_requires_source_native_success_and_child_hosted_checks(self):
        self.api.generated_child()
        plan = self.qualify()
        self.assertEqual(MERGE, plan['source_sha'])
        self.assertEqual(GENERATED, plan['settled_sha'])
        self.assertEqual(7, len(plan['runs']))
        self.assertEqual('closed', closure.close(self.api, plan))

    def test_every_actual_merge_in_multi_pr_issue_must_qualify(self):
        older = 'e' * 40
        self.api.prs[11] = dict(self.api.prs[PR], number=11, merge_commit_sha=older)
        contributor = {'number': 11, 'head_sha': HEAD, 'merge_sha': older}
        self.api.acceptance['pull_requests'].append(contributor)
        self.api.verification['pull_requests'].append(contributor)
        self.api.sync_records()
        self.api.add_checks(older)
        self.assertEqual(8, len(self.qualify()['runs']))
        self.api.prs[11]['merged'] = False
        self.held('Contributing PR')

    def test_ci_before_or_after_acceptance_both_event_orders_aggregate(self):
        for first in ('ci', 'acceptance'):
            with self.subTest(first=first):
                api = FakeGitHub()
                if first == 'ci':
                    api.rows = []
                    self.assertEqual([], closure.candidates(api, self.workflow_event(), 'workflow_run', MERGE))
                    api.sync_records()
                    event = {'repository': api.repo, 'action': 'created',
                             'issue': {'number': PR, 'pull_request': {}}, 'comment': api.rows[-1]}
                    self.assertEqual([(PR, 202)], closure.candidates(api, event, 'issue_comment', MERGE))
                else:
                    api.runs[1000]['status'] = 'in_progress'
                    with self.assertRaises(closure.Blocked):
                        closure.qualify(api, PR, 202, MERGE)
                    api.runs[1000]['status'] = 'completed'
                    self.assertEqual([(PR, 202)], closure.candidates(api, self.workflow_event(), 'workflow_run', MERGE))
                self.assertEqual(ISSUE, closure.qualify(api, PR, 202, MERGE)['issue'])
                self.assertEqual([], api.writes)

    def workflow_event(self):
        return {'repository': self.api.repo, 'workflow_run': self.api.runs[1000]}

    def test_every_workflow_missing_pending_failed_cancelled_or_skipped_holds(self):
        for filename in closure.WORKFLOWS:
            for state in ('missing', 'in_progress', 'failure', 'cancelled', 'skipped'):
                with self.subTest(workflow=filename, state=state):
                    self.api = FakeGitHub()
                    run = self.api.run_lists[(MERGE, filename)][0]
                    if state == 'missing':
                        self.api.run_lists[(MERGE, filename)] = []
                    elif state == 'in_progress':
                        run['status'] = state
                    else:
                        run['conclusion'] = state
                    self.held()

    def test_successful_run_with_missing_duplicate_or_skipped_required_job_holds(self):
        for kind in ('missing', 'duplicate', 'skipped', 'wrong-head', 'wrong-attempt'):
            with self.subTest(kind=kind):
                self.api = FakeGitHub()
                jobs = self.api.jobs[1000]
                if kind == 'missing':
                    jobs.pop()
                elif kind == 'duplicate':
                    jobs.append(copy.deepcopy(jobs[0]))
                elif kind == 'skipped':
                    jobs[0]['conclusion'] = 'skipped'
                elif kind == 'wrong-head':
                    jobs[0]['head_sha'] = HEAD
                else:
                    jobs[0]['run_attempt'] = 2
                self.held()

    def test_foreign_workflow_or_check_cannot_supply_green_ci(self):
        for target, key, value in (('run', 'workflow_id', 999), ('run', 'head_sha', HEAD),
                                   ('run', 'head_branch', 'topic'), ('run', 'event', 'pull_request'),
                                   ('run', 'path', '.github/workflows/foreign.yml'),
                                   ('workflow', 'state', 'disabled_manually'),
                                   ('check', 'app', {'id': 9}), ('check', 'head_sha', HEAD)):
            with self.subTest(target=target, key=key):
                self.api = FakeGitHub()
                row = self.api.runs[1000] if target == 'run' else (
                    self.api.workflows['tool-tests.yml'] if target == 'workflow' else self.api.checks[2000])
                row[key] = value
                self.held()

    def test_latest_run_and_latest_attempt_supersede_previous_green(self):
        run = copy.deepcopy(self.api.runs[1000])
        run.update(id=9999, status='in_progress', run_attempt=2)
        self.api.runs[9999] = run
        self.api.run_lists[(MERGE, 'tool-tests.yml')].append(run)
        self.held('Latest workflow')
        self.api = FakeGitHub()
        self.api.runs[1000]['run_attempt'] = 2
        self.api.runs[1000]['conclusion'] = 'failure'
        self.held('Latest workflow')

    def test_successful_rerun_uses_exact_attempt_jobs(self):
        self.api.runs[1000]['run_attempt'] = 2
        for job in self.api.jobs[1000]:
            job['run_attempt'] = 2
        plan = self.qualify()
        self.assertEqual(2, plan['runs'][0]['attempt'])
        self.assertTrue(any('/attempts/2/jobs?' in path for path, _, _ in self.api.requests))

    def test_native_compilation_cannot_be_skipped_on_source_merge(self):
        self.api.jobs[1003][1]['conclusion'] = 'skipped'
        self.held('Required job')

    def test_partial_or_unverified_criteria_never_close(self):
        for role, key, value in (('approval', 'scope', 'partial'), ('verification', 'all_issue_criteria_verified', False),
                                 ('verification', 'independent', False), ('verification', 'publication_verified', False),
                                 ('approval', 'independent_verification_accepted', False), ('approval', 'decision', 'hold'),
                                 ('verification', 'criteria', []), ('verification', 'criteria',
                                  [{'criterion': 'A', 'result': 'not-run', 'evidence': 'pending'}])):
            with self.subTest(kind=role, key=key):
                self.api = FakeGitHub()
                row = self.api.acceptance if role == 'approval' else self.api.verification
                row[key] = value
                self.api.sync_records()
                self.held()

    def test_no_check_refs_mention_or_normal_prose_is_acceptance(self):
        for body in ('Refs #34. All green.', 'All criteria independently verified. — verification',
                     '<!-- codex-pull-request-verification-summary -->\nCompleted'):
            self.api.rows = [dict(self.api.rows[-1], body=body)]
            self.held('removed or superseded')

    def test_missing_wrong_or_same_verification_id_holds(self):
        for number in (200, 202, 999):
            self.api.acceptance['verification_comment_id'] = number
            self.api.sync_records()
            self.held()

    def test_unauthenticated_bot_forged_login_or_permission_loss_holds(self):
        for field, value in (('login', 'stranger'), ('id', 999), ('type', 'Bot')):
            for index in (0, 1):
                self.api = FakeGitHub()
                self.api.rows[index]['user'][field] = value
                self.held('authenticated|authorized principal write permission')
        self.api = FakeGitHub()
        self.api.permission = 'read'
        self.held('authorized principal write permission')

    def test_edited_evidence_deleted_approval_and_new_hold_supersede(self):
        for index in (0, 1):
            self.api = FakeGitHub()
            self.api.rows[index]['updated_at'] = LATER
            self.held('Edited acceptance|Later edited discussion')
        self.api = FakeGitHub()
        self.api.rows.pop()
        self.held('removed or superseded')
        self.api = FakeGitHub()
        self.api.rows.append(attested(dict(self.api.acceptance, decision='hold'), 203, LATER))
        self.held('removed or superseded')

    def test_new_verification_record_supersedes_old_independent_receipt(self):
        self.api.rows.insert(1, attested(dict(self.api.verification), 203, VERIFIED_AT))
        self.held('superseded')

    def test_malformed_duplicate_unknown_fields_and_script_text_fail_closed(self):
        for body in (closure.MARKER + '{bad json}',
                     closure.MARKER + '{"kind":"approval","kind":"verification"}',
                     closure.MARKER + '$(touch /tmp/should-never-run)'):
            self.api = FakeGitHub()
            self.api.rows[-1]['body'] = body
            self.held()
        self.api = FakeGitHub()
        self.api.acceptance['unexpected'] = True
        self.api.sync_records()
        self.held('fields differ')

    def test_configured_principals_with_write_access_qualify_separate_records(self):
        plan = self.qualify()
        self.assertNotEqual(plan['approval_comment'], plan['verification_comment'])
        self.assertEqual({101, 202}, {row['user']['id'] for row in self.api.rows})
        for profile in PROFILES.values():
            self.assertTrue(any('/collaborators/' + profile['login'] + '/permission' in path
                                for path, _, _ in self.api.requests))
        self.assertEqual([], self.api.writes)

    def test_unconfigured_principal_cannot_grant_acceptance_even_with_admin(self):
        for index in (0, 1):
            self.api = FakeGitHub()
            self.api.permission = 'admin'
            self.api.rows[index]['user'] = copy.deepcopy(OUTSIDE_USER)
            self.held('authorized principal')

    def test_crossed_login_and_id_cannot_impersonate_authorized_principals(self):
        for user in ({'login': 'verification-user', 'id': 303, 'type': 'User'},
                     {'login': 'outside-user', 'id': 101, 'type': 'User'}):
            self.api = FakeGitHub()
            self.api.rows[0]['user'] = user
            self.held('authorized principal')

    def test_permission_response_identity_and_current_access_are_verified(self):
        for key, value in (('login', 'outside-user'), ('id', 303), ('type', 'Bot')):
            self.api = FakeGitHub()
            self.api.permission_user = copy.deepcopy(PROFILES['approval'])
            self.api.permission_user[key] = value
            self.held('authorized principal write permission')
        for permission in ('read', 'none', 'triage'):
            self.api = FakeGitHub()
            self.api.permission = permission
            self.held('authorized principal write permission')

    def test_acceptance_is_one_strict_json_object(self):
        for row in self.api.rows:
            self.assertIn(json.loads(row['body'][len(closure.MARKER):])['kind'], closure.RECORD_KINDS)
        self.assertEqual(ISSUE, self.qualify()['issue'])
        self.api.rows[0]['body'] += '\n\nUnexpected trailing prose'
        self.held('Malformed acceptance JSON')

    def test_unknown_record_kind_has_no_acceptance_authority(self):
        for kind in ('unsupported', [], {}, True):
            self.api.rows[0]['body'] = closure.MARKER + json.dumps(dict(self.api.verification, kind=kind))
            self.held('record kind differs')
        with self.assertRaisesRegex(closure.Blocked, 'no acceptance authority'):
            closure.trusted(self.api, self.api.rows[0], 'unsupported', POLICY)
        self.assertEqual([], self.api.writes)

    def test_missing_or_malformed_external_authorization_holds_without_writes(self):
        for raw in ('', 'not-json', '{}', 'x' * 4097,
                    '{"verification_principals":[101],"verification_principals":[102]}'):
            with mock.patch.dict(os.environ, {closure.AUTHORIZATION_ENV: raw}):
                self.held('authorization|Authorization')

    def test_authorization_principal_sets_are_typed_bounded_and_unique(self):
        for field, value in (('approval_principals', []), ('verification_principals', [True]),
                             ('verification_principals', ['101']), ('verification_principals', [101.0]),
                             ('verification_principals', [0]), ('verification_principals', [101, 101]),
                             ('verification_principals', list(range(1, 12))),
                             ('excluded_principals', [101])):
            policy = dict(POLICY, **{field: value})
            with mock.patch.dict(os.environ, {closure.AUTHORIZATION_ENV: json.dumps(policy)}):
                self.held('principal|principals')

    def test_capabilities_are_separate_for_distinct_configured_principals(self):
        for index, profile in ((0, PROFILES['approval']), (1, PROFILES['verification'])):
            self.api = FakeGitHub()
            self.api.rows[index]['user'] = copy.deepcopy(profile)
            self.held('authorized principal')

    def test_same_configured_principal_still_requires_two_distinct_records(self):
        policy = dict(POLICY, approval_principals=[101])
        self.api.rows[1]['user'] = copy.deepcopy(PROFILES['verification'])
        with mock.patch.dict(os.environ, {closure.AUTHORIZATION_ENV: json.dumps(policy)}):
            plan = self.qualify()
            self.assertNotEqual(plan['approval_comment'], plan['verification_comment'])
            self.api.acceptance['verification_comment_id'] = 202
            self.api.sync_records()
            self.api.rows[1]['user'] = copy.deepcopy(PROFILES['verification'])
            self.held('Separate verification evidence')

    def test_authorization_change_during_final_checks_prevents_patch(self):
        plan = self.qualify()
        last = self.api.jobs[1003][-1]['check_run_url'].removeprefix('https://api.github.com')
        changed = dict(POLICY, approval_principals=[404])
        def revoke(path, method, body):
            if path == last:
                os.environ[closure.AUTHORIZATION_ENV] = json.dumps(changed)
        self.api.hook = revoke
        with self.assertRaisesRegex(closure.Blocked, 'Authorization changed'):
            closure.close(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_permission_revoked_during_final_checks_prevents_patch(self):
        plan = self.qualify()
        last = self.api.jobs[1003][-1]['check_run_url'].removeprefix('https://api.github.com')
        def revoke(path, method, body):
            if path == last:
                self.api.permission = 'read'
        self.api.hook = revoke
        with self.assertRaisesRegex(closure.Blocked, 'write permission'):
            closure.close(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_principal_login_cannot_redirect_permission_requests(self):
        for login in ('verification-user/../issues/34', 'verification-user?x=1', 'other.example/user', ''):
            self.api = FakeGitHub()
            self.api.rows[0]['user']['login'] = login
            self.held('authenticated')

    def test_changed_issue_body_or_reopened_issue_requires_new_acceptance(self):
        self.api.issue['body'] += ' New acceptance requirement.'
        self.held('criteria changed')
        self.api = FakeGitHub()
        self.api.issue['updated_at'] = LATER
        self.held('reopened')

    def test_stale_heads_wrong_rebase_sha_unmerged_or_foreign_prs_hold(self):
        for key, value in (('merged', False), ('state', 'open'), ('merge_commit_sha', HEAD),
                           ('head', {'sha': 'f' * 40}), ('base', {'ref': 'develop', 'repo': {'full_name': closure.REPOSITORY}})):
            self.api = FakeGitHub()
            self.api.prs[PR][key] = value
            self.held()
        self.api = FakeGitHub()
        self.api.commit['sha'] = 'f' * 40
        self.held('actual latest merged PR')

    def test_changed_pr_or_premerge_verification_holds(self):
        self.api.prs[PR]['updated_at'] = LATER
        self.held('PR changed')
        self.api = FakeGitHub()
        self.api.rows[0]['created_at'] = BEFORE
        self.api.rows[0]['updated_at'] = BEFORE
        self.held('must follow every actual merge')

    def test_nonancestor_merge_and_omitted_anchor_hold(self):
        self.api.comparisons[MERGE] = {'status': 'diverged', 'base_commit': {'sha': MERGE},
                                       'merge_base_commit': {'sha': HEAD}}
        self.held('not contained')
        self.api = FakeGitHub()
        for row in (self.api.verification, self.api.acceptance):
            row['pull_requests'] = [{'number': 11, 'head_sha': HEAD, 'merge_sha': MERGE}]
        self.api.sync_records()
        self.held('Anchor omitted')

    def test_generated_child_boundaries_cannot_hide_source_or_readme_changes(self):
        for kind in ('source', 'readme', 'merge', 'rename', 'message', 'bot-type', 'no-publication-acceptance'):
            self.api = FakeGitHub()
            self.api.generated_child()
            if kind in ('source', 'readme'):
                self.api.commit['files'].append({'filename': 'source.cs' if kind == 'source' else 'README.md', 'status': 'modified'})
            elif kind == 'merge':
                self.api.commit['parents'].append({'sha': HEAD})
            elif kind == 'rename':
                self.api.commit['files'][0]['previous_filename'] = 'source.cs'
            elif kind == 'message':
                self.api.commit['commit']['message'] += ' extra'
            elif kind == 'bot-type':
                self.api.commit['author']['type'] = 'User'
            else:
                self.api.verification['publication_verified'] = False
                self.api.sync_records()
            self.held()

    def test_wrong_settled_sha_and_stale_control_hold(self):
        self.api.acceptance['settled_sha'] = HEAD
        self.api.sync_records()
        self.held('settled SHA is stale')
        self.api = FakeGitHub()
        with self.assertRaisesRegex(closure.Blocked, 'advanced'):
            closure.qualify(self.api, PR, 202, HEAD)

    def test_unrelated_newer_pr_compilation_never_supersedes_the_main_trigger(self):
        for generated in (False, True):
            for state in ('queued', 'in_progress', 'failure', 'success'):
                self.api = FakeGitHub()
                if generated:
                    self.api.generated_child()
                newer = copy.deepcopy(self.api.runs[1002])
                newer.update(id=9999, display_title=closure.compilation_title(
                    {'id': 999, 'attempt': 1, 'sha': HEAD}), status=state, conclusion=state)
                self.api.runs[9999] = newer
                self.api.run_lists[(MERGE, 'pr-compilation.yml')].append(newer)
                plan = self.qualify()
                self.assertIn(1002, {run['id'] for run in plan['runs']})
                self.assertNotIn(9999, {run['id'] for run in plan['runs']})
                self.assertFalse(any(path == closure.ROOT + '/actions/runs/9999'
                                     for path, _, _ in self.api.requests))
                self.assertEqual([], self.api.writes)

    def test_newer_matching_pr_compilation_failure_or_pending_never_falls_back(self):
        for state in ('queued', 'in_progress', 'failure', 'cancelled'):
            self.api = FakeGitHub()
            newer = copy.deepcopy(self.api.runs[1002])
            newer.update(id=9999, status=state, conclusion=state)
            self.api.runs[9999] = newer
            self.api.run_lists[(MERGE, 'pr-compilation.yml')].append(newer)
            self.held('Latest workflow attempt')

    def test_upstream_code_progress_rerun_requires_its_own_downstream_trigger(self):
        self.api.runs[1001]['run_attempt'] = 2
        self.api.jobs[1001][0]['run_attempt'] = 2
        self.held('exact trigger')
        newer = copy.deepcopy(self.api.runs[1002])
        newer.update(id=9999, display_title=closure.compilation_title({'id': 1001, 'attempt': 2, 'sha': MERGE}))
        self.api.runs[9999] = newer
        self.api.run_lists[(MERGE, 'pr-compilation.yml')].append(newer)
        self.api.jobs[9999] = copy.deepcopy(self.api.jobs[1002])
        for job in self.api.jobs[9999]:
            job['run_id'] = 9999
        plan = self.qualify()
        downstream = next(run for run in plan['runs'] if run['workflow'] == 'pr-compilation.yml')
        self.assertEqual(9999, downstream['id'])
        self.assertEqual(2, downstream['upstream']['attempt'])

    def test_workflow_run_control_sha_can_differ_from_attested_upstream_source(self):
        self.api.generated_child()
        self.api.runs[1002]['head_sha'] = GENERATED
        for job in self.api.jobs[1002]:
            job['head_sha'] = GENERATED
            self.api.checks[job['id']]['head_sha'] = GENERATED
        plan = self.qualify()
        run = next(row for row in plan['runs'] if row['id'] == 1002)
        self.assertEqual(MERGE, run['sha'])
        self.assertEqual(GENERATED, run['control_sha'])
        self.assertEqual(MERGE, run['upstream']['sha'])
        queries = [path for path, _, _ in self.api.requests if '/pr-compilation.yml/runs?' in path]
        self.assertTrue(queries)
        self.assertTrue(all('head_sha=' not in path for path in queries))

    def test_foreign_control_and_missing_source_provenance_never_qualify(self):
        self.api.runs[1002]['head_sha'] = HEAD
        self.api.comparisons[HEAD] = {'status': 'diverged', 'base_commit': {'sha': HEAD},
                                     'merge_base_commit': {'sha': MERGE}}
        self.held('control is not contained')
        for title in ('PR compilation', closure.compilation_title({'id': 1001, 'attempt': 1, 'sha': HEAD}),
                      f'PR compilation (upstream 1001/1 pull_request {MERGE})'):
            self.api = FakeGitHub()
            self.api.runs[1002]['display_title'] = title
            self.held('exact trigger')

    def test_rerun_started_during_last_job_read_is_refreshed_before_patch(self):
        for workflow in (1000, 1001, 1002):
            self.api = FakeGitHub()
            plan = self.qualify()
            native_last = self.api.jobs[1003][-1]['check_run_url'].removeprefix('https://api.github.com')
            triggered = []
            def start_rerun(path, method, body):
                if path == native_last and not triggered:
                    triggered.append(True)
                    self.api.runs[workflow].update(run_attempt=2, status='in_progress', conclusion=None)
            self.api.hook = start_rerun
            with self.assertRaisesRegex(closure.Blocked, 'Latest workflow attempt'):
                closure.close(self.api, plan)
            self.assertEqual([True], triggered)
            self.assertEqual([], self.api.writes)

    def test_new_run_started_during_final_metadata_reads_is_refreshed_before_patch(self):
        plan = self.qualify()
        fired = []
        def start_run(path, method, body):
            if '/issues/12/comments?' in path and not fired:
                fired.append(True)
                newer = copy.deepcopy(self.api.runs[1000])
                newer.update(id=9999, status='queued', conclusion=None)
                self.api.runs[9999] = newer
                self.api.run_lists[(MERGE, 'tool-tests.yml')].append(newer)
        self.api.hook = start_run
        with self.assertRaisesRegex(closure.Blocked, 'Latest workflow attempt'):
            closure.require_current(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_issue_title_changes_are_bound_at_initial_and_final_validation(self):
        self.api.issue['title'] = 'New unresolved requirement'
        self.held('criteria changed')
        self.api = FakeGitHub()
        plan = self.qualify()
        native_last = self.api.jobs[1003][-1]['check_run_url'].removeprefix('https://api.github.com')
        def change_title(path, method, body):
            if path == native_last:
                self.api.issue['title'] = 'New unresolved requirement'
        self.api.hook = change_title
        with self.assertRaisesRegex(closure.Blocked, 'Issue changed during qualification'):
            closure.close(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_scope_digest_is_canonical_utf8_title_and_body(self):
        import hashlib
        issue = {'title': 'é', 'body': 'A\nB'}
        expected = hashlib.sha256('{"body":"A\\nB","title":"é"}'.encode('utf-8')).hexdigest()
        self.assertEqual(expected, closure.scope_digest(issue))
        self.assertNotEqual(closure.scope_digest({'title': 'a', 'body': 'bc'}),
                            closure.scope_digest({'title': 'ab', 'body': 'c'}))

    def test_edited_newer_revocation_cannot_expose_an_older_acceptance(self):
        plan = self.qualify()
        revoked = attested(dict(self.api.acceptance, decision='hold'), 203, LATER)
        self.api.rows.append(revoked)
        self.held('removed or superseded')
        revoked['body'] = 'Acceptance withdrawn. The whole issue is not fixed.'
        revoked['updated_at'] = '2026-10-14T06:00:00Z'
        self.held('Later edited discussion')
        with self.assertRaisesRegex(closure.Blocked, 'Later edited discussion'):
            closure.close(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_late_marker_removed_revocation_is_caught_in_final_snapshot(self):
        plan = self.qualify()
        native_last = self.api.jobs[1003][-1]['check_run_url'].removeprefix('https://api.github.com')
        fired = []
        def revoke(path, method, body):
            if path == native_last and not fired:
                fired.append(True)
                row = attested(dict(self.api.acceptance, decision='hold'), 203, LATER)
                row.update(body='Acceptance withdrawn.', updated_at='2026-10-14T06:00:00Z')
                self.api.rows.append(row)
        self.api.hook = revoke
        with self.assertRaisesRegex(closure.Blocked, 'Later edited discussion'):
            closure.close(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_fresh_explicit_acceptance_can_follow_a_previously_edited_revocation(self):
        revocation = attested(dict(self.api.acceptance, decision='hold'), 203, LATER)
        revocation.update(body='Acceptance withdrawn.', updated_at='2026-10-14T06:00:00Z')
        self.api.rows.append(revocation)
        renewed_at = '2026-10-14T07:00:00Z'
        self.api.rows.append(attested(self.api.acceptance, 204, renewed_at))
        self.api.prs[PR]['updated_at'] = renewed_at
        self.assertEqual(ISSUE, closure.qualify(self.api, PR, 204, MERGE)['issue'])
        self.assertEqual([], self.api.writes)

    def test_older_contributor_metadata_changes_are_attested_and_rechecked(self):
        older = 'e' * 40
        self.api.prs[11] = dict(self.api.prs[PR], number=11, merge_commit_sha=older)
        contributor = {'number': 11, 'head_sha': HEAD, 'merge_sha': older}
        self.api.acceptance['pull_requests'].append(copy.deepcopy(contributor))
        self.api.verification['pull_requests'].append(copy.deepcopy(contributor))
        self.api.sync_records()
        self.api.add_checks(older)
        plan = self.qualify()
        self.api.prs[11]['updated_at'] = LATER
        self.held('Contributing PR changed after acceptance')
        with self.assertRaisesRegex(closure.Blocked, 'Contributing PR changed during qualification'):
            closure.require_current(self.api, plan)
        self.assertEqual([], self.api.writes)

    def test_mutation_between_planning_and_patch_rebuilds_gate(self):
        for kind in ('main', 'issue', 'verification', 'ci'):
            self.api = FakeGitHub()
            plan = self.qualify()
            if kind == 'main':
                self.api.commit['sha'] = HEAD
            elif kind == 'issue':
                self.api.issue['body'] += ' Changed.'
            elif kind == 'verification':
                self.api.rows[0]['updated_at'] = LATER
            else:
                self.api.runs[1000]['conclusion'] = 'failure'
            with self.assertRaises(closure.Blocked):
                closure.close(self.api, plan)
            self.assertEqual([], self.api.writes)

    def test_main_advance_during_qualification_rejected_at_final_read(self):
        def change(path, method, body):
            if '/check-runs/' in path:
                self.api.commit['sha'] = HEAD
        self.api.hook = change
        self.held('Main advanced during')

    def test_duplicate_events_and_already_closed_issues_are_noops(self):
        plan = self.qualify()
        closure.close(self.api, plan)
        self.assertEqual([], closure.candidates(self.api, self.workflow_event(), 'workflow_run', MERGE))
        self.assertEqual(1, len(self.api.writes))

    def test_ambiguous_patch_is_read_back_once_without_blind_retry(self):
        for applied in (True, False):
            self.api = FakeGitHub()
            self.api.patch_error = urllib.error.URLError('simulated lost response')
            self.api.patch_applied_on_error = applied
            plan = self.qualify()
            if applied:
                self.assertEqual('closed-confirmed-after-error', closure.close(self.api, plan))
            else:
                with self.assertRaisesRegex(closure.Blocked, 'uncertain'):
                    closure.close(self.api, plan)
            self.assertEqual(1, len(self.api.writes))

    def test_closed_pr_closed_issue_and_hold_do_not_become_issue_candidates(self):
        self.api.issue['state'] = 'closed'
        self.assertEqual([], closure.candidates(self.api, self.workflow_event(), 'workflow_run', MERGE))
        self.api.issue['state'] = 'open'
        self.api.acceptance['decision'] = 'hold'
        self.api.sync_records()
        self.assertEqual([], closure.candidates(self.api, self.workflow_event(), 'workflow_run', MERGE))

    def test_issue_is_not_a_pull_request(self):
        self.api.issue['pull_request'] = {}
        self.held('open issue')

    def test_event_foreign_run_nonmain_and_untrusted_comment_are_rejected(self):
        for key, value in (('head_sha', HEAD), ('head_branch', 'topic'), ('name', 'Foreign workflow'),
                           ('event', 'pull_request'), ('head_repository', {'full_name': 'stranger/project-lucid'})):
            event = copy.deepcopy(self.workflow_event())
            event['workflow_run'][key] = value
            with self.assertRaises(closure.Blocked):
                closure.candidates(self.api, event, 'workflow_run', MERGE)
        event = {'repository': self.api.repo, 'action': 'created', 'issue': {'number': PR, 'pull_request': {}},
                 'comment': {'body': 'Refs #34'}}
        self.assertEqual([], closure.candidates(self.api, event, 'issue_comment', MERGE))
        event['issue'].pop('pull_request')
        event['comment']['body'] = self.api.rows[-1]['body']
        self.assertEqual([], closure.candidates(self.api, event, 'issue_comment', MERGE))

    def test_manual_retry_needs_an_explicit_merged_anchor(self):
        event = {'repository': self.api.repo, 'inputs': {'pull_request': str(PR)}}
        self.assertEqual([(PR, 202)], closure.candidates(self.api, event, 'workflow_dispatch', MERGE))
        event['inputs']['pull_request'] = '12; rm anything'
        with self.assertRaises(closure.Blocked):
            closure.candidates(self.api, event, 'workflow_dispatch', MERGE)

    def test_paginated_comments_and_jobs_are_complete_and_overflow_blocks(self):
        self.api.rows = [{'id': index, 'body': 'Unrelated discussion.'} for index in range(1, 102)] + self.api.rows
        self.assertEqual(ISSUE, self.qualify()['issue'])
        self.assertTrue(any('/comments?per_page=100&page=2' in path for path, _, _ in self.api.requests))
        self.api.rows = [{'id': index, 'body': 'Unrelated discussion.'} for index in range(1, 501)] + self.api.rows
        self.held('pagination limit')

    def test_invalid_ids_sha_types_and_duplicate_contributors_fail_closed(self):
        for field, value in (('issue', True), ('settled_sha', None), ('issue_scope_sha256', []),
                             ('pull_requests', []), ('pull_requests', self.api.acceptance['pull_requests'] * 2)):
            self.api = FakeGitHub()
            self.api.acceptance[field] = value
            self.api.sync_records()
            self.held()

    def test_contributor_and_issue_candidate_counts_are_bounded(self):
        contributor = {'number': 11, 'head_sha': HEAD, 'merge_sha': MERGE}
        self.api.acceptance['pull_requests'] = [contributor] * 6
        self.api.sync_records()
        self.held('PR count exceeds bound')
        self.api = FakeGitHub()
        for index in range(3):
            self.api.rows.append(attested(dict(self.api.acceptance, issue=100 + index), 203 + index, ACCEPTED_AT))
        with self.assertRaisesRegex(closure.Blocked, 'candidate count'):
            closure.candidates(self.api, self.workflow_event(), 'workflow_run', MERGE)
        self.assertEqual([], self.api.writes)

    def test_largest_contributor_set_still_fits_the_api_budget_for_one_closure(self):
        for index in range(4):
            number = 11 - index
            merge = str(index + 1) * 40
            self.api.prs[number] = dict(self.api.prs[PR], number=number, merge_commit_sha=merge)
            contributor = {'number': number, 'head_sha': HEAD, 'merge_sha': merge}
            self.api.acceptance['pull_requests'].append(copy.deepcopy(contributor))
            self.api.verification['pull_requests'].append(copy.deepcopy(contributor))
            self.api.add_checks(merge)
        self.api.sync_records()
        self.api.generated_child()
        self.assertEqual('closed', closure.close(self.api, self.qualify()))
        self.assertLess(len(self.api.requests), closure.MAX_REQUESTS)

    def test_run_pagination_overflow_and_noninteger_job_check_id_hold(self):
        self.api.run_lists[(MERGE, 'tool-tests.yml')] *= 500
        self.held('pagination limit')
        self.api = FakeGitHub()
        self.api.jobs[1000][0]['check_run_url'] = 'https://api.github.com' + closure.ROOT + '/check-runs/not-an-id'
        self.held('check identity')

    def test_no_opt_in_means_no_scan_of_existing_backlog(self):
        self.api.rows = [{'id': 1, 'body': 'Refs #34 and #35. Partial fix.'}]
        self.assertEqual([], closure.candidates(self.api, self.workflow_event(), 'workflow_run', MERGE))
        self.assertFalse(any('/issues/34' in path for path, _, _ in self.api.requests))
        self.assertEqual([], self.api.writes)

    def test_main_cli_is_read_only_by_default(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'event.json'
            path.write_text(json.dumps(self.workflow_event()))
            env = {'GITHUB_REPOSITORY': closure.REPOSITORY, 'GITHUB_SHA': MERGE,
                   'GITHUB_REF': 'refs/heads/main', 'GITHUB_EVENT_NAME': 'workflow_run',
                   'GITHUB_EVENT_PATH': str(path)}
            output = io.StringIO()
            with mock.patch.dict(os.environ, env), mock.patch.object(closure, 'GitHub', return_value=self.api), \
                    mock.patch('sys.argv', ['issue_closure.py']), contextlib.redirect_stdout(output):
                closure.main()
            self.assertEqual('qualified-dry-run', json.loads(output.getvalue())['result'])
            self.assertNotIn('authorization_sha256', json.loads(output.getvalue())['plan'])
            self.assertNotIn('verification_principals', output.getvalue())
            self.assertEqual([], self.api.writes)

    def test_http_client_blocks_foreign_targets_redirects_unexpected_writes_and_request_budget(self):
        api = closure.GitHub(token='fixture-token')
        with mock.patch('urllib.request.build_opener') as network:
            for path, method, body in (('/repos/foreign/repo/issues/1', 'GET', None),
                                        (closure.ROOT + '/issues/1/comments', 'POST', {'body': 'x'}),
                                        (closure.ROOT + '/issues/1', 'PATCH', {'state': 'open'})):
                with self.assertRaises(closure.Blocked):
                    api.request(path, method, body)
            api.calls = closure.MAX_REQUESTS
            with self.assertRaisesRegex(closure.Blocked, 'budget'):
                api.request(closure.ROOT)
            network.assert_not_called()

    def test_workflow_control_is_pinned_read_only_and_never_executes_pr_inputs(self):
        workflow = (Path(__file__).resolve().parents[1] / 'workflows/issue-closure.yml').read_text()
        self.assertIn('ref: ${{ github.sha }}', workflow)
        self.assertIn('persist-credentials: false', workflow)
        self.assertIn('actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803', workflow)
        self.assertIn('issues: read', workflow)
        self.assertNotIn('write', workflow)
        self.assertNotIn('--apply', workflow)
        self.assertEqual(1, workflow.count('secrets.'))
        self.assertIn('ISSUE_CLOSURE_AUTHORIZATION: ${{ secrets.ISSUE_CLOSURE_AUTHORIZATION }}', workflow)
        self.assertNotIn('pull_request_target', workflow)
        self.assertNotIn('github.event.comment.body', workflow)

    def test_nonmain_dispatch_is_guarded_before_checkout_and_token_use(self):
        workflow = (Path(__file__).resolve().parents[1] / 'workflows/issue-closure.yml').read_text()
        guard = "    if: github.repository == 'dev-camo/project-lucid' && github.ref == 'refs/heads/main'"
        self.assertIn(guard, workflow)
        self.assertLess(workflow.index(guard), workflow.index('uses: actions/checkout@'))
        self.assertLess(workflow.index(guard), workflow.index('GH_TOKEN:'))
        self.assertNotIn('if: github.repository ==', workflow.replace(guard, ''))

    def test_pr_compilation_run_name_binds_actual_webhook_upstream_provenance(self):
        workflow = (Path(__file__).resolve().parents[1] / 'workflows/pr-compilation.yml').read_text()
        expected = ('PR compilation (upstream ${{ github.event.workflow_run.id }}/'
                    '${{ github.event.workflow_run.run_attempt }} ${{ github.event.workflow_run.event }} '
                    '${{ github.event.workflow_run.head_sha }})')
        self.assertIn('run-name: >-\n  ' + expected + '\n', workflow)
        self.assertNotIn('run-name: ${{ github.sha }}', workflow)


if __name__ == '__main__':
    unittest.main()

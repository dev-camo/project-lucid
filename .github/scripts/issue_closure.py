"""Validate whole-issue verification and approval against trusted main checks."""
import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

REPOSITORY = 'dev-camo/project-lucid'
ROOT = '/repos/' + REPOSITORY
RECORD_KINDS = {'verification', 'approval'}
AUTHORIZATION_ENV = 'ISSUE_CLOSURE_AUTHORIZATION'
AUTHORIZATION_FIELDS = {'verification_principals', 'approval_principals', 'excluded_principals'}
LOGIN = re.compile(r'[A-Za-z0-9][A-Za-z0-9-]{0,38}\Z')
MARKER = '<!-- lucid-issue-acceptance:v1 -->\n'
SHA = re.compile(r'[0-9a-f]{40}\Z')
DIGEST = re.compile(r'[0-9a-f]{64}\Z')
MAX_PAGES = 5
MAX_CANDIDATES = 3
MAX_REQUESTS = 700
REVIEW_QUERY = '''query IssueClosureReviews($owner:String!, $name:String!, $number:Int!, $cursor:String) {
  repository(owner:$owner, name:$name) {
    nameWithOwner
    pullRequest(number:$number) {
      number
      reviews(first:100, after:$cursor) {
        nodes { databaseId body state createdAt submittedAt lastEditedAt
          commit { oid } author { __typename login ... on User { databaseId } } }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}'''
TOOL_JOBS = {'Python (ubuntu-24.04, 3.13)', 'Python (macos-15, 3.13)',
             'C# identity (ubuntu-24.04)', 'C# identity (windows-2025)',
             'C# identity (macos-15)', 'C# identity (macos-15-intel)'}
WORKFLOWS = {
    'tool-tests.yml': ('Tool tests', 'push', {name: 'success' for name in TOOL_JOBS}),
    'code-progress.yml': ('Code progress', 'push', {'check': 'success'}),
    'pr-compilation.yml': ('PR compilation', 'workflow_run',
                           {'gate': 'success', 'Native compilation and evidence': 'skipped'}),
    'native-progress.yml': ('Refresh native progress', 'push',
                            {'gate': 'success', 'Merged main compilation and progress': 'success',
                             'publish': 'success'}),
}
COMMON = {'issue', 'issue_scope_sha256', 'settled_sha', 'pull_requests',
          'scope', 'all_issue_criteria_verified'}
APPROVAL_FIELDS = COMMON | {'kind', 'decision', 'verification_review_id', 'independent_verification_accepted'}
VERIFICATION_FIELDS = COMMON | {'kind', 'independent', 'publication_verified', 'criteria'}


class Blocked(ValueError):
    """Qualification is incomplete or unsafe; leave the issue open."""


class Uncertain(Blocked):
    """A closure response was lost; inspect the exact issue before retrying."""


class GitHub:
    def __init__(self, token=None):
        self.token = os.environ.get('GH_TOKEN', '') if token is None else token
        self.calls = 0

    def request(self, path, method='GET', body=None):
        self.calls += 1
        if self.calls > MAX_REQUESTS:
            raise Blocked('API request budget exhausted')
        query = (path == '/graphql' and method == 'POST' and isinstance(body, dict)
                 and set(body) == {'query', 'variables'} and body['query'] == REVIEW_QUERY)
        if query:
            variables = body['variables']
            require(isinstance(variables, dict) and set(variables) == {'owner', 'name', 'number', 'cursor'}
                    and (variables['owner'], variables['name']) == tuple(REPOSITORY.split('/'))
                    and positive(variables['number'])
                    and (variables['cursor'] is None or isinstance(variables['cursor'], str)
                         and 0 < len(variables['cursor']) <= 1024), 'Invalid review query target')
        if not query and not (path == ROOT or path.startswith(ROOT + '/')):
            raise Blocked('Foreign API target')
        if not query and method != 'GET' and not (method == 'PATCH'
                and re.fullmatch(re.escape(ROOT) + r'/issues/[1-9][0-9]*', path)
                and body == {'state': 'closed', 'state_reason': 'completed'}):
            raise Blocked('Unexpected mutation')
        headers = {'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2026-03-10'}
        if self.token:
            headers['Authorization'] = 'Bearer ' + self.token
        request = urllib.request.Request('https://api.github.com' + path,
            data=None if body is None else json.dumps(body).encode(), headers=headers, method=method)
        # Never follow a redirect carrying credentials to another host.
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self, req, fp, code, msg, headers, newurl):
                return None
        with urllib.request.build_opener(NoRedirect).open(request, timeout=15) as response:
            raw = response.read(2 * 1024 * 1024 + 1)
        if len(raw) > 2 * 1024 * 1024:
            raise Blocked('API response exceeds bound')
        return json.loads(raw)


def require(condition, reason):
    if not condition:
        raise Blocked(reason)


def positive(value):
    return type(value) is int and value > 0


def matches(pattern, value):
    return isinstance(value, str) and pattern.fullmatch(value) is not None


def timestamp(value):
    require(isinstance(value, str), 'Missing evidence timestamp')
    try:
        result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError as error:
        raise Blocked('Invalid evidence timestamp') from error
    require(result.tzinfo is not None, 'Evidence timestamp lacks timezone')
    return result


def scope_digest(issue):
    scope = {'title': issue.get('title') or '', 'body': issue.get('body') or ''}
    require(all(isinstance(value, str) for value in scope.values()), 'Invalid issue scope')
    raw = json.dumps(scope, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')
    return hashlib.sha256(raw).hexdigest()


def evidence_digest(approval, verification):
    fields = ('id', 'body', 'user', 'created_at', 'updated_at', 'submitted_at',
              'last_edited_at', 'state', 'commit_sha', 'source')
    raw = [{key: comment.get(key) for key in fields} for comment in (approval, verification)]
    return hashlib.sha256(json.dumps(raw, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def require_no_later_edits(rows, accepted_at):
    for row in rows:
        if row['last_edited_at'] is not None:
            require(timestamp(row['last_edited_at']) < timestamp(accepted_at),
                    'Later edited discussion requires renewed acceptance')


def review_barrier(rows, approval, verification):
    # Submitted reviews cannot be deleted. Keep all rows, including unmarked,
    # edited and dismissed summaries, before interpreting acceptance payloads.
    submitted = [row for row in rows if row['state'] != 'PENDING']
    require(all(row['id'] == approval['id'] or timestamp(row['submitted_at']) <
                timestamp(approval['submitted_at']) for row in submitted),
            'Later or same-time submitted review invalidates approval')
    require(timestamp(verification['submitted_at']) < timestamp(approval['submitted_at']),
            'Verification must be submitted before approval')
    require(all(row['id'] in {approval['id'], verification['id']} or
                timestamp(row['submitted_at']) < timestamp(verification['submitted_at'])
                for row in submitted), 'Intervening submitted review invalidates verification')


def review_history_digest(rows):
    return hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def paginated(api, path, key=None):
    rows = []
    for page in range(1, MAX_PAGES + 1):
        data = api.request(path + ('&' if '?' in path else '?') + f'per_page=100&page={page}')
        batch = data if key is None else data.get(key)
        require(isinstance(batch, list), 'Invalid API collection')
        require(key is None or data.get('total_count', 0) <= MAX_PAGES * 100,
                'API collection exceeds bound')
        rows.extend(batch)
        if len(batch) < 100:
            return rows
    raise Blocked('API pagination limit reached')


def default_head(api):
    repo = api.request(ROOT)
    require(repo.get('full_name') == REPOSITORY and repo.get('default_branch') == 'main',
            'Canonical default branch required')
    commit = api.request(ROOT + '/commits/main')
    require(matches(SHA, commit.get('sha')), 'Invalid default head')
    return commit


def immutable(comment):
    return (comment.get('source') == 'submitted_review' and comment.get('state') == 'COMMENTED'
            and comment.get('submitted_at') is not None and comment.get('last_edited_at') is None)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate acceptance field')
        result[key] = value
    return result


def authorization_policy():
    raw = os.environ.get(AUTHORIZATION_ENV, '')
    require(isinstance(raw, str) and 0 < len(raw.encode()) <= 4096,
            'Issue closure authorization is not configured or exceeds its bound')
    try:
        policy = json.loads(raw, object_pairs_hook=unique_object)
    except (ValueError, RecursionError) as error:
        raise Blocked('Malformed authorization policy') from error
    require(isinstance(policy, dict) and set(policy) == AUTHORIZATION_FIELDS,
            'Authorization policy fields differ')
    for field in AUTHORIZATION_FIELDS:
        principals = policy[field]
        minimum = 0 if field == 'excluded_principals' else 1
        require(isinstance(principals, list) and minimum <= len(principals) <= 10
                and all(positive(principal) for principal in principals)
                and len(set(principals)) == len(principals), 'Invalid authorization principal set')
        policy[field] = sorted(principals)
    require(not set(policy['excluded_principals']) &
            (set(policy['verification_principals']) | set(policy['approval_principals'])),
            'Excluded principals cannot receive authorization')
    return policy


def authorization_digest(policy):
    return hashlib.sha256(json.dumps(policy, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def record(comment):
    body = comment.get('body') or ''
    if not body.startswith(MARKER):
        return None
    require(len(body.encode('utf-8')) <= 16384, 'Acceptance exceeds bound')
    try:
        value = json.loads(body[len(MARKER):], object_pairs_hook=unique_object)
    except (ValueError, RecursionError) as error:
        raise Blocked('Malformed acceptance JSON') from error
    require(isinstance(value, dict) and isinstance(value.get('kind'), str)
            and value['kind'] in RECORD_KINDS, 'Acceptance record kind differs')
    return value


def trusted(api, comment, kind, policy):
    require(isinstance(kind, str) and kind in RECORD_KINDS, 'Record kind has no acceptance authority')
    user = comment.get('user') or {}
    require(positive(user.get('id')) and user['id'] in policy[kind + '_principals']
            and user['id'] not in policy['excluded_principals'] and user.get('type') == 'User'
            and matches(LOGIN, user.get('login')), 'Acceptance must be authenticated by an authorized principal')
    permission = api.request(ROOT + '/collaborators/' + urllib.parse.quote(user['login'], safe='') + '/permission')
    current = permission.get('user') or {}
    require(permission.get('permission') in {'write', 'admin'}
            and current.get('login') == user['login'] and current.get('id') == user['id']
            and current.get('type') == 'User', 'Current authorized principal write permission required')
    require(positive(comment.get('id')) and immutable(comment), 'Edited acceptance must be replaced')


def validate_record(value, fields):
    require(set(value) == fields, 'Acceptance fields differ')
    require(positive(value['issue']) and matches(DIGEST, value['issue_scope_sha256'])
            and matches(SHA, value['settled_sha']), 'Invalid acceptance identity')
    require(value['scope'] == 'whole-issue' and value['all_issue_criteria_verified'] is True,
            'Partial acceptance cannot close an issue')
    prs = value['pull_requests']
    require(isinstance(prs, list) and 1 <= len(prs) <= 5, 'Contributing PR count exceeds bound')
    seen = set()
    for pr in prs:
        require(isinstance(pr, dict) and set(pr) == {'number', 'head_sha', 'merge_sha'}
                and positive(pr['number']) and pr['number'] not in seen
                and matches(SHA, pr['head_sha'])
                and matches(SHA, pr['merge_sha']), 'Invalid contributing PR identity')
        seen.add(pr['number'])


def reviews(api, pr):
    owner, name = REPOSITORY.split('/')
    rows, cursor = [], None
    for _ in range(MAX_PAGES):
        result = api.request('/graphql', 'POST', {'query': REVIEW_QUERY,
            'variables': {'owner': owner, 'name': name, 'number': pr, 'cursor': cursor}})
        require(not result.get('errors'), 'Review query failed or returned partial data')
        repository = (result.get('data') or {}).get('repository') or {}
        pull = repository.get('pullRequest') or {}
        require(repository.get('nameWithOwner') == REPOSITORY and pull.get('number') == pr,
                'Review query repository or PR differs')
        connection = pull.get('reviews') or {}
        batch = connection.get('nodes')
        require(isinstance(batch, list) and len(batch) <= 100, 'Invalid review collection')
        for row in batch:
            require(isinstance(row, dict) and all(key in row for key in
                ('databaseId', 'body', 'state', 'createdAt', 'submittedAt', 'lastEditedAt', 'author', 'commit'))
                and positive(row['databaseId']) and isinstance(row['body'], str)
                and row['state'] in {'PENDING', 'COMMENTED', 'APPROVED', 'CHANGES_REQUESTED', 'DISMISSED'},
                'Invalid submitted review metadata')
            timestamp(row['createdAt'])
            if row['state'] != 'PENDING':
                timestamp(row['submittedAt'])
            author = row['author'] or {}
            rows.append({'id': row['databaseId'], 'body': row['body'], 'state': row['state'],
                'created_at': row['submittedAt'] or row['createdAt'],
                'updated_at': row['lastEditedAt'] or row['submittedAt'] or row['createdAt'],
                'submitted_at': row['submittedAt'], 'last_edited_at': row['lastEditedAt'],
                'commit_sha': (row['commit'] or {}).get('oid'), 'source': 'submitted_review',
                'user': {'login': author.get('login'), 'id': author.get('databaseId'),
                         'type': author.get('__typename')}})
        page = connection.get('pageInfo') or {}
        require(type(page.get('hasNextPage')) is bool, 'Invalid review pagination')
        if not page['hasNextPage']:
            require(len({row['id'] for row in rows}) == len(rows), 'Duplicate review identity')
            return rows
        next_cursor = page.get('endCursor')
        require(isinstance(next_cursor, str) and 0 < len(next_cursor) <= 1024 and next_cursor != cursor,
                'Invalid review cursor')
        cursor = next_cursor
    raise Blocked('Review pagination limit reached')


def approval_records(rows):
    # Only the latest submitted row can grant. Historical bodies never grant or
    # poison a fresh pair; their rows remain durable barriers and digest entries.
    submitted = [row for row in rows if row['state'] != 'PENDING']
    if not submitted:
        return {}
    newest_at = max(timestamp(row['submitted_at']) for row in submitted)
    newest = [row for row in submitted if timestamp(row['submitted_at']) == newest_at]
    require(len(newest) == 1,
            'Later or same-time submitted review invalidates approval; latest submission is ambiguous')
    comment = newest[0]
    value = record(comment)
    if not value or value['kind'] != 'approval':
        return {}
    validate_record(value, APPROVAL_FIELDS)
    require(value['decision'] in {'close', 'hold'}, 'Unknown approval disposition')
    return {value['issue']: (comment, value)}


def source_head(api, settled):
    """Recognize only one direct generated report child, never arbitrary ancestry."""
    author = settled.get('author') or {}
    committer = settled.get('committer') or {}
    if author.get('login') != 'github-actions[bot]' or committer.get('login') != 'github-actions[bot]':
        return settled['sha']
    require(author.get('type') == 'Bot' and committer.get('type') == 'Bot', 'Generated bot identity differs')
    require(settled.get('commit', {}).get('message') == 'Update verified main code progress',
            'Generated commit message differs')
    require(len(settled.get('parents', [])) == 1, 'Generated commit must have one parent')
    files = settled.get('files', [])
    require(len(files) == 1 and files[0].get('filename') == 'docs/code-progress.json'
            and files[0].get('status') == 'modified' and not files[0].get('previous_filename'),
            'Only a direct docs/code-progress.json updater child is admitted')
    source = settled['parents'][0].get('sha', '')
    require(matches(SHA, source), 'Invalid generated parent')
    return source


def compilation_title(upstream):
    return f"PR compilation (upstream {upstream['id']}/{upstream['attempt']} push {upstream['sha']})"


def latest_run(api, sha, filename, upstream=None, settled_sha=None, verify_jobs=True):
    name, event, required_jobs = WORKFLOWS[filename]
    workflow = api.request(ROOT + '/actions/workflows/' + filename)
    require(workflow.get('path') == '.github/workflows/' + filename
            and workflow.get('name') == name and workflow.get('state') == 'active'
            and positive(workflow.get('id')), 'Trusted workflow identity differs')
    query = {'head_sha': sha, 'event': event}
    if filename == 'pr-compilation.yml':
        require(upstream is not None and upstream['workflow'] == 'code-progress.yml'
                and upstream['sha'] == sha, 'Exact upstream main Code progress required')
        timestamp(upstream['created_at'])
        # workflow_run.head_sha is default control, not the upstream source SHA.
        query = {'event': event, 'created': '>=' + upstream['created_at']}
    runs = paginated(api, ROOT + '/actions/workflows/' + filename + '/runs?'
                     + urllib.parse.urlencode(query), 'workflow_runs')
    require(all(positive(row.get('id')) for row in runs), 'Invalid workflow run identity')
    if upstream is not None:
        runs = [row for row in runs if row.get('display_title') == compilation_title(upstream)]
    require(bool(runs), 'Required postmerge workflow has not run for its exact trigger: ' + name)
    run = api.request(ROOT + '/actions/runs/' + str(max(row['id'] for row in runs)))
    require(run.get('workflow_id') == workflow['id'] and run.get('path') == workflow['path']
            and matches(SHA, run.get('head_sha')) and run.get('head_branch') == 'main'
            and run.get('event') == event and run.get('repository', {}).get('full_name') == REPOSITORY
            and run.get('head_repository', {}).get('full_name') == REPOSITORY
            and positive(run.get('run_attempt')), 'Foreign or wrong-SHA workflow rejected')
    if upstream is None:
        require(run['head_sha'] == sha, 'Workflow source SHA differs')
    else:
        require(run.get('display_title') == compilation_title(upstream), 'Workflow upstream identity changed')
        require(matches(SHA, settled_sha), 'Settled control ancestry required')
        if run['head_sha'] != settled_sha:
            comparison = api.request(ROOT + f"/compare/{run['head_sha']}...{settled_sha}")
            require(comparison.get('status') in {'ahead', 'identical'}
                    and comparison.get('base_commit', {}).get('sha') == run['head_sha']
                    and comparison.get('merge_base_commit', {}).get('sha') == run['head_sha'],
                    'Workflow control is not contained in the attested main')
    require(run.get('status') == 'completed' and run.get('conclusion') == 'success',
            'Latest workflow attempt is not successful: ' + name)
    timestamp(run.get('created_at'))
    snapshot = {'id': run['id'], 'attempt': run['run_attempt'], 'sha': sha, 'workflow': filename,
                'control_sha': run['head_sha'], 'created_at': run['created_at']}
    if upstream is not None:
        snapshot['upstream'] = upstream
    if not verify_jobs:
        return snapshot
    jobs = paginated(api, ROOT + f"/actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs", 'jobs')
    require(len(jobs) == len(required_jobs) and {job.get('name') for job in jobs} == set(required_jobs),
            'Required postmerge job roster differs: ' + name)
    for job in jobs:
        require(job.get('status') == 'completed' and job.get('conclusion') == required_jobs[job['name']]
                and job.get('head_sha') == run['head_sha'] and job.get('run_id') == run['id']
                and job.get('run_attempt') == run['run_attempt'], 'Required job did not qualify: ' + job['name'])
        url = job.get('check_run_url', '')
        prefix = 'https://api.github.com' + ROOT + '/check-runs/'
        require(isinstance(url, str) and re.fullmatch(re.escape(prefix) + r'[1-9][0-9]*', url) is not None,
                'Job check identity differs')
        check = api.request(url[len('https://api.github.com'):])
        require(check.get('app', {}).get('id') == 15368 and check.get('head_sha') == run['head_sha']
                and check.get('name') == job['name'] and check.get('status') == 'completed'
                and check.get('conclusion') == job['conclusion'], 'Trusted GitHub Actions check required')
    return snapshot


def merged_checks(api, sha, settled_sha, native=True):
    selected = {}
    for filename in WORKFLOWS:
        if not native and filename == 'native-progress.yml':
            continue
        upstream = selected.get('code-progress.yml') if filename == 'pr-compilation.yml' else None
        selected[filename] = latest_run(api, sha, filename, upstream, settled_sha)
    return list(selected.values())


def qualify(api, anchor_number, comment_id, control_sha):
    policy = authorization_policy()
    settled = default_head(api)
    require(settled['sha'] == control_sha, 'Control or settled main advanced; renew acceptance')
    source = source_head(api, settled)
    anchor = api.request(ROOT + f'/pulls/{anchor_number}')
    require(anchor.get('merged') is True and anchor.get('merge_commit_sha') == source,
            'Acceptance anchor must be the actual latest merged PR')
    rows = reviews(api, anchor_number)
    selected = next(((comment, value) for comment, value in approval_records(rows).values()
                     if comment['id'] == comment_id), None)
    require(selected is not None, 'Approval was removed or superseded')
    approval, value = selected
    trusted(api, approval, value['kind'], policy)
    require(value['decision'] == 'close' and value['independent_verification_accepted'] is True
            and positive(value['verification_review_id']), 'Explicit closure approval required')
    require(value['settled_sha'] == settled['sha'], 'Acceptance settled SHA is stale')
    require_no_later_edits(rows, approval['created_at'])
    require(timestamp(anchor['updated_at']) <= timestamp(approval['created_at']),
            'PR changed after approval acceptance; renew the record')
    issue = api.request(ROOT + f"/issues/{value['issue']}")
    require('pull_request' not in issue and issue.get('number') == value['issue']
            and issue.get('state') == 'open', 'An open issue is required')
    require(scope_digest(issue) == value['issue_scope_sha256'], 'Issue criteria changed')
    require(timestamp(issue['updated_at']) <= timestamp(approval['created_at']),
            'Issue changed or reopened after acceptance')
    verification = next((row for row in rows if row['id'] == value['verification_review_id']), None)
    require(verification is not None and verification['id'] != approval['id'], 'Separate verification evidence required')
    verified = record(verification)
    require(verified is not None, 'Structured verification evidence required')
    validate_record(verified, VERIFICATION_FIELDS)
    trusted(api, verification, verified['kind'], policy)
    require(approval['commit_sha'] == anchor.get('head', {}).get('sha')
            and verification['commit_sha'] == anchor.get('head', {}).get('sha'), 'Review commit differs from exact PR head')
    review_barrier(rows, approval, verification)
    require(verified['kind'] == 'verification' and verified['independent'] is True
            and verified['publication_verified'] is True
            and {key: verified[key] for key in COMMON} == {key: value[key] for key in COMMON},
            'Whole-issue verification and publication acceptance required')
    criteria = verified['criteria']
    require(isinstance(criteria, list) and 1 <= len(criteria) <= 30, 'Explicit criterion evidence required')
    for criterion in criteria:
        require(isinstance(criterion, dict) and set(criterion) == {'criterion', 'result', 'evidence'}
                and criterion['result'] == 'passed'
                and all(isinstance(criterion[key], str) and 1 <= len(criterion[key].strip()) <= 2048
                        for key in ('criterion', 'evidence')), 'Issue criterion did not pass with evidence')
    require(timestamp(verification['created_at']) <= timestamp(approval['created_at']),
            'Approval must accept the completed independent verification')
    runs, contributors = [], []
    require(anchor_number in {pr['number'] for pr in value['pull_requests']}, 'Anchor omitted from contributor set')
    for expected in value['pull_requests']:
        pr = api.request(ROOT + f"/pulls/{expected['number']}")
        require(pr.get('merged') is True and pr.get('state') == 'closed'
                and pr.get('head', {}).get('sha') == expected['head_sha']
                and pr.get('merge_commit_sha') == expected['merge_sha']
                and pr.get('base', {}).get('ref') == 'main'
                and pr.get('base', {}).get('repo', {}).get('full_name') == REPOSITORY,
                'Contributing PR is unmerged, changed or foreign')
        require(timestamp(pr['merged_at']) <= timestamp(verification['created_at']),
                'Independent verification must follow every actual merge')
        require(timestamp(pr['updated_at']) <= timestamp(approval['created_at']),
                'Contributing PR changed after acceptance')
        contributors.append(dict(expected, updated_at=pr['updated_at']))
        comparison = api.request(ROOT + f"/compare/{expected['merge_sha']}...{source}")
        require(comparison.get('status') in {'ahead', 'identical'}
                and comparison.get('base_commit', {}).get('sha') == expected['merge_sha']
                and comparison.get('merge_base_commit', {}).get('sha') == expected['merge_sha'],
                'Contributing merge is not contained in the attested main')
        runs.extend(merged_checks(api, expected['merge_sha'], settled['sha']))
    if settled['sha'] != source:
        runs.extend(merged_checks(api, settled['sha'], settled['sha'], native=False))
    plan = {'issue': value['issue'], 'anchor_pr': anchor_number, 'approval_review': approval['id'],
            'verification_review': verification['id'], 'settled_sha': settled['sha'], 'source_sha': source,
            'issue_updated_at': issue['updated_at'], 'issue_scope_sha256': value['issue_scope_sha256'],
            'anchor_updated_at': anchor['updated_at'], 'accepted_at': approval['created_at'],
            'evidence_sha256': evidence_digest(approval, verification), 'review_history_sha256': review_history_digest(rows),
            'authorization_sha256': authorization_digest(policy), 'contributors': contributors, 'runs': runs}
    require_current(api, plan)
    return plan


def require_current(api, plan):
    policy = authorization_policy()
    require(authorization_digest(policy) == plan['authorization_sha256'],
            'Authorization changed during qualification')
    require(default_head(api)['sha'] == plan['settled_sha'], 'Main advanced during qualification')
    issue = api.request(ROOT + f"/issues/{plan['issue']}")
    require(issue.get('state') == 'open' and issue.get('updated_at') == plan['issue_updated_at']
            and scope_digest(issue) == plan['issue_scope_sha256'], 'Issue changed during qualification')
    for expected in plan['contributors']:
        pr = api.request(ROOT + f"/pulls/{expected['number']}")
        require(pr.get('updated_at') == expected['updated_at'] and pr.get('merged') is True
                and pr.get('head', {}).get('sha') == expected['head_sha']
                and pr.get('merge_commit_sha') == expected['merge_sha'], 'Contributing PR changed during qualification')
    rows = reviews(api, plan['anchor_pr'])
    require(review_history_digest(rows) == plan['review_history_sha256'], 'Submitted review history changed')
    require_no_later_edits(rows, plan['accepted_at'])
    selected = approval_records(rows).get(plan['issue'])
    require(selected is not None and selected[0]['id'] == plan['approval_review'],
            'Approval removed or superseded during qualification')
    verification = next((row for row in rows if row['id'] == plan['verification_review']), None)
    require(verification is not None and evidence_digest(selected[0], verification) == plan['evidence_sha256'],
            'Acceptance changed during qualification')
    trusted(api, selected[0], 'approval', policy)
    trusted(api, verification, 'verification', policy)
    review_barrier(rows, selected[0], verification)
    # Refresh selection and current attempts after all earlier job/check reads.
    # In apply mode this is the final gate before PATCH, not a cached green result.
    for run in plan['runs']:
        current = latest_run(api, run['sha'], run['workflow'], run.get('upstream'),
                             plan['settled_sha'], verify_jobs=False)
        require(current == run, 'Workflow qualification changed before closure')


def close(api, plan):
    # Rebuild the entire gate, including latest attempts and immutable comments.
    require(qualify(api, plan['anchor_pr'], plan['approval_review'], plan['settled_sha']) == plan,
            'Qualification changed before closure')
    require_current(api, plan)
    path = ROOT + f"/issues/{plan['issue']}"
    try:
        response = api.request(path, 'PATCH', {'state': 'closed', 'state_reason': 'completed'})
    except (urllib.error.URLError, TimeoutError, OSError) as error:
        try:
            response = api.request(path)
        except (urllib.error.URLError, TimeoutError, OSError) as read_error:
            raise Uncertain('Closure and readback unconfirmed; inspect before retry') from read_error
        if response.get('number') != plan['issue'] or response.get('state') != 'closed' \
                or response.get('state_reason') != 'completed':
            raise Uncertain('Closure outcome uncertain; inspect before any retry') from error
        return 'closed-confirmed-after-error'
    if response.get('number') != plan['issue'] or response.get('state') != 'closed' \
            or response.get('state_reason') != 'completed':
        raise Uncertain('Closure response unconfirmed; inspect before retry')
    return 'closed'


def candidates(api, event, event_name, control_sha):
    require(event.get('repository', {}).get('full_name') == REPOSITORY, 'Event repository differs')
    settled = default_head(api)
    require(settled['sha'] == control_sha, 'Stale control commit')
    source = source_head(api, settled)
    if event_name == 'workflow_run':
        run = event.get('workflow_run') or {}
        if run.get('name') == 'Issue acceptance event':
            require(positive(run.get('id')), 'Invalid review wake-up run')
            admitted = api.request(ROOT + '/actions/runs/' + str(run.get('id')))
            workflow = api.request(ROOT + '/actions/workflows/issue-acceptance-event.yml')
            require(positive(workflow.get('id')) and workflow.get('name') == 'Issue acceptance event'
                    and workflow.get('path') == '.github/workflows/issue-acceptance-event.yml'
                    and workflow.get('state') == 'active'
                    and admitted.get('id') == run.get('id') and admitted.get('workflow_id') == workflow.get('id')
                    and admitted.get('path') == '.github/workflows/issue-acceptance-event.yml'
                    and admitted.get('repository', {}).get('full_name') == REPOSITORY
                    and admitted.get('event') == 'pull_request_review' and admitted.get('status') == 'completed',
                    'Review wake-up identity differs')
        else:
            # Configured run-name changes run.name; registered ID/path admit a
            # wake-up while the full qualification gate still binds upstream.
            require(positive(run.get('id')), 'Invalid completion wake-up run')
            admitted = api.request(ROOT + '/actions/runs/' + str(run['id']))
            filename = next((name for name in WORKFLOWS
                if admitted.get('path') == '.github/workflows/' + name), None)
            require(filename is not None, 'Postmerge workflow path differs')
            workflow = api.request(ROOT + '/actions/workflows/' + filename)
            name, expected_event, _ = WORKFLOWS[filename]
            require(positive(workflow.get('id')) and workflow.get('name') == name
                and workflow.get('path') == admitted.get('path') and workflow.get('state') == 'active'
                and admitted.get('id') == run['id'] and admitted.get('workflow_id') == workflow['id']
                and all(run.get(key) == admitted.get(key)
                        for key in ('workflow_id', 'head_sha', 'head_branch', 'event')),
                'Postmerge completion identity differs')
            require(all(row.get('repository', {}).get('full_name') == REPOSITORY
                and row.get('head_repository', {}).get('full_name') == REPOSITORY
                and row.get('head_branch') == 'main' and row.get('head_sha') in {source, settled['sha']}
                and row.get('event') == expected_event and row.get('status') == 'completed'
                for row in (run, admitted)),
                    'Only canonical postmerge workflow completion is admitted')
        prs = paginated(api, ROOT + f'/commits/{source}/pulls')
        numbers = [pr['number'] for pr in prs if pr.get('merge_commit_sha') == source and pr.get('merged_at')]
    elif event_name == 'issue_comment':
        return []
    elif event_name == 'workflow_dispatch':
        # Manual retries are restricted to default main and its merged anchor.
        number = str(event.get('inputs', {}).get('pull_request', ''))
        require(re.fullmatch(r'[1-9][0-9]*', number) is not None, 'Explicit merged PR required')
        numbers = [int(number)]
    else:
        raise Blocked('Event type is not admitted')
    require(len(numbers) <= 1, 'Ambiguous actual merge association')
    result = []
    for number in numbers:
        for approval, value in approval_records(reviews(api, number)).values():
            issue = api.request(ROOT + f"/issues/{value['issue']}")
            if issue.get('state') == 'open' and value['decision'] == 'close':
                result.append((number, approval['id']))
    require(len(result) <= MAX_CANDIDATES, 'Issue candidate count exceeds bound')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    require(os.environ.get('GITHUB_REPOSITORY') == REPOSITORY
            and matches(SHA, os.environ.get('GITHUB_SHA'))
            and os.environ.get('GITHUB_REF') == 'refs/heads/main', 'Trusted main workflow context required')
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    api = GitHub()
    try:
        pending = candidates(api, event, os.environ['GITHUB_EVENT_NAME'], os.environ['GITHUB_SHA'])
        for pr, comment in pending:
            try:
                plan = qualify(api, pr, comment, os.environ['GITHUB_SHA'])
                result = close(api, plan) if args.apply else 'qualified-dry-run'
                visible_plan = {key: value for key, value in plan.items() if key != 'authorization_sha256'}
                print(json.dumps({'result': result, 'plan': visible_plan}, sort_keys=True))
            except Uncertain as error:
                print(json.dumps({'result': 'closure-unconfirmed', 'anchor_pr': pr, 'reason': str(error)}))
            except Blocked as error:
                print(json.dumps({'result': 'held-open', 'anchor_pr': pr, 'reason': str(error)}))
        if not pending:
            print(json.dumps({'result': 'no-opt-in-candidates'}))
    except Blocked as error:
        print(json.dumps({'result': 'held-open', 'reason': str(error)}))


if __name__ == '__main__':
    main()

"""Compile admitted PRs without original inputs; generate progress from merged main.

Only hosted publication writes generated main data. It dispatches the real
required checks for the generated SHA and never bypasses branch protection.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import sys
import subprocess
import re
import time
import urllib.parse
import urllib.error

import direct_progress as shared

REPOSITORY = shared.REPOSITORY
MAIN = 'main'
REQUEST_FIELDS = {'head', 'control_commit'}
CHECKS = {'check', 'Python (ubuntu-24.04, 3.13)', 'Python (macos-15, 3.13)',
          'C# identity (ubuntu-24.04)', 'C# identity (windows-2025)',
          'C# identity (macos-15)', 'C# identity (macos-15-intel)'}
CHECK_APP = 15368


def require_main_context(request=None):
    if (os.environ.get('GITHUB_REPOSITORY') != REPOSITORY
            or os.environ.get('GITHUB_REF') != 'refs/heads/main'
            or os.environ.get('GITHUB_EVENT_NAME') not in {'push', 'workflow_dispatch'}
            or os.environ.get('GITHUB_RUN_ATTEMPT') != '1'
            or not shared.SHA.fullmatch(os.environ.get('GITHUB_SHA', ''))
            or not str(os.environ.get('GITHUB_RUN_ID', '')).isdigit()
            or int(os.environ.get('GITHUB_RUN_ID', '0')) <= 0):
        raise ValueError('Only first-attempt canonical main runs may generate or publish progress')
    if request is not None and (set(request) != REQUEST_FIELDS
            or request['head'] != request['control_commit']
            or request['head'] != os.environ['GITHUB_SHA']):
        raise ValueError('Main source/control/workflow identity differs')


def require_main_head(api, request):
    require_main_context(request)
    branch, head = shared.default_head(api)
    if branch != MAIN or head != request['head']:
        raise ValueError('Main advanced; discard this measurement and let a newer main run catch up')


def gate(api, event):
    require_main_context()
    if event.get('repository', {}).get('full_name') != REPOSITORY:
        raise ValueError('Event repository differs')
    if os.environ['GITHUB_EVENT_NAME'] == 'push' and (
            event.get('ref') != 'refs/heads/main' or event.get('after') != os.environ['GITHUB_SHA']):
        raise ValueError('Only the exact merged main push may generate progress')
    branch, head = shared.default_head(api)
    if branch != MAIN:
        raise ValueError('Main is not the protected default branch')
    if head != os.environ['GITHUB_SHA']:
        return None
    return {'head': head, 'control_commit': head}


def prepare_job():
    root = shared.job_root()
    if root.parent.is_symlink():
        raise ValueError('Symlinked job parent rejected')
    for marker in root.parent.glob('direct-*/compiler-group.json'):
        shared.require_stopped_marker(marker)
    root.mkdir(mode=0o700)
    return root, root / 'control', root / 'source'


def compiler_command(control, source, head, control_commit, fingerprint, *, compile_only=False):
    command = ['python3', str(control / '.github/scripts/native_compilation.py'),
               '--source', str(source), '--control', str(control),
               '--work-dir', str(source / '.cache/project-lucid'),
               '--expected-head', head, '--expected-source', fingerprint,
               '--image', shared.EDITOR, '--control-commit', control_commit,
               '--run-id', os.environ['GITHUB_RUN_ID']]
    if compile_only:
        command.append('--compile-only')
    else:
        command.extend(['--input', '/srv/lucid-task5/input/SonicDreamTeam.app'])
    return command


def verify_report(raw, producer, request, control, source):
    require_main_context(request)
    if len(raw) > shared.MAX_REPORT_BYTES:
        raise ValueError('Public report exceeds bound')
    report = json.loads(raw)
    shared.reporting_tools(control)[0](report, source)
    shared.driver.require_approved_original(report, json.loads((control / 'docs/code-progress.json').read_bytes()))
    expected = {'head': request['head'], 'control_commit': request['control_commit'],
                'image': shared.EDITOR, 'run_id': os.environ['GITHUB_RUN_ID'],
                'source_fingerprint': report['source_fingerprint'],
                'report_sha256': hashlib.sha256(raw).hexdigest(),
                'compiler_receipt_sha256': report['compiler_receipt_sha256']}
    if set(producer) != shared.PRODUCER_FIELDS or producer != expected:
        raise ValueError('Report differs from protected compiler step outputs')
    return report


def compile_job(api, request):
    require_main_head(api, request)
    root, control, source = prepare_job()
    shared.checkout(control, request['control_commit'])
    shared.checkout(source, request['head'])
    require_main_head(api, request)
    shared.driver.require_control_reporting_tools(source, control)
    sys.path.insert(0, str(control / 'tools'))
    from lucidlib.verification import artifact_fingerprint
    command = compiler_command(control, source, request['head'], request['control_commit'], artifact_fingerprint(source))
    with (root / 'private-driver-stderr.log').open('wb') as errors:
        producer = json.loads(shared.run_compiler(command, source, errors, root / 'compiler-group.json'))
    raw = (source / 'docs/code-progress.json').read_bytes()
    verify_report(raw, producer, request, control, source)
    require_main_head(api, request)
    (root / 'request.json').write_text(json.dumps(request, sort_keys=True))
    (root / 'code-progress.json').write_bytes(raw)
    shared.write_outputs(producer)


def seal_job(api, request, producer):
    require_main_head(api, request)
    root = shared.job_root()
    control, source = root / 'control', root / 'source'
    if (json.loads((root / 'request.json').read_bytes()) != request
            or shared.git_head(control) != request['control_commit'] or shared.git_head(source) != request['head']):
        raise ValueError('Local source/control/request differs')
    shared.driver.require_control_reporting_tools(source, control)
    raw = (root / 'code-progress.json').read_bytes()
    verify_report(raw, producer, request, control, source)
    shared.write_outputs({'producer_base64': shared.encode(producer),
                          'report_base64': base64.b64encode(raw).decode()})


def pr_gate(api, event):
    if (os.environ.get('GITHUB_REPOSITORY') != REPOSITORY
            or os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run'
            or os.environ.get('GITHUB_RUN_ATTEMPT') != '1'):
        raise ValueError('PR compilation must follow an admitted workflow run')
    run = event.get('workflow_run', {})
    if run.get('event') != 'pull_request' or run.get('conclusion') != 'success':
        return None
    request = shared.gate(api, event, os.environ['GITHUB_SHA'])
    if request and request['head_repository'] == REPOSITORY and request['head_ref'].startswith('lucid/progress-checks-'):
        pr = shared.require_current_pr(api, request)
        if pr.get('user', {}).get('login') == shared.BOT:
            commit = api.request(f"/repos/{REPOSITORY}/commits/{request['head']}")
            paths = {entry.get('filename') for entry in commit.get('files', [])}
            if paths and paths <= {'README.md', 'docs/code-progress.json'}:
                return None
    return request


def pr_compile_job(api, request):
    if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run':
        raise ValueError('PR compilation requires its admitted workflow event')
    shared.require_workflow_context(request)
    shared.require_current_pr(api, request)
    run = shared.require_upstream_run(api, request)
    if run.get('conclusion') != 'success':
        raise ValueError('PR source checks must pass before compilation')
    root, control, source = prepare_job()
    shared.checkout(control, request['control_commit'])
    shared.checkout(source, request['head'])
    shared.require_current_pr(api, request)
    shared.driver.require_control_reporting_tools(source, control)
    sys.path.insert(0, str(control / 'tools'))
    from lucidlib.verification import artifact_fingerprint
    fingerprint = artifact_fingerprint(source)
    command = compiler_command(control, source, request['head'], request['control_commit'], fingerprint, compile_only=True)
    with (root / 'private-driver-stderr.log').open('wb') as errors:
        producer = json.loads(shared.run_compiler(command, source, errors, root / 'compiler-group.json'))
    expected = {'head': request['head'], 'control_commit': request['control_commit'], 'image': shared.EDITOR,
                'run_id': os.environ['GITHUB_RUN_ID'], 'source_fingerprint': fingerprint,
                'compiler_receipt_sha256': producer.get('compiler_receipt_sha256')}
    if (producer != expected or not isinstance(expected['compiler_receipt_sha256'], str)
            or len(expected['compiler_receipt_sha256']) != 64
            or any(char not in '0123456789abcdef' for char in expected['compiler_receipt_sha256'])):
        raise ValueError('PR compilation receipt identity differs')
    shared.require_current_pr(api, request)
    shared.driver.require_control_reporting_tools(source, control)
    if shared.git_head(source) != request['head'] or artifact_fingerprint(source) != fingerprint:
        raise ValueError('PR source changed during compilation')
    print('Exact-head PR compilation verified')


def check_ref(api, branch, expected):
    path = f'/repos/{REPOSITORY}/git/refs/heads/' + urllib.parse.quote(branch, safe='')
    if api.request(path)['object']['sha'] != expected:
        raise ValueError('Generated check ref changed')
    return path


def wait_for_checks(api, request, branch, head, *, sleep=time.sleep, monotonic=time.monotonic, seconds=600):
    deadline = monotonic() + seconds
    while True:
        require_main_head(api, request)
        check_ref(api, branch, head)
        checks = api.request(f'/repos/{REPOSITORY}/commits/{head}/check-runs?per_page=100')['check_runs']
        found = {}
        for check in checks:
            if check.get('name') in CHECKS and check.get('app', {}).get('id') == CHECK_APP and check.get('head_sha') == head:
                # The API is newest first; never let an older success hide a newer failure.
                found.setdefault(check['name'], check)
        if any(check.get('status') == 'completed' and check.get('conclusion') != 'success' for check in found.values()):
            raise ValueError('Generated commit required checks failed')
        if set(found) == CHECKS and all(check.get('status') == 'completed' and check.get('conclusion') == 'success' for check in found.values()):
            return
        if monotonic() >= deadline:
            raise ValueError('Generated commit required checks did not finish; main retained')
        sleep(min(2, max(0, deadline - monotonic())))


def cleanup_check_ref(api, branch, expected, control):
    if not re.fullmatch(r'lucid/progress-checks-[1-9][0-9]*-[0-9a-f]{12}', branch):
        raise ValueError('Ref is not owned by this publisher')
    path = f'/repos/{REPOSITORY}/git/refs/heads/' + urllib.parse.quote(branch, safe='')
    try:
        current = api.request(path)['object']['sha']
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return
        raise
    if current != expected:
        return
    # Git's lease makes deletion atomic; GET followed by API DELETE would lose
    # a concurrent writer between the read and deletion.
    helper = '!f() { printf "username=x-access-token\\npassword=%s\\n" "$GH_TOKEN"; }; f'
    environment = {key: value for key, value in os.environ.items() if key in {'PATH', 'HOME', 'LANG', 'LC_ALL'}}
    environment.update(GH_TOKEN=os.environ['GH_TOKEN'], GIT_TERMINAL_PROMPT='0')
    ref = 'refs/heads/' + branch
    subprocess.run(['git', '-c', 'core.hooksPath=/dev/null', '-c', 'credential.helper=',
                    '-c', 'credential.helper=' + helper, 'push', '--force-with-lease=' + ref + ':' + expected,
                    'https://github.com/' + REPOSITORY + '.git', ':' + ref],
                   cwd=control, env=environment, check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def require_generated_pr(api, number, branch, head, bot_id):
    pr = api.request(f'/repos/{REPOSITORY}/pulls/{number}')
    if (pr.get('user', {}).get('login') != shared.BOT or pr.get('user', {}).get('id') != bot_id
            or pr.get('head', {}).get('sha') != head or pr.get('head', {}).get('ref') != branch
            or pr.get('head', {}).get('repo', {}).get('full_name') != REPOSITORY
            or pr.get('base', {}).get('ref') != MAIN
            or pr.get('base', {}).get('repo', {}).get('full_name') != REPOSITORY):
        raise ValueError('Generated PR ownership changed; preserve it for inspection')
    return pr


def publish(api, request, raw, producer, control, source):
    require_main_head(api, request)
    if shared.git_head(control) != request['control_commit'] or shared.git_head(source) != request['head']:
        raise ValueError('Publisher source/control checkout differs')
    shared.driver.require_control_reporting_tools(source, control)
    report = verify_report(raw, producer, request, control, source)
    _, render, split = shared.reporting_tools(control)
    readme = source / 'README.md'
    if readme.is_symlink() or not readme.is_file() or readme.stat().st_size > 1024 * 1024:
        raise ValueError('Invalid README')
    before, _, after = split(readme.read_text())
    updated = (before + render(report).rstrip('\n') + after).encode()
    updated += b'' if updated.endswith(b'\n') else b'\n'
    payload = (json.dumps(report, indent=2, sort_keys=True) + '\n').encode()
    if readme.read_bytes() == updated and (source / 'docs/code-progress.json').read_bytes() == payload:
        return request['head']
    user = api.request('/users/github-actions%5Bbot%5D')
    if user.get('login') != shared.BOT or user.get('type') != 'Bot' or type(user.get('id')) is not int or user['id'] <= 0:
        raise ValueError('GitHub Actions bot identity differs')
    identity = {'name': shared.BOT, 'email': f"{user['id']}+{shared.BOT}@users.noreply.github.com"}
    commit = api.request(f"/repos/{REPOSITORY}/git/commits/{request['head']}")
    tree = []
    for path, data in (('README.md', updated), ('docs/code-progress.json', payload)):
        require_main_head(api, request)
        blob = api.request(f'/repos/{REPOSITORY}/git/blobs', 'POST', {'content': base64.b64encode(data).decode(), 'encoding': 'base64'})['sha']
        tree.append({'path': path, 'mode': '100644', 'type': 'blob', 'sha': blob})
    require_main_head(api, request)
    tree_sha = api.request(f'/repos/{REPOSITORY}/git/trees', 'POST', {'base_tree': commit['tree']['sha'], 'tree': tree})['sha']
    created = api.request(f'/repos/{REPOSITORY}/git/commits', 'POST', {'message': 'Update verified main code progress',
                          'tree': tree_sha, 'parents': [request['head']], 'author': identity, 'committer': identity})['sha']
    if not isinstance(created, str) or not shared.SHA.fullmatch(created):
        raise ValueError('Generated commit identity differs')
    branch = 'lucid/progress-checks-' + os.environ['GITHUB_RUN_ID'] + '-' + created[:12]
    require_main_head(api, request)
    api.request(f'/repos/{REPOSITORY}/git/refs', 'POST', {'ref': 'refs/heads/' + branch, 'sha': created})
    number = None
    merged = False
    try:
        created_pr = api.request(f'/repos/{REPOSITORY}/pulls', 'POST',
                                {'head': branch, 'base': MAIN, 'title': 'Update main code progress', 'body': ''})
        number = created_pr.get('number')
        if type(number) is not int or number <= 0:
            raise ValueError('Generated PR creation unconfirmed; no automatic retry')
        require_generated_pr(api, number, branch, created, user['id'])
        for workflow in shared.CHECK_WORKFLOWS:
            require_main_head(api, request)
            check_ref(api, branch, created)
            # A 204 response is normal. Observe actual exact-SHA check runs; an
            # uncertain dispatch is never automatically repeated.
            api.request(f'/repos/{REPOSITORY}/actions/workflows/{workflow}/dispatches', 'POST',
                        {'ref': branch, 'inputs': {'expected_head': created}})
        wait_for_checks(api, request, branch, created)
        require_main_head(api, request)
        pr = require_generated_pr(api, number, branch, created, user['id'])
        if pr.get('state') != 'open' or pr.get('base', {}).get('sha') != request['head']:
            raise ValueError('Generated PR no longer targets this measured main head')
        # Required reviews/checks/strict freshness remain GitHub's decision.
        # Never use an admin bypass, force update or automatic rebase retry.
        result = api.request(f'/repos/{REPOSITORY}/pulls/{number}/merge', 'PUT',
                             {'sha': created, 'merge_method': 'rebase'})
        if result.get('merged') is not True or not shared.SHA.fullmatch(result.get('sha', '')):
            raise ValueError('Protected rebase publication unavailable; no permission or rule changes attempted')
        merged = True
        published = result['sha']
        actual_commit = api.request(f'/repos/{REPOSITORY}/git/commits/{published}')
        if (actual_commit.get('tree', {}).get('sha') != tree_sha
                or [parent.get('sha') for parent in actual_commit.get('parents', [])] != [request['head']]
                or actual_commit.get('author', {}).get('name') != shared.BOT
                or actual_commit.get('author', {}).get('email') != identity['email']
                or actual_commit.get('committer', {}).get('name') != shared.BOT
                or actual_commit.get('committer', {}).get('email') != identity['email']):
            raise ValueError('Published tree, parent or bot identity differs; inspect without rewriting history')
        branch_name, actual = shared.default_head(api)
        if branch_name != MAIN or actual != published:
            raise ValueError('Main publication superseded or unconfirmed; no automatic retry')
        # Rebase changes the SHA. Bind the ordinary checks to the actual main
        # commit as well; GITHUB_TOKEN writes do not trigger ordinary push runs.
        for workflow in shared.CHECK_WORKFLOWS:
            check_ref(api, MAIN, published)
            api.request(f'/repos/{REPOSITORY}/actions/workflows/{workflow}/dispatches', 'POST',
                        {'ref': MAIN, 'inputs': {'expected_head': published}})
        return published
    finally:
        # Attempt both cleanups, retaining the publication error if one fails.
        original_error = sys.exc_info()[1]
        cleanup_errors = []
        try:
            if number is not None and not merged:
                pr = require_generated_pr(api, number, branch, created, user['id'])
                if pr.get('state') == 'open':
                    api.request(f'/repos/{REPOSITORY}/pulls/{number}', 'PATCH', {'state': 'closed'})
        except Exception as error:
            cleanup_errors.append(('PR', error))
        try:
            cleanup_check_ref(api, branch, created, control)
        except Exception as error:
            cleanup_errors.append(('ref', error))
        for stage, error in cleanup_errors:
            print('Generated ' + stage + ' cleanup failed (' + type(error).__name__
                  + '); preserve remaining owned resources for inspection', file=sys.stderr)
        if cleanup_errors and original_error is None:
            raise cleanup_errors[0][1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=('gate', 'compile', 'seal', 'cleanup', 'publish', 'pr-gate', 'pr-compile'))
    mode = parser.parse_args().mode
    if mode == 'cleanup':
        shared.cleanup_job()
        return
    api = shared.GitHub()
    if mode in {'gate', 'pr-gate'}:
        event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
        request = gate(api, event) if mode == 'gate' else pr_gate(api, event)
        shared.write_outputs({'run': 'true' if request else 'false', 'head': request['head'] if request else '',
                              'request_base64': shared.encode(request) if request else ''})
        return
    request = json.loads(base64.b64decode(os.environ['REQUEST_BASE64'], validate=True))
    if mode == 'compile':
        compile_job(api, request)
    elif mode == 'pr-compile':
        pr_compile_job(api, request)
    elif mode == 'seal':
        seal_job(api, request, json.loads(os.environ['COMPILE_OUTPUTS']))
    else:
        created = publish(api, request, base64.b64decode(os.environ['REPORT_BASE64'], validate=True),
                          json.loads(base64.b64decode(os.environ['PRODUCER_BASE64'], validate=True)), Path('control'), Path('source'))
        print('Verified merged-main generated commit: ' + created)


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        detail = 'HTTP ' + str(error.code) if isinstance(error, urllib.error.HTTPError) else type(error).__name__
        print('Progress operation failed (' + detail + '); original refs retained or publication requires inspection', file=sys.stderr)
        sys.exit(1)

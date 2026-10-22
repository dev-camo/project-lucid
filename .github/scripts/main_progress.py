"""Compile admitted PRs without original inputs; generate progress from merged main.

Only hosted publication holds the dedicated app token. It may bypass PR/check
requirements, but can only fast-forward a validated generated-only child of main.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import sys
import subprocess
import tempfile
import urllib.error

import direct_progress as shared

REPOSITORY = shared.REPOSITORY
MAIN = 'main'
REQUEST_FIELDS = {'head', 'control_commit'}
GENERATED_PATHS = ('README.md', 'docs/code-progress.json')
PR_COMPILATION_CONTEXT = 'Native compilation (PR head)'
# Separate status so the compilation context above keeps its meaning. Requiring it is a
# repository setting for the owner.
PR_OWNERSHIP_CONTEXT = 'Generated progress ownership (PR)'
README_START = '<!-- LUCID_CODE_PROGRESS:START -->'
README_END = '<!-- LUCID_CODE_PROGRESS:END -->'
PROGRESS_BRANCH_PREFIX = 'lucid/progress-checks-'
# needs.compile.result -> (state, description). Only a completed compile job may be
# reported as success; skipped and cancelled jobs never are. A failed job is a compiler
# failure only when pr_compile_job says so (see PR_COMPILE_OUTCOMES).
PR_COMPILATION_RESULTS = {
    'success': ('success', 'Exact-head PR compilation verified'),
    'failure': ('error', 'Native compilation job failed outside the compiler; see the linked run'),
    'cancelled': ('error', 'Native compilation was cancelled before it finished'),
    'skipped': ('error', 'Native compilation did not run'),
}
# The compile job's classification of its own failure, from a controller-written step
# output (the compiler never receives GITHUB_OUTPUT). Anything else is "outside the compiler".
PR_COMPILE_OUTCOMES = {
    'compile_failed': ('failure', 'Native compilation of this head failed'),
    'superseded_head': ('error', 'PR head advanced or PR closed during compilation; no result for this head'),
    'superseded_control': ('error', 'Main advanced during compilation; push again to re-run for this head'),
}
PR_SUCCESS_SUBSTITUTED = 'Exact-head PR compilation verified; trusted reporting code replaced the PR\'s'
PR_STALE_AT_REPORT = {
    'head': 'Superseded before reporting: PR head advanced or PR closed; this head was not accepted',
    'control': 'Compiled, but main advanced before reporting; push again to re-run for this head',
}


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


def bot_progress_branch(request):
    return request['head_repository'] == REPOSITORY and request['head_ref'].startswith(PROGRESS_BRANCH_PREFIX)


def pr_by_bot(pr):
    return pr.get('user', {}).get('login') == shared.BOT


def pr_gate(api, event):
    if (os.environ.get('GITHUB_REPOSITORY') != REPOSITORY
            or os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run'
            or os.environ.get('GITHUB_RUN_ATTEMPT') != '1'):
        raise ValueError('PR compilation must follow an admitted workflow run')
    run = event.get('workflow_run', {})
    if run.get('event') != 'pull_request' or run.get('conclusion') != 'success':
        return None
    request = shared.gate(api, event, os.environ['GITHUB_SHA'])
    if request and bot_progress_branch(request):
        pr = shared.require_current_pr(api, request)
        if pr_by_bot(pr):
            commit = api.request(f"/repos/{REPOSITORY}/commits/{request['head']}")
            paths = {entry.get('filename') for entry in commit.get('files', [])}
            if paths and paths <= {'README.md', 'docs/code-progress.json'}:
                return None
    return request


def pr_staleness(api, request):
    """None when the PR head and control are current; else 'head' (head advanced, PR
    closed or otherwise not this request's) or 'control' (head still current, main moved)."""
    try:
        shared.require_current_pr(api, request)
        return None
    except ValueError:
        pass
    try:
        shared.require_pr_metadata(api.request(f"/repos/{REPOSITORY}/pulls/{request['pr']}"), request)
    except ValueError:
        return 'head'
    return 'control'


def pr_compile_job(api, request):
    """Compile the PR head; the 'outcome' and 'substituted' step outputs let the report
    job tell a compiler failure from stale state or a precondition failure."""
    outputs = {'outcome': 'precondition', 'substituted': 'false'}
    try:
        pr_compile_steps(api, request, outputs)
    finally:
        shared.write_outputs(outputs)


def pr_compile_steps(api, request, outputs):
    def current():
        stale = pr_staleness(api, request)
        if stale:
            outputs['outcome'] = 'superseded_' + stale
            raise ValueError('PR head or control changed: ' + stale)

    if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run':
        raise ValueError('PR compilation requires its admitted workflow event')
    shared.require_workflow_context(request)
    current()
    run = shared.require_upstream_run(api, request)
    if run.get('conclusion') != 'success':
        raise ValueError('PR source checks must pass before compilation')
    root, control, source = prepare_job()
    shared.checkout(control, request['control_commit'])
    shared.checkout(source, request['head'])
    current()
    # PR reporting code never runs here: compile the PR's game code with the trusted
    # control's reporting code, and say exactly which paths were substituted.
    substituted = shared.driver.overlay_control_reporting_tools(source, control)
    shared.driver.require_control_reporting_tools(source, control)
    sys.path.insert(0, str(control / 'tools'))
    from lucidlib.verification import artifact_fingerprint
    fingerprint = artifact_fingerprint(source)
    command = compiler_command(control, source, request['head'], request['control_commit'], fingerprint, compile_only=True)
    with (root / 'private-driver-stderr.log').open('wb') as errors:
        try:
            output = shared.run_compiler(command, source, errors, root / 'compiler-group.json')
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            outputs['outcome'] = 'compile_failed'
            raise
        producer = json.loads(output)
    expected = {'head': request['head'], 'control_commit': request['control_commit'], 'image': shared.EDITOR,
                'run_id': os.environ['GITHUB_RUN_ID'], 'source_fingerprint': fingerprint,
                'compiler_receipt_sha256': producer.get('compiler_receipt_sha256')}
    if (producer != expected or not isinstance(expected['compiler_receipt_sha256'], str)
            or len(expected['compiler_receipt_sha256']) != 64
            or any(char not in '0123456789abcdef' for char in expected['compiler_receipt_sha256'])):
        raise ValueError('PR compilation receipt identity differs')
    current()
    shared.driver.require_control_reporting_tools(source, control)
    if shared.git_head(source) != request['head'] or artifact_fingerprint(source) != fingerprint:
        raise ValueError('PR source changed during compilation')
    outputs['outcome'] = 'compiled'
    if substituted:
        outputs['substituted'] = 'true'
        print('Exact-head PR game code compiled with trusted control reporting code; '
              'PR reporting changes need review and the post-merge main run: ' + ', '.join(substituted))
    else:
        print('Exact-head PR compilation verified')


def read_blob(api, path, ref):
    """Bytes of a tracked file at an exact commit, through the trusted API (never a PR checkout)."""
    entry = api.request(f"/repos/{REPOSITORY}/contents/{path}?ref={ref}")
    if entry.get('type') != 'file' or entry.get('encoding') != 'base64':
        raise ValueError(path + ' must be a regular file at ' + ref)
    return base64.b64decode(entry['content'])


def readme_generated_section(text, split):
    """The bytes between the README progress markers, markers included.

    A missing marker pair is an error here, unlike split_readme's normalising fallback:
    dropping the markers is how a PR could escape the owned region. Counts are checked
    here; ordering by split_readme.
    """
    if text.count(README_START) != 1 or text.count(README_END) != 1:
        raise ValueError('README requires exactly one generated progress section')
    return split(text)[1]


def ownership_violations(api, request, split):
    """Owned outputs the PR's own changes touch, as a list of names (empty when clean).

    Compares merge-base with head, not main with head, so a branch that predates a bot
    publication is not blamed for carrying older generated bytes.
    """
    head = request['head']
    comparison = api.request(f"/repos/{REPOSITORY}/compare/{request['control_commit']}...{head}?per_page=1")
    base = (comparison.get('merge_base_commit') or {}).get('sha')
    if not isinstance(base, str) or not shared.SHA.fullmatch(base):
        raise ValueError('PR has no merge base with main')
    violations = []
    if read_blob(api, GENERATED_PATHS[1], base) != read_blob(api, GENERATED_PATHS[1], head):
        violations.append(GENERATED_PATHS[1])
    try:
        changed = (readme_generated_section(read_blob(api, 'README.md', base).decode('utf-8'), split)
                   != readme_generated_section(read_blob(api, 'README.md', head).decode('utf-8'), split))
    except ValueError:
        changed = True
    if changed:
        violations.append('README.md generated section')
    return violations


def pr_ownership(api, request, split):
    """Runs from trusted main code: the PR's own workflows and tools cannot weaken it."""
    if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run':
        raise ValueError('PR ownership check requires its admitted workflow event')
    shared.require_workflow_context(request)
    pr = shared.require_current_pr(api, request)
    # Same exact identity as pr_gate's exemption: the publisher's own branch and account.
    if bot_progress_branch(request) and pr_by_bot(pr):
        state, description = 'success', 'Generated-only PR from the progress publisher'
    else:
        violations = ownership_violations(api, request, split)
        if violations:
            state = 'failure'
            description = 'Owned by main Actions; remove changes to: ' + ', '.join(violations)
        else:
            state, description = 'success', 'No change to main-owned generated progress outputs'
    pr_status(api, request, state, description, PR_OWNERSHIP_CONTEXT)
    return state


def pr_status(api, request, state, description, context=PR_COMPILATION_CONTEXT):
    """Publish the compilation status on the validated PR head and nowhere else.

    The workflow runs on main's SHA, so its own checks never show on the PR commit.
    Statuses are per-SHA: a head that advances starts with no result of its own.
    The success description says if trusted reporting code replaced the PR's.
    Recovery: a stuck pending (the report job failed; run attempts above 1 are rejected)
    or an error for a superseded run is repaired by a new push or upstream run. A run the
    gate declines posts nothing, because no validated request exists to name a head.
    """
    shared.validate_request(request)
    api.request(f"/repos/{REPOSITORY}/statuses/{request['head']}", 'POST', {
        'state': state, 'context': context, 'description': description[:140],
        'target_url': f"https://github.com/{REPOSITORY}/actions/runs/{os.environ['GITHUB_RUN_ID']}"})


def pr_status_pending(api, request):
    if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run':
        raise ValueError('PR compilation status requires its admitted workflow event')
    shared.require_workflow_context(request)
    shared.require_current_pr(api, request)
    pr_status(api, request, 'pending', 'Native compilation of this head is queued or running')


def pr_status_final(api, request, compile_result, outcome='', substituted=False):
    if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_run':
        raise ValueError('PR compilation status requires its admitted workflow event')
    shared.require_workflow_context(request)
    state, description = PR_COMPILATION_RESULTS.get(
        compile_result, ('error', 'Native compilation ended without a recognised result'))
    if compile_result == 'failure':
        state, description = PR_COMPILE_OUTCOMES.get(outcome, (state, description))
    elif state == 'success':
        if substituted:
            description = PR_SUCCESS_SUBSTITUTED
        stale = pr_staleness(api, request)
        if stale:
            # Head advanced, PR closed or control moved: never leave a success behind.
            state, description = 'error', PR_STALE_AT_REPORT[stale]
    pr_status(api, request, state, description)


def git(source, *arguments, data=None, index=None):
    # Neither the app token nor any workflow output channel reaches git.
    environment = {key: value for key, value in os.environ.items()
                   if key in {'PATH', 'HOME', 'LANG', 'LC_ALL'}}
    if index is not None:
        environment['GIT_INDEX_FILE'] = str(index)
    return subprocess.check_output(['git', '-c', 'core.hooksPath=/dev/null', *arguments],
                                   cwd=source, env=environment, input=data)


def require_clean_checkout(source, head):
    if git(source, 'rev-parse', 'HEAD').decode().strip() != head:
        raise ValueError('Checkout identity differs')
    if git(source, 'status', '--porcelain', '--untracked-files=all'):
        raise ValueError('Publisher checkout contains unexpected path/content changes')
    for path in GENERATED_PATHS:
        entry = git(source, 'ls-tree', head, '--', path).decode().strip()
        if not entry.startswith('100644 blob ') or entry.split('\t')[-1] != path:
            raise ValueError('Generated outputs must be tracked regular files')
        target = source / path
        if any(part.is_symlink() for part in (target, *target.parents)) or not target.is_file():
            raise ValueError('Generated output is missing or symlinked')


def generated_payload(api, request, raw, producer, control, source):
    require_main_head(api, request)
    if shared.git_head(control) != request['control_commit'] or shared.git_head(source) != request['head']:
        raise ValueError('Publisher source/control checkout differs')
    require_clean_checkout(control, request['control_commit'])
    require_clean_checkout(source, request['head'])
    shared.driver.require_control_reporting_tools(source, control)
    report = verify_report(raw, producer, request, control, source)
    _, render, split = shared.reporting_tools(control)
    original = (source / 'README.md').read_bytes()
    if len(original) > 1024 * 1024:
        raise ValueError('Invalid README')
    # split_readme's missing-marker fallback normalizes human text. Never use
    # that fallback with a branch bypass; the generated section must exist.
    text = original.decode('utf-8')
    if text.count('<!-- LUCID_CODE_PROGRESS:START -->') != 1 or text.count('<!-- LUCID_CODE_PROGRESS:END -->') != 1:
        raise ValueError('README requires exactly one generated section')
    before, _, after = split(text)
    updated = (before + render(report).rstrip('\n') + after).encode('utf-8')
    # Preserve human prefix/suffix, including a missing final newline, exactly.
    new_before, _, new_after = split(updated.decode('utf-8'))
    if (before, after) != (new_before, new_after):
        raise ValueError('Generated README changed human content')
    payload = (json.dumps(report, indent=2, sort_keys=True) + '\n').encode()
    # Final check over the composed bytes, equivalent to check_progress: the snapshot is
    # the validated report and the README section is exactly its rendering.
    if json.loads(payload) != report or split(updated.decode('utf-8'))[1] + '\n' != render(report):
        raise ValueError('Composed progress outputs differ from the validated report')
    return {'README.md': updated, 'docs/code-progress.json': payload}


def expected_tree(source, head, payload):
    if set(payload) != set(GENERATED_PATHS):
        raise ValueError('Unexpected generated path')
    # Build the expected Git tree using a separate index and explicit paths.
    # This cannot absorb staged, untracked, human or mode changes.
    with tempfile.TemporaryDirectory(prefix='lucid-progress-index-') as directory:
        index = Path(directory) / 'index'
        git(source, 'read-tree', head, index=index)
        for path in GENERATED_PATHS:
            blob = git(source, 'hash-object', '-w', '--stdin', data=payload[path]).decode().strip()
            git(source, 'update-index', '--cacheinfo', '100644,' + blob + ',' + path, index=index)
        tree = git(source, 'write-tree', index=index).decode().strip()
        changed = git(source, 'diff-tree', '--no-commit-id', '--name-only', '-r', '-z', head, tree).split(b'\0')
        if not {path.decode() for path in changed if path} <= set(GENERATED_PATHS):
            raise ValueError('Generated tree changed an unexpected path')
        return tree


def require_created_commit(api, created, head, tree, identity):
    commit = api.request(f'/repos/{REPOSITORY}/git/commits/{created}')
    if (commit.get('tree', {}).get('sha') != tree
            or [parent.get('sha') for parent in commit.get('parents', [])] != [head]
            or any(commit.get(field, {}).get('name') != identity['name']
                   or commit.get(field, {}).get('email') != identity['email']
                   for field in ('author', 'committer'))):
        raise ValueError('Generated commit tree, parent or bot identity differs')


def publish(api, request, raw, producer, control, source):
    payload = generated_payload(api, request, raw, producer, control, source)
    if all((source / path).read_bytes() == data for path, data in payload.items()):
        return request['head']
    expected = expected_tree(source, request['head'], payload)
    user = api.request('/users/github-actions%5Bbot%5D')
    if user.get('login') != shared.BOT or user.get('type') != 'Bot' or type(user.get('id')) is not int or user['id'] <= 0:
        raise ValueError('GitHub Actions bot identity differs')
    identity = {'name': shared.BOT, 'email': f"{user['id']}+{shared.BOT}@users.noreply.github.com"}
    commit = api.request(f"/repos/{REPOSITORY}/git/commits/{request['head']}")
    if commit.get('tree', {}).get('sha') != git(source, 'rev-parse', request['head'] + '^{tree}').decode().strip():
        raise ValueError('Main base tree differs from clean checkout')
    tree = []
    for path in GENERATED_PATHS:
        require_main_head(api, request)
        blob = api.request(f'/repos/{REPOSITORY}/git/blobs', 'POST',
                           {'content': base64.b64encode(payload[path]).decode(), 'encoding': 'base64'})['sha']
        tree.append({'path': path, 'mode': '100644', 'type': 'blob', 'sha': blob})
    require_main_head(api, request)
    tree_sha = api.request(f'/repos/{REPOSITORY}/git/trees', 'POST', {'base_tree': commit['tree']['sha'], 'tree': tree})['sha']
    if tree_sha != expected:
        raise ValueError('Generated tree differs from the two validated outputs')
    created = api.request(f'/repos/{REPOSITORY}/git/commits', 'POST', {'message': 'Update verified main code progress',
                          'tree': tree_sha, 'parents': [request['head']], 'author': identity, 'committer': identity})['sha']
    if not isinstance(created, str) or not shared.SHA.fullmatch(created):
        raise ValueError('Generated commit identity differs')
    require_created_commit(api, created, request['head'], expected, identity)
    # Revalidate sealed bytes and clean checkout immediately before publication.
    # A newer main push gets its own measurement; never rebase a stale receipt.
    if generated_payload(api, request, raw, producer, control, source) != payload:
        raise ValueError('Generated payload changed before publication')
    require_main_head(api, request)
    # GitHub atomically rejects a concurrent divergent main update. No force,
    # lease override, retry, temporary ref, PR or workflow dispatch is needed.
    result = api.request(f'/repos/{REPOSITORY}/git/refs/heads/main', 'PATCH', {'sha': created, 'force': False})
    if result.get('object', {}).get('sha') != created:
        raise ValueError('Main update unconfirmed; inspect without retrying')
    branch, actual = shared.default_head(api)
    if branch != MAIN or actual != created:
        raise ValueError('Main publication superseded or unconfirmed; inspect without retrying')
    print('Generated-only main fast-forward verified; ordinary push tests will run')
    return created


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=('gate', 'compile', 'seal', 'cleanup', 'publish', 'validate', 'pr-gate', 'pr-compile', 'pr-status-pending', 'pr-status-final', 'pr-ownership'))
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
    elif mode == 'pr-ownership':
        pr_ownership(api, request, shared.reporting_tools(Path('control'))[2])
    elif mode == 'pr-status-pending':
        pr_status_pending(api, request)
    elif mode == 'pr-status-final':
        pr_status_final(api, request, os.environ['COMPILE_RESULT'], os.environ.get('COMPILE_OUTCOME', ''),
                        os.environ.get('COMPILE_SUBSTITUTED') == 'true')
    elif mode == 'seal':
        seal_job(api, request, json.loads(os.environ['COMPILE_OUTPUTS']))
    else:
        operation = generated_payload if mode == 'validate' else publish
        created = operation(api, request, base64.b64decode(os.environ['REPORT_BASE64'], validate=True),
                          json.loads(base64.b64decode(os.environ['PRODUCER_BASE64'], validate=True)), Path('control'), Path('source'))
        if mode == 'publish':
            print('Verified merged-main generated commit: ' + created)
        else:
            print('Main report, receipt, freshness and generated-only payload validated')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        detail = 'HTTP ' + str(error.code) if isinstance(error, urllib.error.HTTPError) else type(error).__name__
        print('Progress operation failed (' + detail + '); original refs retained or publication requires inspection', file=sys.stderr)
        sys.exit(1)

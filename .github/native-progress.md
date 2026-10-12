# Direct native progress

`native-progress.yml` runs directly in this repository. Its hosted gate admits only
an open, ready, current owner-authored same-repository PR. Its native compile/seal
job selects `[self-hosted, Linux, X64, lucid-native]`. A separate Ubuntu24 publisher
updates only README's generated progress section and `docs/code-progress.json`,
then explicitly dispatches Code progress and Tool tests on the resulting branch
with an exact-head guard. All seven required check names and protection stay intact.
No GitHub App, extra token, cross-repository dispatch, package or input artifact is
required. There are no project-specific Actions secrets or variables to configure.

The runner is `lucid-builder-native`, registered directly to this public repository
as non-sudo `lucid-runner`. The user's approved fork workflow policy is now approval
for all outside collaborators. A repository runner can accept any matching workflow;
that policy must be preserved and approvals must consider the readable persistent
runner account's Unity licensing state, runner credentials and private game input.
Our source gate does not sandbox other workflows. Contributor compilation and fork
publication are still disabled in this controller pending separate qualification.

The native job gets only Contents read and Pull requests read. Unity2022.3.54f1,
SDK10.0.401, runtime6.0.36 and the isolated compatible libraries are installed on
the VM. The driver checks the installed Editor hash, current source fingerprint,
trusted reporting tools, approved original input hashes and genuine sealed DLL/PDB
receipts. Logs and inputs stay only on private VM disk. Protected compiler step
outputs authenticate the exact sanitized report; sealing checks those fields again.
Only that bounded report and its producer fields pass as job outputs to publication.
The public publisher receives Contents write, Pull requests read and Actions write;
that token never goes to the VM or PR code. It imports only default-branch tools.

The publisher checks source/control freshness and the current PR/ref before a
non-force update. Both author and committer are the verified github-actions Bot,
with its numeric-ID GitHub noreply address, one parent and no co-author trailer.
The check workflows support `workflow_dispatch`: these events trigger with
GITHUB_TOKEN even though its pushes do not automatically trigger push workflows.
Explicit checks validate GITHUB_SHA against the requested commit before checkout.
Their names remain the existing seven contexts, with legacy platform aliases
running on Ubuntu. No Windows/macOS runtime or gameplay proof is implied.

Native refresh observes Code progress completion on PR heads, including failed
freshness checks. Push and dispatched-check completions are ignored, preventing
loops. A fresh, output-only github-actions commit skips recompilation. If check
dispatch failed after a successful output push, manually dispatch native refresh
from main with the current PR number and bot head; it verifies the output-only
commit and requests the checks without another compilation. An uncertain dispatch
is never automatically retried. A queued run whose head or control has changed
fails closed. Repository-wide concurrency serializes native compilation.

The driver is capped at38minutes within a40minute compile job. Cleanup is eligible
on success/failure/cancellation, refuses symlinked roots, and removes only that
run's source/control caches. Hard termination can prevent cleanup: after confirming
the exact run is terminal and no compiler is running, remove only its matching
checkouts under `/home/lucid-runner/lucid-jobs/direct-<run>-1`. Preserve inputs,
licensing state, unrelated runs and small private diagnostics. No broad prune.

This remains a local candidate until reviewed and published. Real Actions PR update,
all seven checks, bot recovery/skip and stale-head acceptance are not yet qualified.
The local report now comes from genuine Linux compilation and measurement, validated
against sealed DLL/PDB evidence and the previously approved Linux input hashes.
Never substitute its receipt for a new source/tool fingerprint. Keep manual totals
until the complete actual automation is verified. Test locally with:

    python3 -m unittest discover -s .github/scripts -p 'test_*.py'

Official trigger behavior:
https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow

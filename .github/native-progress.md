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
as non-sudo `lucid-runner`. The user's approved fork workflow policy is
**Require approval for first-time contributors**. Preserve that exact setting.
A repository runner can accept any matching workflow;
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
loops. Authenticated workflow events initiated by github-actions are ignored;
that is an event filter, not approval of their report or receipt. Every admitted request compiles; bot labels and a self-consistent report do
not establish genuine compiler authority and cannot skip compilation. If check
dispatch failed after a successful output push, manually dispatch native refresh
from main with the current PR number and head; it performs another genuine build.
An uncertain dispatch is never automatically retried. A queued run whose head or control has changed
fails closed. Repository-wide concurrency serializes native compilation.

The driver is capped at38minutes within a40minute compile job. Cleanup is eligible
on success/failure/cancellation, refuses symlinked roots, and removes only that
run's source/control caches. Hard termination can prevent cleanup: after confirming
the exact run is terminal and no compiler is running, remove only its matching
checkouts under `/home/lucid-runner/lucid-jobs/direct-<run>-1`. Preserve inputs,
licensing state, unrelated runs and small private diagnostics. No broad prune.

The workflow is deployed. PR31 exercised genuine native compilation, sealed report
publication and a generated `github-actions[bot]` commit, followed by all seven
protected checks on the exact published head. Automatic PR checks on that bot commit
required normal run approval under the preserved contributor policy; after approval,
both workflows passed. Explicit dispatched checks also passed. This qualifies the
owner PR compilation/publication/check path. Stale-head rejection and bot-event
filtering have regression coverage; concurrent-head recovery and contributor
compilation/publication are not qualified by this acceptance run.

The published report uses the approved stripped CDN input for Sonic Dream Team
1.10.1, build 1022527550. Its universal Mach-O contains x86_64 and arm64; measurement
selects x86_64. The complete binary SHA-256 is
`e94d984c0bc726a3a66ed0b39785d5166f92aa4356b3dd742fce098d42521987`,
the selected slice is
`8c80e6a4a3d75419548b288fac14680858a2b7ccd6fc55fb7c9fc38198de39c3`,
and metadata version31 has SHA-256
`88af04a6e548dae5e00c13dd52add4e48f9917a31db784d3b21274b137e49837`.
Keep separate Mac original-input provenance; its different binary/metadata bytes
must not be relabeled as this input. For reviewed contributor source, use an
owner-authored same-repository integration PR and regenerate against the existing
canonical runner input. Dispatch from main with the current PR and exact head,
then verify the generated head and all seven checks before merging. Never substitute
a receipt for a new source/tool fingerprint. Generated totals come from that fresh
report; gameplay and original-code byte parity remain unqualified.

Test locally with:

    python3 -m unittest discover -s .github/scripts -p 'test_*.py'

Official trigger behavior:
https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow

# Automated checks

Pushes and pull requests, including forks, run the input-free tooling and content identity checks on Ubuntu 24.04. Existing macOS and Windows matrix names are compatibility aliases preserving the seven required check contexts; they do not represent execution on those operating systems. Tool tests use Python 3.13 and .NET SDK 10.0.401; Code progress retains its minimum supported Python 3.9 freshness check.

The Python suite uses synthetic files and controlled process fixtures. Its 21 opt-in native recovery checks require a supplied game bundle and pinned recovery tools and remain visibly skipped. Hosted checks do not run Unity compilation, EditMode or PlayMode tests, desktop game builds, or game fidelity checks. Full Windows Python testing remains pending because extraction-host orchestration currently requires POSIX file locks.

Owner-authored same-repository PRs also trigger `Refresh native progress`. Its separate Ubuntu native runner performs genuine Unity script compilation and sealed progress measurement, then a hosted publisher updates generated progress and requests the seven required checks on the resulting head. Game inputs and full compiler logs remain on the runner. This proves compiled code coverage; it does not establish gameplay or native-byte parity. See [native-progress.md](native-progress.md) for the execution boundary and recovery procedure.

Run the input-free checks locally:

```sh
python3 -m unittest discover -s tools/tests -v
python3 tools/lucid.py progress --check
cd tools/tests/ArtifactIdentity
dotnet run --project ArtifactIdentity.csproj --configuration Release
```

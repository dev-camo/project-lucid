# Automated checks

Pushes and pull requests, including forks, run the Python tooling suite on Ubuntu and macOS with Python 3.13. The existing Code progress check retains Ubuntu and the minimum supported Python 3.9. Hosted C# checks run the maintained content identity implementation against fixed shared vectors on Ubuntu, Windows, and macOS ARM64 and Intel with .NET SDK 10.0.401.

The Python suite uses synthetic files and controlled process fixtures. Its 21 opt-in native recovery checks require a supplied game bundle and pinned recovery tools and remain visibly skipped. Hosted checks do not run Unity compilation, EditMode or PlayMode tests, desktop game builds, or game fidelity checks. Full Windows Python testing remains pending because extraction-host orchestration currently requires POSIX file locks.

Run the input-free checks locally:

```sh
python3 -m unittest discover -s tools/tests -v
python3 tools/lucid.py progress --check
cd tools/tests/ArtifactIdentity
dotnet run --project ArtifactIdentity.csproj --configuration Release
```

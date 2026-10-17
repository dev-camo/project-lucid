"""AssetRipper runs from a per-run copy so its runtime output never enters the sealed install (#301).

Inert fixtures only: the pinned archive is a synthetic tar and the launch is a stand-in
that writes what AssetRipper 2.0.0 leaves beside its executable (temp/<4 hex>/<file>).
No supplied executable, game binary or network service is ever run.
"""

from contextlib import ExitStack
import hashlib
import io
import json
import os
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest import mock

from tools.lucidlib import assets, bootstrap


ENTRYPOINT = "AssetRipper.GUI.Free"


class AssetRipperRuntimeCopyTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.work = self.root / "cache"
        self.work.mkdir()
        stack = ExitStack()
        self.addCleanup(stack.close)
        self.addCleanup(self.forget_seals)
        stack.enter_context(mock.patch.object(bootstrap, "CACHE_ROOT", self.work))
        stack.enter_context(mock.patch.object(bootstrap, "host_name", return_value="fixture-host"))
        self.lock = {"schema_version": 1, "platform": "fixture-host", "rid": "fixture-rid", "artifacts": {}}
        stack.enter_context(mock.patch.object(bootstrap, "load_lock", return_value=self.lock))
        for module in (bootstrap, assets):
            stack.enter_context(mock.patch.object(module.subprocess, "Popen",
                                                  side_effect=AssertionError("No fixture executable may run")))
        stack.enter_context(mock.patch("urllib.request.urlopen", side_effect=AssertionError("Offline fixtures only")))
        stack.enter_context(mock.patch.object(assets, "_stop_ripper"))
        self.app = self.declared_app(stack)
        self.archive()
        self.launches = []

    def forget_seals(self):
        for path in list(bootstrap._INSTALL_SEALS):
            if self.root in path.parents:
                del bootstrap._INSTALL_SEALS[path]

    def declared_app(self, stack):
        app = self.root / "Game.app"
        critical = {assets.inputvariants.GAME_ASSEMBLY: b"synthetic code",
                    assets.inputvariants.METADATA: b"synthetic metadata",
                    assets.inputvariants.INFO_PLIST: b"synthetic plist",
                    "Contents/Resources/Data/globalgamemanagers": b"synthetic Unity data"}
        for relative, content in critical.items():
            (app / relative).parent.mkdir(parents=True, exist_ok=True)
            (app / relative).write_bytes(content)
        digest = {relative: hashlib.sha256(content).hexdigest() for relative, content in critical.items()}
        # Admit the synthetic bundle as its own declared variant for these tests only.
        declared = {"binary_sha256": digest[assets.inputvariants.GAME_ASSEMBLY],
                    "metadata_sha256": digest[assets.inputvariants.METADATA],
                    "info_plist_sha256": digest[assets.inputvariants.INFO_PLIST], "x86_64_slice_sha256": None,
                    "qualified": frozenset((assets.inputvariants.ASSET_PREPARATION,))}
        stack.enter_context(mock.patch.dict(assets.inputvariants.VARIANTS, {"synthetic": declared}))
        return app

    def archive(self):
        payload = io.BytesIO()
        with tarfile.open(fileobj=payload, mode="w") as archive:
            for filename, data, mode in ((ENTRYPOINT, b"inert pinned executable", 0o755),
                                         ("runtimes/dependency.dll", b"inert pinned dependency", 0o644)):
                member = tarfile.TarInfo(filename)
                member.size = len(data)
                member.mode = mode
                archive.addfile(member, io.BytesIO(data))
        downloads = self.work / "downloads"
        downloads.mkdir()
        for name, filename, data, extra in (
                ("assetripper", "assetripper.tar", payload.getvalue(), {"format": "tar", "entrypoint": ENTRYPOINT}),
                ("assetripper-license", "LICENSE.md", b"inert license", {})):
            (downloads / filename).write_bytes(data)
            self.lock["artifacts"][name] = {"version": "fixture-pin", "filename": filename,
                                            "url": "https://example.invalid/unused",
                                            "sha256": hashlib.sha256(data).hexdigest(), **extra}

    @property
    def install(self):
        return self.work / "tools/assetripper"

    def runtime_copies(self):
        return sorted((self.work / "tools").glob(".run-assetripper-*"))

    def launch(self, executable, run):
        """Stand in for AssetRipper: it writes temp/<hex>/<guid> beside its executable."""
        self.launches.append(Path(executable))
        self.assertTrue(Path(executable).is_file())
        self.assertNotIn(self.install, Path(executable).parents)
        output = Path(executable).parent / "temp" / "40d0"
        output.mkdir(parents=True, mode=0o700)
        (output / ("e" * 32)).write_bytes(b"runtime output")
        return mock.Mock(), "http://127.0.0.1:1", mock.Mock()

    def extract(self, request):
        with mock.patch.object(assets, "_start_ripper", side_effect=self.launch), \
             mock.patch.object(assets, "_request", side_effect=request), \
             mock.patch.object(assets, "finalize_asset_export", return_value={"status": "exported"}):
            return assets.extract_assets(self.app, self.work)

    def reports(self):
        return [json.loads(path.read_text()) for path in sorted((self.work / "assets/runs").glob("*/report.json"))]

    def test_runtime_output_does_not_poison_the_next_selection_after_failure_or_success(self):
        pinned = bootstrap.verified_tool_path(self.work, "assetripper")
        seal = bootstrap.tree_seal(self.install)
        with self.assertRaisesRegex(RuntimeError, "synthetic export failure"):
            self.extract(RuntimeError("synthetic export failure"))
        self.assertEqual(seal, bootstrap.tree_seal(self.install))
        self.assertEqual({"status": "exported"}, self.extract(lambda *args, **kwargs: b""))
        self.assertEqual(2, len(self.launches))
        self.assertNotEqual(self.launches[0].parent, self.launches[1].parent)
        for launched in self.launches:
            self.assertEqual(ENTRYPOINT, launched.name)
            self.assertFalse(launched.parent.exists())
        self.assertEqual([], self.runtime_copies())
        self.assertFalse((self.install / "temp").exists())
        self.assertEqual(seal, bootstrap.tree_seal(self.install))
        # finalize_asset_export is stubbed, so the successful run's report stays "running".
        self.assertEqual(["failed", "running"], sorted(report["status"] for report in self.reports()))
        # A new process revalidates the pinned install against the archive, not the in-process seal.
        bootstrap._INSTALL_SEALS.pop(self.install)
        self.assertEqual(pinned, bootstrap.verified_tool_path(self.work, "assetripper"))
        # The executable identity recorded for the run is the pinned executable's.
        self.assertEqual(hashlib.sha256(b"inert pinned executable").hexdigest(),
                         self.reports()[0]["tool"]["executable_sha256"])

    def discard_install(self):
        bootstrap._INSTALL_SEALS.pop(self.install, None)
        if self.install.exists():
            assets.shutil.rmtree(self.install)

    def test_poisoned_install_is_rejected_before_any_copy_or_launch(self):
        link = "link or special file"
        mutations = {
            "byte": (lambda root: (root / "runtimes/dependency.dll").write_bytes(b"substituted dependency"), None),
            "mode": (lambda root: (root / "runtimes/dependency.dll").chmod(0o755), None),
            "link": (lambda root: ((root / "runtimes/dependency.dll").unlink(),
                                   (root / "runtimes/dependency.dll").symlink_to(root / ENTRYPOINT)), link),
            "extra file": (lambda root: (root / "extra-plugin.dll").write_bytes(b"extra plugin"), None),
            "runtime output": (lambda root: (root / "temp/1bdc").mkdir(parents=True, mode=0o700), None),
        }
        for branch in ("process seal", "archive revalidation"):
            for name, (mutate, message) in mutations.items():
                with self.subTest(branch=branch, mutation=name):
                    bootstrap.verified_tool_path(self.work, "assetripper")
                    try:
                        mutate(self.install)
                        if branch == "archive revalidation":
                            bootstrap._INSTALL_SEALS.pop(self.install)
                        expected = message or ("closure changed" if branch == "process seal"
                                               else "differs from the pinned archive")
                        with self.assertRaisesRegex(bootstrap.ToolError, expected):
                            self.extract(AssertionError("must not reach the export"))
                        self.assertEqual([], self.launches)
                        self.assertEqual([], self.runtime_copies())
                        self.assertFalse((self.work / "assets/runs").exists())
                    finally:
                        self.discard_install()

    def test_install_changed_after_verification_never_reaches_the_launched_copy(self):
        verified = bootstrap.verified_tool_path
        differs = "runtime copy differs from the pinned verified closure"
        mutations = {
            "byte": (lambda root: (root / "runtimes/dependency.dll").write_bytes(b"substituted dependency"), differs),
            "mode": (lambda root: (root / "runtimes/dependency.dll").chmod(0o755), differs),
            "extra file": (lambda root: (root / "extra-plugin.dll").write_bytes(b"extra plugin"), differs),
            "link": (lambda root: (root / "linked.dll").symlink_to(root / ENTRYPOINT), "link or special file"),
        }
        for name, (mutate, message) in mutations.items():
            with self.subTest(mutation=name):
                def verify_then_mutate(work, tool):
                    selected = verified(work, tool)
                    mutate(self.install)
                    return selected
                verified(self.work, "assetripper")
                try:
                    with mock.patch.object(assets, "verified_tool_path", side_effect=verify_then_mutate):
                        with self.assertRaisesRegex(bootstrap.ToolError, message):
                            self.extract(AssertionError("must not reach the export"))
                    self.assertEqual([], self.launches)
                    self.assertEqual([], self.runtime_copies())
                finally:
                    self.discard_install()
        self.assertEqual(["failed"] * len(mutations), [report["status"] for report in self.reports()])

    def test_runtime_copy_is_kept_and_recorded_only_when_removal_fails(self):
        bootstrap.verified_tool_path(self.work, "assetripper")
        with mock.patch.object(assets.shutil, "rmtree", side_effect=OSError("synthetic removal failure")):
            result = self.extract(lambda *args, **kwargs: b"")
        [copy] = self.runtime_copies()
        self.assertEqual({"path": str(copy), "error": "synthetic removal failure"}, result["retained_runtime_copy"])
        self.assertEqual(result["retained_runtime_copy"], self.reports()[0]["retained_runtime_copy"])
        self.assertTrue((copy / "temp/40d0").is_dir())
        self.assertFalse((self.install / "temp").exists())


if __name__ == "__main__":
    unittest.main()

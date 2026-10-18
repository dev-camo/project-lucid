"""AssetRipper runs from a per-run copy so its runtime output never enters the sealed install (#301).

Inert fixtures only: the pinned archive is a synthetic tar and the launch is a stand-in.
The stand-in creates temp/<hex> directories beside its executable, as issue #301 observed
AssetRipper 2.0.0 doing on Linux (macOS and Windows base-directory behaviour is unverified),
plus one file so cleanup is exercised on a non-empty tree.
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
        """Stand in for AssetRipper: temp/<hex> beside its executable, plus a fixture file."""
        self.launches.append(Path(executable))
        self.assertTrue(Path(executable).is_file())
        output = Path(executable).parent / "temp" / "40d0"
        output.mkdir(parents=True, mode=0o700)
        (output / ("e" * 32)).write_bytes(b"runtime output")
        return mock.Mock(), "http://127.0.0.1:1", mock.Mock()

    def extract(self, request, finalize=None):
        def default_finalize(run, work, before_publish=None):
            if before_publish is not None:
                before_publish()
            return {"status": "exported"}
        finalize = finalize or default_finalize
        with mock.patch.object(assets, "_start_ripper", side_effect=self.launch), \
             mock.patch.object(assets, "_request", side_effect=request), \
             mock.patch.object(assets, "finalize_asset_export", side_effect=finalize):
            return assets.extract_assets(self.app, self.work)

    def failing_runtime_removal(self):
        """Fail shutil.rmtree only for runtime copies; every other removal stays real."""
        remove = assets.shutil.rmtree

        def rmtree(path, *args, **kwargs):
            if Path(path).name.startswith(".run-assetripper-"):
                raise OSError("synthetic removal failure")
            return remove(path, *args, **kwargs)
        return mock.patch.object(assets.shutil, "rmtree", side_effect=rmtree)

    def reports(self):
        return [json.loads(path.read_text()) for path in sorted((self.work / "assets/runs").glob("*/report.json"))]

    def test_runtime_output_does_not_poison_the_next_selection_after_failure_or_success(self):
        pinned = bootstrap.verified_tool_path(self.work, "assetripper")
        seal = bootstrap.tree_seal(self.install)
        with self.assertRaisesRegex(RuntimeError, "synthetic export failure"):
            self.extract(RuntimeError("synthetic export failure"))
        # The symptom in #301: the next selection rejected the closure after real runtime output.
        self.assertEqual({"status": "exported"}, self.extract(lambda *args, **kwargs: b""))
        self.assertEqual(seal, bootstrap.tree_seal(self.install))
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

    def test_the_launched_executable_is_outside_the_pinned_install(self):
        bootstrap.verified_tool_path(self.work, "assetripper")
        self.extract(lambda *args, **kwargs: b"")
        [launched] = self.launches
        self.assertNotIn(self.install, launched.parents)
        self.assertEqual(self.work / "tools", launched.parent.parent)
        self.assertTrue(launched.parent.name.startswith(".run-assetripper-"))

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
        with self.subTest(path="success"):
            with self.failing_runtime_removal():
                result = self.extract(lambda *args, **kwargs: b"")
            [copy] = self.runtime_copies()
            self.assertEqual({"path": str(copy), "error": "synthetic removal failure"},
                             result["retained_runtime_copy"])
            [report] = self.reports()
            self.assertEqual(result["retained_runtime_copy"], report["retained_runtime_copy"])
            self.assertTrue((copy / "temp/40d0").is_dir())
        with self.subTest(path="failure"):
            with self.failing_runtime_removal(), \
                 self.assertRaisesRegex(RuntimeError, "synthetic export failure"):
                self.extract(RuntimeError("synthetic export failure"))
            retained = [path for path in self.runtime_copies() if path != copy]
            self.assertEqual(1, len(retained))
            [report] = [report for report in self.reports() if report["status"] == "failed"]
            self.assertEqual({"path": str(retained[0]), "error": "synthetic removal failure"},
                             report["retained_runtime_copy"])
            self.assertEqual("synthetic export failure", report["error"])
            # The earlier copy left behind is reported, never removed, by the next run.
            self.assertEqual([str(copy)], report["stale_runtime_copies"])
        self.assertFalse((self.install / "temp").exists())

    def test_runtime_copy_is_removed_when_stopping_the_tool_fails(self):
        bootstrap.verified_tool_path(self.work, "assetripper")
        with mock.patch.object(assets, "_stop_ripper", side_effect=RuntimeError("synthetic stop failure")), \
             self.assertRaisesRegex(RuntimeError, "synthetic stop failure"):
            self.extract(RuntimeError("synthetic export failure"))
        self.assertEqual([], self.runtime_copies())
        self.assertEqual(bootstrap.tree_seal(self.install), bootstrap.verified_install_seal(self.install))

    def test_report_failure_while_recording_a_kept_copy_does_not_mask_the_export_error(self):
        bootstrap.verified_tool_path(self.work, "assetripper")
        write = assets._json_write

        def json_write(path, value):
            if "retained_runtime_copy" in value:
                raise ValueError("synthetic report failure")
            return write(path, value)
        with self.failing_runtime_removal(), mock.patch.object(assets, "_json_write", side_effect=json_write), \
             mock.patch.object(assets.sys, "stderr", new_callable=io.StringIO) as stderr, \
             self.assertRaisesRegex(RuntimeError, "synthetic export failure"):
            self.extract(RuntimeError("synthetic export failure"))
        [copy] = self.runtime_copies()
        self.assertIn(str(copy), stderr.getvalue())
        self.assertIn("synthetic report failure", stderr.getvalue())
        [report] = self.reports()
        self.assertEqual("failed", report["status"])
        self.assertNotIn("retained_runtime_copy", report)

    def test_install_changed_during_the_run_fails_the_run_before_publication(self):
        bootstrap.verified_tool_path(self.work, "assetripper")
        finalized = []

        def change_install(*args, **kwargs):
            if args[1] == "/Export/UnityProject":
                (self.install / "extra-plugin.dll").write_bytes(b"extra plugin")
            return b""
        try:
            with self.assertRaisesRegex(bootstrap.ToolError, "closure changed"):
                self.extract(change_install, finalize=lambda run, work, before_publish=None: (before_publish and before_publish(), finalized.append(run)))
            self.assertEqual([], finalized)
            [report] = self.reports()
            self.assertEqual("failed", report["status"])
            self.assertFalse((self.work / "assets/latest-assets.json").exists())
            self.assertEqual([], self.runtime_copies())
        finally:
            self.discard_install()

if __name__ == "__main__":
    unittest.main()

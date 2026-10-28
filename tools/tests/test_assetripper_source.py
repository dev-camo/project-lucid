"""Inert fixtures for the pinned AssetRipper source build; no executable is ever launched."""

from contextlib import ExitStack
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock
import urllib.error

from tools.lucidlib import bootstrap
from tools.tests import test_tool_cache_trust

PROJECT = "Source/AssetRipper.GUI.Free/AssetRipper.GUI.Free.csproj"
SOURCE = {PROJECT: b"verified project", "Source/Other/Other.csproj": b"verified dependency\n",
          "nuget.config": b"upstream feed", "LICENSE.md": b"inert license"}
LOCK = b'{"version": 2}'
# An add-only patch: one line appended to a pinned file and one new file.
PATCH = (b"--- a/Source/Other/Other.csproj\n+++ b/Source/Other/Other.csproj\n@@ -1 +1,2 @@\n verified dependency\n+patched line\n"
         b"--- /dev/null\n+++ b/Source/Other/Added.cs\n@@ -0,0 +1 @@\n+added file\n")
PATCHED = {"Source/Other/Other.csproj": b"verified dependency\npatched line\n", "Source/Other/Added.cs": b"added file\n"}


class AssetRipperSourceTests(unittest.TestCase):
    archive = test_tool_cache_trust.ToolCacheTrustTests.archive
    release_leases = test_tool_cache_trust.ToolCacheTrustTests.release_leases

    def setUp(self):
        bootstrap.CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.directory = Path(tempfile.mkdtemp(prefix="test-assetripper-source-", dir=bootstrap.CACHE_ROOT))
        self.work = self.directory / "work"
        self.work.mkdir()
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.addCleanup(shutil.rmtree, self.directory)
        self.addCleanup(self.release_leases)
        self.spawn = self.stack.enter_context(mock.patch.object(bootstrap.subprocess, "Popen", side_effect=AssertionError("No fixture executable may run")))
        self.network = self.stack.enter_context(mock.patch("urllib.request.urlopen", side_effect=urllib.error.URLError("offline")))
        self.stack.enter_context(mock.patch.object(bootstrap, "host_name", return_value="fixture-host"))
        self.lock = {"platform": "fixture-host", "rid": "fixture-rid", "artifacts": {}}
        self.archive("dotnet", {"dotnet": b"verified SDK"}, "dotnet")
        self.archive("assetripper-source", SOURCE, PROJECT)
        self.lock["artifacts"]["assetripper-source"]["patch_sha256"] = hashlib.sha256(PATCH).hexdigest()
        # Committed inputs: manifest, feed configuration and one lock per project.
        self.committed = self.directory / "committed"
        self.stack.enter_context(mock.patch.object(bootstrap, "ASSETRIPPER_SOURCE_DIR", self.committed))
        self.write_committed(SOURCE)

    def write_committed(self, files):
        shutil.rmtree(self.committed, ignore_errors=True)
        locks = self.committed / "locks/fixture-rid/Source/AssetRipper.GUI.Free"
        locks.mkdir(parents=True)
        (locks / "packages.lock.json").write_bytes(LOCK)
        (self.committed / "nuget.config").write_bytes(b"pinned feed")
        (self.committed / "lucid-identity.patch").write_bytes(PATCH)
        self.manifest.write_text("".join(f"{hashlib.sha256(data).hexdigest()}  {name}\n"
                                         for name, data in sorted(files.items())), encoding="utf-8")

    @property
    def manifest(self):
        return self.committed / "source-manifest.txt"

    @property
    def lock_file(self):
        return self.committed / "locks/fixture-rid/Source/AssetRipper.GUI.Free/packages.lock.json"

    def build(self):
        seen = {}

        def fixture_build(command, *args, **kwargs):
            tree = kwargs["cwd"]
            seen.update(command=command, nuget=(tree / "nuget.config").read_bytes(),
                        patched={name: (tree / name).read_bytes() for name in PATCHED},
                        lock=(tree / "Source/AssetRipper.GUI.Free/packages.lock.json").read_bytes())
            stage = Path(command[command.index("-o") + 1])
            stage.mkdir(parents=True)
            bootstrap.executable(stage / "AssetRipper.GUI.Free").write_bytes(b"inert generated executable")
            return {"returncode": 0, "timed_out": False, "log": "fixture"}

        with mock.patch.object(bootstrap, "run_logged", side_effect=fixture_build) as runner:
            result = bootstrap._ensure_assetripper_source(self.lock, self.work)
        return result, seen, runner

    def test_source_builds_without_aot_from_locked_restore_with_committed_feed_and_locks(self):
        result, seen, runner = self.build()
        command = seen["command"]
        self.assertEqual("publish", command[1])
        self.assertEqual(["-r", "fixture-rid", "--self-contained", "true"], command[command.index("-r"):command.index("-r") + 4])
        for argument in ("-p:PublishAot=false", "-p:RestoreLockedMode=true", "-p:RestorePackagesWithLockFile=true"):
            self.assertIn(argument, command)
        self.assertFalse([argument for argument in command if "PublishAot=true" in argument])
        self.assertTrue(any(argument.startswith("-p:RestoreConfigFile=") and argument.endswith("nuget.config") for argument in command))
        self.assertEqual((b"pinned feed", LOCK), (seen["nuget"], seen["lock"]))
        self.assertEqual(PATCHED, seen["patched"])
        self.assertIn(self.work / "tools/assetripper-source-builds", Path(result["path"]).parents)
        identity = json.loads(Path(result["path"]).with_name("lucid-build.json").read_text())
        self.assertEqual({"commit", "archive_sha256", "source_manifest_sha256", "lock_files_sha256", "nuget_config_sha256",
                          "sdk", "runtime", "publish_aot", "build_schema", "patch_sha256"}, set(identity))
        self.assertEqual((hashlib.sha256(PATCH).hexdigest(), 2), (identity["patch_sha256"], identity["build_schema"]))
        self.assertEqual((hashlib.sha256(self.manifest.read_bytes()).hexdigest(), "fixture-pin", "fixture-rid", False),
                         (identity["source_manifest_sha256"], identity["sdk"], identity["runtime"], identity["publish_aot"]))
        self.assertEqual(b"inert license", Path(result["path"]).with_name("LICENSE.txt").read_bytes())
        self.assertEqual([], sorted(path.name for path in (self.work / "tools").glob(".assetripper-source-*")))
        self.assertEqual(1, runner.call_count)
        self.spawn.assert_not_called()

    def test_archive_hash_drift_is_rejected_before_any_build(self):
        (self.work / "downloads/assetripper-source.tar").write_bytes(b"substituted archive")
        with mock.patch.object(bootstrap, "run_logged") as runner, \
                self.assertRaisesRegex(bootstrap.ToolError, "Could not download"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        runner.assert_not_called()

    def test_source_manifest_drift_is_rejected_before_any_build(self):
        cases = {"changed file": (dict(SOURCE, **{PROJECT: b"modified project"}), "differs from the pinned manifest"),
                 "missing file": ({name: data for name, data in SOURCE.items() if name != "Source/Other/Other.csproj"}, "Other.csproj"),
                 "extra file": (dict(SOURCE, **{"Source/Other/Extra.cs": b"extra"}), "Additional unpinned AssetRipper source: Source/Other/Extra.cs"),
                 "source-supplied lock": (dict(SOURCE, **{"Source/Other/packages.lock.json": LOCK}), "Additional unpinned")}
        for label, (files, message) in cases.items():
            with self.subTest(label), mock.patch.object(bootstrap, "run_logged") as runner:
                work = self.directory / label.replace(" ", "-")
                work.mkdir()
                shutil.copytree(self.work / "downloads", work / "downloads")
                self.archive("assetripper-source", files, PROJECT)
                self.lock["artifacts"]["assetripper-source"]["patch_sha256"] = hashlib.sha256(PATCH).hexdigest()
                shutil.copyfile(self.work / "downloads/assetripper-source.tar", work / "downloads/assetripper-source.tar")
                with self.assertRaisesRegex(bootstrap.ToolError, message):
                    bootstrap._ensure_assetripper_source(self.lock, work)
                runner.assert_not_called()

    def test_build_that_alters_pinned_source_is_rejected(self):
        def tampering_build(command, *args, **kwargs):
            (kwargs["cwd"] / PROJECT).write_bytes(b"build rewrote its own source")
            stage = Path(command[command.index("-o") + 1])
            stage.mkdir(parents=True)
            bootstrap.executable(stage / "AssetRipper.GUI.Free").write_bytes(b"inert")
            return {"returncode": 0, "timed_out": False, "log": "fixture"}

        with mock.patch.object(bootstrap, "run_logged", side_effect=tampering_build), \
                self.assertRaisesRegex(bootstrap.ToolError, "differs from the pinned manifest"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        self.assertEqual([], list((self.work / "tools/assetripper-source-builds").glob("[0-9a-f]*")))

    def test_patch_drift_is_rejected_before_any_build_and_the_patch_is_applied_after_the_manifest_check(self):
        (self.committed / "lucid-identity.patch").write_bytes(PATCH + b"--- /dev/null\n+++ b/Source/Other/More.cs\n@@ -0,0 +1 @@\n+more\n")
        with mock.patch.object(bootstrap, "run_logged") as runner, \
                self.assertRaisesRegex(bootstrap.ToolError, "differs from the pinned patch_sha256"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        runner.assert_not_called()

    def test_patch_that_touches_an_unpinned_or_missing_file_is_rejected(self):
        patch = b"--- a/Source/Other/Gone.cs\n+++ b/Source/Other/Gone.cs\n@@ -1 +1,2 @@\n x\n+y\n"
        (self.committed / "lucid-identity.patch").write_bytes(patch)
        self.lock["artifacts"]["assetripper-source"]["patch_sha256"] = hashlib.sha256(patch).hexdigest()
        with mock.patch.object(bootstrap, "run_logged") as runner, \
                self.assertRaisesRegex(bootstrap.ToolError, "is missing or a link"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        runner.assert_not_called()

    def test_build_that_alters_a_patched_file_is_rejected(self):
        def tampering_build(command, *args, **kwargs):
            (kwargs["cwd"] / "Source/Other/Added.cs").write_bytes(b"build rewrote the patch")
            stage = Path(command[command.index("-o") + 1])
            stage.mkdir(parents=True)
            bootstrap.executable(stage / "AssetRipper.GUI.Free").write_bytes(b"inert")
            return {"returncode": 0, "timed_out": False, "log": "fixture"}

        with mock.patch.object(bootstrap, "run_logged", side_effect=tampering_build), \
                self.assertRaisesRegex(bootstrap.ToolError, "patched source changed during the build: Source/Other/Added.cs"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)

    def test_lock_file_drift_changes_the_build_identity(self):
        first, _, runner = self.build()
        self.lock_file.write_bytes(b'{"version": 2, "dependencies": {}}')
        with mock.patch.object(bootstrap, "run_logged") as second, \
                self.assertRaisesRegex(bootstrap.ToolError, "changed during this process"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        second.assert_not_called()
        self.assertEqual(1, runner.call_count)
        self.assertTrue(Path(first["path"]).is_file())

    def test_feed_configuration_drift_changes_the_build_identity(self):
        self.build()
        (self.committed / "nuget.config").write_bytes(b"another feed")
        with mock.patch.object(bootstrap, "run_logged") as second, \
                self.assertRaisesRegex(bootstrap.ToolError, "changed during this process"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        second.assert_not_called()

    def test_sdk_drift_is_rejected_and_the_sdk_is_part_of_the_identity(self):
        result, _, _ = self.build()
        self.assertEqual("fixture-pin", json.loads(Path(result["path"]).with_name("lucid-build.json").read_text())["sdk"])
        self.lock["artifacts"]["dotnet"]["version"] = "other-sdk"
        with mock.patch.object(bootstrap, "run_logged") as second, \
                self.assertRaisesRegex(bootstrap.ToolError, "dotnet executable/dependency closure differs"):
            bootstrap._ensure_assetripper_source(self.lock, self.work)
        second.assert_not_called()

    def test_missing_or_unexpected_locks_are_rejected(self):
        for label in ("no rid", "stray file", "orphan project"):
            with self.subTest(label):
                self.write_committed(SOURCE)
                root = self.committed / "locks/fixture-rid"
                if label == "no rid":
                    shutil.rmtree(root)
                elif label == "stray file":
                    (root / "notes.txt").write_text("x")
                else:
                    (root / "Source/Missing").mkdir()
                    (root / "Source/Missing/packages.lock.json").write_bytes(LOCK)
                with mock.patch.object(bootstrap, "run_logged") as runner, \
                        self.assertRaises(bootstrap.ToolError):
                    bootstrap._ensure_assetripper_source(self.lock, self.work)
                runner.assert_not_called()

    def test_malformed_manifests_are_rejected(self):
        for text in ("", "zz  a.cs\n", f"{'0' * 64}  ../a.cs\n", f"{'0' * 64}  /a.cs\n",
                     f"{'0' * 64}  b.cs\n{'0' * 64}  a.cs\n", f"{'0' * 64}  a.cs\n{'0' * 64}  a.cs\n"):
            with self.subTest(text=text):
                self.manifest.write_text(text, encoding="utf-8")
                with self.assertRaises(bootstrap.ToolError):
                    bootstrap._source_manifest(self.manifest)

    def test_prebuilt_assetripper_and_default_selection_do_not_use_the_source_build(self):
        with mock.patch.object(bootstrap, "_ensure_assetripper_source") as source, \
                mock.patch.object(bootstrap, "_ensure_cpp2il", return_value={"status": "ready"}), \
                mock.patch.object(bootstrap, "_install", return_value=self.work / "tools/assetripper"), \
                mock.patch.object(bootstrap, "load_lock", return_value=dict(self.lock, artifacts={
                    "assetripper": {"version": "2.0.0", "entrypoint": "AssetRipper.GUI.Free"}})):
            bootstrap.bootstrap(self.work, ["assetripper"])
            bootstrap.bootstrap(self.work)
        source.assert_not_called()


class AddOnlyPatchTests(unittest.TestCase):
    def setUp(self):
        self.directory = Path(tempfile.mkdtemp(prefix="test-addonly-patch-"))
        self.addCleanup(shutil.rmtree, self.directory)
        self.tree = self.directory / "tree"
        (self.tree / "dir").mkdir(parents=True)
        (self.tree / "dir/a.txt").write_bytes(b"one\ntwo\nthree\nfour\n")

    def apply(self, patch: bytes):
        path = self.directory / "x.patch"
        path.write_bytes(patch)
        return bootstrap.apply_add_only_patch(self.tree, path)

    def test_hunks_insert_lines_and_new_files_are_created(self):
        result = self.apply(b"--- a/dir/a.txt\n+++ b/dir/a.txt\n@@ -1,2 +1,3 @@\n one\n+1.5\n two\n@@ -4 +5,2 @@\n four\n+five\n"
                            b"--- /dev/null\n+++ b/dir/new/b.txt\n@@ -0,0 +1,2 @@\n+x\n+y\n")
        self.assertEqual(b"one\n1.5\ntwo\nthree\nfour\nfive\n", (self.tree / "dir/a.txt").read_bytes())
        self.assertEqual(b"x\ny\n", (self.tree / "dir/new/b.txt").read_bytes())
        self.assertEqual({"dir/a.txt": hashlib.sha256(b"one\n1.5\ntwo\nthree\nfour\nfive\n").hexdigest(),
                          "dir/new/b.txt": hashlib.sha256(b"x\ny\n").hexdigest()}, result)

    def test_everything_that_is_not_an_exact_addition_is_rejected(self):
        before = (self.tree / "dir/a.txt").read_bytes()
        cases = {"removal": b"--- a/dir/a.txt\n+++ b/dir/a.txt\n@@ -1,2 +1 @@\n-one\n two\n",
                 "context mismatch": b"--- a/dir/a.txt\n+++ b/dir/a.txt\n@@ -1,2 +1,3 @@\n one\n+x\n zzz\n",
                 "shifted context": b"--- a/dir/a.txt\n+++ b/dir/a.txt\n@@ -2,1 +2,2 @@\n one\n+x\n",
                 "wrong new position": b"--- a/dir/a.txt\n+++ b/dir/a.txt\n@@ -1 +3,2 @@\n one\n+x\n",
                 "short hunk": b"--- a/dir/a.txt\n+++ b/dir/a.txt\n@@ -1 +1,3 @@\n one\n+x\n",
                 "existing file as new": b"--- /dev/null\n+++ b/dir/a.txt\n@@ -0,0 +1 @@\n+x\n",
                 "missing file": b"--- a/dir/none.txt\n+++ b/dir/none.txt\n@@ -1 +1,2 @@\n one\n+x\n",
                 "traversal": b"--- /dev/null\n+++ b/dir/../../evil\n@@ -0,0 +1 @@\n+x\n",
                 "rename": b"--- a/dir/a.txt\n+++ b/dir/c.txt\n@@ -1 +1,2 @@\n one\n+x\n",
                 "no final newline": b"--- /dev/null\n+++ b/dir/n.txt\n@@ -0,0 +1 @@\n+x",
                 "empty": b""}
        for label, patch in cases.items():
            with self.subTest(label), self.assertRaises(bootstrap.ToolError):
                self.apply(patch)
        self.assertEqual(before, (self.tree / "dir/a.txt").read_bytes())
        self.assertFalse((self.tree.parent / "evil").exists())


class CommittedAssetRipperSourceTests(unittest.TestCase):
    """The committed pin, manifest, locks and notice agree with one another."""

    def test_prebuilt_assetripper_pins_are_unchanged(self):
        lock = bootstrap.load_lock()
        self.assertEqual("237a6da404bf089c5e5c71a5a9a698c2ea580872292dfa5ce21e25fd1dbe9045", lock["artifacts"]["assetripper"]["sha256"])
        self.assertEqual("https://github.com/AssetRipper/AssetRipper/releases/download/2.0.0/AssetRipper_mac_arm64.tar.xz",
                         lock["artifacts"]["assetripper"]["url"])

    def test_source_pin_is_a_commit_tarball_that_every_host_shares(self):
        lock = bootstrap.load_lock()
        source = lock["artifacts"]["assetripper-source"]
        self.assertRegex(source["version"], "^[0-9a-f]{40}$")
        self.assertEqual("https://codeload.github.com/AssetRipper/AssetRipper/tar.gz/" + source["version"], source["url"])
        self.assertEqual("GPL-3.0", source["license"])
        self.assertEqual(1, source["strip_components"])
        for host in lock["hosts"].values():
            self.assertNotIn("assetripper-source", host["artifacts"])

    def test_manifest_covers_the_project_notice_and_every_locked_project(self):
        root = bootstrap.ASSETRIPPER_SOURCE_DIR
        manifest = bootstrap._source_manifest(root / "source-manifest.txt")
        source = bootstrap.load_lock()["artifacts"]["assetripper-source"]
        self.assertIn(source["entrypoint"], manifest)
        self.assertEqual(manifest["LICENSE.md"], hashlib.sha256((root / "LICENSE.md").read_bytes()).hexdigest())
        locks, _ = bootstrap._assetripper_locks(bootstrap.load_lock()["rid"])
        self.assertGreater(len(locks), 1)
        for relative in locks:
            project = Path(relative).parent
            self.assertTrue([name for name in manifest if Path(name).parent == project and name.endswith(".csproj")], relative)
        self.assertFalse([name for name in manifest if name.endswith("packages.lock.json")])

    def test_committed_patch_is_pinned_add_only_and_applies_to_the_pinned_manifest_files(self):
        root = bootstrap.ASSETRIPPER_SOURCE_DIR
        source = bootstrap.load_lock()["artifacts"]["assetripper-source"]
        self.assertEqual(source["patch_sha256"], hashlib.sha256((root / "lucid-identity.patch").read_bytes()).hexdigest())
        manifest = bootstrap._source_manifest(root / "source-manifest.txt")
        patch = (root / "lucid-identity.patch").read_text(encoding="utf-8")
        self.assertFalse([line for line in patch.splitlines() if line.startswith("-") and not line.startswith("--- ")])
        edited = [line[6:] for line in patch.splitlines() if line.startswith("+++ b/")]
        created = [line for line in edited if line not in manifest]
        self.assertEqual(["Source/AssetRipper.Export.UnityProjects/IdentityManifest.cs"], created)
        self.assertEqual(2, len(edited) - len(created))

    def test_feed_configuration_trusts_only_nuget_org_with_source_mapping(self):
        config = ET.parse(bootstrap.ASSETRIPPER_SOURCE_DIR / "nuget.config").getroot()
        sources = config.find("packageSources")
        self.assertIsNotNone(sources.find("clear"))
        self.assertEqual(["nuget.org"], [item.get("key") for item in sources.findall("add")])
        mapped = config.findall("packageSourceMapping/packageSource")
        self.assertEqual([("nuget.org", ["*"])], [(item.get("key"), [package.get("pattern") for package in item]) for item in mapped])


if __name__ == "__main__":
    unittest.main()

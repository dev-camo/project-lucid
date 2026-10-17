"""Asset path ownership regressions using only disposable synthetic trees."""

import json
import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lucidlib import assets, bootstrap
from test_assets import write_asset


def snapshot(root):
    """Record directories, links and complete file bytes without following links."""
    result = {}
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            result[relative] = ("link", os.readlink(path))
        elif path.is_dir():
            result[relative] = ("directory",)
        else:
            result[relative] = ("file", path.read_bytes())
    return result


class AssetPathTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.work = self.root / "cache"
        self.repo = self.root / "repo"
        self.foreign = self.root / "foreign"
        for path in (self.work, self.repo, self.foreign):
            path.mkdir()
        (self.foreign / "keep.bin").write_bytes(bytes(range(256)))
        self.app = self.root / "Game.app"
        data = self.app / "Contents/Resources/Data"
        data.mkdir(parents=True)
        (data / "globalgamemanagers").write_bytes(b"synthetic Unity data")
        cache_root = mock.patch.object(bootstrap, "CACHE_ROOT", self.work)
        cache_root.start()
        self.addCleanup(cache_root.stop)
        self.tool = bootstrap.executable(self.work / "tools/assetripper/AssetRipper.GUI.Free")
        self.tool.parent.mkdir(parents=True)
        self.tool.write_bytes(b"not executable")

    def make_export(self):
        run = self.work / "assets/runs/test"
        project = run / "project"
        write_asset(project, "Data/example.asset", "a" * 32)
        mapping = assets.build_asset_map(project)
        report = {"status": "exported", "run_id": "test", "project_path": str(project),
                  "asset_map_path": str(run / "asset-map.json"), "input_path": str(self.app),
                  "input_fingerprint": assets.fingerprint_manifest(assets._manifest(self.app)),
                  "tool": assets._tool_lock(), "settings": dict(assets.EXPORT_SETTINGS)}
        assets._json_write(run / "asset-map.json", mapping)
        assets._json_write(run / "report.json", report)
        assets._json_write(self.work / "assets/latest-assets.json", report)
        (run / "assetripper.log").write_text("Finished post-export\n")
        return run

    def assert_preserved(self, action, protected, message=None):
        before = {path: snapshot(path) for path in protected}
        with self.assertRaisesRegex(ValueError, message or ""):
            action()
        for path, original in before.items():
            self.assertEqual(snapshot(path), original, str(path))

    def test_extraction_rejects_descendant_links_before_tool_verification_or_writes(self):
        for target in (self.app, self.foreign):
            for relative in ("assets", "assets/runs"):
                with self.subTest(target=target.name, relative=relative):
                    link = self.work / relative
                    link.parent.mkdir(parents=True, exist_ok=True)
                    link.symlink_to(target, target_is_directory=True)
                    try:
                        bootstrap.validate_work_dir(self.work)
                        with mock.patch.object(assets, "verified_tool_path", return_value=self.tool) as verified, \
                             mock.patch.object(assets, "_start_ripper", side_effect=RuntimeError("must not launch")) as start:
                            self.assert_preserved(lambda: assets.extract_assets(self.app, self.work),
                                                  (self.app, self.foreign, self.work))
                            verified.assert_not_called()
                            start.assert_not_called()
                    finally:
                        link.unlink()
                        if relative == "assets/runs":
                            link.parent.rmdir()

    def test_extraction_rejects_dangling_publication_link_before_start(self):
        link = self.work / "assets/latest-assets.json"
        link.parent.mkdir()
        link.symlink_to(self.foreign / "missing.json")
        # Declare the fixture bundle so variant admission cannot be what rejects it.
        for relative in (assets.inputvariants.GAME_ASSEMBLY, assets.inputvariants.METADATA, assets.inputvariants.INFO_PLIST):
            (self.app / relative).parent.mkdir(parents=True, exist_ok=True)
            (self.app / relative).write_bytes(b"synthetic " + relative.encode())
        fingerprint = assets.fingerprint_manifest(assets._manifest(self.app))["critical_files"]
        declared = {"binary_sha256": fingerprint.get(assets.inputvariants.GAME_ASSEMBLY),
                    "metadata_sha256": fingerprint.get(assets.inputvariants.METADATA),
                    "info_plist_sha256": fingerprint.get(assets.inputvariants.INFO_PLIST), "x86_64_slice_sha256": None,
                    "qualified": frozenset((assets.inputvariants.ASSET_PREPARATION,))}
        with mock.patch.dict(assets.inputvariants.VARIANTS, {"fixture": declared}), \
             mock.patch.object(assets, "verified_tool_path", return_value=self.tool), \
             mock.patch.object(assets, "_start_ripper") as start:
            self.assertEqual("fixture", assets.inputvariants.admit(fingerprint, assets.inputvariants.ASSET_PREPARATION))
            self.assert_preserved(lambda: assets.extract_assets(self.app, self.work),
                                  (self.app, self.foreign, self.work), "cannot contain a symbolic link")
            start.assert_not_called()

    def test_work_alias_is_rejected_without_writing_foreign_cache(self):
        alias = self.work / "alias"
        alias.symlink_to(self.foreign, target_is_directory=True)
        self.assert_preserved(lambda: assets._safe_work_dir(alias, self.app),
                              (self.app, self.foreign, self.work))

    def test_finalization_rejects_linked_report_map_log_and_publication_files(self):
        run = self.make_export()
        for path in (run / "report.json", run / "asset-map.json", run / "assetripper.log",
                     self.work / "assets/latest-assets.json"):
            with self.subTest(path=path.name):
                original = path.read_bytes()
                foreign = self.foreign / path.name
                foreign.write_bytes(original)
                path.unlink()
                path.symlink_to(foreign)
                self.assert_preserved(lambda: assets.finalize_asset_export(run, self.work),
                                      (self.app, self.foreign, self.work))
                path.unlink()
                path.write_bytes(original)

    def test_finalization_rejects_internal_project_and_run_aliases(self):
        run = self.make_export()
        project = run / "project"
        project.rename(run / "physical-project")
        project.symlink_to(run / "physical-project", target_is_directory=True)
        self.assert_preserved(lambda: assets.finalize_asset_export(run, self.work),
                              (self.app, self.foreign, self.work))
        project.unlink()
        (run / "physical-project").rename(project)
        alias = run.with_name("alias")
        alias.symlink_to(run, target_is_directory=True)
        self.assert_preserved(lambda: assets.finalize_asset_export(alias, self.work),
                              (self.app, self.foreign, self.work))

    def test_valid_retained_export_still_finalizes(self):
        run = self.make_export()
        before = snapshot(self.app)
        result = assets.finalize_asset_export(run, self.work)
        self.assertEqual(result["status"], "exported")
        self.assertEqual(result["summary"]["assets"], 1)
        self.assertEqual(snapshot(self.app), before)
        self.assertEqual(json.loads((self.work / "assets/latest-assets.json").read_text()), result)

    def test_relative_retained_run_still_finalizes_from_physical_fixture_cwd(self):
        self.make_export()
        before = snapshot(self.app), snapshot(self.foreign)
        previous_cwd = Path.cwd()
        try:
            os.chdir(self.root)
            result = assets.finalize_asset_export(Path("cache/assets/runs/test"), Path("cache"))
        finally:
            os.chdir(previous_cwd)
        self.assertEqual(result["status"], "exported")
        self.assertEqual(result["summary"]["assets"], 1)
        self.assertEqual(json.loads((self.work / "assets/latest-assets.json").read_text()), result)
        self.assertEqual((snapshot(self.app), snapshot(self.foreign)), before)

    def test_relative_finalization_still_rejects_links_and_parent_traversal(self):
        run = self.make_export()
        alias = run.with_name("alias")
        alias.symlink_to(run, target_is_directory=True)
        previous_cwd = Path.cwd()
        try:
            os.chdir(self.root)
            for path, diagnostic in ((Path("cache/assets/runs/alias"), "symbolic link"),
                                     (Path("cache/assets/runs/test/../test"), "parent traversal")):
                with self.subTest(path=path), self.assertRaisesRegex(ValueError, diagnostic):
                    before = snapshot(self.app), snapshot(self.foreign), snapshot(self.work)
                    try:
                        assets.finalize_asset_export(path, Path("cache"))
                    finally:
                        self.assertEqual((snapshot(self.app), snapshot(self.foreign), snapshot(self.work)), before)
        finally:
            os.chdir(previous_cwd)

    def test_prepare_rejects_linked_receipts_and_backups_before_replacing_owned_assets(self):
        self.make_export()
        assets.prepare_assets(self.repo, self.work)
        for relative in ("assets/backups", "assets/latest-assets.json", "assets/latest-prepare.json"):
            with self.subTest(relative=relative):
                path = self.work / relative
                original = path.read_bytes() if path.is_file() else None
                if original is not None:
                    foreign = self.foreign / path.name
                    foreign.write_bytes(original)
                    path.unlink()
                else:
                    foreign = self.foreign
                path.symlink_to(foreign, target_is_directory=original is None)
                self.assert_preserved(lambda: assets.prepare_assets(self.repo, self.work),
                                      (self.app, self.foreign, self.work, self.repo))
                path.unlink()
                if original is not None:
                    path.write_bytes(original)

    def test_prepare_rejects_linked_and_external_captured_evidence(self):
        run = self.make_export()
        pointer = self.work / "assets/latest-assets.json"
        original_report = json.loads(pointer.read_text())
        for field in ("project_path", "asset_map_path"):
            with self.subTest(field=field):
                source = Path(original_report[field])
                alias = run / ("alias-" + source.name)
                alias.symlink_to(source, target_is_directory=source.is_dir())
                report = dict(original_report, **{field: str(alias)})
                assets._json_write(pointer, report)
                self.assert_preserved(lambda: assets.prepare_assets(self.repo, self.work),
                                      (self.app, self.foreign, self.work, self.repo))
                alias.unlink()
        foreign_map = self.foreign / "map.json"
        foreign_map.write_bytes(Path(original_report["asset_map_path"]).read_bytes())
        assets._json_write(pointer, dict(original_report, asset_map_path=str(foreign_map)))
        self.assert_preserved(lambda: assets.prepare_assets(self.repo, self.work),
                              (self.app, self.foreign, self.work, self.repo))

    def test_prepare_rejects_repository_alias_and_linked_ownership_marker(self):
        self.make_export()
        alias = self.root / "repo-alias"
        alias.symlink_to(self.repo, target_is_directory=True)
        self.assert_preserved(lambda: assets.prepare_assets(alias, self.work),
                              (self.app, self.foreign, self.work, self.repo))
        assets.prepare_assets(self.repo, self.work)
        marker = self.repo / "Assets/Recovered" / assets.MARKER
        foreign = self.foreign / "owner.json"
        foreign.write_bytes(marker.read_bytes())
        marker.unlink()
        marker.symlink_to(foreign)
        self.assert_preserved(lambda: assets.prepare_assets(self.repo, self.work),
                              (self.app, self.foreign, self.work, self.repo))

    def test_preparation_cannot_publish_into_supplied_bundle(self):
        self.make_export()
        self.assert_preserved(lambda: assets.prepare_assets(self.app, self.work),
                              (self.app, self.foreign, self.work))

    def test_atomic_report_collision_does_not_delete_or_follow_foreign_entry(self):
        destination = self.work / "report.json"
        temporary = self.work / "report.json.fixed.tmp"
        for link in (False, True):
            with self.subTest(link=link):
                if link:
                    temporary.symlink_to(self.foreign / "keep.bin")
                else:
                    temporary.write_bytes(b"pre-existing unrelated temporary file")
                before = snapshot(self.work), snapshot(self.foreign)
                with mock.patch.object(assets.uuid, "uuid4", return_value=SimpleNamespace(hex="fixed")):
                    with self.assertRaises(FileExistsError):
                        assets._json_write(destination, {"synthetic": True})
                self.assertEqual((snapshot(self.work), snapshot(self.foreign)), before)
                temporary.unlink()


if __name__ == "__main__":
    unittest.main()

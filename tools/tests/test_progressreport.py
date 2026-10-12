"""Generated publication must retain prior output on failed or stale evidence."""

import json
import importlib.util
import io
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from contextlib import redirect_stdout

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import progressreport
from lucidlib.verification import artifact_fingerprint


class ProgressPublicationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="Lucid code progress ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        (self.root / "Assets").mkdir()
        (self.root / "Assets/Runtime.cs").write_text("// sealed runtime input\n")
        self.readme = self.root / "README.md"
        self.readme.write_text("# Project Lucid\n\nUser instructions.\n")
        self.snapshot = self.root / progressreport.SNAPSHOT
        self.snapshot.parent.mkdir()
        self.snapshot.write_text("previous validated snapshot\n")
        self.original = (self.readme.read_bytes(), self.snapshot.read_bytes())
        self.source = artifact_fingerprint(self.root)
        self.report = {"source_fingerprint": self.source, "totals": {"implemented_source_bodies": 1}}
        self.section = progressreport.START + "\n## Progress\n\nOne sealed method.\n" + progressreport.END + "\n"
        self.patches = [
            patch("lucidlib.codeprogress.run_code_progress", return_value={"public": self.report}),
            patch("lucidlib.codeprogress.validate_public_counts", side_effect=self.validate),
            patch("lucidlib.codeprogress.render_readme_section", return_value=self.section)]
        for item in self.patches:
            item.start()
            self.addCleanup(item.stop)

    def validate(self, report, root=None):
        if root is not None and report["source_fingerprint"] != artifact_fingerprint(root):
            raise ValueError("stale source")

    def generate(self, stage=False):
        return progressreport._generate_progress(self.root, self.root / "cache", self.root / "input", "macos", stage)

    def assert_preserved(self):
        self.assertEqual(self.original, (self.readme.read_bytes(), self.snapshot.read_bytes()))

    def test_success_retains_user_text_and_checks_without_input(self):
        self.generate()
        self.assertTrue(self.readme.read_text().startswith("# Project Lucid\n\nUser instructions.\n\n"))
        self.assertEqual(self.report, json.loads(self.snapshot.read_text()))
        self.assertEqual("complete", progressreport.check_progress(self.root)["status"])

    def test_repeated_publication_is_identical(self):
        self.generate()
        first = (self.readme.read_bytes(), self.snapshot.read_bytes())
        self.generate()
        self.assertEqual(first, (self.readme.read_bytes(), self.snapshot.read_bytes()))

    def test_collector_failure_preserves_previous_outputs(self):
        with patch("lucidlib.codeprogress.run_code_progress", side_effect=ValueError("missing compiler output")):
            with self.assertRaisesRegex(ValueError, "missing compiler"):
                self.generate()
        self.assert_preserved()

    def test_source_drift_preserves_previous_outputs(self):
        def drift(*args):
            (self.root / "Assets/Runtime.cs").write_text("changed source\n")
            return {"public": self.report}
        with patch("lucidlib.codeprogress.run_code_progress", side_effect=drift):
            with self.assertRaisesRegex(ValueError, "stale"):
                self.generate()
        self.assert_preserved()

    def test_concurrent_readme_edit_is_retained(self):
        def edit(*args):
            self.readme.write_text("Concurrent user edit\n")
            return {"public": self.report}
        with patch("lucidlib.codeprogress.run_code_progress", side_effect=edit):
            with self.assertRaisesRegex(ValueError, "outputs changed"):
                self.generate()
        self.assertEqual("Concurrent user edit\n", self.readme.read_text())
        self.assertEqual(self.original[1], self.snapshot.read_bytes())

    def test_second_replace_failure_rolls_back_both_outputs(self):
        replace = progressreport._replace
        count = [0]
        def fail_second(path, content):
            count[0] += 1
            if count[0] == 2:
                raise OSError("interrupted publication")
            return replace(path, content)
        with patch.object(progressreport, "_replace", side_effect=fail_second):
            with self.assertRaisesRegex(OSError, "interrupted"):
                self.generate()
        self.assert_preserved()

    def test_missing_previous_snapshot_is_removed_after_failed_publish(self):
        self.snapshot.unlink()
        with patch.object(progressreport, "check_progress", side_effect=ValueError("invalid output")):
            with self.assertRaises(ValueError):
                self.generate()
        self.assertFalse(self.snapshot.exists())
        self.assertEqual(self.original[0], self.readme.read_bytes())

    def test_edited_generated_table_is_rejected_offline(self):
        self.generate()
        self.readme.write_text(self.readme.read_text().replace("One sealed", "Two guessed"))
        with self.assertRaisesRegex(ValueError, "differs"):
            progressreport.check_progress(self.root)

    def test_duplicate_or_reversed_markers_are_rejected(self):
        for text in (progressreport.START * 2 + progressreport.END, progressreport.END + progressreport.START):
            with self.assertRaises(ValueError):
                progressreport.split_readme(text)

    def test_progress_output_symlink_is_rejected(self):
        self.snapshot.unlink()
        outside = self.root / "outside.json"
        outside.write_text("outside\n")
        self.snapshot.symlink_to(outside)
        with self.assertRaisesRegex(ValueError, "symlinked"):
            self.generate()
        self.assertEqual("outside\n", outside.read_text())

    def test_source_changes_after_write_roll_back_outputs(self):
        replace = progressreport._replace
        count = [0]
        def drift_after(path, content):
            replace(path, content)
            count[0] += 1
            if count[0] == 2:
                (self.root / "Assets/Runtime.cs").write_text("changed during publication\n")
        with patch.object(progressreport, "_replace", side_effect=drift_after):
            with self.assertRaisesRegex(ValueError, "Source changed"):
                self.generate()
        self.assert_preserved()

    def test_unstaged_readme_edits_are_not_absorbed_by_hook(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        subprocess.run(["git", "add", "README.md", "Assets"], cwd=self.root, check=True)
        self.readme.write_text("Unstaged user instruction\n")
        with self.assertRaisesRegex(ValueError, "Stage README edits"):
            self.generate(stage=True)
        self.assertEqual("Unstaged user instruction\n", self.readme.read_text())

    def test_staged_readme_only_race_is_not_overwritten(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        subprocess.run(["git", "add", "README.md", "Assets"], cwd=self.root, check=True)
        def edit_index(*args):
            oid = subprocess.check_output(["git", "hash-object", "-w", "--stdin"],
                                          cwd=self.root, input=b"Staged-only human instructions\n").decode().strip()
            subprocess.run(["git", "update-index", "--cacheinfo", "100644," + oid + ",README.md"],
                           cwd=self.root, check=True)
            return {"public": self.report}
        with patch("lucidlib.codeprogress.run_code_progress", side_effect=edit_index):
            with self.assertRaisesRegex(ValueError, "Stage README edits"):
                self.generate(stage=True)
        self.assert_preserved()
        self.assertEqual(b"Staged-only human instructions\n", subprocess.check_output(
            ["git", "show", ":README.md"], cwd=self.root))

    def test_cli_offline_check_never_uses_cache_validation(self):
        spec = importlib.util.spec_from_file_location("lucid_cli_progress_test", Path(__file__).resolve().parents[1] / "lucid.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        report = {"status": "complete", "totals": {"implemented_source_bodies": 1, "original_native_bodies": 10}}
        with patch.object(progressreport, "check_progress", return_value=report), patch(
                "lucidlib.bootstrap.validate_work_dir", side_effect=AssertionError("offline check accessed cache")), redirect_stdout(io.StringIO()):
            self.assertEqual(0, module.main(["progress", "--check", "--work-dir", str(self.root / "unsupported-cache")]))


if __name__ == "__main__":
    unittest.main()


class ProgressRefreshTests(unittest.TestCase):
    validate = ProgressPublicationTests.validate
    assert_preserved = ProgressPublicationTests.assert_preserved

    def setUp(self):
        ProgressPublicationTests.setUp(self)
        self.git('init', '-q')
        self.git('config', 'user.name', 'Test')
        self.git('config', 'user.email', 'test@example.invalid')
        self.git('add', '.')
        self.git('commit', '-qm', 'Initial source')
        self.head = self.git('rev-parse', 'HEAD').strip()
        self.build = patch('lucidlib.playercode.run_player_code', return_value={'status': 'compiled'})
        self.compiler = self.build.start()
        self.addCleanup(self.build.stop)
        self.work = self.root / '.cache/project-lucid/refresh'
        for name, value in [('REPO_ROOT', self.root), ('CACHE_ROOT', self.root / '.cache/project-lucid')]:
            item = patch('lucidlib.bootstrap.' + name, value)
            item.start()
            self.addCleanup(item.stop)

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root).decode()

    def refresh(self, head=None):
        return progressreport.refresh_progress(self.root, self.work, self.root / 'input',
                                              'macos', head or self.head)

    def test_refresh_compiles_before_measurement(self):
        order = []
        self.compiler.side_effect = lambda *args: order.append('compile') or {'status': 'compiled'}
        with patch('lucidlib.codeprogress.run_code_progress',
                   side_effect=lambda *args: order.append('measure') or {'public': self.report}):
            self.refresh()
        self.assertEqual(order, ['compile', 'measure'])
        self.assertEqual(self.git('rev-parse', 'HEAD').strip(), self.head)
        self.assertIn('One sealed method', self.readme.read_text())

    def test_wrong_or_abbreviated_head_does_not_build(self):
        for head in ('0' * 40, self.head[:12], '--help'):
            with self.assertRaises(ValueError):
                self.refresh(head)
        self.compiler.assert_not_called()
        self.assert_preserved()

    def test_staged_and_unstaged_edits_do_not_build(self):
        (self.root / 'Assets/Runtime.cs').write_text('changed source')
        with self.assertRaisesRegex(ValueError, 'clean tracked'):
            self.refresh()
        self.git('add', 'Assets/Runtime.cs')
        with self.assertRaisesRegex(ValueError, 'clean tracked'):
            self.refresh()
        self.compiler.assert_not_called()
        self.assert_preserved()

    def test_failed_build_does_not_measure_or_publish(self):
        for outcome in ({'status': 'failed'}, ValueError('compiler failed')):
            self.compiler.side_effect = outcome if isinstance(outcome, Exception) else None
            self.compiler.return_value = outcome
            with patch('lucidlib.codeprogress.run_code_progress') as measure:
                with self.assertRaises(ValueError):
                    self.refresh()
                measure.assert_not_called()
            self.assert_preserved()

    def test_head_changed_during_compilation_does_not_measure(self):
        def compile(*args):
            self.git('commit', '--allow-empty', '-qm', 'Concurrent commit')
            return {'status': 'compiled'}
        self.compiler.side_effect = compile
        with patch('lucidlib.codeprogress.run_code_progress') as measure:
            with self.assertRaisesRegex(ValueError, 'head changed'):
                self.refresh()
            measure.assert_not_called()
        self.assert_preserved()

    def test_source_changed_during_compilation_does_not_measure(self):
        def compile(*args):
            (self.root / 'Assets/Runtime.cs').write_text('concurrent edit')
            return {'status': 'compiled'}
        self.compiler.side_effect = compile
        with patch('lucidlib.codeprogress.run_code_progress') as measure:
            with self.assertRaisesRegex(ValueError, 'clean tracked'):
                self.refresh()
            measure.assert_not_called()
        self.assert_preserved()

    def test_head_changed_during_measurement_does_not_publish(self):
        def measure(*args):
            self.git('commit', '--allow-empty', '-qm', 'Concurrent commit')
            return {'public': self.report}
        with patch('lucidlib.codeprogress.run_code_progress', side_effect=measure):
            with self.assertRaisesRegex(ValueError, 'head changed'):
                self.refresh()
        self.assert_preserved()

    def test_final_head_change_rolls_back_publication(self):
        def check(*args):
            self.git('commit', '--allow-empty', '-qm', 'Concurrent commit')
        with patch('lucidlib.progressreport.check_progress', side_effect=check):
            with self.assertRaisesRegex(ValueError, 'head changed'):
                self.refresh()
        self.assert_preserved()

    def test_untracked_maintained_source_does_not_build(self):
        (self.root / 'Assets/Untracked.cs').write_text('not part of reviewed commit')
        with self.assertRaisesRegex(ValueError, 'absent from the index'):
            self.refresh()
        self.compiler.assert_not_called()
        self.assert_preserved()

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

    def init_commit(self):
        self.generate()
        subprocess.run(['git', 'init', '-q'], cwd=self.root, check=True)
        subprocess.run(['git', 'add', 'README.md', 'docs', 'Assets'], cwd=self.root, check=True)
        subprocess.run(['git', '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                        'commit', '-qm', 'inert published fixture'], cwd=self.root, check=True)

    def test_stage_validates_changed_source_without_generation_or_writes(self):
        self.init_commit()
        (self.root / 'Assets/Runtime.cs').write_text('// changed maintained source\n')
        subprocess.run(['git', 'add', 'Assets'], cwd=self.root, check=True)
        self.readme.write_text(self.readme.read_text().replace('User instructions.', 'Staged human instructions.'))
        subprocess.run(['git', 'add', 'README.md'], cwd=self.root, check=True)
        self.readme.write_text(self.readme.read_text() + '\nUnstaged human instructions.\n')
        before = (self.readme.read_bytes(), self.snapshot.read_bytes(), subprocess.check_output(['git', 'write-tree'], cwd=self.root))
        with patch('lucidlib.codeprogress.run_code_progress', side_effect=AssertionError('Hook must not measure')), \
             patch('lucidlib.bootstrap.validate_work_dir', side_effect=AssertionError('Hook must not access cache')):
            report = progressreport.generate_progress(self.root, self.root / 'cache', self.root / 'input', stage=True)
        self.assertFalse(report['staged'])
        self.assertEqual(before, (self.readme.read_bytes(), self.snapshot.read_bytes(), subprocess.check_output(['git', 'write-tree'], cwd=self.root)))

    def test_stage_rejects_generated_snapshot_or_section_edits(self):
        self.init_commit()
        for path in (self.snapshot, self.readme):
            original = path.read_bytes()
            path.write_text(path.read_text().replace('One sealed', 'Guessed') if path == self.readme else '{}\n')
            subprocess.run(['git', 'add', str(path)], cwd=self.root, check=True)
            with self.subTest(path=path), self.assertRaisesRegex(ValueError, 'owned by main Actions'):
                progressreport.check_staged_progress(self.root)
            path.write_bytes(original)
            subprocess.run(['git', 'add', str(path)], cwd=self.root, check=True)

    def test_stage_retains_matching_source_index_validation(self):
        self.init_commit()
        (self.root / 'Assets/Runtime.cs').write_text('// unstaged source\n')
        with self.assertRaisesRegex(ValueError, 'Stage all maintained source'):
            progressreport.check_staged_progress(self.root)

    def test_cli_stage_never_measures_stages_or_validates_cache(self):
        spec = importlib.util.spec_from_file_location('lucid_cli_hook_test', Path(__file__).resolve().parents[1] / 'lucid.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with patch.object(progressreport, 'check_staged_progress', return_value={'status': 'complete'}) as check, \
             patch('lucidlib.bootstrap.validate_work_dir', side_effect=AssertionError('Hook accessed cache')), \
             patch.object(progressreport, 'generate_progress', side_effect=AssertionError('Hook generated totals')), redirect_stdout(io.StringIO()):
            self.assertEqual(0, module.main(['progress', '--stage', '--work-dir', str(self.root / 'unused-cache')]))
        check.assert_called_once_with(module.ROOT)

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

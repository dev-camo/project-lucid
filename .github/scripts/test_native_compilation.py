"""Native driver boundaries; these fixtures do not compile or create receipts."""
import sys
import tempfile
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import native_compilation
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))


class NativeCompilationTests(unittest.TestCase):
    def test_source_reporting_code_must_match_trusted_control(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = Path(directory) / 'source', Path(directory) / 'control'
            for root in (source, control):
                (root / 'tools').mkdir(parents=True)
                (root / 'tools/helper.py').write_text('trusted = True\n')
                (root / 'Assets/Editor').mkdir(parents=True)
                for name in ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'):
                    (root / 'Assets/Editor' / name).write_text('// trusted helper\n')
            native_compilation.require_control_reporting_tools(source, control)
            (source / 'tools/helper.py').write_text('raise AssertionError("PR tools must not execute")\n')
            with self.assertRaisesRegex(ValueError, 'trusted control'):
                native_compilation.require_control_reporting_tools(source, control)

    def test_control_cache_binding_preserves_confinement_and_restores_state(self):
        from lucidlib import bootstrap
        original = bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT, bootstrap.LOCK_FILE
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve()
            work = source / '.cache/project-lucid'
            with native_compilation.project_cache_scope(bootstrap, source, work):
                self.assertEqual(work, bootstrap.validate_work_dir(work))
                self.assertEqual(original[2], bootstrap.LOCK_FILE)
                with self.assertRaises(ValueError):
                    bootstrap.validate_work_dir(source / 'Assets')
            self.assertEqual(original, (bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT, bootstrap.LOCK_FILE))

    def test_relative_pdb_paths_use_source_directory_and_restore_after_failure(self):
        previous = Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve()
            expected = source / 'Packages/maintained/Runtime/Source.cs'
            with self.assertRaisesRegex(ValueError, 'phase failed'):
                with native_compilation.source_working_directory(source):
                    self.assertEqual(expected, Path('Packages/maintained/Runtime/Source.cs').resolve())
                    raise ValueError('phase failed')
        self.assertEqual(previous, Path.cwd())

    def test_cache_binding_rejects_symlink_and_restores_state_on_failure(self):
        from lucidlib import bootstrap
        original = bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT
        with tempfile.TemporaryDirectory() as directory:
            source, outside = Path(directory).resolve() / 'source', Path(directory).resolve() / 'outside'
            source.mkdir(); outside.mkdir(); (source / '.cache').symlink_to(outside, target_is_directory=True)
            with self.assertRaises(ValueError):
                with native_compilation.project_cache_scope(bootstrap, source, source / '.cache/project-lucid'):
                    self.fail('Symlink must reject before the phase starts')
        self.assertEqual(original, (bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT))

    def test_native_identity_is_bound_to_actual_editor_bytes(self):
        engine = [{'name': 'editor', 'sha256': 'a' * 64}]
        native_compilation.require_execution_identity('native@sha256:' + 'a' * 64, engine)
        with self.assertRaises(ValueError):
            native_compilation.require_execution_identity('native@sha256:' + 'b' * 64, engine)
        with self.assertRaises(ValueError):
            native_compilation.require_execution_identity('mutable:latest', engine)

    def test_actual_compiled_phase_status_is_accepted_and_failures_rejected(self):
        native_compilation.require_phase_success('player-code', {'status': 'compiled'})
        for phase in ('bootstrap', 'player-code', 'progress'):
            with self.subTest(phase=phase), self.assertRaises(ValueError):
                native_compilation.require_phase_success(phase, {'status': 'failed'})

    def test_original_input_approval_cannot_follow_pr_report_claims(self):
        approved = {'input_binary_sha256': 'a' * 64, 'input_metadata_sha256': 'b' * 64}
        native_compilation.require_approved_original(dict(approved), approved)
        forged = dict(approved, input_binary_sha256='0' * 64)
        with self.assertRaises(ValueError):
            native_compilation.require_approved_original(forged, approved)


if __name__ == "__main__":
    unittest.main()

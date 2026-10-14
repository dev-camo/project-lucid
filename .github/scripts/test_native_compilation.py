"""Native driver boundaries; these fixtures do not compile or create receipts."""
import shutil
import sys
import tempfile
import unittest
from unittest import mock
from contextlib import nullcontext
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

    def test_overlay_admits_pr_game_code_with_trusted_reporting_code_only(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = Path(directory) / 'source', Path(directory) / 'control'
            for root in (source, control):
                (root / 'tools/lucidlib').mkdir(parents=True)
                (root / 'tools/lucidlib/verification.py').write_text('REQUIRED = ("trusted",)\n')
                (root / 'Assets/Editor').mkdir(parents=True)
                for name in ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'):
                    (root / 'Assets/Editor' / name).write_text('// trusted helper\n')
                (root / 'Assets/Scripts').mkdir(parents=True)
            (control / 'tools/removed_by_pr.py').write_text('trusted = True\n')
            (source / 'tools/lucidlib/verification.py').write_text('REQUIRED = ("trusted", "pr")\n')
            (source / 'tools/added_by_pr.py').write_text('raise AssertionError("PR tools must not execute")\n')
            (source / 'Assets/Editor/LucidPlayerCompilation.cs').write_text('// PR driver must not run\n')
            (source / 'Assets/Scripts/Game.cs').write_text('// PR game code is kept\n')
            self.assertEqual([], native_compilation.overlay_control_reporting_tools(control, control))
            replaced = native_compilation.overlay_control_reporting_tools(source, control)
            self.assertEqual(['Assets/Editor/LucidPlayerCompilation.cs', 'tools/added_by_pr.py',
                              'tools/lucidlib/verification.py', 'tools/removed_by_pr.py'], replaced)
            native_compilation.require_control_reporting_tools(source, control)
            self.assertEqual(native_compilation.reporting_files(control), native_compilation.reporting_files(source))
            self.assertFalse((source / 'tools/added_by_pr.py').exists())
            self.assertEqual('// PR game code is kept\n', (source / 'Assets/Scripts/Game.cs').read_text())
            self.assertEqual([], native_compilation.overlay_control_reporting_tools(source, control))

    def test_overlay_restores_a_deleted_fixed_editor_reporting_file(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = Path(directory) / 'source', Path(directory) / 'control'
            for root in (source, control):
                (root / 'tools').mkdir(parents=True)
                (root / 'tools/helper.py').write_text('trusted = True\n')
                (root / 'Assets/Editor').mkdir(parents=True)
                for name in ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'):
                    (root / 'Assets/Editor' / name).write_text('// trusted helper\n')
            (source / 'Assets/Editor/LucidPlayerCompilation.cs').unlink()
            with self.assertRaisesRegex(ValueError, 'regular files'):
                native_compilation.require_control_reporting_tools(source, control)
            self.assertEqual(['Assets/Editor/LucidPlayerCompilation.cs'],
                             native_compilation.overlay_control_reporting_tools(source, control))
            self.assertEqual('// trusted helper\n', (source / 'Assets/Editor/LucidPlayerCompilation.cs').read_text())
            native_compilation.require_control_reporting_tools(source, control)

    def overlay_pair(self, directory, trusted):
        source, control = Path(directory) / 'source', Path(directory) / 'control'
        for root in (source, control):
            (root / 'Assets/Editor').mkdir(parents=True)
            for name in ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'):
                (root / 'Assets/Editor' / name).write_text('// trusted helper\n')
            (root / 'tools').mkdir()
        for relative, text in trusted.items():
            for root in (source, control):
                (root / relative).parent.mkdir(parents=True, exist_ok=True)
                (root / relative).write_text(text)
        return source, control

    def assert_overlaid(self, source, control):
        native_compilation.require_control_reporting_tools(source, control)
        self.assertEqual(native_compilation.reporting_files(control), native_compilation.reporting_files(source))

    def test_overlay_replaces_a_directory_where_a_trusted_file_belongs(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = self.overlay_pair(directory, {'tools/helper.py': 'trusted = True\n'})
            (source / 'tools/helper.py').unlink()
            (source / 'tools/helper.py').mkdir()
            (source / 'tools/helper.py/child.py').write_text('pr = True\n')
            self.assertEqual(['tools/helper.py', 'tools/helper.py/child.py'],
                             native_compilation.overlay_control_reporting_tools(source, control))
            self.assert_overlaid(source, control)

    def test_overlay_replaces_a_file_where_a_trusted_directory_belongs(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = self.overlay_pair(directory, {'tools/pkg/helper.py': 'trusted = True\n'})
            (source / 'tools/pkg/helper.py').unlink()
            (source / 'tools/pkg').rmdir()
            (source / 'tools/pkg').write_text('pr = True\n')
            self.assertEqual(['tools/pkg', 'tools/pkg/helper.py'],
                             native_compilation.overlay_control_reporting_tools(source, control))
            self.assert_overlaid(source, control)

    def test_overlay_restores_a_tools_tree_replaced_by_a_file(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = self.overlay_pair(directory, {'tools/a.py': 'a\n', 'tools/lucidlib/b.py': 'b\n'})
            shutil.rmtree(source / 'tools')
            (source / 'tools').write_text('not a directory\n')
            self.assertEqual(['tools/a.py', 'tools/lucidlib/b.py'],
                             native_compilation.overlay_control_reporting_tools(source, control))
            self.assert_overlaid(source, control)

    def test_overlay_replaces_a_directory_at_a_fixed_editor_reporting_path(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = self.overlay_pair(directory, {'tools/a.py': 'a\n'})
            fixed = source / 'Assets/Editor/LucidCleanPlayerCompilation.cs'
            fixed.unlink()
            fixed.mkdir()
            (fixed / 'inner.cs').write_text('// pr\n')
            self.assertEqual(['Assets/Editor/LucidCleanPlayerCompilation.cs'],
                             native_compilation.overlay_control_reporting_tools(source, control))
            self.assert_overlaid(source, control)

    def test_overlay_rejects_symlinked_reporting_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            source, control = Path(directory) / 'source', Path(directory) / 'control'
            for root in (source, control):
                (root / 'tools').mkdir(parents=True)
                (root / 'Assets/Editor').mkdir(parents=True)
                for name in ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'):
                    (root / 'Assets/Editor' / name).write_text('// trusted helper\n')
            (control / 'tools/helper.py').write_text('trusted = True\n')
            outside = Path(directory) / 'outside.py'
            outside.write_text('untouched\n')
            (source / 'tools/helper.py').symlink_to(outside)
            with self.assertRaisesRegex(ValueError, 'regular files'):
                native_compilation.overlay_control_reporting_tools(source, control)
            self.assertEqual('untouched\n', outside.read_text())

    def test_control_cache_binding_preserves_confinement_and_restores_state(self):
        from lucidlib import bootstrap
        original = bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT, bootstrap.LOCK_FILE
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve() / 'source'
            source.mkdir()
            work = source / '.cache/project-lucid'
            with native_compilation.project_cache_scope(bootstrap, source, work):
                self.assertEqual(work, bootstrap.validate_work_dir(work))
                self.assertEqual(original[0], bootstrap.REPO_ROOT)
                self.assertEqual(original[2], bootstrap.LOCK_FILE)
                host = bootstrap.tool_work_dir(work)
                self.assertEqual(source.parent, host.parent)
                self.assertNotIn(source, host.parents)
                self.assertTrue(host.is_dir())
                with self.assertRaises(ValueError):
                    bootstrap.validate_work_dir(source / 'Assets')
            self.assertEqual(original, (bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT, bootstrap.LOCK_FILE))
            self.assertFalse(host.exists())
            self.assertIsNone(bootstrap.HOST_CACHE_ROOT)

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

    def test_preexisting_contributor_cache_is_rejected_before_host_bootstrap(self):
        from lucidlib import bootstrap
        from unittest import mock
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve() / 'source'
            work = source / '.cache/project-lucid'
            executable = work / 'tools/cpp2il/Cpp2IL'
            executable.parent.mkdir(parents=True)
            executable.write_bytes(b'inert matching-marker contributor executable')
            (executable.parent / 'lucid-build.json').write_text('{"build_schema":2}')
            with mock.patch.object(bootstrap, 'bootstrap') as install:
                with self.assertRaisesRegex(ValueError, 'Fresh project data cache'):
                    with native_compilation.project_cache_scope(bootstrap, source, work):
                        bootstrap.bootstrap(work, ['cpp2il'])
                install.assert_not_called()
            self.assertEqual(b'inert matching-marker contributor executable', executable.read_bytes())
            self.assertIsNone(bootstrap.HOST_CACHE_ROOT)

    def test_scope_restores_state_and_removes_only_its_own_host_tree_on_failure(self):
        from lucidlib import bootstrap
        original = bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT, bootstrap.LOCK_FILE
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve() / 'source'
            source.mkdir()
            sibling = source.parent / 'unrelated-cache'
            sibling.mkdir()
            with self.assertRaisesRegex(RuntimeError, 'fixture failure'):
                with native_compilation.project_cache_scope(bootstrap, source, source / '.cache/project-lucid'):
                    host = bootstrap.HOST_CACHE_ROOT
                    (host / 'fixture').write_bytes(b'inert scratch')
                    raise RuntimeError('fixture failure')
            self.assertFalse(host.exists())
            self.assertTrue(sibling.exists())
            self.assertEqual(original, (bootstrap.REPO_ROOT, bootstrap.CACHE_ROOT, bootstrap.LOCK_FILE))
            self.assertIsNone(bootstrap.HOST_CACHE_ROOT)

    def test_reporting_directory_symlink_is_rejected_before_reading_target(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve() / 'source'
            outside = source.parent / 'outside'
            source.mkdir(); outside.mkdir()
            (outside / 'helper.py').write_bytes(b'inert outside file')
            (source / 'tools').symlink_to(outside, target_is_directory=True)
            with self.assertRaisesRegex(ValueError, 'regular files'):
                native_compilation.reporting_files(source)

    def test_compile_only_refuses_original_input_before_resolving_or_reading_it(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            forbidden = mock.Mock(resolve=mock.Mock(side_effect=AssertionError("Original input must not be read")))
            with self.assertRaisesRegex(ValueError, 'cannot receive original'):
                native_compilation.run(root, root, forbidden, root / '.cache/project-lucid',
                                       'a' * 40, 'b' * 64, 'native@sha256:' + 'c' * 64, 'd' * 40, '123', compile_only=True)
            forbidden.resolve.assert_not_called()

    def test_compile_only_runs_editor_and_receipt_checks_without_bootstrap_or_progress(self):
        from lucidlib import bootstrap, playercode, playerschema, verification, progressreport
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory).resolve(); source = base / 'source'; control = base / 'control'
            for root in (source, control):
                (root / 'tools').mkdir(parents=True); (root / 'tools/helper.py').write_text('trusted=True\n')
                (root / 'Assets/Editor').mkdir(parents=True)
                for name in ('LucidPlayerCompilation.cs', 'LucidCleanPlayerCompilation.cs'):
                    (root / 'Assets/Editor' / name).write_text('// trusted\n')
            engine = [{'name': 'editor', 'sha256': 'c' * 64}]
            with mock.patch.object(bootstrap, 'host_name', return_value='linux-x86_64'), \
                 mock.patch.object(bootstrap, 'bootstrap', side_effect=AssertionError('No native dependency bootstrap for PRs')), \
                 mock.patch.object(verification, 'artifact_fingerprint', return_value='b' * 64), \
                 mock.patch.object(verification, 'find_editor', return_value=Path('/inert/editor')), \
                 mock.patch.object(playercode, '_engine_identity', return_value=engine), \
                 mock.patch.object(playercode, 'run_player_code', return_value={'status': 'compiled'}) as compile, \
                 mock.patch.object(playerschema, '_player_evidence', return_value={'receipt_sha256': 'e' * 64}) as receipt, \
                 mock.patch.object(progressreport, 'generate_progress', side_effect=AssertionError('No PR progress')), \
                 mock.patch.object(native_compilation, 'git_head', side_effect=lambda root: 'a' * 40 if root == source else 'd' * 40), \
                 mock.patch.object(native_compilation, 'isolated_editor', return_value=nullcontext()):
                result = native_compilation.run(source, control, None, source / '.cache/project-lucid',
                                               'a' * 40, 'b' * 64, 'native@sha256:' + 'c' * 64, 'd' * 40, '123', compile_only=True)
            self.assertEqual('e' * 64, result['compiler_receipt_sha256'])
            self.assertNotIn('report_sha256', result)
            self.assertFalse((source / 'docs/code-progress.json').exists())
            compile.assert_called_once()
            receipt.assert_called_once()
            self.assertIsNone(bootstrap.DIAGNOSTICS_ROOT)
            self.assertTrue((base / 'private-native-diagnostics').is_dir())

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

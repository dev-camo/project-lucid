from pathlib import Path
import os
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.scriptmetadata import validate_script_metadata


BASE = "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n"
FULL = BASE + "MonoImporter:\n  externalObjects: {}\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
FOLDER = BASE + "folderAsset: yes\n"


class ScriptMetadataTests(unittest.TestCase):
    def root(self, temporary):
        root = Path(temporary).resolve()
        (root / "Assets").mkdir()
        (root / "Packages").mkdir()
        return root

    def script(self, root, relative="Assets/Example.cs", metadata=FULL):
        script = root / relative
        script.parent.mkdir(parents=True, exist_ok=True)
        script.write_text("// inert source, never compiled\n")
        meta = script.with_name(script.name + ".meta")
        meta.write_text(metadata)
        return script, meta

    def test_full_and_exact_minimal_scripts_in_both_scopes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            self.script(root)
            self.script(root, "Packages/example/Runtime/Minimal.cs", BASE)
            self.script(root, "Assets/Empty.cs", BASE + "MonoImporter: {}\n")
            self.script(root, "Assets/Windows.cs", FULL.replace("\n", "\r\n"))
            self.script(root, "Assets/Historical.cs", BASE + "timeCreated: 1791181930\nlicenseType: Free\nMonoImporter:\n  icon: {fileID: 0}\n")
            result = validate_script_metadata(root)
            self.assertEqual("complete", result["status"], result["errors"])
            self.assertEqual(5, result["counts"]["script_files"])
            self.assertEqual(4, result["counts"]["script_expanded"])
            self.assertEqual(1, result["counts"]["script_minimal"])
            self.assertEqual("NOT_RUN", result["unity_import"])

    def test_full_and_exact_minimal_folders_include_cs_named_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            for name, content in (("Full", FOLDER + "DefaultImporter:\n  userData:\n"), ("Folder.cs", FOLDER)):
                folder = root / "Assets" / name
                folder.mkdir()
                folder.with_name(folder.name + ".meta").write_text(content)
            result = validate_script_metadata(root)
            self.assertEqual("complete", result["status"], result["errors"])
            self.assertEqual(0, result["counts"]["script_files"])
            self.assertEqual(1, result["counts"]["folder_expanded"])
            self.assertEqual(1, result["counts"]["folder_minimal"])

    def test_typo_and_contradictory_headers_do_not_hide_in_a_valid_mapping(self):
        vectors = {
            "typo": BASE + "MonImporter:\n  executionOrder: 0\n",
            "other-importer": BASE + "DefaultImporter:\n",
            "correct-and-typo": FULL + "MonImporter:\n",
            "duplicate-correct": FULL + "MonoImporter:\n",
            "nested-only": BASE + "  MonoImporter:\n",
            "string-only": BASE + 'userData: "MonoImporter:"\n',
            "quoted-key": BASE + '"MonoImporter":\n',
            "inline-scalar": BASE + "MonoImporter: true\n",
            "inline-sequence": BASE + "MonoImporter: []\n",
            "minimal-extra": BASE + "executionOrder: 0\n",
            "minimal-indented-extra": BASE + "  executionOrder: 0\n",
            "historical-without-importer": BASE + "timeCreated: 1791181930\nlicenseType: Free\n",
        }
        for name, content in vectors.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = self.root(temporary)
                self.script(root, metadata=content)
                result = validate_script_metadata(root)
                self.assertEqual("incomplete", result["status"])
                self.assertEqual(1, len(result["errors"]), result["errors"])

    def test_malformed_fields_retain_diagnostics_and_do_not_pass_as_minimal(self):
        vectors = {
            "no-version": BASE.replace("fileFormatVersion: 2\n", ""),
            "wrong-version": BASE.replace("Version: 2", "Version: 3"),
            "bad-guid": BASE.replace("0123456789abcdef0123456789abcdef", "not-a-guid"),
            "duplicate-guid": BASE + "guid: fedcba9876543210fedcba9876543210\n",
            "bad-key": BASE + "Mon Importer:\n",
            "script-as-folder": FOLDER,
        }
        for name, content in vectors.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = self.root(temporary)
                self.script(root, metadata=content)
                result = validate_script_metadata(root)
                self.assertEqual("incomplete", result["status"])
                self.assertIn("Example.cs.meta", result["errors"][0])
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            _, meta = self.script(root)
            meta.write_bytes(b"\xff\xfe")
            self.assertEqual("incomplete", validate_script_metadata(root)["status"])

    def test_folder_header_must_match_the_physical_folder(self):
        for content in (BASE, FOLDER.replace("yes", "no"), FOLDER + "MonoImporter:\n", FOLDER + "userData: value\n"):
            with self.subTest(content=content), tempfile.TemporaryDirectory() as temporary:
                root = self.root(temporary)
                folder = root / "Packages/Folder"
                folder.mkdir()
                folder.with_name(folder.name + ".meta").write_text(content)
                self.assertEqual("incomplete", validate_script_metadata(root)["status"])

    def test_missing_and_orphan_pairs_preserve_all_errors(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            _, meta = self.script(root)
            meta.unlink()
            (root / "Packages/Orphan.cs.meta").write_text(FULL)
            result = validate_script_metadata(root)
            self.assertEqual("incomplete", result["status"])
            self.assertEqual(2, len(result["errors"]))
            self.assertIn("missing physical script metadata pair", result["errors"][0])
            self.assertIn("no physical C# file pair", result["errors"][1])

    def test_prepared_private_and_cache_trees_are_outside_header_scope(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            for relative in ("Assets/Recovered", "Assets/StreamingAssets", "Assets/.private", "Assets/obj", "Packages/example/bin"):
                self.script(root, relative + "/Bad.cs", BASE + "MonImporter:\n")
                folder = root / relative
                folder.with_name(folder.name + ".meta").write_text("not metadata")
            (root / ".agents").mkdir()
            (root / ".agents/Bad.cs.meta").write_text("not metadata")
            (root / "Assets/Other.asset.meta").write_text(BASE + "NativeFormatImporter:\n  mainObjectFileID: 1\n")
            result = validate_script_metadata(root)
            self.assertEqual("complete", result["status"], result["errors"])
            self.assertEqual(0, result["counts"]["script_files"])
            self.assertEqual(1, result["counts"]["other_metadata_not_checked"])

    def test_unity_ignored_paths_do_not_require_import_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            self.script(root, "Packages/example/Runtime/Kept.cs")
            for relative in ("Packages/example/Samples~", "Packages/example/Documentation~", "Assets/Archived~", "Assets/cvs"):
                folder = root / relative
                folder.mkdir(parents=True)
                (folder / "NoMetadata.cs").write_text("// ignored inert source\n")
                folder.with_name(folder.name + ".meta").write_text("not tracked importer metadata")
            (root / "Assets/.Hidden.cs").write_text("// ignored inert source\n")
            result = validate_script_metadata(root)
            self.assertEqual("complete", result["status"], result["errors"])
            self.assertEqual(1, result["counts"]["script_files"])
            self.assertEqual(1, result["counts"]["script_expanded"])

    def test_symlink_root_scope_directory_script_and_meta_are_rejected(self):
        for kind in ("root", "scope", "directory", "script", "meta"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                parent = Path(temporary).resolve()
                root = parent / "project"
                root.mkdir()
                self.root(root)
                script, meta = self.script(root)
                if kind == "root":
                    link = parent / "link"
                    link.symlink_to(root, target_is_directory=True)
                    root = link
                elif kind == "scope":
                    (root / "Packages").rmdir()
                    (root / "Packages").symlink_to(root / "Assets", target_is_directory=True)
                elif kind == "directory":
                    (root / "Assets/Linked").symlink_to(root / "Packages", target_is_directory=True)
                else:
                    path = script if kind == "script" else meta
                    target = parent / "borrowed"
                    target.write_bytes(path.read_bytes())
                    path.unlink()
                    path.symlink_to(target)
                result = validate_script_metadata(root)
                self.assertEqual("incomplete", result["status"])
                self.assertTrue(any("symbolic-link" in error for error in result["errors"]))

    def test_scope_missing_or_non_directory_and_parent_traversal_reject(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = self.root(temporary)
            (root / "Packages").rmdir()
            self.assertEqual("incomplete", validate_script_metadata(root)["status"])
            (root / "Packages").write_text("not a directory")
            self.assertEqual("incomplete", validate_script_metadata(root)["status"])
            self.assertEqual("incomplete", validate_script_metadata(root / ".." / root.name)["status"])

    @unittest.skipUnless(hasattr(os, "mkfifo"), "named pipe creation is unavailable on this host")
    def test_special_script_and_metadata_paths_are_rejected_without_reading(self):
        for kind in ("script", "meta"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                root = self.root(temporary)
                script, meta = self.script(root)
                path = script if kind == "script" else meta
                path.unlink()
                os.mkfifo(path)
                result = validate_script_metadata(root)
                self.assertEqual("incomplete", result["status"])
                self.assertTrue(any("physical regular file" in error for error in result["errors"]))


if __name__ == "__main__":
    unittest.main()

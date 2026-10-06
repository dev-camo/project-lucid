import subprocess
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.progressgit import require_matching_source_index


class ProgressIndexTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="lucid progress ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git("init", "-q")
        # Disposable repositories must not spawn maintenance that can race cleanup.
        self.git("config", "maintenance.auto", "false")
        self.git("config", "gc.auto", "0")
        self.git("config", "user.name", "Progress test")
        self.git("config", "user.email", "test@example.invalid")
        (self.root / "Assets").mkdir()
        (self.root / "Assets" / "Source with spaces.cs").write_text("first\n")
        self.git("add", "Assets")
        self.git("commit", "-qm", "Initial")

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.root, check=True,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE)

    def test_clean_index(self):
        require_matching_source_index(self.root)

    def test_staged_change(self):
        (self.root / "Assets" / "Source with spaces.cs").write_text("second\n")
        self.git("add", "Assets")
        require_matching_source_index(self.root)

    def test_unstaged_change_is_rejected(self):
        (self.root / "Assets" / "Source with spaces.cs").write_text("second\n")
        with self.assertRaisesRegex(ValueError, "Stage all maintained source"):
            require_matching_source_index(self.root)

    def test_partial_staging_is_rejected(self):
        path = self.root / "Assets" / "Source with spaces.cs"
        path.write_text("second\n")
        self.git("add", "Assets")
        path.write_text("third\n")
        with self.assertRaises(ValueError):
            require_matching_source_index(self.root)

    def test_untracked_source_is_rejected(self):
        (self.root / "Assets" / "Other.cs").write_text("new\n")
        with self.assertRaisesRegex(ValueError, "Stage all maintained source"):
            require_matching_source_index(self.root)

    def test_staged_rename_with_spaces(self):
        self.git("mv", "Assets/Source with spaces.cs", "Assets/Renamed source.cs")
        require_matching_source_index(self.root)

    def test_staged_deletion(self):
        self.git("rm", "Assets/Source with spaces.cs")
        require_matching_source_index(self.root)

    def test_document_edit_does_not_change_source(self):
        (self.root / "README.md").write_text("user documentation\n")
        require_matching_source_index(self.root)

    def test_ignored_source_is_rejected(self):
        (self.root / ".gitignore").write_text("*.csproj\n")
        (self.root / "tools").mkdir()
        (self.root / "tools" / "local.csproj").write_text("local source\n")
        with self.assertRaisesRegex(ValueError, "ignored local source"):
            require_matching_source_index(self.root)

    def test_excluded_generated_files_do_not_change_identity(self):
        for relative in ("tools/bin", "tools/obj", "Assets/Recovered", "Assets/StreamingAssets"):
            path = self.root / relative
            path.mkdir(parents=True)
            (path / "generated.cs").write_text("generated\n")
        require_matching_source_index(self.root)


if __name__ == "__main__":
    unittest.main()

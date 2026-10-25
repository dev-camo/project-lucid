"""Compare maintained C# hashing with Python and exercise the audit hash boundary.

Reference scanning and Editor launch remain simulated; C# hashing is executed.
Build ArtifactIdentity.csproj first, then pass its DLL and the dotnet executable.
"""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from test_audit_handshake import AuditHandshakeTests
from lucidlib import verification


def csharp_identity(root, prepared):
    result = subprocess.run(
        [DOTNET, str(ASSEMBLY), "--fingerprint", str(root), "prepared" if prepared else "source"],
        check=True, capture_output=True, text=True, timeout=30)
    value = result.stdout.strip()
    if len(value) != 64 or any(char not in "0123456789abcdef" for char in value):
        raise AssertionError("C# runner did not return one SHA256 digest: " + repr(result.stdout))
    return value


class MaintainedIdentityInteropTests(unittest.TestCase):
    def test_same_content_trees_have_equal_cross_language_identities(self):
        for prepared, scope in ((False, "Assets/Scripts"), (True, "Assets/Recovered")):
            with self.subTest(prepared=prepared), tempfile.TemporaryDirectory(prefix="Lucid interop ") as temporary:
                root = Path(temporary)
                parent = root / scope
                (parent / "literal").mkdir(parents=True)
                nested = parent / "literal/name.asset"
                literal = parent / "literal\\name.asset"
                nested.write_bytes(b"payload")
                nested_hash = csharp_identity(root, prepared)
                self.assertEqual(verification.artifact_fingerprint(root, prepared), nested_hash)
                nested.unlink()
                literal.write_bytes(b"payload")
                literal_hash = csharp_identity(root, prepared)
                self.assertEqual(verification.artifact_fingerprint(root, prepared), literal_hash)
                if os.sep == "/":
                    self.assertNotEqual(nested_hash, literal_hash)
                    nested.write_bytes(b"payload")
                    together = csharp_identity(root, prepared)
                    self.assertEqual(verification.artifact_fingerprint(root, prepared), together)
                    self.assertNotIn(together, (nested_hash, literal_hash))
                else:
                    self.assertEqual(nested_hash, literal_hash)


class MaintainedIdentityFallbackTests(AuditHandshakeTests):
    def setUp(self):
        super().setUp()
        # Retain the orchestration fixture's assertions, replacing only its
        # synthetic report identity with output from the maintained C# source.
        def identity(report, context, pending):
            actual = {"source_fingerprint": csharp_identity(self.root, False),
                      "prepared_asset_fingerprint": csharp_identity(self.root, True)}
            if context is None:
                report.update(actual)
            else:
                for key, value in actual.items():
                    self.assertEqual(report[key], value)
        self.action = identity


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("assembly", type=Path)
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    ASSEMBLY, DOTNET = args.assembly.resolve(), args.dotnet
    suite = unittest.TestSuite((
        MaintainedIdentityInteropTests("test_same_content_trees_have_equal_cross_language_identities"),
        MaintainedIdentityFallbackTests("test_non_bmp_and_literal_backslash_paths_use_direct_hash_fallback"),
    ))
    sys.exit(not unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful())

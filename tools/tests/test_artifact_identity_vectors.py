"""Exact shared path/content vectors for the Python and C# identity implementations."""
from pathlib import Path
import os
import tempfile
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.verification import artifact_fingerprint


class ArtifactIdentityVectorTests(unittest.TestCase):
    def test_shared_fingerprint_orders_bmp_and_supplementary_paths_as_utf16(self):
        # The hosted C# runner checks these same fixed digests. U+E000 sorts
        # after supplementary characters in UTF16, unlike Python's default.
        rows = (
            ('\ue000.cs', 'high BMP path\n'),
            ('ascii.cs', 'ASCII path\n'),
            ('\U0001f680.cs', 'rocket path\n'),
            ('\U00010000.cs', 'supplementary path\n'),
        )
        vectors = (
            (False, 'Assets/Scripts', '450c68cbceb82d4d16bdf02dfa6956332f8d856a4cb15e5f4218c77a2f7616c1'),
            (True, 'Assets/Recovered', 'ab5b67e4566261ce27053dddd489a55bef1ec98ea4effc21ad4a69ecd1d284eb'),
        )
        for prepared, scope, expected in vectors:
            for creation_order in (rows, tuple(reversed(rows))):
                with self.subTest(prepared=prepared, creation_order=creation_order):
                    with tempfile.TemporaryDirectory() as temporary:
                        root = Path(temporary)
                        (root / scope).mkdir(parents=True)
                        for name, content in creation_order:
                            (root / scope / name).write_bytes(content.encode('utf8'))
                        self.assertEqual(expected, artifact_fingerprint(root, prepared))

    def test_shared_fingerprint_includes_all_prepared_asset_directories(self):
        source_rows = {
            'Assets/Scripts/fixture.cs': 'maintained source\n',
            'Packages/com.example.fixture/Runtime/fixture.cs': 'maintained package\n',
            'ProjectSettings/fixture.asset': 'project settings\n',
            'tools/fixture.py': 'public tooling\n',
        }
        prepared_rows = {
            'Assets/Recovered/__pycache__/model.bundle': 'Assets/Recovered:__pycache__\n',
            'Assets/Recovered/__pycache__/model.bundle.meta': 'asset meta\n',
            'Assets/Recovered/bin/model.bundle': 'Assets/Recovered:bin\n',
            'Assets/Recovered/bin/model.bundle.meta': 'asset meta\n',
            'Assets/Recovered/mesh-é-λ.bundle': 'unicode content é λ\n',
            'Assets/Recovered/obj/model.bundle': 'Assets/Recovered:obj\n',
            'Assets/Recovered/obj/model.bundle.meta': 'asset meta\n',
            'Assets/StreamingAssets/__pycache__/model.bundle': 'Assets/StreamingAssets:__pycache__\n',
            'Assets/StreamingAssets/__pycache__/model.bundle.meta': 'asset meta\n',
            'Assets/StreamingAssets/bin/model.bundle': 'Assets/StreamingAssets:bin\n',
            'Assets/StreamingAssets/bin/model.bundle.meta': 'asset meta\n',
            'Assets/StreamingAssets/obj/model.bundle': 'Assets/StreamingAssets:obj\n',
            'Assets/StreamingAssets/obj/model.bundle.meta': 'asset meta\n',
        }
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            def write(relative, value):
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(value.encode("utf8"))
            for relative, value in {**source_rows, **prepared_rows}.items():
                write(relative, value)
            source = 'f37b79d175d61b5da2cecd9cdb5317e6cd3f766aa3d15216fe1ff22c81f5e005'
            prepared = '373ae7371342069cd66210e0a7359def1e129d54885a32ab31d2ad42160c6106'
            self.assertEqual(source, artifact_fingerprint(root))
            self.assertEqual(prepared, artifact_fingerprint(root, True))
            for scope in ("Assets", "Packages", "ProjectSettings", "tools"):
                for folder in ("obj", "bin", "__pycache__"):
                    write(scope + "/" + folder + "/temporary.cs", "build cache\n")
            self.assertEqual(source, artifact_fingerprint(root))
            self.assertEqual(prepared, artifact_fingerprint(root, True))
            for scope in ("Assets/Recovered", "Assets/StreamingAssets"):
                for folder in ("obj", "bin", "__pycache__"):
                    relative = scope + "/" + folder + "/model.bundle"
                    write(relative, "changed bundle\n")
                    self.assertNotEqual(prepared, artifact_fingerprint(root, True))
                    self.assertEqual(source, artifact_fingerprint(root))
                    write(relative, prepared_rows[relative])
                    write(relative + ".meta", "changed meta\n")
                    self.assertNotEqual(prepared, artifact_fingerprint(root, True))
                    write(relative + ".meta", prepared_rows[relative + ".meta"])
            self.assertEqual(prepared, artifact_fingerprint(root, True))

    def test_shared_fingerprint_preserves_host_filename_characters(self):
        # Fixed independent SHA256 rows: UTF8(path + NUL + SHA256(b"payload") + LF).
        vectors = (
            (False, "Assets/Scripts",
             "25c60721cacf2e91396e62b5fb27f2b9ba7555cfe3057efebbb6d8b883e04385",
             "3a632fd7752dbac934e3ff2b3a1d127f05f884b08b2bf599c8ed3a7a778863a9",
             "d7cc3ab2b63753726490df5bba7a5bb319867ae384c1f69958299798e9f2c25c"),
            (True, "Assets/Recovered",
             "acc6343bf6cc30e1844ed8440fb6323e97de7c981f2ccc2f3491baaa95ac25a4",
             "ca2bfe19ae0c37c5b66d71aa0ab32d9f29189c96bd58c22efa0c4e1c39ab0f65",
             "0d4a433ccac0e2270cc780adb4ac7a8958d379c457171c65f9728fb71c4f7648"),
        )
        for prepared, scope, nested, literal, together in vectors:
            with self.subTest(prepared=prepared), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                parent = root / scope
                (parent / "literal").mkdir(parents=True)
                nested_path = parent / "literal/name.asset"
                literal_path = parent / "literal\\name.asset"
                nested_path.write_bytes(b"payload")
                self.assertEqual(nested, artifact_fingerprint(root, prepared))
                nested_path.unlink()
                literal_path.write_bytes(b"payload")
                self.assertEqual(literal if os.sep == "/" else nested, artifact_fingerprint(root, prepared))
                if os.sep == "/":
                    self.assertNotEqual(literal, nested)
                    nested_path.write_bytes(b"payload")
                    self.assertEqual(together, artifact_fingerprint(root, prepared))

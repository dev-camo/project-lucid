"""The refusals of tools/assetripper_identity_table.py build and roundtrip, on synthetic inputs (no export is read)."""

import argparse
import contextlib
import hashlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from tools import assetripper_identity_table as command


class Refusals(unittest.TestCase):
    def setUp(self):
        self.directory = Path(tempfile.mkdtemp())
        self.addCleanup(__import__("shutil").rmtree, self.directory, True)
        self.run_dir = self.directory / "run"
        self.run_dir.mkdir()
        manifest = b'{"files": []}\n'
        (self.run_dir / "input-manifest.json").write_bytes(manifest)
        self.pinned = hashlib.sha256(manifest).hexdigest()
        patcher = mock.patch.object(command, "INPUT_MANIFEST_SHA256", self.pinned)
        patcher.start()
        self.addCleanup(patcher.stop)

    def arguments(self, **extra):
        values = dict(witness=str(self.directory / "w"), baseline=str(self.run_dir), app=str(self.directory / "app"),
                      out=str(self.directory / "table.txt"), report=None, expect=None, stage=str(self.directory / "s"),
                      table=str(self.directory / "t"))
        values.update(extra)
        return argparse.Namespace(**values)

    def call(self, function, args, **patches):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            with contextlib.ExitStack() as stack:
                for name, value in patches.items():
                    stack.enter_context(mock.patch.object(command, name, value))
                code = function(args)
        return code, output.getvalue()

    def test_the_pinned_baseline_run_is_accepted(self):
        self.assertIsNone(command.require_pinned_baseline(self.run_dir))

    def test_a_baseline_run_with_another_input_manifest_is_refused(self):
        (self.run_dir / "input-manifest.json").write_bytes(b'{"files": ["other"]}\n')
        self.assertIn("pinned one", command.require_pinned_baseline(self.run_dir))

    def test_a_baseline_run_without_an_input_manifest_is_refused(self):
        (self.run_dir / "input-manifest.json").unlink()
        self.assertIn("no input-manifest.json", command.require_pinned_baseline(self.run_dir))

    def test_build_refuses_a_baseline_that_is_not_the_pinned_run_before_reading_anything_else(self):
        (self.run_dir / "input-manifest.json").write_bytes(b"not the pinned manifest\n")
        boom = mock.Mock(side_effect=AssertionError("read past the refusal"))
        code, text = self.call(command.build, self.arguments(), input_manifest_sha256=boom)
        self.assertEqual(1, code)
        self.assertIn("REFUSED: the baseline run's input manifest", text)
        boom.assert_not_called()

    def test_roundtrip_refuses_a_baseline_that_is_not_the_pinned_run(self):
        (self.run_dir / "input-manifest.json").write_bytes(b"not the pinned manifest\n")
        with mock.patch.object(command.identitymap, "build_baseline_project", side_effect=AssertionError("ran")):
            code, text = self.call(command.roundtrip, self.arguments())
        self.assertEqual(1, code)
        self.assertIn("REFUSED", text)

    def test_build_refuses_an_input_whose_manifest_is_not_the_pinned_one(self):
        compare = mock.Mock(side_effect=AssertionError("compared a witness of the wrong input"))
        with mock.patch.object(command.compare, "compare", compare):
            code, text = self.call(command.build, self.arguments(), input_manifest_sha256=lambda app: "0" * 64)
        self.assertEqual(1, code)
        self.assertIn("REFUSED: the input manifest is " + "0" * 64, text)
        compare.assert_not_called()

    def test_build_refuses_a_witness_that_differs_from_the_baseline(self):
        result = {"ok": False, "counts": {}, "extra_in_patched": ["x"], "missing_in_patched": [],
                  "other_differences": ["y"], "other_difference_count": 1}
        load = mock.Mock(side_effect=AssertionError("loaded the identity of a witness that differs"))
        with mock.patch.object(command.compare, "compare", return_value=result), \
                mock.patch.object(command.exportidentity, "load_identity", load):
            code, text = self.call(command.build, self.arguments(), input_manifest_sha256=lambda app: self.pinned)
        self.assertEqual(1, code)
        self.assertIn("REFUSED: the witness differs from the baseline", text)
        load.assert_not_called()
        self.assertFalse((self.directory / "table.txt").exists())


if __name__ == "__main__":
    unittest.main()

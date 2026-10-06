import importlib.util
import io
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import runtimecontracts


class ContractCliTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location(
            "lucid_contract_cli_test", Path(__file__).resolve().parents[1] / "lucid.py")
        self.cli = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.cli)
        self.paths = ["--schema", "/cache with spaces/original.json",
                      "--inventory", "/cache with spaces/loaded.json"]

    def test_writer_requires_explicit_schema_inventory_and_target(self):
        for args in (["runtime-contracts"], ["runtime-contracts", *self.paths]):
            with self.subTest(args=args), redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as failure:
                self.cli.parser().parse_args(args)
            self.assertEqual(failure.exception.code, 2)

    def test_writer_dispatch_preserves_paths_and_target(self):
        report = {"status": "metadata-ready", "runtime_verified": False}
        with patch("lucidlib.bootstrap.validate_work_dir", return_value=Path("/owned/cache")), \
                patch.object(runtimecontracts, "run_runtime_contracts", return_value=report) as run, \
                patch.object(self.cli, "write_report"), redirect_stdout(io.StringIO()):
            self.assertEqual(0, self.cli.main(["runtime-contracts", *self.paths, "--target", "linux", "--json"]))
        self.assertEqual(run.call_args.args, (self.cli.ROOT, Path("/owned/cache"), "linux",
                                             Path(self.paths[1]), Path(self.paths[3])))

    def test_comparison_dispatch_retains_all_target_validation(self):
        report = {"status": "evidence-ready-for-root-review", "owners_verified": False}
        with patch("lucidlib.bootstrap.validate_work_dir", return_value=Path("/owned/cache")), \
                patch.object(runtimecontracts, "compare_verified_theme_planes", return_value=report) as run, \
                patch.object(self.cli, "write_report"), redirect_stdout(io.StringIO()):
            self.assertEqual(0, self.cli.main(["theme-evidence", *self.paths, "--json"]))
        self.assertEqual(run.call_args.args, (self.cli.ROOT, Path("/owned/cache"),
                                             Path(self.paths[1]), Path(self.paths[3])))

    def test_failed_contract_evidence_returns_nonzero_without_publication(self):
        with patch("lucidlib.bootstrap.validate_work_dir", return_value=Path("/owned/cache")), \
                patch.object(runtimecontracts, "run_runtime_contracts", side_effect=ValueError("stale source")), \
                patch.object(self.cli, "write_report") as publish, redirect_stderr(io.StringIO()):
            self.assertEqual(1, self.cli.main(["runtime-contracts", *self.paths, "--target", "macos"]))
        publish.assert_not_called()


if __name__ == "__main__":
    unittest.main()

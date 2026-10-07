import importlib.util
import io
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import port


class PortCliTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location(
            "lucid_port_cli_test", Path(__file__).resolve().parents[1] / "lucid.py")
        self.cli = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.cli)

    def test_port_defaults_follow_the_workspace_and_require_a_target(self):
        with redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as failure:
            self.cli.parser().parse_args(["port"])
        self.assertEqual(2, failure.exception.code)
        args = self.cli.parser().parse_args(["port", "--target", "macos"])
        self.assertEqual(self.cli.ROOT / "input", args.input)
        self.assertEqual(self.cli.ROOT / "output", args.output)

    def test_port_dispatch_preserves_paths_spaces_target_and_nonzero_exit(self):
        for status, code in (("failed", 1), ("interrupted", 130), ("complete", 0)):
            with self.subTest(status=status), \
                    patch("lucidlib.bootstrap.validate_work_dir", return_value=Path("/owned/cache")), \
                    patch.object(port, "run_port", return_value={"status": status, "exit_code": code}) as run, \
                    patch.object(self.cli, "write_report"), redirect_stdout(io.StringIO()):
                observed = self.cli.main(["port", "--target", "windows", "--input", "/game with spaces/input",
                                          "--output", "/output with spaces/player", "--json"])
            self.assertEqual(code, observed)
            self.assertEqual((self.cli.ROOT, Path("/owned/cache"), Path("/game with spaces/input"),
                              "windows", Path("/output with spaces/player")), run.call_args.args)

    def test_missing_input_is_a_real_failed_preflight_without_tools_or_output(self):
        with patch("lucidlib.bootstrap.validate_work_dir", return_value=Path("/owned/cache")), \
                patch.object(self.cli, "write_report"), patch.object(port.bootstrap, "bootstrap") as bootstrap, \
                redirect_stdout(io.StringIO()) as output:
            code = self.cli.main(["port", "--target", "linux", "--input", "/nonexistent/lucid-fixture", "--json"])
        self.assertEqual(1, code)
        self.assertIn('"failed_stage": "preflight"', output.getvalue())
        bootstrap.assert_not_called()


if __name__ == "__main__":
    unittest.main()

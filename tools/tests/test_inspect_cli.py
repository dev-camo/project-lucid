"""Inspection CLI controls using real inert catalogs, without game inputs."""
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap, inspection


class InspectCliTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location(
            "lucid_inspect_cli_test", Path(__file__).resolve().parents[1] / "lucid.py")
        self.cli = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.cli)
        self.temporary = tempfile.TemporaryDirectory(prefix="lucid inspect CLI ")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.cache = self.root / "cache"
        self.app = self.root / "Fixture.app"
        self.app.mkdir()
        self.catalog = self.app / "catalog.json"
        self.settings = self.app / "settings.json"
        self.settings.write_text("{}", encoding="utf-8")

    def run_inspect(self, ids, compact=True):
        self.catalog.write_text(json.dumps({"m_InternalIds": ids}), encoding="utf-8")
        addressables = inspection.inspect_catalog(self.catalog, self.settings, self.app)
        report = {"identity": {"version": "synthetic", "unity_version": "synthetic"},
                  "addressables": addressables}
        arguments = ["inspect", "--input", str(self.app), "--work-dir", str(self.cache)]
        if compact:
            arguments.append("--json")
        with patch.object(bootstrap, "CACHE_ROOT", self.cache), \
                patch.object(inspection, "inspect_bundle", return_value=report) as bundle, \
                redirect_stdout(io.StringIO()) as output:
            code = self.cli.main(arguments)
        bundle.assert_called_once_with(self.app)
        saved = self.cache / "reports/inspection.json"
        self.assertEqual(report, json.loads(saved.read_text(encoding="utf-8")))
        return code, output.getvalue(), addressables

    def test_json_status_uses_all_catalog_completeness_failures(self):
        for ids, missing, remote, unmapped in (
            (["https://example.invalid/remote.bundle"], 0, 1, 0),
            (["{UnmappedRoot}/unknown.bundle"], 0, 0, 1),
            (["missing.bundle"], 1, 0, 0),
            (["missing.bundle", "https://example.invalid/remote.bundle",
              "{UnmappedRoot}/unknown.bundle"], 1, 1, 1),
        ):
            with self.subTest(ids=ids):
                code, output, actual = self.run_inspect(ids)
                self.assertFalse(actual["complete"])
                summary = json.loads(output)
                self.assertEqual(1, code)
                self.assertEqual("failed", summary["status"])
                self.assertEqual(missing, summary["bundles_missing"])
                self.assertEqual(remote, summary["bundle_locations_remote"])
                self.assertEqual(unmapped, summary["bundle_locations_unmapped"])

    def test_complete_local_catalog_keeps_success_and_full_report(self):
        (self.app / "local.bundle").write_bytes(b"inert bundle")
        code, output, actual = self.run_inspect(["local.bundle"])
        self.assertTrue(actual["complete"])
        summary = json.loads(output)
        self.assertEqual(0, code)
        self.assertEqual("complete", summary["status"])
        self.assertEqual(1, summary["bundles_present"])
        self.assertEqual(0, summary["bundles_missing"])
        self.assertEqual(0, summary["bundle_locations_remote"])
        self.assertEqual(0, summary["bundle_locations_unmapped"])

    def test_human_summary_exposes_incomplete_remote_and_unmapped_locations(self):
        code, output, actual = self.run_inspect(
            ["https://example.invalid/remote.bundle", "{UnmappedRoot}/unknown.bundle"], compact=False)
        self.assertFalse(actual["complete"])
        self.assertEqual(1, code)
        self.assertIn("inspection: failed", output)
        self.assertIn("1 remote locations", output)
        self.assertIn("1 unmapped locations", output)


if __name__ == "__main__":
    unittest.main()

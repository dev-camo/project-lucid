"""Safety boundaries and conservative code-recovery reporting regressions."""

import hashlib
import io
import json
from pathlib import Path
import shutil
import stat
import sys
import tarfile
import tempfile
import unittest
from unittest import mock
import zipfile

from tools.lucidlib.bootstrap import (CACHE_ROOT, REPO_ROOT, ToolError, bootstrap,
                                   download_artifact, managed_path, run_logged,
                                   unpack_archive, validate_work_dir)
from tools.lucidlib.recovery import _managed_files, classify_il, recover_code


class RecoverySafetyTests(unittest.TestCase):
    def setUp(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix="test-recovery-", dir=CACHE_ROOT))

    def tearDown(self):
        shutil.rmtree(self.work)

    def _tar(self, members):
        output = self.work / "fixture.tar"
        with tarfile.open(output, "w") as archive:
            for name, content, link in members:
                member = tarfile.TarInfo(name)
                if link is not None:
                    member.type = tarfile.SYMTYPE
                    member.linkname = link
                    archive.addfile(member)
                else:
                    member.size = len(content)
                    archive.addfile(member, io.BytesIO(content))
        return output

    def test_repo_and_input_cannot_be_work_directories(self):
        for path in (REPO_ROOT, REPO_ROOT / "input", REPO_ROOT / "Assets", self.work / ".." / ".." / "escape"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                validate_work_dir(path)

    def test_existing_symlink_cannot_redirect_generated_output(self):
        (self.work / "tools").symlink_to(REPO_ROOT / "input", target_is_directory=True)
        with self.assertRaises(ValueError):
            managed_path(self.work, "tools", "replacement.dll")
        with self.assertRaises(ValueError):
            validate_work_dir(self.work / "tools" / "nested")

    def test_selected_tool_names_are_validated_before_any_download(self):
        for selected in ([], ["unknown"], "../input"):
            with self.subTest(selected=selected), mock.patch("tools.lucidlib.bootstrap.download_artifact") as download:
                with self.assertRaises(ValueError):
                    bootstrap(self.work, selected)
                download.assert_not_called()

    def test_traversal_is_rejected_before_stripping_archive_root(self):
        archive = self._tar([("source/../escape.txt", b"bad", None)])
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "tar", "strip_components": 1})
        self.assertFalse(destination.exists())
        self.assertFalse((self.work / "escape.txt").exists())

    def test_tar_link_cannot_redirect_later_member(self):
        archive = self._tar([("tools/link", b"", str(REPO_ROOT / "input")),
                             ("tools/link/overwrite", b"bad", None)])
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "tar"})
        self.assertFalse(destination.exists())

    def test_zip_link_is_rejected(self):
        archive = self.work / "fixture.zip"
        with zipfile.ZipFile(archive, "w") as source:
            member = zipfile.ZipInfo("redirect")
            member.create_system = 3
            member.external_attr = (stat.S_IFLNK | 0o777) << 16
            source.writestr(member, "../../input")
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "zip"})
        self.assertFalse(destination.exists())

    def test_archive_unpacked_size_limit_is_cumulative(self):
        archive = self._tar([("one", b"1234", None), ("two", b"5678", None)])
        destination = self.work / "stage"
        with self.assertRaises(ToolError):
            unpack_archive(archive, destination, {"format": "tar", "max_unpacked_bytes": 6})
        self.assertFalse(destination.exists())

    def test_failed_download_checksum_never_promotes_artifact(self):
        artifact = {"filename": "tool.bin", "url": "https://example.invalid/pinned-tool",
                    "sha256": hashlib.sha256(b"expected").hexdigest()}
        with mock.patch("urllib.request.urlopen", return_value=io.BytesIO(b"unexpected")):
            with self.assertRaises(ToolError):
                download_artifact(artifact, self.work)
        self.assertFalse((self.work / "downloads/tool.bin").exists())
        self.assertEqual(list((self.work / "downloads").glob("*.part")), [])

    def test_verified_download_is_reused_without_network(self):
        payload = b"expected"
        artifact = {"filename": "tool.bin", "url": "https://example.invalid/pinned-tool",
                    "sha256": hashlib.sha256(payload).hexdigest()}
        with mock.patch("urllib.request.urlopen", return_value=io.BytesIO(payload)):
            downloaded = download_artifact(artifact, self.work)
        with mock.patch("urllib.request.urlopen") as network:
            self.assertEqual(download_artifact(artifact, self.work), downloaded)
            network.assert_not_called()

    def test_timeout_stops_process_and_retains_diagnostic(self):
        result = run_logged([sys.executable, "-c", "import time; time.sleep(60)"], self.work,
                            "timeout.log", timeout=1)
        self.assertTrue(result["timed_out"])
        self.assertNotEqual(result["returncode"], 0)
        self.assertIn("stopped the process", Path(result["log"]).read_text())

    def test_schema_fallback_cannot_satisfy_body_request(self):
        app = self.work.parent / "source-recovery-fixture.app"
        binary, metadata = app / "binary", app / "metadata"
        empty = {"image_count": 1, "type_count": 1, "method_count": 1, "field_count": 1,
                 "version": 31, "images": [{"name": "Game.Runtime.dll"}]}
        failed = {"tool": "cpp2il", "status": "failed", "assembly_files": []}
        fallback = {"tool": "dumper", "status": "ready", "output_dir": str(self.work / "fallback"),
                    "assembly_files": ["Game.Runtime.dll"], "process": {"log": "fallback.log"}}
        with mock.patch("tools.lucidlib.recovery._resolve_input", return_value=(app, binary, metadata, "2022.3.54f1")), \
             mock.patch("tools.lucidlib.recovery.parse_metadata", return_value=empty), \
             mock.patch("tools.lucidlib.recovery.sha256_file", return_value="source-hash"), \
             mock.patch("tools.lucidlib.recovery._thin_binary", return_value=(binary, {"name": "x86_64"})), \
             mock.patch("tools.lucidlib.recovery._cpp2il", return_value=failed), \
             mock.patch("tools.lucidlib.recovery._dumper", return_value=fallback):
            report = recover_code(app, self.work, mode="bodies")
        self.assertEqual(report["status"], "failed")
        self.assertFalse(report["playable_game"])
        self.assertEqual(report["verified_gameplay_methods"], 0)
        self.assertIn("declaration-only", report["limitations"][-1])

    def test_extensionless_generated_image_is_not_falsely_missing(self):
        app = self.work.parent / "source-recovery-fixture.app"
        binary, metadata = app / "binary", app / "metadata"
        fixture = {"image_count": 1, "type_count": 1, "method_count": 1, "field_count": 1,
                   "version": 31, "images": [{"name": "__Generated"}]}
        succeeded = {"tool": "cpp2il", "status": "ready", "output_dir": str(self.work),
                     "assembly_files": [str(self.work / "__Generated.dll")], "process": {"log": "schema.log"}}
        quality = {"assemblies": [{"types": 1, "methods": 1}], "counts": {"schema_placeholder": 1}}
        with mock.patch("tools.lucidlib.recovery._resolve_input", return_value=(app, binary, metadata, "2022.3.54f1")), \
             mock.patch("tools.lucidlib.recovery.parse_metadata", return_value=fixture), \
             mock.patch("tools.lucidlib.recovery.sha256_file", return_value="source-hash"), \
             mock.patch("tools.lucidlib.recovery._thin_binary", return_value=(binary, {"name": "x86_64"})), \
             mock.patch("tools.lucidlib.recovery._cpp2il", return_value=succeeded), \
             mock.patch("tools.lucidlib.recovery._inspect_managed_output", return_value=quality) as inspect:
            report = recover_code(app, self.work)
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["missing_assemblies"], [])
        self.assertTrue(inspect.call_args.kwargs["schemas"])

    def test_extensionless_managed_assembly_is_included_in_output_coverage(self):
        generated = self.work / "__Generated"
        generated.write_bytes(b"MZ" + b"\0" * 30)
        normal = self.work / "Game.Runtime.dll"
        normal.write_bytes(b"MZ" + b"\0" * 30)
        (self.work / "script.json").write_text('{"addresses": []}')
        self.assertEqual(_managed_files(self.work), sorted([generated, normal]))


class IlQualityTests(unittest.TestCase):
    def test_throwing_and_default_bodies_are_never_verified(self):
        cases = [b"\x14\x7a", b"\x72\1\0\0\x70\x73\1\0\0\x0a\x7a", b"\x16\x2a",
                 b"\x02\x28\1\0\0\x0a\x2a", b"\x12\0\xfe\x15\1\0\0\x02\x06\x2a"]
        for body in cases:
            with self.subTest(body=body):
                self.assertIn(classify_il(body), ("throwing_stub_or_trivial_throw", "trivial_or_default_body"))

    def test_nontrivial_il_remains_provisional(self):
        self.assertEqual(classify_il(b"\x02\x03\x58\x2a"), "provisional_il_body")
        self.assertEqual(classify_il(b"", has_body=False), "declaration_only")


if __name__ == "__main__":
    unittest.main()

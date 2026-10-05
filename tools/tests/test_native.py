"""Atomic native evidence runs and conservative provenance/selection checks."""

import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest import mock

from tools.lucidlib import native
from tools.lucidlib.bootstrap import CACHE_ROOT, ToolError, load_lock


class NativeHelpersTests(unittest.TestCase):
    def setUp(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.directory = Path(tempfile.mkdtemp(prefix="test-native-", dir=CACHE_ROOT))
        self.work = self.directory / "work"
        self.work.mkdir()
        self.app = self.directory / "original.app"
        self.app.mkdir()
        self.binary, self.metadata_file = self.app / "binary", self.app / "metadata"
        self.binary.write_bytes(b"original native fixture")
        self.metadata_file.write_bytes(b"original metadata fixture")
        self.inventory = {"version": 31, "images": [{"assembly_name": "Game.Runtime", "name": "Game.Runtime.dll"}],
                          "methods": [{"assembly": "Game.Runtime.dll", "token": "0x060039b9"}]}
        self.lock = load_lock()
        self.project_dir = self.directory / "harness"
        self.project_dir.mkdir()
        self.project = self.project_dir / "NativeRecovery.csproj"
        self.project.write_text("fixture project")
        (self.project_dir / "Program.cs").write_text("fixture harness")
        self.source = self.work / "tools/cpp2il-source"
        items = []
        for name in native._SOURCE_PROJECTS:
            source_file = self.source / name / "Original.cs"
            source_file.parent.mkdir(parents=True)
            source_file.write_text("// original pinned fixture\n")
            checksum = hashlib.sha256(source_file.read_bytes()).hexdigest()
            items.append(f'<PinnedCpp2ILSource Include="$(Cpp2ILSource)/{name}/Original.cs" ExpectedHash="{checksum}" />')
        self.manifest = self.project_dir / "SourcePin.props"
        self.manifest.write_text("<Project><ItemGroup>" + "".join(items) + "</ItemGroup></Project>")
        self.dotnet = self.work / "tools/dotnet/dotnet"
        self.dotnet.parent.mkdir(parents=True)
        self.dotnet.write_bytes(b"pinned dotnet fixture")
        self.archive = self.work / "downloads/source.tar.gz"
        self.archive.parent.mkdir(parents=True)
        self.archive.write_bytes(b"pinned archive fixture")
        self.build = {"status": "ready", "path": str(self.work / "harness.dll"), "fingerprint": "fixture"}

    def tearDown(self):
        shutil.rmtree(self.directory)

    def input_patches(self):
        return [mock.patch.object(native, "_resolve_input", return_value=(self.app, self.binary, self.metadata_file, "2022.3.54f1")),
                mock.patch.object(native, "parse_metadata", return_value=self.inventory),
                mock.patch.object(native, "_thin_binary", return_value=(self.binary, {"name": "x86_64"})),
                mock.patch.object(native, "build_native_harness", return_value=self.build)]

    def evidence(self, command):
        result = {"schema_version": 1, "status": "ready", "command": command,
                  "cpp2il_pin": native._PIN, "cpp2il_source_archive_sha256": native._ARCHIVE_HASH,
                  "native_binary_sha256": hashlib.sha256(self.binary.read_bytes()).hexdigest(),
                  "metadata_sha256": hashlib.sha256(self.metadata_file.read_bytes()).hexdigest(),
                  "unity_version": "2022.3.54f1", "managed_semantics_recovered": False,
                  "native_addresses_verified": False,
                  "custom_attribute_string_encoding": "metadata-v29-utf8-length-preserved"}
        if command == "schemas":
            result.update(assemblies=[{"name": "Game.Runtime", "types": [{"schema_complete": True, "fields": []}]}],
                          assembly_count=1, type_count=1, field_count=0, errors=[])
        else:
            result.update(method={"assembly": "Game.Runtime", "token": "0x060039b9", "native_size": 4,
                                  "native_sha256": hashlib.sha256(b"code").hexdigest()},
                          analysis_pipeline_invoked=False, isil_instruction_count=1)
        return result

    def runner(self, command, work_dir, log_name, **kwargs):
        output = Path(command[command.index("--output") + 1])
        self.assertFalse(output.exists())
        output.mkdir(parents=True)
        evidence = self.evidence(command[2])
        (output / "report.json").write_text(json.dumps(evidence))
        if command[2] == "method":
            (output / "native.bin").write_bytes(b"code")
            (output / "native.txt").write_text("ret\n")
            (output / "isil.txt").write_text("0 Return\n")
        return {"returncode": 0, "timed_out": False, "log": str(self.work / "process.log")}

    def invoke(self, runner=None, method=False):
        patches = self.input_patches()
        with patches[0], patches[1], patches[2], patches[3], mock.patch.object(native, "run_logged", side_effect=runner or self.runner) as run:
            result = (native.native_method(self.app, self.work, "Game.Runtime", "0x060039b9") if method else
                      native.native_schema(self.app, self.work, "Game.Runtime"))
        return result, run

    def test_schema_run_promotes_complete_evidence_atomically(self):
        report, run = self.invoke()
        self.assertEqual(report["status"], "ready")
        full = json.loads(Path(report["evidence_report"]).read_text())
        self.assertEqual(full["output_dir"], report["evidence_dir"])
        self.assertFalse(report["managed_semantics_recovered"])
        self.assertEqual(report["verified_gameplay_methods"], 0)
        self.assertEqual(run.call_args.kwargs["timeout"], 180)
        self.assertEqual(run.call_args.kwargs["env"]["DOTNET_TieredCompilation"], "0")
        self.assertTrue((self.work / "native-recovery/latest-schemas.json").is_file())
        self.assertEqual(list((self.work / "native-recovery/runs").glob(".stage-*")), [])

    def test_native_method_keeps_raw_evidence_and_bounded_timeout(self):
        report, run = self.invoke(method=True)
        self.assertEqual(report["status"], "ready")
        self.assertEqual(run.call_args.kwargs["timeout"], 120)
        self.assertIn("--token", run.call_args.args[0])
        self.assertFalse(report["analysis_pipeline_invoked"])
        self.assertEqual(Path(report["evidence_dir"]).joinpath("native.bin").read_bytes(), b"code")

    def test_missing_or_pending_report_cannot_be_ready(self):
        for pending in (False, True):
            with self.subTest(pending=pending):
                def runner(command, *args, **kwargs):
                    output = Path(command[command.index("--output") + 1])
                    output.mkdir(parents=True)
                    if pending:
                        value = dict(self.evidence("schemas"), status="pending")
                        (output / "report.json").write_text(json.dumps(value))
                    return {"returncode": -11, "timed_out": False, "log": "decoder.log"}
                report, _ = self.invoke(runner)
                self.assertEqual(report["status"], "failed")
                self.assertFalse((self.work / "native-recovery/latest-schemas.json").exists())

    def test_incomplete_schema_is_retained_without_ready_pointer(self):
        def runner(command, *args, **kwargs):
            output = Path(command[command.index("--output") + 1])
            output.mkdir(parents=True)
            value = dict(self.evidence("schemas"), status="incomplete", errors=[{"field": "privateField"}])
            value["assemblies"][0]["types"][0]["schema_complete"] = False
            (output / "report.json").write_text(json.dumps(value))
            return {"returncode": 1, "timed_out": False, "log": "attributes.log"}
        report, _ = self.invoke(runner)
        self.assertEqual(report["status"], "incomplete")
        self.assertEqual(report["decode_error_count"], 1)
        self.assertTrue(Path(report["evidence_report"]).is_file())
        self.assertFalse((self.work / "native-recovery/latest-schemas.json").exists())

    def test_wrong_fingerprint_or_behavior_claim_is_rejected(self):
        for mutation in ({"metadata_sha256": "wrong"}, {"managed_semantics_recovered": True},
                         {"native_addresses_verified": True}, {"custom_attribute_string_encoding": None},
                         {"custom_attribute_string_encoding": "unverified"}):
            with self.subTest(mutation=mutation):
                def runner(command, *args, **kwargs):
                    output = Path(command[command.index("--output") + 1])
                    output.mkdir(parents=True)
                    (output / "report.json").write_text(json.dumps(dict(self.evidence("schemas"), **mutation)))
                    return {"returncode": 0, "timed_out": False, "log": "invalid.log"}
                report, _ = self.invoke(runner)
                self.assertEqual(report["status"], "failed")
                self.assertIn("differs", report["error"])

    def test_native_body_tampering_is_rejected(self):
        def runner(command, *args, **kwargs):
            process = self.runner(command, *args, **kwargs)
            output = Path(command[command.index("--output") + 1])
            (output / "native.bin").write_bytes(b"fake")
            return process
        report, _ = self.invoke(runner, method=True)
        self.assertEqual(report["status"], "failed")
        self.assertIn("fingerprint differs", report["error"])

    def test_unknown_or_nonmethod_selection_stops_before_build(self):
        patches = self.input_patches()
        with patches[0], patches[1], patches[2], mock.patch.object(native, "build_native_harness") as build:
            for assembly, token in (("Unknown", "0x060039b9"), ("Game.Runtime", "0x02000009"),
                                    ("Game.Runtime", "0x06000000"), ("Game.Runtime", "0x06ffffff")):
                with self.subTest(assembly=assembly, token=token), self.assertRaises(ValueError):
                    native.native_method(self.app, self.work, assembly, token)
            build.assert_not_called()

    def test_source_mutation_and_additional_code_are_rejected(self):
        with mock.patch.object(native, "_MANIFEST", self.manifest):
            native._verify_cpp2il_source(self.source, self.work)
            file = self.source / "Cpp2IL.Core/Original.cs"
            original = file.read_text()
            file.write_text("changed")
            with self.assertRaisesRegex(ToolError, "differs from the pinned archive"):
                native._verify_cpp2il_source(self.source, self.work)
            file.write_text(original)
            (self.source / "Cpp2IL.Core/Unpinned.cs").write_text("new code")
            with self.assertRaisesRegex(ToolError, "Additional unpinned"):
                native._verify_cpp2il_source(self.source, self.work)

    def build_runner(self, command, work_dir, log_name, **kwargs):
        root = next(item.split("=", 1)[1] for item in command if item.startswith("-p:NativeRecoveryBuildRoot="))
        output = Path(root) / "bin/Release/net10.0"
        output.mkdir(parents=True)
        (output / native._HARNESS).write_bytes(b"built harness")
        (output / "Cpp2IL.Core.dll").write_bytes(b"pinned Core")
        return {"returncode": 0, "timed_out": False, "log": "build.log"}

    def build_patches(self):
        return [mock.patch.object(native, "_PROJECT", self.project), mock.patch.object(native, "_MANIFEST", self.manifest),
                mock.patch.object(native, "bootstrap", return_value={"status": "ready", "tools": {"dotnet": {"status": "ready"}}}),
                mock.patch.object(native, "_install", return_value=self.source),
                mock.patch.object(native, "download_artifact", return_value=self.archive)]

    def test_build_reuses_only_matching_verified_outputs(self):
        patches = self.build_patches()
        with patches[0], patches[1], patches[2], patches[3], patches[4], mock.patch.object(native, "run_logged", side_effect=self.build_runner) as run:
            first = native.build_native_harness(self.work)
            self.assertEqual(first["status"], "ready")
            self.assertEqual(run.call_args.kwargs["timeout"], 600)
            reused = native.build_native_harness(self.work)
            self.assertTrue(reused["reused"])
            self.assertEqual(first["path"], reused["path"])
            self.assertEqual(run.call_count, 1)
            Path(first["path"]).write_bytes(b"modified generated DLL")
            rebuilt = native.build_native_harness(self.work)
            self.assertEqual(rebuilt["status"], "ready")
            self.assertNotEqual(rebuilt["path"], first["path"])
            self.assertEqual(run.call_count, 2)
            self.project.write_text("updated harness project")
            changed = native.build_native_harness(self.work)
            self.assertNotEqual(changed["fingerprint"], rebuilt["fingerprint"])
            self.assertEqual(run.call_count, 3)

    def test_build_failure_does_not_promote_ready_receipt(self):
        patches = self.build_patches()
        with patches[0], patches[1], patches[2], patches[3], patches[4], mock.patch.object(native, "run_logged", return_value={"returncode": 1, "timed_out": False, "log": "failed.log"}):
            result = native.build_native_harness(self.work)
        self.assertEqual(result["status"], "failed")
        self.assertTrue(Path(result["report_path"]).is_file())
        self.assertFalse((self.work / "native-recovery/latest-build.json").exists())
        self.assertEqual(list((self.work / "native-recovery/builds").glob(".stage-*")), [])

    def test_all_harness_sources_invalidate_the_build_cache(self):
        experimental = self.project_dir / "ExperimentalRecovery.cs"
        experimental.write_text("// initial independent emitter\n")
        patches = self.build_patches()
        with patches[0], patches[1], patches[2], patches[3], patches[4], mock.patch.object(native, "run_logged", side_effect=self.build_runner) as run:
            first = native.build_native_harness(self.work)
            self.assertEqual(list(first["harness_sources"]),
                             ["NativeRecovery.csproj", "SourcePin.props", "ExperimentalRecovery.cs", "Program.cs"])
            self.assertTrue(native.build_native_harness(self.work)["reused"])
            experimental.write_text("// corrected independent emitter\n")
            changed = native.build_native_harness(self.work)
            self.assertFalse(changed["reused"])
            self.assertNotEqual(first["fingerprint"], changed["fingerprint"])
            self.assertEqual(run.call_count, 2)
            experimental.unlink()
            removed = native.build_native_harness(self.work)
            self.assertNotEqual(changed["fingerprint"], removed["fingerprint"])
            self.assertEqual(run.call_count, 3)

    def test_work_dir_and_symlink_boundaries_are_enforced(self):
        with self.assertRaises(ValueError):
            native.build_native_harness(self.directory.parents[1])
        (self.work / "native-recovery").symlink_to(self.app, target_is_directory=True)
        patches = self.build_patches()
        with patches[0], patches[1], patches[2], patches[3], patches[4]:
            result = native.build_native_harness(self.work)
        self.assertEqual(result["status"], "failed")
        self.assertIn("symbolic link", result["error"])
        self.assertEqual(sorted(path.name for path in self.app.iterdir()), ["binary", "metadata"])


if __name__ == "__main__":
    unittest.main()

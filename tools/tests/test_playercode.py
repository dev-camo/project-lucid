"""Player compilation publication guards; fixtures are metadata, never executable game code."""
import hashlib
import json
import os
import shutil
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import uuid

from lucidlib import bootstrap, playercode


def managed_fixture(name, mvid=None):
    """Small genuine PE/CLI metadata fixture with one Module and Assembly row."""
    mvid = mvid or uuid.uuid4()
    module_name = (name + ".dll").encode()
    strings = b"\0" + module_name + b"\0" + name.encode() + b"\0"
    assembly_index = len(module_name) + 2
    tables = struct.pack("<IBBBBQQII", 0, 2, 0, 0, 1, (1 << 0) | (1 << 32), 0, 1, 1)
    tables += struct.pack("<HHHHH", 0, 1, 1, 0, 0)
    tables += struct.pack("<IHHHHIHHH", 0x8004, 0, 0, 0, 0, 0, 0, assembly_index, 0)
    version = b"v4.0.30319\0\0"
    prefix = struct.pack("<IHHII", 0x424a5342, 1, 1, 0, len(version)) + version + struct.pack("<HH", 0, 3)
    streams = [(b"#~", tables), (b"#Strings", strings), (b"#GUID", mvid.bytes_le)]
    header_size = len(prefix) + sum(8 + ((len(n) + 1 + 3) & ~3) for n, data in streams)
    headers, payload, cursor = b"", b"", header_size
    for name_bytes, data in streams:
        label = name_bytes + b"\0"
        label += b"\0" * ((-len(label)) & 3)
        headers += struct.pack("<II", cursor, len(data)) + label
        payload += data; cursor += len(data)
    metadata = prefix + headers + payload
    blob = bytearray(1024)
    blob[:2] = b"MZ"; struct.pack_into("<I", blob, 0x3c, 0x80)
    blob[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<HHIIIHH", blob, 0x84, 0x14c, 1, 0, 0, 0, 224, 0x2102)
    struct.pack_into("<H", blob, 0x98, 0x10b)
    struct.pack_into("<I", blob, 0x98 + 92, 16)
    struct.pack_into("<II", blob, 0x98 + 96 + 14 * 8, 0x2000, 0x48)
    blob[0x178:0x180] = b".text\0\0\0"
    struct.pack_into("<IIII", blob, 0x180, 512, 0x2000, 512, 512)
    struct.pack_into("<IHHII", blob, 512, 0x48, 2, 5, 0x2048, len(metadata))
    blob[584:584 + len(metadata)] = metadata
    return bytes(blob), str(mvid)


class PlayerCodeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="player code space ")
        self.root = Path(self.temp.name).resolve()
        self.work = self.root / ".cache" / "project-lucid"
        (self.root / "ProjectSettings").mkdir()
        (self.root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 2022.3.54f1\n")
        (self.root / "Assets").mkdir()
        self.source = self.root / "Assets/Fixture.cs"
        self.source.write_text("// fixed managed compilation input\n")
        self.editor = self.root / "engine/Unity.app/Contents/MacOS/Unity"
        self.editor.parent.mkdir(parents=True)
        self.editor.write_bytes(b"installed native editor identity fixture")
        managed = self.editor.parents[1] / "Managed"
        (managed / "UnityEngine").mkdir(parents=True)
        for path, name in [(managed / "UnityEngine/UnityEditor.CoreModule.dll", "UnityEditor.CoreModule"),
                           (managed / "UnityEngine/UnityEngine.CoreModule.dll", "UnityEngine.CoreModule"),
                           (managed / "Unity.Cecil.dll", "Unity.Cecil"),
                           (managed / "Unity.SerializationLogic.dll", "Unity.SerializationLogic")]:
            path.write_bytes(managed_fixture(name)[0])
        self.latest = self.work / "player-code/latest-macos.json"
        self.latest.parent.mkdir(parents=True)
        self.latest.write_bytes(b"previous validated player receipt bytes\n")
        self.old = self.latest.read_bytes()
        self.patches = [patch.object(bootstrap, "REPO_ROOT", self.root),
                        patch.object(bootstrap, "CACHE_ROOT", self.work),
                        patch.object(playercode, "find_editor", return_value=self.editor)]
        for p in self.patches: p.start()
        self.addCleanup(self.temp.cleanup)
        for p in self.patches: self.addCleanup(p.stop)

    def native_queries(self, engine, target):
        core = next(e for e in engine if e["name"] == "editor_core")
        directory = self.editor.parents[1] / "MonoBleedingEdge/EmbedRuntime"
        directory.mkdir(parents=True, exist_ok=True)
        declarations = [("UnityEditor.BuildPipeline", "GetMonoRuntimeLibDirectory", "0x06002264", 147, "UnityEditor.BuildTarget"),
                        ("UnityEditor.BuildPipeline", "CompatibilityProfileToClassLibFolder", "0x06002265", 147, "UnityEditor.ApiCompatibilityLevel"),
                        ("UnityEditor.BuildTargetDiscovery", "GetPlatformProfileSuffix", "0x0600237b", 150, "UnityEditor.BuildTarget")]
        return {"unity_version": "2022.3.54f1", "target": playercode.TARGETS[target][1],
                "module_path": core["path"], "module_sha256": core["sha256"], "module_mvid": core["mvid"],
                "module_assembly": "UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                "api_compatibility_value": 6, "api_compatibility_name": "NET_Standard_2_0",
                "scripting_backend_value": 0, "scripting_backend_name": "Mono2x",
                "compatibility_profile_folder": "observed-profile", "mono_runtime_lib_directory": str(directory),
                "platform_profile_suffix": "observed-target", "runtime_selection_verified": False,
                "runtime_assemblies_loaded": False, "build_player_called": False, "layout_approved": False, "gameplay_verified": False,
                "methods": [{"declaring_type": owner, "method": name, "token": token, "attributes": flags,
                             "implementation_attributes": 4096, "parameter_type": parameter, "return_type": "System.String"}
                            for owner, name, token, flags, parameter in declarations]}

    def emit(self, command, target="macos", change=None):
        option = lambda name: command[command.index(name)+1]
        context_path = Path(option("-lucidPlayerContext"))
        context = dict(line.split("=", 1) for line in context_path.read_text().splitlines())
        run = context_path.parent
        output = run / "assemblies"
        output.mkdir()
        compiler = self.work / "player-code/compiler-runs" / run.name
        compiler.mkdir(parents=True)
        files, modules, returned, compiler_files = [], [], [], []
        for name in sorted(playercode.REQUIRED_ASSEMBLIES):
            path = output / (name + ".dll")
            path.write_bytes(managed_fixture(name)[0])
            origin = compiler / path.name
            origin.write_bytes(path.read_bytes())
            compiler_file = playercode._file_record(origin)
            compiler_files.append(dict(relative_path=path.name, **compiler_file))
            record = dict(relative_path=path.name, **playercode._file_record(path))
            files.append(record)
            modules.append(dict(record, returned_path=path.name, compiler_file=compiler_file, **playercode._pe_identity(path)))
            returned.append(path.name)
        engine = playercode._engine_identity(self.editor)
        inputs = [dict(kind="source", **playercode._file_record(self.source))]
        source = context["source_fingerprint"]
        report = {"schema_version": 2, "command": "player-code", "status": "pending-identity-verification",
                  "identity_status": "pending-wrapper", "compilation_status": "compiled", "unity_version": "2022.3.54f1",
                  "target": target, "active_target": playercode.TARGETS[target][1], "build_target": playercode.TARGETS[target][1],
                  "build_group": "Standalone", "subtarget": 0, "options": "None", "extra_scripting_defines": [],
                  "compilation_api": "PlayerBuildInterface.CompilePlayerScripts", "identity_nonce": option("-lucidPlayerNonce"),
                  "identity_context_sha256": option("-lucidPlayerDigest"), "project_root": str(self.root), "work_root": str(self.work), "run": str(run),
                  "source_fingerprint_before": source, "source_fingerprint_after": source,
                  "engine_before": engine, "engine_after": engine, "diagnostics": [], "inputs": inputs,
                  "inputs_fingerprint_before": playercode._input_identity(inputs), "inputs_fingerprint_after": playercode._input_identity(inputs),
                  "modules": modules, "returned_assemblies": returned, "files": files,
                  "compiler_output": str(compiler), "compiler_files": compiler_files,
                  "snapshot_policy": "separate-compiler-output-exact-snapshot-v1",
                  "compilation_cache_policy": "engine-clean-player-cache-v1",
                  "player_schema_verified": False, "gameplay_verified": False}
        core = next(e for e in engine if e["name"] == "editor_core")
        api = "UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface"
        options = "UnityEditor.Scripting.ScriptCompilation.EditorScriptCompilationOptions"
        status = "UnityEditor.Scripting.ScriptCompilation.EditorCompilation+CompileStatus"
        parameters = options + ",UnityEditor.BuildTargetGroup,UnityEditor.BuildTarget,System.Int32,System.String[]"
        report["clean_compilation"] = {
            "status": "completed", "error": "", "unity_version": "2022.3.54f1",
            "module_path": core["path"], "module_sha256_before": core["sha256"],
            "module_sha256_after": core["sha256"], "module_mvid": core["mvid"],
            "interface_type": api, "options_type": options, "status_type": status,
            "compile_method": api + ".CompileScripts(" + parameters + ")->" + status,
            "tick_method": api + ".TickCompilationPipeline(" + parameters + ",System.Boolean)->" + status,
            "get_output_method": api + ".GetCompileScriptsOutputDirectory()->System.String",
            "set_output_method": api + ".SetCompileScriptsOutputDirectory(System.String)->System.Void",
            "options_names": "BuildingCleanCompilation, BuildingExtractTypeDB, BuildingUseDeterministicCompilation",
            "options_value": 20992, "target_name": playercode.TARGETS[target][1],
            "target_value": {"macos": 2, "windows": 19, "linux": 24}[target],
            "group_name": "Standalone", "group_value": 1, "subtarget": 0,
            "extra_scripting_defines": [], "building_for_editor": False,
            "output_path": str(compiler), "output_restored": True, "public_player_compile_called": True,
            "terminal_status": "CompilationComplete", "terminal_status_value": 4,
            "start_status": "CompilationStarted", "start_status_value": 2,
            "tick_count": 1, "elapsed_milliseconds": 10, "observed_statuses": ["CompilationStarted", "CompilationComplete"],
            "previous_output_path": "Library/ScriptAssemblies", "restored_output_path": "Library/ScriptAssemblies"}
        report["native_profile_queries_before"] = self.native_queries(engine, target)
        report["native_profile_queries_after"] = json.loads(json.dumps(report["native_profile_queries_before"]))
        if change: change(report, run)
        (run / "pending.json").write_text(json.dumps(report))
        return subprocess.CompletedProcess(command, 0)

    def recheck(self, receipt, **options):
        run = Path(receipt["run"])
        staged = json.loads((run / "pending.json").read_bytes())
        playercode._check_report(staged, self.root, self.work, run, receipt["target"], run.name,
                                receipt["source_fingerprint_before"], receipt["engine_before"],
                                receipt["identity_context_sha256"], **options)

    def test_archive_replay_survives_deleted_compiler_working_folder(self):
        self.run_fake()
        receipt = json.loads(self.latest.read_bytes())
        original = [Path(m["path"]).read_bytes() for m in receipt["modules"]]
        shutil.rmtree(receipt["compiler_output"])
        self.recheck(receipt)
        self.assertEqual(original, [Path(m["path"]).read_bytes() for m in receipt["modules"]])

    def test_archive_replay_survives_later_compiler_working_bytes(self):
        self.run_fake()
        receipt = json.loads(self.latest.read_bytes())
        Path(receipt["modules"][0]["compiler_file"]["path"]).write_bytes(b"later compiler scratch")
        self.recheck(receipt)
        with self.assertRaises(ValueError): self.recheck(receipt, require_compiler_output=True)

    def test_live_origin_byte_change_prevents_publication(self):
        self.assert_rejected(lambda r, run: Path(r["compiler_files"][0]["path"]).write_bytes(b"changed during copy"))

    def test_live_origin_removal_prevents_publication(self):
        self.assert_rejected(lambda r, run: shutil.rmtree(r["compiler_output"]))

    def test_live_origin_extra_file_prevents_publication(self):
        self.assert_rejected(lambda r, run: (Path(r["compiler_output"]) / "unreported.pdb").write_bytes(b"extra"))

    def test_live_origin_symlink_prevents_publication(self):
        def mutate(r, run):
            p = Path(r["compiler_files"][0]["path"]);p.unlink();p.symlink_to(Path(r["files"][0]["path"]))
        self.assert_rejected(mutate)

    def test_missing_snapshot_policy_prevents_publication(self):
        self.assert_rejected(lambda r, run: r.pop("snapshot_policy"))

    def test_clean_player_receipt_required_before_publication(self):
        self.assert_rejected(lambda r, run: r.pop("clean_compilation"))

    def test_failed_editor_or_incomplete_clean_player_receipt_rejected(self):
        for changed in ({"status": "failed"}, {"building_for_editor": True},
                        {"terminal_status_value": 3}, {"output_restored": False},
                        {"public_player_compile_called": False}, {"options_value": 20994},
                        {"restored_output_path": "another/output"}, {"target_name": "StandaloneWindows64"},
                        {"module_mvid": "00000000-0000-0000-0000-000000000000"},
                        {"observed_statuses": ["CompilationStarted"]}, {"tick_count": True},
                        {"elapsed_milliseconds": 120001}):
            with self.subTest(changed=changed):
                self.assert_rejected(lambda r, run: r["clean_compilation"].update(changed))

    def test_missing_or_reused_compiler_cache_policy_prevents_publication(self):
        self.assert_rejected(lambda r, run: r.pop("compilation_cache_policy"))
        self.assert_rejected(lambda r, run: r.update(compilation_cache_policy="reuse-build-cache"))

    def test_unowned_compiler_directory_prevents_publication(self):
        self.assert_rejected(lambda r, run: r.update(compiler_output=str(run / "assemblies")))

    def test_missing_compiler_manifest_prevents_publication(self):
        self.assert_rejected(lambda r, run: r.pop("compiler_files"))

    def test_duplicate_origin_manifest_prevents_publication(self):
        self.assert_rejected(lambda r, run: r["compiler_files"].__setitem__(1, r["compiler_files"][0]))

    def test_nonscalar_origin_relative_paths_fail_with_controlled_errors(self):
        for value in ({}, [], None, 1, True):
            self.assert_rejected(lambda r, run: r["compiler_files"][0].update(relative_path=value))

    def test_changed_origin_association_prevents_publication(self):
        self.assert_rejected(lambda r, run: r["modules"][0]["compiler_file"].update(path=r["files"][0]["path"]))

    def test_rewritten_returned_path_to_archive_prevents_publication(self):
        def mutate(r, run):
            r["returned_assemblies"][0] = r["modules"][0]["returned_path"] = r["modules"][0]["path"]
        self.assert_rejected(mutate)

    def test_new_publication_rejects_legacy_schema(self):
        self.assert_rejected(lambda r, run: r.update(schema_version=1))

    def test_historical_schema_one_retains_exact_archive_path_contract(self):
        self.run_fake()
        receipt = json.loads(self.latest.read_bytes());run = Path(receipt["run"])
        staged = json.loads((run / "pending.json").read_bytes())
        staged["schema_version"] = 1
        for key in ("compiler_output", "compiler_files", "snapshot_policy"): staged.pop(key)
        for item in staged["modules"]: item.pop("compiler_file")
        for raw, item in zip(staged["returned_assemblies"], staged["modules"]): self.assertEqual(raw, Path(item["path"]).name)
        context = playercode._context(self.root, self.work, run, "macos", staged["source_fingerprint_before"], run.name,
                                      staged["engine_before"], schema_version=1)
        (run / "context.txt").write_bytes(context)
        staged["identity_context_sha256"] = hashlib.sha256(context).hexdigest()
        (run / "pending.json").write_text(json.dumps(staged))
        receipt.update(staged, status="compiled", identity_status="complete",
                       staged_report_sha256=hashlib.sha256((run / "pending.json").read_bytes()).hexdigest())
        self.recheck(receipt)
        from lucidlib import playerschema
        self.latest.write_text(json.dumps(receipt))
        playerschema._player_evidence(self.root, self.work, "macos", receipt["source_fingerprint_before"], receipt["engine_before"])

    def test_context_version_rejects_noninteger_or_unsupported_values(self):
        for value in (True, 2.0, "2", 0, 3, None):
            with self.assertRaises(ValueError): playercode._context(self.root, self.work, self.work / "run", "macos", "a" * 64,
                                                                    "b" * 32, [], schema_version=value)

    def run_fake(self, change=None, target="macos"):
        with patch.object(playercode.subprocess, "run", side_effect=lambda args, timeout: self.emit(args, target, change)) as invoke:
            result = playercode.run_player_code(self.root, self.work, target)
        return result, invoke

    def assert_rejected(self, change):
        reached = []
        def checked_change(report, run):
            reached.append(True)
            change(report, run)
        with self.assertRaises((ValueError, OSError)):
            self.run_fake(checked_change)
        self.assertTrue(reached, "Failure fixture must reach the actual guarded mutation")
        self.assertEqual(self.old, self.latest.read_bytes())
        self.assertFalse((self.work / "locks/player-code.lock").exists())

    def test_publish_only_after_metadata_and_identity_checks(self):
        result, invoke = self.run_fake()
        self.assertEqual("compiled", result["status"])
        report = json.loads(self.latest.read_bytes())
        self.assertEqual("complete", report["identity_status"])
        self.assertFalse(report["player_schema_verified"])
        self.assertFalse(report["gameplay_verified"])
        self.assertEqual(4, len(report["modules"]))
        args = invoke.call_args.args[0]
        self.assertEqual("osxuniversal", args[args.index("-buildTarget")+1])
        self.assertEqual("Player", args[args.index("-standaloneBuildSubtarget")+1])

    def test_every_target_uses_its_actual_player_profile(self):
        for target in ("windows", "linux"):
            result, invoke = self.run_fake(target=target)
            self.assertEqual("compiled", result["status"])
            args = invoke.call_args.args[0]
            self.assertEqual(playercode.TARGETS[target][0], args[args.index("-buildTarget")+1])

    def test_source_change_retains_prior_receipt(self):
        self.assert_rejected(lambda report, run: self.source.write_text("// source changed\n"))

    def test_precompiled_package_input_change_is_detected(self):
        reference = self.root / "Library/PackageCache/package/runtime.dll"
        reference.parent.mkdir(parents=True); reference.write_bytes(managed_fixture("Package.Runtime")[0])
        def mutate(report, run):
            report["inputs"].append(dict(kind="precompiled_reference", **playercode._file_record(reference)))
            report["inputs_fingerprint_before"] = report["inputs_fingerprint_after"] = playercode._input_identity(report["inputs"])
            reference.write_bytes(managed_fixture("Package.Runtime")[0])
        self.assert_rejected(mutate)

    def test_installed_editor_drift_is_detected(self):
        self.assert_rejected(lambda report, run: self.editor.write_bytes(b"changed editor bytes"))

    def test_context_mutation_is_detected(self):
        self.assert_rejected(lambda report, run: (run / "context.txt").write_bytes(b"changed context\n"))

    def test_module_mvid_cannot_be_forged(self):
        self.assert_rejected(lambda report, run: report["modules"][0].update(mvid=str(uuid.uuid4())))

    def test_declared_assembly_name_cannot_be_forged(self):
        self.assert_rejected(lambda report, run: report["modules"][0].update(assembly_name="Different.Runtime"))

    def test_module_bytes_cannot_be_changed_after_hashing(self):
        self.assert_rejected(lambda report, run: Path(report["modules"][0]["path"]).write_bytes(b"not a managed DLL"))

    def test_foreign_existing_destination_is_not_accepted(self):
        def mutate(report, run):
            old = self.work / "prior/Game.Runtime.dll"; old.parent.mkdir(); old.write_bytes(managed_fixture("Game.Runtime")[0])
            report["modules"][0]["returned_path"] = report["returned_assemblies"][0] = str(old)
        self.assert_rejected(mutate)

    def test_relative_parent_traversal_is_rejected(self):
        self.assert_rejected(lambda report, run: report["files"][0].update(relative_path="../context.txt"))

    def test_stale_nonce_is_rejected(self):
        self.assert_rejected(lambda report, run: report.update(identity_nonce="0" * 32))

    def test_compiler_error_is_not_published_even_when_files_exist(self):
        self.assert_rejected(lambda report, run: report["diagnostics"].append({"type": "Error", "message": "actual compiler failed"}))

    def test_unreported_output_file_is_rejected(self):
        self.assert_rejected(lambda report, run: (run / "assemblies/unreported.pdb").write_bytes(b"new file"))

    def test_missing_required_assembly_is_rejected(self):
        def mutate(report, run):
            report["modules"] = report["modules"][1:]; report["returned_assemblies"] = report["returned_assemblies"][1:]
        self.assert_rejected(mutate)

    def test_symlinked_compiler_output_is_rejected(self):
        def mutate(report, run):
            module = Path(report["modules"][0]["path"]); copy = self.root / "outside.dll"; copy.write_bytes(module.read_bytes())
            module.unlink(); module.symlink_to(copy)
        self.assert_rejected(mutate)

    def test_symlinked_latest_cannot_redirect_publication(self):
        self.latest.unlink(); outside = self.root / "outside.json"; outside.write_bytes(self.old); self.latest.symlink_to(outside)
        with self.assertRaises(ValueError): self.run_fake()
        self.assertEqual(self.old, outside.read_bytes())

    def test_existing_nonce_run_is_never_deleted_or_reused(self):
        nonce = uuid.uuid4(); run = self.work / "player-code/runs" / nonce.hex; run.mkdir(parents=True); marker = run / "keep"; marker.write_bytes(b"retained")
        before = {p.name: p.read_bytes() for p in run.iterdir()}
        with patch.object(playercode.uuid, "uuid4", return_value=nonce):
            with self.assertRaises(FileExistsError): self.run_fake()
        self.assertEqual(before, {p.name: p.read_bytes() for p in run.iterdir()})
        self.assertEqual(self.old, self.latest.read_bytes())

    def test_child_work_directory_is_sealed_in_context_and_report(self):
        self.work = self.work / "owned child cache"
        self.latest = self.work / "player-code/latest-macos.json"
        self.latest.parent.mkdir(parents=True); self.latest.write_bytes(self.old)
        result, _ = self.run_fake()
        self.assertEqual("compiled", result["status"])
        report = json.loads(self.latest.read_bytes())
        self.assertEqual(str(self.work), report["work_root"])
        run = Path(report["run"])
        self.assertEqual(self.work / "player-code/runs", run.parent)
        context = dict(line.split("=", 1) for line in (run / "context.txt").read_text().splitlines())
        import base64
        self.assertEqual(str(self.work), base64.b64decode(context["work_root_base64"]).decode())

    def test_work_directory_cannot_be_maintained_or_arbitrary(self):
        for path in (self.root, self.root / "Assets", self.root / "input", self.root / "arbitrary"):
            with self.assertRaises(ValueError): playercode.run_player_code(self.root, path, "macos")
        self.assertEqual(self.old, self.latest.read_bytes())

    def test_forged_work_root_is_rejected(self):
        self.assert_rejected(lambda report, run: report.update(work_root=str(self.root / "Assets")))

    def test_source_change_during_final_engine_hash_is_rejected(self):
        original = playercode._engine_identity
        calls = []
        def identity(editor):
            actual = original(editor); calls.append(True)
            if len(calls) == 4: self.source.write_text("// changed during final engine hashing\n")
            return actual
        with patch.object(playercode, "_engine_identity", side_effect=identity):
            with self.assertRaisesRegex(ValueError, "final installed Editor identity"):
                self.run_fake()
        self.assertEqual(4, len(calls))
        self.assertEqual(self.old, self.latest.read_bytes())

    def test_pending_change_during_final_engine_hash_is_rejected(self):
        original = playercode._engine_identity
        calls = []
        def identity(editor):
            actual = original(editor); calls.append(True)
            if len(calls) == 4:
                pending, = (self.work / "player-code/runs").glob("*/pending.json")
                pending.write_bytes(b"{}\n")
            return actual
        with patch.object(playercode, "_engine_identity", side_effect=identity):
            with self.assertRaisesRegex(ValueError, "final identity validation"):
                self.run_fake()
        self.assertEqual(4, len(calls))
        self.assertEqual(self.old, self.latest.read_bytes())

    def test_input_manifest_unicode_sort_matches_explicit_utf16_ordinal(self):
        records = [{"kind": "source", "path": "\ue000.cs", "sha256": "1" * 64},
                   {"kind": "source", "path": "\U00010000.cs", "sha256": "2" * 64}]
        expected = "".join(r["kind"] + "\0" + r["path"] + "\0" + r["sha256"] + "\n" for r in records[::-1])
        self.assertEqual(hashlib.sha256(expected.encode()).hexdigest(), playercode._input_identity(records))

    def test_interrupted_compiler_retains_prior_receipt(self):
        with patch.object(playercode.subprocess, "run", side_effect=subprocess.TimeoutExpired("Editor", 1800)):
            with self.assertRaises(subprocess.TimeoutExpired): playercode.run_player_code(self.root, self.work, "macos")
        self.assertEqual(self.old, self.latest.read_bytes())
        self.assertFalse((self.work / "locks/player-code.lock").exists())

    def test_nonzero_editor_return_retains_prior_receipt(self):
        with patch.object(playercode.subprocess, "run", return_value=subprocess.CompletedProcess([], 1)):
            result = playercode.run_player_code(self.root, self.work, "macos")
        self.assertEqual("failed", result["status"])
        self.assertEqual(self.old, self.latest.read_bytes())

    def test_strict_json_rejects_duplicate_and_nonfinite_fields(self):
        for data in (b'{"status":"compiled","status":"failed"}', b'{"count":NaN}'):
            with self.assertRaises(ValueError): playercode._strict_json(data)

    def test_pe_parser_reads_exact_identity_and_rejects_truncation(self):
        path = self.root / "fixture.dll"; data, mvid = managed_fixture("Fixture.Runtime"); path.write_bytes(data)
        self.assertEqual({"assembly_name": "Fixture.Runtime", "mvid": mvid}, playercode._pe_identity(path))
        for broken in (b"MZ", data[:520], b"not PE"):
            path.write_bytes(broken)
            with self.assertRaises(ValueError): playercode._pe_identity(path)

    def test_engine_version_is_not_inferred_from_filename(self):
        (self.root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 2022.3.53f1\n")
        with self.assertRaises(ValueError): self.run_fake()
        self.assertEqual(self.old, self.latest.read_bytes())


if __name__ == "__main__":
    unittest.main()


class NativeQueryGuards(unittest.TestCase):
    setUp = PlayerCodeTests.setUp
    native_queries = PlayerCodeTests.native_queries
    emit = PlayerCodeTests.emit
    run_fake = PlayerCodeTests.run_fake
    assert_rejected = PlayerCodeTests.assert_rejected
    # Mutation tests change sealed native evidence, never invoke runtime libraries.
    def test_missing_native_query_rejected(self):
        self.assert_rejected(lambda r, run: r.pop("native_profile_queries_before"))

    def test_native_query_drift_rejected(self):
        self.assert_rejected(lambda r, run: r["native_profile_queries_after"].update(platform_profile_suffix="different"))

    def test_native_query_module_rejected(self):
        self.assert_rejected(lambda r, run: [r[k].update(module_mvid=str(uuid.uuid4())) for k in ("native_profile_queries_before", "native_profile_queries_after")])

    def test_native_query_approval_rejected(self):
        self.assert_rejected(lambda r, run: [r[k].update(runtime_selection_verified=True) for k in ("native_profile_queries_before", "native_profile_queries_after")])

    def test_native_query_backend_rejected(self):
        self.assert_rejected(lambda r, run: [r[k].update(scripting_backend_value=1) for k in ("native_profile_queries_before", "native_profile_queries_after")])

    def test_native_query_api_rejected(self):
        self.assert_rejected(lambda r, run: [r[k].update(api_compatibility_value=3) for k in ("native_profile_queries_before", "native_profile_queries_after")])

    def test_native_query_directory_rejected(self):
        self.assert_rejected(lambda r, run: [r[k].update(mono_runtime_lib_directory=str(self.work)) for k in ("native_profile_queries_before", "native_profile_queries_after")])

    def test_native_query_method_rejected(self):
        def mutate(r, run):
            for k in ("native_profile_queries_before", "native_profile_queries_after"):
                r[k]["methods"][0]["token"] = "0x06002265"
        self.assert_rejected(mutate)

    def test_native_query_profile_path_rejected(self):
        self.assert_rejected(lambda r, run: [r[k].update(compatibility_profile_folder="../profile") for k in ("native_profile_queries_before", "native_profile_queries_after")])

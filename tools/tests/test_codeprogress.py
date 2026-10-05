import copy
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
from contextlib import ExitStack
from types import SimpleNamespace
import subprocess

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap, codeprogress


class CodeWorkTests(unittest.TestCase):
    def signature(self, name="Calculate", **changes):
        return dict({"owner": "Hardlight.Sample", "name": name, "is_static": True, "return_type": "primitive:System.Int32",
                     "parameters": [{"type": "primitive:System.Int32", "direction": 0}], "generic_parameters": [], "declaring_generic_parameters": []}, **changes)

    def original_row(self, token="0x06000001", assembly="Game.Runtime", native=True, signature=None):
        signature = self.signature() if signature is None else signature
        return {"id": assembly + ":" + signature["owner"] + ":" + token, "assembly": assembly, "token": token,
                "declaring_type": signature["owner"], "name": signature["name"], "signature": signature, "native_body": native}

    def player_row(self, token="0x06000007", signature=None, **changes):
        signature = self.signature() if signature is None else signature
        return dict({"assembly": "Game.Runtime", "token": token, "declaring_type": signature["owner"], "name": signature["name"],
                     "signature": signature, "body_status": "implementation_candidate", "source_status": "verified",
                     "source_documents": [{"relative_path": "Assets/Scripts/Sample.cs", "size": 4, "sha256": "a" * 64}], "il_sha256": "b" * 64}, **changes)

    def inventories(self, original=None, player=None):
        flags = {"native_byte_matching_verified": False, "gameplay_verified": False}
        return ({"kind": "original-native-method-inventory-v1", "methods": [self.original_row()] if original is None else original, **flags},
                {"kind": "maintained-player-source-body-inventory-v1", "methods": [self.player_row()] if player is None else player, **flags})

    def measure(self, original=None, player=None, scope=None, filters=None):
        return codeprogress.measure_code_work(*self.inventories(original, player), scope or ["Game.Runtime", "HLUnityCore.Runtime"], filters)

    def test_exact_signature_body_count_ignores_different_compiled_tokens(self):
        report = self.measure()
        self.assertEqual(1, report["totals"]["implemented_source_bodies"])
        self.assertEqual(1, report["totals"]["original_native_bodies"])
        self.assertEqual("0x06000007", report["method_map"][0]["maintained_token"])
        self.assertFalse(report["behavior_parity_verified"])

    def test_zero_implemented_original_assemblies_remain_present(self):
        report = self.measure()
        self.assertEqual(["Game.Runtime", "HLUnityCore.Runtime"], [g["assembly"] for g in report["assemblies"]])
        self.assertEqual(0, report["assemblies"][1]["implemented_source_bodies"])

    def test_missing_method_has_no_credit(self):
        report = self.measure(player=[])
        self.assertEqual((0, 1), (report["totals"]["implemented_source_bodies"], report["totals"]["not_implemented"]))

    def test_declaration_enum_or_bodyfree_contract_does_not_enter_denominator(self):
        report = self.measure(original=[self.original_row(native=False)])
        self.assertEqual((0, 0, 1), tuple(report["totals"][k] for k in ("implemented_source_bodies", "original_native_bodies", "original_declarations")))

    def test_compiled_declaration_empty_default_constant_throw_and_generated_are_withheld(self):
        for state in codeprogress.BODY_STATUSES - {"implementation_candidate"}:
            with self.subTest(state=state):
                report = self.measure(player=[self.player_row(body_status=state)])
                self.assertEqual(0, report["totals"]["implemented_source_bodies"])
                self.assertEqual(1, report["totals"]["withheld_bodies"])

    def test_bad_source_checksum_missing_sequencepoints_and_foreign_source_are_withheld(self):
        for state in codeprogress.SOURCE_STATUSES - {"verified"}:
            report = self.measure(player=[self.player_row(source_status=state)])
            self.assertEqual(0, report["totals"]["implemented_source_bodies"])
            self.assertEqual(1, report["totals"]["withheld_bodies"])

    def test_placeholder_has_no_credit_even_with_matching_signature_and_pdb(self):
        report = self.measure(player=[self.player_row(body_status="exception_placeholder_withheld")])
        self.assertEqual("exception_placeholder_withheld", report["method_map"][0]["status"])

    def test_generic_native_variants_do_not_multiply_original_body_count(self):
        row = self.original_row(); row["concrete_generic_variants"] = 200
        self.assertEqual(1, self.measure(original=[row])["totals"]["original_native_bodies"])

    def test_same_signature_two_original_tokens_is_ambiguous_not_two_credits(self):
        report = self.measure(original=[self.original_row(), self.original_row("0x06000002")])
        self.assertEqual(0, report["totals"]["implemented_source_bodies"])
        self.assertEqual(2, report["totals"]["unresolved_identities"])

    def test_same_signature_two_compiled_methods_is_ambiguous(self):
        report = self.measure(player=[self.player_row(), self.player_row("0x06000008")])
        self.assertEqual(1, report["totals"]["unresolved_identities"])
        self.assertEqual(0, report["totals"]["implemented_source_bodies"])

    def test_exact_owner_overload_return_static_byref_and_constraint_differences_do_not_match(self):
        for changes in ({"owner": "Hardlight.Different"}, {"return_type": "primitive:System.Single"}, {"is_static": False},
                        {"parameters": [{"type": "primitive:System.Int32&", "direction": 2}]},
                        {"generic_parameters": [{"flags": 4, "constraints": ["[mscorlib]System.IConvertible"]}]},
                        {"declaring_generic_parameters": [{"flags": 4, "constraints": []}]}):
            with self.subTest(changes=changes):
                report = self.measure(player=[self.player_row(signature=self.signature(**changes))])
                self.assertEqual(0, report["totals"]["implemented_source_bodies"])

    def test_netstandard_mscorlib_named_contracts_are_not_blanket_aliased(self):
        original = self.original_row(signature=self.signature(parameters=[{"type": "[mscorlib]System.Action", "direction": 0}]))
        player = self.player_row(signature=self.signature(parameters=[{"type": "[netstandard]System.Action", "direction": 0}]))
        report = self.measure(original=[original], player=[player])
        self.assertEqual("unresolved_signature_difference", report["method_map"][0]["status"])

    def test_unresolved_original_signature_is_uncredited(self):
        row = self.original_row(); row["signature"] = None
        report = self.measure(original=[row])
        self.assertEqual((0, 1), (report["totals"]["implemented_source_bodies"], report["totals"]["unresolved_identities"]))

    def test_unresolved_player_signature_is_uncredited_and_reported_unresolved(self):
        row = self.player_row(); row["signature"] = None
        report = self.measure(player=[row])
        self.assertEqual(1, report["totals"]["unresolved_identities"])
        self.assertEqual(0, report["totals"]["not_implemented"])

    def test_altered_identity_owner_or_token_and_duplicate_methods_are_rejected(self):
        for key, value in (("id", "forged"), ("token", "0x02000001"), ("declaring_type", "Other"), ("name", "Other")):
            row = self.original_row(); row[key] = value
            with self.assertRaises(ValueError): self.measure(original=[row])
        with self.assertRaises(ValueError): self.measure(original=[self.original_row(), self.original_row()])
        with self.assertRaises(ValueError): self.measure(player=[self.player_row(), self.player_row()])

    def test_duplicate_original_token_cannot_credit_two_owners(self):
        other = self.original_row(signature=self.signature(owner="Hardlight.Other"))
        with self.assertRaisesRegex(ValueError, "token"): self.measure(original=[self.original_row(), other])

    def test_missing_generic_graph_or_il_seal_or_source_docs_rejected(self):
        for missing in ("generic_parameters", "declaring_generic_parameters"):
            row = self.player_row(); del row["signature"][missing]
            with self.assertRaises(ValueError): self.measure(player=[row])
        for changes in ({"il_sha256": None}, {"source_documents": []}):
            with self.assertRaises(ValueError): self.measure(player=[self.player_row(**changes)])

    def test_signature_flags_and_native_body_types_are_strict(self):
        row = self.original_row(); row["native_body"] = 1
        with self.assertRaises(ValueError): self.measure(original=[row])
        row = self.player_row(); row["signature"]["is_static"] = 1
        with self.assertRaises(ValueError): self.measure(player=[row])

    def test_non_bmp_constraint_sort_mismatch_is_rejected_conservatively(self):
        row = self.player_row()
        row["signature"]["generic_parameters"] = [{"flags": 0, "constraints": ["[Game.Runtime]\U00010000", "[Game.Runtime]\ue000"]}]
        with self.assertRaisesRegex(ValueError, "constraints"): self.measure(player=[row])

    def test_parity_claims_or_unknown_body_status_are_rejected(self):
        for key in ("native_byte_matching_verified", "gameplay_verified"):
            original, player = self.inventories(); player[key] = True
            with self.assertRaises(ValueError): codeprogress.measure_code_work(original, player, ["Game.Runtime"])
        with self.assertRaises(ValueError): self.measure(player=[self.player_row(body_status="verified-gameplay")])

    def test_assembly_csharp_scope_requires_exact_namespace_filters(self):
        row = self.original_row(assembly="Assembly-CSharp", signature=self.signature(owner="HardlightProject.Debug"))
        with self.assertRaises(ValueError): self.measure(original=[row], player=[], scope=["Assembly-CSharp"])
        report = self.measure(original=[row], player=[], scope=["Assembly-CSharp"], filters={"Assembly-CSharp": ["HardlightProject."]})
        self.assertEqual(1, report["totals"]["original_native_bodies"])

    def test_nested_firstpass_closures_are_in_scope_and_amplify_is_excluded(self):
        row = self.original_row(assembly="Assembly-CSharp-firstpass", signature=self.signature(owner="Hardlight.UIWidget+<>c__DisplayClass0"))
        report = self.measure(original=[row], player=[], scope=["Assembly-CSharp-firstpass"], filters={"Assembly-CSharp-firstpass": ["Hardlight."]})
        self.assertEqual(1, report["totals"]["original_native_bodies"])
        row = self.original_row(assembly="Assembly-CSharp", signature=self.signature(owner="AmplifyColor.Manager"))
        with self.assertRaises(ValueError): self.measure(original=[row], player=[], scope=["Assembly-CSharp"], filters={"Assembly-CSharp": ["HardlightProject."]})

    def test_output_is_deterministic_and_public_counts_contain_no_raw_method_or_paths(self):
        measurement = self.measure()
        report = codeprogress.public_counts(measurement, source="a" * 64, input_binary="b" * 64, input_metadata="c" * 64,
                                            scope_sha256="d" * 64, compiler_receipt_sha256="e" * 64)
        text = json.dumps(report, sort_keys=True) + codeprogress.render_readme_section(report)
        for forbidden in ("Sample.cs", "source_documents", "method_map", "native_pointer", "file_offset", "il_sha256", "<svg", ".cache/", "input/"):
            self.assertNotIn(forbidden, text)
        self.assertEqual(measurement, self.measure())
        self.assertIn("the game is not yet playable", text)

    def test_public_content_identities_cannot_be_paths(self):
        with self.assertRaises(ValueError):
            codeprogress.public_counts(self.measure(), source="/Users/example", input_binary="b" * 64, input_metadata="c" * 64,
                                       scope_sha256="d" * 64, compiler_receipt_sha256="e" * 64)


class NativeByteMapTests(unittest.TestCase):
    def fixture(self, folder, section_name=b"il2cpp", flags=0x80000400):
        path = folder / "native.dylib"; address = 0x1000; offset = 256; data = b"actual native method prefix"
        segment = struct.pack("<2I16s4Q4I", 0x19, 152, b"__TEXT", 0, 4096, 0, 4096, 5, 5, 1, 0)
        section = struct.pack("<16s16s2Q8I", section_name, b"__TEXT", address, len(data), offset, 4, 0, 0, flags, 0, 0, 0)
        header = struct.pack("<8I", 0xfeedfacf, 0x1000007, 3, 6, 1, len(segment + section), 0, 0)
        path.write_bytes(header + segment + section + bytes(offset - len(header + segment + section)) + data)
        row = {"native_body": True, "native_pointer": hex(address), "file_offset": offset, "native_prefix_sha256": hashlib.sha256(data[:16]).hexdigest()}
        return path, {"methods": [row]}

    def test_native_prefix_matches_exact_executable_section_mapping(self):
        with tempfile.TemporaryDirectory() as folder:
            path, report = self.fixture(Path(folder).resolve()); codeprogress.validate_native_bytes(report, path)

    def test_wrong_offset_pointer_changed_bytes_or_nonexecutable_section_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            folder = Path(folder).resolve()
            for mutation in ("offset", "pointer", "hash", "nonexec", "data"):
                path, report = self.fixture(folder, section_name=b"__const" if mutation == "data" else b"il2cpp", flags=0 if mutation == "nonexec" else 0x80000400)
                if mutation == "offset": report["methods"][0]["file_offset"] += 1
                if mutation == "pointer": report["methods"][0]["native_pointer"] = "0x9999"
                if mutation == "hash": report["methods"][0]["native_prefix_sha256"] = "0" * 64
                with self.assertRaises(ValueError): codeprogress.validate_native_bytes(report, path)

    def test_corrupted_native_header_or_command_bounds_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder).resolve() / "invalid.dylib"
            for data in (b"", b"bad!" + bytes(28), struct.pack("<8I", 0xfeedfacf, 0, 0, 6, 1, 152, 0, 0)):
                path.write_bytes(data)
                with self.assertRaises(ValueError): codeprogress._executable_sections(path)


class PublicSnapshotTests(unittest.TestCase):
    def report(self):
        return codeprogress.public_counts(CodeWorkTests().measure(), source="a" * 64, input_binary="b" * 64, input_metadata="c" * 64,
                                         scope_sha256="d" * 64, compiler_receipt_sha256="e" * 64)

    def test_injected_path_count_bool_or_unaccounted_counts_rejected(self):
        for change in ("path", "bool", "missing", "parity", "extra", "schema_bool", "schema_float"):
            report = self.report()
            if change == "path": report["assemblies"][0]["assembly"] = "Game.Runtime|/Users/private"
            if change == "bool": report["assemblies"][0]["implemented_source_bodies"] = True
            if change == "missing": report["totals"]["not_implemented"] += 1
            if change == "parity": report["behavior_parity_verified"] = True
            if change == "extra": report["source_path"] = "/Users/private"
            if change == "schema_bool": report["schema_version"] = True
            if change == "schema_float": report["schema_version"] = 1.0
            with self.assertRaises(ValueError): codeprogress.render_readme_section(report)

    def test_snapshot_source_or_scope_drift_rejected_offline(self):
        report = self.report()
        with patch.object(codeprogress, "_scope", return_value=(["Game.Runtime", "HLUnityCore.Runtime"], {}, "d" * 64)), patch.object(codeprogress, "artifact_fingerprint", return_value="a" * 64):
            self.assertEqual(report, codeprogress.validate_public_counts(report, Path("/example")))
        for source, scope in (("f" * 64, "d" * 64), ("a" * 64, "f" * 64)):
            with patch.object(codeprogress, "_scope", return_value=(["Game.Runtime", "HLUnityCore.Runtime"], {}, scope)), patch.object(codeprogress, "artifact_fingerprint", return_value=source):
                with self.assertRaises(ValueError): codeprogress.validate_public_counts(report, Path("/example"))


class SourcePlanTests(unittest.TestCase):
    def test_generated_hidden_and_nonruntime_documents_rejected(self):
        for relative in ("Assets/Recovered/Raw.cs", "Assets/StreamingAssets/Raw.cs", "Assets/.generated/Raw.cs", "Assets/Scripts/bin/Raw.cs",
                         "Packages/embedded/obj/Raw.cs", "Assets/Editor/Raw.cs", "Assets/Tests/Raw.cs", "Assets/../Raw.cs", "../Raw.cs", "Assets//Raw.cs"):
            self.assertFalse(codeprogress._maintained_document(relative), relative)
        self.assertTrue(codeprogress._maintained_document("Assets/Scripts/Body.cs"))
        self.assertTrue(codeprogress._maintained_document("Packages/com.project.lucid.library/Runtime/Body.cs"))


class WrapperPublicationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="lucid code guard ")
        self.base = Path(self.temporary.name).resolve(); self.root = self.base / "project with spaces"; self.root.mkdir()
        self.work = self.root / ".cache/project-lucid"; self.work.mkdir(parents=True)
        self.doc = self.root / "Assets/Scripts/Sample.cs"; self.doc.parent.mkdir(parents=True); self.doc.write_text("class Sample {}\n")
        config = {"schema_version": 2, "assemblies": ["Game.Runtime", "HLUnityCore.Runtime"],
                  "namespace_filters": {"Assembly-CSharp": ["HardlightProject."], "Assembly-CSharp-firstpass": ["Hardlight."]}}
        path = self.root / codeprogress.SCOPE_PATH; path.parent.mkdir(parents=True); path.write_text(json.dumps(config))
        self.thin, self.native = NativeByteMapTests().fixture(self.work)
        self.binary = self.work / "original fat.dylib"; self.binary.write_bytes(self.thin.read_bytes())
        self.metadata = self.work / "global-metadata.dat"; self.metadata.write_bytes(b"sealed original metadata")
        self.cecil = self.work / "Unity.Cecil.dll"; self.cecil.write_bytes(b"installed cecil")
        self.mvid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
        self.engine = [{"name": "cecil", "mvid": self.mvid, **codeprogress.playercode._file_record(self.cecil)}]
        self.dll = self.work / "compiled/Game.Runtime.dll"; self.dll.parent.mkdir(); self.dll.write_bytes(b"genuine returned module")
        self.pdb = self.dll.with_suffix(".pdb"); self.pdb.write_bytes(b"genuine returned symbols")
        self.module = {"assembly_name": "Game.Runtime", "mvid": self.mvid, **codeprogress.playercode._file_record(self.dll)}
        self.evidence = {"receipt_raw": b"sealed actual compiler receipt", "staged_raw": b"sealed pending", "context_raw": b"sealed context",
                         "receipt_sha256": "a" * 64, "staged": {"modules": [self.module],
                         "files": [codeprogress.playercode._file_record(self.dll), codeprogress.playercode._file_record(self.pdb)],
                         "inputs": [{"kind": "source", **codeprogress.playercode._file_record(self.doc)}]}}
        self.prior = self.work / "code-progress/latest-macos.json"; self.prior.parent.mkdir(); self.prior.write_bytes(b"previous validated receipt bytes")
        self.failure = None; self.runs = []
        self.stack = ExitStack()
        self.stack.enter_context(patch.object(bootstrap, "REPO_ROOT", self.root)); self.stack.enter_context(patch.object(bootstrap, "CACHE_ROOT", self.work))
        self.stack.enter_context(patch.object(codeprogress, "artifact_fingerprint", return_value="a" * 64))
        self.stack.enter_context(patch.object(codeprogress, "find_editor", return_value=self.cecil))
        self.stack.enter_context(patch.object(codeprogress.playercode, "_engine_identity", side_effect=lambda _: copy.deepcopy(self.engine)))
        self.stack.enter_context(patch.object(codeprogress.playerschema, "_player_evidence", side_effect=self.compiler_evidence))
        self.stack.enter_context(patch.object(codeprogress.recovery, "_resolve_input", return_value=(self.root, self.binary, self.metadata, codeprogress.UNITY_VERSION)))
        self.stack.enter_context(patch.object(codeprogress.recovery, "_thin_binary", return_value=(self.thin, "x86_64")))
        self.stack.enter_context(patch.object(codeprogress, "_build_reader", side_effect=self.build_reader))
        self.stack.enter_context(patch.object(codeprogress.playercode, "_pe_identity", return_value={"mvid": self.mvid, "assembly_name": "Cpp2IL.Core"}))
        self.stack.enter_context(patch.object(codeprogress.subprocess, "run", side_effect=self.reader_process))

    def tearDown(self):
        self.stack.close(); self.temporary.cleanup()

    def build_reader(self, root, work, run, engine):
        self.run = run; self.runs.append(run)
        output = run / "reader"; output.mkdir(); reader = output / "ProjectLucid.CodeProgress.dll"; reader.write_bytes(b"pinned reader")
        core = output / "Cpp2IL.Core.dll"; core.write_bytes(b"pinned cpp core")
        host = work / "tools/dotnet/dotnet"; host.parent.mkdir(parents=True, exist_ok=True); host.write_bytes(b"pinned host")
        return {"host": codeprogress.playercode._file_record(host), "path": str(reader), "sha256": codeprogress.sha256_file(reader), "sources": [],
                "outputs": codeprogress.playerschema._tree_records(output, work)}

    def compiler_evidence(self, *args):
        result = copy.deepcopy(self.evidence)
        self.evidence_calls += 1
        if self.evidence_calls > 1:
            if self.failure == "compiler": result["receipt_raw"] = b"changed actual compiler receipt"
            if self.failure == "late_document": self.doc.write_bytes(b"late changed source")
            if self.failure == "late_output": (self.run / "native.json").write_bytes(b"forged output")
        return result

    def reader_process(self, command, **kwargs):
        mode = command[2]; context_path, output = Path(command[3]), Path(command[4]); raw = context_path.read_bytes(); context = json.loads(raw)
        if self.failure == "reader_exit": return SimpleNamespace(returncode=1)
        if self.failure == "interrupted": raise subprocess.TimeoutExpired(command, 300)
        helper = CodeWorkTests(); orig, player = helper.inventories()
        orig["cpp2il_pin"] = codeprogress.native._PIN; orig["modules"] = [codeprogress.playercode._file_record(self.thin), codeprogress.playercode._file_record(self.metadata)]
        orig["methods"][0].update(self.native["methods"][0])
        record = codeprogress.playercode._file_record(self.doc)
        player["methods"][0]["source_documents"] = [{"relative_path": "Assets/Scripts/Sample.cs", "size": record["size"], "sha256": record["sha256"],
                                                       "pdb_checksum_algorithm": "SHA256", "pdb_checksum": record["sha256"]}]
        player["modules"] = [{"assembly": "Game.Runtime", "mvid": self.mvid, "dll": codeprogress.playercode._file_record(self.dll), "pdb": codeprogress.playercode._file_record(self.pdb)}]
        if self.failure == "source_plan": self.evidence["staged"]["inputs"] = []
        report = {"schema_version": 1, "status": "pending-wrapper-verification", "identity_nonce": context["identity_nonce"],
                  "source_fingerprint": "a" * 64, "context_sha256": hashlib.sha256(raw).hexdigest(),
                  "recovered_behavior_verified": False, "native_byte_matching_verified": False, "gameplay_verified": False,
                  "inventory": orig if mode == "native" else player,
                  "reader": {"sha256": codeprogress.sha256_file(Path(command[1])),
                             "cpp2il_core": {"sha256": codeprogress.sha256_file(Path(command[1]).with_name("Cpp2IL.Core.dll")), "mvid": self.mvid},
                             "cecil": {"sha256": codeprogress.sha256_file(self.cecil), "mvid": self.mvid}}}
        if self.failure == "nonce": report["identity_nonce"] = "0" * 32
        if self.failure == "approval": report["gameplay_verified"] = True
        output.write_text(json.dumps(report))
        if mode == "player":
            if self.failure == "input": self.binary.write_bytes(b"changed original")
            if self.failure == "host": Path(command[0]).write_bytes(b"changed host")
            if self.failure == "reader_tree": Path(command[1]).with_name("extra.dll").write_bytes(b"unsealed")
            if self.failure == "context": context_path.write_bytes(b"changed context")
        return SimpleNamespace(returncode=0)

    def invoke(self):
        self.evidence_calls = 0
        return codeprogress.run_code_progress(self.root, self.work, self.root / "input/game with spaces.app", "macos")

    def assert_retained(self):
        self.assertEqual(b"previous validated receipt bytes", self.prior.read_bytes())
        self.assertFalse((self.work / "locks/code-progress.lock").exists())

    def test_success_publishes_only_after_all_source_input_and_module_checks(self):
        result = self.invoke()
        self.assertEqual("ready", result["status"])
        self.assertEqual(1, result["public"]["totals"]["implemented_source_bodies"])
        self.assertFalse(result["behavior_parity_verified"])
        self.assertEqual(result, json.loads(self.prior.read_bytes()))
        self.assertFalse((self.work / "locks/code-progress.lock").exists())

    def test_error_or_interruption_does_not_replace_previous_validated_output(self):
        for failure in ("reader_exit", "interrupted", "nonce", "approval", "compiler", "late_output", "input", "host", "reader_tree", "context", "late_document"):
            with self.subTest(failure=failure):
                self.failure = failure
                self.prior.write_bytes(b"previous validated receipt bytes")
                with self.assertRaises((ValueError, subprocess.TimeoutExpired)): self.invoke()
                self.assert_retained()
                self.binary.write_bytes(self.thin.read_bytes()); self.doc.write_text("class Sample {}\n")

    def test_current_source_stale_on_final_recheck_cannot_publish(self):
        with patch.object(codeprogress, "artifact_fingerprint", side_effect=["a" * 64, "a" * 64, "f" * 64]):
            with self.assertRaisesRegex(ValueError, "Source changed before"): self.invoke()
        self.assert_retained()

    def test_uncompiled_pdb_document_not_in_actual_compiler_source_plan_rejected(self):
        self.evidence["staged"]["inputs"] = []
        with self.assertRaisesRegex(ValueError, "source plan"): self.invoke()
        self.assert_retained()

    def test_current_compiler_required_before_any_new_staging(self):
        with patch.object(codeprogress.playerschema, "_player_evidence", side_effect=ValueError("stale compiled source")):
            with self.assertRaisesRegex(ValueError, "stale compiled source"): self.invoke()
        self.assertFalse(self.runs); self.assert_retained()

    def test_foreign_work_destination_or_symlink_rejected_without_touching_previous(self):
        with self.assertRaises(ValueError): codeprogress.run_code_progress(self.root, self.root / "Assets", self.root, "macos")
        link = self.work / "code-progress/runs"; target = self.base / "foreign"; target.mkdir(); link.symlink_to(target, target_is_directory=True)
        with self.assertRaises(ValueError): self.invoke()
        self.assert_retained(); self.assertEqual([], list(target.iterdir()))

    def test_unsupported_target_preserves_previous(self):
        with self.assertRaises(ValueError): codeprogress.run_code_progress(self.root, self.work, self.root, "android")
        self.assert_retained()


if __name__ == "__main__":
    unittest.main()

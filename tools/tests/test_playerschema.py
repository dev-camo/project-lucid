"""Publication guards for metadata evidence; fixtures cannot approve original assets."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lucidlib import bootstrap, bindings, playercode, playerschema
import test_playercode as fixtures
managed_fixture = fixtures.managed_fixture


class PlayerSchemaTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.PlayerCodeTests("runTest")
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.root, self.work, self.editor = self.fixture.root, self.fixture.work, self.fixture.editor
        with patch.object(playercode.subprocess, "run", side_effect=lambda command, **kwargs: self.fixture.emit(command)):
            result = playercode.run_player_code(self.root, self.work, "macos")
        self.player_latest = Path(result["report_path"])
        receipt = json.loads(self.player_latest.read_bytes())
        self.compiled_pending = Path(receipt["staged_report_path"])
        staged = json.loads(self.compiled_pending.read_bytes())
        managed = self.editor.parents[1] / "Managed"
        for name in ("UnityEngine.CoreModule", "UnityEngine.SharedInternalsModule", "netstandard"):
            path = managed / (name + ".dll")
            path.write_bytes(managed_fixture(name)[0])
            staged["inputs"].append(dict(kind="precompiled_reference", **playercode._file_record(path)))
        digest = playercode._input_identity(staged["inputs"])
        staged.update(inputs_fingerprint_before=digest, inputs_fingerprint_after=digest)
        self.compiled_pending.write_text(json.dumps(staged))
        receipt.update(staged, status="compiled", identity_status="complete",
                       staged_report_sha256=hashlib.sha256(self.compiled_pending.read_bytes()).hexdigest())
        self.player_latest.write_text(json.dumps(receipt))
        self.latest = self.work / "player-schema/latest-macos-zone-theme-v1.json"
        self.latest.parent.mkdir()
        self.latest.write_bytes(b"previous valid metadata receipt\n")
        self.old = self.latest.read_bytes()
        self.reader_change = None
        self.schema_change = None
        self.patches = [patch.object(playerschema, "find_editor", return_value=self.editor),
                        patch.object(playerschema, "_build_reader", side_effect=self.build),
                        patch.object(playerschema.subprocess, "run", side_effect=self.emit)]
        for p in self.patches:
            p.start(); self.addCleanup(p.stop)

    def build(self, root, work, run, engine):
        output = run / "reader"; output.mkdir()
        binary = output / "ProjectLucid.PlayerSchema.dll"
        binary.write_bytes(managed_fixture("ProjectLucid.PlayerSchema")[0])
        for entry in engine:
            if entry["name"] in ("cecil", "serialization"):
                (output / Path(entry["path"]).name).write_bytes(Path(entry["path"]).read_bytes())
        host = work / "tools/dotnet/dotnet"
        host.parent.mkdir(parents=True, exist_ok=True)
        host.write_bytes(b"sealed host fixture")
        source = root / "Assets/Fixture.cs"
        return {"host": playercode._file_record(host), "sources": [playercode._file_record(source)],
                "binary": dict(playercode._file_record(binary), **playercode._pe_identity(binary)),
                "output": playerschema._tree_records(output, work), "env": {}, "log": str(run / "build.log")}

    @staticmethod
    def reference(assembly, name):
        return {"kind": "named", "assembly": assembly, "reflection_full_name": name, "canonical_name": name}

    def inventory(self, manifest, raw, binary):
        modules = {m["assembly_name"]: m for m in manifest["modules"]}
        groups = {}
        for i, (assembly, name) in enumerate(sorted(playerschema.ALLOWED)):
            record = {"assembly": assembly, "full_name": name, "name": name.rsplit(".", 1)[-1],
                      "namespace": name.rsplit(".", 1)[0], "token": "0x02%06x" % (i + 1),
                      "attributes": 1, "is_value_type": name == "UnityEngine.Color", "is_enum": False,
                      "is_abstract": False, "base_type": None, "schema_complete": True,
                      "custom_attributes": [], "custom_attributes_complete": True, "generic_parameters": [],
                      "fields": [], "builtin_serializer_eligibility": name == "UnityEngine.Color",
                      "module_provenance": modules[assembly]}
            if name.endswith("AssetReferenceT`1"):
                record["generic_parameters"] = [{"name": "TObject", "index": 0, "attributes": 0, "constraints_complete": True,
                    "constraints": [self.reference("UnityEngine.CoreModule", "UnityEngine.Object")]}]
            groups.setdefault(assembly, []).append(record)
        assemblies = [{"name": a, "module": modules[a], "types": types} for a, types in sorted(groups.items())]
        zone = next(t for a in assemblies for t in a["types"] if t["full_name"] == "HardlightProject.ZoneThemeOverride")
        field_token = "0x04000001"
        blob = bytes.fromhex("0100166d5f7a6f6e654772616469656e744f766572726964650eff0000")
        value = {"kind": "primitive", "type": "IL2CPP_TYPE_STRING", "value": None, "value_complete": True,
                 "string_length": -1, "raw_encoding_sha256": hashlib.sha256(b"\xff").hexdigest(),
                 "ecma_tag": 14, "ecma_boxed_tag": 14, "ecma_boxed": True}
        attribute = {"assembly": "HLUnityCore.Runtime", "full_name": "Hardlight.ShowIfAttribute",
                     "arguments": [{"kind": "primitive", "type": "IL2CPP_TYPE_STRING", "value": "m_zoneGradientOverride", "value_complete": True}, value],
                     "fields": [], "properties": [], "ecma_blob_hex": blob.hex(), "ecma_blob_sha256": hashlib.sha256(blob).hexdigest(),
                     "ecma_decoding_complete": True,
                     "constructor_parameter_types": [self.reference("netstandard", "System.String"), self.reference("netstandard", "System.Object")],
                     "ecma_provenance": {"module_path": modules["Game.Runtime"]["path"], "module_sha256": modules["Game.Runtime"]["sha256"],
                                         "module_mvid": modules["Game.Runtime"]["mvid"], "owner_token": field_token}}
        zone["fields"] = [{"name": "m_backgroundStartColour", "token": field_token, "attributes": 6,
                          "field_type": self.reference("UnityEngine.CoreModule", "UnityEngine.Color"),
                          "schema_complete": True, "custom_attributes_complete": True, "custom_attributes": [attribute],
                          "is_public": True, "is_static": False, "is_const": False, "is_readonly": False,
                          "non_serialized": False, "serialize_field": False, "serialize_reference": False,
                          "unity_serialization_candidate": True, "unity_type_eligibility_verified": True, "will_unity_serialize": True,
                          "default_value_complete": True, "has_default_value": False, "default_value": None}]
        report = {k: manifest[k] for k in ("profile", "target", "nonce", "source_fingerprint", "player_receipt_sha256", "project_root", "work_root", "run")}
        report.update(schema_version=1, command="player-schema", status="pending-native-verification", identity_status="pending-wrapper",
                      manifest_sha256=hashlib.sha256(raw).hexdigest(), metadata_graph_complete=True, references_modified=False,
                      player_schema_verified=False, remap_approved=False, gameplay_verified=False,
                      compiler_response_files_verified=False, resolver_provenance="metadata-resolution-over-sealed-plan-candidates",
                      errors=[], assemblies=assemblies, type_count=len(playerschema.ALLOWED), assembly_count=len(assemblies),
                      closure_edges=[{"type": a + ":" + t, "reason": "profile-root"} for a, t in sorted(playerschema.ROOTS)])
        for field, engine_name in (("reader", "cecil"), ("serializer", "serialization")):
            entry = next(e for e in manifest["engine"] if e["name"] == engine_name)
            report[field] = {"path": str(Path(binary).parent / Path(entry["path"]).name), "sha256": entry["sha256"], "mvid": entry["mvid"]}
        return report

    def emit(self, command, **kwargs):
        manifest_path, pending = Path(command[-2]), Path(command[-1])
        raw = manifest_path.read_bytes(); manifest = json.loads(raw)
        report = self.inventory(manifest, raw, command[1])
        if self.schema_change: self.schema_change(report)
        pending.write_text(json.dumps(report))
        if self.reader_change: self.reader_change(manifest, command)
        return subprocess.CompletedProcess(command, 0)

    def run_schema(self):
        return playerschema.run_player_schema(self.root, self.work, "macos")

    def rejects(self, match=None):
        with self.assertRaises((ValueError, OSError, bindings.LayoutError)) as result: self.run_schema()
        if match: self.assertRegex(str(result.exception), match)
        self.assertEqual(self.old, self.latest.read_bytes())
        self.assertFalse((self.work / "locks/player-schema.lock").exists())

    def test_valid_metadata_is_never_layout_or_asset_approval(self):
        result = self.run_schema()
        self.assertEqual("metadata-ready", result["status"])
        receipt = json.loads(self.latest.read_bytes())
        for flag in ("player_schema_verified", "remap_approved", "gameplay_verified", "compiler_response_files_verified"):
            self.assertIs(False, receipt[flag])
        self.assertEqual("netstandard", next(t for a in receipt["assemblies"] for t in a["types"]
            if t["full_name"] == "HardlightProject.ZoneThemeOverride")["fields"][0]["custom_attributes"][0]["constructor_parameter_types"][1]["assembly"])

    def test_current_source_required(self):
        self.fixture.source.write_text("// new implementation\n"); self.rejects("current genuine")

    def test_source_only_plan_is_not_compilation(self):
        receipt = json.loads(self.player_latest.read_bytes()); receipt["compilation_status"] = "planned"
        self.player_latest.write_text(json.dumps(receipt)); self.rejects("staged")

    def test_staged_output_hash_changed(self):
        r = json.loads(self.player_latest.read_bytes()); Path(r["modules"][0]["path"]).write_bytes(b"not the compiled PE")
        self.rejects("changed|forged")

    def test_compiler_context_changed(self):
        (self.compiled_pending.parent / "context.txt").write_bytes(b"forged context")
        self.rejects("context")

    def test_staged_receipt_changed(self):
        self.compiled_pending.write_text("{}"); self.rejects("staged")

    def test_resolver_duplicate_assembly_rejected(self):
        evidence = playerschema._player_evidence(self.root, self.work, "macos", playercode.artifact_fingerprint(self.root), playercode._engine_identity(self.editor))
        evidence["staged"]["inputs"].append(dict(kind="precompiled_reference", **evidence["staged"]["modules"][0]))
        with self.assertRaisesRegex(ValueError, "Ambiguous"): playerschema._modules(evidence)

    def test_source_drift_in_child_retains_latest(self):
        self.reader_change = lambda m, c: self.fixture.source.write_text("// changed in reader\n")
        self.rejects("changed|forged|stale")

    def test_engine_drift_in_child_retains_latest(self):
        self.reader_change = lambda m, c: self.editor.write_bytes(b"different installed engine")
        self.rejects("context|changed|stale")

    def test_reference_drift_in_child_retains_latest(self):
        self.reader_change = lambda m, c: Path(next(r["path"] for r in m["modules"] if r["kind"] == "sealed_plan_reference")).write_bytes(b"altered reference")
        self.rejects("changed|forged")

    def test_reader_binary_drift_in_child_retains_latest(self):
        self.reader_change = lambda m, c: Path(c[1]).write_bytes(b"altered reader")
        self.rejects("changed")

    def test_final_source_check_catches_late_mutation(self):
        actual = playerschema.artifact_fingerprint
        calls = []
        def fingerprint(root):
            calls.append(root)
            if len(calls) == 7: self.fixture.source.write_text("// changed immediately before publication\n")
            return actual(root)
        with patch.object(playerschema, "artifact_fingerprint", side_effect=fingerprint):
            self.rejects("immediately before")
        self.assertEqual(7, len(calls), "Late mutation must reach the final publication check")

    def test_source_drift_during_final_engine_hash_is_caught(self):
        actual = playercode._engine_identity
        calls = []
        def identity(editor):
            value = actual(editor); calls.append(editor)
            if len(calls) == 3: self.fixture.source.write_text("// changed during final engine identity\n")
            return value
        with patch.object(playercode, "_engine_identity", side_effect=identity):
            self.rejects("final installed engine")
        self.assertEqual(3, len(calls), "Mutation must happen inside the final engine identity check")

    def test_reader_build_failure_retains_latest(self):
        with patch.object(playerschema, "_build_reader", side_effect=ValueError("Pinned reader build failed")):
            self.rejects("build failed")

    def test_pending_symlink_is_not_followed(self):
        def change(manifest, command):
            pending = Path(command[-1]); other = pending.with_name("other.json")
            pending.rename(other); pending.symlink_to(other)
        self.reader_change = change; self.rejects("symbolic link")

    def test_pending_duplicate_json_keys_are_rejected(self):
        def change(manifest, command):
            pending = Path(command[-1])
            pending.write_text(pending.read_text()[:-1] + ', "status":"pending-native-verification"}')
        self.reader_change = change; self.rejects("Duplicate")

    def test_unapproved_closure_rejected(self):
        self.schema_change = lambda r: r["assemblies"][0]["types"][0].update(full_name="HardlightProject.FakeManager")
        self.rejects("profile")

    def test_omitted_dependency_rejected(self):
        def change(r):
            a = next(a for a in r["assemblies"] if a["name"] == "netstandard")
            a["types"] = [t for t in a["types"] if t["full_name"] != "System.Object"]
            r["type_count"] -= 1
        self.schema_change = change; self.rejects("profile")

    def test_complete_constraints_required(self):
        def change(r):
            t = next(t for a in r["assemblies"] for t in a["types"] if t["full_name"].endswith("AssetReferenceT`1"))
            t["generic_parameters"][0]["constraints_complete"] = False
        self.schema_change = change; self.rejects("constraints")

    def test_no_generic_null_normalization(self):
        self.schema_change = lambda r: r["assemblies"][0]["types"][0]["fields"][0]["custom_attributes"][0]["arguments"][1].update(kind="null")
        self.rejects("null string")

    def test_boxed_string_tag_required(self):
        self.schema_change = lambda r: r["assemblies"][0]["types"][0]["fields"][0]["custom_attributes"][0]["arguments"][1].update(ecma_boxed_tag=81)
        self.rejects("null string")

    def test_blob_owner_mvid_required(self):
        self.schema_change = lambda r: r["assemblies"][0]["types"][0]["fields"][0]["custom_attributes"][0]["ecma_provenance"].update(module_mvid="wrong")
        self.rejects("provenance")

    def test_field_serialization_flags_required(self):
        self.schema_change = lambda r: r["assemblies"][0]["types"][0]["fields"][0].update(is_public=False)
        self.rejects("flags")

    def test_module_mvid_required(self):
        self.schema_change = lambda r: r["assemblies"][0].update(module=dict(r["assemblies"][0]["module"], mvid="different"))
        self.rejects("sealed")

    def test_metadata_cannot_self_approve(self):
        self.schema_change = lambda r: r.update(remap_approved=True)
        self.rejects("approved")

    def test_plan_cannot_claim_actual_compiler_resolution(self):
        self.schema_change = lambda r: r.update(compiler_response_files_verified=True)
        self.rejects("approved")

    def test_child_failure_retains_latest(self):
        with patch.object(playerschema.subprocess, "run", return_value=subprocess.CompletedProcess([], 1)):
            self.rejects("reader failed")

    def test_child_interruption_retains_latest(self):
        with patch.object(playerschema.subprocess, "run", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt): self.run_schema()
        self.assertEqual(self.old, self.latest.read_bytes())
        self.assertFalse((self.work / "locks/player-schema.lock").exists())

    def test_fresh_only_run_retains_existing_directory(self):
        from uuid import UUID
        nonce = "1234567890abcdef1234567890abcdef"
        run = self.work / "player-schema/runs" / nonce; run.mkdir(parents=True)
        marker = run / "pending.json"; marker.write_bytes(b"old unrelated output")
        with patch.object(playerschema.uuid, "uuid4", return_value=UUID(hex=nonce)): self.rejects()
        self.assertEqual(b"old unrelated output", marker.read_bytes())
        self.assertFalse((run / "wrapper-failure.json").exists())

    def test_symlink_latest_rejected(self):
        other = self.root / "unchanged.txt"; other.write_bytes(self.old)
        self.latest.unlink(); self.latest.symlink_to(other)
        with self.assertRaisesRegex(ValueError, "symbolic link"): self.run_schema()
        self.assertEqual(self.old, other.read_bytes())

    def test_no_arbitrary_work_directory(self):
        for path in (self.root, self.root / "Assets", self.root / "input", self.root / "outside"):
            with self.subTest(path=path), self.assertRaises(ValueError): playerschema.run_player_schema(self.root, path, "macos")

    def test_profile_is_exact(self):
        with self.assertRaises(ValueError): playerschema.run_player_schema(self.root, self.work, "macos", "all-types")


if __name__ == "__main__": unittest.main()

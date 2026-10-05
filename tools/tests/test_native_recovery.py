"""Opt-in integration checks against the pinned Core API and supplied release.

Build the harness, then set LUCID_NATIVE_INTEGRATION=1 to run these checks. All
mutated fixtures and evidence stay in fresh recovery-cache directories.
"""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import unittest
import uuid
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / ".cache/project-lucid"
PIN = "b5ad444b82267cb1e4b88b8b373c008105bdea52"
HARNESS = CACHE / "native-recovery/build/bin/Release/net10.0/ProjectLucid.NativeRecovery.dll"
DOTNET = CACHE / "tools/dotnet/dotnet"
METADATA = ROOT / "input/SonicDreamTeam.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"


@unittest.skipUnless(os.environ.get("LUCID_NATIVE_INTEGRATION") == "1", "requires pinned native-recovery build and supplied release")
class NativeRecoveryIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        pointer = CACHE / "native-recovery/latest-build.json"
        cls.harness = Path(json.loads(pointer.read_text())["path"]) if pointer.is_file() else HARNESS
        if not cls.harness.resolve().is_relative_to(CACHE.resolve()):
            raise RuntimeError("Native integration harness must remain in the generated cache")
        for file in (cls.harness, DOTNET, METADATA):
            if not file.is_file():
                raise RuntimeError(f"Required integration input is absent: {file}")
        candidates = sorted((CACHE / "recovery/inputs").glob("*.x86_64.dylib"))
        if len(candidates) != 1:
            raise RuntimeError("Integration checks need one hash-verified x86_64 analysis slice")
        cls.binary = candidates[0]
        original = ROOT / "input/SonicDreamTeam.app/Contents/Frameworks/GameAssembly.dylib"
        # Extract the original ARM64 slice to a fresh cache child; never rewrite
        # the supplied universal binary or depend on an agent-generated probe.
        cls.architecture_dir = CACHE / "native-recovery/tests" / uuid.uuid4().hex
        cls.architecture_dir.mkdir(parents=True)
        cls.arm64 = cls.architecture_dir / "GameAssembly.arm64.dylib"
        with original.open("rb") as stream:
            magic, count = struct.unpack(">II", stream.read(8))
            if magic != 0xcafebabe or not 1 <= count <= 64:
                raise RuntimeError("Integration release requires the original FAT32 Mach-O")
            rows = [struct.unpack(">iiIII", stream.read(20)) for _ in range(count)]
            arm = next(row for row in rows if row[0] == 0x0100000c)
            stream.seek(arm[2])
            with cls.arm64.open("wb") as output:
                remaining = arm[3]
                while remaining:
                    block = stream.read(min(1024 * 1024, remaining))
                    if not block:
                        raise RuntimeError("Truncated ARM64 input")
                    output.write(block)
                    remaining -= len(block)
        cls.environment = os.environ.copy()
        cls.environment.update(DOTNET_ROOT=str(DOTNET.parent), DOTNET_NOLOGO="1",
                               DOTNET_TieredCompilation="0", COMPlus_TieredCompilation="0",
                               DOTNET_CLI_HOME=str(CACHE / "dotnet-home"),
                               NUGET_PACKAGES=str(CACHE / "nuget-packages"))

    def setUp(self):
        self.directory = CACHE / "native-recovery/tests" / uuid.uuid4().hex
        self.directory.mkdir(parents=True)
        self.output = self.directory / "output"

    def invoke(self, command="schemas", assembly="Game.Runtime", token=None, **overrides):
        options = {"binary": self.binary, "metadata": METADATA, "unity-version": "2022.3.54f1",
                   "output": self.output}
        if assembly is not None:
            options["assembly"] = assembly
        if token is not None:
            options["token"] = token
        options.update(overrides)
        args = [str(DOTNET), str(self.harness), command]
        for key, value in options.items():
            args.extend(["--" + key, str(value)])
        result = subprocess.run(args, env=self.environment, capture_output=True, text=True, timeout=120)
        (self.directory / "process.log").write_text(result.stdout + result.stderr)
        return result

    def assert_failed_without_output(self, result, message):
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn(message, result.stderr)
        self.assertFalse(self.output.exists())

    def test_real_mesh_method_produces_bounded_native_and_raw_isil(self):
        result = self.invoke("method", token="0x060039b9")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["cpp2il_pin"], PIN)
        self.assertEqual(report["method"]["declaring_type"], "HardlightProject.MeshGenerationUtilities")
        self.assertEqual(report["method"]["name"], "CreateMeshPyramid")
        self.assertEqual(report["method"]["native_pointer"], "0x6b27f0")
        native = (self.output / "native.bin").read_bytes()
        self.assertEqual(len(native), 320)
        self.assertEqual(hashlib.sha256(native).hexdigest(), "e1c6d109a46ab25b86640bcdfe38d6ed9341d8fdbdedaa0f417f244fa550ef48")
        self.assertIn("jmp near ptr 00000000006B2930h", (self.output / "native.txt").read_text())
        self.assertGreater(report["isil_instruction_count"], 0)
        self.assertEqual(len((self.output / "isil.txt").read_text().splitlines()), report["isil_instruction_count"])
        self.assertFalse(report["analysis_pipeline_invoked"])
        self.assertFalse(report["managed_semantics_recovered"])
        self.assertFalse(report["native_addresses_verified"])

    def recovered_threshold(self, binary):
        result = self.invoke("recover-method", assembly="HLUnityCore.Runtime", token="0x06001006", binary=binary)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertTrue(report["experimental"])
        self.assertTrue(report["native_addresses_verified"])
        self.assertTrue(report["analysis_pipeline_invoked"])
        self.assertFalse(report["managed_semantics_recovered"])
        self.assertFalse(report["generated_runtime_installation"])
        self.assertFalse(report["original_native_body_executed"])
        self.assertEqual(report["counts"], {"declarations": 1, "il_emitted": 1, "executable": 1,
                                         "bounded_behavior_verified": 1, "maintained_implementations": 0})
        self.assertTrue(all(report["recovery_stages"].values()))
        variants = {row["variant"]: row for row in report["variants"]}
        baseline, corrected = variants["baseline"], variants["corrected"]
        self.assertEqual(baseline["max_stack"], 0)
        self.assertFalse(baseline["executable"])
        self.assertEqual(baseline["exception_type"], "System.InvalidProgramException")
        self.assertEqual(corrected["max_stack"], 2)
        self.assertTrue(corrected["initialized_locals"])
        self.assertTrue(corrected["executable"])
        self.assertEqual(corrected["execution_status"], "passed")
        self.assertEqual(corrected["checks"], 10343)
        for variant in variants.values():
            artifact = Path(variant["assembly"])
            self.assertTrue(artifact.resolve().is_relative_to(self.output.resolve()))
            self.assertEqual(hashlib.sha256(artifact.read_bytes()).hexdigest(), variant["assembly_sha256"])
        correction = report["emission_corrections"]
        self.assertFalse(correction["upstream_source_modified"])
        self.assertTrue(correction["initialized_typed_zero"])
        self.assertEqual(correction["int64_subtraction_temporaries"], 2)
        self.assertGreater(correction["int64_comparison_zero_operands"], 0)
        return report, variants

    def test_x64_threshold_baseline_and_corrected_execution_are_distinct(self):
        report, variants = self.recovered_threshold(self.binary)
        self.assertEqual(report["native_symbol"]["address"], "0x1b20ac0")
        self.assertEqual(report["native_symbol"]["native_sha256"], "58275e261cf2c9bde993c15a82dc3e41d0e0fc9b7d9dbe498303e575a5e6fbcb")
        self.assertEqual(variants["maxstack-only"]["checks"], 10343)
        self.assertEqual(variants["maxstack-only"]["execution_status"], "passed")
        self.assertEqual(report["emission_corrections"]["boolean_temporaries"], 1)

    def test_arm64_threshold_requires_boolean_propagation_as_well_as_maxstack(self):
        report, variants = self.recovered_threshold(self.arm64)
        self.assertEqual(report["native_symbol"]["address"], "0x1b25ba8")
        self.assertEqual(report["native_symbol"]["native_sha256"], "3411df1222f5da945c4fdbdfd68dd6959e16ec98a6392cf761e1eab456d7e510")
        wrong = variants["maxstack-only"]
        self.assertTrue(wrong["executable"])
        self.assertEqual(wrong["execution_status"], "failed")
        self.assertEqual(wrong["counterexample"], {"previous": -(2**63), "threshold": -(2**63) + 1,
                                                 "current": -(2**63) + 1, "actual": False, "expected": True})
        self.assertEqual(report["emission_corrections"]["boolean_temporaries"], 3)

    def test_experimental_recovery_rejects_unproven_mesh_selection(self):
        self.assert_failed_without_output(self.invoke("recover-method", token="0x060039b9"), "supports only the original static TimeUtils threshold")

    def test_experimental_recovery_refuses_changed_native_bytes(self):
        binary = self.directory / "changed.dylib"
        shutil.copyfile(self.binary, binary)
        with binary.open("r+b") as stream:
            stream.seek(0x1b20ac0)
            stream.write(b"\x90")
        result = self.invoke("recover-method", assembly="HLUnityCore.Runtime", token="0x06001006", binary=binary)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertEqual(report["status"], "incomplete")
        self.assertIn("native body differs", report["recovery_error"])
        self.assertFalse(report["recovery_stages"]["il_emitted"])
        self.assertEqual(list(self.output.rglob("*.dll")), [])

    def test_experimental_recovery_requires_the_exact_native_symbol(self):
        data = self.binary.read_bytes()
        symbol = b"_TimeUtils_HasTimePassedThresholdSinceLastCheck_mA1AF9ACB8472CCE7736F80B11BB3ECBA9344A729\0"
        self.assertEqual(data.count(symbol), 1)
        binary = self.directory / "renamed-symbol.dylib"
        binary.write_bytes(data.replace(symbol, b"_X" + symbol[2:]))
        result = self.invoke("recover-method", assembly="HLUnityCore.Runtime", token="0x06001006", binary=binary)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertEqual(report["status"], "incomplete")
        self.assertIn("exact original Mach-O symbol", report["recovery_error"])
        self.assertFalse(report["analysis_pipeline_invoked"])
        self.assertFalse(report["native_addresses_verified"])
        self.assertEqual(list(self.output.rglob("*.dll")), [])

    def test_boolean_inference_and_unsupported_graph_guards(self):
        # Compile a cache-only runner against the actual pinned harness API. The
        # synthetic graph checks exercise inference/refusal, independently of
        # the positive native threshold graph and its expected output.
        source = r'''
using System.Reflection;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.ISIL;
new Cpp2IlCorePlugin().OnLoad();
Cpp2IlApi.InitializeLibCpp2Il(args[0], args[1], UnityVersion.Parse("2022.3.54f1"), false);
var types = Cpp2IlApi.CurrentAppContext!.SystemTypes;
var helper = Assembly.LoadFrom(args[2]).GetType("ProjectLucid.NativeRecovery.ExperimentalRecovery")!;
var flags = BindingFlags.Static | BindingFlags.NonPublic;
object? Invoke(string name, params object[] values) => helper.GetMethod(name, flags)!.Invoke(null, values);
void Reject(string name, params object[] values) {
    try { Invoke(name, values); } catch (TargetInvocationException e) when (e.InnerException is InvalidDataException) { return; }
    throw new Exception("Expected refusal: " + name);
}
LocalVariable Local(string name, Cpp2IL.Core.Model.Contexts.TypeAnalysisContext? type = null) => new(name, new Register(null, name), type);
var b = Local("knownBool", types.SystemBooleanType);
var i = Local("knownInt64", types.SystemInt64Type);
LocalVariable first = Local("first"), second = Local("second"), both = Local("both"), mixed = Local("mixed"), untyped = Local("untyped"),
    bothOr = Local("bothOr"), bothXor = Local("bothXor"), difference = Local("difference");
Instruction[] chain = [new(0, OpCode.Not, second, first), new(1, OpCode.Not, first, b),
    new(2, OpCode.And, both, second, b), new(3, OpCode.Xor, mixed, b, i), new(4, OpCode.Not, untyped, i),
    new(5, OpCode.Or, bothOr, second, b), new(6, OpCode.Xor, bothXor, second, b)];
if ((int)Invoke("PropagateBooleans", chain, types.SystemBooleanType)! != 5 || first.Type != types.SystemBooleanType ||
    second.Type != types.SystemBooleanType || both.Type != types.SystemBooleanType || bothOr.Type != types.SystemBooleanType ||
    bothXor.Type != types.SystemBooleanType || mixed.Type != null || untyped.Type != null)
    throw new Exception("Boolean fixed point or narrow inference failed");
if ((int)Invoke("PropagateInt64Subtractions", new Instruction[] { new(0, OpCode.Subtract, difference, i, i) }, types.SystemInt64Type)! != 1 ||
    difference.Type != types.SystemInt64Type) throw new Exception("Exact Int64 subtraction inference failed");
Reject("PropagateInt64Subtractions", new Instruction[] { new(0, OpCode.Subtract, b, i, i) }, types.SystemInt64Type);
Reject("PropagateBooleans", new Instruction[] { new(0, OpCode.Not, i, b) }, types.SystemBooleanType);
Reject("GuardGraph", (object)new Instruction[] { new(0, OpCode.CallVoid, new Immediate(0)) });
Reject("GuardGraph", (object)new Instruction[] { new(0, OpCode.Return, new Register(null, "rawRegister")) });
Reject("GuardGraph", (object)new Instruction[] { new(0, OpCode.Not, b) });
Reject("GuardGraph", (object)new Instruction[] { new(0, OpCode.Move, b, new Immediate(2)) });
Reject("GuardTypedGraph", (object)new Instruction[] { new(0, OpCode.And, b, b, i) });
Reject("GuardTypedGraph", (object)new Instruction[] { new(0, OpCode.Return, Local("unknown")) });
var loop = new Cpp2IL.Core.Graphs.Block(); loop.Successors.Add(loop);
Reject("GuardAcyclic", loop, new HashSet<Cpp2IL.Core.Graphs.Block>(), new HashSet<Cpp2IL.Core.Graphs.Block>());
Console.WriteLine("guard_checks=11");
'''
        (self.directory / "Program.cs").write_text(source)
        references = "".join(f'<Reference Include="{escape(file.stem)}"><HintPath>{escape(str(file))}</HintPath></Reference>'
                             for file in sorted(self.harness.parent.glob("*.dll")))
        (self.directory / "GuardTests.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
            '</PropertyGroup><ItemGroup>' + references + '</ItemGroup></Project>')
        build = subprocess.run([str(DOTNET), "build", str(self.directory / "GuardTests.csproj"), "-c", "Release"],
                               env=self.environment, capture_output=True, text=True, timeout=60)
        (self.directory / "guard-build.log").write_text(build.stdout + build.stderr)
        self.assertEqual(build.returncode, 0, build.stdout + build.stderr)
        run = subprocess.run([str(DOTNET), str(self.directory / "bin/Release/net10.0/GuardTests.dll"),
                              str(self.binary), str(METADATA), str(self.harness)],
                             env=self.environment, capture_output=True, text=True, timeout=120)
        (self.directory / "guard-run.log").write_text(run.stdout + run.stderr)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertIn("guard_checks=11", run.stdout)

    def test_real_game_schema_preserves_identity_and_serialization_candidates(self):
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["errors"], [])
        assembly = report["assemblies"][0]
        self.assertEqual(assembly["name"], "Game.Runtime")
        self.assertEqual(assembly["image_name"], "Game.Runtime.dll")
        self.assertEqual(report["type_count"], 2781)
        self.assertEqual(report["field_count"], 11665)
        types = {type_["full_name"]: type_ for type_ in assembly["types"]}
        setter = types["AppleTVRemoteSetter"]
        self.assertEqual(setter["token"], "0x02000009")
        self.assertEqual(setter["base_type"]["canonical_name"], "UnityEngine.MonoBehaviour")
        self.assertTrue(setter["unity_component"])
        fields = {field["name"]: field for field in setter["fields"]}
        private = fields["m_setOnAwake"]
        self.assertFalse(private["is_public"])
        self.assertTrue(private["serialize_field"])
        self.assertTrue(private["unity_serialization_candidate"])
        self.assertEqual(private["field_type"]["canonical_name"], "System.Boolean")
        self.assertTrue(private["custom_attributes_complete"])
        self.assertFalse(private["unity_type_eligibility_verified"])
        show_if = next(attribute for attribute in fields["m_allowExitToHome"]["custom_attributes"]
                       if attribute["full_name"] == "Hardlight.ShowIfAttribute")
        self.assertEqual(show_if["arguments"][0]["value"], "m_setOnAwake")
        self.assertIsNone(show_if["arguments"][1]["value"])
        option = setter["custom_attributes"][0]
        self.assertIsInstance(option["arguments"][0]["value"]["value"], int)
        self.assertIsInstance(option["arguments"][1]["value"], bool)
        enum = types["HardlightProject.InGameAudioListener+LowFilterType"]
        self.assertEqual(enum["enum_underlying_type"]["canonical_name"], "System.Int32")
        literals = {field["name"]: field for field in enum["fields"] if field["is_const"]}
        self.assertEqual({name: field["default_value"] for name, field in literals.items()},
                         {"None": -1, "PauseMenu": 0, "Transporter": 1, "ScoreAttack": 2})
        for field in literals.values():
            self.assertTrue(field["has_default_value"])
            self.assertTrue(field["default_value_complete"])
            self.assertEqual(field["default_value_type"]["canonical_name"], "System.Int32")
        text_constant = next(field for field in types["Hardlight.Analytics.AnalyticsConsentManager"]["fields"]
                             if field["name"] == "DebugMenuPath")
        self.assertEqual(text_constant["default_value"], "Analytics")
        self.assertEqual(text_constant["default_value_type"]["canonical_name"], "System.String")
        shapes = [field["field_type"] for type_ in assembly["types"] for field in type_["fields"]]
        self.assertTrue(any(shape["kind"] == "generic_instance" and "[[" in (shape["reflection_full_name"] or "") for shape in shapes))
        self.assertTrue(any(shape["kind"] == "array" and (shape["reflection_full_name"] or "").endswith("[]") for shape in shapes))
        self.assertTrue(any("+" in type_["full_name"] for type_ in assembly["types"]))
        for type_ in assembly["types"]:
            for field in type_["fields"]:
                if field["is_static"] or field["is_const"] or field["is_readonly"] or field["non_serialized"]:
                    self.assertFalse(field["unity_serialization_candidate"], field["name"])

    def test_unknown_assembly_is_rejected(self):
        self.assert_failed_without_output(self.invoke(assembly="Game.Runtime.dll"), "exact original assembly name")

    def cinemachine_menu(self, report):
        type_ = next(t for t in report["assemblies"][0]["types"] if t["full_name"] == "Cinemachine.CinemachineConfiner2D")
        menu = next(a for a in type_["custom_attributes"] if a["full_name"] == "UnityEngine.AddComponentMenu")
        return menu["arguments"][0]

    def mutated_menu_metadata(self, encoded_length):
        # Original CinemachineConfiner2D token0x0200001d has five attributes.
        # Its first string is AddComponentMenu: blob-relative offset25, where
        # compressed byte0 encodes empty and byte1 encodes null. Preserve size.
        data = bytearray(METADATA.read_bytes())
        strings, _ = struct.unpack_from("<II", data, 8 + 2 * 8)
        images, images_size = struct.unpack_from("<II", data, 8 + 20 * 8)
        attributes, _ = struct.unpack_from("<II", data, 8 + 24 * 8)
        ranges, _ = struct.unpack_from("<II", data, 8 + 25 * 8)
        image = next(row for row in struct.iter_unpack("<10i", data[images:images + images_size])
                     if data[strings + row[0]:].split(b"\0", 1)[0] == b"Cinemachine.dll")
        selected = next(row for row in struct.iter_unpack("<II", data[ranges + image[8] * 8:ranges + (image[8] + image[9]) * 8])
                        if row[0] == 0x0200001d)
        position = attributes + selected[1] + 25
        self.assertEqual(data[position - 1:position + 1], b"\x0e\x00")
        data[position] = encoded_length
        path = self.directory / "menu-metadata.dat"
        path.write_bytes(data)
        return path

    def test_original_cinemachine_empty_menu_string_is_byte_corroborated(self):
        result = self.invoke(assembly="Cinemachine")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        menu = self.cinemachine_menu(report)
        self.assertEqual(menu["value"], "")
        self.assertEqual(menu["string_length"], 0)
        self.assertTrue(menu["value_complete"])
        self.assertEqual(menu["blob_offset"], 25)
        self.assertEqual(menu["raw_encoding_sha256"], hashlib.sha256(b"\x00").hexdigest())
        self.assertEqual(report["errors"], [])

    def test_original_blob_null_menu_string_remains_distinct(self):
        result = self.invoke(assembly="Cinemachine", metadata=self.mutated_menu_metadata(1))
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        menu = self.cinemachine_menu(json.loads((self.output / "report.json").read_text()))
        self.assertIsNone(menu["value"])
        self.assertEqual(menu["string_length"], -1)
        self.assertTrue(menu["value_complete"])
        self.assertEqual(menu["raw_encoding_sha256"], hashlib.sha256(b"\x01").hexdigest())

    def test_invalid_negative_attribute_string_length_is_incomplete(self):
        result = self.invoke(assembly="Cinemachine", metadata=self.mutated_menu_metadata(3))
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertEqual(report["status"], "incomplete")
        self.assertTrue(any(e["type"] == "Cinemachine.CinemachineConfiner2D" and "string length" in e["error"] for e in report["errors"]))
        type_ = next(t for t in report["assemblies"][0]["types"] if t["full_name"] == "Cinemachine.CinemachineConfiner2D")
        self.assertFalse(type_["schema_complete"])
        self.assertFalse(type_["custom_attributes_complete"])

    def test_unknown_method_token_is_rejected(self):
        self.assert_failed_without_output(self.invoke("method", token="0x06ffffff"), "match one method")

    def test_type_token_cannot_select_method(self):
        self.assert_failed_without_output(self.invoke("method", token="0x02000009"), "original MethodDef token")

    def test_corrupted_metadata_is_rejected(self):
        corrupted = self.directory / "corrupt-metadata.dat"
        corrupted.write_bytes(b"\0" * 64)
        self.assert_failed_without_output(self.invoke(metadata=corrupted), "Invalid IL2CPP metadata signature")

    def test_corrupted_binary_is_rejected(self):
        corrupted = self.directory / "corrupt-binary.dylib"
        corrupted.write_bytes(b"\0" * 64)
        self.assert_failed_without_output(self.invoke(binary=corrupted), "thin, little-endian")

    def test_corrupted_field_attributes_are_explicitly_incomplete(self):
        # Metadata v31 image records contain per-image custom-attribute ranges.
        # Corrupt only the private SerializeField blob for Game.Runtime's
        # AppleTVRemoteSetter.m_setOnAwake (original field token 0x0400000e).
        data = bytearray(METADATA.read_bytes())
        strings, strings_size = struct.unpack_from("<II", data, 8 + 2 * 8)
        images, images_size = struct.unpack_from("<II", data, 8 + 20 * 8)
        attributes, _ = struct.unpack_from("<II", data, 8 + 24 * 8)
        ranges, _ = struct.unpack_from("<II", data, 8 + 25 * 8)
        image = next(row for row in struct.iter_unpack("<10i", data[images:images + images_size])
                     if data[strings + row[0]:].split(b"\0", 1)[0] == b"Game.Runtime.dll")
        self.assertGreater(strings_size, 0)
        selected = next(row for row in struct.iter_unpack("<II", data[ranges + image[8] * 8:ranges + (image[8] + image[9]) * 8])
                        if row[0] == 0x0400000e)
        data[attributes + selected[1]] = 2  # Claim two constructors in a one-attribute blob.
        corrupted = self.directory / "corrupt-field-attributes.dat"
        corrupted.write_bytes(data)
        result = self.invoke(metadata=corrupted)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        report = json.loads((self.output / "report.json").read_text())
        self.assertEqual(report["status"], "incomplete")
        self.assertTrue(any(error["type"] == "AppleTVRemoteSetter" and error["field"] == "m_setOnAwake"
                            for error in report["errors"]))
        type_ = next(type_ for type_ in report["assemblies"][0]["types"] if type_["full_name"] == "AppleTVRemoteSetter")
        field = next(field for field in type_["fields"] if field["name"] == "m_setOnAwake")
        self.assertFalse(type_["schema_complete"])
        self.assertFalse(field["schema_complete"])
        self.assertFalse(field["custom_attributes_complete"])
        self.assertIsNone(field["unity_serialization_candidate"])

    def test_existing_output_is_rejected_without_modification(self):
        self.output.mkdir()
        sentinel = self.output / "original.txt"
        sentinel.write_text("retain existing evidence")
        result = self.invoke()
        self.assertEqual(result.returncode, 2)
        self.assertIn("must be fresh", result.stderr)
        self.assertEqual(sentinel.read_text(), "retain existing evidence")

    def verify_source(self, source, archive=None):
        args = [str(DOTNET), "msbuild", str(ROOT / "tools/native-recovery/NativeRecovery.csproj"),
                "-t:VerifyCpp2ILSource", "-p:Cpp2ILSource=" + str(source)]
        if archive is not None:
            args.append("-p:Cpp2ILArchive=" + str(archive))
        result = subprocess.run(args, env=self.environment, capture_output=True, text=True, timeout=60)
        (self.directory / "build-check.log").write_text(result.stdout + result.stderr)
        return result

    def test_verified_source_passes_build_pin(self):
        result = self.verify_source(CACHE / "tools/cpp2il-source")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_changed_source_fails_build_pin(self):
        source = self.directory / "source"
        for name in ("Cpp2IL.Core", "LibCpp2IL", "StableNameDotNet", "WasmDisassembler"):
            shutil.copytree(CACHE / "tools/cpp2il-source" / name, source / name,
                            ignore=shutil.ignore_patterns("bin", "obj"))
        (source / "Cpp2IL.Core/Cpp2IlApi.cs").write_text("// changed upstream input\n")
        archive = CACHE / "downloads" / f"Cpp2IL-{PIN}.tar.gz"
        result = self.verify_source(source, archive)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("source differs from the pinned archive", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()

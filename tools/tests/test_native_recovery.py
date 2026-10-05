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
        for file in (HARNESS, DOTNET, METADATA):
            if not file.is_file():
                raise RuntimeError(f"Required integration input is absent: {file}")
        candidates = sorted((CACHE / "recovery/inputs").glob("*.x86_64.dylib"))
        if len(candidates) != 1:
            raise RuntimeError("Integration checks need one hash-verified x86_64 analysis slice")
        cls.binary = candidates[0]
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
        args = [str(DOTNET), str(HARNESS), command]
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

"""Opt-in integration checks against the pinned Core API and supplied release.

These checks require an explicitly authorized controller to build and seal the
host and harness in the same process before opt-in discovery. A cached marker
alone is rejected; this fixture never bootstraps or rebuilds those dependencies,
or recreates their seals.
All mutated fixtures and evidence stay in fresh recovery-cache directories.
"""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import struct
import subprocess
import unittest
import uuid
from xml.sax.saxutils import escape

from tools.lucidlib import bootstrap, native
from tools.lucidlib.inspection import sha256_file
from tools.lucidlib.macho import inspect_macho


ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / ".cache/project-lucid"
PIN = "b5ad444b82267cb1e4b88b8b373c008105bdea52"
METADATA = ROOT / "input/SonicDreamTeam.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"


def _physical_file(path):
    """Reject path aliases before reading borrowed inputs or generated evidence."""
    path = Path(os.path.abspath(os.fspath(path)))
    for component in (*reversed(path.parents), path):
        if component.is_symlink():
            raise bootstrap.ToolError(f"Integration input cannot contain a symbolic link: {component}")
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
        raise bootstrap.ToolError(f"Integration input must be one physical regular file: {path}")
    return path


def _file_identity(path):
    path = _physical_file(path)
    before = path.stat()
    digest = sha256_file(path)
    after = path.stat()
    fields = ("st_dev", "st_ino", "st_size", "st_mtime_ns", "st_ctime_ns")
    identity = tuple(getattr(before, field) for field in fields)
    if identity != tuple(getattr(after, field) for field in fields):
        raise bootstrap.ToolError(f"Integration input changed while hashing: {path}")
    return identity, digest


class _IntegrationPreflight:
    """Read-only admission; public cache files cannot create execution authority."""

    def __init__(self, work, original, metadata):
        self.work = bootstrap.validate_work_dir(work)
        self.original, self.metadata = _physical_file(original), _physical_file(metadata)
        self.pointer = bootstrap.managed_path(bootstrap.tool_work_dir(self.work),
                                              "native-recovery", "latest-build.json")
        self.pointer_identity = _file_identity(self.pointer)
        report = native._load_json(self.pointer, 1024 * 1024)
        self.dotnet = self._host()
        identity = native._lock_identity(bootstrap.load_lock())
        identity.update(
            cpp2il_source_closure_sha256=report.get("cpp2il_source_closure_sha256"),
            harness_sources=self._harness_sources(),
            dotnet_host_sha256=sha256_file(self.dotnet))
        fingerprint = hashlib.sha256(json.dumps(identity, sort_keys=True).encode()).hexdigest()
        if any(report.get(key) != value for key, value in identity.items()):
            raise bootstrap.ToolError("Integration harness source/host identity differs from the current pinned build")
        build = native._cached_build(self.pointer, bootstrap.tool_work_dir(self.work), fingerprint)
        if build is None:
            raise bootstrap.ToolError("Integration requires a genuine same-process sealed native harness; cached markers are insufficient")
        self.fingerprint, self.build_identity = fingerprint, identity
        self.harness = _physical_file(build["path"])
        self.original_identity, self.metadata_identity = _file_identity(self.original), _file_identity(self.metadata)
        architectures = inspect_macho(self.original)["architectures"]
        self.architectures = {}
        for name in ("x86_64", "arm64"):
            rows = [row for row in architectures if row["name"] == name]
            if len(rows) != 1 or rows[0]["encrypted"]:
                raise bootstrap.ToolError(f"Integration requires one readable original {name} slice")
            self.architectures[name] = rows[0]
        self.binary = bootstrap.managed_path(self.work, "recovery", "inputs",
                                             self.original_identity[1] + ".x86_64.dylib")
        self.binary_identity = _file_identity(self.binary)
        self.slice_hashes = {name: self._slice_hash(row) for name, row in self.architectures.items()}
        if self.binary_identity[1] != self.slice_hashes["x86_64"]:
            raise bootstrap.ToolError("Integration analysis slice differs from the physical original")
        self.verify()

    def _harness_sources(self):
        return {path.name: sha256_file(_physical_file(path)) for path in
                (native._PROJECT, native._MANIFEST, *sorted(native._PROJECT.parent.glob("*.cs")))}

    def _host(self):
        host_work = bootstrap.tool_work_dir(self.work)
        directory = bootstrap.managed_path(host_work, "tools", "dotnet")
        # Do not let verified_tool_path install/download on an opt-in test's behalf.
        # Presence is only a prerequisite: its production validator checks the
        # exact pinned identity and complete executable/dependency closure below.
        artifact = bootstrap.host_lock()["artifacts"]["dotnet"]
        identity = {key: artifact[key] for key in ("version", "sha256", "sha512") if key in artifact}
        recorded = bootstrap._INSTALL_SEALS.get(directory)
        if recorded is None or recorded[0] != identity:
            raise bootstrap.ToolError("Integration requires a genuine same-process verified host tool; no installation is attempted")
        return _physical_file(bootstrap.verified_tool_path(self.work, "dotnet"))

    def _slice_hash(self, architecture):
        checksum = hashlib.sha256()
        with self.original.open("rb") as stream:
            stream.seek(architecture["offset"])
            remaining = architecture["size"]
            while remaining:
                block = stream.read(min(1024 * 1024, remaining))
                if not block:
                    raise bootstrap.ToolError("Integration original slice is truncated")
                checksum.update(block)
                remaining -= len(block)
        return checksum.hexdigest()

    def verify(self):
        current_lock = native._lock_identity(bootstrap.load_lock())
        if any(self.build_identity.get(key) != value for key, value in current_lock.items()):
            raise bootstrap.ToolError("Integration native lock identity changed after admission")
        if _file_identity(self.pointer) != self.pointer_identity:
            raise bootstrap.ToolError("Integration build pointer changed after admission")
        if self._harness_sources() != self.build_identity["harness_sources"]:
            raise bootstrap.ToolError("Integration control sources changed after admission")
        if self._host() != self.dotnet or native._cached_build(
                self.pointer, bootstrap.tool_work_dir(self.work), self.fingerprint) is None:
            raise bootstrap.ToolError("Integration executable/dependency closure changed after admission")
        for path, identity in ((self.original, self.original_identity), (self.metadata, self.metadata_identity),
                               (self.binary, self.binary_identity)):
            if _file_identity(path) != identity:
                raise bootstrap.ToolError(f"Integration sealed input changed after admission: {path}")
        if any(self._slice_hash(row) != self.slice_hashes[name] for name, row in self.architectures.items()):
            raise bootstrap.ToolError("Integration original slice changed after admission")

    def extract_arm64(self, directory):
        self.verify()
        target = bootstrap.managed_path(self.work, *directory.relative_to(self.work).parts, "GameAssembly.arm64.dylib")
        architecture = self.architectures["arm64"]
        with self.original.open("rb") as stream, target.open("xb") as output:
            stream.seek(architecture["offset"])
            remaining = architecture["size"]
            while remaining:
                block = stream.read(min(1024 * 1024, remaining))
                if not block:
                    raise bootstrap.ToolError("Integration original ARM64 slice is truncated")
                output.write(block)
                remaining -= len(block)
        self.verify()
        if _file_identity(target)[1] != self.slice_hashes["arm64"]:
            raise bootstrap.ToolError("Integration ARM64 slice differs from the physical original")
        return target


def _integration_environment(directory, dotnet):
    # Absolute verified executable paths do not need caller-provided search or
    # runtime injection channels. Restore/build caches and HOME are test-owned.
    home = directory / "home"
    home.mkdir(exist_ok=False)
    env = {"PATH": os.defpath, "LANG": "C", "LC_ALL": "C", "HOME": str(home),
           "USERPROFILE": str(home), "XDG_CONFIG_HOME": str(home / ".config"),
           "DOTNET_ROOT": str(dotnet.parent), "DOTNET_NOLOGO": "1",
           "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
           "DOTNET_MULTILEVEL_LOOKUP": "0", "DOTNET_TieredCompilation": "0",
           "COMPlus_TieredCompilation": "0", "DOTNET_TieredPGO": "0"}
    for key, name in (("DOTNET_CLI_HOME", "dotnet-home"), ("NUGET_PACKAGES", "nuget-packages"),
                      ("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "dotnet-bundles")):
        path = directory / name
        path.mkdir(exist_ok=False)
        env[key] = str(path)
    return env


@unittest.skipUnless(os.environ.get("LUCID_NATIVE_INTEGRATION") == "1", "requires explicit native integration opt-in and genuine same-process tool seals")
class NativeRecoveryIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        original = ROOT / "input/SonicDreamTeam.app/Contents/Frameworks/GameAssembly.dylib"
        cls.preflight = _IntegrationPreflight(CACHE, original, METADATA)
        cls.harness, cls.dotnet, cls.binary = cls.preflight.harness, cls.preflight.dotnet, cls.preflight.binary
        cls.architecture_dir = bootstrap.managed_path(CACHE, "native-recovery", "tests", uuid.uuid4().hex)
        cls.architecture_dir.mkdir(parents=True, exist_ok=False)
        cls.arm64 = cls.preflight.extract_arm64(cls.architecture_dir)
        cls.arm64_identity = _file_identity(cls.arm64)
        cls.environment = _integration_environment(cls.architecture_dir, cls.dotnet)

    def setUp(self):
        self.directory = bootstrap.managed_path(CACHE, "native-recovery", "tests", uuid.uuid4().hex)
        self.directory.mkdir(parents=True, exist_ok=False)
        self.output = self.directory / "output"

    def invoke(self, command="schemas", assembly="Game.Runtime", token=None, **overrides):
        options = {"binary": self.binary, "metadata": METADATA, "unity-version": "2022.3.54f1",
                   "output": self.output}
        if assembly is not None:
            options["assembly"] = assembly
        if token is not None:
            options["token"] = token
        options.update(overrides)
        self._verify_launch_inputs(options["binary"], options["metadata"])
        args = [str(self.dotnet), str(self.harness), command]
        for key, value in options.items():
            args.extend(["--" + key, str(value)])
        result = subprocess.run(args, env=self.environment, cwd=self.directory, stdin=subprocess.DEVNULL,
                                capture_output=True, text=True, timeout=120)
        (self.directory / "process.log").write_text(result.stdout + result.stderr)
        return result

    def _verify_launch_inputs(self, binary=None, metadata=None):
        self.preflight.verify()
        if _file_identity(self.arm64) != self.arm64_identity:
            raise bootstrap.ToolError("Integration ARM64 fixture changed after extraction")
        for path, original in ((binary, (self.binary, self.arm64)), (metadata, (METADATA,))):
            if path is not None and Path(path) not in original:
                checked = bootstrap.managed_path(CACHE, *Path(path).relative_to(CACHE).parts)
                if self.directory not in checked.parents:
                    raise bootstrap.ToolError("Mutated integration inputs must belong to this test's fresh directory")
                _file_identity(checked)

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

    def test_real_generic_constraint_graphs_distinguish_unconstrained_and_unity_object(self):
        for assembly, type_name, parameter_name, expected in (
                ("HLBinders.Runtime", "Hardlight.UI.Binding.Bindable`1", "T", []),
                ("Unity.Addressables", "UnityEngine.AddressableAssets.AssetReferenceT`1", "TObject",
                 [("UnityEngine.CoreModule", "UnityEngine.Object")])):
            self.output = self.directory / (assembly + "-constraints")
            result = self.invoke(assembly=assembly)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            report = json.loads((self.output / "report.json").read_text())
            types = [t for a in report["assemblies"] for t in a["types"] if t["full_name"] == type_name]
            self.assertEqual(len(types), 1)
            self.assertTrue(types[0]["schema_complete"])
            self.assertEqual(len(types[0]["generic_parameters"]), 1)
            parameter = types[0]["generic_parameters"][0]
            self.assertEqual(parameter["name"], parameter_name)
            self.assertEqual(parameter["index"], 0)
            self.assertTrue(parameter["constraints_complete"])
            constraints = parameter["constraints"]
            self.assertEqual([(c["assembly"], c["reflection_full_name"]) for c in constraints], expected)
            for constraint in constraints:
                self.assertEqual(constraint["kind"], "named")
                self.assertEqual(constraint["canonical_name"], constraint["reflection_full_name"])

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
        self._verify_launch_inputs()
        build = subprocess.run([str(self.dotnet), "build", str(self.directory / "GuardTests.csproj"), "-c", "Release"],
                               env=self.environment, cwd=self.directory, stdin=subprocess.DEVNULL, capture_output=True, text=True, timeout=60)
        (self.directory / "guard-build.log").write_text(build.stdout + build.stderr)
        self.assertEqual(build.returncode, 0, build.stdout + build.stderr)
        guard_output = self.directory / "bin/Release/net10.0"
        bootstrap.seal_built_tool(guard_output, {"fixture": "owned-guard-build"})
        self._verify_launch_inputs()
        if not bootstrap.verify_built_tool(guard_output, {"fixture": "owned-guard-build"}):
            raise bootstrap.ToolError("Owned guard runner changed after its successful build")
        run = subprocess.run([str(self.dotnet), str(guard_output / "GuardTests.dll"),
                              str(self.binary), str(METADATA), str(self.harness)],
                             env=self.environment, cwd=self.directory, stdin=subprocess.DEVNULL, capture_output=True, text=True, timeout=120)
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
        self._verify_launch_inputs()
        args = [str(self.dotnet), "msbuild", str(ROOT / "tools/native-recovery/NativeRecovery.csproj"),
                "-t:VerifyCpp2ILSource", "-p:Cpp2ILSource=" + str(source)]
        if archive is not None:
            args.append("-p:Cpp2ILArchive=" + str(archive))
        result = subprocess.run(args, env=self.environment, cwd=self.directory, stdin=subprocess.DEVNULL, capture_output=True, text=True, timeout=60)
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

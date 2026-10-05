import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.bindings import LayoutError, compare_layout_files, compare_layouts, run_layout_inventory, type_identity
from lucidlib.verification import artifact_fingerprint


def named(assembly, name):
    return {"kind": "named", "assembly": assembly, "canonical_name": name, "reflection_full_name": name}


def container(kind, element):
    if kind == "array":
        return {"kind": "array", "assembly": element["assembly"], "element": element, "rank": 1, "vector_array": True}
    return {"kind": "generic_instance", "assembly": "mscorlib", "definition": "System.Collections.Generic.List`1",
            "arguments": [element]}


def attribute(name, arguments=None):
    return {"assembly": "UnityEngine.CoreModule", "full_name": name, "arguments": arguments or [], "fields": [], "properties": []}


def field(name, reference=None, flags=6, attributes=None):
    attributes = attributes or []
    serialize = any(a["full_name"] == "UnityEngine.SerializeField" for a in attributes)
    managed = any(a["full_name"] == "UnityEngine.SerializeReference" for a in attributes)
    return {"name": name, "attributes": flags, "schema_complete": True,
            "custom_attributes_complete": True, "custom_attributes": attributes,
            "field_type": reference or named("mscorlib", "System.Int32"),
            "is_public": flags & 7 == 6, "is_static": bool(flags & 16), "is_readonly": bool(flags & 32),
            "is_const": bool(flags & 64), "non_serialized": bool(flags & 128),
            "serialize_field": serialize, "serialize_reference": managed,
            "unity_serialization_candidate": not bool(flags & (16 | 32 | 64 | 128)) and (flags & 7 == 6 or serialize or managed)}


def record(assembly, name, fields=None, base=None, component=False, flags=1):
    return {"assembly": assembly, "full_name": name, "token": "0x02000001", "attributes": flags,
            "schema_complete": True, "custom_attributes_complete": True, "custom_attributes": [],
            "fields": fields or [], "base_type": base, "unity_component": component, "unity_scriptable_object": False,
            "is_value_type": False, "is_enum": False, "is_abstract": False, "declaring_type": None, "generic_parameters": []}


class BindingTests(unittest.TestCase):
    def fixture(self):
        original = {"schema_version": 1, "status": "ready", "unity_version": "2022.3.54f1", "assemblies": [
            {"name": "Example", "version": "0.0.0.0", "types": [record("Example", "Example.Widget", [field("value")],
                named("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour"), True)]},
            {"name": "UnityEngine.CoreModule", "types": [
                record("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour", base=named("UnityEngine.CoreModule", "UnityEngine.Object")),
                record("UnityEngine.CoreModule", "UnityEngine.Object", base=named("mscorlib", "System.Object"))]}]}
        inventory = copy.deepcopy(original)
        inventory.update(command="monoscript-inventory", errors=[], original_schema_sha256="a" * 64,
                         monoscripts=[{"assembly": "Example", "full_name": "Example.Widget", "path": "Packages/com.example/Widget.cs",
                                       "guid": "b" * 32, "file_id": 11500000, "class_resolved": True, "source_sha256": "c" * 64}])
        return original, inventory

    def result(self, original, inventory):
        return compare_layouts(original, inventory)["candidates"][0]

    def test_exact_layout_with_different_tokens_versions_is_evidence_only(self):
        original, inventory = self.fixture()
        inventory["assemblies"][0]["version"] = "2.0.0.0"
        inventory["assemblies"][0]["types"][0]["token"] = "0x02000342"
        candidate = self.result(original, inventory)
        self.assertEqual("compatible_layout", candidate["status"])
        self.assertFalse(candidate["remap_eligible"])
        self.assertFalse(compare_layouts(original, inventory)["references_modified"])

    def test_structured_identity_retains_generic_argument_assembly_and_array_shape(self):
        source = {"kind": "generic_instance", "assembly": "mscorlib", "definition": "System.Collections.Generic.List`1",
                  "arguments": [named("Example", "Outer+Nested`1")]}
        changed = copy.deepcopy(source)
        changed["arguments"][0]["assembly"] = "Other"
        self.assertNotEqual(type_identity(source), type_identity(changed))
        vector = {"kind": "array", "assembly": "Example", "element": named("Example", "T"), "rank": 1, "vector_array": True}
        multidim = dict(vector, vector_array=False)
        self.assertNotEqual(type_identity(vector), type_identity(multidim))

    def test_original_assembly_names_are_not_fuzzy_matched(self):
        original, inventory = self.fixture()
        inventory["monoscripts"][0]["assembly"] = "Unity.Example"
        self.assertEqual(0, compare_layouts(original, inventory)["candidate_count"])

    def test_maintained_game_scripts_require_the_same_exact_layout(self):
        original, inventory = self.fixture()
        inventory["monoscripts"][0]["path"] = "Assets/Scripts/Definitions/Widget.cs"
        candidate = self.result(original, inventory)
        self.assertEqual("compatible_layout", candidate["status"])
        self.assertFalse(candidate["remap_eligible"])
        inventory["assemblies"][0]["types"][0]["fields"][0]["field_type"] = named("mscorlib", "System.String")
        self.assertIn("type_identity_mismatch", [issue["reason"] for issue in self.result(original, inventory)["issues"]])

    def test_generated_and_escaped_paths_do_not_supply_binding_evidence(self):
        paths = ("Assets/Recovered/Scripts/Widget.cs", "Assets/StreamingAssets/Widget.cs",
                 "Assets/Editor/Widget.cs", "input/Widget.cs", "/Packages/com.example/Widget.cs",
                 "Packages/com.example/../Widget.cs", "Packages//com.example/Widget.cs",
                 "Assets/Scripts/../Recovered/Widget.cs", "Assets/Scripts/Widget.dll",
                 "Assets\\Scripts\\Widget.cs", "./Assets/Scripts/Widget.cs")
        for path in paths:
            with self.subTest(path=path):
                original, inventory = self.fixture()
                inventory["monoscripts"][0]["path"] = path
                self.assertEqual(0, compare_layouts(original, inventory)["candidate_count"])

    def test_package_and_maintained_aliases_of_a_type_are_ambiguous(self):
        original, inventory = self.fixture()
        inventory["monoscripts"].append(dict(inventory["monoscripts"][0],
            guid="d" * 32, path="Assets/Scripts/Widget.cs"))
        self.assertIn("ambiguous_monoscript_identity", [issue["reason"] for issue in self.result(original, inventory)["issues"]])

    def test_inherited_private_serialized_field_change_blocks(self):
        original, inventory = self.fixture()
        attrs = [attribute("UnityEngine.SerializeField")]
        base = record("Example", "Example.Base", [field("secret", flags=1, attributes=attrs)],
                      named("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour"), True)
        original["assemblies"][0]["types"].append(base)
        inventory["assemblies"][0]["types"].append(copy.deepcopy(base))
        for report in (original, inventory):
            report["assemblies"][0]["types"][0]["base_type"] = named("Example", "Example.Base")
        inventory["assemblies"][0]["types"][1]["fields"][0]["field_type"] = named("mscorlib", "System.String")
        self.assertIn("type_identity_mismatch", [i["reason"] for i in self.result(original, inventory)["issues"]])

    def test_field_order_and_renaming_are_not_relaxed_by_formerly_serialized_as(self):
        original, inventory = self.fixture()
        original["assemblies"][0]["types"][0]["fields"].append(field("next"))
        inventory["assemblies"][0]["types"][0]["fields"] = [field("next"), field("renamed", attributes=[attribute(
            "UnityEngine.Serialization.FormerlySerializedAsAttribute", [{"kind": "primitive", "type": "IL2CPP_TYPE_STRING", "value": "value"}])])]
        self.assertEqual("blocked", self.result(original, inventory)["status"])

    def test_candidate_inconsistency_and_incomplete_attributes_fail_closed(self):
        original, inventory = self.fixture()
        inventory["assemblies"][0]["types"][0]["fields"][0]["is_static"] = True
        self.assertIn("incomplete_or_invalid_layout", [i["reason"] for i in self.result(original, inventory)["issues"]])
        original, inventory = self.fixture()
        inventory["assemblies"][0]["types"][0]["custom_attributes_complete"] = False
        self.assertEqual("blocked", self.result(original, inventory)["status"])

    def string_attribute_fixture(self, original_value, loaded_value, *, verified=True):
        original, inventory = self.fixture()
        left = {"kind": "primitive", "type": "IL2CPP_TYPE_STRING", "value": original_value}
        right = {"kind": "primitive", "type": "IL2CPP_TYPE_STRING", "value": loaded_value}
        if verified:
            left.update(value_complete=True, string_length=-1 if original_value is None else 0,
                        raw_encoding_sha256=hashlib.sha256(b"\x01" if original_value is None else b"\x00").hexdigest())
        original["assemblies"][0]["types"][0]["custom_attributes"] = [attribute("UnityEngine.AddComponentMenu", [left])]
        inventory["assemblies"][0]["types"][0]["custom_attributes"] = [attribute("UnityEngine.AddComponentMenu", [right])]
        return original, inventory

    def test_verified_original_empty_and_null_strings_remain_distinct(self):
        for value in (None, ""):
            original, inventory = self.string_attribute_fixture(value, value)
            self.assertEqual("compatible_layout", self.result(original, inventory)["status"])
        original, inventory = self.string_attribute_fixture("", None)
        self.assertIn("type_attributes_mismatch", [i["reason"] for i in self.result(original, inventory)["issues"]])

    def test_legacy_or_inconsistent_original_string_encoding_blocks(self):
        for value in (None, ""):
            original, inventory = self.string_attribute_fixture(value, value, verified=False)
            self.assertIn("unverified empty/null", self.result(original, inventory)["issues"][0]["detail"])
        original, inventory = self.string_attribute_fixture(None, None)
        original["assemblies"][0]["types"][0]["custom_attributes"][0]["arguments"][0]["string_length"] = 0
        self.assertEqual("blocked", self.result(original, inventory)["status"])

    def test_nested_and_named_incomplete_attribute_values_block(self):
        for group in ("arguments", "fields", "properties"):
            original, inventory = self.fixture()
            value = {"kind": "array", "element_type": "IL2CPP_TYPE_STRING", "value": [
                {"kind": "primitive", "type": "IL2CPP_TYPE_STRING", "value": None, "value_complete": False}]}
            custom = attribute("Example.CustomAttribute")
            custom[group] = [value] if group == "arguments" else [{"name": "text", "value": value}]
            original["assemblies"][0]["types"][0]["custom_attributes"] = [custom]
            inventory["assemblies"][0]["types"][0]["custom_attributes"] = [copy.deepcopy(custom)]
            self.assertEqual("blocked", self.result(original, inventory)["status"])

    def test_unresolved_dependency_and_managed_reference_are_blocked(self):
        original, inventory = self.fixture()
        for report in (original, inventory):
            report["assemblies"][0]["types"][0]["fields"] = [field("data", named("Missing", "Unknown"), attributes=[attribute("UnityEngine.SerializeReference")])]
        reasons = [i["reason"] for i in self.result(original, inventory)["issues"]]
        self.assertIn("missing_layout_dependency", reasons)
        self.assertIn("managed_reference_subtypes_unverified", reasons)

    def test_matching_nested_containers_are_rejected_but_single_containers_are_supported(self):
        for outer in ("array", "list"):
            with self.subTest(outer=outer):
                original, inventory = self.fixture()
                flat = container(outer, named("mscorlib", "System.Int32"))
                for report in (original, inventory):
                    report["assemblies"][0]["types"][0]["fields"] = [field("values", flat)]
                self.assertEqual("compatible_layout", self.result(original, inventory)["status"])
            for inner in ("array", "list"):
                with self.subTest(outer=outer, inner=inner):
                    original, inventory = self.fixture()
                    nested = container(outer, container(inner, named("mscorlib", "System.Int32")))
                    for report in (original, inventory):
                        report["assemblies"][0]["types"][0]["fields"] = [field("values", nested)]
                    self.assertIn("unsupported_nested_serialized_container",
                                  [i["reason"] for i in self.result(original, inventory)["issues"]])

    def test_serializable_wrappers_can_own_their_own_containers(self):
        for outer in ("array", "list"):
            for inner in ("array", "list"):
                with self.subTest(outer=outer, inner=inner):
                    original, inventory = self.fixture()
                    wrapper = record("Example", "Example.Row", [field("values", container(inner, named("mscorlib", "System.Int32")))],
                                     named("mscorlib", "System.Object"), flags=0x2001)
                    for report in (original, inventory):
                        report["assemblies"][0]["types"].append(copy.deepcopy(wrapper))
                        report["assemblies"][0]["types"][0]["fields"] = [field("rows", container(outer, named("Example", "Example.Row")))]
                    self.assertEqual("compatible_layout", self.result(original, inventory)["status"])

    def test_matching_multidimensional_arrays_and_invalid_list_arity_remain_blocked(self):
        invalid = [dict(container("array", named("mscorlib", "System.Int32")), rank=2, vector_array=False),
                   dict(container("list", named("mscorlib", "System.Int32")), arguments=[named("mscorlib", "System.Int32")] * 2)]
        for reference, reason in zip(invalid, ("unsupported_serialized_array", "unsupported_serialized_generic")):
            with self.subTest(reason=reason):
                original, inventory = self.fixture()
                for report in (original, inventory):
                    report["assemblies"][0]["types"][0]["fields"] = [field("values", reference)]
                self.assertIn(reason, [i["reason"] for i in self.result(original, inventory)["issues"]])

    def enum_fixture(self):
        original, inventory = self.fixture()
        enum = record("Example", "Example.Mode", [field("value__"), field("Enabled", named("Example", "Example.Mode"), 32854)],
                      named("mscorlib", "System.Enum"), flags=257)
        enum.update(is_enum=True, is_value_type=True, enum_underlying_type=named("mscorlib", "System.Int32"))
        enum["fields"][1].update(has_default_value=True, default_value_complete=True, default_value=7,
                                  default_value_type=named("mscorlib", "System.Int32"))
        for report in (original, inventory):
            report["assemblies"][0]["types"].append(copy.deepcopy(enum))
            report["assemblies"][0]["types"][0]["fields"] = [field("mode", named("Example", "Example.Mode"))]
        return original, inventory

    def test_enum_numeric_values_are_required_and_compared(self):
        original, inventory = self.enum_fixture()
        self.assertEqual("compatible_layout", self.result(original, inventory)["status"])
        inventory["assemblies"][0]["types"][1]["fields"][1]["default_value"] = 8
        self.assertIn("enum_constants_mismatch", [i["reason"] for i in self.result(original, inventory)["issues"]])
        original, inventory = self.enum_fixture()
        del original["assemblies"][0]["types"][1]["enum_underlying_type"]
        self.assertIn("enum_values_unverified", [i["reason"] for i in self.result(original, inventory)["issues"]])

    def test_matching_enum_storage_must_be_supported_even_for_small_constants(self):
        for name in ("Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Boolean", "Char"):
            with self.subTest(underlying=name):
                original, inventory = self.enum_fixture()
                for report in (original, inventory):
                    enum = report["assemblies"][0]["types"][1]
                    enum["enum_underlying_type"] = named("mscorlib", "System." + name)
                    enum["fields"][0]["field_type"] = named("mscorlib", "System." + name)
                    enum["fields"][1]["default_value_type"] = named("mscorlib", "System." + name)
                candidate = self.result(original, inventory)
                if name in ("Int64", "UInt64", "Boolean", "Char"):
                    self.assertIn("unsupported_serialized_enum_underlying_type", [i["reason"] for i in candidate["issues"]])
                else:
                    self.assertEqual("compatible_layout", candidate["status"])

    def test_ambiguous_types_or_script_identities_are_never_accepted(self):
        original, inventory = self.fixture()
        inventory["assemblies"][0]["types"].append(copy.deepcopy(inventory["assemblies"][0]["types"][0]))
        with self.assertRaises(LayoutError):
            compare_layouts(original, inventory)
        original, inventory = self.fixture()
        second = dict(inventory["monoscripts"][0], guid="d" * 32, path="Packages/com.example/Other.cs")
        inventory["monoscripts"].append(second)
        self.assertIn("ambiguous_monoscript_identity", [i["reason"] for i in self.result(original, inventory)["issues"]])

    def test_stale_inventory_and_unity_version_mismatch_are_rejected(self):
        original, inventory = self.fixture()
        with self.assertRaises(LayoutError):
            compare_layouts(original, inventory, schema_sha256="z" * 64)
        inventory["unity_version"] = "6000.0.0f1"
        with self.assertRaises(LayoutError):
            compare_layouts(original, inventory)

    def test_file_comparison_preserves_sources_and_failed_prior_report(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            original, inventory = self.fixture()
            schema = root / "schema.json"
            source = json.dumps(original).encode()
            schema.write_bytes(source)
            inventory["original_schema_sha256"] = hashlib.sha256(source).hexdigest()
            loaded = root / "inventory.json"
            loaded.write_text(json.dumps(inventory))
            output = root / "cache/report.json"
            self.assertEqual(1, compare_layout_files(schema, loaded, output)["compatible_layout_count"])
            previous = output.read_bytes()
            inventory["errors"] = [{"type": "bad"}]
            loaded.write_text(json.dumps(inventory))
            with self.assertRaises(LayoutError):
                compare_layout_files(schema, loaded, output)
            self.assertEqual(previous, output.read_bytes())
            self.assertEqual(source, schema.read_bytes())
            with self.assertRaises(LayoutError):
                compare_layout_files(schema, loaded, schema)
            with self.assertRaises(LayoutError):
                compare_layout_files(schema, loaded, root / "input/report.json")

    def wrapper_fixture(self, root):
        work = root / ".cache/project-lucid"
        schema_path = work / "native-recovery/runs/schema/evidence/report.json"
        schema_path.parent.mkdir(parents=True)
        original, inventory = self.fixture()
        original.update(command="schemas", metadata_sha256="1" * 64, native_binary_sha256="2" * 64)
        raw = json.dumps(original).encode()
        schema_path.write_bytes(raw)
        inventory.update(original_schema_sha256=hashlib.sha256(raw).hexdigest(), source_fingerprint=artifact_fingerprint(root))
        summary = {"schema_version": 1, "command": "schemas", "status": "ready", "evidence_report": str(schema_path),
                   "source": {"metadata_sha256": "1" * 64, "analysis_binary_sha256": "2" * 64, "unity_version": "2022.3.54f1"}}
        pointer = work / "native-recovery/latest-schemas.json"
        pointer.write_text(json.dumps(summary))
        return work, pointer, inventory

    def test_editor_wrapper_uses_latest_schema_and_preserves_old_result_on_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            work, pointer, inventory = self.wrapper_fixture(root)
            with patch("lucidlib.bootstrap.REPO_ROOT", root), patch("lucidlib.bootstrap.CACHE_ROOT", work), \
                    patch("lucidlib.bindings.CACHE_ROOT", work), patch("lucidlib.verification.find_editor", return_value=root / "Unity"):
                def editor(command, env, timeout):
                    self.assertEqual("ProjectLucid.Editor.MonoScriptLayoutInventory.Run", command[command.index("-executeMethod") + 1])
                    self.assertEqual(json.loads(pointer.read_text())["evidence_report"], env["LUCID_LAYOUT_SCHEMA_PATH"])
                    Path(env["LUCID_LAYOUT_INVENTORY_PATH"]).write_text(json.dumps(inventory))
                    return type("Exit", (), {"returncode": 0})()
                with patch("lucidlib.bindings.subprocess.run", side_effect=editor):
                    report = run_layout_inventory(root, work)
                self.assertEqual("compared", report["status"])
                self.assertEqual(1, report["compatible_layout_count"])
                self.assertFalse(report["references_modified"])
                latest = work / "reports/latest-layout-inventory.json"
                before = latest.read_bytes()
                with patch("lucidlib.bindings.subprocess.run", return_value=type("Exit", (), {"returncode": 1})()):
                    self.assertEqual("failed", run_layout_inventory(root, work)["status"])
                self.assertEqual(before, latest.read_bytes())

    def test_editor_wrapper_rejects_escaped_pointer_and_changed_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            work, pointer, inventory = self.wrapper_fixture(root)
            with patch("lucidlib.bootstrap.REPO_ROOT", root), patch("lucidlib.bootstrap.CACHE_ROOT", work), \
                    patch("lucidlib.bindings.CACHE_ROOT", work), patch("lucidlib.verification.find_editor", return_value=root / "Unity"):
                original_summary = json.loads(pointer.read_text())
                summary = dict(original_summary, evidence_report=str(root / "input/schema.json"))
                pointer.write_text(json.dumps(summary))
                with patch("lucidlib.bindings.subprocess.run") as run:
                    with self.assertRaises(LayoutError):
                        run_layout_inventory(root, work)
                    run.assert_not_called()
                pointer.write_text(json.dumps(original_summary))
                def editor(command, env, timeout):
                    Path(env["LUCID_LAYOUT_INVENTORY_PATH"]).write_text(json.dumps(inventory))
                    (root / "tools").mkdir()
                    (root / "tools/changed.py").write_text("changed during inventory")
                    return type("Exit", (), {"returncode": 0})()
                with patch("lucidlib.bindings.subprocess.run", side_effect=editor):
                    result = run_layout_inventory(root, work)
                self.assertEqual("failed", result["status"])
                self.assertIn("stale", result["errors"][0])
                self.assertFalse((work / "reports/latest-layout-inventory.json").exists())


if __name__ == "__main__":
    unittest.main()

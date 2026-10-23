"""Literal Vector3 and LayerMask declarations; no runtime/provider substitution."""
import copy
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bindings
from lucidlib.bindings import _Comparison, compare_layouts
import test_bindings as base_tests
from test_bindings import attribute, field, named, record

CORE, SHARED = "UnityEngine.CoreModule", "UnityEngine.SharedInternalsModule"


def primitive(kind, value):
    return {"kind": "primitive", "type": "IL2CPP_TYPE_" + kind, "value": value}


def cad(assembly, name, arguments=(), properties=()):
    return {"assembly": assembly, "full_name": name, "arguments": list(arguments), "fields": [], "properties": list(properties)}


def required():
    return cad(SHARED, "UnityEngine.Scripting.RequiredByNativeCodeAttribute", properties=[
        {"name": "Optional", "value": primitive("BOOLEAN", True)}, {"name": "GenerateProxy", "value": primitive("BOOLEAN", True)}])


def completed(name, reference, flags, default=None, attributes=None):
    value = field(name, reference, flags=flags, attributes=attributes)
    constant = bool(flags & 64)
    value.update(has_default_value=constant, default_value_complete=True, default_value=default,
                 default_value_type=copy.deepcopy(reference) if constant else None)
    return value


def vector3_record():
    single, vector = named("mscorlib", "System.Single"), named(CORE, "UnityEngine.Vector3")
    fields = [completed("kEpsilon", single, 32854, 1e-5), completed("kEpsilonNormalSqrt", single, 32854, 1e-15)]
    fields += [completed(name, single, 6) for name in ("x", "y", "z")]
    fields += [completed(name, vector, 49) for name in ("zeroVector", "oneVector", "upVector", "downVector", "leftVector",
                                                       "rightVector", "forwardVector", "backVector", "positiveInfinityVector",
                                                       "negativeInfinityVector")]
    result = record(CORE, "UnityEngine.Vector3", fields, named("mscorlib", "System.ValueType"), flags=0x100109)
    result.update(namespace="UnityEngine", name="Vector3", is_value_type=True, enum_underlying_type=None)
    result["custom_attributes"] = [
        cad(SHARED, "UnityEngine.Bindings.NativeTypeAttribute", properties=[{"name": "Header", "value": primitive("STRING", "Runtime/Math/Vector3.h")}]),
        cad(SHARED, "UnityEngine.Bindings.NativeHeaderAttribute", [primitive("STRING", "Runtime/Math/MathScripting.h")]),
        cad(SHARED, "UnityEngine.Bindings.NativeHeaderAttribute", [primitive("STRING", "Runtime/Math/Vector3.h")]),
        cad(CORE, "Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstructionAttribute"),
        cad("mscorlib", "System.Reflection.DefaultMemberAttribute", [primitive("STRING", "Item")]),
        cad(SHARED, "UnityEngine.NativeClassAttribute", [primitive("STRING", "Vector3f")]),
        required()]
    return result


def layer_mask_record():
    native = cad(SHARED, "UnityEngine.Bindings.NativeNameAttribute", [primitive("STRING", "m_Bits")])
    result = record(CORE, "UnityEngine.LayerMask", [completed("m_Mask", named("mscorlib", "System.Int32"), 1, attributes=[native])],
                    named("mscorlib", "System.ValueType"), flags=0x100109)
    result.update(namespace="UnityEngine", name="LayerMask", is_value_type=True, enum_underlying_type=None)
    result["custom_attributes"] = [
        required(), cad(SHARED, "UnityEngine.NativeClassAttribute", [primitive("STRING", "BitField"), primitive("STRING", "struct BitField;")]),
        cad(SHARED, "UnityEngine.Bindings.NativeHeaderAttribute", [primitive("STRING", "Runtime/BaseClasses/TagManager.h")]),
        cad(SHARED, "UnityEngine.Bindings.NativeHeaderAttribute", [primitive("STRING", "Runtime/BaseClasses/BitField.h")])]
    return result


class BuiltinStructComparisonTests(unittest.TestCase):
    CASES = ((("UnityEngine.CoreModule", "UnityEngine.Vector3"), vector3_record),
             (("UnityEngine.CoreModule", "UnityEngine.LayerMask"), layer_mask_record))

    def fixture(self, key, build):
        original, loaded = base_tests.BindingTests().fixture()
        for report in (original, loaded):
            report["assemblies"][0]["types"][0]["fields"] = [field("value", named(*key))]
            report["assemblies"][1]["types"].append(build())
        return original, loaded

    def struct(self, report): return report["assemblies"][1]["types"][-1]

    def result(self, original, loaded): return compare_layouts(original, loaded)["candidates"][0]

    def test_complete_struct_is_inline_builtin_data_not_a_pointer(self):
        for key, build in self.CASES:
            with self.subTest(key=key[1]):
                original, loaded = self.fixture(key, build); before = copy.deepcopy((original, loaded))
                comparison = _Comparison(original, loaded)
                self.assertFalse(comparison._unity_object(key))
                self.assertTrue(comparison._unity_2022_builtin_value(key))
                self.assertEqual("compatible_layout", self.result(original, loaded)["status"])
                self.assertEqual(before, (original, loaded))
                # Order of the type CADs differs between planes; the set is exact.
                self.struct(loaded)["custom_attributes"].reverse()
                self.assertEqual("compatible_layout", self.result(original, loaded)["status"])

    def test_without_the_contract_the_struct_stays_blocked(self):
        for key, build in self.CASES:
            with self.subTest(key=key[1]), patch.dict(bindings._BUILTIN_STRUCTS, clear=True):
                original, loaded = self.fixture(key, build)
                result = self.result(original, loaded)
                self.assertEqual("blocked", result["status"])
                self.assertIn("Unity object ancestry is not a reference class", [i["detail"] for i in result["issues"]])

    def test_the_builtin_rule_needs_the_exact_engine_version(self):
        for key, build in self.CASES:
            with self.subTest(key=key[1]):
                original, loaded = self.fixture(key, build)
                loaded["unity_version"] = original["unity_version"] = "2022.3.53f1"
                self.assertEqual("blocked", self.result(original, loaded)["status"])

    def test_every_declared_row_fails_closed_on_both_graphs(self):
        for key, build in self.CASES:
            for side in (0, 1):
                for change in ("flags", "Serializable", "reference-class", "namespace", "base", "cad-missing", "cad-extra",
                               "cad-value", "field-missing", "field-order", "field-flags", "field-type", "field-cad",
                               "constant", "candidate", "default-incomplete", "managed-reference"):
                    with self.subTest(key=key[1], side=side, change=change):
                        original, loaded = self.fixture(key, build)
                        struct = self.struct((original, loaded)[side]); fields = struct["fields"]
                        if change == "flags": struct["attributes"] ^= 8
                        elif change == "Serializable":
                            for graph in (original, loaded): self.struct(graph)["attributes"] |= 0x2000
                        elif change == "reference-class": struct["is_value_type"] = False
                        elif change == "namespace": struct["namespace"] = "Other"
                        elif change == "base": struct["base_type"] = named("Other", "System.ValueType")
                        elif change == "cad-missing": struct["custom_attributes"].pop()
                        elif change == "cad-extra": struct["custom_attributes"].append(attribute("Example.ChangedAttribute"))
                        elif change == "cad-value":
                            target = next(a for a in struct["custom_attributes"] if a["full_name"].endswith("RequiredByNativeCodeAttribute"))
                            target["properties"][0]["value"]["value"] = False
                        elif change == "field-missing": fields.pop()
                        elif change == "field-order":
                            if len(fields) > 1: fields.reverse()
                            else: fields.insert(0, completed("m_Extra", named("mscorlib", "System.Int32"), 1))
                        elif change == "field-flags": fields[-1]["attributes"] ^= 0x100
                        elif change == "field-type": fields[-1]["field_type"] = named("mscorlib", "System.Double")
                        elif change == "field-cad": fields[-1]["custom_attributes"] = [attribute("Example.ChangedAttribute")]
                        elif change == "constant":
                            if key[1].endswith("Vector3"): fields[0]["default_value"] = 2e-5
                            else: fields[0].update(has_default_value=True, default_value=1.0)
                        elif change == "candidate":
                            target = fields[2] if key[1].endswith("Vector3") else fields[0]
                            target["attributes"] = 6 if target["attributes"] != 6 else 1
                            target["is_public"] = target["attributes"] == 6
                            target["unity_serialization_candidate"] = target["is_public"]
                        elif change == "default-incomplete": fields[-1]["default_value_complete"] = False
                        elif change == "managed-reference": fields[-1]["serialize_reference"] = True
                        self.assertEqual("blocked", self.result(original, loaded)["status"])

    def test_other_structs_are_not_admitted_by_these_contracts(self):
        original, loaded = self.fixture(("UnityEngine.CoreModule", "UnityEngine.Vector3"), vector3_record)
        for report in (original, loaded):
            struct = self.struct(report)
            struct.update(full_name="UnityEngine.Vector4", name="Vector4")
            report["assemblies"][0]["types"][0]["fields"] = [field("value", named(CORE, "UnityEngine.Vector4"))]
        self.assertEqual("blocked", self.result(original, loaded)["status"])


if __name__ == "__main__":
    unittest.main()

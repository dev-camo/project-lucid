"""Literal engine classes the 2022.3.54f1 serializer stores natively; no runtime/provider substitution."""
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
from test_bindings_builtin_structs import CORE, SHARED, cad, completed, primitive

KEY = (CORE, "UnityEngine.AnimationCurve")


def animation_curve_record():
    # Shipped and installed CoreModule declaration: one internal native handle, no Serializable bit.
    result = record(CORE, "UnityEngine.AnimationCurve", [completed("m_Ptr", named("mscorlib", "System.IntPtr"), 3)],
                    named("mscorlib", "System.Object"), flags=0x100009)
    result.update(namespace="UnityEngine", name="AnimationCurve", enum_underlying_type=None)
    result["custom_attributes"] = [
        cad(SHARED, "UnityEngine.Bindings.NativeHeaderAttribute", [primitive("STRING", "Runtime/Math/AnimationCurve.bindings.h")]),
        cad(SHARED, "UnityEngine.Scripting.RequiredByNativeCodeAttribute"),
        cad("mscorlib", "System.Reflection.DefaultMemberAttribute", [primitive("STRING", "Item")])]
    return result


class EngineNativeClassComparisonTests(unittest.TestCase):
    def fixture(self):
        original, loaded = base_tests.BindingTests().fixture()
        for report in (original, loaded):
            report["assemblies"][0]["types"][0]["fields"] = [field("curve", named(*KEY))]
            report["assemblies"][1]["types"].append(animation_curve_record())
        return original, loaded

    def curve(self, report): return report["assemblies"][1]["types"][-1]

    def result(self, original, loaded): return compare_layouts(original, loaded)["candidates"][0]

    def test_complete_curve_is_natively_stored_engine_data(self):
        original, loaded = self.fixture(); before = copy.deepcopy((original, loaded))
        comparison = _Comparison(original, loaded)
        self.assertFalse(comparison._unity_object(KEY))
        self.assertFalse(comparison._unity_2022_builtin_value(KEY))
        self.assertTrue(comparison._engine_native_class_contract(KEY))
        self.assertEqual("compatible_layout", self.result(original, loaded)["status"])
        self.assertEqual(before, (original, loaded))
        # Order of the type CADs differs between planes; the set is exact.
        self.curve(loaded)["custom_attributes"].reverse()
        self.assertEqual("compatible_layout", self.result(original, loaded)["status"])

    def test_without_the_contract_the_curve_stays_blocked(self):
        with patch.dict(bindings._ENGINE_NATIVE_CLASSES, clear=True):
            original, loaded = self.fixture()
            result = self.result(original, loaded)
            self.assertEqual("blocked", result["status"])
            self.assertIn("field_type_eligibility_unverified", [i["reason"] for i in result["issues"]])

    def test_the_rule_needs_the_exact_engine_version(self):
        original, loaded = self.fixture()
        loaded["unity_version"] = original["unity_version"] = "2022.3.53f1"
        self.assertEqual("blocked", self.result(original, loaded)["status"])

    def test_every_declared_row_fails_closed_on_both_graphs(self):
        for side in (0, 1):
            for change in ("flags", "Serializable", "value-type", "namespace", "base", "cad-missing", "cad-extra", "cad-value",
                           "field-missing", "field-extra", "field-flags", "field-type", "field-cad", "default",
                           "default-incomplete", "managed-reference", "component"):
                with self.subTest(side=side, change=change):
                    original, loaded = self.fixture()
                    curve = self.curve((original, loaded)[side]); fields = curve["fields"]
                    if change == "flags": curve["attributes"] ^= 8
                    elif change == "Serializable":
                        for graph in (original, loaded): self.curve(graph)["attributes"] |= 0x2000
                    elif change == "value-type": curve["is_value_type"] = True
                    elif change == "namespace": curve["namespace"] = "Other"
                    elif change == "base": curve["base_type"] = named("Other", "System.Object")
                    elif change == "cad-missing": curve["custom_attributes"].pop()
                    elif change == "cad-extra": curve["custom_attributes"].append(attribute("Example.ChangedAttribute"))
                    elif change == "cad-value": curve["custom_attributes"][0]["arguments"][0]["value"] = "Runtime/Other.h"
                    elif change == "field-missing": fields.pop()
                    elif change == "field-extra": fields.append(completed("m_Extra", named("mscorlib", "System.Int32"), 1))
                    elif change == "field-flags": fields[0]["attributes"] ^= 0x100
                    elif change == "field-type": fields[0]["field_type"] = named("mscorlib", "System.Int64")
                    elif change == "field-cad": fields[0]["custom_attributes"] = [attribute("Example.ChangedAttribute")]
                    elif change == "default": fields[0].update(has_default_value=True, default_value=0)
                    elif change == "default-incomplete": fields[0]["default_value_complete"] = False
                    elif change == "managed-reference": fields[0]["serialize_reference"] = True
                    elif change == "component": curve["unity_component"] = True
                    self.assertEqual("blocked", self.result(original, loaded)["status"])

    def test_other_engine_classes_are_not_admitted_by_this_contract(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            curve = self.curve(report)
            curve.update(full_name="UnityEngine.Gradient", name="Gradient")
            report["assemblies"][0]["types"][0]["fields"] = [field("curve", named(CORE, "UnityEngine.Gradient"))]
        self.assertEqual("blocked", self.result(original, loaded)["status"])


if __name__ == "__main__":
    unittest.main()

"""Closed generic bases and Unity-skipped plain-class fields in pure declaration reports."""
import copy
import unittest

from lucidlib.bindings import compare_layouts
import test_bindings as base_tests
from test_bindings import attribute, field, named, record


SERIALIZE = [attribute("UnityEngine.SerializeField")]


def parameter(index, name, constraints=()):
    return {"index": index, "name": name, "attributes": 0, "constraints_complete": True,
            "constraints": [dict(c, token="0x0200%04x" % (index + 1)) for c in constraints]}


def type_parameter(index):
    return {"kind": "generic_parameter", "assembly": "Example", "parameter_owner": "type", "parameter_index": index}


def instance(assembly, definition, *arguments):
    return {"kind": "generic_instance", "assembly": assembly, "definition": definition, "arguments": list(arguments)}


def by_name(report, name):
    return next(t for a in report["assemblies"] for t in a["types"] if t["full_name"] == name)


def candidate(original, loaded, name="Example.Widget"):
    return next(c for c in compare_layouts(original, loaded)["candidates"] if c["full_name"] == name)


def reasons(result):
    return [issue["reason"] for issue in result["issues"]]


class GenericBaseComparisonTests(unittest.TestCase):
    """Example.Widget : Example.Singleton`1<Example.Widget> : MonoBehaviour."""

    def fixture(self, base_fields=None):
        original, loaded = base_tests.BindingTests().fixture()
        monobehaviour = named("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour")
        for report in (original, loaded):
            widget = by_name(report, "Example.Widget")
            widget["base_type"] = instance("Example", "Example.Singleton`1", named("Example", "Example.Widget"))
            singleton = record("Example", "Example.Singleton`1", copy.deepcopy(base_fields) if base_fields else
                               [field("<Instance>k__BackingField", type_parameter(0), flags=0x11)], monobehaviour, True)
            singleton["generic_parameters"] = [parameter(0, "T", [monobehaviour])]
            report["assemblies"][0]["types"].append(singleton)
        # Each build numbers its own constraint metadata.
        by_name(loaded, "Example.Singleton`1")["generic_parameters"][0]["constraints"][0]["token"] = "0x0200028f"
        return original, loaded

    def test_static_only_generic_base_contributes_no_serialized_fields(self):
        original, loaded = self.fixture()
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])

    def test_generic_base_fields_are_compared_with_the_instance_arguments(self):
        typed = [field("m_value", type_parameter(0), flags=1, attributes=SERIALIZE)]
        original, loaded = self.fixture(typed)
        by_name(original, "Example.Widget")["base_type"]["arguments"] = [named("mscorlib", "System.Int32")]
        by_name(loaded, "Example.Widget")["base_type"]["arguments"] = [named("mscorlib", "System.Int32")]
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        # The closed field type is an unresolved struct here: substitution really happened.
        for report in (original, loaded):
            by_name(report, "Example.Widget")["base_type"]["arguments"] = [named("Example", "Example.Missing")]
        self.assertIn("missing_layout_dependency", reasons(candidate(original, loaded)))

    def test_changed_generic_base_field_blocks(self):
        typed = [field("m_value", type_parameter(0), flags=1, attributes=SERIALIZE)]
        original, loaded = self.fixture(typed)
        for report in (original, loaded):
            by_name(report, "Example.Widget")["base_type"]["arguments"] = [named("mscorlib", "System.Int32")]
        by_name(loaded, "Example.Singleton`1")["fields"][0]["name"] = "m_renamed"
        self.assertIn("serialized_field_order_or_names_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture(typed)
        for report in (original, loaded):
            by_name(report, "Example.Widget")["base_type"]["arguments"] = [named("mscorlib", "System.Int32")]
        by_name(loaded, "Example.Singleton`1")["fields"].append(field("m_extra", flags=1, attributes=SERIALIZE))
        self.assertIn("serialized_field_order_or_names_mismatch", reasons(candidate(original, loaded)))

    def test_generic_base_argument_arity_and_constraints_must_match(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Widget")["base_type"]["arguments"] = [named("Example", "Example.Other")]
        self.assertIn("base_type_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.Singleton`1")["generic_parameters"].append(parameter(1, "U"))
        self.assertIn("generic_base_arity_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.Singleton`1")["generic_parameters"][0]["constraints"][0] = named(
            "UnityEngine.CoreModule", "UnityEngine.Component")
        self.assertIn("type_shape_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.Singleton`1")["generic_parameters"][0]["constraints_complete"] = False
        self.assertIn("incomplete_or_invalid_layout", reasons(candidate(original, loaded)))

    def test_missing_generic_definition_blocks(self):
        original, loaded = self.fixture()
        loaded["assemblies"][0]["types"] = [t for t in loaded["assemblies"][0]["types"] if t["full_name"] != "Example.Singleton`1"]
        self.assertIn("missing_layout_dependency", reasons(candidate(original, loaded)))

    def test_parameter_outside_the_instance_arity_blocks(self):
        typed = [field("m_value", type_parameter(1), flags=1, attributes=SERIALIZE)]
        original, loaded = self.fixture(typed)
        self.assertIn("incomplete_or_invalid_layout", reasons(candidate(original, loaded)))

    def test_nested_generic_base_closes_through_the_chain(self):
        # Example.Singleton`1<T> : Example.Holder`1<T>; Holder`1 owns a T field.
        original, loaded = self.fixture()
        monobehaviour = named("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour")
        for report in (original, loaded):
            by_name(report, "Example.Widget")["base_type"]["arguments"] = [named("mscorlib", "System.Single")]
            by_name(report, "Example.Singleton`1")["base_type"] = instance("Example", "Example.Holder`1", type_parameter(0))
            holder = record("Example", "Example.Holder`1", [field("m_held", type_parameter(0), flags=1, attributes=SERIALIZE)],
                            monobehaviour, True)
            holder["generic_parameters"] = [parameter(0, "T")]
            report["assemblies"][0]["types"].append(holder)
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        by_name(loaded, "Example.Holder`1")["fields"][0]["field_type"] = named("mscorlib", "System.Int32")
        self.assertIn("type_identity_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Singleton`1")["base_type"] = instance("Example", "Example.Holder`1", type_parameter(0))
            holder = record("Example", "Example.Holder`1", [field("m_held", type_parameter(0), flags=1, attributes=SERIALIZE)],
                            named("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour"), True)
            holder["generic_parameters"] = [parameter(0, "T")]
            report["assemblies"][0]["types"].append(holder)
        # Closed with Widget itself, the inherited T field is a nested MonoBehaviour pointer.
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])


class GenericPointerAncestryTests(unittest.TestCase):
    """Example.Widget stores a PPtr to Example.Group : Example.Table`2<int, string> : ScriptableObject."""

    def fixture(self):
        original, loaded = base_tests.BindingTests().fixture()
        scriptable = named("UnityEngine.CoreModule", "UnityEngine.ScriptableObject")
        for report in (original, loaded):
            by_name(report, "Example.Widget")["fields"] = [field("m_group", named("Example", "Example.Group"), flags=1, attributes=SERIALIZE)]
            group = record("Example", "Example.Group", [], instance("Example", "Example.Table`2",
                           named("mscorlib", "System.Int32"), named("mscorlib", "System.String")))
            table = record("Example", "Example.Table`2", [field("m_rows", {"kind": "array", "assembly": "Example",
                           "element": type_parameter(0), "rank": 1, "vector_array": True}, flags=1, attributes=SERIALIZE)], scriptable)
            table["generic_parameters"] = [parameter(0, "TKey"), parameter(1, "TValue")]
            table["is_abstract"] = True
            for item in (group, table):
                item["unity_scriptable_object"] = True
            report["assemblies"][0]["types"].extend([group, table])
            report["assemblies"][1]["types"].append(record("UnityEngine.CoreModule", "UnityEngine.ScriptableObject",
                                                           base=named("UnityEngine.CoreModule", "UnityEngine.Object")))
        return original, loaded

    def test_pointer_through_a_generic_ancestor_stops_at_the_reference(self):
        original, loaded = self.fixture()
        # The pointed-to payload differs, but a PPtr stores only the identity.
        by_name(loaded, "Example.Table`2")["fields"][0]["name"] = "m_changed"
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])

    def test_generic_ancestry_still_requires_matching_arity_and_identity(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Table`2")["generic_parameters"].pop()
        self.assertEqual("blocked", candidate(original, loaded)["status"])
        original, loaded = self.fixture()
        by_name(loaded, "Example.Group")["base_type"]["arguments"][1] = named("mscorlib", "System.Int32")
        self.assertEqual("blocked", candidate(original, loaded)["status"])
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Table`2")["base_type"] = named("mscorlib", "System.Object")
        self.assertEqual("blocked", candidate(original, loaded)["status"])


class UnserializedClassFieldTests(unittest.TestCase):
    """Example.Widget has [SerializeField] Example.Table m_table, a class without [Serializable]."""

    def fixture(self, serializable=False, assembly="Example", name="Example.Table"):
        original, loaded = base_tests.BindingTests().fixture()
        hashset = instance("mscorlib", "System.Collections.Generic.HashSet`1", named("mscorlib", "System.UInt32"))
        for report in (original, loaded):
            by_name(report, "Example.Widget")["fields"] = [
                field("m_before", flags=1, attributes=SERIALIZE),
                field("m_table", named(assembly, name), flags=1, attributes=SERIALIZE),
                field("m_after", flags=1, attributes=SERIALIZE)]
            table = record(assembly, name, [field("leading", hashset)], named("mscorlib", "System.Object"),
                           flags=0x100002 | (0x2000 if serializable else 0))
            types = next(a for a in report["assemblies"] if a["name"] == assembly)["types"] if any(
                a["name"] == assembly for a in report["assemblies"]) else None
            if types is None:
                report["assemblies"].append({"name": assembly, "types": [table]})
            else:
                types.append(table)
            if not any(a["name"] == "mscorlib" for a in report["assemblies"]):
                report["assemblies"].append({"name": "mscorlib", "types": [record("mscorlib", "System.Object")]})
        return original, loaded

    def test_identical_plain_class_field_is_skipped_like_unity(self):
        original, loaded = self.fixture()
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        # Its members are never serialized, so they are not compared either.
        by_name(loaded, "Example.Table")["fields"][0]["name"] = "renamed"
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])

    def test_serializable_class_is_still_traversed(self):
        original, loaded = self.fixture(serializable=True)
        self.assertIn("unsupported_serialized_generic", reasons(candidate(original, loaded)))

    def test_serializable_bit_on_one_side_blocks(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Table")["attributes"] |= 0x2000
        self.assertEqual("blocked", candidate(original, loaded)["status"])

    def test_changed_or_one_sided_field_blocks(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Widget")["fields"][1]["field_type"] = named("Example", "Example.Other")
        self.assertEqual("blocked", candidate(original, loaded)["status"])
        original, loaded = self.fixture()
        by_name(loaded, "Example.Widget")["fields"].pop(1)
        self.assertIn("serialized_field_order_or_names_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.Widget")["fields"][1]["attributes"] = 6
        by_name(loaded, "Example.Widget")["fields"][1].update(is_public=True, unity_serialization_candidate=True)
        self.assertEqual("blocked", candidate(original, loaded)["status"])

    def test_structs_interfaces_engine_classes_and_managed_references_are_not_skipped(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Table")["is_value_type"] = True
        self.assertEqual("blocked", candidate(original, loaded)["status"])
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Table")["attributes"] |= 0x20
        self.assertEqual("blocked", candidate(original, loaded)["status"])
        # AnimationCurve has no Serializable bit yet the engine serializes it natively.
        original, loaded = self.fixture(assembly="UnityEngine.CoreModule", name="UnityEngine.AnimationCurve")
        self.assertIn("field_type_eligibility_unverified", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        for report in (original, loaded):
            widget_field = by_name(report, "Example.Widget")["fields"][1]
            widget_field["custom_attributes"] = [attribute("UnityEngine.SerializeReference")]
            widget_field["serialize_field"], widget_field["serialize_reference"] = False, True
        self.assertEqual("blocked", candidate(original, loaded)["status"])

    def test_arrays_of_plain_classes_stay_blocked(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Widget")["fields"][1]["field_type"] = {
                "kind": "array", "assembly": "Example", "element": named("Example", "Example.Table"), "rank": 1, "vector_array": True}
        self.assertIn("field_type_eligibility_unverified", reasons(candidate(original, loaded)))


if __name__ == "__main__":
    unittest.main()

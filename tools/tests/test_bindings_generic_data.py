"""Closed generic data fields and engine enum aliases in pure declaration reports."""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_bindings as base_tests
from test_bindings import attribute, field, named, record
from test_bindings_generic import SERIALIZE, by_name, candidate, instance, parameter, reasons, type_parameter


def notes(result):
    return [(item["reason"], item["detail"]) for item in result["notes"]]


def assembly(report, name):
    found = [a for a in report["assemblies"] if a["name"] == name]
    if found:
        return found[0]["types"]
    report["assemblies"].append({"name": name, "types": []})
    return report["assemblies"][-1]["types"]


class GenericDataFieldTests(unittest.TestCase):
    """Example.Widget has [SerializeField] Example.Dictionary`2<int, Example.Target> m_lookup.

    The shape of the shipped SerializableDictionary: Dictionary`2<TKey, TValue> :
    DictionaryBase`3<TKey, TValue, Pair`2<TKey, TValue>>; the base keeps a
    readonly runtime Dictionary and a [SerializeField] List<TPair> m_values;
    Pair`2 has [SerializeField] TKey m_key and TValue m_value.
    """

    def fixture(self, key=None, value=None):
        original, loaded = base_tests.BindingTests().fixture()
        key = key or named("mscorlib", "System.Int32")
        value = value or named("Example", "Example.Target")
        scriptable = named("UnityEngine.CoreModule", "UnityEngine.ScriptableObject")
        runtime = instance("mscorlib", "System.Collections.Generic.Dictionary`2", type_parameter(0), type_parameter(1))
        values = instance("mscorlib", "System.Collections.Generic.List`1", type_parameter(2))
        for report in (original, loaded):
            by_name(report, "Example.Widget")["fields"] = [
                field("m_lookup", instance("Example", "Example.Dictionary`2", copy.deepcopy(key), copy.deepcopy(value)),
                      flags=1, attributes=SERIALIZE)]
            dictionary = record("Example", "Example.Dictionary`2", [], instance(
                "Example", "Example.DictionaryBase`3", type_parameter(0), type_parameter(1),
                instance("Example", "Example.Pair`2", type_parameter(0), type_parameter(1))), flags=0x102001)
            dictionary["generic_parameters"] = [parameter(0, "TKey"), parameter(1, "TValue")]
            base = record("Example", "Example.DictionaryBase`3", [
                field("m_dictionary", runtime, flags=0x21), field("m_values", values, flags=1, attributes=SERIALIZE)],
                named("mscorlib", "System.Object"), flags=0x102081)
            base["is_abstract"] = True
            base["generic_parameters"] = [parameter(0, "TKey"), parameter(1, "TValue"), parameter(2, "TPair")]
            pair = record("Example", "Example.Pair`2", [
                field("m_key", type_parameter(0), flags=1, attributes=SERIALIZE),
                field("m_value", type_parameter(1), flags=1, attributes=SERIALIZE)],
                named("mscorlib", "System.Object"), flags=0x102001)
            pair["generic_parameters"] = [parameter(0, "TKey"), parameter(1, "TValue")]
            target = record("Example", "Example.Target", [], scriptable)
            target["unity_scriptable_object"] = True
            report["assemblies"][0]["types"].extend([dictionary, base, pair, target])
            assembly(report, "UnityEngine.CoreModule").append(
                record("UnityEngine.CoreModule", "UnityEngine.ScriptableObject", base=named("UnityEngine.CoreModule", "UnityEngine.Object")))
        return original, loaded

    def test_closed_generic_dictionary_compares_its_backing_list(self):
        original, loaded = self.fixture()
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])

    def test_a_changed_member_of_the_stored_pair_blocks(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Pair`2")["fields"][1]["name"] = "m_renamed"
        self.assertIn("serialized_field_order_or_names_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.DictionaryBase`3")["fields"][1]["name"] = "m_entries"
        self.assertIn("serialized_field_order_or_names_mismatch", reasons(candidate(original, loaded)))

    def test_changed_type_argument_blocks(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Widget")["fields"][0]["field_type"]["arguments"][0] = named("mscorlib", "System.String")
        self.assertIn("type_identity_mismatch", reasons(candidate(original, loaded)))
        # The argument's assembly is part of its identity.
        original, loaded = self.fixture()
        by_name(loaded, "Example.Widget")["fields"][0]["field_type"]["arguments"][1]["assembly"] = "Other"
        self.assertIn("type_identity_mismatch", reasons(candidate(original, loaded)))

    def test_stored_members_are_closed_with_the_instance_arguments(self):
        # Substituted, m_value becomes an unresolved class and blocks; left as a
        # generic parameter it would block for a different reason.
        original, loaded = self.fixture(value=named("Example", "Example.Missing"))
        self.assertIn("missing_layout_dependency", reasons(candidate(original, loaded)))
        original, loaded = self.fixture(key=named("mscorlib", "System.String"))
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])

    def test_substituted_nested_containers_still_block(self):
        # Pair`2<int, List<int>> stores a List in a class: Unity accepts it.
        nested = instance("mscorlib", "System.Collections.Generic.List`1", named("mscorlib", "System.Int32"))
        original, loaded = self.fixture(value=nested)
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        # A member that closes to List<List<int>> is a nested container Unity skips.
        original, loaded = self.fixture(value=nested)
        for report in (original, loaded):
            by_name(report, "Example.Pair`2")["fields"][1]["field_type"] = instance(
                "mscorlib", "System.Collections.Generic.List`1", type_parameter(1))
        self.assertIn("unsupported_nested_serialized_container", reasons(candidate(original, loaded)))

    def test_an_argument_no_member_stores_is_not_data(self):
        # Like SerializableDictionary`3<ISurface, Object, ...>: the interface key
        # type is only a runtime key; the stored member uses another argument.
        interface = named("Example", "Example.IThing")
        original, loaded = self.fixture(key=interface)
        for report in (original, loaded):
            thing = record("Example", "Example.IThing", [], None, flags=0xa1)
            thing["is_abstract"] = True
            report["assemblies"][0]["types"].append(thing)
            by_name(report, "Example.Pair`2")["fields"].pop(0)
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        # Stored, the same interface argument blocks.
        original, loaded = self.fixture(key=interface)
        for report in (original, loaded):
            thing = record("Example", "Example.IThing", [], None, flags=0xa1)
            thing["is_abstract"] = True
            report["assemblies"][0]["types"].append(thing)
        self.assertEqual("blocked", candidate(original, loaded)["status"])

    def test_runtime_library_and_engine_generics_stay_blocked(self):
        # These carry [Serializable] yet Unity 2022 does not serialize them as data.
        for library, definition in (("mscorlib", "System.Collections.Generic.Dictionary`2"),
                                    ("System.Core", "System.Collections.Generic.HashSet`1"),
                                    ("netstandard", "Example.Shim`2"),
                                    ("UnityEngine.CoreModule", "UnityEngine.Events.UnityEvent`1"),
                                    ("UnityEngine", "Example.EngineData`2")):
            with self.subTest(definition=definition):
                original, loaded = self.fixture()
                arity = 1 if definition.endswith("`1") else 2
                for report in (original, loaded):
                    data = record(library, definition, [], named("mscorlib", "System.Object"), flags=0x102001)
                    data["generic_parameters"] = [parameter(i, "T%d" % i) for i in range(arity)]
                    assembly(report, library).append(data)
                    by_name(report, "Example.Widget")["fields"][0]["field_type"] = instance(
                        library, definition, *[named("mscorlib", "System.Int32")] * arity)
                self.assertIn("unsupported_serialized_generic", reasons(candidate(original, loaded)))

    def test_package_script_generics_are_compared(self):
        # A package assembly such as Unity.Addressables is ordinary script code.
        original, loaded = self.fixture()
        moving = ("Example.Dictionary`2", "Example.DictionaryBase`3", "Example.Pair`2")
        for report in (original, loaded):
            types = report["assemblies"][0]["types"]
            moved = [t for t in types if t["full_name"] in moving]
            report["assemblies"][0]["types"] = [t for t in types if t["full_name"] not in moving]
            for item in moved:
                item["assembly"] = "Unity.Example"
            assembly(report, "Unity.Example").extend(moved)
            by_name(report, "Example.Widget")["fields"][0]["field_type"]["assembly"] = "Unity.Example"
            dictionary_base = by_name(report, "Example.Dictionary`2")["base_type"]
            dictionary_base["assembly"] = dictionary_base["arguments"][2]["assembly"] = "Unity.Example"
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])

    def test_definition_without_serializable_bit_blocks(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Dictionary`2")["attributes"] &= ~0x2000
        self.assertIn("field_type_eligibility_unverified", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.Dictionary`2")["attributes"] &= ~0x2000
        self.assertIn("type_flags_mismatch", reasons(candidate(original, loaded)))

    def test_definition_arity_and_parameters_must_match(self):
        original, loaded = self.fixture()
        by_name(loaded, "Example.Dictionary`2")["generic_parameters"].append(parameter(2, "TExtra"))
        self.assertIn("generic_base_arity_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        for report in (original, loaded):
            by_name(report, "Example.Dictionary`2")["generic_parameters"].append(parameter(2, "TExtra"))
        self.assertIn("generic_base_arity_mismatch", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(loaded, "Example.Pair`2")["generic_parameters"][1]["name"] = "TOther"
        self.assertIn("type_shape_mismatch", reasons(candidate(original, loaded)))

    def test_missing_or_incomplete_definition_blocks(self):
        original, loaded = self.fixture()
        loaded["assemblies"][0]["types"] = [t for t in loaded["assemblies"][0]["types"] if t["full_name"] != "Example.Pair`2"]
        self.assertIn("missing_layout_dependency", reasons(candidate(original, loaded)))
        original, loaded = self.fixture()
        by_name(original, "Example.Dictionary`2")["schema_complete"] = False
        self.assertIn("incomplete_or_invalid_layout", reasons(candidate(original, loaded)))

    def test_method_parameter_arguments_stay_unsupported(self):
        method = {"kind": "generic_parameter", "assembly": "Example", "parameter_owner": "method", "parameter_index": 0}
        original, loaded = self.fixture(value=method)
        self.assertIn("unsupported_serialized_type", reasons(candidate(original, loaded)))

    def test_closed_generic_struct_is_inline_data(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            data = record("Example", "Example.Range`1", [field("min", type_parameter(0)), field("max", type_parameter(0))],
                          named("mscorlib", "System.ValueType"), flags=0x102109)
            data.update(is_value_type=True, generic_parameters=[parameter(0, "T")])
            report["assemblies"][0]["types"].append(data)
            by_name(report, "Example.Widget")["fields"][0]["field_type"] = instance(
                "Example", "Example.Range`1", named("mscorlib", "System.Single"))
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        by_name(loaded, "Example.Range`1")["fields"][1]["name"] = "maximum"
        self.assertIn("serialized_field_order_or_names_mismatch", reasons(candidate(original, loaded)))

    def test_closed_generic_unity_object_field_is_a_pointer(self):
        original, loaded = self.fixture()
        scriptable = named("UnityEngine.CoreModule", "UnityEngine.ScriptableObject")
        for report in (original, loaded):
            asset = record("Example", "Example.Asset`1", [field("m_payload", type_parameter(0), flags=1, attributes=SERIALIZE)],
                           scriptable)
            asset.update(unity_scriptable_object=True, generic_parameters=[parameter(0, "T")])
            report["assemblies"][0]["types"].append(asset)
            by_name(report, "Example.Widget")["fields"][0]["field_type"] = instance(
                "Example", "Example.Asset`1", named("mscorlib", "System.Int32"))
        # A PPtr stores only the identity: the pointed-to payload is not compared.
        by_name(loaded, "Example.Asset`1")["fields"][0]["name"] = "m_changed"
        self.assertEqual("compatible_layout", candidate(original, loaded)["status"])
        by_name(loaded, "Example.Asset`1")["generic_parameters"].append(parameter(1, "U"))
        self.assertEqual("blocked", candidate(original, loaded)["status"])

    def test_endlessly_growing_generic_data_is_blocked_at_unity_depth(self):
        # Node`1<T> { [SerializeField] Node`1<List<T>> next; } closes to a new type at every level.
        original, loaded = self.fixture()
        for report in (original, loaded):
            grown = instance("Example", "Example.Node`1", instance("mscorlib", "System.Collections.Generic.List`1", type_parameter(0)))
            node = record("Example", "Example.Node`1", [field("next", grown, flags=1, attributes=SERIALIZE)],
                          named("mscorlib", "System.Object"), flags=0x102001)
            node["generic_parameters"] = [parameter(0, "T")]
            report["assemblies"][0]["types"].append(node)
            by_name(report, "Example.Widget")["fields"][0]["field_type"] = instance(
                "Example", "Example.Node`1", named("mscorlib", "System.Int32"))
        result = candidate(original, loaded)
        self.assertEqual("blocked", result["status"])
        self.assertIn("serialized_generic_depth_exceeded", reasons(result))


class EngineEnumAliasTests(unittest.TestCase):
    """Example.Widget stores an enum; the Editor's engine module may add aliases."""

    SHIPPED = (("OSXPlayer", 1), ("WP8Player", 21), ("BlackBerryPlayer", 22), ("tvOS", 31))

    def fixture(self, library="UnityEngine.CoreModule", name="UnityEngine.RuntimePlatform", constants=SHIPPED, ours=None):
        original, loaded = base_tests.BindingTests().fixture()
        enum_type = named(library, name)
        for report, values in ((original, constants), (loaded, constants if ours is None else ours)):
            enum = record(library, name, [field("value__", flags=0x606)], named("mscorlib", "System.Enum"), flags=257)
            enum.update(is_enum=True, is_value_type=True, enum_underlying_type=named("mscorlib", "System.Int32"))
            for constant, value in values:
                item = field(constant, enum_type, 32854)
                item.update(has_default_value=True, default_value_complete=True, default_value=value,
                            default_value_type=named("mscorlib", "System.Int32"))
                enum["fields"].append(item)
            assembly(report, library).append(enum)
            by_name(report, "Example.Widget")["fields"] = [field("m_platform", enum_type, flags=1, attributes=SERIALIZE)]
        return original, loaded

    def editor(self):
        # The 2022.3.54f1 Editor's CoreModule declares obsolete BB10Player = 22.
        return self.SHIPPED[:2] + (("BB10Player", 22),) + self.SHIPPED[2:]

    def test_identical_engine_enum_has_no_note(self):
        result = candidate(*self.fixture())
        self.assertEqual(("compatible_layout", []), (result["status"], notes(result)))

    def test_unshipped_engine_alias_is_compatible_with_a_note(self):
        result = candidate(*self.fixture(ours=self.editor()))
        self.assertEqual("compatible_layout", result["status"])
        self.assertEqual([("enum_unshipped_constants", "['BB10Player']")], notes(result))

    def test_game_and_package_enums_stay_strict(self):
        for library, name in (("Example", "Example.Mode"), ("UnityEngine.UI", "UnityEngine.UI.Mode"),
                              ("Unity.Example", "UnityEngine.Mode")):
            with self.subTest(library=library):
                result = candidate(*self.fixture(library, name, ours=self.editor()))
                self.assertIn("enum_constants_mismatch", reasons(result))
                self.assertEqual([], notes(result))

    def test_every_shipped_constant_must_survive_in_order(self):
        changed = {
            "missing": self.SHIPPED[1:] + (("BB10Player", 22),),
            "missing last": self.SHIPPED[:3] + (("BB10Player", 22),),
            "value": (("OSXPlayer", 2),) + self.SHIPPED[1:] + (("BB10Player", 22),),
            "order": (self.SHIPPED[1], self.SHIPPED[0]) + self.SHIPPED[2:] + (("BB10Player", 22),),
            "renamed": (("OSXPlayerRenamed", 1),) + self.SHIPPED[1:] + (("BB10Player", 22),),
        }
        for label, ours in changed.items():
            with self.subTest(label=label):
                self.assertIn("enum_constants_mismatch", reasons(candidate(*self.fixture(ours=ours))))

    def test_extra_constants_on_the_shipped_side_block(self):
        original, loaded = self.fixture(constants=self.editor(), ours=self.SHIPPED)
        self.assertIn("enum_constants_mismatch", reasons(candidate(original, loaded)))

    def test_constant_type_must_match(self):
        original, loaded = self.fixture(ours=self.editor())
        enum = by_name(loaded, "UnityEngine.RuntimePlatform")
        enum["fields"][1]["default_value_type"] = named("mscorlib", "System.UInt32")
        self.assertIn("enum_constants_mismatch", reasons(candidate(original, loaded)))

    def test_underlying_type_must_match(self):
        original, loaded = self.fixture(ours=self.editor())
        enum = by_name(loaded, "UnityEngine.RuntimePlatform")
        enum["enum_underlying_type"] = named("mscorlib", "System.UInt32")
        self.assertIn("enum_underlying_mismatch", reasons(candidate(original, loaded)))


if __name__ == "__main__":
    unittest.main()

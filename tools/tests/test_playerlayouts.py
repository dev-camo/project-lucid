"""Player-plane layouts: the exact reader profiles, runtime-library projection and the Editor guard."""
import copy
from pathlib import Path
import re
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bindings, playerlayouts, playerschema
from test_bindings import field, named, record

REPO = Path(__file__).resolve().parents[2]
URP = "Unity.RenderPipelines.Universal.Runtime"


class ReaderProfileTests(unittest.TestCase):
    def table(self, name):
        source = (REPO / "tools/player-schema/Program.cs").read_text()
        block = re.search(r"HashSet<string> " + name + r" = new\(StringComparer\.Ordinal\)\n    \{\n(.*?)\n    \};", source, re.S)
        return {tuple(entry.split(":", 1)) for entry in re.findall(r'"([^"]+)"', block[1])}

    def test_reader_and_wrapper_pin_the_same_exact_profiles(self):
        self.assertEqual(playerschema.URP_ALLOWED, self.table("UniversalRenderer"))
        self.assertEqual(playerschema.ALLOWED, self.table("ZoneTheme"))
        self.assertEqual(96, len(playerschema.URP_ALLOWED))
        self.assertLessEqual(playerschema.URP_ROOTS, playerschema.URP_ALLOWED)
        self.assertEqual({"zone-theme-v1", "urp-renderer-v1"}, set(playerschema.PROFILES))

    def test_an_unknown_profile_is_refused_before_any_work(self):
        with self.assertRaisesRegex(ValueError, "exact profile"):
            playerschema.run_player_schema(REPO, REPO / ".cache/project-lucid", "macos", "urp-renderer-v2")


def ref(assembly, name):
    return named(assembly, name)


def player_fixture():
    """Original, player (netstandard-scoped) and Editor reports for one URP-like type."""
    def owner(fields, cads=()):
        result = record(URP, "Example.Feature", fields, ref("UnityEngine.CoreModule", "UnityEngine.ScriptableObject"))
        result["unity_scriptable_object"] = True
        result["custom_attributes"] = list(cads)
        return result
    base = lambda assembly: [record("UnityEngine.CoreModule", "UnityEngine.ScriptableObject", base=ref("UnityEngine.CoreModule", "UnityEngine.Object")),
                             record("UnityEngine.CoreModule", "UnityEngine.Object", base=ref(assembly, "System.Object"))]
    original = {"schema_version": 1, "status": "ready", "unity_version": "2022.3.54f1", "assemblies": [
        {"name": URP, "types": [owner([field("m_Active", ref("mscorlib", "System.Boolean")), field("count")])]},
        {"name": "UnityEngine.CoreModule", "types": base("mscorlib")}]}
    player = {"unity_version": "2022.3.54f1", "assemblies": [
        {"name": URP, "types": [owner([field("m_Active", ref("netstandard", "System.Boolean")), field("count", ref("netstandard", "System.Int32"))])]},
        {"name": "UnityEngine.CoreModule", "types": base("netstandard")},
        {"name": "netstandard", "types": [record("netstandard", "System.Object")]}]}
    editor_only = {"assembly": "UnityEditor.CoreModule", "full_name": "UnityEditor.ShaderKeywordFilter.RemoveIfAttribute",
                   "arguments": [], "fields": [], "properties": []}
    editor = copy.deepcopy(original)
    feature = editor["assemblies"][0]["types"][0]
    feature["fields"].insert(1, field("m_PrefilterExample", ref("mscorlib", "System.Boolean"), flags=1))
    feature["fields"][0]["custom_attributes"] = [editor_only]
    feature["custom_attributes"] = [dict(editor_only, full_name="Example.URPHelpURLAttribute", assembly=URP)]
    return original, player, editor


class PlayerPlaneTests(unittest.TestCase):
    key = (URP, "Example.Feature")

    def verdict(self, original, player, editor):
        return playerlayouts.compare_player_plane(original, player, editor, {self.key})

    def test_editor_only_members_absent_from_the_player_compile_are_recorded_not_blocking(self):
        result = self.verdict(*player_fixture())
        self.assertEqual("compatible_layout", result["verdicts"][self.key]["status"])
        delta = result["editor_only"][URP + ":Example.Feature"]
        self.assertEqual(["m_PrefilterExample"], delta["fields"])
        self.assertEqual(["Example.URPHelpURLAttribute"], delta["type_attributes"])
        self.assertEqual([["m_Active", ["UnityEditor.ShaderKeywordFilter.RemoveIfAttribute"]]], delta["field_attributes"])

    def test_the_editor_may_only_add_members(self):
        for change in ("missing-field", "field-flags", "field-type", "field-order", "missing-cad", "base", "serialized",
                       "shape", "missing"):
            with self.subTest(change=change):
                original, player, editor = player_fixture()
                feature = editor["assemblies"][0]["types"][0]; fields = feature["fields"]
                if change == "missing-field": del fields[2]
                elif change == "field-flags": fields[2]["attributes"] = 1; fields[2].update(is_public=False, unity_serialization_candidate=False)
                elif change == "field-type": fields[2]["field_type"] = ref("mscorlib", "System.Int64")
                elif change == "field-order": fields[0], fields[2] = fields[2], fields[0]
                elif change == "missing-cad":
                    for report in (original, player):
                        report["assemblies"][0]["types"][0]["custom_attributes"] = [
                            {"assembly": URP, "full_name": "Example.Shipped", "arguments": [], "fields": [], "properties": []}]
                elif change == "base": feature["base_type"] = ref("UnityEngine.CoreModule", "UnityEngine.Object")
                elif change == "serialized":
                    # A player field the Editor would serialize differently.
                    fields[2]["custom_attributes"] = [{"assembly": "mscorlib", "full_name": "System.NonSerializedAttribute",
                                                       "arguments": [], "fields": [], "properties": []}]
                    fields[2]["non_serialized"] = True; fields[2]["unity_serialization_candidate"] = False
                elif change == "shape": feature["is_abstract"] = True
                elif change == "missing": editor["assemblies"][0]["types"] = []
                self.assertEqual("blocked", self.verdict(original, player, editor)["verdicts"][self.key]["status"])

    def test_a_field_in_both_player_and_editor_but_not_the_original_blocks(self):
        original, player, editor = player_fixture()
        for report in (player, editor):
            report["assemblies"][0]["types"][0]["fields"].append(field("m_Extra", ref(report["assemblies"][0]["types"][0]["fields"][1]["field_type"]["assembly"], "System.Int32")))
        self.assertEqual("blocked", self.verdict(original, player, editor)["verdicts"][self.key]["status"])

    def test_runtime_library_projection_is_per_type_and_fail_closed(self):
        original, player, editor = player_fixture()
        projected = playerlayouts.project_player_report(player)
        names = {a["name"] for a in projected["assemblies"]}
        self.assertNotIn("netstandard", names); self.assertIn("mscorlib", names)
        self.assertEqual("mscorlib", projected["assemblies"][0]["types"][0]["fields"][0]["field_type"]["assembly"])
        browsable = playerlayouts._project({"kind": "named", "assembly": "netstandard", "reflection_full_name":
                                            "System.ComponentModel.EditorBrowsableAttribute", "canonical_name": "x"})
        self.assertEqual("System", browsable["assembly"])
        for change in ("unlisted", "scoped-parameter", "already-mscorlib", "container"):
            with self.subTest(change=change):
                altered = copy.deepcopy(player)
                if change == "unlisted":
                    altered["assemblies"][0]["types"][0]["fields"][1]["field_type"] = ref("netstandard", "System.Decimal")
                elif change == "scoped-parameter":
                    altered["assemblies"][0]["types"][0]["fields"][1]["field_type"] = {
                        "kind": "generic_parameter", "assembly": "netstandard", "parameter_owner": "type", "parameter_index": 0}
                elif change == "already-mscorlib":
                    altered["assemblies"].append({"name": "mscorlib", "types": []})
                elif change == "container":
                    altered["assemblies"][0]["types"][0]["fields"][1]["field_type"] = {
                        "kind": "array", "assembly": "netstandard", "rank": 1, "vector_array": True, "element": ref("Example", "Example.Item")}
                with self.assertRaises(bindings.LayoutError):
                    playerlayouts.project_player_report(altered)
        # A listed name outside netstandard is never rewritten.
        self.assertEqual({"kind": "named", "assembly": "Other", "reflection_full_name": "System.Int32"},
                         playerlayouts._project({"kind": "named", "assembly": "Other", "reflection_full_name": "System.Int32"}))

    def test_builtin_structs_need_the_installed_serializer_predicate(self):
        original, player, editor = player_fixture()
        vector = record("UnityEngine.CoreModule", "UnityEngine.Vector3")
        vector["builtin_serializer_eligibility"] = False
        for report in (original, editor):
            report["assemblies"][1]["types"].append(copy.deepcopy(vector))
        player["assemblies"][1]["types"].append(vector)
        self.assertEqual("blocked", self.verdict(original, player, editor)["verdicts"][self.key]["status"])
        vector["builtin_serializer_eligibility"] = True
        self.assertEqual("compatible_layout", self.verdict(original, player, editor)["verdicts"][self.key]["status"])

    def test_the_player_plane_requires_the_exact_engine_version_and_its_roots(self):
        original, player, editor = player_fixture()
        player["unity_version"] = "2022.3.53f1"
        with self.assertRaises(bindings.LayoutError):
            self.verdict(original, player, editor)
        with self.assertRaises(bindings.LayoutError):
            playerlayouts.compare_player_plane(*player_fixture(), {(URP, "Example.Missing")})

    def test_the_editor_only_delta_is_the_exact_reviewed_roster(self):
        roster = copy.deepcopy(playerlayouts.URP_EDITOR_ONLY)
        self.assertIs(roster, playerlayouts.require_editor_only(roster))
        key = "Unity.RenderPipelines.Universal.Runtime:UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
        self.assertIn("m_AnyShadowsSupported", roster[key]["fields"])
        for change in ("added-field", "removed-field", "added-type", "added-attribute"):
            with self.subTest(change=change):
                altered = copy.deepcopy(roster)
                if change == "added-field": altered[key]["fields"].append("m_NewEditorField")
                elif change == "removed-field": altered[key]["fields"].remove("m_AnyShadowsSupported")
                elif change == "added-type": altered["Example:Example.Type"] = {"fields": ["x"], "field_attributes": [], "type_attributes": []}
                elif change == "added-attribute": altered[key]["type_attributes"].append("Example.EditorAttribute")
                with self.assertRaises(bindings.LayoutError):
                    playerlayouts.require_editor_only(altered)

    def test_vector2_keeps_one_exact_cad_order_per_plane(self):
        import test_bindings_vector2
        vector = test_bindings_vector2.vector2_record()
        reordered = copy.deepcopy(vector)
        cads = reordered["custom_attributes"]
        reordered["custom_attributes"] = [cads[2], cads[1], cads[0], cads[3]]
        report = lambda value: {"unity_version": "2022.3.54f1", "assemblies": [{"name": "UnityEngine.CoreModule", "types": [value]}]}
        key = ("UnityEngine.CoreModule", "UnityEngine.Vector2")
        self.assertTrue(bindings._Comparison(report(vector), report(reordered), loaded_plane="player")._vector2_data_contract(key))
        with self.assertRaises(bindings.LayoutError):
            bindings._Comparison(report(vector), report(reordered))._vector2_data_contract(key)
        with self.assertRaises(bindings.LayoutError):
            bindings._Comparison(report(vector), report(vector), loaded_plane="player")._vector2_data_contract(key)
        with self.assertRaises(bindings.LayoutError):
            bindings._Comparison(report(vector), report(vector), loaded_plane="other")


if __name__ == "__main__":
    unittest.main()

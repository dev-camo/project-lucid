"""Whole-byte configuration pointer tests with synthetic identities and content."""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.configurationbindings import rewrite_configuration_pointers


def binding(index):
    assembly, name = (("HLUnityCore.Runtime", "Hardlight.ActiveRuntimeConfiguration"),
                      ("HLUnityCore.Runtime", "Hardlight.RuntimeConfiguration"),
                      ("Game.Runtime", "HardlightProject.GameLevels"))[index]
    return dict(assembly=assembly, full_name=name, exported_guid=str(index+1)*32,
                target_guid=str(index+4)*32, exported_file_id=11500000, target_file_id=11500000)


def asset(index):
    return "{fileID: 11400000, guid: " + format(index+16, "032x") + ", type: 2}"


def document(index, own):
    row = binding(index)
    common = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
              "--- !u!114 &11400000\nMonoBehaviour:\n"
              "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
              "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
              "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
              "  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}\n"
              "  m_Name: SyntheticConfiguration\n  m_EditorClassIdentifier:\n")
    return (common + own).encode()


def runtime_document():
    return document(1, "  m_configurationAssets:\n" + "".join("  - " + asset(i) + "\n" for i in range(12)))


def game_document():
    return document(2, "  m_baseAsset: " + asset(42) + "\n"
        "  m_overriddenFields:\n  - m_loadableLevels\n  - m_gameScenes\n"
        "  - m_gameplayEntries\n  - m_gameCenter\n  m_systemScenes:\n" +
        "".join("  - " + asset(i) + "\n" for i in range(13)) +
        "  m_uiScenes: []\n  m_gameplayEntries:\n  - " + asset(100) + "\n")


def definitions_binding():
    return dict(assembly="Game.Runtime", full_name="HardlightProject.DataDefinitions",
                exported_guid="7" * 32, target_guid="8" * 32,
                exported_file_id=11500000, target_file_id=11500000)


def definitions_document(own):
    return document(0, own).replace(binding(0)["exported_guid"].encode(), b"7" * 32)


class DataDefinitionsDocumentTests(unittest.TestCase):
    def assert_pointer_only(self, content):
        row = definitions_binding()
        result, count = rewrite_configuration_pointers(content, [row])
        old = ("  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}").encode()
        new = ("  m_Script: {fileID: 11500000, guid: " + row["target_guid"] + ", type: 3}").encode()
        self.assertEqual(1, content.count(old))
        self.assertEqual((content.replace(old, new), 1), (result, count))
        return result

    def test_ordered_57_pointer_array_changes_only_script(self):
        raw = definitions_document("  DataDefinitionsArray:\n" +
            "".join("  - " + asset(i) + "\n" for i in range(57)))
        self.assert_pointer_only(raw)

    def test_empty_duplicates_nulls_and_newline_bytes_are_retained(self):
        for own in ("  DataDefinitionsArray: []\n",
                    "  DataDefinitionsArray:\n  - " + asset(1) + "\n  - {fileID: 0}\n  - " + asset(1) + "\n"):
            raw = definitions_document(own)
            for content in (raw, raw.replace(b"\n", b"\r\n"), raw.rstrip(b"\n")):
                with self.subTest(content=content):
                    self.assert_pointer_only(content)

    def test_exact_owner_and_field_set_remain_required(self):
        raw = definitions_document("  DataDefinitionsArray: []\n")
        for change in (b"  DataDefinitionsArray: []\n  Other: []\n",
                       b"  DataDefinitionsArray: []\n  DataDefinitionsArray: []\n",
                       b"  m_elements: []\n", b""):
            with self.subTest(change=change), self.assertRaises(ValueError):
                rewrite_configuration_pointers(raw.replace(b"  DataDefinitionsArray: []\n", change), [definitions_binding()])
        with self.assertRaises(ValueError):
            rewrite_configuration_pointers(raw, [dict(definitions_binding(), full_name="HardlightProject.StateMachinesGroup")])

    def test_nested_alias_tag_and_invalid_asset_shapes_are_rejected(self):
        for own in ("  DataDefinitionsArray:\n", "  DataDefinitionsArray: null\n",
                    "  DataDefinitionsArray:\n  - *group\n",
                    "  DataDefinitionsArray:\n  - !Group " + asset(1) + "\n",
                    "  DataDefinitionsArray:\n  - Name: group\n    Data: " + asset(1) + "\n",
                    "  DataDefinitionsArray:\n  - {fileID: 1}\n",
                    "  DataDefinitionsArray:\n  - {fileID: 11400000, guid: " + "0" * 32 + ", type: 2}\n",
                    "  DataDefinitionsArray: []\n  - " + asset(1) + "\n"):
            with self.subTest(own=own), self.assertRaises(ValueError):
                rewrite_configuration_pointers(definitions_document(own), [definitions_binding()])

    def test_prepared_dispatch_preserves_unrelated_asset_references(self):
        from lucidlib.scriptbindings import rewrite_prepared_script_pointers
        raw = definitions_document("  DataDefinitionsArray:\n  - " + asset(1) + "\n  - " + asset(2) + "\n")
        expected = rewrite_configuration_pointers(raw, [definitions_binding()])
        self.assertEqual(expected, rewrite_prepared_script_pointers(raw, [binding(0), definitions_binding()]))


def fsm_binding():
    return dict(assembly="HLUnityCore.Runtime", full_name="Hardlight.FiniteStateMachineScriptableObject",
                exported_guid="9" * 32, target_guid="a" * 32,
                exported_file_id=11500000, target_file_id=11500000)


def fsm_document(referenced="  m_referencedScriptableObjects: []\n", name="FSMApplication_Splash",
                 path="FSM/Application/Application_Splash.json", embedded=""):
    own = ("  m_name: " + name + "\n  m_isOverwriteOk: 0\n  m_reuseExisting: 1\n  m_releaseOnDestroy: 1\n" +
           referenced + "  m_relativePathToJSON: " + path + "\n  m_embeddedJSON:" + (" " + embedded if embedded else "") + "\n")
    return document(0, own).replace(binding(0)["exported_guid"].encode(), b"9" * 32)


class FiniteStateMachineAssetDocumentTests(unittest.TestCase):
    """The 124 exported FSM definitions: 123 with no references, one with two."""

    def assert_pointer_only(self, content):
        row = fsm_binding()
        result, count = rewrite_configuration_pointers(content, [row])
        old = ("  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}").encode()
        new = ("  m_Script: {fileID: 11500000, guid: " + row["target_guid"] + ", type: 3}").encode()
        self.assertEqual((content.replace(old, new), 1), (result, count))

    def test_observed_definition_shapes_change_only_script(self):
        self.assert_pointer_only(fsm_document())
        self.assert_pointer_only(fsm_document("  m_referencedScriptableObjects:\n  - " + asset(1) + "\n  - " + asset(2) + "\n",
                                              name="FSMApplication", path="FSM/Application/Application.json"))
        self.assert_pointer_only(fsm_document().replace(b"\n", b"\r\n"))

    def test_embedded_graphs_and_unobserved_shapes_are_rejected(self):
        cases = {
            "embedded JSON": fsm_document(embedded='"{}"'),
            "block embedded JSON": fsm_document(embedded="|\n    {}"),
            "Boolean range": fsm_document().replace(b"m_reuseExisting: 1", b"m_reuseExisting: 2"),
            "quoted name": fsm_document(name="'FSM'"),
            "empty name": fsm_document(name=""),
            "quoted path": fsm_document(path="'FSM/a.json'"),
            "scene pointer": fsm_document("  m_referencedScriptableObjects:\n  - {fileID: 1}\n"),
            "nested reference": fsm_document("  m_referencedScriptableObjects:\n  - Name: x\n    Data: " + asset(1) + "\n"),
            "extra field": fsm_document() + b"  m_extra: 0\n",
            "missing field": fsm_document().replace(b"  m_isOverwriteOk: 0\n", b""),
        }
        for label, content in cases.items():
            with self.subTest(case=label), self.assertRaises(ValueError):
                rewrite_configuration_pointers(content, [fsm_binding()])


class ConfigurationDocumentTests(unittest.TestCase):
    def assert_pointer_only(self, content, index):
        row = binding(index)
        before = dict(row)
        result, count = rewrite_configuration_pointers(content, [row])
        old = ("  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}").encode()
        new = ("  m_Script: {fileID: " + str(row["target_file_id"]) + ", guid: " + row["target_guid"] + ", type: 3}").encode()
        self.assertEqual(1, content.count(old))
        self.assertEqual(content.replace(old, new), result)
        self.assertEqual(1, count)
        self.assertEqual(before, row)
        return result

    def test_active_configuration_changes_only_script_identity(self):
        self.assert_pointer_only(document(0, "  ActiveConfig: " + asset(12) + "\n"), 0)

    def test_runtime_keeps_all_twelve_entries_in_order(self):
        content = runtime_document()
        result = self.assert_pointer_only(content, 1)
        self.assertEqual(12, result.count(b"  - {fileID: 11400000,"))
        self.assertEqual(content.split(b"  m_configurationAssets:")[1], result.split(b"  m_configurationAssets:")[1])

    def test_game_configuration_retains_scenes_overrides_and_entries(self):
        content = game_document()
        result = self.assert_pointer_only(content, 2)
        self.assertEqual(content.split(b"  m_baseAsset:")[1], result.split(b"  m_baseAsset:")[1])
        self.assertIn(b"  - m_gameScenes\n", result)

    def test_base_configuration_empty_lists_and_null_base_survive(self):
        self.assert_pointer_only(document(2, "  m_baseAsset: {fileID: 0}\n"
            "  m_overriddenFields: []\n  m_systemScenes: []\n  m_uiScenes: []\n  m_gameplayEntries: []\n"), 2)

    def test_crlf_and_no_final_newline_are_preserved(self):
        for content in [runtime_document().replace(b"\n", b"\r\n"), game_document().rstrip(b"\n")]:
            with self.subTest(content=content):
                self.assert_pointer_only(content, 1 if b"m_configurationAssets" in content else 2)

    def test_duplicate_and_null_runtime_entries_survive_in_order(self):
        self.assert_pointer_only(document(1, "  m_configurationAssets:\n  - " + asset(1) +
            "\n  - {fileID: 0}\n  - " + asset(1) + "\n"), 1)

    def test_unselected_document_and_empty_binding_list_are_unchanged(self):
        for content, rows in [(document(0, "  ActiveConfig: {fileID: 0}\n"), [binding(1)]), (b"ordinary bytes", [])]:
            self.assertEqual((content, 0), rewrite_configuration_pointers(content, rows))

    def test_all_three_bindings_do_not_change_unrelated_pointers(self):
        content = game_document()
        rows = [binding(i) for i in range(3)]
        result, count = rewrite_configuration_pointers(content, rows)
        self.assertEqual(count, 1)
        expected, _ = rewrite_configuration_pointers(content, [binding(2)])
        self.assertEqual(expected, result)

    def test_duplicate_binding_owner_export_or_target_is_rejected(self):
        a = binding(0); b = binding(1)
        cases = [[a, a], [a, dict(a, exported_guid="9"*32)],
                 [a, dict(b, target_guid=a["target_guid"], target_file_id=a["target_file_id"])]]
        for rows in cases:
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                rewrite_configuration_pointers(runtime_document(), rows)

    def test_unknown_owner_and_invalid_pointer_identities_are_rejected(self):
        changes = [dict(full_name="Hardlight.AnotherConfiguration"), dict(assembly="Other"),
                   dict(exported_guid="0"*32), dict(target_guid="z"*32),
                   dict(exported_file_id=True), dict(exported_file_id=11400000),
                   dict(target_file_id=True), dict(target_file_id=0), dict(target_file_id=1<<63)]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                rewrite_configuration_pointers(runtime_document(), [dict(binding(1), **change)])

    def test_extra_duplicate_missing_or_reordered_fields_are_rejected(self):
        raw = game_document()
        cases = [raw+b"  m_unknown: []\n", raw+b"  m_uiScenes: []\n",
                 raw.replace(b"  m_uiScenes: []\n", b""),
                 raw.replace(b"  m_uiScenes: []\n  m_gameplayEntries:", b"  m_gameplayEntries:\n  m_uiScenes: []")]
        for content in cases:
            with self.subTest(content=content), self.assertRaises(ValueError):
                rewrite_configuration_pointers(content, [binding(2)])

    def test_unsupported_sequence_and_pointer_syntax_is_rejected(self):
        raw = runtime_document()
        first = ("  - " + asset(0)).encode()
        cases = [raw.replace(first, b"   - "+asset(0).encode()),
                 raw.replace(b"  m_configurationAssets:\n", b"  m_configurationAssets: []\n"),
                 raw.replace(first, b"  - *configuration"),
                 raw.replace(first, b"  - !Other "+asset(0).encode()),
                 raw.replace(first, b"  - {fileID: 11400000, guid: " + b"0"*32 + b", type: 2}"),
                 raw.replace(first, b"  - {fileID: 11400000, guid: "+b"a"*32+b", type: 3}"),
                 raw.replace(first, b"  - {fileID: 1}"),
                 raw.replace(b"  m_configurationAssets:", b'  "m_configurationAssets":')]
        for content in cases:
            with self.subTest(content=content), self.assertRaises(ValueError):
                rewrite_configuration_pointers(content, [binding(1)])

    def test_unsupported_override_strings_are_rejected(self):
        for replacement in [b"'m_gameScenes'", b'"m_gameScenes"', b"m_gameScenes: other", b"&override m_gameScenes", b"|", b"*override"]:
            with self.subTest(replacement=replacement), self.assertRaises(ValueError):
                rewrite_configuration_pointers(game_document().replace(b"  - m_gameScenes", b"  - "+replacement), [binding(2)])

    def test_standalone_envelope_unknown_common_values_or_multiple_documents_are_rejected(self):
        raw = runtime_document()
        changes = [(b"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n", b""),
                   (b"--- !u!114 &11400000", b"--- !u!114 &11400001"),
                   (b"MonoBehaviour:", b"Other:"), (b"m_GameObject: {fileID: 0}", b"m_GameObject: {fileID: 1}"),
                   (b"m_Enabled: 1", b"m_Enabled: 0"), (b"m_EditorHideFlags: 0", b"m_EditorHideFlags: 1"),
                   (b"m_EditorClassIdentifier:", b"m_EditorClassIdentifier: Other")]
        cases = [raw.replace(a,b) for a,b in changes] + [raw+raw, b"# unqualified prefix\n"+raw]
        for content in cases:
            with self.subTest(content=content), self.assertRaises(ValueError):
                rewrite_configuration_pointers(content, [binding(1)])

    def test_malformed_selected_script_and_embedded_selected_guids_are_rejected(self):
        raw = runtime_document(); guid = binding(1)["exported_guid"].encode()
        cases = [raw.replace(b"fileID: 11500000", b"fileID: 11400000"),
                 raw.replace(b", type: 3}", b", type: 2}"),
                 raw.replace(b"  m_Script:", b"   m_Script:"),
                 b"# "+guid+b"\n"+raw, raw+b"# "+guid+b"\n"]
        for content in cases:
            with self.subTest(content=content), self.assertRaises(ValueError):
                rewrite_configuration_pointers(content, [binding(1)])

    def test_invalid_encoding_and_control_characters_are_rejected(self):
        for content in [b"\xff", runtime_document()+b"\x00", runtime_document().replace(b"\n", b"\r"),
                        runtime_document()+"\u2028".encode(), runtime_document()+b"\v", runtime_document()+b"\f",
                        runtime_document()+b"\x1c", runtime_document()+"\x85".encode()]:
            with self.subTest(content=content), self.assertRaises(ValueError):
                rewrite_configuration_pointers(content, [binding(1)])

    def test_failed_transform_keeps_input_bytes_and_binding_rows_unchanged(self):
        content = game_document()+b"  m_unknown: []\n"
        rows = [binding(i) for i in range(3)]; before_rows = copy.deepcopy(rows)
        before_bytes = bytes(content)
        with self.assertRaises(ValueError): rewrite_configuration_pointers(content, rows)
        self.assertEqual(before_bytes, content)
        self.assertEqual(before_rows, rows)


def menu_binding(index):
    assembly, name = (("Game.Runtime", "HardlightProject.CoreGameConfiguration"),
                      ("HLLocalisation.Runtime", "Hardlight.HLLocalisationConfigurationAsset"),
                      ("Game.Runtime", "HardlightProject.HashedGroupConfiguration"))[index]
    return dict(assembly=assembly, full_name=name, exported_guid=str(index+7)*32,
                target_guid=chr(ord("b")+index)*32, exported_file_id=11500000, target_file_id=11500000)


def menu_document(index, own):
    common = document(0, "").replace(binding(0)["exported_guid"].encode(), menu_binding(index)["exported_guid"].encode())
    return common + own


def core_document(*, development=False):
    base = b"{fileID: 0}" if development else asset(42).encode()
    overrides = (b"  - m_useFastValidation\n  - m_applicationVersionDefinition\n  - m_hashedComponentGroupDefaults\n"
                 if development else b"  - m_gameCenter\n  - m_useExportedScenes\n")
    return menu_document(0, b"  m_baseAsset: " + base + b"\n  m_overriddenFields:\n" + overrides +
        b"  m_useExportedScenes: 1\n  m_missionIntrosEnabled: 0\n  m_useFastValidation: 1\n"
        b"  m_gameCenter: 0\n  m_replayMode: -951563343\n  m_allowUnityShaderDebuggingWindow: 0\n"
        b"  m_sendCrashReports: 0\n  m_runtimeIssueProfile: {fileID: 0}\n  m_deviceConsoleEnabled: 0\n"
        b"  m_logHandlerEnabled: 0\n  m_calculateFPSCounter: 0\n  m_tapToSkipEnabled: 0\n"
        b"  m_showGameCenterChallenges: 1\n  m_challengeManagerIntegrationType: 0\n"
        b"  m_hashedComponentGroupDefaults: " + (asset(99).encode() if development else b"{fileID: 0}") + b"\n")


def localisation_document():
    return menu_document(1, b"  m_baseAsset: {fileID: 0}\n  m_overriddenFields:\n"
        b"  - m_localisedDefinitionsDirectory\n  - m_nodeDataExportPath\n"
        b"  m_localisedDefinitionsDirectory: ExampleStrings\n"
        b"  m_nodeDataExportPath: ../../synthetic/export/full_data_export.js\n"
        b"  m_charactersOutputDirectory: /Example Data/Characters/\n"
        b"  m_enableEditorDataGeneration: 1\n  m_enableLocalisedUIStringFunctionality: 0\n")


class MenuConfigurationDocumentTests(unittest.TestCase):
    def assert_pointer_only(self, content, index):
        row = menu_binding(index); before = copy.deepcopy(row)
        result, count = rewrite_configuration_pointers(content, [row])
        source = ("  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}").encode()
        target = ("  m_Script: {fileID: 11500000, guid: " + row["target_guid"] + ", type: 3}").encode()
        self.assertEqual(1, content.count(source)); self.assertEqual((content.replace(source, target), 1), (result, count))
        self.assertEqual(before, row)
        return result

    def test_core_prod_and_dev_keep_differing_raw_graphs(self):
        for development in (False, True):
            with self.subTest(development=development):
                content = core_document(development=development)
                result = self.assert_pointer_only(content, 0)
                self.assertEqual(content.split(b"  m_baseAsset:")[1], result.split(b"  m_baseAsset:")[1])
                if development:
                    self.assertIn(b"  - m_applicationVersionDefinition\n", result)
                    self.assertNotIn(b"  m_applicationVersionDefinition:", result)

    def test_localisation_paths_flags_and_override_order_are_literal(self):
        self.assert_pointer_only(localisation_document(), 1)

    def test_hashed_is_common_only_without_configuration_or_guid_fields(self):
        self.assert_pointer_only(menu_document(2, b""), 2)
        for field in (b"  m_baseAsset: {fileID: 0}\n", b"  m_overriddenFields: []\n", b"  m_guid: synthetic\n"):
            with self.subTest(field=field), self.assertRaises(ValueError):
                rewrite_configuration_pointers(menu_document(2, field), [menu_binding(2)])

    def test_new_profiles_preserve_crlf_and_missing_final_newline(self):
        for index, content in [(0, core_document(development=True)), (1, localisation_document()), (2, menu_document(2, b""))]:
            for variant in (content.replace(b"\n", b"\r\n"), content.rstrip(b"\n")):
                with self.subTest(index=index), self.subTest(variant=variant): self.assert_pointer_only(variant, index)

    def test_core_boolean_and_enum_payloads_are_bounded_without_clamping(self):
        for value in (b"-2147483648", b"2147483647", b"1122295862"):
            self.assert_pointer_only(core_document().replace(b"m_replayMode: -951563343", b"m_replayMode: " + value), 0)
        cases = [core_document().replace(b"m_gameCenter: 0", b"m_gameCenter: " + v) for v in (b"true", b"2", b"-1", b"0x1")]
        cases += [core_document().replace(b"m_replayMode: -951563343", b"m_replayMode: " + v) for v in (b"2147483648", b"-2147483649", b"+1", b"1.0", b"0x42", b"01")]
        for content in cases:
            with self.subTest(content=content), self.assertRaises(ValueError): rewrite_configuration_pointers(content, [menu_binding(0)])

    def test_core_pointer_payloads_remain_data_with_exact_asset_shape(self):
        self.assert_pointer_only(core_document().replace(b"m_runtimeIssueProfile: {fileID: 0}", b"m_runtimeIssueProfile: " + asset(123).encode()), 0)
        for pointer in (b"{fileID: 1}", b"{fileID: 11400000, guid: " + b"0"*32 + b", type: 2}", b"{fileID: 11400000, guid: " + b"a"*32 + b", type: 3}"):
            with self.subTest(pointer=pointer), self.assertRaises(ValueError):
                rewrite_configuration_pointers(core_document().replace(b"m_runtimeIssueProfile: {fileID: 0}", b"m_runtimeIssueProfile: " + pointer), [menu_binding(0)])

    def test_localisation_rejects_yaml_path_extensions_and_bool_aliases(self):
        for path in (b"*alias", b"&anchor path", b"!tag path", b'"quoted"', b"path # comment", b"|", b"path: value", b"[path]"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                rewrite_configuration_pointers(localisation_document().replace(b"ExampleStrings", path), [menu_binding(1)])
        with self.assertRaises(ValueError):
            rewrite_configuration_pointers(localisation_document().replace(b"m_enableEditorDataGeneration: 1", b"m_enableEditorDataGeneration: true"), [menu_binding(1)])

    def test_extra_missing_duplicate_reordered_and_nested_fields_are_rejected(self):
        c = core_document(); l = localisation_document()
        cases = [(0, c+b"  m_extra: 0\n"), (0, c+b"  m_gameCenter: 1\n"), (0, c.replace(b"  m_gameCenter: 0\n", b"")),
                 (0, c.replace(b"  m_gameCenter: 0\n  m_replayMode:", b"  m_replayMode: 0\n  m_gameCenter:")),
                 (0, c.replace(b"  m_gameCenter: 0", b"  m_gameCenter:\n    nested: 0")),
                 (1, l.replace(b"  m_enableEditorDataGeneration: 1", b"  m_enableEditorDataGeneration:\n  - 1")),
                 (1, l+b"  m_overriddenFields: []\n")]
        for index, content in cases:
            with self.subTest(index=index, content=content), self.assertRaises(ValueError): rewrite_configuration_pointers(content, [menu_binding(index)])

    def test_new_bindings_do_not_affect_existing_runtime_twelve_slots(self):
        content = runtime_document(); rows = [menu_binding(i) for i in range(3)]
        self.assertEqual((content, 0), rewrite_configuration_pointers(content, rows))
        expected, count = rewrite_configuration_pointers(content, [binding(1)])
        self.assertEqual((expected, count), rewrite_configuration_pointers(content, rows+[binding(1)]))

    def test_dispatch_routes_new_configuration_only_and_keeps_scalar_body(self):
        from lucidlib.scriptbindings import rewrite_prepared_script_pointers
        for index, content in [(0, core_document()), (1, localisation_document()), (2, menu_document(2, b""))]:
            rows = [menu_binding(i) for i in range(3)]
            expected = self.assert_pointer_only(content, index)
            self.assertEqual((expected, 1), rewrite_prepared_script_pointers(content, rows))
        raw = document(0, "  ActiveConfig: {fileID: 0}\n")
        self.assertEqual((raw, 0), rewrite_prepared_script_pointers(raw, [menu_binding(i) for i in range(3)]))

    def test_failure_preserves_bytes_binding_rows_and_complete_stale_list(self):
        content = core_document(development=True)+b"  m_invented: 0\n"; rows = [menu_binding(i) for i in range(3)]; before = copy.deepcopy(rows)
        with self.assertRaises(ValueError): rewrite_configuration_pointers(content, rows)
        self.assertEqual(before, rows); self.assertIn(b"  - m_applicationVersionDefinition\n", content)


if __name__ == "__main__": unittest.main()

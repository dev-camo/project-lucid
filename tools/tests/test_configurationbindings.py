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


if __name__ == "__main__": unittest.main()

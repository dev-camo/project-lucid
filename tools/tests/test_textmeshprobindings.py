"""Whole-byte TextMeshPro Resources pointer tests with synthetic identities.

The documents mirror the exported `Default Style Sheet` and `EmojiOne` shapes
(abridged payload); the real export is not available to this suite.
"""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.textmeshprobindings import rewrite_textmeshpro_pointers


def binding(index):
    name = ("TMPro.TMP_StyleSheet", "TMPro.TMP_SpriteAsset")[index]
    return dict(assembly="Unity.TextMeshPro", full_name=name, exported_guid=str(index + 1) * 32,
                target_guid=str(index + 4) * 32, exported_file_id=11500000, target_file_id=11500000)


def document(index, name, own, newline="\n"):
    text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            "  m_Script: {fileID: 11500000, guid: " + binding(index)["exported_guid"] + ", type: 3}\n"
            "  m_Name: " + name + "\n  m_EditorClassIdentifier:\n" + own)
    return text.replace("\n", newline).encode()


STYLE_SHEET = ("  m_StyleList:\n  - m_Name: Normal\n    m_HashCode: -1183493901\n    m_OpeningDefinition:\n"
               "    m_ClosingDefinition:\n    m_OpeningTagArray:\n    m_ClosingTagArray:\n"
               "  - m_Name: H1\n    m_HashCode: 2425\n    m_OpeningDefinition: '<size=2em><b><#40ff80>*'\n"
               "    m_ClosingDefinition: '*</size></b></color>'\n    m_OpeningTagArray: 3c000000\n"
               "    m_ClosingTagArray: 2a000000\n")
SPRITE_ASSET = ("  m_Version: 1.1.0\n  m_FaceInfo:\n    m_FaceIndex: 0\n    m_FamilyName:\n    m_PointSize: 0\n"
                "  m_Material: {fileID: 2100000, guid: " + "a" * 32 + ", type: 2}\n"
                "  spriteSheet: {fileID: 2800000, guid: " + "b" * 32 + ", type: 3}\n"
                "  m_SpriteCharacterTable:\n  - m_ElementType: 2\n    m_Unicode: 128514\n    m_GlyphIndex: 0\n"
                "    m_Scale: 1\n    m_Name: 1f602\n  m_GlyphTable:\n  - m_Index: 0\n    m_Metrics:\n"
                "      m_Width: 128\n    m_GlyphRect:\n      m_X: 0\n    m_Scale: 1\n    m_AtlasIndex: 0\n"
                "    sprite: {fileID: 0}\n  spriteInfoList:\n  - id: 0\n    x: 0\n    name: 1f602\n"
                "    sprite: {fileID: 0}\n  fallbackSpriteAssets: []\n")


def style_sheet(newline="\n"):
    return document(0, "Default Style Sheet", STYLE_SHEET, newline)


def sprite_asset():
    return document(1, "EmojiOne", SPRITE_ASSET)


class TextMeshProDocumentTests(unittest.TestCase):
    def assert_pointer_only(self, content, index):
        row = binding(index)
        actual, count = rewrite_textmeshpro_pointers(content, [binding(0), binding(1)])
        old = ("  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}").encode()
        new = ("  m_Script: {fileID: 11500000, guid: " + row["target_guid"] + ", type: 3}").encode()
        self.assertEqual(1, count)
        self.assertEqual(content.replace(old, new), actual)
        self.assertEqual((actual, 0), rewrite_textmeshpro_pointers(actual, [binding(0), binding(1)]))

    def test_observed_style_sheet_and_sprite_asset_change_only_their_script_line(self):
        self.assert_pointer_only(style_sheet(), 0)
        self.assert_pointer_only(sprite_asset(), 1)
        self.assert_pointer_only(style_sheet("\r\n"), 0)

    def test_unselected_content_is_returned_unchanged(self):
        content = style_sheet().replace(b"1" * 32, b"c" * 32)
        self.assertEqual((content, 0), rewrite_textmeshpro_pointers(content, [binding(0), binding(1)]))

    def test_payload_drift_and_envelope_changes_are_refused(self):
        base = style_sheet()
        cases = {
            "extra top-level field": base + b"  m_Extra: 1\n",
            "reordered profile": sprite_asset().replace(b"  m_Version: 1.1.0\n", b"").replace(
                b"  fallbackSpriteAssets: []\n", b"  fallbackSpriteAssets: []\n  m_Version: 1.1.0\n"),
            "missing profile field": sprite_asset().replace(b"  fallbackSpriteAssets: []\n", b""),
            "other owner's profile": style_sheet().replace(b"1" * 32, b"2" * 32),
            "multiline top-level scalar": sprite_asset().replace(b"  m_Version: 1.1.0\n", b"  m_Version: '1.1\n    .0'\n"),
            "nested line without a block": base.replace(b"  m_EditorClassIdentifier:\n", b"  m_EditorClassIdentifier: x\n    y: 1\n"),
            "unindented payload line": base + b"m_Stray: 1\n",
            "tab-indented payload": base + b"\tm_Name: Tab\n",
            "selected GUID in payload": base + b"    m_OpeningTagArray: " + b"1" * 32 + b"\n",
            "second document": base + b"--- !u!114 &11400001\nMonoBehaviour:\n  m_Name: other\n",
            "stripped header": base.replace(b"&11400000", b"&11400000 stripped"),
            "component, not asset": base.replace(b"  m_GameObject: {fileID: 0}", b"  m_GameObject: {fileID: 5}"),
            "hide flags": base.replace(b"  m_ObjectHideFlags: 0", b"  m_ObjectHideFlags: 1"),
            "disabled": base.replace(b"  m_Enabled: 1", b"  m_Enabled: 0"),
            "class identifier": base.replace(b"  m_EditorClassIdentifier:\n", b"  m_EditorClassIdentifier: TMPro\n"),
            "quoted name": base.replace(b"Default Style Sheet", b"'Default'"),
            "wrong script file ID": base.replace(b"fileID: 11500000", b"fileID: 11500001"),
            "missing prefix": base.split(b"\n", 2)[2],
            "control character": base.replace(b"Normal", b"Nor\x0bmal"),
            "duplicate field": base.replace(b"  m_EditorClassIdentifier:\n", b"  m_EditorClassIdentifier:\n  m_Name: Again\n"),
        }
        for label, content in cases.items():
            with self.subTest(case=label), self.assertRaises(ValueError):
                rewrite_textmeshpro_pointers(content, [binding(0), binding(1)])

    def test_invalid_or_duplicate_bindings_are_refused(self):
        rows = [
            [dict(binding(0), full_name="TMPro.TMP_Settings")],
            [dict(binding(0), assembly="Unity.TextMeshPro.Editor")],
            [binding(0), dict(binding(1), exported_guid=binding(0)["exported_guid"])],
            [binding(0), dict(binding(1), target_guid=binding(0)["target_guid"])],
            [dict(binding(0), exported_file_id=11400000)],
            [dict(binding(0), target_file_id=0)],
            [dict(binding(0), target_guid="0" * 32)],
        ]
        for case in rows:
            with self.subTest(rows=case), self.assertRaises(ValueError):
                rewrite_textmeshpro_pointers(style_sheet(), case)


if __name__ == "__main__":
    unittest.main()

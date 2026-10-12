"""Input-free contracts for the explicit three-field font-reference conversion."""
import copy
import hashlib
from pathlib import Path
import re
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import fontreferences as fonts


def owner_fixture(count):
    """Test-owned bytes in the two supported block shapes; no owner authorization."""
    keys = [format(index + 1, "032x") for index in range(count)]
    prefix = b"%YAML 1.1\nOwner:\n  unrelated: caf\xc3\xa9\n"
    tail = b"  untouched: keep this value\n"
    original = [prefix]
    converted = [prefix]
    for index, key in enumerate(keys):
        if count == 6:
            heading = ("  - Name: Owned" + str(index) + "\n    m_language: " + str(index) +
                       "\n    m_fontsForLanguage:\n").encode("ascii")
            indent = "    "
        else:
            heading = b"  m_fontAssetsAndFallBackCollectionRefs:\n" if index == 0 else b""
            indent = "  "
        fragment = (indent + "- m_AssetGUID: " + key + "\n" + indent +
                    "  m_SubObjectName:\n" + indent + "  m_SubObjectType:\n").encode("ascii")
        original.extend((heading, fragment))
        converted.extend((heading, fragment, (indent + "  m_EditorAssetChanged: 0\n").encode("ascii")))
    return b"".join(original) + tail, b"".join(converted) + tail, keys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def keys_in(raw):
    return [key.decode("ascii") for key in re.findall(rb"m_AssetGUID: ([a-f0-9]{32})", raw)]


class FontReferenceConversionTests(unittest.TestCase):
    def test_whole_bytes_key_order_and_inverse_survive_both_block_shapes(self):
        for count in (4, 6):
            for newline in (b"\n", b"\r\n"):
                with self.subTest(count=count, newline=newline):
                    raw, expected, keys = owner_fixture(count)
                    raw = raw.replace(b"\n", newline)
                    expected = expected.replace(b"\n", newline)
                    before = copy.deepcopy((raw, keys))
                    receipt = fonts.convert_pinned_owner(raw, sha(raw), keys)
                    self.assertEqual(expected, receipt["converted"])
                    self.assertEqual(raw, fonts.reverse_pinned_owner(receipt))
                    self.assertEqual(before, (raw, keys))
                    self.assertEqual(keys, keys_in(receipt["converted"]))
                    self.assertEqual(count, receipt["converted"].count(b"m_EditorAssetChanged: 0"))
                    self.assertEqual(sha(raw), receipt["original_sha256"])
                    self.assertEqual(sha(expected), receipt["converted_sha256"])
                    self.assertEqual(count, len(receipt["fragments"]))
                    for fragment in receipt["fragments"]:
                        self.assertEqual(fragment["original"] + fragment["addition"], fragment["converted"])
                        self.assertEqual(fragment["original"], fonts.reverse_reference_fragment(fragment))
                    self.assertFalse(receipt["script_references_modified"])
                    self.assertFalse(receipt["player_serialized_profile_verified"])
                    self.assertFalse(receipt["runtime_verified"])

    def test_unknown_fragment_payload_is_rejected_without_rewriting_it(self):
        key = "00000000000000000000000000000001"
        raw = ("    - m_AssetGUID: " + key + "\n      m_SubObjectName:\n      m_SubObjectType:\n").encode()
        mutations = (raw.replace(b"m_AssetGUID", b"m_assetGUID"), raw + b"      unknown: 1\n",
                     raw.replace(b"m_SubObjectName:\n", b"m_SubObjectName: named\n"),
                     raw.replace(b"m_SubObjectType:\n", b"m_SubObjectType: type\n"),
                     raw.replace(key.encode(), b"A" * 32),
                     raw.replace(b"m_SubObjectName:", b"m_SubObjectName: # comment"))
        for changed in mutations:
            with self.subTest(changed=changed):
                before = bytes(changed)
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_reference_fragment(changed, key)
                self.assertEqual(before, changed)

    def test_crlf_indentation_and_blank_fragment_fields_survive_exactly(self):
        key = "00000000000000000000000000000001"
        for indent in ("", "  ", "      "):
            with self.subTest(indent=indent):
                raw = (indent + "- m_AssetGUID: " + key + "\r\n" + indent + "  m_SubObjectName:\r\n" +
                       indent + "  m_SubObjectType:\r\n").encode()
                receipt = fonts.convert_reference_fragment(raw, key)
                self.assertEqual(raw + (indent + "  m_EditorAssetChanged: 0\r\n").encode(), receipt["converted"])
                self.assertEqual(raw, fonts.reverse_reference_fragment(receipt))
                changed = copy.deepcopy(receipt)
                changed["converted"] += b"\n"
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.reverse_reference_fragment(changed)

    def test_hash_key_order_repeat_conversion_and_receipt_tampering_fail_closed(self):
        for count in (4, 6):
            with self.subTest(count=count):
                raw, _, keys = owner_fixture(count)
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_pinned_owner(raw, "0" * 64, keys)
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_pinned_owner(raw, sha(raw), list(reversed(keys)))
                receipt = fonts.convert_pinned_owner(raw, sha(raw), keys)
                original_receipt = copy.deepcopy(receipt)
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_pinned_owner(receipt["converted"], receipt["converted_sha256"], keys)
                for field in ("converted", "converted_sha256", "fragments", "runtime_verified"):
                    with self.subTest(field=field):
                        changed = copy.deepcopy(receipt)
                        if field == "converted": changed[field] += b"\n"
                        elif field == "converted_sha256": changed[field] = "0" * 64
                        elif field == "fragments": changed[field][0]["converted"] += b"\n"
                        else: changed[field] = True
                        with self.assertRaises(fonts.FontReferenceError):
                            fonts.reverse_pinned_owner(changed)
                self.assertEqual(original_receipt, receipt)

    def test_extra_reference_field_is_rejected_in_both_shapes_at_every_item(self):
        for count in (4, 6):
            raw, _, keys = owner_fixture(count)
            for index, terminal in enumerate(re.finditer(rb"(?m)^( *)m_SubObjectType:\n", raw)):
                with self.subTest(count=count, item=index):
                    changed = raw[:terminal.end()] + terminal[1] + b"unknown: 1\n" + raw[terminal.end():]
                    with self.assertRaises(fonts.FontReferenceError):
                        fonts.convert_pinned_owner(changed, sha(changed), keys)

    def test_blank_line_cannot_hide_escaped_tagged_commented_or_nested_payload(self):
        for count in (4, 6):
            raw, _, keys = owner_fixture(count)
            terminal = next(re.finditer(rb"(?m)^( *)m_SubObjectType:\n", raw))
            for suffix in (b"unknown: 1\n", b'"unknown": 1\n', b"unknown: !owned-tag value\n",
                           b"# unsupported reference comment\n", b"unknown:\n        nested: 1\n"):
                with self.subTest(count=count, suffix=suffix):
                    changed = raw[:terminal.end()] + b"\n" + terminal[1] + suffix + raw[terminal.end():]
                    with self.assertRaises(fonts.FontReferenceError):
                        fonts.convert_pinned_owner(changed, sha(changed), keys)

    def test_crlf_extra_payload_and_tab_indentation_are_rejected(self):
        raw, _, keys = owner_fixture(4)
        for prefix, newline in ((b"    ", b"\r\n"), (b"\t", b"\n"), (b"    \t", b"\n")):
            with self.subTest(prefix=prefix, newline=newline):
                changed_newlines = raw.replace(b"\n", newline)
                terminal = next(re.finditer(rb"(?m)^ *m_SubObjectType:\r?\n", changed_newlines))
                changed = (changed_newlines[:terminal.end()] + prefix + b"unknown: 1" + newline +
                           changed_newlines[terminal.end():])
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_pinned_owner(changed, sha(changed), keys)


    def test_argument_profiles_and_mixed_newlines_are_rejected(self):
        raw, _, keys = owner_fixture(4)
        fragment = ("  - m_AssetGUID: " + keys[0] + "\n    m_SubObjectName:\n    m_SubObjectType:\n").encode()
        for changed, key in ((fragment.decode(), keys[0]), (bytearray(fragment), keys[0]),
                             (fragment, keys[0].encode()), (fragment, "A" * 32),
                             (fragment.replace(b"m_SubObjectName:\n", b"m_SubObjectName:\r\n"), keys[0])):
            with self.subTest(fragment=changed, key=key):
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_reference_fragment(changed, key)
        for changed, source_hash, order in ((raw.decode(), sha(raw), keys),
                                           (bytearray(raw), sha(raw), keys),
                                           (raw, sha(raw).encode(), keys),
                                           (raw, sha(raw).upper(), keys),
                                           (raw, sha(raw), tuple(keys)),
                                           (raw, sha(raw), keys[:3]),
                                           (raw, sha(raw), [1] + keys[1:]),
                                           (raw, sha(raw), ["A" * 32] + keys[1:])):
            with self.subTest(source=changed, source_hash=source_hash, key_order=order):
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.convert_pinned_owner(changed, source_hash, order)

    def test_editor_single_space_empty_scalars_are_a_distinct_unsupported_profile(self):
        for count in (4, 6):
            raw, _, keys = owner_fixture(count)
            for newline in (b"\n", b"\r\n"):
                original = raw.replace(b"\n", newline)
                spaced = original.replace(b"m_SubObjectName:" + newline, b"m_SubObjectName: " + newline)
                spaced = spaced.replace(b"m_SubObjectType:" + newline, b"m_SubObjectType: " + newline)
                with self.subTest(count=count, newline=newline):
                    before = bytes(spaced)
                    with self.assertRaises(fonts.FontReferenceError):
                        fonts.convert_pinned_owner(spaced, sha(spaced), keys)
                    fragment = ("  - m_AssetGUID: " + keys[0] +
                                "\n    m_SubObjectName: \n    m_SubObjectType: \n").encode().replace(b"\n", newline)
                    with self.assertRaises(fonts.FontReferenceError):
                        fonts.convert_reference_fragment(fragment, keys[0])
                    self.assertEqual(before, spaced)
                    self.assertEqual(original, fonts.reverse_pinned_owner(fonts.convert_pinned_owner(original, sha(original), keys)))

    def test_every_receipt_field_missing_mutated_or_extended_is_rejected(self):
        raw, _, keys = owner_fixture(4)
        whole = fonts.convert_pinned_owner(raw, sha(raw), keys)
        fragment = whole["fragments"][0]
        for receipt, reverse in ((whole, fonts.reverse_pinned_owner), (fragment, fonts.reverse_reference_fragment)):
            before = copy.deepcopy(receipt)
            for field, value in receipt.items():
                for operation in ("missing", "mutated"):
                    changed = copy.deepcopy(receipt)
                    if operation == "missing": del changed[field]
                    elif type(value) is bool: changed[field] = not value
                    elif type(value) is bytes: changed[field] += b"\n"
                    elif type(value) is str: changed[field] = "tampered"
                    else: changed[field] = list(reversed(value))
                    with self.subTest(reverse=reverse.__name__, field=field, operation=operation):
                        with self.assertRaises(fonts.FontReferenceError):
                            reverse(changed)
            extended = copy.deepcopy(receipt)
            extended["unsupported_receipt_field"] = True
            with self.assertRaises(fonts.FontReferenceError):
                reverse(extended)
            self.assertEqual(before, receipt)
        for field, value in fragment.items():
            changed = copy.deepcopy(whole)
            if type(value) is bool: changed["fragments"][0][field] = not value
            elif type(value) is bytes: changed["fragments"][0][field] += b"\n"
            else: changed["fragments"][0][field] = "tampered"
            with self.subTest(nested_fragment_field=field):
                with self.assertRaises(fonts.FontReferenceError):
                    fonts.reverse_pinned_owner(changed)
        self.assertEqual(raw, fonts.reverse_pinned_owner(whole))



if __name__ == "__main__":
    unittest.main()

"""Explicit evidence-only conversion for the observed original font references.

The caller retains the whole immutable source asset and a conversion receipt.
These pure functions do not bind scripts, write assets, or load Addressables.
"""

import hashlib
import re

class FontReferenceError(ValueError):
    """The exact observed reference/type/path profile is not established."""


def convert_reference_fragment(fragment, key):
    """Add the observed false Editor default without rewriting three raw fields.

    A fragment is exactly one original list item with a GUID and two blank
    subobject fields. Other field names, values, comments, escaping, or profiles
    remain unsupported. Full asset/context/profile approval belongs to caller.
    """
    if type(fragment) is not bytes or type(key) is not str or re.fullmatch(r"[a-f0-9]{32}", key) is None:
        raise FontReferenceError("Invalid original reference fragment")
    expression = rb"( *)- m_AssetGUID: " + key.encode("ascii") + rb"(\r?\n)\1  m_SubObjectName:\2\1  m_SubObjectType:\2"
    match = re.fullmatch(expression, fragment)
    if match is None:
        raise FontReferenceError("Unsupported original font reference payload profile")
    addition = match[1] + b"  m_EditorAssetChanged: 0" + match[2]
    converted = fragment + addition
    return {"original": fragment, "converted": converted, "addition": addition,
            "original_sha256": hashlib.sha256(fragment).hexdigest(),
            "converted_sha256": hashlib.sha256(converted).hexdigest(),
            "original_key": key, "field_rename": False, "editor_only_field_default": False,
            "player_serialized_profile_verified": False, "runtime_verified": False}


def reverse_reference_fragment(receipt):
    """Verify the complete forward receipt and recover exact original bytes."""
    expected = convert_reference_fragment(receipt.get("original"), receipt.get("original_key"))
    if receipt != expected:
        raise FontReferenceError("Reference conversion receipt differs from its exact forward result")
    return receipt["original"]


def convert_pinned_owner(original, source_sha256, key_order):
    """Convert an already qualified whole owner copy and retain a full inverse.

    A caller must establish the original MonoScript/field/type scope and typed
    aliases before calling. This function verifies the exact acquired source
    hash, all three-field reference fragments and original key order; its result
    still grants no script binding or Engine/player/runtime qualification.
    """
    if (type(original) is not bytes or type(source_sha256) is not str or
            re.fullmatch(r"[a-f0-9]{64}", source_sha256) is None or
            hashlib.sha256(original).hexdigest() != source_sha256 or type(key_order) is not list or
            len(key_order) not in (4, 6) or any(type(key) is not str or re.fullmatch(r"[a-f0-9]{32}", key) is None for key in key_order)):
        raise FontReferenceError("Unqualified original font owner source identity or key order")
    pattern = rb"(?m)^( *)- m_AssetGUID: ([a-f0-9]{32})(\r?\n)\1  m_SubObjectName:\3\1  m_SubObjectType:\3"
    matches = list(re.finditer(pattern, original))
    if (len(matches) != len(key_order) or [match[2].decode("ascii") for match in matches] != key_order or
            len(re.findall(rb"m_AssetGUID:", original)) != len(matches) or b"m_EditorAssetChanged:" in original):
        raise FontReferenceError("Whole font owner reference payload or ordered keys differs")
    # The exact three-line match must cover the complete reference item, not
    # a valid prefix followed by an unsupported fourth field or nested payload.
    # Blank lines preserve source bytes; the next nonblank line must have left
    # the reference item's child indentation. This is an observed block-profile
    # boundary, not a general YAML decoder or owner/type authorization.
    for match in matches:
        following = next((line for line in original[match.end():].splitlines() if line.strip()), b"")
        indentation = re.match(rb"([ \t]*)", following)[1]
        if b"\t" in indentation or len(indentation) > len(match[1]):
            raise FontReferenceError("Original font reference has unsupported continuation payload")
    fragments = [convert_reference_fragment(match[0], key) for match, key in zip(matches, key_order)]
    pieces, offset = [], 0
    for match, fragment in zip(matches, fragments):
        pieces.extend((original[offset:match.start()], fragment["converted"]))
        offset = match.end()
    pieces.append(original[offset:])
    converted = b"".join(pieces)
    return {"original": original, "converted": converted, "original_sha256": source_sha256,
            "converted_sha256": hashlib.sha256(converted).hexdigest(), "key_order": list(key_order),
            "fragments": fragments, "script_references_modified": False, "runtime_verified": False,
            "player_serialized_profile_verified": False}


def reverse_pinned_owner(receipt):
    expected = convert_pinned_owner(receipt.get("original"), receipt.get("original_sha256"), receipt.get("key_order"))
    if receipt != expected:
        raise FontReferenceError("Whole owner conversion differs from its complete forward receipt")
    return receipt["original"]

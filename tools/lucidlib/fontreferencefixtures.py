"""Synthetic test-owned inputs for genuine import of Python-converted YAML.

These bytes host the maintained test carrier. They are not recovered game
assets, script-binding authorization, or evidence of live font loading.
"""

import base64
import hashlib
import re

from . import fontreferences


def build_import_fixture(carrier_script_guid):
    """Return deterministic LF/CRLF owner bytes produced by the actual converter.

    The caller obtains the carrier's maintained MonoScript GUID and writes this
    test context before its Editor invocation. The Editor checks that identity
    against a normally created carrier, then imports these exact source bytes.
    """
    if type(carrier_script_guid) is not str or re.fullmatch(r"[a-f0-9]{32}", carrier_script_guid) is None:
        raise ValueError("Invalid test carrier MonoScript GUID")
    keys = [format(index + 1, "032x") for index in range(4)]
    header = (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: " + carrier_script_guid + ", type: 3}\n"
        "  m_Name: PythonConverterOwned\n  m_EditorClassIdentifier: \n"
        "  m_fontAssetsAndFallBackCollectionRefs:\n"
    ).encode("ascii")
    rows = b"".join(("  - m_AssetGUID: " + key +
                     "\n    m_SubObjectName:\n    m_SubObjectType:\n").encode("ascii") for key in keys)
    variants = []
    for name, newline in (("LF", b"\n"), ("CRLF", b"\r\n")):
        original = (header + rows).replace(b"\n", newline)
        receipt = fontreferences.convert_pinned_owner(original, hashlib.sha256(original).hexdigest(), keys)
        if fontreferences.reverse_pinned_owner(receipt) != original:
            raise ValueError("Test fixture conversion does not have its exact inverse")
        variants.append({
            "name": name,
            "original_base64": base64.b64encode(receipt["original"]).decode("ascii"),
            "converted_base64": base64.b64encode(receipt["converted"]).decode("ascii"),
            "original_sha256": receipt["original_sha256"],
            "converted_sha256": receipt["converted_sha256"],
            "fragment_count": len(receipt["fragments"]),
            "script_references_modified": receipt["script_references_modified"],
            "player_serialized_profile_verified": receipt["player_serialized_profile_verified"],
            "runtime_verified": receipt["runtime_verified"],
        })
    return {"schema_version": 1, "generated_by": "ProjectLucid.fontreferences.import-fixture",
            "carrier_script_guid": carrier_script_guid, "keys": keys, "variants": variants}

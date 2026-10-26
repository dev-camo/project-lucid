"""Bounded scene and prefab component support; callers must qualify bindings first.

A selected component document sits in a multi-document scene or prefab beside
unrelated objects, and its payload has nested blocks (a serializable class, a
string list, a UnityEvent). Only its m_Script line changes. Its top-level field names and order must
equal the shape observed in this release, and every payload line must lie inside
a block that one of those fields opens. Unselected documents are not read.
"""

import math
import re
import struct

from .configurationbindings import COMMON, GUID, SCRIPT


PROFILES = {
    ("Game.Runtime", "HardlightProject.VisualAssetManager"): (
        "m_requirementForHDUI", "m_atlasesWithVariants", "m_devicePerformanceGroup", "m_sdAtlasSuffix"),
    # The highlight component of the ten uihighlight_* prefabs.
    ("HLModernUI.Runtime", "Hardlight.UIHighlight"): ("m_definition", "m_onShowHighlight"),
    ("UnityEngine.UI", "UnityEngine.UI.Image"): (
        "m_Material", "m_Color", "m_RaycastTarget", "m_RaycastPadding", "m_Maskable",
        "m_OnCullStateChanged", "m_Sprite", "m_Type", "m_PreserveAspect", "m_FillCenter",
        "m_FillMethod", "m_FillAmount", "m_FillClockwise", "m_FillOrigin", "m_UseSpriteMesh",
        "m_PixelsPerUnitMultiplier"),
}
# The profile fields whose payload is a nested block: a serializable class
# (PerformanceAttribute), a string array and a UnityEvent's persistent calls.
# No other field may open a block.
NESTED = {
    ("Game.Runtime", "HardlightProject.VisualAssetManager"): ("m_requirementForHDUI", "m_atlasesWithVariants"),
    ("HLModernUI.Runtime", "Hardlight.UIHighlight"): ("m_onShowHighlight",),
    ("UnityEngine.UI", "UnityEngine.UI.Image"): ("m_OnCullStateChanged",),
}
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?\n(\w+):\r?\n", re.MULTILINE)
FIELD = re.compile(r"  ([A-Za-z_][A-Za-z_0-9]*):(?: (.*))?")
PREFIXES = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n",
            "%YAML 1.1\r\n%TAG !u! tag:unity3d.com,2011:\r\n")


def _image_float(value):
    if re.fullmatch(r"-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", value) is None:
        raise ValueError("Unsupported Image float syntax")
    try:
        number = float(value)
        if not math.isfinite(number) or not math.isfinite(struct.unpack("<f", struct.pack("<f", number))[0]):
            raise ValueError("Nonfinite Image float32")
    except (OverflowError, struct.error) as error:
        raise ValueError("Unsupported Image float32") from error


def _image_payload(body, values):
    # Original CullStateChangedEvent has an empty persistent-call list in this
    # authored domain. Indentation alone does not establish its event payload.
    event = "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
    if event not in body.replace("\r\n", "\n"):
        raise ValueError("Unsupported Image persistent event")
    lines = body.splitlines()
    nested = [line for line in lines if line.startswith("    ") or line.startswith("  - ")]
    if nested != ["    m_PersistentCalls:", "      m_Calls: []"]:
        raise ValueError("Unsupported Image nested payload")
    for name in ("m_RaycastTarget", "m_Maskable", "m_PreserveAspect", "m_FillCenter",
                 "m_FillClockwise", "m_UseSpriteMesh"):
        if values[name] not in ("0", "1"):
            raise ValueError("Unsupported Image Boolean")
    for name, maximum in (("m_Type", 3), ("m_FillMethod", 4)):
        if values[name] not in tuple(str(n) for n in range(maximum + 1)):
            raise ValueError("Unsupported Image enum")
    if re.fullmatch(r"-?[0-9]+", values["m_FillOrigin"]) is None or not -(1 << 31) <= int(values["m_FillOrigin"]) < (1 << 31):
        raise ValueError("Unsupported Image Int32")
    for name in ("m_FillAmount", "m_PixelsPerUnitMultiplier"):
        _image_float(values[name])
    for name, components in (("m_Color", ("r", "g", "b", "a")),
                             ("m_RaycastPadding", ("x", "y", "z", "w"))):
        pattern = r"\{" + ", ".join(component + r": ([^,{}]+)" for component in components) + r"\}"
        match = re.fullmatch(pattern, values[name])
        if match is None:
            raise ValueError("Unsupported Image vector/color")
        for value in match.groups():
            _image_float(value)
    for name in ("m_Material", "m_Sprite"):
        if values[name] == "{fileID: 0}":
            continue
        pointer = re.fullmatch(r"\{fileID: (-?[0-9]+), guid: ([0-9a-f]{32}), type: 2\}", values[name])
        if (pointer is None or not -(1 << 63) <= int(pointer[1]) < (1 << 63)
                or int(pointer[1]) == 0 or pointer[2] == "0" * 32):
            raise ValueError("Unsupported Image asset pointer")


def _bindings(rows):
    result, owners, targets = {}, set(), set()
    for row in rows:
        owner = (row.get("assembly"), row.get("full_name")) if isinstance(row, dict) else None
        if owner not in PROFILES:
            raise ValueError("Unsupported component binding owner")
        for key in ("exported_guid", "target_guid"):
            if not isinstance(row.get(key), str) or GUID.fullmatch(row[key]) is None or row[key] == "0" * 32:
                raise ValueError("Invalid component script GUID")
        if type(row.get("exported_file_id")) is not int or row["exported_file_id"] != 11500000:
            raise ValueError("Unsupported exported component script file ID")
        if type(row.get("target_file_id")) is not int or not -(1 << 63) <= row["target_file_id"] < (1 << 63) or row["target_file_id"] == 0:
            raise ValueError("Invalid target component script file ID")
        target = (row["target_guid"], row["target_file_id"])
        if row["exported_guid"] in result or owner in owners or target in targets:
            raise ValueError("Duplicate component script binding")
        owners.add(owner)
        targets.add(target)
        result[row["exported_guid"]] = row
    return result


def _document(body, row):
    fields, values = [], {}
    profile = PROFILES[(row["assembly"], row["full_name"])]
    block_open = False
    for line in body.splitlines():
        match = FIELD.fullmatch(line)
        if match is not None:
            name, value = match[1], match[2] or ""
            if name in values:
                raise ValueError("Duplicate component field")
            fields.append(name)
            values[name] = value
            # Only a nested profile field with no inline value opens a block.
            block_open = not value and name in NESTED[(row["assembly"], row["full_name"])]
        elif block_open and (line.startswith("    ") or line.startswith("  - ")):
            continue
        else:
            raise ValueError("Unsupported component document line")
    if tuple(fields) != COMMON + profile:
        raise ValueError("Unsupported component field set or order")
    for name in ("m_ObjectHideFlags", "m_EditorHideFlags"):
        if values[name] != "0":
            raise ValueError("Unsupported component hide flags")
    for name in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset"):
        if values[name] != "{fileID: 0}":
            raise ValueError("Component is not the qualified unprefabbed scene shape")
    image = (row["assembly"], row["full_name"]) == ("UnityEngine.UI", "UnityEngine.UI.Image")
    owner_pattern = r"\{fileID: (-?[1-9][0-9]*)\}" if image else r"\{fileID: ([1-9][0-9]*)\}"
    owner = re.fullmatch(owner_pattern, values["m_GameObject"])
    if owner is None or not -(1 << 63) <= int(owner[1]) < 1 << 63:
        raise ValueError("Unsupported component owner pointer")
    if values["m_Enabled"] not in ("0", "1"):
        raise ValueError("Unsupported component enabled flag")
    if values["m_Name"] or values["m_EditorClassIdentifier"]:
        raise ValueError("Unsupported component name or class identifier")
    pointer = SCRIPT.fullmatch(values["m_Script"])
    if pointer is None or (pointer[1], pointer[2]) != (str(row["exported_file_id"]), row["exported_guid"]):
        raise ValueError("Unsupported selected component script pointer")
    if (row["assembly"], row["full_name"]) == ("UnityEngine.UI", "UnityEngine.UI.Image"):
        _image_payload(body, values)


def rewrite_component_pointers(content, bindings):
    """Validate each selected component document and replace its script line.

    Pure: no source admission, provider action or file IO. Unsupported shapes are
    rejected before any transformed content is returned.
    """
    if not isinstance(content, bytes):
        raise ValueError("Component content must be bytes")
    mapping = _bindings(bindings)
    if not mapping:
        return content, 0
    try:
        text = content.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError("Unsupported component UTF8") from error
    if (any((ord(character) < 32 and character not in "\r\n") or ord(character) == 127 for character in text)
            or any(character in text for character in ("\x85", " ", " "))
            or "\r" in text.replace("\r\n", "")):
        raise ValueError("Unsupported component control or newline")
    mentions = [match for guid in mapping for match in re.finditer(re.escape(guid), text, re.IGNORECASE)]
    if not mentions:
        return content, 0
    headers = list(HEADER.finditer(text))
    if not headers or text[:headers[0].start()] not in PREFIXES:
        raise ValueError("Unsupported component scene envelope")
    replacements = []
    for index, header in enumerate(headers):
        end = headers[index + 1].start() if index + 1 < len(headers) else len(text)
        body = text[header.end():end]
        if not any(guid in body.lower() for guid in mapping):
            continue
        if header[1] != "114" or header[3] != "MonoBehaviour" or "stripped" in header[0]:
            raise ValueError("Unsupported component document envelope")
        lines = list(re.finditer(r"^  m_Script: *(.*?)(\r?)$", body, re.MULTILINE))
        if len(lines) != 1:
            raise ValueError("Unsupported selected component script line")
        value = lines[0][1]
        pointer = SCRIPT.fullmatch(value)
        if pointer is None or pointer[2] not in mapping:
            raise ValueError("Unsupported selected component script identity")
        row = mapping[pointer[2]]
        if (row["assembly"], row["full_name"]) == ("UnityEngine.UI", "UnityEngine.UI.Image"):
            anchor = header[2]
            if (re.fullmatch(r"-?[0-9]+", anchor) is None or int(anchor) == 0
                    or not -(1 << 63) <= int(anchor) < (1 << 63)):
                raise ValueError("Unsupported Image document local ID")
            if sum(int(other[2]) == int(anchor) for other in headers) != 1:
                raise ValueError("Duplicate Image document local ID")
        _document(body, row)
        start = header.end() + lines[0].start(1)
        replacements.append((start, start + len(value), row))
    if any(not any(start <= match.start() < finish for start, finish, _ in replacements) for match in mentions):
        raise ValueError("Selected component GUID outside its script pointer")
    result = text
    for start, finish, row in reversed(replacements):
        target = "{fileID: " + str(row["target_file_id"]) + ", guid: " + row["target_guid"] + ", type: 3}"
        result = result[:start] + target + result[finish:]
    return result.encode("utf-8"), len(replacements)

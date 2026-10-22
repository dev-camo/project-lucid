"""Bounded TextMeshPro Resources asset support; callers must qualify bindings first.

The selected documents are standalone package ScriptableObjects whose payload is
nested (style lists, sprite and glyph tables). Only the m_Script line changes.
It is the eighth field of the fixed common header, so no authored payload can
precede or imitate it. The payload stays opaque, but its top-level field names
and order must equal the shape observed in this release, and every payload line
must lie inside a block that one of those fields opens.
"""

import re

from .configurationbindings import COMMON, HEADER, SCRIPT, GUID


PROFILES = {
    ("Unity.TextMeshPro", "TMPro.TMP_StyleSheet"): ("m_StyleList",),
    ("Unity.TextMeshPro", "TMPro.TMP_SpriteAsset"): (
        "m_Version", "m_FaceInfo", "m_Material", "spriteSheet", "m_SpriteCharacterTable",
        "m_GlyphTable", "spriteInfoList", "fallbackSpriteAssets"),
}
PREFIXES = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n",
            "%YAML 1.1\r\n%TAG !u! tag:unity3d.com,2011:\r\n")
FIELD = re.compile(r"  ([A-Za-z_][A-Za-z_0-9]*):(?: (.*))?")


def _bindings(rows):
    result, owners, targets = {}, set(), set()
    for row in rows:
        owner = (row.get("assembly"), row.get("full_name")) if isinstance(row, dict) else None
        if owner not in PROFILES:
            raise ValueError("Unsupported TextMeshPro binding owner")
        for key in ("exported_guid", "target_guid"):
            if not isinstance(row.get(key), str) or GUID.fullmatch(row[key]) is None or row[key] == "0" * 32:
                raise ValueError("Invalid TextMeshPro script GUID")
        if type(row.get("exported_file_id")) is not int or row["exported_file_id"] != 11500000:
            raise ValueError("Unsupported exported TextMeshPro script file ID")
        if type(row.get("target_file_id")) is not int or not -(1 << 63) <= row["target_file_id"] < (1 << 63) or row["target_file_id"] == 0:
            raise ValueError("Invalid target TextMeshPro script file ID")
        target = (row["target_guid"], row["target_file_id"])
        if row["exported_guid"] in result or owner in owners or target in targets:
            raise ValueError("Duplicate TextMeshPro script binding")
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
                raise ValueError("Duplicate TextMeshPro field")
            fields.append(name)
            values[name] = value
            # Only a profile field with no inline value opens a nested block;
            # empty header fields and multiline top-level scalars never do.
            block_open = not value and name in profile
        elif block_open and (line.startswith("    ") or line.startswith("  - ")):
            continue
        else:
            raise ValueError("Unsupported TextMeshPro document line")
    if tuple(fields) != COMMON + profile:
        raise ValueError("Unsupported TextMeshPro field set or order")
    for name in COMMON:
        value = values[name]
        if name in ("m_ObjectHideFlags", "m_EditorHideFlags") and value != "0":
            raise ValueError("Unsupported TextMeshPro hide flags")
        if name in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject") and value != "{fileID: 0}":
            raise ValueError("TextMeshPro asset is not the qualified standalone shape")
        if name == "m_Enabled" and value != "1":
            raise ValueError("Unsupported TextMeshPro enabled flag")
        if name == "m_EditorClassIdentifier" and value:
            raise ValueError("Unsupported TextMeshPro class identifier")
        if name == "m_Name" and re.fullmatch(r"[A-Za-z_][A-Za-z_0-9 .-]*", value) is None:
            raise ValueError("Unsupported TextMeshPro plain name")
    pointer = SCRIPT.fullmatch(values["m_Script"])
    if pointer is None or (pointer[1], pointer[2]) != (str(row["exported_file_id"]), row["exported_guid"]):
        raise ValueError("Unsupported selected TextMeshPro script pointer")


def rewrite_textmeshpro_pointers(content, bindings):
    """Validate a qualified standalone TextMeshPro asset and replace its script line.

    Pure: no source admission, provider action or file IO. Unsupported shapes are
    rejected before any transformed content is returned.
    """
    if not isinstance(content, bytes):
        raise ValueError("TextMeshPro content must be bytes")
    mapping = _bindings(bindings)
    if not mapping:
        return content, 0
    try:
        text = content.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError("Unsupported TextMeshPro UTF8") from error
    if (any((ord(character) < 32 and character not in "\r\n") or ord(character) == 127 for character in text)
            or any(character in text for character in ("\x85", " ", " "))
            or "\r" in text.replace("\r\n", "")):
        raise ValueError("Unsupported TextMeshPro control or newline")
    mentions = [match for guid in mapping for match in re.finditer(re.escape(guid), text, re.IGNORECASE)]
    if not mentions:
        return content, 0
    headers = list(HEADER.finditer(text))
    if (len(headers) != 1 or headers[0].groups() != ("114", "11400000", "MonoBehaviour")
            or text[:headers[0].start()] not in PREFIXES):
        raise ValueError("Unsupported standalone TextMeshPro document envelope")
    body = text[headers[0].end():]
    lines = list(re.finditer(r"^  m_Script: *(.*?)(\r?)$", body, re.MULTILINE))
    if len(lines) != 1:
        raise ValueError("Unsupported selected TextMeshPro script line")
    value = lines[0][1]
    pointer = SCRIPT.fullmatch(value)
    if pointer is None or pointer[2] not in mapping:
        raise ValueError("Unsupported selected TextMeshPro script identity")
    row = mapping[pointer[2]]
    _document(body, row)
    start = headers[0].end() + lines[0].start(1)
    finish = start + len(value)
    if any(not start <= match.start() < finish for match in mentions):
        raise ValueError("Selected TextMeshPro GUID outside its script pointer")
    target = "{fileID: " + str(row["target_file_id"]) + ", guid: " + row["target_guid"] + ", type: 3}"
    return (text[:start] + target + text[finish:]).encode("utf-8"), 1

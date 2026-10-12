"""Bounded configuration document support; callers must qualify bindings first."""

import re


PROFILES = {
    ("HLUnityCore.Runtime", "Hardlight.ActiveRuntimeConfiguration"): ("ActiveConfig",),
    ("HLUnityCore.Runtime", "Hardlight.RuntimeConfiguration"): ("m_configurationAssets",),
    ("Game.Runtime", "HardlightProject.GameLevels"): (
        "m_baseAsset", "m_overriddenFields", "m_systemScenes", "m_uiScenes", "m_gameplayEntries"),
}
COMMON = ("m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
          "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script",
          "m_Name", "m_EditorClassIdentifier")
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\r?\n(\w+):\r?\n", re.MULTILINE)
FIELD = re.compile(r"^  ([A-Za-z_][A-Za-z_0-9]*): *(.*)$")
SCRIPT = re.compile(r"\{fileID: (-?[0-9]+), guid: ([0-9a-f]{32}), type: 3\}")
ASSET = re.compile(r"\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}")
GUID = re.compile(r"[0-9a-f]{32}")
IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z_0-9]*")
POINTER_LISTS = {"m_configurationAssets", "m_systemScenes", "m_uiScenes", "m_gameplayEntries"}


def _bindings(rows):
    result = {}
    owners = set()
    targets = set()
    for row in rows:
        if not isinstance(row, dict) or (row.get("assembly"), row.get("full_name")) not in PROFILES:
            raise ValueError("Unsupported configuration binding owner")
        for key in ("exported_guid", "target_guid"):
            if not isinstance(row.get(key), str) or GUID.fullmatch(row[key]) is None or row[key] == "0" * 32:
                raise ValueError("Invalid configuration script GUID")
        if type(row.get("exported_file_id")) is not int or row["exported_file_id"] != 11500000:
            raise ValueError("Unsupported exported configuration script file ID")
        if type(row.get("target_file_id")) is not int or not -(1 << 63) <= row["target_file_id"] < (1 << 63) or row["target_file_id"] == 0:
            raise ValueError("Invalid target configuration script file ID")
        owner = (row["assembly"], row["full_name"])
        target = (row["target_guid"], row["target_file_id"])
        if row["exported_guid"] in result or owner in owners or target in targets:
            raise ValueError("Duplicate configuration script binding")
        owners.add(owner)
        targets.add(target)
        result[row["exported_guid"]] = row
    return result


def _asset(value):
    if value == "{fileID: 0}":
        return
    match = ASSET.fullmatch(value)
    if match is None or match[1] == "0" * 32:
        raise ValueError("Unsupported configuration asset pointer")


def _document(body, row):
    lines = body.splitlines()
    fields = []
    values = {}
    position = 0
    while position < len(lines):
        match = FIELD.fullmatch(lines[position])
        if match is None or match[1] in values:
            raise ValueError("Unsupported or duplicate configuration field")
        name, value = match.groups()
        fields.append(name)
        position += 1
        items = []
        while position < len(lines) and lines[position].startswith("  - "):
            items.append(lines[position][4:])
            position += 1
        if items and value:
            raise ValueError("Configuration sequence has both inline and block values")
        values[name] = (value, items)
    expected = COMMON + PROFILES[(row["assembly"], row["full_name"])]
    if tuple(fields) != expected:
        raise ValueError("Unsupported configuration field set or order")
    for name in COMMON:
        value, items = values[name]
        if items:
            raise ValueError("Unsupported configuration common field sequence")
        if name in ("m_ObjectHideFlags", "m_EditorHideFlags") and value != "0":
            raise ValueError("Unsupported configuration hide flags")
        if name in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject") and value != "{fileID: 0}":
            raise ValueError("Configuration is not the qualified standalone asset shape")
        if name == "m_Enabled" and value != "1":
            raise ValueError("Unsupported configuration enabled flag")
        if name == "m_EditorClassIdentifier" and value:
            raise ValueError("Unsupported configuration class identifier")
        if name == "m_Name" and re.fullmatch(r"[A-Za-z_][A-Za-z_0-9 .-]*", value) is None:
            raise ValueError("Unsupported configuration plain name")
    pointer = SCRIPT.fullmatch(values["m_Script"][0])
    if pointer is None or (pointer[1], pointer[2]) != (str(row["exported_file_id"]), row["exported_guid"]):
        raise ValueError("Unsupported selected configuration script pointer")
    for name in PROFILES[(row["assembly"], row["full_name"])]:
        value, items = values[name]
        if name in ("ActiveConfig", "m_baseAsset"):
            if items:
                raise ValueError("Unsupported configuration scalar sequence")
            _asset(value)
        else:
            if value != "[]" and (value or not items):
                raise ValueError("Unsupported configuration sequence shape")
            if name in POINTER_LISTS:
                for item in items:
                    _asset(item)
            elif name == "m_overriddenFields":
                if any(IDENTIFIER.fullmatch(item) is None for item in items):
                    raise ValueError("Unsupported configuration override string")
            else:
                raise ValueError("Unsupported configuration sequence field")


def rewrite_configuration_pointers(content, bindings):
    """Validate only these three authored profiles and replace their script line.

    This pure function performs no source admission, provider action or file IO.
    It rejects unsupported shapes before returning any transformed content.
    Legacy scalar and uGUI document validation remain in their existing helper.
    """
    if not isinstance(content, bytes):
        raise ValueError("Configuration content must be bytes")
    mapping = _bindings(bindings)
    if not mapping:
        return content, 0
    try:
        text = content.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError("Unsupported configuration UTF8") from error
    if (any((ord(character) < 32 and character not in "\r\n") or ord(character) == 127
            for character in text) or any(character in text for character in ("\x85", "\u2028", "\u2029"))
            or "\r" in text.replace("\r\n", "")):
        raise ValueError("Unsupported configuration control or newline")
    mentions = [match for guid in mapping for match in re.finditer(re.escape(guid), text, re.IGNORECASE)]
    if not mentions:
        return content, 0
    headers = list(HEADER.finditer(text))
    if len(headers) != 1 or text[:headers[0].start()] not in (
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n",
            "%YAML 1.1\r\n%TAG !u! tag:unity3d.com,2011:\r\n"):
        raise ValueError("Unsupported standalone configuration document envelope")
    replacements = []
    selected_ranges = []
    for index, header in enumerate(headers):
        end = headers[index + 1].start() if index + 1 < len(headers) else len(text)
        body = text[header.end():end]
        selected = any(guid in body.lower() for guid in mapping)
        if not selected:
            continue
        if header.groups() != ("114", "11400000", "MonoBehaviour"):
            raise ValueError("Unsupported configuration document envelope")
        lines = [match for match in re.finditer(r"^  m_Script: *(.*)(\r?)$", body, re.MULTILINE)]
        if len(lines) != 1:
            raise ValueError("Unsupported selected configuration script line")
        value = lines[0][1].removesuffix("\r")
        pointer = SCRIPT.fullmatch(value)
        if pointer is None or pointer[2] not in mapping:
            raise ValueError("Unsupported selected configuration script identity")
        row = mapping[pointer[2]]
        _document(body, row)
        prefix = text[:header.start()] if index == 0 else ""
        if index == 0 and prefix not in ("", "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n", "%YAML 1.1\r\n%TAG !u! tag:unity3d.com,2011:\r\n"):
            raise ValueError("Unsupported configuration document prefix")
        start = header.end() + lines[0].start(1)
        finish = start + len(value)
        target = "{fileID: " + str(row["target_file_id"]) + ", guid: " + row["target_guid"] + ", type: 3}"
        replacements.append((start, finish, target))
        selected_ranges.append((header.start(), end))
    if any(not any(start <= match.start() < end for start, end in selected_ranges) for match in mentions):
        raise ValueError("Selected configuration GUID outside qualified document")
    result = text
    for start, end, target in reversed(replacements):
        result = result[:start] + target + result[end:]
    return result.encode("utf-8"), len(replacements)

"""Normalize Material shader pointer kinds for qualified maintained ShaderLab assets.

The exported compiled Shader is a serialized asset (type 2); its same-GUID
ShaderImporter replacement is an imported asset (type 3). Identity never moves.
"""

from pathlib import Path, PurePosixPath
import re


_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)
_IMPORTER = re.compile(r"^([A-Za-z_][A-Za-z_0-9]*Importer):$", re.MULTILINE)
_FILE_ID = re.compile(r"^  mainObjectFileID: (-?[0-9]+)$", re.MULTILINE)
_DECLARATION = re.compile(r'^[ \t]*Shader[ \t]+"([^"\\\r\n]+)"', re.MULTILINE)
_STRINGS_AND_COMMENTS = re.compile(r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?(?:\*/|\Z)')
_NAME = re.compile(r"^  m_Name: (.+)$", re.MULTILINE)
_PREFIX = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
_SHADER = _PREFIX + "--- !u!48 &4800000\nShader:\n"
_MATERIAL = _PREFIX + "--- !u!21 &2100000\nMaterial:\n"
_POINTER = re.compile(rb"  m_Shader: \{fileID: (?P<id>-?[0-9]+), guid: (?P<guid>[0-9a-f]{32}), type: (?P<type>[0-9]+)\}")


def _physical(path):
    path = Path(path)
    if any(part.is_symlink() for part in (path, *path.parents)):
        raise ValueError("Refusing symlink in material shader evidence: " + str(path))
    return path


def _file(root, relative):
    if (not isinstance(relative, str) or not relative or PurePosixPath(relative).is_absolute()
            or not PurePosixPath(relative).parts or PurePosixPath(relative).parts[0] != "Assets"
            or relative != PurePosixPath(relative).as_posix()
            or ".." in PurePosixPath(relative).parts or "\\" in relative or ":" in relative):
        raise ValueError("Invalid material shader evidence path")
    path = _physical(root / relative)
    if not path.is_file():
        raise ValueError("Material shader evidence is missing: " + str(path))
    return path


def _text(data):
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError("Unsupported material shader evidence UTF8") from error
    lf = text.replace("\r\n", "\n")
    if "\r" in lf or ("\r\n" in text and "\n" in text.replace("\r\n", "")):
        raise ValueError("Unsupported material shader evidence line endings")
    if any(ord(char) < 32 and char not in "\n\t" or ord(char) == 127 for char in lf):
        raise ValueError("Unsupported material shader evidence control character")
    return lf


def checked_metadata_text(content):
    """Admit plain block keys without decoding alternate YAML key spellings."""
    text = _text(content)
    _simple_yaml_lines(text)
    # Identity checks below read literal keys; quoted/explicit keys could hide
    # duplicates, including escaped spellings of an otherwise ordinary key.
    if re.search(r"^[ \t]*(?:[\"']|\?(?:[ \t]|$))", text, re.MULTILINE):
        raise ValueError("Unsupported mapping key in material shader metadata")
    return text


def _metadata(path, importer, file_id=None):
    text = checked_metadata_text(path.read_bytes())
    guids = _GUID.findall(text)
    if (len(guids) != 1 or len(re.findall(r"^guid\b.*$", text, re.MULTILINE)) != 1
            or guids[0] == "0" * 32 or _IMPORTER.findall(text) != [importer]
            or len(re.findall(r"^[A-Za-z_][A-Za-z_0-9]*Importer\b.*$", text, re.MULTILINE)) != 1):
        raise ValueError("Ambiguous material shader metadata: " + str(path))
    if file_id is not None:
        following = text[next(_IMPORTER.finditer(text)).end():]
        next_top = re.search(r"^[^ \t\n#].*$", following, re.MULTILINE)
        block = following if next_top is None else following[:next_top.start()]
        if (_FILE_ID.findall(block) != [str(file_id)]
                or len(re.findall(r"^  mainObjectFileID\b.*$", text, re.MULTILINE)) != 1):
            raise ValueError("Wrong material shader importer local ID: " + str(path))
    return guids[0]


def _shader_names(text):
    # Preserve quoted names/paths; declarations inside comments are not sources.
    def without_comment(match):
        token = match[0]
        return token if token.startswith('"') else "".join("\n" if char == "\n" else " " for char in token)
    return _DECLARATION.findall(_STRINGS_AND_COMMENTS.sub(without_comment, text))


def _simple_yaml_lines(text):
    """Reject scalar shapes that can hide a field-looking line in string data.

    This is a conservative boundary for the observed standalone exports, not a
    YAML parser. Single-line quoted values/keys, escaped quotes and plain-text
    apostrophes remain intact. Multiline quoted/block scalars, tags, anchors and
    aliases are unsupported; their properties can hide where a quoted value starts.
    Flow collections must also close on the same line, outside quotes/comments.
    """
    for line in text.splitlines():
        index = 0
        flow = []
        while index < len(line):
            char = line[index]
            if char == "#" and (index == 0 or line[index - 1].isspace()):
                break
            prefix = line[:index].rstrip()
            scalar_start = not prefix or prefix[-1] in ":,[{-?"
            if scalar_start and char in "|>&*!":
                raise ValueError("Unsupported scalar property in shader identity YAML")
            if char in "[{":
                flow.append(char)
            elif char in "]}":
                opening = "{" if char == "}" else "["
                if not flow or flow.pop() != opening:
                    raise ValueError("Unbalanced flow collection in shader identity YAML")
            if not scalar_start or char not in "\"'":
                index += 1
                continue
            quote = char
            index += 1
            while index < len(line):
                if quote == '"' and line[index] == "\\":
                    index += 2
                elif line[index] == quote:
                    if quote == "'" and index + 1 < len(line) and line[index + 1] == "'":
                        index += 2
                        continue
                    index += 1
                    break
                else:
                    index += 1
            else:
                raise ValueError("Unsupported multiline quoted scalar in shader identity YAML")
        if flow:
            raise ValueError("Unsupported multiline flow collection in shader identity YAML")


def maintained_shader_overrides(repo_root, exported_project, asset_map):
    """Join each recovered ShaderLab source to one quarantined compiled Shader.

    Callers activate this only for the release whose critical code hashes were
    qualified. Names validate an exact GUID join; they never select a target.
    """
    root, export = _physical(Path(repo_root)), _physical(Path(exported_project))
    directory = _physical(root / "Assets/Shaders/Recovered")
    if not directory.exists():
        return {}
    if not directory.is_dir():
        raise ValueError("Recovered shader sources are not a directory")
    paths = sorted(directory.rglob("*"))
    for path in paths:
        _physical(path)
        if path.name.lower().endswith(".shader.meta"):
            _file(root, path.relative_to(root).as_posix()[:-5])
    if not isinstance(asset_map, dict) or not isinstance(asset_map.get("assets"), list) or any(not isinstance(row, dict) for row in asset_map["assets"]):
        raise ValueError("Invalid material shader asset map")
    result = {}
    for path in paths:
        if not path.is_file() or path.suffix.lower() != ".shader":
            continue
        relative = path.relative_to(root).as_posix()
        names = _shader_names(_text(_file(root, relative).read_bytes()))
        guid = _metadata(_file(root, relative + ".meta"), "ShaderImporter")
        if len(names) != 1 or guid in result:
            raise ValueError("Ambiguous recovered shader source identity: " + relative)
        rows = [row for row in asset_map.get("assets", []) if row.get("guid") == guid]
        if (len(rows) != 1 or rows[0].get("shader_quarantined") is not True
                or rows[0].get("code_quarantined") is not False):
            raise ValueError("Recovered shader lacks one quarantined original identity: " + relative)
        row = rows[0]
        original = _text(_file(export, row.get("path")).read_bytes())
        original_guid = _metadata(_file(export, row["path"] + ".meta"), "NativeFormatImporter", 4800000)
        if not original.startswith(_SHADER):
            raise ValueError("Original shader has an unsupported document envelope: " + row["path"])
        preamble = re.split(r"^  m_ParsedForm:$", original[len(_SHADER):], maxsplit=1, flags=re.MULTILINE)
        if len(preamble) != 2:
            raise ValueError("Original shader lacks its compiled parsed form: " + row["path"])
        _simple_yaml_lines(preamble[0])
        original_names = _NAME.findall(original)
        if (len(re.findall(r"^--- !u!", original, re.MULTILINE)) != 1
                or _NAME.findall(preamble[0]) != names or original_names != names or original_guid != guid):
            raise ValueError("Recovered shader differs from its compiled original identity: " + relative)
        result[guid] = {"name": names[0], "file_id": 4800000,
                        "exported_path": row["path"], "maintained_path": relative}
    return result


def rewrite_material_shader_pointer(content, qualified):
    """Return unchanged bytes or replace only a selected Material.m_Shader kind.

    Unknown/null pointers and non-Material documents are never normalized. A
    selected pointer requires the complete standalone Material envelope and one
    exact top-level field; malformed selections fail before staging is published.
    """
    if not isinstance(content, bytes) or not isinstance(qualified, dict):
        raise ValueError("Material shader rewrite requires bytes and qualified identities")
    if not qualified or not re.search(rb"^--- !u!21\b|^Material:\r?$", content, re.MULTILINE):
        return content, None
    lines = content.splitlines(keepends=True)
    fields = [(offset, line.rstrip(b"\r\n")) for offset, line in _offset_lines(lines)
              if re.match(rb"^  (?:m_Shader\b|[\"']m_Shader[\"'][ \t]*:)", line)]
    if not any(guid.encode() in line.lower() for _, line in fields for guid in qualified):
        return content, None
    text = _text(content)
    if (not text.startswith(_MATERIAL) or len(re.findall(r"^--- !u!", text, re.MULTILINE)) != 1
            or not text.endswith("\n") or len(fields) != 1):
        raise ValueError("Unsupported selected Material envelope or shader field")
    _simple_yaml_lines(text[len(_MATERIAL):])
    offset, field = fields[0]
    pointer = _POINTER.fullmatch(field)
    if pointer is None:
        raise ValueError("Malformed selected Material.m_Shader pointer")
    guid = pointer["guid"].decode("ascii")
    if guid not in qualified:
        return content, None
    if pointer["id"] != b"4800000" or pointer["type"] not in (b"2", b"3") or qualified[guid].get("file_id") != 4800000:
        raise ValueError("Unsupported selected Material.m_Shader identity or kind")
    if pointer["type"] == b"3":
        return content, None
    start = offset + pointer.start("type")
    rewritten = content[:start] + b"3" + content[start + 1:]
    return rewritten, {"guid": guid, "file_id": 4800000, "old_type": 2, "prepared_type": 3, **qualified[guid]}


def _offset_lines(lines):
    offset = 0
    for line in lines:
        yield offset, line
        offset += len(line)

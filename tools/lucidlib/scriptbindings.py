"""Restore approved script pointers in generated asset staging only."""

import hashlib
import json
import math
import struct
from pathlib import Path
import re

from .bindings import run_layout_inventory
from .verification import artifact_fingerprint


# These are maintained implementations, not exported declarations. New types
# require native behavior recovery and a real Unity serialization proof before
# joining this list. Each supplied release still gets a strict layout comparison.
RULES = ({
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.LevelStartPositionDefinition",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/LevelStartPositionDefinition.cs",
    "maintained_path": "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs",
    "binary_sha256": "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
    "metadata_sha256": "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
    "runtime_sources": {
        "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs": "13283ab5c73e5d97032f1be3b2be1478708fa187999f5943b14c9295dbf7a92f",
        "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
        "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    },
}, {
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.LevelDefinition",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/LevelDefinition.cs",
    "maintained_path": "Assets/Scripts/Definitions/LevelDefinition.cs",
    "binary_sha256": "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
    "metadata_sha256": "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
    "runtime_sources": {
        "Assets/Scripts/Definitions/LevelDefinition.cs": "e1789c615fe0c7d6156d6ea84a355afb270706e274647fda8d9737c4668d3774",
        "Assets/Scripts/Definitions/ILevelDefinition.cs": "a3fb7c57bad19ec9224b97ba969c03e72cd72b84adfc1e92e6b9b9ec1ac8aec9",
        "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
        "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    },
},)

# These three upstream components retain the shipped scalar layout. Pin the
# actual builtin package sources as well as its version: different Editor
# releases can supply different code under the same builtin version.
UGUI_RUNTIME_SOURCES = {
    "Packages/com.unity.ugui/Runtime/UnityEngine.UI.asmdef": "5e1a5809db8304714763c1eab0c2086b4816b806902da5aac2aa46b18addffde",
    "Packages/com.unity.ugui/Runtime/EventSystem/UIBehaviour.cs": "2a145c6aa56cfd949bad90853cce3bb5c20272c3cf1583e72d48fde786a6c1fb",
    "Packages/com.unity.ugui/Runtime/UI/Core/SetPropertyUtility.cs": "763d54578999cf89f9d7102f181caeb973cfcb82f81cd812d3f0b7f0780b68e4",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutElement.cs": "c6e88f6d086a6dfc4111a03024ea1279d6d2c6b755e0a5df50f64dfa06edd732",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/ContentSizeFitter.cs": "a6b30a3b6e697524ce16f07e1807e870faf8d0c3e8c43004474eb5b8bb3c887b",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/AspectRatioFitter.cs": "c433f85be46913c31817c56abbbfbd4869337bc86929c6ad6f45b4861f16c555",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutUtility.cs": "1a5d931224f626980f509af94acdc17836139e47333ca271e7df18430c37710d",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutRebuilder.cs": "a716457d2bd3539145a02a9ef2184582db066bf437648cbd2a6d42b7c60b42fe"
}

PACKAGE_RULES = (
    {
        "assembly": "UnityEngine.UI",
        "full_name": "UnityEngine.UI.LayoutElement",
        "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/LayoutElement.cs",
        "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutElement.cs",
        "binary_sha256": "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
        "metadata_sha256": "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
        "package": "com.unity.ugui",
        "runtime_sources": UGUI_RUNTIME_SOURCES
    },
    {
        "assembly": "UnityEngine.UI",
        "full_name": "UnityEngine.UI.ContentSizeFitter",
        "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/ContentSizeFitter.cs",
        "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Layout/ContentSizeFitter.cs",
        "binary_sha256": "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
        "metadata_sha256": "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
        "package": "com.unity.ugui",
        "runtime_sources": UGUI_RUNTIME_SOURCES
    },
    {
        "assembly": "UnityEngine.UI",
        "full_name": "UnityEngine.UI.AspectRatioFitter",
        "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/AspectRatioFitter.cs",
        "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Layout/AspectRatioFitter.cs",
        "binary_sha256": "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
        "metadata_sha256": "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
        "package": "com.unity.ugui",
        "runtime_sources": UGUI_RUNTIME_SOURCES
    },
)
RULES += PACKAGE_RULES
UGUI_PACKAGE_SHA256 = "6830b8501f301344de9feab76c65d1d9339b496c55cb16f9827658e78234ee78"
UGUI_LOCK = {"version": "1.0.0", "depth": 0, "source": "builtin",
             "dependencies": {"com.unity.modules.ui": "1.0.0", "com.unity.modules.imgui": "1.0.0"}}

GUID = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
DOCUMENT = re.compile(r"^--- !u!(?P<class>\d+) &-?\d+\r?\n(?P<label>\w+):\r?\n(?P<fields>.*?)(?=^--- !u!|\Z)", re.MULTILINE | re.DOTALL)
POINTER = re.compile(r"^(?P<prefix>  m_Script: *)\{(?P<body>[^}\r\n]*)\}(?P<trailer> *\r?$)", re.MULTILINE)
EXACT_POINTER = re.compile(r"\s*fileID:\s*(?P<id>-?\d+)\s*,\s*guid:\s*(?P<guid>[0-9a-fA-F]{32})\s*,\s*type:\s*(?P<type>\d+)\s*")


def _owned_file(root, relative):
    path = root / relative
    if path.is_symlink() or any(p.is_symlink() for p in path.parents):
        raise ValueError("Refusing symlinked script evidence: " + str(path))
    if not path.is_file():
        raise ValueError("Script evidence is missing: " + str(path))
    return path


def _package_json(data):
    def object_pairs(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate key in approved package JSON: " + key)
            result[key] = value
        return result
    value = json.loads(data, object_pairs_hook=object_pairs)
    if not isinstance(value, dict) or not isinstance(value.get("dependencies"), dict):
        raise ValueError("Approved package JSON must contain a dependency object")
    return value


def _script_file(root, relative, rule):
    """Resolve only the reviewed builtin UI package; never arbitrary packages."""
    if "package" not in rule:
        return _owned_file(root, relative)
    if rule["package"] != "com.unity.ugui" or not relative.startswith("Packages/com.unity.ugui/"):
        raise ValueError("Unsupported approved script package identity")
    manifest = _package_json(_owned_file(root, "Packages/manifest.json").read_bytes())
    lock = _package_json(_owned_file(root, "Packages/packages-lock.json").read_bytes())
    if manifest.get("dependencies", {}).get("com.unity.ugui") != "1.0.0" or lock.get("dependencies", {}).get("com.unity.ugui") != UGUI_LOCK:
        raise ValueError("Approved Unity UI package resolution changed")
    package = root / "Library/PackageCache/com.unity.ugui@1.0.0"
    descriptor = _owned_file(package, "package.json").read_bytes()
    if hashlib.sha256(descriptor).hexdigest() != UGUI_PACKAGE_SHA256:
        raise ValueError("Approved Unity UI package descriptor changed")
    suffix = relative[len("Packages/com.unity.ugui/"):]
    if not suffix or any(part in ("", ".", "..") for part in suffix.split("/")):
        raise ValueError("Unsupported approved script package path")
    return _owned_file(package, suffix)


def resolve_bindings(repo_root, work_dir, exported_project, asset_map, *, input_fingerprint=None):
    """Prove exact current source/layout/asset identities before staging changes."""
    root = Path(repo_root).expanduser()
    if root.is_symlink() or any(parent.is_symlink() for parent in root.parents):
        raise ValueError("Refusing symlinked project root for script evidence")
    root = root.resolve(strict=True)
    work, export = map(Path, (work_dir, exported_project))
    rows = asset_map.get("assets", [])
    selected = []
    for rule in RULES:
        originals = [row for row in rows if row.get("path") == rule["export_path"]]
        if not originals:
            continue
        if len(originals) != 1 or originals[0].get("code_quarantined") is not True:
            raise ValueError("Exported script identity is ambiguous: " + rule["full_name"])
        if sum(row.get("guid") == originals[0].get("guid") for row in rows) != 1:
            raise ValueError("Exported script GUID has multiple asset owners")
        source = _script_file(root, rule["maintained_path"], rule)
        raw = _owned_file(export, rule["export_path"])
        meta = _owned_file(export, rule["export_path"] + ".meta")
        identities = GUID.findall(meta.read_text(encoding="utf-8"))
        if len(identities) != 1 or identities[0] != originals[0].get("guid"):
            raise ValueError("Exported script metadata differs from its asset map")
        selected.append((rule, originals[0], source, raw))
    if not selected:
        return {"status": "not_applicable", "bindings": [], "references_modified": False}

    critical = (input_fingerprint or {}).get("critical_files", {})
    for rule, _, _, _ in selected:
        for relative, expected in rule["runtime_sources"].items():
            if hashlib.sha256(_script_file(root, relative, rule).read_bytes()).hexdigest() != expected:
                raise ValueError("Maintained script behavior changed; review before binding: " + relative)
        if critical.get("Contents/Frameworks/GameAssembly.dylib") != rule["binary_sha256"] or critical.get(
                "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat") != rule["metadata_sha256"]:
            raise ValueError("Script behavior has not been verified for this supplied release")

    summary = run_layout_inventory(root, work)
    if summary.get("status") != "compared" or summary.get("source_freshness_verified") is not True:
        raise ValueError("Current Unity script layout proof failed; previous assets remain unchanged")
    comparison_path = Path(summary["comparison_path"])
    if work.resolve() not in comparison_path.resolve().parents or comparison_path.is_symlink():
        raise ValueError("Script layout proof lies outside the generated work directory")
    comparison_bytes = comparison_path.read_bytes()
    comparison = json.loads(comparison_bytes)
    if comparison.get("command") != "compare-monoscript-layouts" or comparison.get("status") != "compared":
        raise ValueError("Unsupported script layout comparison")
    # Layout alone cannot establish release identity: another release can have
    # the same fields but different behavior. Match both recovered code inputs.
    schema_path = Path(summary["schema_path"])
    if work.resolve() not in schema_path.resolve().parents:
        raise ValueError("Original schema lies outside the generated work directory")
    schema_path = _owned_file(work, schema_path.relative_to(work))
    schema_bytes = schema_path.read_bytes()
    schema_digest = hashlib.sha256(schema_bytes).hexdigest()
    schema = json.loads(schema_bytes)
    schema_summary = json.loads(_owned_file(work, "native-recovery/latest-schemas.json").read_bytes())
    schema_source = schema_summary.get("source", {})
    if schema_summary.get("status") != "ready" or schema_summary.get("command") != "schemas" or schema_summary.get(
            "evidence_report") != str(schema_path) or schema.get("status") != "ready" or schema.get("command") != "schemas":
        raise ValueError("Original schema source proof is incomplete")
    if schema_digest != comparison.get("original_schema_sha256") or schema_digest != summary.get("original_schema_sha256"):
        raise ValueError("Original schema differs from the Unity layout proof")
    if schema.get("metadata_sha256") != critical.get("Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat") or schema_source.get(
            "metadata_sha256") != schema.get("metadata_sha256") or schema_source.get("binary_sha256") != critical.get(
                "Contents/Frameworks/GameAssembly.dylib") or schema_source.get("analysis_binary_sha256") != schema.get("native_binary_sha256"):
        raise ValueError("Original schema belongs to a different supplied release")
    current = artifact_fingerprint(root)
    if comparison.get("source_fingerprint") != current or summary.get("source_fingerprint") != current:
        raise ValueError("Script layout proof is stale for maintained source")
    bindings = []
    for rule, original, source, raw in selected:
        candidates = [c for c in comparison.get("candidates", [])
                      if c.get("assembly") == rule["assembly"] and c.get("full_name") == rule["full_name"]]
        if len(candidates) != 1 or candidates[0].get("status") != "compatible_layout" or candidates[0].get("issues"):
            raise ValueError("Original script layout is unresolved: " + rule["full_name"])
        candidate = candidates[0]
        scripts = candidate.get("loaded_scripts", [])
        if len(scripts) != 1 or scripts[0].get("path") != rule["maintained_path"] or scripts[0].get("class_resolved") is not True:
            raise ValueError("Loaded maintained MonoScript identity is ambiguous")
        target = scripts[0]
        if rule.get("package") == "com.unity.ugui":
            expected_package = root / "Library/PackageCache/com.unity.ugui@1.0.0"
            if (target.get("package_name") != "com.unity.ugui" or target.get("package_version") != "1.0.0" or
                    target.get("package_builtin") is not True or target.get("package_resolved_path") != str(expected_package) or
                    target.get("source_path") != str(source)):
                raise ValueError("Loaded Unity UI package differs from its approved physical source")
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if target.get("source_sha256") != digest:
            raise ValueError("Maintained script changed after its Unity inventory")
        if not re.fullmatch(r"[0-9a-f]{32}", target.get("guid", "")) or target["guid"] == "0" * 32 or type(target.get("file_id")) is not int or not (
                -(1 << 63) <= target["file_id"] < (1 << 63)) or not target["file_id"]:
            raise ValueError("Loaded script GUID/local file ID is invalid")
        local_meta = _script_file(root, rule["maintained_path"] + ".meta", rule)
        if GUID.findall(local_meta.read_text(encoding="utf-8")) != [target["guid"]]:
            raise ValueError("Maintained script metadata differs from the loaded MonoScript")
        bindings.append({**rule, "exported_guid": original["guid"], "exported_file_id": 11500000,
                         "target_guid": target["guid"], "target_file_id": target["file_id"],
                         "original_token": candidate["original_token"], "source_sha256": digest,
                         "exported_source_sha256": hashlib.sha256(raw.read_bytes()).hexdigest()})
    return {"status": "verified_layout", "bindings": bindings, "references_modified": False,
            "source_fingerprint": current, "comparison_path": str(comparison_path),
            "comparison_sha256": hashlib.sha256(comparison_bytes).hexdigest(),
            "original_schema_sha256": comparison.get("original_schema_sha256")}



UGUI_FIELDS = {
    "UnityEngine.UI.LayoutElement": {
        "m_IgnoreLayout": "bool", "m_MinWidth": "float", "m_MinHeight": "float",
        "m_PreferredWidth": "float", "m_PreferredHeight": "float",
        "m_FlexibleWidth": "float", "m_FlexibleHeight": "float", "m_LayoutPriority": "int"},
    "UnityEngine.UI.ContentSizeFitter": {"m_HorizontalFit": 2, "m_VerticalFit": 2},
    "UnityEngine.UI.AspectRatioFitter": {"m_AspectMode": 4, "m_AspectRatio": "float"},
}
UGUI_COMMON_FIELDS = ("m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
                      "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier")


def _validate_ugui_document(fields, rule):
    """Accept only the complete scalar documents observed in this release."""
    if rule.get("package") != "com.unity.ugui":
        return
    own = UGUI_FIELDS.get(rule.get("full_name"))
    if own is None:
        raise ValueError("Unsupported Unity UI component")
    values = {}
    for line in fields.splitlines():
        parsed = re.fullmatch(r"  ([A-Za-z_][A-Za-z_0-9]*): *(.*)", line)
        if not parsed or parsed[1] in values:
            raise ValueError("Unsupported or duplicate Unity UI field")
        values[parsed[1]] = parsed[2]
    if tuple(values) != UGUI_COMMON_FIELDS + tuple(own):
        raise ValueError("Unsupported Unity UI field set or order")
    if any(values[key] != "0" for key in ("m_ObjectHideFlags", "m_EditorHideFlags")) or values["m_Enabled"] != "1" or any(
            values[key] != "{fileID: 0}" for key in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset")) or any(
            values[key] for key in ("m_Name", "m_EditorClassIdentifier")):
        raise ValueError("Unsupported Unity UI common field value")
    owner = re.fullmatch(r"\{fileID: (-?[0-9]+)\}", values["m_GameObject"])
    if not owner or not -(1 << 63) <= int(owner[1]) < (1 << 63) or int(owner[1]) == 0:
        raise ValueError("Unsupported Unity UI owner pointer")
    if not re.fullmatch(r"\{fileID: -?[0-9]+, guid: [0-9a-fA-F]{32}, type: 3\}", values["m_Script"]):
        raise ValueError("Unsupported Unity UI script pointer syntax")
    for key, kind in own.items():
        value = values[key]
        if kind == "float":
            try:
                if not re.fullmatch(r"-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", value):
                    raise ValueError("Unsupported Unity UI numeric syntax")
                number = float(value)
                if not math.isfinite(number) or not math.isfinite(struct.unpack("<f", struct.pack("<f", number))[0]):
                    raise ValueError("Nonfinite Unity UI scalar")
            except (ValueError, OverflowError, struct.error) as error:
                raise ValueError("Unsupported Unity UI float") from error
        elif kind == "bool":
            if value not in ("0", "1"):
                raise ValueError("Unsupported Unity UI Boolean")
        elif not re.fullmatch(r"-?[0-9]+", value) or not -(1 << 31) <= int(value) < (1 << 31) or (type(kind) is int and not 0 <= int(value) <= kind):
            raise ValueError("Unsupported Unity UI integer or enum")


def rewrite_script_pointers(content, bindings):
    """Change only exact m_Script pointers, preserving every other byte."""
    mapping = {row["exported_guid"]: row for row in bindings}
    if len(mapping) != len(bindings):
        raise ValueError("Duplicate exported script binding")
    changes = 0
    package_changes = {row["exported_guid"]: 0 for row in bindings if row.get("package") == "com.unity.ugui"}

    def replace(match):
        nonlocal changes
        body = match.group("body")
        referenced = re.search(r"\bguid:\s*([0-9a-fA-F]{32})\b", body)
        if not referenced or referenced[1].lower() not in mapping:
            return match[0]
        exact = EXACT_POINTER.fullmatch(body)
        rule = mapping[referenced[1].lower()]
        if not exact or int(exact["id"]) != rule["exported_file_id"] or int(exact["type"]) != 3:
            raise ValueError("Unsupported original MonoScript pointer identity")
        changes += 1
        if rule.get("package") == "com.unity.ugui":
            package_changes[referenced[1].lower()] += 1
        return match["prefix"] + "{fileID: " + str(rule["target_file_id"]) + ", guid: " + rule["target_guid"] + ", type: 3}" + match["trailer"]

    def document(match):
        fields = match["fields"]
        pointers = [p for p in POINTER.finditer(fields)
                    if any(guid in p["body"].lower() for guid in mapping)]
        if not pointers:
            # A selected UI pointer with unsupported indentation must not be
            # silently left unresolved alongside successfully rewritten peers.
            if any(row.get("package") == "com.unity.ugui" and row["exported_guid"] in line.lower()
                   for row in bindings for line in re.findall(r"^[ \t]*m_Script:[^\r\n]*", fields, re.MULTILINE)):
                raise ValueError("Unsupported Unity UI script pointer indentation")
            return match[0]
        if match["class"] != "114" or match["label"] != "MonoBehaviour" or len(pointers) != 1:
            raise ValueError("Unsupported Unity script document")
        # The approved definition has only scalar fields and inline engine
        # pointers. Refuse nested/block/quoted multiline data rather than risk
        # mistaking string contents for a field. Extend this only with proof for
        # a newly approved type's real authored serialization.
        for line in fields.splitlines():
            field = re.fullmatch(r"  [A-Za-z_][A-Za-z_0-9]*: *(.*)", line)
            if not field or field[1].startswith(("|", ">", "'")):
                raise ValueError("Unsupported authored script field shape")
            if field[1].startswith('"'):
                try:
                    if not isinstance(json.loads(field[1]), str):
                        raise ValueError("Unsupported quoted Unity field")
                except (ValueError, TypeError) as error:
                    raise ValueError("Unsupported quoted Unity field") from error
        referenced = EXACT_POINTER.fullmatch(pointers[0]["body"])
        if referenced:
            _validate_ugui_document(fields, mapping[referenced["guid"].lower()])
        rewritten = POINTER.sub(replace, fields)
        return match[0][:match.start("fields") - match.start()] + rewritten

    # Unity text assets are UTF-8. Decoding only selected assets avoids treating
    # arbitrary binary/audio data as YAML; newline bytes survive unchanged.
    text = content.decode("utf-8")
    # A malformed/stripped document header or label must not hide a selected
    # script beside a valid document. Count exact selected pointer field lines
    # independently of the supported document parser before rewriting.
    pointer_lines = re.findall(r"^[ \t]*m_Script:[^\r\n]*", text, re.MULTILINE)
    expected = {guid: sum(guid in line.lower() for line in pointer_lines) for guid in package_changes}
    rewritten = DOCUMENT.sub(document, text)
    if package_changes != expected:
        raise ValueError("Unsupported Unity UI script document envelope")
    return rewritten.encode("utf-8"), changes

"""Strict, read-only comparisons of original schemas and loaded MonoScript layouts.

This module never edits references or imports generated declarations. A matching
layout is evidence for a later binding decision, not recovered method behavior.
"""

import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
from typing import Any, Dict, Tuple
import uuid

from .bootstrap import CACHE_ROOT, managed_path, validate_work_dir, write_json


class LayoutError(ValueError):
    """A report cannot establish an unambiguous layout."""


def type_identity(record: dict) -> Tuple[Any, ...]:
    """Ignore metadata tokens/versions; preserve CLR assembly and type shape."""
    if not isinstance(record, dict) or not isinstance(record.get("assembly"), str):
        raise LayoutError("Missing type assembly identity")
    assembly = record["assembly"]
    kind = record.get("kind")
    if kind == "named":
        name = record.get("reflection_full_name")
        if not isinstance(name, str) or not name or record.get("canonical_name") != name:
            raise LayoutError("Invalid named CLR type identity")
        return kind, assembly, name
    if kind == "generic_instance":
        name, arguments = record.get("definition"), record.get("arguments")
        if not isinstance(name, str) or not isinstance(arguments, list) or not arguments:
            raise LayoutError("Invalid constructed generic type identity")
        return kind, assembly, name, tuple(type_identity(a) for a in arguments)
    if kind in ("array", "pointer", "by_reference"):
        element = type_identity(record.get("element"))
        if kind == "array":
            rank, vector = record.get("rank"), record.get("vector_array")
            if type(rank) is not int or rank < 1 or type(vector) is not bool or (vector and rank != 1):
                raise LayoutError("Invalid array rank/vector identity")
            return kind, assembly, element, rank, vector
        return kind, assembly, element
    if kind == "generic_parameter":
        owner, index = record.get("parameter_owner"), record.get("parameter_index")
        if owner not in ("type", "method") or type(index) is not int or index < 0:
            raise LayoutError("Invalid generic parameter identity")
        return kind, assembly, owner, index
    raise LayoutError("Unsupported type reference kind: " + str(kind))


def _attribute_value(value: dict, *, require_string_evidence=False) -> Any:
    if not isinstance(value, dict):
        raise LayoutError("Incomplete custom attribute value")
    if value.get("value_complete") is False:
        raise LayoutError("Custom attribute value is explicitly incomplete")
    kind = value.get("kind")
    if kind == "primitive":
        primitive, data = value.get("type"), value.get("value")
        if primitive == "IL2CPP_TYPE_R4":
            data = struct.unpack("<f", struct.pack("<f", data))[0]
        if primitive == "IL2CPP_TYPE_CHAR" and isinstance(data, str) and len(data) == 1:
            data = ord(data)
        if not isinstance(primitive, str) or "value" not in value:
            raise LayoutError("Incomplete primitive attribute")
        if primitive == "IL2CPP_TYPE_STRING":
            if data is not None and not isinstance(data, str):
                raise LayoutError("Custom attribute string value is invalid")
            # The pinned native reader previously collapsed zero-length strings
            # and null strings. Loaded Reflection values preserve the distinction;
            # original ambiguous values require a bounded raw-blob decode receipt.
            if require_string_evidence and (data is None or data == ""):
                expected_length = -1 if data is None else 0
                digest = value.get("raw_encoding_sha256")
                if (value.get("value_complete") is not True or type(value.get("string_length")) is not int or
                        value["string_length"] != expected_length or not isinstance(digest, str) or
                        re.fullmatch(r"[a-f0-9]{64}", digest) is None):
                    raise LayoutError("Original attribute string has unverified empty/null encoding")
        return kind, primitive, data
    if kind == "enum":
        return kind, type_identity(value.get("type")), _attribute_value(value.get("value"), require_string_evidence=require_string_evidence)
    if kind == "type":
        return kind, None if value.get("value") is None else type_identity(value["value"])
    if kind == "null":
        return kind, None
    if kind == "array":
        values = value.get("value")
        if values is not None and not isinstance(values, list):
            raise LayoutError("Incomplete attribute array")
        enum = value.get("enum_type")
        return kind, value.get("element_type"), None if enum is None else type_identity(enum), \
            None if values is None else tuple(_attribute_value(v, require_string_evidence=require_string_evidence) for v in values)
    raise LayoutError("Unsupported custom attribute value kind")


def _attributes(record: dict, *, require_string_evidence=False) -> Tuple[Any, ...]:
    if record.get("custom_attributes_complete") is not True or not isinstance(record.get("custom_attributes"), list):
        raise LayoutError("Custom attributes are incomplete")
    result = []
    for attribute in record["custom_attributes"]:
        # Reflection exposes these ECMA flags as pseudo attributes. The original
        # schema records their exact bits separately rather than fabricating them.
        name = attribute.get("full_name")
        if name in ("System.SerializableAttribute", "System.NonSerializedAttribute"):
            continue
        if not isinstance(name, str) or not isinstance(attribute.get("assembly"), str):
            raise LayoutError("Custom attribute identity is incomplete")
        arguments = tuple(_attribute_value(v, require_string_evidence=require_string_evidence) for v in attribute.get("arguments", []))
        named = []
        for group in ("fields", "properties"):
            entries = attribute.get(group)
            if not isinstance(entries, list):
                raise LayoutError("Custom attribute named arguments are incomplete")
            named.extend((group, p["name"], _attribute_value(p["value"], require_string_evidence=require_string_evidence)) for p in entries)
        result.append((attribute["assembly"], name, arguments, tuple(sorted(named, key=repr))))
    return tuple(sorted(result, key=repr))


def _index(report: dict) -> Dict[Tuple[str, str], dict]:
    index = {}
    assemblies = report.get("assemblies")
    if not isinstance(assemblies, list):
        raise LayoutError("Assembly inventory is missing")
    assembly_names = set()
    for assembly in assemblies:
        name = assembly.get("name")
        if not isinstance(name, str) or not name or name in assembly_names:
            raise LayoutError("Ambiguous assembly identity: " + str(name))
        assembly_names.add(name)
        if not isinstance(assembly.get("types"), list):
            raise LayoutError("Type inventory is missing")
        for record in assembly["types"]:
            full_name = record.get("full_name")
            if not isinstance(full_name, str) or not full_name or record.get("assembly") != name:
                raise LayoutError("Type identity does not agree with its assembly")
            key = name, full_name
            if key in index:
                raise LayoutError("Ambiguous type identity: " + repr(key))
            index[key] = record
    return index


def _candidate_fields(record: dict, *, require_string_evidence=False) -> list:
    if record.get("schema_complete") is not True:
        raise LayoutError("Type schema is incomplete")
    _attributes(record, require_string_evidence=require_string_evidence)
    if not isinstance(record.get("fields"), list):
        raise LayoutError("Field inventory is missing")
    names, result = set(), []
    for field in record["fields"]:
        name = field.get("name")
        if not isinstance(name, str) or name in names or field.get("schema_complete") is not True:
            raise LayoutError("Ambiguous or incomplete declared field")
        names.add(name)
        _attributes(field, require_string_evidence=require_string_evidence)
        flags = field.get("attributes")
        if type(flags) is not int:
            raise LayoutError("Field flags are incomplete")
        expected = {
            "is_public": flags & 7 == 6,
            "is_static": bool(flags & 16), "is_const": bool(flags & 64),
            "is_readonly": bool(flags & 32),
            "non_serialized": bool(flags & 128) or any(a.get("full_name") == "System.NonSerializedAttribute" for a in field["custom_attributes"]),
            "serialize_field": any(a.get("full_name") == "UnityEngine.SerializeField" for a in field["custom_attributes"]),
            "serialize_reference": any(a.get("full_name") == "UnityEngine.SerializeReference" for a in field["custom_attributes"]),
        }
        if any(field.get(k) is not v for k, v in expected.items()):
            raise LayoutError("Inconsistent field serialization flags: " + name)
        candidate = not any(expected[k] for k in ("is_static", "is_const", "is_readonly", "non_serialized")) and \
            any(expected[k] for k in ("is_public", "serialize_field", "serialize_reference"))
        if field.get("unity_serialization_candidate") is not candidate:
            raise LayoutError("Inconsistent serialization candidate: " + name)
        type_identity(field.get("field_type"))
        if candidate:
            result.append(field)
    # Do not sort: metadata declaration order matters for native serialized layout.
    return result


class _Comparison:
    def __init__(self, original: dict, loaded: dict):
        self.original = _index(original)
        self.loaded = _index(loaded)

    def layout(self, key: Tuple[str, str]) -> list:
        issues, active, checked = [], set(), set()

        def issue(reason, owner, detail=""):
            issues.append({"reason": reason, "type": list(owner), "detail": detail})

        def reference(left, right, owner, detail):
            if type_identity(left) != type_identity(right):
                issue("type_identity_mismatch", owner, detail)
                return
            kind = left["kind"]
            if kind == "array":
                if left["rank"] != 1 or not left["vector_array"]:
                    issue("unsupported_serialized_array", owner, detail)
                reference(left["element"], right["element"], owner, detail)
            elif kind == "generic_instance":
                # Unity 2022 permits List<T>; arbitrary generic data requires
                # additional serializer evidence and is deliberately blocked.
                if left["assembly"] != "mscorlib" or left["definition"] != "System.Collections.Generic.List`1":
                    issue("unsupported_serialized_generic", owner, detail)
                for l_arg, r_arg in zip(left["arguments"], right["arguments"]):
                    reference(l_arg, r_arg, owner, detail)
            elif kind in ("generic_parameter", "pointer", "by_reference"):
                issue("unsupported_serialized_type", owner, detail)
            elif kind == "named":
                dependency = left["assembly"], left["reflection_full_name"]
                if dependency[0] == "mscorlib" and dependency[1] in {
                    "System.Boolean", "System.Byte", "System.SByte", "System.Char", "System.Int16", "System.UInt16",
                    "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Single", "System.Double", "System.String"
                }:
                    return
                visit(dependency, as_field=True)

        def visit(owner, as_field=False):
            if owner in checked or owner in active:
                return
            left, right = self.original.get(owner), self.loaded.get(owner)
            if left is None or right is None:
                issue("missing_layout_dependency", owner)
                return
            active.add(owner)
            try:
                lfields = _candidate_fields(left, require_string_evidence=True)
                rfields = _candidate_fields(right)
                for label in ("is_value_type", "is_enum", "is_abstract", "declaring_type", "generic_parameters", "unity_component", "unity_scriptable_object"):
                    if left.get(label) != right.get(label):
                        issue("type_shape_mismatch", owner, label)
                # Serializable/interface/explicit-layout bits affect support and
                # storage. Visibility and BeforeFieldInit do not define fields.
                if (left.get("attributes", 0) & 0x2038) != (right.get("attributes", 0) & 0x2038):
                    issue("type_flags_mismatch", owner)
                if _attributes(left, require_string_evidence=True) != _attributes(right):
                    issue("type_attributes_mismatch", owner)
                lbase, rbase = left.get("base_type"), right.get("base_type")
                if lbase is None or rbase is None:
                    if lbase != rbase:
                        issue("base_type_mismatch", owner)
                elif type_identity(lbase) != type_identity(rbase):
                    issue("base_type_mismatch", owner)
                elif lbase["kind"] != "named":
                    issue("unsupported_generic_base", owner)
                elif (lbase["assembly"], lbase["reflection_full_name"]) not in {
                    ("mscorlib", "System.Object"), ("mscorlib", "System.ValueType"), ("mscorlib", "System.Enum")
                }:
                    visit((lbase["assembly"], lbase["reflection_full_name"]))
                if left.get("is_enum"):
                    if left.get("enum_underlying_type") is None or right.get("enum_underlying_type") is None:
                        issue("enum_values_unverified", owner, "missing enum underlying/default constants")
                    elif type_identity(left["enum_underlying_type"]) != type_identity(right["enum_underlying_type"]):
                        issue("enum_underlying_mismatch", owner)
                    lconstants = [f for f in left["fields"] if f.get("is_const")]
                    rconstants = [f for f in right["fields"] if f.get("is_const")]
                    if any(f.get("has_default_value") is not True or f.get("default_value_complete") is not True for f in lconstants + rconstants):
                        issue("enum_values_unverified", owner, "incomplete enum numeric constants")
                    else:
                        def constants(fields):
                            result = []
                            for f in fields:
                                value = f.get("default_value")
                                if type(value) is not int:
                                    raise LayoutError("Enum constant is not an exact integer")
                                result.append((f["name"], type_identity(f.get("default_value_type")), value))
                            return result
                        if constants(lconstants) != constants(rconstants):
                            issue("enum_constants_mismatch", owner)
                    return
                if as_field and not (left.get("attributes", 0) & 0x2000) and not self._unity_object(owner):
                    issue("field_type_eligibility_unverified", owner, "not a serializable data type or Unity object")
                # Unity object references store identity, not the pointed-to
                # component's serialized members. The caller itself still gets
                # its full layout compared when it is the target MonoScript.
                if as_field and self._unity_object(owner):
                    return
                if [f["name"] for f in lfields] != [f["name"] for f in rfields]:
                    issue("serialized_field_order_or_names_mismatch", owner,
                          repr([f["name"] for f in lfields]) + " != " + repr([f["name"] for f in rfields]))
                lookup = {f["name"]: f for f in rfields}
                for field in lfields:
                    other = lookup.get(field["name"])
                    if other is None:
                        continue
                    if field["attributes"] != other["attributes"] or _attributes(field, require_string_evidence=True) != _attributes(other):
                        issue("field_flags_or_attributes_mismatch", owner, field["name"])
                    if field["serialize_reference"] or other["serialize_reference"]:
                        issue("managed_reference_subtypes_unverified", owner, field["name"])
                    reference(field["field_type"], other["field_type"], owner, field["name"])
            except (LayoutError, KeyError, TypeError, ValueError, OverflowError) as error:
                issue("incomplete_or_invalid_layout", owner, str(error))
            finally:
                active.remove(owner)
                checked.add(owner)

        visit(key)
        # Sort/deduplicate diagnostics without changing field comparison order.
        return sorted({json.dumps(item, sort_keys=True): item for item in issues}.values(), key=lambda item: json.dumps(item, sort_keys=True))

    def _unity_object(self, key):
        seen = set()
        while key not in seen:
            seen.add(key)
            if key == ("UnityEngine.CoreModule", "UnityEngine.Object"):
                return True
            record = self.original.get(key)
            base = record.get("base_type") if record else None
            if not base or base.get("kind") != "named":
                return False
            key = base["assembly"], base["reflection_full_name"]
        raise LayoutError("Cyclic Unity object ancestry")


def compare_layouts(schema_report: dict, reflection_inventory: dict, *, schema_sha256=None) -> dict:
    """Compare exact upstream package MonoScripts; produce evidence, never edits."""
    for report in (schema_report, reflection_inventory):
        if report.get("schema_version") != 1 or report.get("status") != "ready":
            raise LayoutError("Unsupported or incomplete schema report")
    if reflection_inventory.get("command") != "monoscript-inventory" or reflection_inventory.get("errors"):
        raise LayoutError("Reflection inventory contains errors or is not canonical")
    if schema_report.get("unity_version") != reflection_inventory.get("unity_version"):
        raise LayoutError("Unity version differs between original and loaded layouts")
    if schema_sha256 is not None and reflection_inventory.get("original_schema_sha256") != schema_sha256:
        raise LayoutError("Inventory was made against a different original schema")
    comparison = _Comparison(schema_report, reflection_inventory)
    scripts = reflection_inventory.get("monoscripts")
    if not isinstance(scripts, list):
        raise LayoutError("MonoScript inventory is missing")
    grouped, bindings = {}, set()
    for script in scripts:
        key = script.get("assembly"), script.get("full_name")
        guid, local_id, path = script.get("guid"), script.get("file_id"), script.get("path")
        if not isinstance(path, str) or not path.startswith("Packages/") or script.get("class_resolved") is not True:
            continue
        if not isinstance(guid, str) or re.fullmatch(r"[a-f0-9]{32}", guid) is None or type(local_id) is not int or not local_id:
            raise LayoutError("Invalid MonoScript GUID/fileID identity")
        if (guid, local_id) in bindings:
            raise LayoutError("Duplicate MonoScript asset identity")
        bindings.add((guid, local_id))
        grouped.setdefault(key, []).append(script)
    candidates = []
    for key, sources in sorted(grouped.items()):
        original = comparison.original.get(key)
        if original is None or not (original.get("unity_component") or original.get("unity_scriptable_object")):
            continue
        issues = comparison.layout(key)
        if any(not isinstance(s.get("source_sha256"), str) or re.fullmatch(r"[a-f0-9]{64}", s["source_sha256"]) is None for s in sources):
            issues.append({"reason": "script_source_fingerprint_missing", "type": list(key), "detail": "source file could not be fingerprinted"})
        if len(sources) != 1:
            issues.append({"reason": "ambiguous_monoscript_identity", "type": list(key), "detail": "multiple asset identities expose the exact same CLR type"})
        candidates.append({"assembly": key[0], "full_name": key[1], "original_token": original.get("token"),
                           "loaded_scripts": sources, "status": "compatible_layout" if not issues else "blocked",
                           "issues": issues, "remap_eligible": False})
    compatible = sum(c["status"] == "compatible_layout" for c in candidates)
    return {"schema_version": 1, "command": "compare-monoscript-layouts", "status": "compared",
            "unity_version": schema_report["unity_version"], "original_schema_sha256": schema_sha256,
            "original_type_count": len(comparison.original), "loaded_type_count": len(comparison.loaded),
            "candidate_count": len(candidates), "compatible_layout_count": compatible,
            "blocked_count": len(candidates) - compatible, "candidates": candidates,
            "references_modified": False, "managed_semantics_verified": False,
            "limitations": ["Declaration compatibility does not prove method behavior or original MonoScript GUID identity.",
                            "Unsupported generic, managed-reference and unverified enum layouts remain blocked."]}


def compare_layout_files(schema_path: Path, inventory_path: Path, output_path: Path, *, repo_root=None) -> dict:
    """Atomically write a comparison outside immutable input/ and Unity assets."""
    schema_path, inventory_path, output_path = map(Path, (schema_path, inventory_path, output_path))
    if output_path.resolve() in (schema_path.resolve(), inventory_path.resolve()):
        raise LayoutError("Comparison output must not overwrite its evidence inputs")
    if output_path.is_symlink() or any(parent.is_symlink() for parent in output_path.parents):
        raise LayoutError("Refusing a symlinked comparison destination")
    if "input" in output_path.parts or "Assets" in output_path.parts or "Packages" in output_path.parts:
        raise LayoutError("Comparison output must be outside supplied input and Unity sources")
    raw = schema_path.read_bytes()
    inventory_raw = inventory_path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    inventory = json.loads(inventory_raw)
    if repo_root is not None:
        from .verification import artifact_fingerprint
        if inventory.get("source_fingerprint") != artifact_fingerprint(repo_root):
            raise LayoutError("Loaded declaration inventory is stale for the maintained source")
    report = compare_layouts(json.loads(raw), inventory, schema_sha256=digest)
    report["source_fingerprint"] = inventory.get("source_fingerprint")
    report["source_freshness_verified"] = repo_root is not None
    report["inventory_sha256"] = hashlib.sha256(inventory_raw).hexdigest()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    import tempfile
    descriptor, temporary = tempfile.mkstemp(prefix=output_path.name + ".", suffix=".tmp", dir=str(output_path.parent))
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump(report, stream, indent=2, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, output_path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
    return report


def run_layout_inventory(repo_root: Path, work_dir: Path, schema_path=None) -> dict:
    """Run the matching Editor and compare fresh declarations without remapping.

    Each invocation owns fresh staging. Failure leaves previous successful
    inventory/comparison reports intact, and retains the invocation's log.
    """
    from .verification import find_editor
    root = Path(repo_root).resolve()
    work = validate_work_dir(work_dir)
    if schema_path is None:
        pointer = managed_path(work, "native-recovery", "latest-schemas.json")
        summary = json.loads(pointer.read_bytes())
        if not isinstance(summary, dict) or summary.get("schema_version") != 1 or summary.get("command") != "schemas" or summary.get("status") != "ready":
            raise LayoutError("No complete generated native schema is available")
        selected = summary.get("evidence_report")
        if not isinstance(selected, str) or not Path(selected).is_absolute():
            raise LayoutError("Latest schema has no absolute evidence report identity")
        schema_path = Path(selected)
    else:
        schema_path = Path(schema_path).absolute()
        summary = None
    try:
        relative = schema_path.relative_to(CACHE_ROOT)
    except ValueError as error:
        raise LayoutError("Schema evidence must remain inside the generated recovery cache") from error
    schema_path = managed_path(CACHE_ROOT, *relative.parts)
    raw = schema_path.read_bytes()
    original = json.loads(raw)
    if not isinstance(original, dict) or original.get("schema_version") != 1 or original.get("command") != "schemas" or original.get("status") != "ready":
        raise LayoutError("Original schema is incomplete or unsupported")
    if summary is not None:
        source = summary.get("source", {})
        for summary_key, report_key in (("metadata_sha256", "metadata_sha256"), ("analysis_binary_sha256", "native_binary_sha256"), ("unity_version", "unity_version")):
            if not source.get(summary_key) or source.get(summary_key) != original.get(report_key):
                raise LayoutError("Latest schema evidence differs from its generated source identity")
    digest = hashlib.sha256(raw).hexdigest()
    stage = managed_path(work, "reports", "layout-runs", uuid.uuid4().hex)
    stage.mkdir(parents=True, exist_ok=False)
    inventory = managed_path(work, *stage.relative_to(work).parts, "inventory.json")
    comparison = managed_path(work, *stage.relative_to(work).parts, "comparison.json")
    log = managed_path(work, *stage.relative_to(work).parts, "editor.log")
    environment = os.environ.copy()
    environment["LUCID_LAYOUT_SCHEMA_PATH"] = str(schema_path)
    environment["LUCID_LAYOUT_INVENTORY_PATH"] = str(inventory)
    command = [str(find_editor()), "-batchmode", "-quit", "-projectPath", str(root),
               "-executeMethod", "ProjectLucid.Editor.MonoScriptLayoutInventory.Run", "-logFile", str(log)]
    result = {"schema_version": 1, "command": "layout-inventory", "status": "failed", "log": str(log),
              "schema_path": str(schema_path), "original_schema_sha256": digest, "inventory_path": str(inventory),
              "comparison_path": str(comparison), "references_modified": False}
    try:
        process = subprocess.run(command, env=environment, timeout=600)
        result["exit_code"] = process.returncode
        if process.returncode or not inventory.is_file():
            raise LayoutError("Unity did not produce a complete loaded declaration inventory")
        if hashlib.sha256(schema_path.read_bytes()).hexdigest() != digest:
            raise LayoutError("Original schema changed during the Editor inventory")
        report = compare_layout_files(schema_path, inventory, comparison, repo_root=root)
        result.update(status="compared", candidate_count=report["candidate_count"],
                      compatible_layout_count=report["compatible_layout_count"], blocked_count=report["blocked_count"],
                      source_fingerprint=report["source_fingerprint"], source_freshness_verified=True)
        write_json(managed_path(work, "reports", "latest-layout-inventory.json"), result)
    except (LayoutError, subprocess.TimeoutExpired, OSError, ValueError) as error:
        result["errors"] = [str(error)]
    write_json(managed_path(work, *stage.relative_to(work).parts, "report.json"), result)
    return result

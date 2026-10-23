"""Strict, read-only comparisons of original and maintained MonoScript layouts.

This module never edits references or imports generated declarations. A matching
layout is evidence for a later binding decision, not recovered method behavior.
"""

import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import subprocess
from typing import Any, Dict, Tuple
import uuid

from .bootstrap import CACHE_ROOT, exclusive_lock, managed_path, open_coordinator_lock, validate_work_dir, write_json


class LayoutError(ValueError):
    """A report cannot establish an unambiguous layout."""


_PRIMITIVES = frozenset((
    "System.Boolean", "System.Byte", "System.SByte", "System.Char", "System.Int16", "System.UInt16",
    "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Single", "System.Double", "System.String"))


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


def _generic_parameters(record: dict) -> Tuple[Any, ...]:
    """Compare generic parameters by declaration, not by metadata tokens."""
    parameters = record.get("generic_parameters")
    if not isinstance(parameters, list):
        raise LayoutError("Generic parameter inventory is missing")
    result = []
    for parameter in parameters:
        if (not isinstance(parameter, dict) or parameter.get("constraints_complete") is not True or
                not isinstance(parameter.get("constraints"), list)):
            raise LayoutError("Generic parameter declaration is incomplete")
        result.append((parameter.get("index"), parameter.get("name"), parameter.get("attributes"),
                       tuple(type_identity(c) for c in parameter["constraints"])))
    return tuple(result)


# Unity 2022 serializes nested inline data at most ten levels deep. A closed
# generic that keeps growing (Node<T> { Node<List<T>> next; }) never reaches a
# checked closure, so deeper generic data is blocked instead of followed.
_GENERIC_DATA_DEPTH = 10


def _engine_module(assembly: str) -> bool:
    """The Unity engine's own module assemblies, not packages such as UnityEngine.UI."""
    return assembly == "UnityEngine" or (assembly.startswith("UnityEngine.") and assembly.endswith("Module"))


def _generic_data_definition(reference: dict) -> bool:
    """A closed generic type whose definition is ordinary script code.

    Runtime-library generics carry [Serializable] (Dictionary, KeyValuePair,
    Nullable) yet Unity 2022 never serializes them, and engine generics such
    as UnityEvent<T> have engine-side rules; neither is admitted here.
    """
    assembly, definition = reference["assembly"], reference["definition"]
    return not (assembly in ("mscorlib", "netstandard") or assembly.startswith(("System", "Mono.", "UnityEditor")) or
                _engine_module(assembly) or definition.startswith(("System.", "UnityEngine.", "UnityEditor.")))


def _substitute(reference: dict, arguments) -> dict:
    """Close a declaration's type-parameter references with one instance's arguments.

    Only class type parameters are replaced. A class parameter index outside the
    instance's arity raises (an incomplete layout, never a guess); a method
    parameter is returned unchanged, and the field comparison then blocks it as
    an unsupported serialized type.
    """
    if arguments is None or not isinstance(reference, dict):
        return reference
    kind = reference.get("kind")
    if kind == "generic_parameter" and reference.get("parameter_owner") == "type":
        index = reference.get("parameter_index")
        if type(index) is not int or not 0 <= index < len(arguments):
            raise LayoutError("Generic parameter lies outside its instance arguments")
        return arguments[index]
    if kind == "generic_instance" and isinstance(reference.get("arguments"), list):
        return dict(reference, arguments=[_substitute(a, arguments) for a in reference["arguments"]])
    if kind in ("array", "pointer", "by_reference"):
        return dict(reference, element=_substitute(reference.get("element"), arguments))
    return reference


def _unshipped_constants(original, loaded):
    """Our extra enum constants when the original's are an ordered subsequence; else None.

    Every original (name, type, value) must appear on our side with the same
    value and in the original's relative order; our side may only add. Names
    are unique on each side (_candidate_fields), so a changed shipped constant
    can only be skipped as an extra, and then the original is not exhausted.
    """
    position, extras = 0, []
    for constant in loaded:
        if position < len(original) and constant == original[position]:
            position += 1
        else:
            extras.append(constant[0])
    return extras if position == len(original) and extras else None


class _Comparison:
    def __init__(self, original: dict, loaded: dict):
        self.original = _index(original)
        self.loaded = _index(loaded)
        self.unity_versions = original.get("unity_version"), loaded.get("unity_version")

    @staticmethod
    def _is_delegate(reference, index):
        """Prove delegate ancestry; names/generic arguments alone are insufficient."""
        seen = set()
        while reference and reference.get("kind") in ("named", "generic_instance"):
            type_identity(reference)
            name = reference.get("reflection_full_name") if reference["kind"] == "named" else reference["definition"]
            key = reference["assembly"], name
            if key in seen:
                raise LayoutError("Cyclic delegate ancestry")
            seen.add(key)
            record = index.get(key)
            if record is None or record.get("schema_complete") is not True:
                return False
            if key == ("mscorlib", "System.Delegate"):
                return True
            reference = record.get("base_type")
        return False

    def layout(self, key: Tuple[str, str], notes=None) -> list:
        """Return the sorted blocking issues; append informational notes to ``notes``."""
        issues, active, checked = [], set(), set()
        found = []

        def issue(reason, owner, detail=""):
            issues.append({"reason": reason, "type": list(owner), "detail": detail})

        def note(reason, owner, detail=""):
            found.append({"reason": reason, "type": list(owner), "detail": detail})

        def reference(left, right, owner, detail, container_depth=0):
            if type_identity(left) != type_identity(right):
                issue("type_identity_mismatch", owner, detail)
                return
            kind = left["kind"]
            if kind == "array":
                if container_depth:
                    issue("unsupported_nested_serialized_container", owner, detail)
                if left["rank"] != 1 or not left["vector_array"]:
                    issue("unsupported_serialized_array", owner, detail)
                reference(left["element"], right["element"], owner, detail, container_depth + 1)
            elif kind == "generic_instance":
                is_list = left["assembly"] == "mscorlib" and left["definition"] == "System.Collections.Generic.List`1"
                if not is_list and _generic_data_definition(left):
                    # Unity 2022 serializes a closed generic class or struct as
                    # inline data: the definition's own fields, closed with the
                    # instance's arguments. Its type identity, arguments included,
                    # already matched above; an argument no field uses stores nothing.
                    if sum(1 for _, as_data, closure in active if as_data and closure is not None) >= _GENERIC_DATA_DEPTH:
                        issue("serialized_generic_depth_exceeded", owner, detail)
                        return
                    visit((left["assembly"], left["definition"]), as_field=True,
                          arguments=(tuple(left["arguments"]), tuple(right["arguments"])))
                    return
                # Unity 2022 permits List<T>. Runtime-library and engine generics
                # (Dictionary, HashSet, Nullable, UnityEvent<T>) stay blocked.
                if not is_list or len(left["arguments"]) != 1:
                    issue("unsupported_serialized_generic", owner, detail)
                if is_list and container_depth:
                    issue("unsupported_nested_serialized_container", owner, detail)
                for l_arg, r_arg in zip(left["arguments"], right["arguments"]):
                    reference(l_arg, r_arg, owner, detail, container_depth + 1)
            elif kind in ("generic_parameter", "pointer", "by_reference"):
                issue("unsupported_serialized_type", owner, detail)
            elif kind == "named":
                dependency = left["assembly"], left["reflection_full_name"]
                if dependency[0] == "mscorlib" and dependency[1] in _PRIMITIVES:
                    return
                visit(dependency, as_field=True)

        def visit(owner, as_field=False, arguments=None):
            # A pointer dependency and the owner's complete MonoScript layout
            # are separate obligations, even when both occur in one graph.
            # A generic definition reached as a closed base carries each side's
            # instance arguments; every distinct closure is its own obligation.
            closure = None if arguments is None else tuple(type_identity(a) for a in arguments[0])
            role = owner, as_field, closure
            if role in checked or role in active:
                return
            left, right = self.original.get(owner), self.loaded.get(owner)
            if left is None or right is None:
                issue("missing_layout_dependency", owner)
                return
            active.add(role)
            try:
                largs, rargs = (None, None) if arguments is None else arguments
                lfields = _candidate_fields(left, require_string_evidence=True)
                rfields = _candidate_fields(right)
                if arguments is not None and not (
                        len(left.get("generic_parameters") or ()) == len(largs) == len(rargs) ==
                        len(right.get("generic_parameters") or ()) > 0):
                    issue("generic_base_arity_mismatch", owner)
                    return
                ltype = lambda f: _substitute(f["field_type"], largs)
                rtype = lambda f: _substitute(f["field_type"], rargs)
                # Unity2022 WillUnitySerialize excludes types assignable to
                # System.Delegate, including public/SerializeReference fields.
                # Keep coarse field-visibility evidence intact. Exclude only a
                # matching field proven delegate on BOTH complete type graphs;
                # changed types/attributes/visibility still block compatibility.
                rlookup = {f["name"]: f for f in rfields}
                excluded_fields = set()
                for field in lfields:
                    other = rlookup.get(field["name"])
                    if not other or not self._is_delegate(ltype(field), self.original):
                        continue
                    if (type_identity(ltype(field)) == type_identity(rtype(other)) and
                            self._is_delegate(rtype(other), self.loaded) and
                            field["attributes"] == other["attributes"] and
                            _attributes(field, require_string_evidence=True) == _attributes(other)):
                        excluded_fields.add(field["name"])
                # Unity2022 also skips a field whose declared type is a plain
                # class without [Serializable] (TMP_Settings.m_linebreakingRules
                # is absent from the shipped asset). Exclude only an identical
                # field whose type is proven such a class on BOTH graphs.
                for field in lfields:
                    other = rlookup.get(field["name"])
                    if (other and not field["serialize_reference"] and not other["serialize_reference"] and
                            field["attributes"] == other["attributes"] and
                            _attributes(field, require_string_evidence=True) == _attributes(other) and
                            self._unserialized_class(ltype(field), rtype(other))):
                        excluded_fields.add(field["name"])
                lfields = [f for f in lfields if f["name"] not in excluded_fields]
                rfields = [f for f in rfields if f["name"] not in excluded_fields]
                for label in ("is_value_type", "is_enum", "is_abstract", "declaring_type", "generic_parameters", "unity_component", "unity_scriptable_object"):
                    if label == "generic_parameters" and arguments is not None:
                        # Constraint records carry each build's own tokens.
                        if _generic_parameters(left) != _generic_parameters(right):
                            issue("type_shape_mismatch", owner, label)
                    elif left.get(label) != right.get(label):
                        issue("type_shape_mismatch", owner, label)
                # Serializable/interface/explicit-layout bits affect support and
                # storage. Visibility and BeforeFieldInit do not define fields.
                if (left.get("attributes", 0) & 0x2038) != (right.get("attributes", 0) & 0x2038):
                    issue("type_flags_mismatch", owner)
                if _attributes(left, require_string_evidence=True) != _attributes(right):
                    issue("type_attributes_mismatch", owner)
                if as_field and owner == ("UnityEngine.CoreModule", "UnityEngine.Vector2"):
                    self._vector2_data_contract(owner)
                # PPtr storage ends at the reference boundary. Prove both
                # complete, closed, scoped ancestry graphs before traversing
                # an inherited component's payload as inline serialized data.
                arity = 0 if arguments is None else len(largs)
                if as_field and not left.get("is_enum") and not left.get("is_value_type") and self._unity_object(owner, arity):
                    return
                lbase, rbase = _substitute(left.get("base_type"), largs), _substitute(right.get("base_type"), rargs)
                if lbase is None or rbase is None:
                    if lbase != rbase:
                        issue("base_type_mismatch", owner)
                elif type_identity(lbase) != type_identity(rbase):
                    issue("base_type_mismatch", owner)
                elif lbase["kind"] == "generic_instance":
                    # Unity2022 serializes a closed generic base's instance
                    # fields in place, ahead of the derived type's own fields.
                    visit((lbase["assembly"], lbase["definition"]),
                          arguments=(tuple(lbase["arguments"]), tuple(rbase["arguments"])))
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
                    elif type_identity(left["enum_underlying_type"]) not in {
                        ("named", "mscorlib", "System." + name)
                        for name in ("Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32")
                    }:
                        # Unity 2022 supports enum storage up to 32 bits, even
                        # when a matching 64-bit enum's current values are small.
                        issue("unsupported_serialized_enum_underlying_type", owner)
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
                        lvalues, rvalues = constants(lconstants), constants(rconstants)
                        if lvalues != rvalues:
                            extras = _unshipped_constants(lvalues, rvalues) if _engine_module(owner[0]) else None
                            if extras is None:
                                issue("enum_constants_mismatch", owner)
                            else:
                                # The Editor's engine module can declare
                                # constants the shipped player schema lacks
                                # (RuntimePlatform's obsolete BB10Player = 22). Storage is the
                                # underlying integer, so every shipped value
                                # still reads back unchanged.
                                note("enum_unshipped_constants", owner, repr(extras))
                    return
                if (as_field and not (left.get("attributes", 0) & 0x2000) and
                        not self._unity_2022_builtin_value(owner) and not self._unity_object(owner, arity)):
                    issue("field_type_eligibility_unverified", owner, "not a serializable data type or Unity object")
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
                    reference(ltype(field), rtype(other), owner, field["name"])
            except (LayoutError, KeyError, TypeError, ValueError, OverflowError) as error:
                issue("incomplete_or_invalid_layout", owner, str(error))
            finally:
                active.remove(role)
                checked.add(role)

        visit(key)
        # Sort/deduplicate diagnostics without changing field comparison order.
        unique = lambda items: sorted({json.dumps(item, sort_keys=True): item for item in items}.values(),
                                      key=lambda item: json.dumps(item, sort_keys=True))
        if notes is not None:
            notes.extend(unique(found))
        return unique(issues)

    def _vector2_data_contract(self, key):
        """Prove the complete literal Vector2 declaration on both graphs.

        This proves a non-PPtr value type, not serializer eligibility. Unity
        2022's explicit builtin rule is a separate obligation below. Preserve
        all raw declaration evidence, including its absent Serializable bit.
        Unknown structs and lookalike assembly scopes retain their old holds.
        """
        if key != ("UnityEngine.CoreModule", "UnityEngine.Vector2"):
            return False
        left, right = self.original.get(key), self.loaded.get(key)
        if left is None or right is None:
            raise LayoutError("Missing dual Vector2 declaration")
        static_names = ("zeroVector", "oneVector", "upVector", "downVector",
                        "leftVector", "rightVector", "positiveInfinityVector", "negativeInfinityVector")
        names = ("x", "y") + static_names + ("kEpsilon", "kEpsilonNormalSqrt")
        single = ("named", "mscorlib", "System.Single")
        vector = ("named",) + key
        expected_attributes = (
            ("UnityEngine.SharedInternalsModule", "UnityEngine.NativeClassAttribute",
             (("primitive", "IL2CPP_TYPE_STRING", "Vector2f"),), ()),
            ("UnityEngine.CoreModule", "Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstructionAttribute", (), ()),
            ("UnityEngine.SharedInternalsModule", "UnityEngine.Scripting.RequiredByNativeCodeAttribute", (),
             (("properties", "Optional", ("primitive", "IL2CPP_TYPE_BOOLEAN", True)),
              ("properties", "GenerateProxy", ("primitive", "IL2CPP_TYPE_BOOLEAN", True)))),
            ("mscorlib", "System.Reflection.DefaultMemberAttribute",
             (("primitive", "IL2CPP_TYPE_STRING", "Item"),), ()))
        for record, original in ((left, True), (right, False)):
            if (record.get("assembly"), record.get("full_name")) != key:
                raise LayoutError("Vector2 declaration identity differs")
            if (record.get("namespace"), record.get("name")) != ("UnityEngine", "Vector2"):
                raise LayoutError("Vector2 declaration name/namespace differs")
            # 0x100109 is the actual shipping/installed flag value. In particular
            # it does not contain Serializable (0x2000); do not project that bit.
            if type(record.get("attributes")) is not int or record["attributes"] != 0x100109:
                raise LayoutError("Vector2 declaration flags differ")
            if (record.get("is_value_type") is not True or record.get("is_enum") is not False or
                    record.get("is_abstract") is not False or "declaring_type" not in record or record["declaring_type"] is not None or
                    record.get("unity_component") is not False or record.get("unity_scriptable_object") is not False or
                    record.get("generic_parameters") != [] or "enum_underlying_type" not in record or record["enum_underlying_type"] is not None):
                raise LayoutError("Vector2 declaration shape differs")
            if type_identity(record.get("base_type")) != ("named", "mscorlib", "System.ValueType"):
                raise LayoutError("Vector2 declaration has an invalid scoped ValueType base")
            candidates = _candidate_fields(record, require_string_evidence=original)
            # Type and named CAD order is explicit here. The shared general CAD
            # comparator remains unchanged; every physical/raw CAD stays stored.
            ordered_attributes = []
            for attribute in record["custom_attributes"]:
                normalized = _attributes(dict(record, custom_attributes=[attribute]), require_string_evidence=original)
                if len(normalized) != 1:
                    raise LayoutError("Vector2 attribute is not the observed complete CAD")
                for group in ("fields", "properties"):
                    for value in attribute[group]:
                        if (value["value"].get("type") == "IL2CPP_TYPE_BOOLEAN" and
                                type(value["value"].get("value")) is not bool):
                            raise LayoutError("Vector2 named Boolean attribute is not an exact Boolean")
                ordered_named = tuple((group, value["name"], _attribute_value(value["value"], require_string_evidence=original))
                                      for group in ("fields", "properties") for value in attribute[group])
                ordered_attributes.append(normalized[0][:3] + (ordered_named,))
            if tuple(ordered_attributes) != expected_attributes:
                raise LayoutError("Vector2 ordered type attributes differ")
            fields = record["fields"]
            if tuple(field["name"] for field in fields) != names or [field["name"] for field in candidates] != ["x", "y"]:
                raise LayoutError("Vector2 complete declared field order differs")
            for index, field in enumerate(fields):
                expected_flags = 6 if index < 2 else 49 if index < 10 else 32854
                expected_type = vector if 2 <= index < 10 else single
                if field["attributes"] != expected_flags or type_identity(field["field_type"]) != expected_type:
                    raise LayoutError("Vector2 declared field flags/type differ: " + field["name"])
                if field["custom_attributes"] != [] or field["serialize_reference"] is not False:
                    raise LayoutError("Vector2 declared field attributes differ: " + field["name"])
                constant = index >= 10
                if field.get("has_default_value") is not constant or field.get("default_value_complete") is not True:
                    raise LayoutError("Vector2 declared field default evidence is incomplete: " + field["name"])
                if constant:
                    value = 1e-5 if index == 10 else 1e-15
                    if (type(field.get("default_value")) is not float or field["default_value"] != value or
                            type_identity(field.get("default_value_type")) != single):
                        raise LayoutError("Vector2 constant differs: " + field["name"])
                elif ("default_value" not in field or "default_value_type" not in field or
                      field["default_value"] is not None or field["default_value_type"] is not None):
                    raise LayoutError("Vector2 nonconstant has an unexpected default: " + field["name"])
        return True

    def _unity_2022_builtin_value(self, key):
        """Apply only Unity 2022.3.54f1's observed explicit Vector2 rule.

        UnityEngineTypePredicates lists Vector2 under types that should have
        had Serializable; UnitySerializationLogic separately uses that list
        for inline value eligibility. Do not infer eligibility for other structs.
        This predicate never alters fields, flags, references or input records.
        """
        return self.unity_versions == ("2022.3.54f1", "2022.3.54f1") and self._vector2_data_contract(key)

    def _unserialized_class(self, left, right):
        """Prove a field type is a plain class Unity2022 never serializes.

        Both graphs must declare the same closed named reference class without
        the Serializable bit, interface bit or generic parameters, outside the
        Unity object ancestry. Structs, enums, primitives, containers and
        anything unresolved keep their existing checks. Engine and runtime
        library classes are never proven this way: the engine serializes some
        of them natively without the bit (AnimationCurve, Gradient, GUIStyle).
        """
        if type_identity(left) != type_identity(right) or left["kind"] != "named":
            return False
        key = left["assembly"], left["reflection_full_name"]
        if (key[0] in ("mscorlib", "netstandard", "UnityEngine") or key[0].startswith(("System", "UnityEngine.", "Mono.")) or
                key[1].startswith(("System.", "UnityEngine.", "UnityEditor.", "Unity."))):
            return False
        for index in (self.original, self.loaded):
            record = index.get(key)
            # A direct System.Object base keeps any Unity object ancestry, real
            # or lookalike, from being mistaken for skipped inline data.
            if (record is None or record.get("schema_complete") is not True or record.get("is_value_type") is not False or
                    record.get("is_enum") is not False or type(record.get("attributes")) is not int or
                    record["attributes"] & 0x2020 or record.get("generic_parameters") != [] or
                    record.get("unity_component") is not False or record.get("unity_scriptable_object") is not False or
                    not isinstance(record.get("base_type"), dict) or
                    type_identity(record["base_type"]) != ("named", "mscorlib", "System.Object")):
                return False
        return True

    def _unity_object(self, key, arity=0):
        """Prove a closed PPtr ancestry on BOTH complete declaration graphs.

        Raw fields, CAD, constraints and physical tokens remain in each report.
        A named reference stores identity; payload eligibility belongs to the
        pointed-to MonoScript's separate full layout comparison.
        """
        # A literal, complete Vector2 value declaration is outside PPtr.
        # This initial-key check does not run while proving Object ancestry;
        # a malformed reference class inheriting Vector2 remains rejected.
        if self._vector2_data_contract(key):
            return False
        seen = set()
        # Only a declaration reached through a closed generic instance (a base,
        # or a closed generic field type with ``arity`` arguments) may be
        # generic, and then with exactly that instance's arity on both graphs.
        while key not in {
            ("mscorlib", "System.Object"), ("mscorlib", "System.ValueType"), ("mscorlib", "System.Enum")
        }:
            if key in seen:
                raise LayoutError("Cyclic Unity object ancestry")
            seen.add(key)
            left, right = self.original.get(key), self.loaded.get(key)
            if left is None or right is None:
                raise LayoutError("Missing dual Unity object ancestry declaration: " + repr(key))
            for record, original in ((left, True), (right, False)):
                if (record.get("assembly"), record.get("full_name")) != key:
                    raise LayoutError("Unity object ancestry identity does not match its record")
                _candidate_fields(record, require_string_evidence=original)
                if type(record.get("attributes")) is not int or not isinstance(record.get("generic_parameters"), list):
                    raise LayoutError("Incomplete Unity object ancestry type contract")
                if len(record["generic_parameters"]) != arity:
                    raise LayoutError("Open generic Unity object ancestry is unsupported")
                for label in ("is_value_type", "is_enum", "is_abstract", "unity_component", "unity_scriptable_object"):
                    if type(record.get(label)) is not bool:
                        raise LayoutError("Incomplete Unity object ancestry shape: " + label)
                if record["is_value_type"] or record["is_enum"] or record["attributes"] & 0x20:
                    raise LayoutError("Unity object ancestry is not a reference class")
            for label in ("is_abstract", "declaring_type", "unity_component", "unity_scriptable_object"):
                if left.get(label) != right.get(label):
                    raise LayoutError("Unity object ancestry shape differs: " + label)
            if (left["attributes"] & 0x2038) != (right["attributes"] & 0x2038):
                raise LayoutError("Unity object ancestry type flags differ")
            if _attributes(left, require_string_evidence=True) != _attributes(right):
                raise LayoutError("Unity object ancestry attributes differ")
            lbase, rbase = left.get("base_type"), right.get("base_type")
            if lbase is None or rbase is None:
                if lbase != rbase:
                    raise LayoutError("Unity object ancestry base differs")
                if key == ("UnityEngine.CoreModule", "UnityEngine.Object"):
                    raise LayoutError("Unity object root lacks its complete System.Object base")
                return False
            if type_identity(lbase) != type_identity(rbase):
                raise LayoutError("Unity object ancestry base identity differs")
            if lbase["kind"] == "generic_instance":
                # The PPtr boundary needs only the ancestry, not the payload,
                # so the definition's arguments are not substituted here.
                next_key, arity = (lbase["assembly"], lbase["definition"]), len(lbase["arguments"])
                if key == ("UnityEngine.CoreModule", "UnityEngine.Object"):
                    raise LayoutError("Unity object root has an invalid base")
                key = next_key
                continue
            if lbase["kind"] != "named":
                raise LayoutError("Unity object ancestry base is not a closed named type")
            arity = 0
            next_key = lbase["assembly"], lbase["reflection_full_name"]
            if key == ("UnityEngine.CoreModule", "UnityEngine.Object"):
                if next_key != ("mscorlib", "System.Object"):
                    raise LayoutError("Unity object root has an invalid base")
                return True
            key = next_key
        return False


def compare_layouts(schema_report: dict, reflection_inventory: dict, *, schema_sha256=None) -> dict:
    """Compare maintained game and package MonoScripts; produce evidence only."""
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
        if not _maintained_script_path(path) or script.get("class_resolved") is not True:
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
        notes = []
        issues = comparison.layout(key, notes)
        if any(not isinstance(s.get("source_sha256"), str) or re.fullmatch(r"[a-f0-9]{64}", s["source_sha256"]) is None for s in sources):
            issues.append({"reason": "script_source_fingerprint_missing", "type": list(key), "detail": "source file could not be fingerprinted"})
        if len(sources) != 1:
            issues.append({"reason": "ambiguous_monoscript_identity", "type": list(key), "detail": "multiple asset identities expose the exact same CLR type"})
        candidates.append({"assembly": key[0], "full_name": key[1], "original_token": original.get("token"),
                           "loaded_scripts": sources, "status": "compatible_layout" if not issues else "blocked",
                           "issues": issues, "notes": notes, "remap_eligible": False})
    compatible = sum(c["status"] == "compatible_layout" for c in candidates)
    return {"schema_version": 1, "command": "compare-monoscript-layouts", "status": "compared",
            "unity_version": schema_report["unity_version"], "original_schema_sha256": schema_sha256,
            "original_type_count": len(comparison.original), "loaded_type_count": len(comparison.loaded),
            "candidate_count": len(candidates), "compatible_layout_count": compatible,
            "blocked_count": len(candidates) - compatible, "candidates": candidates,
            "references_modified": False, "managed_semantics_verified": False,
            "limitations": ["Declaration compatibility does not prove method behavior or original MonoScript GUID identity.",
                            "Unsupported generic, managed-reference and unverified enum layouts remain blocked."]}


def _maintained_script_path(path):
    """Exclude generated exports and noncanonical paths from binding evidence."""
    if not isinstance(path, str) or "\\" in path:
        return False
    parsed = PurePosixPath(path)
    if parsed.is_absolute() or str(parsed) != path or any(part in (".", "..") for part in parsed.parts):
        return False
    if path.startswith("Assets/Scripts/"):
        return path.endswith(".cs")
    return path.startswith("Packages/") and len(parsed.parts) >= 3 and parsed.suffix in (".cs", ".dll")


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


def _prune_layout_runs(work: Path, current: Path) -> None:
    """Drop stale layout-run stages, keeping the latest result's and this run's.

    Only completed (report.json present) uuid-named directories directly under
    reports/layout-runs are removed; a stage another invocation is still writing,
    and anything unreadable or unexpected, is left alone. Callers hold the
    layout-runs lock, which also covers publishing the latest pointer."""
    runs = current.parent
    keep = {current.name}
    try:
        latest = json.loads(managed_path(work, "reports", "latest-layout-inventory.json").read_bytes())
        keep.add(Path(latest["inventory_path"]).parent.name)
    except (OSError, ValueError, KeyError, TypeError):
        pass
    for stage in runs.iterdir():
        if stage.name not in keep and re.fullmatch("[0-9a-f]{32}", stage.name) and stage.is_dir() and not stage.is_symlink() \
                and (stage / "report.json").is_file():
            shutil.rmtree(stage, ignore_errors=True)


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
    except (LayoutError, subprocess.TimeoutExpired, OSError, ValueError) as error:
        result["errors"] = [str(error)]
    # Finishing the stage, moving the latest pointer and pruning are one step:
    # a concurrent invocation never prunes the stage the pointer is about to name.
    with open_coordinator_lock(stage.parent / ".prune.lock", "Layout runs lock") as lock_stream, \
            exclusive_lock(lock_stream):
        write_json(managed_path(work, *stage.relative_to(work).parts, "report.json"), result)
        if result["status"] == "compared":
            write_json(managed_path(work, "reports", "latest-layout-inventory.json"), result)
        _prune_layout_runs(work, stage)
    return result

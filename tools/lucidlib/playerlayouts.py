"""Player-plane layout evidence for package types whose Editor build differs.

URP 14.0.11 declares members only under UNITY_EDITOR: the prefiltering fields
and m_AnyShadowsSupported (#if UNITY_EDITOR), ShaderKeywordFilter attributes
(#if UNITY_EDITOR, from UnityEditor.CoreModule) and URPHelpURL
([Conditional("UNITY_EDITOR")]). The Editor's loaded declarations therefore
never equal the shipped IL2CPP schema, although the shipped player was compiled
from the same source. The genuine player compilation of the current source is
the plane the shipped build serialized against, so it is compared with the
original schema; the Editor plane is held to a separate guard, because the
Editor loads and re-serializes these assets for the build.

Read-only: nothing here edits a reference or approves a binding by itself.
"""

import copy

from . import bindings

# The player compiles against Unity's netstandard reference assembly; the
# IL2CPP player links the same definitions from mscorlib or System, where the
# shipped schema records them. Each name has its own endpoint; any other
# netstandard type fails, so this is never an assembly alias.
RUNTIME_LIBRARY_TYPES = {name: "mscorlib" for name in (
    "System.Attribute", "System.AttributeTargets", "System.AttributeUsageAttribute", "System.Boolean",
    "System.Collections.Generic.List`1", "System.Diagnostics.DebuggerBrowsableAttribute",
    "System.Diagnostics.DebuggerBrowsableState", "System.Enum", "System.FlagsAttribute", "System.Int32",
    "System.Int64", "System.IntPtr", "System.Object", "System.ObsoleteAttribute",
    "System.Reflection.DefaultMemberAttribute", "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
    "System.Single", "System.String", "System.UInt32", "System.ValueType")}
RUNTIME_LIBRARY_TYPES.update({name: "System" for name in (
    "System.ComponentModel.EditorBrowsableAttribute", "System.ComponentModel.EditorBrowsableState")})
_REFERENCE = "netstandard"
_ENDPOINTS = frozenset(RUNTIME_LIBRARY_TYPES.values())
# Unity 2022's serializer stores these engine structs inline although they lack
# the Serializable bit; the player schema carries the installed serializer's
# own builtin predicate for each, which must agree.
BUILTIN_STRUCTS = frozenset((("UnityEngine.CoreModule", "UnityEngine.Vector2"),
                             ("UnityEngine.CoreModule", "UnityEngine.Vector3"),
                             ("UnityEngine.CoreModule", "UnityEngine.LayerMask")))


def _endpoint(name):
    endpoint = RUNTIME_LIBRARY_TYPES.get(name)
    if endpoint is None:
        raise bindings.LayoutError("Unprojected player runtime-library type: " + str(name))
    return endpoint


def _project(value, owner=None):
    """Project one record; ``owner`` is the endpoint of the enclosing projected type."""
    if isinstance(value, list):
        return [_project(item, owner) for item in value]
    if not isinstance(value, dict):
        return value
    endpoint, inner = None, owner
    if value.get("assembly") == _REFERENCE:
        kind = value.get("kind")
        if kind is None:
            # A type or attribute record names itself by full_name.
            endpoint = _endpoint(value.get("full_name"))
            if "fields" in value:
                inner = endpoint
        elif kind == "named":
            endpoint = _endpoint(value.get("reflection_full_name"))
        elif kind == "generic_instance":
            endpoint = _endpoint(value.get("definition"))
        elif kind in ("array", "pointer", "by_reference"):
            element = value.get("element")
            if not isinstance(element, dict) or element.get("assembly") != _REFERENCE:
                raise bindings.LayoutError("Player runtime-library container scope differs from its element")
            endpoint = _project(element, owner)["assembly"]
        elif kind == "generic_parameter":
            # Only the projected definition's own parameters (List`1's T).
            if owner is None:
                raise bindings.LayoutError("Unscoped player runtime-library generic parameter")
            endpoint = owner
        else:
            raise bindings.LayoutError("Unsupported player runtime-library reference kind")
    result = {key: _project(item, inner) for key, item in value.items()}
    if endpoint is not None:
        result["assembly"] = endpoint
    return result


def project_player_report(report):
    """Return a copy whose exact runtime-library types name their IL2CPP definitions."""
    if not isinstance(report, dict) or not isinstance(report.get("assemblies"), list):
        raise bindings.LayoutError("Player schema assemblies are missing")
    projected = copy.deepcopy(report)
    assemblies, endpoints = [], {}
    for assembly in report["assemblies"]:
        if assembly.get("name") in _ENDPOINTS:
            raise bindings.LayoutError("Player schema already names a runtime-library definition assembly")
        types = _project(assembly.get("types"))
        if assembly.get("name") != _REFERENCE:
            assemblies.append(dict(assembly, types=types))
            continue
        # Split the reference assembly's records by their definition endpoints.
        for record in types:
            endpoints.setdefault(record["assembly"], []).append(record)
    for name, types in sorted(endpoints.items()):
        assemblies.append({"name": name, "types": types, "projected_from": _REFERENCE})
    projected["assemblies"] = assemblies
    return projected


def _editor_guard(key, player, editor):
    """The Editor may only add members the player compile of the same source lacks.

    Every player field must exist in the Editor in the same relative order,
    with identical flags and type; every player attribute must be present too.
    Editor-only fields and attributes are those the player compile lacks:
    the only difference between the two compiles of one source is UNITY_EDITOR.
    The Editor's serialized fields, restricted to the player's names, must be
    the player's serialized fields in order.
    """
    issues, delta = [], {"fields": [], "field_attributes": [], "type_attributes": []}

    def issue(reason, detail=""):
        issues.append({"reason": reason, "type": list(key), "detail": detail})

    if editor is None:
        issue("editor_declaration_missing")
        return issues, delta
    for label in ("is_value_type", "is_enum", "is_abstract", "declaring_type", "unity_component", "unity_scriptable_object"):
        if player.get(label) != editor.get(label):
            issue("editor_type_shape_mismatch", label)
    if (player.get("attributes", 0) & 0x2038) != (editor.get("attributes", 0) & 0x2038):
        issue("editor_type_flags_mismatch")
    pbase, ebase = player.get("base_type"), editor.get("base_type")
    if (pbase is None) != (ebase is None) or (pbase is not None and bindings.type_identity(pbase) != bindings.type_identity(ebase)):
        issue("editor_base_type_mismatch")
    eattributes = list(bindings._attributes(editor))
    for attribute in bindings._attributes(player, require_string_evidence=True):
        if attribute in eattributes:
            eattributes.remove(attribute)
        else:
            issue("editor_type_attribute_missing", attribute[1])
    delta["type_attributes"] = sorted({attribute[1] for attribute in eattributes})
    pfields = player.get("fields") or []
    efields = {field.get("name"): field for field in editor.get("fields") or []}
    eorder = [field.get("name") for field in editor.get("fields") or []]
    position = -1
    for field in pfields:
        other = efields.get(field["name"])
        if other is None:
            issue("editor_field_missing", field["name"])
            continue
        index = eorder.index(field["name"])
        if index < position:
            issue("editor_field_order_mismatch", field["name"])
        position = max(position, index)
        if field["attributes"] != other["attributes"] or bindings.type_identity(field["field_type"]) != bindings.type_identity(other["field_type"]):
            issue("editor_field_shape_mismatch", field["name"])
        fattributes = list(bindings._attributes(other))
        for attribute in bindings._attributes(field, require_string_evidence=True):
            if attribute in fattributes:
                fattributes.remove(attribute)
            else:
                issue("editor_field_attribute_missing", field["name"])
        if fattributes:
            delta["field_attributes"].append([field["name"], sorted({attribute[1] for attribute in fattributes})])
    names = {field["name"] for field in pfields}
    delta["fields"] = [name for name in eorder if name not in names]
    pserialized = [field["name"] for field in bindings._candidate_fields(player, require_string_evidence=True)]
    eserialized = [field["name"] for field in bindings._candidate_fields(editor) if field["name"] in names]
    if pserialized != eserialized:
        issue("editor_serialized_fields_mismatch", repr(pserialized) + " != " + repr(eserialized))
    return issues, delta


def compare_player_plane(original, player, editor, roots):
    """Compare the original schema with the player plane, and guard the Editor plane.

    Returns one verdict per root. A root is compatible only when the player
    comparison has no issue and every closure type passes the Editor guard:
    the profile's roots share one closure, so a guard failure anywhere in it
    blocks every root (fail-closed by design).
    """
    projected = project_player_report(player)
    comparison = bindings._Comparison(original, projected, loaded_plane="player")
    if comparison.unity_versions != ("2022.3.54f1", "2022.3.54f1"):
        raise bindings.LayoutError("Player plane requires the matching Unity version on both schemas")
    editor_index = bindings._index(editor)
    guard_issues, deltas = [], {}
    for key, record in sorted(comparison.loaded.items()):
        if key in BUILTIN_STRUCTS and record.get("builtin_serializer_eligibility") is not True:
            guard_issues.append({"reason": "builtin_struct_not_serializer_eligible", "type": list(key), "detail": ""})
        if key[0] in _ENDPOINTS:
            continue
        issues, delta = _editor_guard(key, record, editor_index.get(key))
        guard_issues.extend(issues)
        if any(delta.values()):
            deltas[key[0] + ":" + key[1]] = delta
    verdicts = {}
    for key in sorted(roots):
        if key not in comparison.loaded:
            raise bindings.LayoutError("Player plane lacks a profile root: " + key[1])
        notes = []
        issues = comparison.layout(key, notes) + guard_issues
        verdicts[key] = {"status": "compatible_layout" if not issues else "blocked", "issues": issues, "notes": notes}
    return {"verdicts": verdicts, "editor_only": deltas}

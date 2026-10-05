"""Read IL2CPP metadata declarations without claiming to recover method bodies.

The supported layout is metadata v31 from the supplied Unity 2022.3 player.
Type indices into the native IL2CPP type table remain explicitly labelled: they
are not interchangeable with metadata type-definition indices.
"""

import re
import struct
from pathlib import Path


_SECTIONS = (
    "string_literals", "string_literal_data", "strings", "events",
    "properties", "methods", "parameter_defaults", "field_defaults",
    "default_data", "marshaled_sizes", "parameters", "fields",
    "generic_parameters", "generic_constraints", "generic_containers",
    "nested_types", "interfaces", "vtable_methods", "interface_offsets",
    "types", "images", "assemblies", "field_refs", "assembly_refs",
    "attribute_data", "attribute_ranges", "virtual_parameter_types",
    "virtual_parameter_ranges", "runtime_type_names", "runtime_strings",
    "exported_types",
)


def _range(start, count, total, description):
    if count == 0 and start == -1:
        return range(0)
    if start < 0 or count < 0 or start + count > total:
        raise ValueError("Invalid metadata %s range: %s + %s / %s" %
                         (description, start, count, total))
    return range(start, start + count)


def parse_metadata(path: Path) -> dict:
    """Return JSON-safe image, type, field, parameter and method inventories.

    Method IDs combine the assembly name, fully qualified declaring type and
    hexadecimal metadata token. Native type indices are retained for later
    decoding alongside GameAssembly; metadata alone cannot resolve all types.
    """
    path = Path(path)
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ValueError("Cannot read IL2CPP metadata: %s" % path) from exc
    if len(data) < 256:
        raise ValueError("Truncated IL2CPP metadata header")
    magic, version = struct.unpack_from("<II", data)
    if magic != 0xFAB11BAF:
        raise ValueError("Invalid IL2CPP metadata magic")
    if version != 31:
        raise ValueError("Unsupported IL2CPP metadata version %d; expected 31" % version)
    sections = {}
    for index, name in enumerate(_SECTIONS):
        offset, size = struct.unpack_from("<II", data, 8 + index * 8)
        if size and (offset < 256 or offset + size > len(data)):
            raise ValueError("Invalid IL2CPP metadata %s section bounds" % name)
        if not size and offset > len(data):
            raise ValueError("Invalid IL2CPP metadata %s offset" % name)
        sections[name] = {"offset": offset, "size": size}
    occupied = sorted((v["offset"], v["offset"] + v["size"], k)
                      for k, v in sections.items() if v["size"])
    for left, right in zip(occupied, occupied[1:]):
        if left[1] > right[0]:
            raise ValueError("Overlapping IL2CPP metadata sections: %s, %s" %
                             (left[2], right[2]))
    strings_section = sections["strings"]
    strings = data[strings_section["offset"]:
                   strings_section["offset"] + strings_section["size"]]
    string_cache = {}

    def string(index):
        if index in string_cache:
            return string_cache[index]
        if index < 0 or index >= len(strings):
            raise ValueError("Invalid metadata string index %d" % index)
        end = strings.find(b"\0", index)
        if end < 0:
            raise ValueError("Unterminated metadata string at %d" % index)
        try:
            value = strings[index:end].decode("utf-8")
        except UnicodeDecodeError as exc:
            raise ValueError("Invalid UTF-8 metadata string at %d" % index) from exc
        string_cache[index] = value
        return value

    def records(name, format_string):
        section = sections[name]
        record = struct.Struct(format_string)
        if section["size"] % record.size:
            raise ValueError("Invalid metadata %s table size" % name)
        return list(record.iter_unpack(data[section["offset"]:
                                            section["offset"] + section["size"]]))

    raw_images = records("images", "<10i")
    raw_types = records("types", "<16i8H2I")
    raw_methods = records("methods", "<iiiIiiI4H")
    raw_fields = records("fields", "<iiI")
    raw_parameters = records("parameters", "<iIi")
    raw_assemblies = records("assemblies", "<14iQ")
    owners = [None] * len(raw_types)
    images = []
    for index, row in enumerate(raw_images):
        assembly_index = row[1]
        if assembly_index < 0 or assembly_index >= len(raw_assemblies):
            raise ValueError("Invalid metadata image assembly index")
        assembly = raw_assemblies[assembly_index]
        if assembly[0] != index:
            raise ValueError("Metadata image/assembly indices disagree")
        indices = list(_range(row[2], row[3], len(raw_types), "image types"))
        name = string(row[0])
        for type_index in indices:
            if owners[type_index] is not None:
                raise ValueError("Metadata type belongs to multiple images")
            owners[type_index] = name
        images.append({
            "index": index, "name": name, "assembly_index": assembly_index,
            "assembly_name": string(assembly[4]), "token": "0x%08x" % (row[7] & 0xFFFFFFFF),
            "assembly_version": ".".join(str(v) for v in assembly[10:14]),
            "type_start": row[2], "type_count": row[3], "type_indices": indices,
        })
    if any(owner is None for owner in owners):
        raise ValueError("Metadata contains types without an owning image")
    byval_to_definition = {row[2]: index for index, row in enumerate(raw_types)}
    full_names = {}

    def full_name(index, visiting=None):
        if index in full_names:
            return full_names[index]
        visiting = set() if visiting is None else visiting
        if index in visiting:
            raise ValueError("Cyclic metadata declaring types")
        visiting.add(index)
        row = raw_types[index]
        name, namespace = string(row[0]), string(row[1])
        parent = byval_to_definition.get(row[3]) if row[3] >= 0 else None
        value = (full_name(parent, visiting) + "+" + name if parent is not None
                 else ((namespace + ".") if namespace else "") + name)
        visiting.remove(index)
        full_names[index] = value
        return value

    parameters = [{"index": i, "name": string(row[0]),
                   "token": "0x%08x" % row[1], "type_index": row[2]}
                  for i, row in enumerate(raw_parameters)]
    fields = [{"index": i, "name": string(row[0]), "type_index": row[1],
               "token": "0x%08x" % row[2]}
              for i, row in enumerate(raw_fields)]
    methods = []
    for index, row in enumerate(raw_methods):
        declaring = row[1]
        if declaring < 0 or declaring >= len(raw_types):
            raise ValueError("Invalid metadata method declaring type index")
        parameter_indices = list(_range(row[4], row[10], len(parameters), "method parameters"))
        token = "0x%08x" % row[6]
        methods.append({
            "index": index, "id": "%s:%s:%s" % (owners[declaring], full_name(declaring), token),
            "name": string(row[0]), "assembly": owners[declaring],
            "declaring_type_index": declaring, "declaring_type": full_name(declaring),
            "return_type_index": row[2], "return_parameter_token": "0x%08x" % row[3],
            "parameter_start": row[4], "parameter_count": row[10],
            "parameter_indices": parameter_indices, "generic_container_index": row[5],
            "token": token, "flags": row[7], "implementation_flags": row[8], "slot": row[9],
            "recovery_status": "declaration_only",
        })
    if len({method["id"] for method in methods}) != len(methods):
        raise ValueError("Duplicate metadata method identifiers")
    types = []
    method_owners = [None] * len(methods)
    field_owners = [None] * len(fields)
    for index, row in enumerate(raw_types):
        method_indices = list(_range(row[9], row[16], len(methods), "type methods"))
        field_indices = list(_range(row[8], row[18], len(fields), "type fields"))
        for method_index in method_indices:
            if method_owners[method_index] is not None or methods[method_index]["declaring_type_index"] != index:
                raise ValueError("Metadata method ownership disagrees")
            method_owners[method_index] = index
        for field_index in field_indices:
            if field_owners[field_index] is not None:
                raise ValueError("Metadata field belongs to multiple types")
            field_owners[field_index] = index
            fields[field_index]["declaring_type_index"] = index
        types.append({
            "index": index, "id": owners[index] + ":" + full_name(index),
            "name": string(row[0]), "namespace": string(row[1]), "full_name": full_name(index),
            "assembly": owners[index], "token": "0x%08x" % row[25],
            "byval_type_index": row[2], "declaring_type_index": row[3],
            "parent_type_index": row[4], "element_type_index": row[5],
            "generic_container_index": row[6], "flags": row[7],
            "field_start": row[8], "field_count": row[18], "field_indices": field_indices,
            "method_start": row[9], "method_count": row[16], "method_indices": method_indices,
            "property_count": row[17], "event_count": row[19], "bitfield": row[24],
        })
    if any(owner is None for owner in method_owners + field_owners):
        raise ValueError("Metadata contains methods or fields without owning types")
    for image in images:
        image["method_count"] = sum(types[i]["method_count"] for i in image["type_indices"])
        image["field_count"] = sum(types[i]["field_count"] for i in image["type_indices"])
    evidence = extract_source_evidence(data)
    return {
        "version": version, "size": len(data), "sections": sections,
        "image_count": len(images), "type_count": len(types), "method_count": len(methods),
        "field_count": len(fields), "parameter_count": len(parameters),
        "images": images, "types": types, "methods": methods,
        "fields": fields, "parameters": parameters,
        "source_paths": evidence["source_paths"], "package_evidence": evidence["package_evidence"],
    }


def extract_source_evidence(data: bytes) -> dict:
    """Recover source-path and package-version evidence embedded in metadata."""
    pattern = rb"(?:Assets|Packages|Library)[/\\][a-zA-Z0-9_. @+()/-\\]+\.cs"
    source_paths = sorted({m.group().decode("ascii").replace("\\", "/")
                           for m in re.finditer(pattern, data)})
    packages = {}
    package_pattern = rb"(com\.[a-zA-Z0-9_.-]+)@([0-9]+(?:\.[0-9]+){1,3}(?:-[a-zA-Z0-9.]+)?)"
    for match in re.finditer(package_pattern, data):
        name, version = (part.decode("ascii") for part in match.groups())
        key = (name, version)
        record = packages.setdefault(key, {"name": name, "version": version,
                                          "exact_version": len(version.split("-", 1)[0].split(".")) >= 3,
                                          "occurrences": 0, "evidence": []})
        record["occurrences"] += 1
    for path in source_paths:
        for match in re.finditer(package_pattern.decode("ascii"), path):
            key = match.groups()
            if key in packages and len(packages[key]["evidence"]) < 3:
                packages[key]["evidence"].append(path)
    return {"source_paths": source_paths,
            "package_evidence": [packages[key] for key in sorted(packages)]}

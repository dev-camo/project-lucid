"""Read-only fingerprint of an original macOS bundle, and a comparison of two fingerprints.

Nothing in the bundle is executed, loaded or modified: files are only read as bytes and
the Mach-O and IL2CPP metadata structures are parsed with struct. The comparison is
evidence for a human reviewer; it never qualifies an input variant by itself.
"""

import hashlib
import mmap
import os
import struct

from . import inputvariants

FORMAT = 3

FAT_MAGIC, FAT_MAGIC_64 = 0xCAFEBABE, 0xCAFEBABF  # stored big-endian
MH_MAGIC_64 = 0xFEEDFACF                           # stored little-endian
LC_SEGMENT_64, LC_UUID, LC_CODE_SIGNATURE, LC_ENCRYPTION_INFO_64 = 0x19, 0x1B, 0x1D, 0x2C
ZEROFILL_TYPES = (0x1, 0xC, 0x12)  # S_ZEROFILL, S_GB_ZEROFILL, S_THREAD_LOCAL_ZEROFILL
CPU_NAMES = {0x01000007: "x86_64", 0x0100000C: "arm64", 7: "i386", 12: "arm"}
CODE_SIGNATURE_DIRECTORY = "Contents/_CodeSignature/"

METADATA_SANITY = 0xFAB11BAF
# IL2CPP v31 global-metadata header: sanity, version, then (offset, size) pairs in this order.
TABLES_V31 = (
    "stringLiteral", "stringLiteralData", "string", "events", "properties", "methods",
    "parameterDefaultValues", "fieldDefaultValues", "fieldAndParameterDefaultValueData",
    "fieldMarshaledSizes", "parameters", "fields", "genericParameters", "genericParameterConstraints",
    "genericContainers", "nestedTypes", "interfaces", "vtableMethods", "interfaceOffsets",
    "typeDefinitions", "images", "assemblies", "fieldRefs", "referencedAssemblies", "attributeData",
    "attributeDataRange", "unresolvedIndirectCallParameterTypes", "unresolvedIndirectCallParameterRanges",
    "windowsRuntimeTypeNames", "windowsRuntimeStrings", "exportedTypeDefinitions")
METADATA_TABLES = {31: TABLES_V31}


def _sha256(data):
    return hashlib.sha256(data).hexdigest()


def _file_sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _cstr(raw):
    return raw.split(b"\0", 1)[0].decode("ascii", "replace")


def _raise(error):
    # os.walk swallows unreadable directories by default; a silently smaller manifest would hide files.
    raise error


def build_manifest(app):
    """Every regular file as (path, size, sha256, permission bits) and every symlink as a link, never followed."""
    files, links = [], []
    for directory, dirnames, filenames in os.walk(app, followlinks=False, onerror=_raise):
        for name in dirnames + filenames:
            full = os.path.join(directory, name)
            relative = os.path.relpath(full, app).replace(os.sep, "/")
            if os.path.islink(full):
                links.append({"path": relative, "target": os.readlink(full)})
            elif os.path.isfile(full):
                files.append({"path": relative, "size": os.path.getsize(full), "sha256": _file_sha256(full),
                              "mode": os.stat(full).st_mode & 0o7777})
    files.sort(key=lambda item: item["path"])
    links.sort(key=lambda item: item["path"])
    return {"files": files, "symlinks": links}


def _slice_ranges(data):
    """Return (header, entries): header holds magic, count, align fields and the container bytes hash.

    entries are (cputype, cpusubtype, offset, size) per architecture of a fat or thin Mach-O.
    """
    if len(data) < 8:
        raise ValueError("Mach-O file is too small to hold a header")
    big = struct.unpack_from(">I", data, 0)[0]
    if big in (FAT_MAGIC, FAT_MAGIC_64):
        wide = big == FAT_MAGIC_64
        count = struct.unpack_from(">I", data, 4)[0]
        layout = ">iiQQII" if wide else ">iiIII"
        entry = struct.calcsize(layout)
        if count > 64 or 8 + count * entry > len(data):
            raise ValueError("Fat header declares {} architectures, which does not fit the file".format(count))
        result, aligns, masked = [], [], []
        for index in range(count):
            at = 8 + index * entry
            fields = struct.unpack_from(layout, data, at)
            result.append((fields[0], fields[1], fields[2], fields[3]))
            aligns.append(fields[4])
            # The offset and size fields follow from the slice sizes and are judged by the slice comparison.
            width = 8 if wide else 4
            masked.append((at + 8, at + 8 + 2 * width))
        if len({(entry[0], entry[1]) for entry in result}) != len(result):
            raise ValueError("Fat header repeats a (cputype, cpusubtype) pair")
        for _, _, offset, size in result:
            if offset < 0 or size < 0 or offset + size > len(data):
                raise ValueError("Slice at 0x{:x} size 0x{:x} lies outside the file".format(offset, size))
            masked.append((offset, offset + size))
        # A slice is canonical when it starts at the first aligned position after the arch table (first slice)
        # or after the previous slice (in file order); only canonical offsets follow from the slice sizes.
        canonical, previous = [False] * count, 8 + count * entry
        for index in sorted(range(count), key=lambda i: result[i][2]):
            step = 1 << aligns[index] if aligns[index] < 32 else 0
            canonical[index] = bool(step) and result[index][2] == -(-previous // step) * step
            previous = result[index][2] + result[index][3]
        # Bytes outside the slices are hashed literally, except all-zero alignment padding between the arch
        # table and the last slice: its length follows from the slice sizes, so only its content counts.
        digest, cursor, table_end = hashlib.sha256(), 0, 8 + count * entry
        last_end = max([end for start, end in masked] or [0])
        for start, end in sorted(masked):
            if start > cursor:
                gap = bytes(data[cursor:start])
                zero_padding = cursor >= table_end and start <= last_end and not any(gap)
                digest.update(b"Z" if zero_padding else gap)
            cursor = max(cursor, end)
        digest.update(data[cursor:])
        return {"magic": big, "count": count, "aligns": aligns, "canonical": canonical,
                        "container_sha256": digest.hexdigest()}, result
    if struct.unpack_from("<I", data, 0)[0] == MH_MAGIC_64:
        cputype, cpusubtype = struct.unpack_from("<ii", data, 4)
        return {"magic": big, "count": 1, "aligns": [None], "canonical": [True], "container_sha256": None}, [(cputype, cpusubtype, 0, len(data))]
    raise ValueError("Unsupported Mach-O magic 0x{:08x}; expected a fat or 64-bit little-endian binary".format(big))


def _parse_slice(data, cputype, cpusubtype, offset, size, align, canonical):
    """Parse one Mach-O 64 slice: segments, sections, UUID, signature, encryption and LINKEDIT hashes."""
    if offset < 0 or size < 32 or offset + size > len(data):
        raise ValueError("Slice at 0x{:x} size 0x{:x} lies outside the file".format(offset, size))
    view = memoryview(data)[offset:offset + size]
    try:
        magic = struct.unpack_from("<I", view, 0)[0]
        if magic != MH_MAGIC_64:
            raise ValueError("Slice magic 0x{:08x} is not a 64-bit little-endian Mach-O".format(magic))
        ncmds, sizeofcmds = struct.unpack_from("<II", view, 16)
        record = {"arch": CPU_NAMES.get(cputype, "cpu_0x{:x}".format(cputype)), "cputype": cputype,
                  "cpusubtype": cpusubtype, "offset": offset, "size": size, "align": align, "canonical": canonical, "sha256": _sha256(view),
                  "uuid": None, "code_signature": None, "encryption": None, "segments": [], "linkedit": None}
        if 32 + sizeofcmds > size:
            raise ValueError("Load commands overrun the slice")
        masked = []  # byte ranges (relative to the slice) that legitimately change when a bundle is re-signed
        position = 32
        for _ in range(ncmds):
            if position + 8 > 32 + sizeofcmds:
                raise ValueError("Load command header at 0x{:x} lies outside sizeofcmds".format(position))
            cmd, cmdsize = struct.unpack_from("<II", view, position)
            if cmdsize < 8 or position + cmdsize > 32 + sizeofcmds:
                raise ValueError("Malformed load command at 0x{:x}".format(position))
            body = position + 8
            minimum = {LC_UUID: 24, LC_CODE_SIGNATURE: 16, LC_ENCRYPTION_INFO_64: 24}.get(cmd)
            if cmd == LC_SEGMENT_64:
                minimum = 72
                if cmdsize >= 72:
                    minimum += 80 * struct.unpack_from("<I", view, body + 56)[0]
            if minimum is not None and cmdsize < minimum:
                raise ValueError("Load command 0x{:x} at 0x{:x} has cmdsize {}, below the {} its payload needs".format(
                    cmd, position, cmdsize, minimum))
            if cmd == LC_SEGMENT_64:
                name, vmaddr, vmsize, fileoff, filesize, _, _, nsects, _ = struct.unpack_from("<16sQQQQiiII", view, body)
                segment = {"segname": _cstr(name), "vmaddr": vmaddr, "vmsize": vmsize, "fileoff": fileoff,
                           "filesize": filesize, "sha256": None, "sections": []}
                if filesize:
                    if fileoff + filesize > size:
                        raise ValueError("Segment {} overruns the slice".format(segment["segname"]))
                    segment["sha256"] = _sha256(view[fileoff:fileoff + filesize])
                for index in range(nsects):
                    sect, seg, addr, length, sect_off, _, _, _, flags, _, _, _ = struct.unpack_from(
                        "<16s16sQQIIIIIIII", view, body + 64 + index * 80)
                    zerofill = (flags & 0xFF) in ZEROFILL_TYPES
                    if not zerofill and sect_off + length > size:
                        raise ValueError("Section {} overruns the slice".format(_cstr(sect)))
                    segment["sections"].append({
                        "sectname": _cstr(sect), "segname": _cstr(seg), "addr": addr, "offset": sect_off,
                        "size": length, "zerofill": zerofill,
                        "sha256": None if zerofill else _sha256(view[sect_off:sect_off + length])})
                record["segments"].append(segment)
                if segment["segname"] == "__LINKEDIT":
                    masked += [(body + 24, body + 32), (body + 40, body + 48)]  # vmsize, filesize
            elif cmd == LC_UUID:
                record["uuid"] = bytes(view[body:body + 16]).hex()
                masked.append((body, body + 16))
            elif cmd == LC_CODE_SIGNATURE:
                dataoff, datasize = struct.unpack_from("<II", view, body)
                record["code_signature"] = {"dataoff": dataoff, "datasize": datasize}
                masked.append((body, body + 8))
            elif cmd == LC_ENCRYPTION_INFO_64:
                cryptoff, cryptsize, cryptid = struct.unpack_from("<III", view, body)
                record["encryption"] = {"cryptoff": cryptoff, "cryptsize": cryptsize, "cryptid": cryptid}
            position += cmdsize
        for segment in record["segments"]:
            if segment["segname"] == "__LINKEDIT":
                start, end = segment["fileoff"], segment["fileoff"] + segment["filesize"]
                if end > size:
                    raise ValueError("__LINKEDIT overruns the slice")
                digest = hashlib.sha256()
                signature = record["code_signature"]
                if signature and start <= signature["dataoff"] and signature["dataoff"] + signature["datasize"] <= end:
                    digest.update(view[start:signature["dataoff"]])
                    digest.update(view[signature["dataoff"] + signature["datasize"]:end])
                else:
                    digest.update(view[start:end])
                masked.append((start, end))
                record["linkedit"] = {"sha256_with_signature": _sha256(view[start:end]),
                                      "sha256_without_signature": digest.hexdigest()}
        # Everything else in the slice (header, load commands, padding, all sections); a change here that
        # no section or LINKEDIT hash explains would otherwise hide behind the slice hash.
        residual, cursor = hashlib.sha256(), 0
        for start, end in sorted(masked):
            residual.update(view[cursor:start])
            cursor = max(cursor, end)
        residual.update(view[cursor:])
        record["residual_sha256"] = residual.hexdigest()
        return record
    finally:
        view.release()  # an error must stay a ValueError, not a BufferError from closing the mmap


def fingerprint_macho(path):
    with open(path, "rb") as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
        header, ranges = _slice_ranges(data)
        file_sha256 = _sha256(data)
        fat = header["magic"] in (FAT_MAGIC, FAT_MAGIC_64)
        return {"file_sha256": file_sha256, "fat": fat, "magic": header["magic"], "count": header["count"],
                "container_sha256": header["container_sha256"],
                "slices": [_parse_slice(data, *entry, align, canonical)
                           for entry, align, canonical in zip(ranges, header["aligns"], header["canonical"])]}


def fingerprint_metadata(path):
    """Header table (offset, size, sha256) for a supported IL2CPP metadata version."""
    with open(path, "rb") as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
        if len(data) < 16:
            raise ValueError("global-metadata.dat is too small to hold a header")
        sanity, version, first = struct.unpack_from("<IiI", data, 0)
        if sanity != METADATA_SANITY:
            raise ValueError("global-metadata.dat sanity 0x{:08x} is not 0x{:08x}".format(sanity, METADATA_SANITY))
        names = METADATA_TABLES.get(version)
        if names is None:
            raise ValueError("Unsupported global-metadata.dat version {}; known: {}".format(
                version, sorted(METADATA_TABLES)))
        # The first table starts right after the header, so its offset is the header length.
        if first < 8 or (first - 8) % 8:
            raise ValueError("Header length 0x{:x} does not hold whole (offset, size) pairs".format(first))
        if (first - 8) // 8 != len(names):
            raise ValueError("Header holds {} pairs, version {} declares {}".format(
                (first - 8) // 8, version, len(names)))
        tables = {}
        for index, name in enumerate(names):
            offset, length = struct.unpack_from("<II", data, 8 + index * 8)
            if offset + length > len(data):
                raise ValueError("Table {} (0x{:x}+0x{:x}) lies outside the file".format(name, offset, length))
            tables[name] = {"offset": offset, "size": length,
                            "sha256": _sha256(memoryview(data)[offset:offset + length])}
        return {"file_sha256": _sha256(data), "sanity": sanity, "version": version, "header_size": first,
                "tables": tables}


def _bundle_file(app, relative):
    """Resolve a critical file without following any symlink, so it is the same file the manifest hashed."""
    path = app
    for part in relative.split("/"):
        path = os.path.join(path, part)
        if os.path.islink(path):
            raise ValueError("Critical input path is a symlink: " + relative)
    if not os.path.isfile(path):
        raise ValueError("Critical input file is missing: " + relative)
    return path


def fingerprint_input(app):
    """Build the full report for a .app bundle without touching it."""
    app = os.path.abspath(app)
    if os.path.islink(app):
        raise ValueError("The input bundle itself is a symlink")
    manifest = build_manifest(app)
    hashes = {item["path"]: item["sha256"] for item in manifest["files"]}
    assembly = fingerprint_macho(_bundle_file(app, inputvariants.GAME_ASSEMBLY))
    metadata = fingerprint_metadata(_bundle_file(app, inputvariants.METADATA))
    binary, meta = hashes.get(inputvariants.GAME_ASSEMBLY), hashes.get(inputvariants.METADATA)
    # The parsed bytes must be the bytes the manifest hashed, or the report would mix two snapshots.
    for relative, parsed, expected in ((inputvariants.GAME_ASSEMBLY, assembly, binary), (inputvariants.METADATA, metadata, meta)):
        if parsed.pop("file_sha256") != expected:
            raise ValueError("Critical input changed while fingerprinting: " + relative)
    return {"format": FORMAT, "app": os.path.basename(app), "manifest": manifest,
            "game_assembly": dict(assembly, path=inputvariants.GAME_ASSEMBLY, sha256=binary),
            "metadata": dict(metadata, path=inputvariants.METADATA, sha256=meta),
            "variant": inputvariants.code_variant(binary, meta)}


# Comparison -----------------------------------------------------------------------------------

def _diff(out, area, detail, packaging, **values):
    out.append(dict(area=area, detail=detail, packaging=packaging, **values))


def _compare_manifest(a, b, out):
    left = {item["path"]: item for item in a["files"]}
    right = {item["path"]: item for item in b["files"]}
    for path in sorted(left.keys() - right.keys()):
        _diff(out, "manifest", "removed", path.startswith(CODE_SIGNATURE_DIRECTORY), path=path)
    for path in sorted(right.keys() - left.keys()):
        _diff(out, "manifest", "added", path.startswith(CODE_SIGNATURE_DIRECTORY), path=path)
    for path in sorted(left.keys() & right.keys()):
        if (left[path]["size"], left[path]["sha256"]) != (right[path]["size"], right[path]["sha256"]):
            # GameAssembly.dylib is judged by its slice comparison; any other file is a real difference.
            _diff(out, "manifest", "changed", path.startswith(CODE_SIGNATURE_DIRECTORY) or path == inputvariants.GAME_ASSEMBLY,
                  path=path, a=[left[path]["size"], left[path]["sha256"]], b=[right[path]["size"], right[path]["sha256"]])
        if left[path]["mode"] != right[path]["mode"]:
            # An exec bit changes behaviour; only the signature seal's own files may differ in mode.
            _diff(out, "manifest", "mode", path.startswith(CODE_SIGNATURE_DIRECTORY),
                  path=path, a=oct(left[path]["mode"]), b=oct(right[path]["mode"]))
    links_a = {item["path"]: item["target"] for item in a["symlinks"]}
    links_b = {item["path"]: item["target"] for item in b["symlinks"]}
    for path in sorted(links_a.keys() | links_b.keys()):
        if links_a.get(path) != links_b.get(path):
            _diff(out, "manifest", "symlink", path.startswith(CODE_SIGNATURE_DIRECTORY),
                  path=path, a=links_a.get(path), b=links_b.get(path))


def _compare_slice(label, a, b, out):
    area = "slice " + label
    # Sizes and the slice hash follow from the signature and UUID differences; an offset follows from the
    # sizes only when the slice sits at its canonical aligned position on both sides.
    for key in ("offset", "size", "sha256"):
        if a[key] != b[key]:
            explained = key != "offset" or (a["canonical"] and b["canonical"])
            _diff(out, area, "slice " + key + (" (follows from other differences)" if explained else " (not at its canonical position)"),
                  explained, a=a[key], b=b[key])
    if a["align"] != b["align"]:
        _diff(out, area, "fat align", False, a=a["align"], b=b["align"])
    if a["residual_sha256"] != b["residual_sha256"]:
        _diff(out, area, "slice bytes outside LINKEDIT, UUID and signature fields", False,
              a=a["residual_sha256"], b=b["residual_sha256"])
    if a["uuid"] != b["uuid"]:
        _diff(out, area, "LC_UUID", True, a=a["uuid"], b=b["uuid"])
    signature_changed = a["code_signature"] != b["code_signature"]
    if signature_changed:
        _diff(out, area, "LC_CODE_SIGNATURE", True, a=a["code_signature"], b=b["code_signature"])
    if a["encryption"] != b["encryption"]:
        _diff(out, area, "LC_ENCRYPTION_INFO_64", False, a=a["encryption"], b=b["encryption"])
    la, lb = a["linkedit"], b["linkedit"]
    unsigned_equal = False
    if (la is None) != (lb is None):
        _diff(out, area, "__LINKEDIT presence", False, a=la is not None, b=lb is not None)
    elif la is not None:
        unsigned_equal = la["sha256_without_signature"] == lb["sha256_without_signature"]
        if not unsigned_equal:
            _diff(out, area, "__LINKEDIT without the code-signature range", False,
                  a=la["sha256_without_signature"], b=lb["sha256_without_signature"])
    segments_a = {s["segname"]: s for s in a["segments"]}
    segments_b = {s["segname"]: s for s in b["segments"]}
    for name in sorted(segments_a.keys() ^ segments_b.keys()):
        _diff(out, area, "segment only on one side", False, segname=name)
    for name in sorted(segments_a.keys() & segments_b.keys()):
        left, right = segments_a[name], segments_b[name]
        for key in ("vmaddr", "vmsize", "fileoff", "filesize"):
            if left[key] == right[key]:
                continue
            # __LINKEDIT's extent follows from the signature size, so only a signature change explains it.
            explained = name == "__LINKEDIT" and key in ("vmsize", "filesize") and signature_changed
            _diff(out, area, "segment " + key + (" (follows from the signature range)" if explained else ""),
                  explained, segname=name, a=left[key], b=right[key])
        if left["sha256"] != right["sha256"]:
            # __LINKEDIT bytes may differ only inside the signature range, judged by the unsigned hash above.
            explained = name == "__LINKEDIT" and unsigned_equal
            _diff(out, area, "segment sha256" + (" (follows from the signature range)" if explained else ""),
                  explained, segname=name, a=left["sha256"], b=right["sha256"])
    sections_a = {(s["segname"], s["sectname"]): s for seg in a["segments"] for s in seg["sections"]}
    sections_b = {(s["segname"], s["sectname"]): s for seg in b["segments"] for s in seg["sections"]}
    for key in sorted(sections_a.keys() ^ sections_b.keys()):
        _diff(out, area, "section only on one side", False, segname=key[0], sectname=key[1])
    for key in sorted(sections_a.keys() & sections_b.keys()):
        for field in ("offset", "size", "zerofill", "sha256"):
            if sections_a[key][field] != sections_b[key][field]:
                _diff(out, area, "section " + field, False, segname=key[0], sectname=key[1],
                      a=sections_a[key][field], b=sections_b[key][field])


def _slice_labels(slices):
    """Display name per (cputype, cpusubtype); the subtype is added only where names would collide."""
    names = [s["arch"] for s in slices.values()]
    return {key: s["arch"] if names.count(s["arch"]) == 1 else "{} (subtype 0x{:x})".format(s["arch"], key[1] & 0xFFFFFFFF)
            for key, s in slices.items()}


def _compare_assembly(a, b, out):
    area = "GameAssembly.dylib"
    if a["fat"] != b["fat"]:
        _diff(out, area, "fat versus thin", False, a=a["fat"], b=b["fat"])
    if a["sha256"] != b["sha256"]:
        _diff(out, area, "binary sha256 (follows from other differences)", True, a=a["sha256"], b=b["sha256"])
    # The fat header and every byte between slices must match; only the per-slice offset and size follow from sizes.
    for key, detail in (("magic", "fat magic"), ("count", "fat architecture count"),
                        ("container_sha256", "bytes outside the slices (fat header and padding)")):
        if a[key] != b[key]:
            _diff(out, area, detail, False, a=a[key], b=b[key])
    slices_a = {(s["cputype"], s["cpusubtype"]): s for s in a["slices"]}
    slices_b = {(s["cputype"], s["cpusubtype"]): s for s in b["slices"]}
    labels = _slice_labels({**slices_a, **slices_b})
    for key in sorted(slices_a.keys() ^ slices_b.keys()):
        _diff(out, area, "architecture only on one side", False, arch=labels[key])
    for key in sorted(slices_a.keys() & slices_b.keys()):
        _compare_slice(labels[key], slices_a[key], slices_b[key], out)


def _compare_metadata(a, b, out):
    for key in ("sanity", "version", "header_size"):
        if a[key] != b[key]:
            _diff(out, "global-metadata.dat", key, False, a=a[key], b=b[key])
    for name in sorted(a["tables"].keys() ^ b["tables"].keys()):
        _diff(out, "global-metadata.dat", "table only on one side", False, table=name)
    for name in sorted(a["tables"].keys() & b["tables"].keys()):
        for field in ("offset", "size", "sha256"):
            if a["tables"][name][field] != b["tables"][name][field]:
                _diff(out, "global-metadata.dat", "table " + field, False, table=name,
                      a=a["tables"][name][field], b=b["tables"][name][field])


def compare_fingerprints(a, b):
    """Compare two reports. packaging_only is evidence for review, never an automatic qualification."""
    for report in (a, b):
        if report.get("format") != FORMAT:
            raise ValueError("Unsupported fingerprint format " + repr(report.get("format")))
    differences = []
    _compare_manifest(a["manifest"], b["manifest"], differences)
    _compare_assembly(a["game_assembly"], b["game_assembly"], differences)
    _compare_metadata(a["metadata"], b["metadata"], differences)
    if a["variant"] != b["variant"]:
        # The variant name follows from the binary and metadata hashes, which are judged above.
        _diff(differences, "variant", "declared variant (follows from the file hashes)", True, a=a["variant"], b=b["variant"])
    other = [item for item in differences if not item["packaging"]]
    return {"a": a["app"], "b": b["app"], "variants": [a["variant"], b["variant"]],
            "differences": differences,
            "summary": {"identical": not differences, "packaging_only": not other, "difference_count": len(differences),
                        "non_packaging_count": len(other)}}

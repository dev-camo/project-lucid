"""Small, bounds-checked Mach-O inventory reader for the supplied Mac player."""

import collections
import mmap
import re
import struct
from pathlib import Path


_CPUS = {0x01000007: "x86_64", 0x0100000C: "arm64", 7: "x86", 12: "arm"}
_METHOD = re.compile(r"^_.+_m[0-9A-Fa-f]{40}$")


def inspect_macho(path: Path) -> dict:
    """Summarize architectures, encryption flags and IL2CPP named symbols.

    Addresses are unslid virtual addresses from each architecture. Symbol
    tables are scanned, but only registration/module names and bounded samples
    are returned; debug entries are not mistaken for live definitions.
    """
    path = Path(path)
    try:
        with path.open("rb") as stream:
            if path.stat().st_size < 4:
                raise ValueError("Truncated Mach-O file: %s" % path)
            with mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
                return _inspect(data)
    except OSError as exc:
        raise ValueError("Cannot read Mach-O file: %s" % path) from exc


def _inspect(data):
    def unpack(format_string, offset, end=None):
        size = struct.calcsize(format_string)
        limit = len(data) if end is None else end
        if offset < 0 or offset + size > limit:
            raise ValueError("Truncated Mach-O structure")
        return struct.unpack_from(format_string, data, offset)

    magic = bytes(data[:4])
    fat_formats = {b"\xca\xfe\xba\xbe": (">", False), b"\xbe\xba\xfe\xca": ("<", False),
                   b"\xca\xfe\xba\xbf": (">", True), b"\xbf\xba\xfe\xca": ("<", True)}
    slices = []
    if magic in fat_formats:
        endian, wide = fat_formats[magic]
        count = unpack(endian + "I", 4)[0]
        if not 1 <= count <= 64:
            raise ValueError("Invalid Mach-O universal architecture count")
        format_string = endian + ("iiQQII" if wide else "iiIII")
        size = struct.calcsize(format_string)
        for i in range(count):
            row = unpack(format_string, 8 + i * size)
            cpu, subtype, offset, length = row[:4]
            if offset < 8 + count * size or length < 28 or offset + length > len(data):
                raise ValueError("Invalid Mach-O universal slice bounds")
            slices.append((cpu, subtype, offset, length))
        ordered = sorted(slices, key=lambda row: row[2])
        if any(a[2] + a[3] > b[2] for a, b in zip(ordered, ordered[1:])):
            raise ValueError("Overlapping Mach-O universal slices")
    else:
        slices = [(None, None, 0, len(data))]
    architectures = []
    for fat_cpu, fat_subtype, base, length in slices:
        end = base + length
        thin_magic = bytes(data[base:base + 4])
        formats = {b"\xcf\xfa\xed\xfe": ("<", True), b"\xfe\xed\xfa\xcf": (">", True),
                   b"\xce\xfa\xed\xfe": ("<", False), b"\xfe\xed\xfa\xce": (">", False)}
        if thin_magic not in formats:
            raise ValueError("Unsupported or invalid Mach-O magic")
        endian, wide = formats[thin_magic]
        header = unpack(endian + ("IiiIIIII" if wide else "IiiIIII"), base, end)
        cpu, subtype, file_type, ncmds, sizeofcmds = header[1:6]
        header_size = 32 if wide else 28
        if fat_cpu is not None and (cpu != fat_cpu or subtype != fat_subtype):
            raise ValueError("Mach-O universal/header architecture mismatch")
        command_end = base + header_size + sizeofcmds
        if command_end > end or ncmds > sizeofcmds // 8:
            raise ValueError("Invalid Mach-O load command bounds")
        pos = base + header_size
        symbols = None
        encryptions = []
        dependencies = []
        for _ in range(ncmds):
            cmd, cmdsize = unpack(endian + "II", pos, command_end)
            if cmdsize < 8 or pos + cmdsize > command_end:
                raise ValueError("Invalid Mach-O load command size")
            if cmd == 2:
                if cmdsize < 24:
                    raise ValueError("Truncated Mach-O symbol command")
                symbols = unpack(endian + "6I", pos, pos + cmdsize)[2:]
            if cmd in (0x21, 0x2C):
                if cmdsize < (24 if cmd == 0x2C else 20):
                    raise ValueError("Truncated Mach-O encryption command")
                cryptoff, cryptsize, cryptid = unpack(endian + "III", pos + 8, pos + cmdsize)
                if cryptoff + cryptsize > length:
                    raise ValueError("Invalid Mach-O encrypted range")
                encryptions.append({"offset": cryptoff, "size": cryptsize, "cryptid": cryptid})
            if cmd in (0xC, 0x18, 0x80000018, 0x8000001F, 0x80000023):
                if cmdsize < 24:
                    raise ValueError("Truncated Mach-O dylib command")
                name_offset = unpack(endian + "I", pos + 8, pos + cmdsize)[0]
                if name_offset < 24 or name_offset >= cmdsize:
                    raise ValueError("Invalid Mach-O dylib name offset")
                name_end = data.find(b"\0", pos + name_offset, pos + cmdsize)
                if name_end < 0:
                    raise ValueError("Unterminated Mach-O dylib name")
                dependencies.append(bytes(data[pos + name_offset:name_end]).decode("utf-8", "replace"))
            pos += cmdsize
        if pos != command_end:
            raise ValueError("Mach-O load command count/size mismatch")
        result = {"name": _CPUS.get(cpu, "cpu_%x" % cpu), "cpu_type": cpu,
                  "cpu_subtype": subtype, "offset": base, "size": length,
                  "file_type": file_type, "is_64_bit": wide,
                  "encryption_commands": encryptions,
                  "encrypted": any(e["cryptid"] != 0 for e in encryptions),
                  "dependencies": sorted(set(dependencies)), "symbol_count": 0,
                  "defined_symbol_count": 0, "method_symbol_count": 0,
                  "registration_symbols": {}, "codegen_modules": {},
                  "method_symbol_samples": []}
        if symbols:
            symoff, nsyms, stroff, strsize = symbols
            record_format = endian + ("IBBHQ" if wide else "IBBHI")
            record_size = struct.calcsize(record_format)
            if (symoff + nsyms * record_size > length or stroff + strsize > length):
                raise ValueError("Invalid Mach-O symbol table bounds")
            result["symbol_count"] = nsyms
            symbol_types = collections.Counter()
            for i in range(nsyms):
                strx, kind, section, desc, value = unpack(record_format, base + symoff + i * record_size, end)
                symbol_types[kind] += 1
                if strx >= strsize:
                    raise ValueError("Invalid Mach-O symbol string index")
                if kind & 0xE0 or kind & 0x0E != 0x0E or not section:
                    continue
                result["defined_symbol_count"] += 1
                name_end = data.find(b"\0", base + stroff + strx, base + stroff + strsize)
                if name_end < 0:
                    raise ValueError("Unterminated Mach-O symbol name")
                name = bytes(data[base + stroff + strx:name_end]).decode("utf-8", "replace")
                if name in ("_g_CodeRegistration", "_g_MetadataRegistration", "_g_CodeGenModules", "_g_CodegenRegistration"):
                    result["registration_symbols"][name[1:]] = "0x%x" % value
                if name.startswith("_g_") and name.endswith("_CodeGenModule"):
                    result["codegen_modules"][name[1:]] = "0x%x" % value
                if _METHOD.match(name):
                    result["method_symbol_count"] += 1
                    if len(result["method_symbol_samples"]) < 8:
                        result["method_symbol_samples"].append({"name": name, "address": "0x%x" % value})
            result["symbol_types"] = {str(k): symbol_types[k] for k in sorted(symbol_types)}
        architectures.append(result)
    return {"universal": magic in fat_formats, "architectures": architectures}

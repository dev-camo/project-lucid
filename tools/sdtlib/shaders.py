"""Preserve compiled Shader YAML and extract its readable Metal evidence.

This is a narrow parser for AssetRipper's Unity 2019.3+ packed-array YAML export,
not a general YAML loader or a ShaderLab/Shader Graph decompiler.
"""

from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import struct
import uuid

from .bootstrap import managed_path, validate_work_dir, write_json


MAX_YAML_BYTES = 64 * 1024 * 1024
MAX_BLOB_BYTES = 32 * 1024 * 1024
MAX_SEGMENT_BYTES = 64 * 1024 * 1024
MAX_SHADER_DECODED_BYTES = 256 * 1024 * 1024
MAX_RUN_DECODED_BYTES = 1024 * 1024 * 1024
MAX_PROGRAMS = 65536
MAX_SHADERS = 4096
PROGRAM_VERSION = 202012090  # Observed Unity 2022.3.54f1 layout.
METAL_PLATFORM = 14
METAL_PROGRAM_TYPES = {23: "vertex", 24: "fragment"}
FIELD_PATTERN = re.compile(r"^  (platforms|offsets|compressedLengths|decompressedLengths|compressedBlob):[ \t]*(.*)$")
HEX_PATTERN = re.compile(r"[0-9a-fA-F]*")


def decode_lz4_block(data: bytes, expected_size: int, *, max_size: int = MAX_SEGMENT_BYTES) -> bytes:
    """Decode a raw LZ4 block with checked input, backreferences, and output size."""
    if not 0 <= expected_size <= max_size:
        raise ValueError("LZ4 decoded size exceeds the configured limit")
    output = bytearray()
    position = 0
    terminal_literals = False

    def length(initial: int) -> int:
        nonlocal position
        result = initial
        if initial == 15:
            while True:
                if position >= len(data):
                    raise ValueError("Truncated LZ4 length extension")
                extra = data[position]
                position += 1
                result += extra
                if result > expected_size:
                    raise ValueError("LZ4 sequence exceeds the declared output size")
                if extra != 255:
                    break
        return result

    while position < len(data):
        token = data[position]
        position += 1
        literal_length = length(token >> 4)
        if position + literal_length > len(data):
            raise ValueError("Truncated LZ4 literals")
        if len(output) + literal_length > expected_size:
            raise ValueError("LZ4 literals exceed the declared output size")
        output.extend(data[position:position + literal_length])
        position += literal_length
        if position == len(data):
            terminal_literals = True
            break
        if position + 2 > len(data):
            raise ValueError("Truncated LZ4 match offset")
        offset = data[position] | (data[position + 1] << 8)
        position += 2
        if offset == 0 or offset > len(output):
            raise ValueError("Invalid LZ4 backreference")
        match_length = length(token & 15) + 4
        if len(output) + match_length > expected_size:
            raise ValueError("LZ4 match exceeds the declared output size")
        # Snapshot a complete repeating period; this also handles overlapping matches.
        period = output[-offset:]
        repeats, remainder = divmod(match_length, offset)
        output.extend(period * repeats)
        output.extend(period[:remainder])
    if not terminal_literals or len(output) != expected_size:
        raise ValueError("LZ4 block has no final literal sequence or has the wrong decoded size")
    return bytes(output)


def _hex_bytes(value: str, field: str, limit: int) -> bytes:
    if len(value) > limit * 2 or len(value) % 2 or not HEX_PATTERN.fullmatch(value):
        raise ValueError(f"Invalid or oversized packed hexadecimal field: {field}")
    return bytes.fromhex(value)


def _uint_array(value: str, field: str) -> list[int]:
    packed = _hex_bytes(value, field, MAX_PROGRAMS * 4)
    if len(packed) % 4:
        raise ValueError(f"Packed uint32 field is not aligned: {field}")
    return [item[0] for item in struct.iter_unpack("<I", packed)]


def parse_shader_yaml(data: bytes) -> dict:
    """Read only the five compiled-blob fields from one Unity Shader document."""
    if len(data) > MAX_YAML_BYTES:
        raise ValueError("Shader YAML exceeds the input size limit")
    text = data.decode("utf-8")
    lines = text.splitlines()
    headers = [line for line in lines if line.startswith("--- ")]
    if len(headers) != 1 or not re.fullmatch(r"--- !u!48 &\d+", headers[0]):
        raise ValueError("Expected one Unity Shader YAML document (class 48)")
    fields = {}
    index = 0
    while index < len(lines):
        match = FIELD_PATTERN.fullmatch(lines[index])
        if not match:
            index += 1
            continue
        field, value = match.groups()
        if field in fields:
            raise ValueError(f"Duplicate compiled shader field: {field}")
        index += 1
        if field in ("offsets", "compressedLengths", "decompressedLengths"):
            if value:
                raise ValueError(f"Expected packed array rows for {field}")
            rows = []
            while index < len(lines) and lines[index].startswith("  - "):
                rows.append(_uint_array(lines[index][4:], field))
                index += 1
            fields[field] = rows
        elif field == "platforms":
            fields[field] = _uint_array(value, field)
        else:
            fields[field] = _hex_bytes(value, field, MAX_BLOB_BYTES)
    required = {"platforms", "offsets", "compressedLengths", "decompressedLengths", "compressedBlob"}
    if set(fields) != required or not fields["platforms"]:
        raise ValueError("Shader has no supported compiled-blob field layout")
    platforms = fields["platforms"]
    if len(platforms) > 32:
        raise ValueError("Shader platform count exceeds the limit")
    total = 0
    for field in ("offsets", "compressedLengths", "decompressedLengths"):
        if len(fields[field]) != len(platforms):
            raise ValueError(f"Shader platform/row count mismatch: {field}")
    for platform_index in range(len(platforms)):
        rows = [fields[field][platform_index] for field in ("offsets", "compressedLengths", "decompressedLengths")]
        if not rows[0] or len(rows[0]) > 256 or len({len(row) for row in rows}) != 1:
            raise ValueError("Shader segment rows differ in length or exceed the limit")
        for offset, compressed, decoded in zip(*rows):
            if compressed == 0 or decoded == 0 or offset + compressed > len(fields["compressedBlob"]):
                raise ValueError("Shader compressed segment is empty or outside compressedBlob")
            if decoded > MAX_SEGMENT_BYTES:
                raise ValueError("Shader decoded segment exceeds the size limit")
            total += decoded
            if total > MAX_SHADER_DECODED_BYTES:
                raise ValueError("Shader decoded segments exceed the cumulative size limit")
    name = next((line[10:] for line in lines if line.startswith("  m_Name: ")), "")
    # Names are diagnostic data only and are never used as output paths.
    fields["shader_name"] = name
    fields["decoded_bytes"] = total
    return fields


def program_entries(segments: list[bytes]) -> list[dict]:
    """Read 2019.3+ segment-zero entries, preserving the original blob indices."""
    if not segments or len(segments[0]) < 4:
        raise ValueError("Shader segment zero has no program table")
    count = struct.unpack_from("<I", segments[0])[0]
    table_end = 4 + count * 12
    if count > MAX_PROGRAMS or table_end > len(segments[0]):
        raise ValueError("Shader program table is truncated or exceeds the entry limit")
    entries = []
    for blob_index in range(count):
        offset, size, segment = struct.unpack_from("<III", segments[0], 4 + blob_index * 12)
        if segment >= len(segments) or offset + size > len(segments[segment]):
            raise ValueError("Shader program entry points outside its decoded segment")
        if size and segment == 0 and offset < table_end:
            raise ValueError("Shader program entry overlaps the program table")
        entries.append({"blob_index": blob_index, "segment_index": segment,
                        "offset": offset, "length": size})
    return entries


def metal_program(program: bytes) -> dict | None:
    """Extract exact source bytes from an observed Unity Metal subprogram layout."""
    if len(program) < 8:
        return None
    version, gpu_type = struct.unpack_from("<II", program)
    if gpu_type not in METAL_PROGRAM_TYPES:
        return None
    if version != PROGRAM_VERSION or len(program) < 28:
        raise ValueError(f"Unsupported Metal subprogram layout: version {version}")
    position = 24
    count = struct.unpack_from("<I", program, position)[0]
    position += 4
    if count > 4096:
        raise ValueError("Metal keyword count exceeds the limit")
    keywords = []
    for _ in range(count):
        if position + 4 > len(program):
            raise ValueError("Truncated Metal keyword length")
        size = struct.unpack_from("<I", program, position)[0]
        position += 4
        if size > 65536 or position + size > len(program):
            raise ValueError("Metal keyword lies outside its subprogram")
        keywords.append(program[position:position + size].decode("utf-8"))
        position = (position + size + 3) & ~3
    if position + 4 > len(program):
        raise ValueError("Metal program has no code length")
    code_length = struct.unpack_from("<I", program, position)[0]
    position += 4
    if code_length < 9 or position + code_length > len(program):
        raise ValueError("Metal code lies outside its subprogram")
    code = program[position:position + code_length]
    magic, entry_offset = struct.unpack_from("<II", code)
    if magic != 0xF00DCAFE or entry_offset < 8 or entry_offset >= len(code):
        raise ValueError("Unsupported Metal code header")
    entry_end = code.find(b"\0", entry_offset)
    if entry_end == -1:
        raise ValueError("Metal entry name is not terminated")
    entry_name = code[entry_offset:entry_end].decode("utf-8")
    source = code[entry_end + 1:]
    source.decode("utf-8")  # Preserve bytes exactly, reject opaque/non-UTF8 data.
    if b"#include <metal_stdlib>" not in source or b"\0" in source:
        raise ValueError("Metal program does not contain supported readable source")
    return {"program_version": version, "gpu_program_type": gpu_type,
            "stage": METAL_PROGRAM_TYPES[gpu_type], "keywords": keywords,
            "entry_name": entry_name, "code_offset_in_program": position,
            "code_length": code_length, "source_offset_in_code": entry_end + 1,
            "source": source}


def _source_path(path: Path) -> Path:
    absolute = Path(os.path.abspath(os.fspath(path)))
    for component in (absolute, *absolute.parents):
        if component.is_symlink():
            raise ValueError("Shader evidence input cannot contain symbolic links")
    if not absolute.exists():
        raise ValueError("Shader evidence input does not exist")
    return absolute


def _shader_files(path: Path) -> list[Path]:
    if path.is_file():
        return [path]
    if not path.is_dir():
        raise ValueError("Shader evidence input must be a YAML file or asset directory")
    files = []
    for directory, directories, names in os.walk(path, followlinks=False):
        for name in directories:
            if (Path(directory) / name).is_symlink():
                raise ValueError("Shader evidence input contains a symbolic link directory")
        for name in sorted(names):
            if not name.endswith(".asset"):
                continue
            candidate = _source_path(Path(directory) / name)
            if not candidate.is_file():
                raise ValueError("Shader evidence asset must be a regular file")
            with candidate.open("rb") as source:
                header = source.read(1024)
            if b"--- !u!48 " in header:
                files.append(candidate)
                if len(files) > MAX_SHADERS:
                    raise ValueError("Shader evidence input exceeds the shader count limit")
    return sorted(files)


def extract_shader_evidence(input_path: Path, work_dir: Path) -> dict:
    """Decode exported shader evidence into a fresh, isolated generated cache run."""
    work_dir = validate_work_dir(work_dir)
    source_path = _source_path(input_path)
    files = _shader_files(source_path)
    if not files:
        raise ValueError("No Unity Shader YAML assets were found")
    run = managed_path(work_dir, "shaders", "runs", uuid.uuid4().hex)
    run.mkdir(parents=True)
    report = {"schema_version": 1, "status": "ready", "input": str(source_path),
              "output_dir": str(run), "shader_count": len(files), "metal_program_count": 0,
              "decoded_bytes": 0, "shaders": [], "errors": [], "runtime_parity_verified": False,
              "limitations": ["Extracted Metal programs are compiled-release source evidence; original Shader Graph and HLSL authoring source are not recovered.",
                              "Extraction does not establish ShaderLab reconstruction, rendering equivalence, or portability."]}
    for shader_index, path in enumerate(files):
        item = {"source_path": str(path), "status": "failed", "programs": [], "platforms": []}
        report["shaders"].append(item)
        try:
            if path.stat().st_size > MAX_YAML_BYTES:
                raise ValueError("Shader YAML exceeds the input size limit")
            with path.open("rb") as source:
                raw = source.read(MAX_YAML_BYTES + 1)
            shader = parse_shader_yaml(raw)
            if report["decoded_bytes"] + shader["decoded_bytes"] > MAX_RUN_DECODED_BYTES:
                raise ValueError("Shader run exceeds the cumulative decoded size limit")
            digest = hashlib.sha256(raw).hexdigest()
            destination = managed_path(work_dir, "shaders", "runs", run.name, f"{shader_index:04d}-{digest[:16]}")
            destination.mkdir()
            item.update({"shader_name": shader["shader_name"], "source_sha256": digest,
                         "compressed_blob_sha256": hashlib.sha256(shader["compressedBlob"]).hexdigest(),
                         "output_dir": str(destination)})
            (destination / "compressed.bin").write_bytes(shader["compressedBlob"])
            for platform_index, platform in enumerate(shader["platforms"]):
                platform_dir = destination / f"platform-{platform_index:02d}-{platform}"
                platform_dir.mkdir()
                decoded_segments = []
                platform_item = {"platform_index": platform_index, "platform_id": platform,
                                 "platform_name": "Metal" if platform == METAL_PLATFORM else "unknown",
                                 "segments": []}
                item["platforms"].append(platform_item)
                for segment_index, (offset, compressed, decoded) in enumerate(zip(
                        shader["offsets"][platform_index], shader["compressedLengths"][platform_index],
                        shader["decompressedLengths"][platform_index])):
                    segment = decode_lz4_block(shader["compressedBlob"][offset:offset + compressed], decoded)
                    report["decoded_bytes"] += decoded
                    decoded_segments.append(segment)
                    segment_path = platform_dir / f"segment-{segment_index:03d}.bin"
                    segment_path.write_bytes(segment)
                    platform_item["segments"].append({"segment_index": segment_index, "compressed_offset": offset,
                                                      "compressed_length": compressed, "decoded_length": decoded,
                                                      "decoded_sha256": hashlib.sha256(segment).hexdigest(),
                                                      "decoded_file": str(segment_path)})
                for entry in program_entries(decoded_segments):
                    segment = decoded_segments[entry["segment_index"]]
                    program = segment[entry["offset"]:entry["offset"] + entry["length"]]
                    program_path = platform_dir / f"blob-{entry['blob_index']:05d}.bin"
                    program_path.write_bytes(program)
                    record = {**entry, "platform_index": platform_index, "platform_id": platform,
                              "program_file": str(program_path), "program_sha256": hashlib.sha256(program).hexdigest(),
                              "classification": "metadata_or_opaque_program"}
                    item["programs"].append(record)
                    if len(program) >= 8:
                        record["program_version"], record["gpu_program_type"] = struct.unpack_from("<II", program)
                    if platform != METAL_PLATFORM:
                        continue
                    metal = metal_program(program)
                    if metal is not None:
                        metal_source = metal.pop("source")
                        metal_path = platform_dir / f"blob-{entry['blob_index']:05d}.metal"
                        metal_path.write_bytes(metal_source)
                        record.update(metal)
                        record.update({"classification": "readable_metal_source", "source_file": str(metal_path),
                                       "source_length": len(metal_source), "source_sha256": hashlib.sha256(metal_source).hexdigest()})
                        report["metal_program_count"] += 1
            item["status"] = "ready"
        except (ValueError, UnicodeError, OSError, struct.error) as error:
            item["error"] = str(error)
            report["errors"].append({"source_path": str(path), "error": str(error)})
            report["status"] = "incomplete"
        if item.get("output_dir"):
            write_json(Path(item["output_dir"]) / "evidence.json", item)
    write_json(run / "evidence.json", report)
    write_json(managed_path(work_dir, "reports", "shader-evidence.json"), report)
    return report

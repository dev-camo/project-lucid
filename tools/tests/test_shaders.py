"""Bounded shader decoding and original program identity regressions."""

import hashlib
import json
from pathlib import Path
import shutil
import struct
import tempfile
import unittest

from tools.lucidlib.bootstrap import CACHE_ROOT, REPO_ROOT
from tools.lucidlib.shaders import (MAX_SEGMENT_BYTES, PROGRAM_VERSION, decode_lz4_block,
                                 extract_shader_evidence, metal_program, parse_shader_yaml,
                                 program_entries)


def literal_block(data):
    length = len(data)
    result = bytearray([min(length, 15) << 4])
    if length >= 15:
        length -= 15
        while length >= 255:
            result.append(255)
            length -= 255
        result.append(length)
    result.extend(data)
    return bytes(result)


def source_program(source, keywords=()):
    header = bytearray(struct.pack("<IIIIII", PROGRAM_VERSION, 23, 0, 0, 0, 0))
    header.extend(struct.pack("<I", len(keywords)))
    for keyword in keywords:
        raw = keyword.encode()
        header.extend(struct.pack("<I", len(raw)) + raw)
        header.extend(b"\0" * (-len(header) % 4))
    code = struct.pack("<II", 0xF00DCAFE, 8) + b"test_entry\0" + source
    return bytes(header) + struct.pack("<I", len(code)) + code


def shader_yaml(segments, platforms=(14,)):
    blocks = [literal_block(segment) for segment in segments]
    offsets = []
    position = 0
    for block in blocks:
        offsets.append(position)
        position += len(block)

    def packed(values):
        return b"".join(struct.pack("<I", value) for value in values).hex()

    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!48 &4800000\nShader:\n"
            "  m_Name: Test/Shader\n  platforms: " + packed(platforms) +
            "\n  offsets:\n  - " + packed(offsets) +
            "\n  compressedLengths:\n  - " + packed([len(block) for block in blocks]) +
            "\n  decompressedLengths:\n  - " + packed([len(segment) for segment in segments]) +
            "\n  compressedBlob: " + b"".join(blocks).hex() + "\n").encode()


class ShaderEvidenceTests(unittest.TestCase):
    def setUp(self):
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix="test-shaders-", dir=CACHE_ROOT))
        self.source = b"#include <metal_stdlib>\nusing namespace metal;\nvertex void test_entry() {}\n"
        self.program = source_program(self.source, ("TEST_VARIANT",))
        metadata = struct.pack("<II", PROGRAM_VERSION, 3)
        offset = 4 + 2 * 12
        self.segment = (struct.pack("<I", 2) + struct.pack("<III", offset, len(metadata), 0) +
                        struct.pack("<III", offset + len(metadata), len(self.program), 0) + metadata + self.program)
        self.asset = self.work / "source.asset"
        self.asset.write_bytes(shader_yaml([self.segment]))

    def tearDown(self):
        shutil.rmtree(self.work)

    def test_lz4_literals_extensions_and_overlapping_backreferences(self):
        self.assertEqual(decode_lz4_block(literal_block(b"a" * 700), 700), b"a" * 700)
        # Three literals, an eight-byte overlapping match at distance three, final literals.
        block = bytes([0x34]) + b"abc" + bytes([3, 0, 0x50]) + b"12345"
        self.assertEqual(decode_lz4_block(block, 16), b"abcabcabcab12345")

    def test_lz4_rejects_corruption_and_output_expansion_before_allocating(self):
        invalid = [(b"\xf0", 20), (b"\x50abc", 5), (b"\x10a\x00\x00\x00", 5),
                   (b"\x10a\x02\x00\x00", 5), (b"\x10a\x01\x00", 5),
                   (literal_block(b"too long"), 1), (literal_block(b"short"), 10)]
        for block, size in invalid:
            with self.subTest(block=block), self.assertRaises(ValueError):
                decode_lz4_block(block, size)
        with self.assertRaises(ValueError):
            decode_lz4_block(b"\0", MAX_SEGMENT_BYTES + 1)

    def test_yaml_rejects_duplicate_fields_bad_rows_and_out_of_bounds_ranges(self):
        raw = self.asset.read_bytes()
        invalid = [raw + b"  platforms: 0e000000\n", raw.replace(b"0e000000", b"0e0000", 1),
                   raw.replace(b"  - 00000000\n", b"  - ffffffff\n", 1),
                   raw.replace(b"  platforms: 0e000000", b"  platforms: 0e0000000e000000"),
                   raw.replace(b"  offsets:\n", b"  offsets: []\n")]
        for candidate in invalid:
            with self.subTest(candidate=candidate[-100:]), self.assertRaises(ValueError):
                parse_shader_yaml(candidate)

    def test_program_table_retains_blob_and_segment_indices(self):
        table = struct.pack("<IIII", 1, 0, len(self.program), 1)
        entries = program_entries([table, self.program])
        self.assertEqual(entries, [{"blob_index": 0, "segment_index": 1, "offset": 0,
                                    "length": len(self.program)}])
        report = extract_shader_evidence(self.asset, self.work)
        program = report["shaders"][0]["programs"][1]
        self.assertEqual(program["blob_index"], 1)
        self.assertEqual(program["gpu_program_type"], 23)
        self.assertEqual(program["keywords"], ["TEST_VARIANT"])
        self.assertEqual(program["entry_name"], "test_entry")
        self.assertEqual(Path(program["source_file"]).read_bytes(), self.source)
        self.assertEqual(program["source_sha256"], hashlib.sha256(self.source).hexdigest())
        self.assertFalse(report["runtime_parity_verified"])

    def test_program_table_rejects_truncation_overlap_and_external_segment(self):
        invalid = [struct.pack("<I", 65537), struct.pack("<IIII", 1, 0, 4, 0),
                   struct.pack("<IIII", 1, 0, 4, 1), struct.pack("<IIII", 1, 16, 100, 0)]
        for table in invalid:
            with self.subTest(table=table), self.assertRaises(ValueError):
                program_entries([table])

    def test_metal_parser_rejects_unrecognized_layout_and_opaque_code(self):
        self.assertIsNone(metal_program(struct.pack("<II", PROGRAM_VERSION, 3)))
        invalid = [struct.pack("<I", PROGRAM_VERSION + 1) + self.program[4:],
                   self.program[:28], self.program.replace(b"test_entry\0", b"test_entryX"),
                   self.program.replace(b"#include <metal_stdlib>", b"#include <opaque_bytes>")]
        for program in invalid:
            with self.subTest(program=program[:32]), self.assertRaises(ValueError):
                metal_program(program)

    def test_non_metal_platform_preserves_opaque_bytes(self):
        self.asset.write_bytes(shader_yaml([self.segment], platforms=(4,)))
        report = extract_shader_evidence(self.asset, self.work)
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["metal_program_count"], 0)
        self.assertEqual(report["shaders"][0]["platforms"][0]["platform_id"], 4)
        self.assertEqual(Path(report["shaders"][0]["programs"][1]["program_file"]).read_bytes(), self.program)

    def test_each_run_is_fresh_and_input_is_unchanged(self):
        original = self.asset.read_bytes()
        first = extract_shader_evidence(self.asset, self.work)
        second = extract_shader_evidence(self.asset, self.work)
        self.assertNotEqual(first["output_dir"], second["output_dir"])
        self.assertEqual(self.asset.read_bytes(), original)
        self.assertTrue((Path(first["output_dir"]) / "evidence.json").is_file())
        latest = json.loads((self.work / "reports/shader-evidence.json").read_text())
        self.assertEqual(latest["output_dir"], second["output_dir"])

    def test_shader_directory_skips_other_assets_and_rejects_symlink_redirects(self):
        (self.work / "material.asset").write_text("--- !u!21 &2100000\nMaterial:\n")
        report = extract_shader_evidence(self.work, self.work / "output")
        self.assertEqual(report["shader_count"], 1)
        (self.work / "linked.asset").symlink_to(self.asset)
        with self.assertRaises(ValueError):
            extract_shader_evidence(self.work, self.work / "output")

    def test_invalid_input_reports_failure_without_claiming_source_recovery(self):
        self.asset.write_bytes(self.asset.read_bytes().replace(b"  - 00000000\n", b"  - ffffffff\n", 1))
        report = extract_shader_evidence(self.asset, self.work)
        self.assertEqual(report["status"], "incomplete")
        self.assertEqual(report["metal_program_count"], 0)
        self.assertEqual(report["shaders"][0]["status"], "failed")
        self.assertEqual(len(report["errors"]), 1)
        with self.assertRaises(ValueError):
            extract_shader_evidence(self.asset, REPO_ROOT / "input")


if __name__ == "__main__":
    unittest.main()

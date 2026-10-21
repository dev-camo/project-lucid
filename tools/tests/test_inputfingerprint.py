"""Input fingerprint parsing and comparison, against synthetic Mach-O and metadata files."""

import hashlib
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import lucid
from lucidlib import inputfingerprint

X86_64, ARM64 = 0x01000007, 0x0100000C
TEXT = b"\x90" * 32 + b"TEXT-BYTES"
SIGNATURE_A, SIGNATURE_B = b"sig-a" * 4, b"sig-bee" * 5
UUID_A, UUID_B = bytes(range(16)), bytes(range(16, 32))
SYMBOLS = b"SYMBOLS!" * 2


def sha(data):
    return hashlib.sha256(data).hexdigest()


def name16(text):
    return text.encode().ljust(16, b"\0")


def segment(name, vmaddr, fileoff, filesize, sections=()):
    body = struct.pack("<16sQQQQiiII", name16(name), vmaddr, 0x1000, fileoff, filesize, 7, 5, len(sections), 0)
    for sect, owner, offset, size, flags in sections:
        body += struct.pack("<16s16sQQIIIIIIII", name16(sect), name16(owner), vmaddr, size, offset, 4, 0, 0, flags, 0, 0, 0)
    return struct.pack("<II", 0x19, 8 + len(body)) + body


def macho(cputype, uuid, signature, text=TEXT, cryptid=None):
    """A thin Mach-O 64: __TEXT/__text, __DATA/__bss (zerofill), __LINKEDIT = symbols + signature."""
    commands_size = (72 + 80) + (72 + 80) + 72 + 24 + 16 + (24 if cryptid is not None else 0)
    text_off = 32 + commands_size
    link_off = text_off + len(text)
    commands = [
        segment("__TEXT", 0x1000, text_off, len(text), [("__text", "__TEXT", text_off, len(text), 0x400)]),
        segment("__DATA", 0x2000, 0, 0, [("__bss", "__DATA", 0, 64, 0x1)]),
        segment("__LINKEDIT", 0x3000, link_off, len(SYMBOLS) + len(signature)),
        struct.pack("<II16s", 0x1B, 24, uuid),
        struct.pack("<IIII", 0x1D, 16, link_off + len(SYMBOLS), len(signature))]
    if cryptid is not None:
        commands.append(struct.pack("<IIIIII", 0x2C, 24, text_off, len(text), cryptid, 0))
    blob = b"".join(commands)
    assert len(blob) == commands_size
    return (struct.pack("<IiiIIIII", 0xFEEDFACF, cputype, 3, 6, len(commands), len(blob), 0, 0) +
            blob + text + SYMBOLS + signature)


def fat(*slices):
    """Fat32 big-endian header with 16-byte aligned slices; slices are (cputype, bytes)."""
    offset = 8 + 20 * len(slices)
    entries, body = b"", b""
    for cputype, data in slices:
        pad = (-offset) % 16
        offset += pad
        body += b"\0" * pad + data
        entries += struct.pack(">iiIII", cputype, 3, offset, len(data), 4)
        offset += len(data)
    return struct.pack(">II", 0xCAFEBABE, len(slices)) + entries + body


def metadata(version=31, bump=None):
    """A 256-byte header, then table n holds the byte n+1 repeated n+1 times (bump changes one byte)."""
    tables, blob, cursor = [], b"", 256
    for index in range(31):
        data = bytes([index + 1]) * (index + 1)
        if index == bump:
            data = data[:-1] + b"\xff"
        tables.append((cursor, len(data)))
        blob += data
        cursor += len(data)
    header = struct.pack("<Ii", 0xFAB11BAF, version) + b"".join(struct.pack("<II", *pair) for pair in tables)
    return header + blob


def make_app(root, uuid=UUID_A, signature=SIGNATURE_A, text=TEXT, meta=None, resource="alpha", fat_binary=True):
    app = Path(root) / "Test.app"
    (app / "Contents" / "Frameworks").mkdir(parents=True)
    (app / "Contents" / "_CodeSignature").mkdir()
    metadata_dir = app / "Contents" / "Resources" / "Data" / "il2cpp_data" / "Metadata"
    metadata_dir.mkdir(parents=True)
    x86 = macho(X86_64, uuid, signature, text)
    arm = macho(ARM64, bytes(reversed(uuid)), signature, text, cryptid=0)
    binary = fat((X86_64, x86), (ARM64, arm)) if fat_binary else x86
    (app / "Contents" / "Frameworks" / "GameAssembly.dylib").write_bytes(binary)
    (metadata_dir / "global-metadata.dat").write_bytes(meta or metadata())
    (app / "Contents" / "_CodeSignature" / "CodeResources").write_bytes(signature)
    (app / "Contents" / "Resources" / "a.txt").write_text(resource)
    os.symlink("a.txt", app / "Contents" / "Resources" / "link")
    os.symlink("Resources", app / "Contents" / "dirlink")
    return app


class ParsingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def test_fat_binary_slices_sections_uuid_and_signature(self):
        report = inputfingerprint.fingerprint_input(make_app(self.tmp.name))
        assembly = report["game_assembly"]
        self.assertTrue(assembly["fat"])
        x86, arm = assembly["slices"]
        self.assertEqual(("x86_64", "arm64"), (x86["arch"], arm["arch"]))
        self.assertEqual(UUID_A.hex(), x86["uuid"])
        self.assertEqual(len(SIGNATURE_A), x86["code_signature"]["datasize"])
        self.assertIsNone(x86["encryption"])
        self.assertEqual(0, arm["encryption"]["cryptid"])
        self.assertEqual(["__TEXT", "__DATA", "__LINKEDIT"], [s["segname"] for s in x86["segments"]])
        text = x86["segments"][0]["sections"][0]
        self.assertEqual(("__text", "__TEXT", 42, sha(b"\x90" * 32 + b"TEXT-BYTES")),
                         (text["sectname"], text["segname"], text["size"], text["sha256"]))
        bss = x86["segments"][1]["sections"][0]
        self.assertEqual((True, None, 64), (bss["zerofill"], bss["sha256"], bss["size"]))
        self.assertEqual(sha(b"SYMBOLS!" * 2), x86["linkedit"]["sha256_without_signature"])
        self.assertEqual(sha(b"SYMBOLS!" * 2 + SIGNATURE_A), x86["linkedit"]["sha256_with_signature"])
        self.assertEqual(sha(macho(X86_64, UUID_A, SIGNATURE_A)), x86["sha256"])
        self.assertEqual(sha((Path(self.tmp.name) / "Test.app/Contents/Frameworks/GameAssembly.dylib").read_bytes()),
                         assembly["sha256"])

    def test_thin_binary(self):
        assembly = inputfingerprint.fingerprint_input(make_app(self.tmp.name, fat_binary=False))["game_assembly"]
        self.assertFalse(assembly["fat"])
        self.assertEqual([("x86_64", 0)], [(s["arch"], s["offset"]) for s in assembly["slices"]])

    def test_manifest_records_symlinks_without_following_them(self):
        manifest = inputfingerprint.fingerprint_input(make_app(self.tmp.name))["manifest"]
        paths = [item["path"] for item in manifest["files"]]
        self.assertEqual(sorted(paths), paths)
        self.assertIn({"path": "Contents/Resources/a.txt", "size": 5, "sha256": sha(b"alpha")}, manifest["files"])
        self.assertEqual([{"path": "Contents/Resources/link", "target": "a.txt"},
                          {"path": "Contents/dirlink", "target": "Resources"}], manifest["symlinks"])
        self.assertNotIn("Contents/Resources/link", paths)
        self.assertFalse([path for path in paths if path.startswith("Contents/dirlink/")])

    def test_metadata_tables_in_header_order(self):
        meta = inputfingerprint.fingerprint_input(make_app(self.tmp.name))["metadata"]
        self.assertEqual((0xFAB11BAF, 31, 256), (meta["sanity"], meta["version"], meta["header_size"]))
        self.assertEqual(31, len(meta["tables"]))
        self.assertEqual({"offset": 256, "size": 1, "sha256": sha(b"\x01")}, meta["tables"]["stringLiteral"])
        self.assertEqual(sha(b"\x14" * 20), meta["tables"]["typeDefinitions"]["sha256"])
        self.assertEqual(sha(b"\x1f" * 31), meta["tables"]["exportedTypeDefinitions"]["sha256"])
        self.assertEqual(256 + sum(range(1, 31)), meta["tables"]["exportedTypeDefinitions"]["offset"])

    def test_declared_variant_is_none_for_unknown_pair(self):
        self.assertIsNone(inputfingerprint.fingerprint_input(make_app(self.tmp.name))["variant"])

    def test_bad_macho_magic_and_unknown_metadata_version_are_rejected(self):
        bad = Path(self.tmp.name) / "bad"
        bad.write_bytes(b"MZ\x90\x00" + b"\0" * 64)
        with self.assertRaisesRegex(ValueError, "Unsupported Mach-O magic 0x4d5a9000"):
            inputfingerprint.fingerprint_macho(bad)
        for version in (24, 29):
            path = Path(self.tmp.name) / "meta{}".format(version)
            path.write_bytes(metadata(version))
            with self.assertRaisesRegex(ValueError, "Unsupported global-metadata.dat version {}".format(version)):
                inputfingerprint.fingerprint_metadata(path)
        wrong = Path(self.tmp.name) / "sanity"
        wrong.write_bytes(b"\0" * 300)
        with self.assertRaisesRegex(ValueError, "sanity"):
            inputfingerprint.fingerprint_metadata(wrong)

    def test_metadata_header_pair_count_must_match_the_version(self):
        path = Path(self.tmp.name) / "short"
        blob = bytearray(metadata())
        struct.pack_into("<I", blob, 8, 248)  # a first table offset that implies 30 pairs
        path.write_bytes(bytes(blob))
        with self.assertRaisesRegex(ValueError, "30 pairs"):
            inputfingerprint.fingerprint_metadata(path)


class CompareTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.counter = 0

    def report(self, **kwargs):
        self.counter += 1
        root = Path(self.tmp.name) / str(self.counter)
        root.mkdir()
        return inputfingerprint.fingerprint_input(make_app(root, **kwargs))

    def test_identical_reports(self):
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.report())
        self.assertEqual([], verdict["differences"])
        self.assertTrue(verdict["summary"]["identical"])

    def test_only_signature_and_uuid_differ(self):
        verdict = inputfingerprint.compare_fingerprints(
            self.report(), self.report(uuid=UUID_B, signature=SIGNATURE_B))
        self.assertTrue(verdict["summary"]["packaging_only"], verdict["differences"])
        self.assertFalse(verdict["summary"]["identical"])
        details = {d["detail"] for d in verdict["differences"]}
        self.assertTrue({"LC_UUID", "LC_CODE_SIGNATURE"} <= details)
        changed = [d["path"] for d in verdict["differences"] if d["area"] == "manifest"]
        self.assertEqual(["Contents/Frameworks/GameAssembly.dylib", "Contents/_CodeSignature/CodeResources"], sorted(changed))

    def test_different_declared_variants_alone_stay_packaging_only(self):
        # Two declared variants always differ in their binary hash, so the name alone must not
        # decide the verdict; the per-section and per-table comparisons do.
        left, right = self.report(), self.report(uuid=UUID_B, signature=SIGNATURE_B)
        left["variant"], right["variant"] = "6c89-acb", "e94d-88af"
        verdict = inputfingerprint.compare_fingerprints(left, right)
        self.assertTrue(verdict["summary"]["packaging_only"], verdict["differences"])
        self.assertEqual(["6c89-acb", "e94d-88af"], verdict["variants"])
        self.assertIn("variant", {d["area"] for d in verdict["differences"]})

    def test_one_text_byte_names_exactly_that_section(self):
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.report(text=TEXT[:-1] + b"X"))
        self.assertFalse(verdict["summary"]["packaging_only"])
        real = [d for d in verdict["differences"] if not d["packaging"]]
        self.assertEqual([("slice arm64", "section sha256", "__TEXT", "__text"),
                          ("slice arm64", "slice bytes outside LINKEDIT, UUID and signature fields", None, None),
                          ("slice x86_64", "section sha256", "__TEXT", "__text"),
                          ("slice x86_64", "slice bytes outside LINKEDIT, UUID and signature fields", None, None)],
                         sorted((d["area"], d["detail"], d.get("segname"), d.get("sectname")) for d in real))

    def test_one_metadata_table_byte_names_exactly_that_table(self):
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.report(meta=metadata(bump=19)))
        self.assertFalse(verdict["summary"]["packaging_only"])
        real = [d for d in verdict["differences"] if not d["packaging"]]
        self.assertEqual([("manifest", "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat")],
                         [(d["area"], d["path"]) for d in real if d["area"] == "manifest"])
        self.assertEqual([("table sha256", "typeDefinitions")],
                         [(d["detail"], d["table"]) for d in real if d["area"] == "global-metadata.dat"])
        self.assertEqual(2, len(real))

    def test_changed_resource_is_not_packaging(self):
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.report(resource="beta"))
        self.assertFalse(verdict["summary"]["packaging_only"])
        self.assertEqual([("manifest", "changed", "Contents/Resources/a.txt")],
                         [(d["area"], d["detail"], d["path"]) for d in verdict["differences"] if not d["packaging"]])

    def test_unexplained_slice_byte_change_is_not_packaging(self):
        a = self.report()
        b = json.loads(json.dumps(a))
        b["game_assembly"]["slices"][0]["residual_sha256"] = "0" * 64
        self.assertFalse(inputfingerprint.compare_fingerprints(a, b)["summary"]["packaging_only"])

    def test_cli_round_trip(self):
        app = make_app(self.tmp.name)
        out = Path(self.tmp.name) / "report.json"
        self.assertEqual(0, lucid.main(["fingerprint-input", str(app), "--output", str(out)]))
        verdict = Path(self.tmp.name) / "verdict.json"
        with mock.patch("builtins.print"):
            self.assertEqual(0, lucid.main(["compare-input-fingerprints", str(out), str(out), "--output", str(verdict)]))
        self.assertTrue(json.loads(verdict.read_text())["summary"]["identical"])


if __name__ == "__main__":
    unittest.main()

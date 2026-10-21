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


def fat(*slices, extra_pad=0):
    """Fat32 big-endian header with 16-byte aligned slices; slices are (cputype, bytes[, cpusubtype]).

    extra_pad adds that many zero bytes (a multiple of 16) before every slice after the first.
    """
    offset = 8 + 20 * len(slices)
    entries, body = b"", b""
    for number, (cputype, data, *subtype) in enumerate(slices):
        pad = (-offset) % 16 + (extra_pad if number else 0)
        offset += pad
        body += b"\0" * pad + data
        entries += struct.pack(">iiIII", cputype, subtype[0] if subtype else 3, offset, len(data), 4)
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
    for path in app.rglob("*"):
        if path.is_file():
            path.chmod(0o644)  # independent of the umask, since modes are part of the manifest
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
        self.assertEqual([sha(TEXT), None, sha(SYMBOLS + SIGNATURE_A)], [s["sha256"] for s in x86["segments"]])
        self.assertEqual((0xCAFEBABE, 2, [4, 4]), (assembly["magic"], assembly["count"], [s["align"] for s in assembly["slices"]]))
        self.assertEqual(64, len(assembly["container_sha256"]))
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
        self.assertIn({"path": "Contents/Resources/a.txt", "size": 5, "sha256": sha(b"alpha"), "mode": 0o644}, manifest["files"])
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
                          ("slice arm64", "segment sha256", "__TEXT", None),
                          ("slice arm64", "slice bytes outside LINKEDIT, UUID and signature fields", None, None),
                          ("slice x86_64", "section sha256", "__TEXT", "__text"),
                          ("slice x86_64", "segment sha256", "__TEXT", None),
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

    def binary_report(self, mutate):
        """Report for an app whose GameAssembly bytes were edited in place by mutate(bytearray)."""
        self.counter += 1
        root = Path(self.tmp.name) / str(self.counter)
        root.mkdir()
        app = make_app(root)
        dylib = app / inputfingerprint.inputvariants.GAME_ASSEMBLY
        blob = bytearray(dylib.read_bytes())
        mutate(blob)
        dylib.write_bytes(bytes(blob))
        return inputfingerprint.fingerprint_input(app)

    def real(self, verdict):
        return [(d["area"], d["detail"]) for d in verdict["differences"] if not d["packaging"]]

    def test_flipped_fat_padding_byte_is_not_packaging(self):
        # x86_64 slice ends at 574 and the arm64 slice starts at 576, so 574..575 is alignment padding.
        def flip(blob):
            self.assertEqual(b"\0\0", bytes(blob[574:576]))
            blob[574] = 1
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.binary_report(flip))
        self.assertFalse(verdict["summary"]["packaging_only"])
        self.assertIn(("GameAssembly.dylib", "bytes outside the slices (fat header and padding)"), self.real(verdict))

    def test_relocated_slice_with_zero_padding_is_not_packaging(self):
        left = self.write_fat(fat((X86_64, macho(X86_64, UUID_A, SIGNATURE_A)), (ARM64, macho(ARM64, UUID_A, SIGNATURE_A))))
        right = self.write_fat(fat((X86_64, macho(X86_64, UUID_A, SIGNATURE_A)), (ARM64, macho(ARM64, UUID_A, SIGNATURE_A)),
                                   extra_pad=16))
        self.assertEqual([True, True], [s["canonical"] for s in left["slices"]])
        self.assertEqual([True, False], [s["canonical"] for s in right["slices"]])
        verdict = inputfingerprint.compare_fingerprints(self.wrap(left), self.wrap(right))
        self.assertFalse(verdict["summary"]["packaging_only"])
        self.assertIn(("slice arm64", "slice offset (not at its canonical position)"), self.real(verdict))

    def write_fat(self, blob):
        path = Path(self.tmp.name) / "relocated{}".format(self.counter)
        self.counter += 1
        path.write_bytes(blob)
        return inputfingerprint.fingerprint_macho(path)

    def wrap(self, assembly):
        return dict(self.report(), game_assembly=dict(assembly, sha256="x", path="p"))

    def test_size_change_moves_canonical_offsets_as_packaging(self):
        # A longer signature in the first slice shifts the second slice; both stay canonical, so it is explained.
        left = self.write_fat(fat((X86_64, macho(X86_64, UUID_A, SIGNATURE_A)), (ARM64, macho(ARM64, UUID_A, SIGNATURE_A))))
        right = self.write_fat(fat((X86_64, macho(X86_64, UUID_A, SIGNATURE_B)), (ARM64, macho(ARM64, UUID_A, SIGNATURE_A))))
        verdict = inputfingerprint.compare_fingerprints(self.wrap(left), self.wrap(right))
        self.assertIn(("slice arm64", "slice offset (follows from other differences)"),
                      [(d["area"], d["detail"]) for d in verdict["differences"]])
        self.assertNotIn("slice offset", " ".join(detail for _, detail in self.real(verdict)))

    def test_trailing_bytes_after_the_last_slice_are_not_packaging(self):
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.binary_report(lambda blob: blob.extend(b"\0")))
        self.assertFalse(verdict["summary"]["packaging_only"])

    def test_fat_align_field_is_not_packaging(self):
        verdict = inputfingerprint.compare_fingerprints(
            self.report(), self.binary_report(lambda blob: blob.__setitem__(slice(27, 28), b"\x05")))
        self.assertIn(("slice x86_64", "fat align"), self.real(verdict))

    def test_fat_magic_and_count_are_compared(self):
        a = self.report()
        b = json.loads(json.dumps(a))
        b["game_assembly"]["magic"], b["game_assembly"]["count"] = 0xCAFEBABF, 3
        details = [d for _, d in self.real(inputfingerprint.compare_fingerprints(a, b))]
        self.assertIn("fat magic", details)
        self.assertIn("fat architecture count", details)

    def test_linkedit_extent_without_a_signature_change_is_not_packaging(self):
        def grow(blob):  # x86_64 slice at 48; __LINKEDIT command at 336, vmsize at +8+24
            blob[48 + 368] = 0x20
        verdict = inputfingerprint.compare_fingerprints(self.report(), self.binary_report(grow))
        self.assertFalse(verdict["summary"]["packaging_only"])
        self.assertIn(("slice x86_64", "segment vmsize"), self.real(verdict))

    def test_linkedit_vmaddr_and_fileoff_are_always_compared(self):
        a = self.report()
        b = json.loads(json.dumps(a))
        b["game_assembly"]["slices"][0]["segments"][2].update(vmaddr=0x9000, fileoff=1)
        b2 = self.report(uuid=UUID_B, signature=SIGNATURE_B)
        b2["game_assembly"]["slices"][0]["segments"][2].update(vmaddr=0x9000, fileoff=1)
        for other in (b, b2):
            details = [d for _, d in self.real(inputfingerprint.compare_fingerprints(a, other))]
            self.assertIn("segment vmaddr", details)
            self.assertIn("segment fileoff", details)

    def test_non_linkedit_segment_hash_is_not_packaging(self):
        a = self.report()
        b = json.loads(json.dumps(a))
        b["game_assembly"]["slices"][1]["segments"][0]["sha256"] = "0" * 64
        self.assertEqual([("slice arm64", "segment sha256")], self.real(inputfingerprint.compare_fingerprints(a, b)))

    def test_linkedit_segment_hash_change_needs_equal_unsigned_bytes(self):
        a = self.report()
        b = json.loads(json.dumps(a))
        linkedit = b["game_assembly"]["slices"][0]["segments"][2]
        linkedit["sha256"] = "0" * 64
        self.assertTrue(inputfingerprint.compare_fingerprints(a, b)["summary"]["packaging_only"])
        b["game_assembly"]["slices"][0]["linkedit"]["sha256_without_signature"] = "1" * 64
        self.assertFalse(inputfingerprint.compare_fingerprints(a, b)["summary"]["packaging_only"])

    def test_arm64_and_arm64e_slices_are_kept_apart(self):
        path = Path(self.tmp.name) / "multi"
        arm, arme = macho(ARM64, UUID_A, SIGNATURE_A), macho(ARM64, UUID_B, SIGNATURE_A, text=TEXT[:-1] + b"X")
        path.write_bytes(fat((ARM64, arm, 0), (ARM64, arme, 2)))
        slices = inputfingerprint.fingerprint_macho(path)["slices"]
        self.assertEqual([(ARM64, 0), (ARM64, 2)], [(s["cputype"], s["cpusubtype"]) for s in slices])
        reports = []
        for blob in (fat((ARM64, arm, 0), (ARM64, arme, 2)), fat((ARM64, arm, 0), (ARM64, macho(ARM64, UUID_B, SIGNATURE_A), 2))):
            reports.append(dict(self.report(), game_assembly=None))
            path.write_bytes(blob)
            reports[-1]["game_assembly"] = dict(inputfingerprint.fingerprint_macho(path), sha256="x", path="p")
        verdict = inputfingerprint.compare_fingerprints(*reports)
        areas = {d["area"] for d in verdict["differences"] if not d["packaging"]}
        self.assertEqual({"slice arm64 (subtype 0x2)"}, areas)

    def test_repeated_cpu_pair_in_the_fat_table_is_rejected(self):
        path = Path(self.tmp.name) / "dup"
        arm = macho(ARM64, UUID_A, SIGNATURE_A)
        path.write_bytes(fat((ARM64, arm), (ARM64, arm)))
        with self.assertRaisesRegex(ValueError, "repeats"):
            inputfingerprint.fingerprint_macho(path)

    def test_malformed_slice_in_a_fat_file_raises_value_error(self):
        path = Path(self.tmp.name) / "overrun"
        blob = bytearray(fat((X86_64, macho(X86_64, UUID_A, SIGNATURE_A))))
        struct.pack_into("<Q", blob, 32 + 152, 10 ** 6)  # __text section size
        path.write_bytes(bytes(blob))
        with self.assertRaisesRegex(ValueError, "overruns the slice"):
            inputfingerprint.fingerprint_macho(path)

    def test_load_command_header_outside_sizeofcmds_is_a_value_error(self):
        path = Path(self.tmp.name) / "short"
        blob = bytearray(macho(X86_64, UUID_A, SIGNATURE_A))
        struct.pack_into("<II", blob, 16, 1, 0)  # ncmds=1, sizeofcmds=0
        path.write_bytes(bytes(blob))
        with self.assertRaisesRegex(ValueError, "outside sizeofcmds"):
            inputfingerprint.fingerprint_macho(path)

    def test_short_load_command_payloads_are_value_errors(self):
        def broken(name, edit):
            path = Path(self.tmp.name) / name
            blob = bytearray(macho(X86_64, UUID_A, SIGNATURE_A))
            edit(blob)
            path.write_bytes(bytes(blob))
            return path
        # __TEXT is the first command at 32: cmdsize at 36, nsects at 32 + 8 + 56.
        cases = {"segment8": (lambda b: struct.pack_into("<I", b, 36, 8), None),
                 "nsects": (lambda b: struct.pack_into("<I", b, 96, 5), None),
                 # UUID is the 4th command: 32 + 152 + 152 + 72.
                 "uuid": (lambda b: struct.pack_into("<I", b, 412, 8), None)}
        for name, (edit, _) in cases.items():
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, "cmdsize"):
                    inputfingerprint.fingerprint_macho(broken(name, edit))

    def test_symlinked_critical_files_are_refused(self):
        outside = Path(self.tmp.name) / "outside"
        outside.mkdir()
        for relative in ("Contents/Frameworks/GameAssembly.dylib", "Contents/Resources/Data/il2cpp_data"):
            with self.subTest(relative=relative):
                app = make_app(Path(self.tmp.name) / relative.replace("/", "_"))
                target = app / relative
                moved = outside / target.name
                target.rename(moved)
                target.symlink_to(moved)
                with self.assertRaisesRegex(ValueError, "symlink"):
                    inputfingerprint.fingerprint_input(app)

    @unittest.skipIf(hasattr(os, "geteuid") and os.geteuid() == 0, "root can read every directory")
    def test_unreadable_directory_fails_instead_of_shrinking_the_manifest(self):
        app = make_app(self.tmp.name)
        locked = app / "Contents" / "Resources" / "locked"
        locked.mkdir()
        (locked / "hidden.txt").write_text("x")
        locked.chmod(0)
        self.addCleanup(locked.chmod, 0o755)
        with self.assertRaises(OSError):
            inputfingerprint.build_manifest(app)

    def test_code_signature_symlink_changes_are_packaging(self):
        a = self.report()
        self.counter += 1
        root = Path(self.tmp.name) / str(self.counter)
        root.mkdir()
        app = make_app(root)
        os.symlink("CodeResources", app / "Contents" / "_CodeSignature" / "alias")
        b = inputfingerprint.fingerprint_input(app)
        verdict = inputfingerprint.compare_fingerprints(a, b)
        self.assertEqual([("manifest", "symlink", True)], [(d["area"], d["detail"], d["packaging"]) for d in verdict["differences"]])
        c = json.loads(json.dumps(a))
        c["manifest"]["symlinks"].append({"path": "Contents/Resources/alias", "target": "a.txt"})
        self.assertFalse(inputfingerprint.compare_fingerprints(a, c)["summary"]["packaging_only"])

    def test_removed_exec_bit_is_not_packaging(self):
        a = self.report()
        self.counter += 1
        root = Path(self.tmp.name) / str(self.counter)
        root.mkdir()
        app = make_app(root)
        (app / "Contents" / "Resources" / "a.txt").chmod(0o755)
        os.chmod(app / "Contents" / "Frameworks" / "GameAssembly.dylib", 0o600)
        (app / "Contents" / "_CodeSignature" / "CodeResources").chmod(0o600)
        verdict = inputfingerprint.compare_fingerprints(a, inputfingerprint.fingerprint_input(app))
        modes = [(d["path"], d["packaging"]) for d in verdict["differences"] if d["detail"] == "mode"]
        self.assertEqual([("Contents/Frameworks/GameAssembly.dylib", False), ("Contents/Resources/a.txt", False),
                          ("Contents/_CodeSignature/CodeResources", True)], sorted(modes))
        self.assertFalse(verdict["summary"]["packaging_only"])

    def test_critical_file_changing_after_the_manifest_is_refused(self):
        app = make_app(self.tmp.name)
        real = inputfingerprint.build_manifest

        def stale(path):
            manifest = real(path)
            for item in manifest["files"]:
                if item["path"] == inputfingerprint.inputvariants.GAME_ASSEMBLY:
                    item["sha256"] = "0" * 64
            return manifest
        with mock.patch.object(inputfingerprint, "build_manifest", stale):
            with self.assertRaisesRegex(ValueError, "changed while fingerprinting"):
                inputfingerprint.fingerprint_input(app)

    def test_cli_refuses_to_overwrite_an_input_report(self):
        app = make_app(self.tmp.name)
        report = Path(self.tmp.name) / "report.json"
        other = Path(self.tmp.name) / "other.json"
        self.assertEqual(0, lucid.main(["fingerprint-input", str(app), "--output", str(report)]))
        other.write_bytes(report.read_bytes())
        before = report.read_bytes()
        link = Path(self.tmp.name) / "alias.json"
        link.symlink_to(report)
        for target in (report, link, Path(self.tmp.name) / "." / "report.json"):
            for pair in ((report, other), (other, report)):
                with self.subTest(output=str(target), pair=pair[0].name), mock.patch("builtins.print"), mock.patch("sys.stderr"):
                    if target.resolve() not in (pair[0].resolve(), pair[1].resolve()):
                        continue
                    self.assertNotEqual(0, lucid.main(["compare-input-fingerprints", str(pair[0]), str(pair[1]),
                                                       "--output", str(target)]))
                    self.assertEqual(before, report.read_bytes())

    def test_directory_mode_and_presence_are_compared(self):
        a = self.report()
        self.counter += 1
        root = Path(self.tmp.name) / str(self.counter)
        root.mkdir()
        app = make_app(root)
        (app / "Contents" / "Resources").chmod(0o700)
        (app / "Contents" / "Extra").mkdir()
        try:
            verdict = inputfingerprint.compare_fingerprints(a, inputfingerprint.fingerprint_input(app))
        finally:
            (app / "Contents" / "Resources").chmod(0o755)
        dirs = sorted((d["path"], d["packaging"]) for d in verdict["differences"] if d["detail"] == "directory")
        self.assertIn(("Contents/Extra", False), dirs)
        self.assertIn(("Contents/Resources", False), dirs)
        self.assertFalse(verdict["summary"]["packaging_only"])

    def test_section_address_differences_name_the_section(self):
        a, b = self.report(), self.report()
        b["game_assembly"]["slices"][0]["segments"][0]["sections"][0]["addr"] += 0x10
        verdict = inputfingerprint.compare_fingerprints(a, b)
        addrs = [(d["sectname"], d["packaging"]) for d in verdict["differences"] if d["detail"] == "section addr"]
        self.assertEqual([("__text", False)], addrs)

    def test_malformed_reports_are_value_errors(self):
        good = self.report()
        for broken in ({"format": inputfingerprint.FORMAT},
                       dict(good, manifest=None),
                       dict(good, game_assembly={"fat": True})):
            with self.subTest(keys=sorted(broken)):
                with self.assertRaisesRegex(ValueError, "Malformed fingerprint report"):
                    inputfingerprint.compare_fingerprints(broken, good)

    def test_duplicate_segment_names_are_rejected(self):
        path = Path(self.tmp.name) / "dup"
        blob = bytearray(macho(X86_64, UUID_A, SIGNATURE_A))
        struct.pack_into("<16s", blob, 32 + 152 + 8, name16("__TEXT"))  # rename __DATA
        path.write_bytes(bytes(blob))
        with self.assertRaisesRegex(ValueError, "Duplicate segment __TEXT"):
            inputfingerprint.fingerprint_macho(path)

    def test_older_format_reports_are_rejected(self):
        for number in (1, 2, 3):
            old = dict(self.report(), format=number)
            with self.assertRaisesRegex(ValueError, "Unsupported fingerprint format {}".format(number)):
                inputfingerprint.compare_fingerprints(old, self.report())

    def test_cli_refuses_an_output_inside_the_bundle(self):
        app = make_app(self.tmp.name)
        out = app / "Contents" / "report.json"
        with mock.patch("sys.stderr"):
            self.assertNotEqual(0, lucid.main(["fingerprint-input", str(app), "--output", str(out)]))
        self.assertFalse(out.exists())

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

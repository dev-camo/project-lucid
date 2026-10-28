"""Synthetic fixtures for the AssetRipper inertness comparison; no real assembly is read or run."""

from pathlib import Path
import shutil
import struct
import tempfile
import unittest

from tools import assetripper_identity_compare as compare

GUID_A = "0123456789abcdef0123456789abcdef"
GUID_B = "fedcba9876543210fedcba9876543210"
DLL = "AuxiliaryFiles/GameAssemblies/Game.dll"


def assembly(mvid: bytes, tail: bytes = b"code") -> bytes:
    """A minimal PE with one section holding a CLI header, a metadata root with #~ and #GUID, and `tail`."""
    section = bytearray(0x200)
    struct.pack_into("<I", section, 8, 0x1100)  # CLI header: metadata RVA
    root = 0x100
    section[root:root + 4] = b"BSJB"
    struct.pack_into("<I", section, root + 12, 12)
    section[root + 16:root + 28] = b"v4.0.30319\0\0"
    cursor = root + 16 + 12 + 2
    tables = bytes(6) + b"\x00\x00" + struct.pack("<QQ", 1, 0) + struct.pack("<I", 1) + struct.pack("<HHHHH", 0, 1, 1, 0, 0)
    streams = [(b"#~\0\0", 0x40, tables), (b"#GUID\0\0\0", 0x80, mvid)]
    struct.pack_into("<H", section, cursor, len(streams))
    cursor += 2
    for name, offset, body in streams:
        struct.pack_into("<II", section, cursor, offset, len(body))
        section[cursor + 8:cursor + 8 + len(name)] = name
        cursor += 8 + len(name)
        section[root + offset:root + offset + len(body)] = body
    section[0x1F0:0x1F0 + len(tail)] = tail
    pe = 0x80
    header = bytearray(0x200)
    struct.pack_into("<I", header, 0x3C, pe)
    header[pe:pe + 4] = b"PE\0\0"
    struct.pack_into("<HH", header, pe + 4, 0x14C, 1)  # machine, one section
    struct.pack_into("<H", header, pe + 20, 224)  # optional header size
    struct.pack_into("<H", header, pe + 24, 0x10B)
    struct.pack_into("<I", header, pe + 24 + 96 + 14 * 8, 0x1000)  # CLI data directory
    struct.pack_into("<IIII", header, pe + 24 + 224 + 8, 0x200, 0x1000, 0x200, 0x200)
    return bytes(header) + bytes(section)


class IdentityCompareTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="test-identitycompare-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)

    def export(self, name, files):
        base = self.root / name
        for relative, data in files.items():
            (base / relative).parent.mkdir(parents=True, exist_ok=True)
            (base / relative).write_bytes(data)
        return base

    def run_compare(self, patched, baseline):
        return compare.compare(self.export("p", dict(patched, **{compare.IDENTITY: b"{}"})), self.export("b", baseline))

    def test_the_synthetic_assembly_exposes_exactly_its_mvid(self):
        data = assembly(b"M" * 16)
        start, end = compare.mvid_range(data)
        self.assertEqual(b"M" * 16, data[start:end])

    def test_an_assembly_differing_only_in_its_mvid_is_accepted(self):
        result = self.run_compare({DLL: assembly(b"A" * 16)}, {DLL: assembly(b"B" * 16)})
        self.assertTrue(result["ok"], result)
        self.assertEqual(1, result["counts"]["gameassembly_mvid_only"])

    def test_a_small_difference_outside_the_mvid_is_not_accepted(self):
        # Codex finding: any change within 16 bytes used to count as the MVID.
        result = self.run_compare({DLL: assembly(b"A" * 16, b"code")}, {DLL: assembly(b"A" * 16, b"c0de")})
        self.assertFalse(result["ok"])
        result = self.run_compare({DLL: assembly(b"A" * 16, b"code")}, {DLL: assembly(b"B" * 16, b"c0de")})
        self.assertFalse(result["ok"])
        result = self.run_compare({DLL: b"MZ not an assembly 1"}, {DLL: b"MZ not an assembly 2"})
        self.assertFalse(result["ok"])

    def test_script_guids_must_be_equal_and_other_guids_may_be_fresh(self):
        meta = lambda guid: f"fileFormatVersion: 2\nguid: {guid}\ntimeCreated: 1\n".encode()
        same = self.run_compare({"Assets/A.cs.meta": meta(GUID_A), "Assets/B.mat.meta": meta(GUID_A)},
                                {"Assets/A.cs.meta": meta(GUID_A), "Assets/B.mat.meta": meta(GUID_B)})
        self.assertTrue(same["ok"], same)
        # Codex finding: a changed script GUID was normalized away like a random one.
        changed = self.run_compare({"Assets/A.cs.meta": meta(GUID_A)}, {"Assets/A.cs.meta": meta(GUID_B)})
        self.assertFalse(changed["ok"])
        self.assertEqual(["Assets/A.cs.meta"], changed["other_differences"])


if __name__ == "__main__":
    unittest.main()

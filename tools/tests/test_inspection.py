"""Synthetic fixtures test parsers without distributing game files."""

import hashlib
import json
import plistlib
import struct
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from sdtlib.inspection import inspect_bundle, inspect_catalog
from sdtlib.macho import inspect_macho
from sdtlib.metadata import parse_metadata


def metadata_fixture():
    names = ["", "Game.Runtime.dll", "Game.Runtime", "Example", "Motor", "velocity", ".ctor", "Move", "input"]
    strings = b""
    offsets = {}
    for name in names:
        offsets[name] = len(strings)
        strings += name.encode() + b"\0"
    sections = [b""] * 31
    sections[2] = strings
    sections[5] = (
        struct.pack("<iiiIiiI4H", offsets[".ctor"], 0, 20, 0x08000000, -1, -1,
                    0x06000001, 0x86, 0, 65535, 0) +
        struct.pack("<iiiIiiI4H", offsets["Move"], 0, 20, 0x08000000, 0, -1,
                    0x06000002, 0x86, 0, 65535, 1))
    sections[8] = (b"Library/PackageCache/com.unity.addressables@1.22.3/Runtime/Thing.cs\0"
                   b"Assets/Scripts/Motor.cs\0")
    sections[10] = struct.pack("<iIi", offsets["input"], 0x08000001, 21)
    sections[11] = struct.pack("<iiI", offsets["velocity"], 22, 0x04000001)
    type_ints = [offsets["Motor"], offsets["Example"], 10, -1, -1, -1, -1, 1,
                 0, 0, -1, -1, -1, -1, 0, -1]
    sections[19] = struct.pack("<16i8H2I", *type_ints, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0x02000001)
    sections[20] = struct.pack("<10i", offsets["Game.Runtime.dll"], 0, 0, 1, -1, 0, -1, 0x20000001, 0, 0)
    assembly_ints = [0, 0x20000001, -1, 0, offsets["Game.Runtime"], 0, 0, 0, 0, 0, 1, 2, 3, 4]
    sections[21] = struct.pack("<14iQ", *assembly_ints, 0)
    header = [0xFAB11BAF, 31]
    body = b""
    for section in sections:
        header += [256 + len(body), len(section)]
        body += section
    return struct.pack("<64I", *header) + body


def macho_fixture(cpu=0x0100000C, cryptid=None):
    names = ["_g_CodeRegistration", "_Motor_Move_m" + "A" * 40]
    strings = b"\0"
    indices = []
    for name in names:
        indices.append(len(strings))
        strings += name.encode() + b"\0"
    encryption = b"" if cryptid is None else struct.pack("<6I", 0x2C, 24, 0, 4, cryptid, 0)
    command_size = 24 + len(encryption)
    symoff = 32 + command_size
    symbol_rows = b"".join(struct.pack("<IBBHQ", index, 0x0E, 1, 0, address)
                           for index, address in zip(indices, (0x1000, 0x2000)))
    commands = struct.pack("<6I", 2, 24, symoff, 2, symoff + len(symbol_rows), len(strings)) + encryption
    header = struct.pack("<IiiIIIII", 0xFEEDFACF, cpu, 0, 6, 1 + bool(encryption), command_size, 0, 0)
    return header + commands + symbol_rows + strings


def universal_fixture():
    first = macho_fixture(0x01000007)
    second = macho_fixture(0x0100000C)
    offset = 48
    return (struct.pack(">II", 0xCAFEBABE, 2) +
            struct.pack(">iiIII", 0x01000007, 0, offset, len(first), 0) +
            struct.pack(">iiIII", 0x0100000C, 0, offset + len(first), len(second), 0) + first + second)


class InspectionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def write(self, relative, data):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def make_app(self, include_bundle=True):
        app = self.root / "Fixture.app"
        plist = {"CFBundleExecutable": "Fixture", "CFBundleIdentifier": "example.fixture",
                 "CFBundleName": "Fixture", "CFBundleShortVersionString": "1.0", "CFBundleVersion": "7",
                 "CFBundleSupportedPlatforms": ["MacOSX"],
                 "CFBundleGetInfoString": "Unity Player version 2022.3.54f1 (129125d4e700)."}
        self.write("Fixture.app/Contents/Info.plist", plistlib.dumps(plist))
        self.write("Fixture.app/Contents/MacOS/Fixture", macho_fixture())
        self.write("Fixture.app/Contents/Frameworks/GameAssembly.dylib", universal_fixture())
        self.write("Fixture.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat", metadata_fixture())
        aa = "Fixture.app/Contents/Resources/Data/StreamingAssets/aa/"
        catalog = {"m_InternalIdPrefixes": ["{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneOSX/"],
                   "m_InternalIds": ["0#level.bundle", "Assets/Scenes/Level.unity"],
                   "m_resourceTypes": [], "m_ProviderIds": []}
        self.write(aa + "catalog.json", json.dumps(catalog).encode())
        self.write(aa + "settings.json", json.dumps({"m_AddressablesVersion": "1.22.3", "m_buildTarget": "StandaloneOSX"}).encode())
        if include_bundle:
            self.write(aa + "StandaloneOSX/level.bundle", b"fixture asset bytes")
        fsm = {"Includes": [], "FSMs": [{"Name": "Movement", "DefaultState": "Moving",
                                         "States": [{"Class": "Motor", "Name": "Moving"}],
                                         "Transitions": [{"Class": "MissingClass", "Name": "Wait"}]}]}
        self.write("Fixture.app/Contents/Resources/Data/StreamingAssets/FSM/Movement.json", json.dumps(fsm).encode())
        self.write("supplied-note.txt", b"Every supplied file belongs in the manifest.\n")
        return app

    def test_metadata_preserves_indices_tokens_and_source_evidence(self):
        path = self.write("metadata.dat", metadata_fixture())
        result = parse_metadata(path)
        self.assertEqual((result["image_count"], result["type_count"], result["method_count"]), (1, 1, 2))
        self.assertEqual(result["images"][0]["assembly_version"], "1.2.3.4")
        self.assertEqual(result["types"][0]["full_name"], "Example.Motor")
        self.assertEqual(result["methods"][1]["id"], "Game.Runtime.dll:Example.Motor:0x06000002")
        self.assertEqual(result["methods"][1]["parameter_indices"], [0])
        self.assertEqual(result["fields"][0]["declaring_type_index"], 0)
        self.assertIn("Assets/Scripts/Motor.cs", result["source_paths"])
        self.assertEqual(result["package_evidence"][0]["version"], "1.22.3")
        self.assertTrue(result["package_evidence"][0]["exact_version"])

    def test_metadata_rejects_unsupported_truncated_and_invalid_ranges(self):
        original = metadata_fixture()
        unsupported = bytearray(original)
        struct.pack_into("<I", unsupported, 4, 29)
        bad_range = bytearray(original)
        struct.pack_into("<I", bad_range, 8 + 19 * 8, len(original) + 10)
        bad_size = bytearray(original)
        struct.pack_into("<I", bad_size, 8 + 5 * 8 + 4, 35)
        for index, data in enumerate((unsupported, original[:128], bad_range, bad_size)):
            with self.subTest(index=index):
                with self.assertRaises(ValueError):
                    parse_metadata(self.write("bad.dat", data))

    def test_macho_architectures_live_symbols_and_encryption(self):
        result = inspect_macho(self.write("native", universal_fixture()))
        self.assertTrue(result["universal"])
        self.assertEqual([a["name"] for a in result["architectures"]], ["x86_64", "arm64"])
        for architecture in result["architectures"]:
            self.assertEqual(architecture["registration_symbols"]["g_CodeRegistration"], "0x1000")
            self.assertEqual(architecture["method_symbol_count"], 1)
            self.assertFalse(architecture["encrypted"])
        encrypted = inspect_macho(self.write("encrypted", macho_fixture(cryptid=1)))
        self.assertTrue(encrypted["architectures"][0]["encrypted"])
        inactive = inspect_macho(self.write("inactive", macho_fixture(cryptid=0)))
        self.assertFalse(inactive["architectures"][0]["encrypted"])

    def test_macho_rejects_corrupt_symbol_and_fat_slice_bounds(self):
        thin = bytearray(macho_fixture())
        struct.pack_into("<I", thin, 32 + 16, len(thin) + 10)
        fat = bytearray(universal_fixture())
        struct.pack_into(">I", fat, 8 + 8, len(fat) + 10)
        for data in (thin, fat, b"nope", macho_fixture()[:40]):
            with self.subTest(size=len(data)):
                with self.assertRaises(ValueError):
                    inspect_macho(self.write("invalid-native", data))

    def test_bundle_manifest_is_complete_deterministic_and_read_only(self):
        self.make_app()
        before = {p.relative_to(self.root).as_posix(): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        result = inspect_bundle(self.root)
        self.assertEqual(result, inspect_bundle(self.root))
        self.assertEqual(result["identity"]["unity_version"], "2022.3.54f1")
        self.assertTrue(result["addressables"]["complete"])
        self.assertEqual(result["addressables"]["catalog_bundle_count"], 1)
        self.assertEqual(result["state_machines"]["unresolved_classes"], ["MissingClass"])
        self.assertEqual(result["state_machines"]["class_bindings"]["Motor"], ["Game.Runtime.dll:Example.Motor"])
        self.assertEqual({row["path"] for row in result["files"]}, set(before))
        for row in result["files"]:
            self.assertEqual(row["sha256"], hashlib.sha256(before[row["path"]]).hexdigest())
        after = {p.relative_to(self.root).as_posix(): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual(before, after)
        json.dumps(result, allow_nan=False)

    def test_missing_bundle_is_an_explicit_finding(self):
        self.make_app(include_bundle=False)
        result = inspect_bundle(self.root)
        self.assertFalse(result["addressables"]["complete"])
        self.assertEqual(len(result["addressables"]["missing_bundles"]), 1)
        self.assertTrue(result["addressables"]["missing_bundles"][0].endswith("StandaloneOSX/level.bundle"))

    def test_missing_metadata_and_ambiguous_input_fail(self):
        app = self.make_app()
        metadata = app / "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"
        metadata.unlink()
        with self.assertRaisesRegex(ValueError, "Required game bundle file"):
            inspect_bundle(self.root)
        (self.root / "Other.app").mkdir()
        with self.assertRaisesRegex(ValueError, "exactly one"):
            inspect_bundle(self.root)

    def test_catalog_rejects_traversal_and_invalid_prefix(self):
        app = self.make_app()
        aa = app / "Contents/Resources/Data/StreamingAssets/aa"
        for identifier in ("0#../escape.bundle", "2#file.bundle"):
            catalog = {"m_InternalIds": [identifier], "m_InternalIdPrefixes": ["../"]}
            (aa / "catalog.json").write_text(json.dumps(catalog))
            with self.assertRaises(ValueError):
                inspect_catalog(aa / "catalog.json", aa / "settings.json", self.root)


if __name__ == "__main__":
    unittest.main()

"""Original-input variant admission, with hashes typed from recorded evidence."""

import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lucidlib import assets, bootstrap, inputvariants, themebindings
from lucidlib.scriptbindings import RULES

GAME_ASSEMBLY = "Contents/Frameworks/GameAssembly.dylib"
METADATA = "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"
INFO_PLIST = "Contents/Info.plist"

# shasum of the private local input/SonicDreamTeam.app (1.10.1 / 1022527550),
# equal to the critical_files of reference export 20261005T062956Z-cef5ba97,
# and of its recovery thin x86_64 slice.
LOCAL = {GAME_ASSEMBLY: "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
         METADATA: "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
         INFO_PLIST: "497b0fdb7b50091f00fd36cf7bbfe667d22e081b30d853d71fed38670eb9c9ae"}
LOCAL_SLICE = "797ef6ac359e8dbfc83d97a6148fe9aa7ac85113217e3013e00c3c5b30502c94"
# The canonical public pair sealed in docs/code-progress.json by 1c24c1eb.
PUBLIC_BINARY = "e94d984c0bc726a3a66ed0b39785d5166f92aa4356b3dd742fce098d42521987"
PUBLIC_METADATA = "88af04a6e548dae5e00c13dd52add4e48f9917a31db784d3b21274b137e49837"


class VariantTableTests(unittest.TestCase):
    def test_binding_and_theme_evidence_name_only_the_local_variant(self):
        self.assertEqual(26, len(RULES))
        for rule in RULES:
            with self.subTest(rule=rule["full_name"]):
                self.assertEqual((LOCAL[GAME_ASSEMBLY], LOCAL[METADATA]), (rule["binary_sha256"], rule["metadata_sha256"]))
        self.assertEqual({"native_binary": LOCAL_SLICE, "metadata": LOCAL[METADATA]}, themebindings.ORIGINAL_INPUT_SHA256)

    def test_public_variant_is_declared_for_progress_only(self):
        self.assertEqual("e94d-88af", inputvariants.code_variant(PUBLIC_BINARY, PUBLIC_METADATA))
        self.assertEqual("e94d-88af", inputvariants.require_code_variant(PUBLIC_BINARY, PUBLIC_METADATA, inputvariants.CODE_PROGRESS))
        for purpose in (inputvariants.ASSET_PREPARATION, inputvariants.SCRIPT_BINDINGS, inputvariants.THEME_DIAGNOSTICS):
            with self.subTest(purpose=purpose):
                with self.assertRaisesRegex(ValueError, "e94d-88af is declared but not qualified for " + purpose):
                    inputvariants.require_code_variant(PUBLIC_BINARY, PUBLIC_METADATA, purpose)
                with self.assertRaisesRegex(ValueError, "not qualified"):
                    inputvariants.variant("e94d-88af", purpose)

    def test_admission_accepts_the_exact_local_bundle(self):
        self.assertEqual("6c89-acb", inputvariants.admit(dict(LOCAL), inputvariants.ASSET_PREPARATION))

    def test_admission_rejects_other_or_mixed_variants_with_supplied_and_supported_hashes(self):
        public = {GAME_ASSEMBLY: PUBLIC_BINARY, METADATA: PUBLIC_METADATA, INFO_PLIST: "1" * 64}
        cases = (
            (public, "e94d-88af is declared but not qualified for asset_preparation"),
            (dict(LOCAL, **{METADATA: PUBLIC_METADATA}), "Unsupported original input variant"),
            (dict(LOCAL, **{GAME_ASSEMBLY: PUBLIC_BINARY}), "Unsupported original input variant"),
            (dict(LOCAL, **{INFO_PLIST: "2" * 64}), "Info.plist differs from declared variant 6c89-acb"),
            ({k: v for k, v in LOCAL.items() if k != INFO_PLIST}, "Info.plist differs"),
            ({}, "Unsupported original input variant"))
        for critical, message in cases:
            with self.subTest(message=message, critical=sorted(critical.values())):
                with self.assertRaisesRegex(ValueError, message) as raised:
                    inputvariants.admit(critical, inputvariants.ASSET_PREPARATION)
                text = str(raised.exception)
                for digest in critical.values():
                    self.assertIn(digest, text)
                self.assertIn("6c89-acb (GameAssembly.dylib " + LOCAL[GAME_ASSEMBLY], text)
                self.assertNotIn("e94d-88af (", text)


    def test_every_asset_preparation_variant_records_its_info_plist(self):
        for name, profile in inputvariants.VARIANTS.items():
            if inputvariants.ASSET_PREPARATION in profile["qualified"]:
                with self.subTest(variant=name):
                    self.assertRegex(profile["info_plist_sha256"] or "", "^[0-9a-f]{64}$")

    def test_admission_of_a_variant_without_a_recorded_info_plist_names_the_gap(self):
        unrecorded = dict(inputvariants.VARIANTS["6c89-acb"], info_plist_sha256=None)
        with mock.patch.dict(inputvariants.VARIANTS, {"6c89-acb": unrecorded}):
            with self.assertRaisesRegex(ValueError, "Info.plist for declared variant 6c89-acb was never recorded") as raised:
                inputvariants.admit(dict(LOCAL), inputvariants.ASSET_PREPARATION)
        self.assertNotIn("differs", str(raised.exception))


class ExportAdmissionTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.work = self.root / "cache"
        self.work.mkdir()
        self.app = self.root / "Game.app"
        for relative, content in ((GAME_ASSEMBLY, b"other code"), (METADATA, b"other metadata"), (INFO_PLIST, b"plist"),
                                  ("Contents/Resources/Data/globalgamemanagers", b"synthetic Unity data")):
            (self.app / relative).parent.mkdir(parents=True, exist_ok=True)
            (self.app / relative).write_bytes(content)
        cache_root = mock.patch.object(bootstrap, "CACHE_ROOT", self.work)
        cache_root.start()
        self.addCleanup(cache_root.stop)
        self.tool = bootstrap.executable(self.work / "tools/assetripper/AssetRipper.GUI.Free")
        self.tool.parent.mkdir(parents=True)
        self.tool.write_bytes(b"not executable")
        self.pointer = self.work / "assets/latest-assets.json"
        assets._json_write(self.pointer, {"status": "exported", "run_id": "previous"})

    def assert_rejected_before_export(self, message):
        with mock.patch.object(assets, "verified_tool_path", return_value=self.tool), \
             mock.patch.object(assets, "_start_ripper", side_effect=RuntimeError("must not launch")) as start:
            with self.assertRaisesRegex(ValueError, message):
                assets.extract_assets(self.app, self.work)
            start.assert_not_called()
        self.assertFalse((self.work / "assets/runs").exists())
        self.assertEqual({"status": "exported", "run_id": "previous"}, json.loads(self.pointer.read_text()))

    def test_unknown_variant_is_rejected_before_any_run_or_export(self):
        self.assert_rejected_before_export("Unsupported original input variant")

    def test_public_variant_is_rejected_before_export_despite_its_release_string(self):
        # The public bytes are not on this machine; supply only their recorded hashes.
        real = assets.fingerprint_manifest
        def public(manifest):
            result = real(manifest)
            result["critical_files"].update({GAME_ASSEMBLY: PUBLIC_BINARY, METADATA: PUBLIC_METADATA})
            return result
        with mock.patch.object(assets, "fingerprint_manifest", side_effect=public):
            self.assert_rejected_before_export("e94d-88af is declared but not qualified for asset_preparation")


if __name__ == "__main__":
    unittest.main()

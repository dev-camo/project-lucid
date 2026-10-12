"""Exercise the real staging transaction with synthetic configuration exports."""
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_assets
from lucidlib import assets
from test_configurationbindings import asset, binding, document, game_document, runtime_document


class ConfigurationStagingTests(unittest.TestCase):
    def test_unsupported_configuration_preserves_previous_prepared_assets_and_receipt(self):
        fixture = test_assets.AssetTests("runTest")
        fixture.setUp()
        self.addCleanup(fixture.tearDown)
        project = fixture.make_export()
        raw = runtime_document()
        test_assets.write_asset(project, "Configuration/Runtime.asset", "e"*32, raw.decode())
        script = project / "Assets/Scripts/HLUnityCore.Runtime/Hardlight/RuntimeConfiguration.cs"
        script.parent.mkdir(parents=True)
        script.write_text("// Synthetic quarantined exporter declaration.\n")
        script.with_name(script.name+".meta").write_text("fileFormatVersion: 2\nguid: "+binding(1)["exported_guid"]+"\n")
        map_path = project.parent / "asset-map.json"
        assets._json_write(map_path, assets.build_asset_map(project))
        synthetic_identity_proof = {"status":"verified_layout", "bindings":[binding(1)], "references_modified":False}
        # This substitutes only the input-free tool identity fixture. It does
        # not qualify a real original binding or execute any Unity provider.
        with patch("lucidlib.scriptbindings.resolve_bindings", return_value=synthetic_identity_proof):
            assets.prepare_assets(fixture.repo, fixture.work)
        prepared = fixture.repo / "Assets/Recovered/Configuration/Runtime.asset"
        expected = raw.replace(binding(1)["exported_guid"].encode(), binding(1)["target_guid"].encode())
        self.assertEqual(expected, prepared.read_bytes())
        self.assertEqual(raw, (project/"Assets/Configuration/Runtime.asset").read_bytes())
        before = {p.relative_to(fixture.repo).as_posix():p.read_bytes() for p in (fixture.repo/"Assets").rglob("*") if p.is_file()}
        receipt_path = fixture.work / "assets/latest-prepare.json"
        previous_receipt = receipt_path.read_bytes()
        bad = project / "Assets/Configuration/Runtime.asset"
        bad.write_bytes(raw+b"  m_unsupported: []\n")
        assets._json_write(map_path, assets.build_asset_map(project))
        with patch("lucidlib.scriptbindings.resolve_bindings", return_value=synthetic_identity_proof), \
                self.assertRaisesRegex(ValueError, "field set or order"):
            assets.prepare_assets(fixture.repo, fixture.work)
        after = {p.relative_to(fixture.repo).as_posix():p.read_bytes() for p in (fixture.repo/"Assets").rglob("*") if p.is_file()}
        self.assertEqual(before, after)
        self.assertEqual(previous_receipt, receipt_path.read_bytes())
        self.assertEqual(raw+b"  m_unsupported: []\n", bad.read_bytes())
        self.assertEqual([], list((fixture.repo/"Assets").glob(".lucid-assets-*")))

    def test_all_three_configuration_routes_and_scalar_route_prepare_without_rewrite_substitution(self):
        fixture = test_assets.AssetTests("runTest")
        fixture.setUp()
        self.addCleanup(fixture.tearDown)
        project = fixture.make_export()
        documents = [document(0, "  ActiveConfig: " + asset(12) + "\n"), runtime_document(), game_document()]
        for index, raw in enumerate(documents):
            test_assets.write_asset(project, "Configuration/" + str(index) + ".asset", format(index + 32, "032x"), raw.decode())
            relative = ("ActiveRuntimeConfiguration", "RuntimeConfiguration", "GameLevels")[index]
            assembly, namespace = ("Game.Runtime", "HardlightProject") if index == 2 else ("HLUnityCore.Runtime", "Hardlight")
            test_assets.write_asset(project, "Scripts/" + assembly + "/" + namespace + "/" + relative + ".cs",
                                   binding(index)["exported_guid"], "// Synthetic quarantined declaration.\n")
        map_path = project.parent / "asset-map.json"
        assets._json_write(map_path, assets.build_asset_map(project))
        scalar = {"exported_guid": "a" * 32, "exported_file_id": 11500000,
                  "target_guid": "f" * 32, "target_file_id": 11500000}
        proof = {"status": "verified_layout", "bindings": [scalar] + [binding(i) for i in range(3)], "references_modified": False}
        source_before = {p.relative_to(project).as_posix(): p.read_bytes() for p in project.rglob("*") if p.is_file()}
        with patch("lucidlib.scriptbindings.resolve_bindings", return_value=proof):
            actual = assets.prepare_assets(fixture.repo, fixture.work)
        self.assertEqual(4, len(actual["script_bindings"]["assets"]))
        self.assertEqual([1] * 4, [row["pointers_rewritten"] for row in actual["script_bindings"]["assets"]])
        self.assertEqual([], actual["quarantined_script_guids"])
        for index, raw in enumerate(documents):
            old = ("  m_Script: {fileID: 11500000, guid: " + binding(index)["exported_guid"] + ", type: 3}").encode()
            new = ("  m_Script: {fileID: 11500000, guid: " + binding(index)["target_guid"] + ", type: 3}").encode()
            prepared = fixture.repo / "Assets/Recovered/Configuration" / (str(index) + ".asset")
            self.assertEqual(raw.replace(old, new), prepared.read_bytes())
            self.assertEqual((project / "Assets/Configuration" / (str(index) + ".asset.meta")).read_bytes(),
                             prepared.with_name(prepared.name + ".meta").read_bytes())
        scene = fixture.repo / "Assets/Recovered/Scenes/demo.unity"
        self.assertIn(("guid: " + "f" * 32).encode(), scene.read_bytes())
        prepared_before = {p.relative_to(fixture.repo).as_posix(): p.read_bytes() for p in (fixture.repo / "Assets").rglob("*") if p.is_file()}
        with patch("lucidlib.scriptbindings.resolve_bindings", return_value=proof):
            repeated = assets.prepare_assets(fixture.repo, fixture.work)
        self.assertEqual(prepared_before, {p.relative_to(fixture.repo).as_posix(): p.read_bytes() for p in (fixture.repo / "Assets").rglob("*") if p.is_file()})
        self.assertEqual(source_before, {p.relative_to(project).as_posix(): p.read_bytes() for p in project.rglob("*") if p.is_file()})
        self.assertEqual(4, len(repeated["script_bindings"]["assets"]))
        self.assertEqual([], list((fixture.repo / "Assets").glob(".lucid-assets-*")))

    def test_a_selected_map_pointer_missing_from_export_aborts_before_promotion(self):
        fixture = test_assets.AssetTests("runTest")
        fixture.setUp()
        self.addCleanup(fixture.tearDown)
        project = fixture.make_export()
        raw = runtime_document()
        test_assets.write_asset(project, "Configuration/Runtime.asset", "e" * 32, raw.decode())
        proof = {"status": "verified_layout", "bindings": [binding(1)], "references_modified": False}
        map_path = project.parent / "asset-map.json"
        assets._json_write(map_path, assets.build_asset_map(project))
        with patch("lucidlib.scriptbindings.resolve_bindings", return_value=proof):
            assets.prepare_assets(fixture.repo, fixture.work)
        before = {p.relative_to(fixture.repo).as_posix(): p.read_bytes() for p in (fixture.repo / "Assets").rglob("*") if p.is_file()}
        receipt = (fixture.work / "assets/latest-prepare.json").read_bytes()
        original = project / "Assets/Configuration/Runtime.asset"
        # Deliberately inconsistent tool fixture: retain the captured map while
        # the selected pointer no longer occurs in its corresponding source.
        changed = raw.replace(binding(1)["exported_guid"].encode(), ("9" * 32).encode())
        original.write_bytes(changed)
        with patch("lucidlib.scriptbindings.resolve_bindings", return_value=proof), self.assertRaisesRegex(ValueError, "Mapped script reference is absent"):
            assets.prepare_assets(fixture.repo, fixture.work)
        self.assertEqual(before, {p.relative_to(fixture.repo).as_posix(): p.read_bytes() for p in (fixture.repo / "Assets").rglob("*") if p.is_file()})
        self.assertEqual(receipt, (fixture.work / "assets/latest-prepare.json").read_bytes())
        self.assertEqual(changed, original.read_bytes())
        self.assertEqual([], list((fixture.repo / "Assets").glob(".lucid-assets-*")))


if __name__ == "__main__": unittest.main()

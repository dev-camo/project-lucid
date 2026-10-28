from collections import Counter
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import codeprogress, shaderprogress

REPOSITORY = Path(__file__).resolve().parents[2]
ORIGIN = {"input_fingerprint_sha256": "1" * 64, "asset_map_catalog_sha256": "2" * 64}
SOURCE = "a" * 64
# Counts read from the immutable export's quarantined shader assets (210 in all).
FOLDER_COUNTS = {"Character": 9, "Environment": 20, "Gameplay": 11, "Hub": 2, "Particle": 11, "RenderFeatures": 1, "Sky": 3, "UI": 2}

# Shipped GUIDs of the 22 shaders recovered so far (every one is a quarantined shader of the immutable export).
RECOVERED_SHADERS = (
    "109d879a0e474224f9576baad15f0273",
    "25aa679e9f8555d459542325f9fd9c14",
    "3518f69d26ca6d140b0b1b2d67fc8dea",
    "3d28166e35482e444b56af851ff24084",
    "439e8a81d893fa244a6c338d94b5b25b",
    "78c329c1d2e2b1f498a41271fa63ce7e",
    "79454e735e6b2824d92c0ebe4fc51d38",
    "834e4dabaab6ec8428bd6a64115a2c5c",
    "89aa1b470a96afe49a9b172ec8300d40",
    "959ce1e53b9a11e449afa3188de59610",
    "a39de842318b98643a7250ed212218ce",
    "ae05e3fda4799aa49a92776741aa42c4",
    "aea03fbf55edc744f86f594b52b8de6d",
    "af6da4f6308a6cd45b8544e8d821d998",
    "beb9536db5dec634f9859840dcb2976b",
    "bf4c82e70ee177f45be870c94d234981",
    "c18e3bc73f2c52c458bbc4569a3d42a1",
    "c24466ea4a824924e820666d8ffb14a4",
    "ccfc70e40a7e6a74385421abe4e85ad8",
    "eb77dff1bca503f43903d55ab1540df0",
    "fb495c01c5fd7534590903b8e77f3281",
    "fc62d258c9bcd2648b4158e86c2d4178",
)


def shader(guid, path):
    return {"guid": guid, "path": path, "shader_quarantined": True}


def asset_map(extra=()):
    folder = "Assets/Data/Art/Shaders/Shaders/"
    rows = [shader("00000000000000000000000000000001", folder + "Character/HL_Char_Standard.asset"),
            shader("00000000000000000000000000000002", folder + "Particle/Temp/HL_Particle_Temp.asset"),
            shader("00000000000000000000000000000003", folder + "RenderFeatures/HL_FullScreen_Effects.asset"),
            shader("00000000000000000000000000000004", "Assets/Shader/Hidden_HardLight_RenderFeature_HL_FullScreen_Effects.asset"),
            shader("00000000000000000000000000000005", "Assets/Resources/base.asset"),
            shader("00000000000000000000000000000006", "Assets/Shader/Hidden_Universal Render Pipeline_Bloom.asset"),
            shader("00000000000000000000000000000007", "Assets/TextMesh Pro/Shaders/TMP_SDF.asset"),
            {"guid": "00000000000000000000000000000008", "path": "Assets/Scripts/Thing.cs", "shader_quarantined": False}]
    return {"assets": rows + list(extra)}


class InventoryTests(unittest.TestCase):
    def test_every_quarantined_shader_is_classified_and_non_shaders_are_ignored(self):
        rows = {row["guid"]: (row["class"], row["category"], row["name"]) for row in self.build()["shaders"]}
        def guid(number): return "%032x" % number
        self.assertEqual(("game_folder", "Character", "HL_Char_Standard"), rows[guid(1)])
        self.assertEqual(("game_folder", "Particle", "HL_Particle_Temp"), rows[guid(2)])
        self.assertEqual(("game_outside_folder", None, "Hidden_HardLight_RenderFeature_HL_FullScreen_Effects"), rows[guid(4)])
        self.assertEqual("third_party", rows[guid(5)][0])
        self.assertEqual(["engine", "engine"], [rows[guid(6)][0], rows[guid(7)][0]])
        self.assertNotIn(guid(8), rows)

    def build(self, extra=()):
        # A synthetic map covers the rules; the full-size inventory is validated against the real counts below.
        full = asset_map(extra)
        folder = "Assets/Data/Art/Shaders/Shaders/"
        index = 100
        for category, count in FOLDER_COUNTS.items():
            for number in range(count - sum(1 for row in full["assets"] if row["path"].startswith(folder + category + "/"))):
                index += 1
                full["assets"].append(shader("%032x" % index, folder + category + "/Filler_" + str(number) + ".asset"))
        fillers = {"game_outside_folder": "Assets/Shader/HardLight_Filler_", "third_party": "Assets/Resources/", "engine": "Assets/Shader/Hidden_Core_Filler_"}
        have = Counter(shaderprogress.classify(row["path"])[0] for row in full["assets"] if row["shader_quarantined"])
        for kind, prefix in fillers.items():
            for number in range(shaderprogress.CLASS_COUNTS[kind] - have[kind]):
                index += 1
                full["assets"].append(shader("%032x" % index, (prefix + "base.asset") if kind == "third_party" else (prefix + str(number) + ".asset")))
        return shaderprogress.build_inventory(full, ORIGIN)

    def test_unclassified_paths_and_unknown_categories_are_rejected(self):
        for path in ("Assets/Shader/Mystery.asset", "Assets/Resources/other.asset", "Assets/Data/Art/Shaders/Shaders/Newcat/X.asset",
                     "Assets/Data/Art/Shaders/Shaders/Loose.asset", "Assets/Shader/HardLightX_Thing.asset", "Other/HardLight_Thing.asset", "Assets/../x.asset", None):
            with self.subTest(path=path), self.assertRaises(ValueError):
                shaderprogress.classify(path)

    def test_duplicate_guid_omitted_entries_and_wrong_categories_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.build([shader("00000000000000000000000000000001", "Assets/Shader/Hidden_Core_FallbackError.asset")])
        inventory = self.build()
        for mutation in ("omit_folder", "omit_other", "omit_outside", "omit_third_party", "wrong_category", "category_on_engine", "unknown_class", "bad_guid", "unsorted", "extra_key", "missing_category"):
            with self.subTest(mutation=mutation):
                broken = copy.deepcopy(inventory); rows = broken["shaders"]
                folder = [r for r in rows if r["class"] == "game_folder"]
                if mutation == "omit_folder": rows.remove(folder[0])
                if mutation == "omit_other": rows.remove(next(r for r in rows if r["class"] == "engine"))
                if mutation == "omit_outside": rows.remove(next(r for r in rows if r["class"] == "game_outside_folder"))
                if mutation == "omit_third_party": rows.remove(next(r for r in rows if r["class"] == "third_party"))
                if mutation == "wrong_category": folder[0]["category"] = "Sky"
                if mutation == "category_on_engine": next(r for r in rows if r["class"] == "engine")["category"] = "Sky"
                if mutation == "unknown_class": folder[0]["class"] = "game"
                if mutation == "bad_guid": rows[0]["guid"] = "XYZ"
                if mutation == "unsorted": rows.reverse()
                if mutation == "extra_key": rows[0]["path"] = "x"
                if mutation == "missing_category": folder[0]["category"] = None
                with self.assertRaises(ValueError): shaderprogress.validate_inventory(broken)

    def test_committed_inventory_matches_the_original_export_counts(self):
        inventory, _ = shaderprogress.load_inventory(REPOSITORY)
        classes = {kind: sum(1 for row in inventory["shaders"] if row["class"] == kind) for kind in shaderprogress.CLASSES}
        self.assertEqual({"game_folder": 59, "game_outside_folder": 13, "third_party": 13, "engine": 125}, classes)
        self.assertEqual(210, len(inventory["shaders"]))
        self.assertEqual(FOLDER_COUNTS, {c: sum(1 for r in inventory["shaders"] if r["category"] == c) for c in FOLDER_COUNTS})
        self.assertEqual(sorted(row["guid"] for row in inventory["shaders"]), [row["guid"] for row in inventory["shaders"]])
        # Outside-folder game shaders named by the issue are present with their shipped GUIDs.
        outside = {row["guid"]: row["name"] for row in inventory["shaders"] if row["class"] == "game_outside_folder"}
        self.assertEqual("Hidden_HardLight_RenderFeature_HL_FullScreen_Effects", outside["89aa1b470a96afe49a9b172ec8300d40"])
        self.assertEqual("HardLight_RenderFeature_HL_UI_Blur_Render_Feature", outside["ae05e3fda4799aa49a92776741aa42c4"])


class PresenceTests(unittest.TestCase):
    def setUp(self):
        self.inventory, _ = shaderprogress.load_inventory(REPOSITORY)
        self.folder = [r["guid"] for r in self.inventory["shaders"] if r["class"] == "game_folder"]
        self.outside = [r["guid"] for r in self.inventory["shaders"] if r["class"] == "game_outside_folder"]

    def test_recovered_shaders_split_twenty_in_the_folder_and_two_outside(self):
        # The GUIDs of the 22 shaders recovered so far, matched to the original asset map (Recovered/*.shader.meta).
        recovered = RECOVERED_SHADERS
        result = shaderprogress.measure(self.inventory, list(recovered))
        game = result["game_authored"]
        self.assertEqual((72, 22), (game["original"], game["source_present"]))
        self.assertEqual((59, 20), (game["folder"]["original"], game["folder"]["source_present"]))
        self.assertEqual((13, 2), (game["outside_folder"]["original"], game["outside_folder"]["source_present"]))
        self.assertEqual({"third_party": 13, "engine": 125}, result["not_game_scope"])
        self.assertEqual({"status": "not_measured"}, result["compilation"])
        self.assertEqual({"status": "not_measured"}, result["fidelity"])
        shaderprogress.validate_public(result)

    def test_live_shader_tree_keeps_every_recovered_shader(self):
        # Further ports may add shaders; removing a recovered one must fail.
        scan = shaderprogress.maintained_shader_guids(REPOSITORY)
        self.assertLessEqual(set(RECOVERED_SHADERS), set(scan))
        result = shaderprogress.measure(self.inventory, scan)
        self.assertEqual(len(scan), result["game_authored"]["source_present"])

    def test_unknown_duplicate_and_non_game_guids_are_rejected(self):
        third_party = next(r["guid"] for r in self.inventory["shaders"] if r["class"] == "third_party")
        engine = next(r["guid"] for r in self.inventory["shaders"] if r["class"] == "engine")
        for guids in (["f" * 32], [self.folder[0], self.folder[0]], [third_party], [engine], ["not-a-guid"], [3]):
            with self.subTest(guids=guids), self.assertRaises(ValueError):
                shaderprogress.measure(self.inventory, guids)

    def test_presence_follows_the_shipped_guid_not_the_name(self):
        result = shaderprogress.measure(self.inventory, [self.folder[0]])
        self.assertEqual(1, result["game_authored"]["source_present"])
        self.assertEqual(0, shaderprogress.measure(self.inventory, [])["game_authored"]["source_present"])

    def test_scan_rejects_shaders_without_a_unique_guid_and_symlinks(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve(); tree = root / shaderprogress.SHADER_ROOT / "Recovered"; tree.mkdir(parents=True)
            (tree / "A.shader").write_text("Shader \"A\" {}")
            with self.assertRaises(ValueError): shaderprogress.maintained_shader_guids(root)
            (tree / "A.shader.meta").write_text("guid: " + "a" * 32 + "\nguid: " + "b" * 32 + "\n")
            with self.assertRaises(ValueError): shaderprogress.maintained_shader_guids(root)
            (tree / "A.shader.meta").write_text("fileFormatVersion: 2\nguid: " + "a" * 32 + "\n")
            self.assertEqual(["a" * 32], shaderprogress.maintained_shader_guids(root))
            (tree / "B.shader").symlink_to(tree / "A.shader")
            with self.assertRaises(ValueError): shaderprogress.maintained_shader_guids(root)

    def test_public_section_tampering_is_rejected(self):
        base = shaderprogress.measure(self.inventory, [self.folder[0], self.outside[0]])
        def edit(change):
            report = copy.deepcopy(base); change(report); return report
        for change in (lambda r: r["game_authored"]["folder"]["categories"]["Sky"].update(original=4),
                       lambda r: r["game_authored"]["folder"]["categories"]["Sky"].update(source_present=99),
                       lambda r: r["game_authored"].update(source_present=9),
                       lambda r: r["game_authored"]["outside_folder"].update(original=12),
                       lambda r: r["game_authored"]["folder"]["categories"].pop("UI"),
                       lambda r: r["fidelity"].update(status="qualified"),
                       lambda r: r.update(compilation={"status": "sealed", "target": "macos", "source_fingerprint": SOURCE, "compiled": 99}),
                       lambda r: r.update(compilation={"status": "sealed", "target": "ps5", "source_fingerprint": SOURCE, "compiled": 1}),
                       lambda r: r.update(compilation={"status": "sealed", "target": "macos", "compiled": 1}),
                       lambda r: r.update(extra=1)):
            with self.assertRaises(ValueError): shaderprogress.validate_public(edit(change))


class CompilationEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.inventory, _ = shaderprogress.load_inventory(REPOSITORY)
        self.present = [r["guid"] for r in self.inventory["shaders"] if r["class"] == "game_folder"][:3]
        self.evidence = {"target": "linux", "source_fingerprint": SOURCE, "compiled_guids": sorted(self.present[:2]), "fallback_or_error_guids": []}

    def measure(self, evidence, source=SOURCE):
        return shaderprogress.measure(self.inventory, self.present, source, evidence)

    def test_sealed_evidence_for_this_exact_source_counts_only_what_it_names(self):
        result = self.measure(self.evidence)
        self.assertEqual({"status": "sealed", "target": "linux", "source_fingerprint": SOURCE, "compiled": 2}, result["compilation"])
        self.assertEqual(3, result["game_authored"]["source_present"])
        shaderprogress.validate_public(result)

    def test_stale_unsupported_or_unsound_compilation_claims_are_rejected(self):
        def edit(**changes): return dict(self.evidence, **changes)
        for evidence in (edit(source_fingerprint="b" * 64), edit(target="ps5"), edit(target=None), edit(compiled_guids=["f" * 32]),
                         edit(compiled_guids=sorted(self.present[:2]) + sorted(self.present[:1])), edit(compiled_guids=sorted(self.present, reverse=True)),
                         edit(fallback_or_error_guids=[self.present[2]]), edit(fallback_or_error_guids=[self.present[0]]), edit(compiled_guids="all"),
                         {k: v for k, v in self.evidence.items() if k != "target"}, dict(self.evidence, extra=1)):
            with self.subTest(evidence=evidence), self.assertRaises(ValueError):
                self.measure(evidence)
        with self.assertRaises(ValueError): self.measure(self.evidence, source=None)
        with self.assertRaises(ValueError): self.measure(self.evidence, source="not-a-hash")


class ReportIntegrationTests(unittest.TestCase):
    def report(self, guids=()):
        helper_scope = ["Game.Runtime"]
        row = {"id": "Game.Runtime:Hardlight.Sample:0x06000001", "assembly": "Game.Runtime", "token": "0x06000001", "declaring_type": "Hardlight.Sample",
               "name": "Calculate", "native_body": True,
               "signature": {"owner": "Hardlight.Sample", "name": "Calculate", "is_static": True, "return_type": "primitive:System.Int32",
                             "parameters": [], "generic_parameters": [], "declaring_generic_parameters": []}}
        flags = {"native_byte_matching_verified": False, "gameplay_verified": False}
        availability = {"Game.Runtime": {"measurement_status": "measured", "unavailable_reason": None}}
        measurement = codeprogress.measure_code_work({"kind": "original-native-method-inventory-v1", "methods": [row], **flags},
                                                     {"kind": "maintained-player-source-body-inventory-v1", "methods": [], **flags}, helper_scope, assembly_availability=availability)
        inventory, _ = shaderprogress.load_inventory(REPOSITORY)
        folder = [r["guid"] for r in inventory["shaders"] if r["class"] == "game_folder"]
        return codeprogress.public_counts(measurement, source=SOURCE, input_binary="6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
                                          input_metadata="acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b", scope_sha256="d" * 64,
                                          compiler_receipt_sha256="e" * 64, shaders=shaderprogress.measure(inventory, folder[:int(len(guids))] if guids else []))

    def test_schema4_requires_the_shader_section_and_older_schemas_forbid_it(self):
        report = self.report()
        self.assertEqual(4, report["schema_version"])
        codeprogress.validate_public_counts(report)
        without = {k: v for k, v in report.items() if k != "shaders"}
        with self.assertRaises(ValueError): codeprogress.validate_public_counts(without)
        legacy = copy.deepcopy(report); legacy["schema_version"] = 3
        with self.assertRaises(ValueError): codeprogress.validate_public_counts(legacy)

    def test_rendered_shader_table_lists_categories_and_separates_presence_from_compilation_and_parity(self):
        text = codeprogress.render_readme_section(self.report([1, 2]))
        self.assertIn("**2 of 72 original game shaders have maintained source, matched by shipped GUID.**", text)
        for category in FOLDER_COUNTS:
            self.assertRegex(text, r"\| " + category + r" \| \d+ \| " + str(FOLDER_COUNTS[category]) + r" \|")
        self.assertIn("| Outside the game shader folder | 0 | 13 |", text)
        self.assertIn("Source presence only: it does not show that a shader compiles for any target or looks like the original. Compilation is not measured.", text)
        self.assertIn("13 third-party and 125 engine shaders are tracked separately.", text)

    def test_compilation_evidence_for_another_source_is_rejected_in_the_report(self):
        report = self.report()
        report["shaders"]["compilation"] = {"status": "sealed", "target": "macos", "source_fingerprint": "b" * 64, "compiled": 0}
        with self.assertRaises(ValueError): codeprogress.validate_public_counts(report)
        report["shaders"]["compilation"]["source_fingerprint"] = SOURCE
        codeprogress.validate_public_counts(report)

    def test_repository_freshness_check_compares_shader_progress_with_the_maintained_tree(self):
        from unittest.mock import patch
        report = self.report()
        with patch.object(codeprogress, "artifact_fingerprint", return_value=SOURCE), \
             patch.object(codeprogress, "_scope", return_value=(["Game.Runtime"], {}, "d" * 64)):
            with self.assertRaisesRegex(ValueError, "shader progress is stale"): codeprogress.validate_public_counts(report, REPOSITORY)
            current = shaderprogress.measure(*[shaderprogress.load_inventory(REPOSITORY)[0], shaderprogress.maintained_shader_guids(REPOSITORY)])
            report["shaders"] = current
            codeprogress.validate_public_counts(report, REPOSITORY)


if __name__ == "__main__":
    unittest.main()

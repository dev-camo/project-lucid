import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.scriptbindings import RULES, resolve_bindings, rewrite_script_pointers
from lucidlib.verification import artifact_fingerprint


class ScriptBindingTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory).resolve() / "project with spaces"
        root.mkdir()
        work = root / ".cache/project-lucid"
        export = work / "assets/export"
        rule = RULES[0]
        for base, relative, guid in ((root, rule["maintained_path"], "b" * 32), (export, rule["export_path"], "a" * 32)):
            path = base / relative
            path.parent.mkdir(parents=True)
            path.write_text("source evidence")
            Path(str(path) + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n")
        for relative in rule["runtime_sources"]:
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes((Path(__file__).resolve().parents[2] / relative).read_bytes())
        mapping = {"assets": [{"path": rule["export_path"], "guid": "a" * 32, "code_quarantined": True}]}
        self.input_fingerprint = {"critical_files": {
            "Contents/Frameworks/GameAssembly.dylib": rule["binary_sha256"],
            "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat": rule["metadata_sha256"]}}
        schema_path = work / "native-recovery/proof/schema.json"
        schema_path.parent.mkdir(parents=True)
        schema_path.write_text(json.dumps({"command": "schemas", "status": "ready",
            "metadata_sha256": rule["metadata_sha256"], "native_binary_sha256": "c" * 64}))
        (work / "native-recovery/latest-schemas.json").write_text(json.dumps({"command": "schemas", "status": "ready",
            "evidence_report": str(schema_path), "source": {"binary_sha256": rule["binary_sha256"],
            "metadata_sha256": rule["metadata_sha256"], "analysis_binary_sha256": "c" * 64}}))
        schema_digest = hashlib.sha256(schema_path.read_bytes()).hexdigest()
        fingerprint = artifact_fingerprint(root)
        script = {"path": rule["maintained_path"], "guid": "b" * 32, "file_id": 11500000,
                  "class_resolved": True, "source_sha256": hashlib.sha256((root / rule["maintained_path"]).read_bytes()).hexdigest()}
        comparison = {"command": "compare-monoscript-layouts", "status": "compared",
            "source_fingerprint": fingerprint, "original_schema_sha256": schema_digest, "candidates": [{
            "assembly": rule["assembly"], "full_name": rule["full_name"], "status": "compatible_layout", "issues": [],
            "original_token": "0x02000569", "loaded_scripts": [script]}]}
        path = work / "reports/layout-runs/proof/comparison.json"
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps(comparison))
        summary = {"status": "compared", "source_freshness_verified": True,
                   "comparison_path": str(path), "source_fingerprint": fingerprint,
                   "schema_path": str(schema_path), "original_schema_sha256": schema_digest}
        return root, work, export, mapping, comparison, path, summary

    def test_current_exact_script_identity_is_recorded_without_source_changes(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            before = artifact_fingerprint(root)
            with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                result = resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            self.assertEqual("verified_layout", result["status"])
            self.assertFalse(result["references_modified"])
            self.assertEqual("a" * 32, result["bindings"][0]["exported_guid"])
            self.assertEqual("b" * 32, result["bindings"][0]["target_guid"])
            self.assertEqual(before, artifact_fingerprint(root))

    def test_unrelated_export_does_not_launch_editor(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                result = resolve_bindings(root, work, export, {"assets": []})
            run.assert_not_called()
            self.assertEqual([], result["bindings"])

    def test_changed_and_ambiguous_export_script_metadata_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                duplicate = {"assets": mapping["assets"] * 2}
                with self.assertRaisesRegex(ValueError, "ambiguous"):
                    resolve_bindings(root, work, export, duplicate, input_fingerprint=self.input_fingerprint)
                reused = {"assets": mapping["assets"] + [{"path": "Assets/other.asset", "guid": "a" * 32}]}
                with self.assertRaisesRegex(ValueError, "multiple asset owners"):
                    resolve_bindings(root, work, export, reused, input_fingerprint=self.input_fingerprint)
                (export / (RULES[0]["export_path"] + ".meta")).write_text("guid: " + "d" * 32)
                with self.assertRaisesRegex(ValueError, "asset map"):
                    resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            run.assert_not_called()

    def test_stale_incomplete_or_incompatible_layout_proof_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, original, path, summary = self.fixture(directory)
            cases = (dict(summary, status="failed"), dict(summary, source_fingerprint="d" * 64),
                     dict(summary, source_freshness_verified=False))
            for value in cases:
                with self.subTest(summary=value), patch("lucidlib.scriptbindings.run_layout_inventory", return_value=value):
                    with self.assertRaises(ValueError):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            for change in ("blocked", "wrong-source", "wrong-guid", "wrong-id", "wide-id", "zero-guid", "duplicate", "source-hash"):
                comparison = copy.deepcopy(original)
                candidate = comparison["candidates"][0]
                script = candidate["loaded_scripts"][0]
                if change == "blocked": candidate.update(status="blocked", issues=[{"reason": "field mismatch"}])
                if change == "wrong-source": script["path"] = "Assets/Recovered/Script.cs"
                if change == "wrong-guid": script["guid"] = "d" * 32
                if change == "wrong-id": script["file_id"] = True
                if change == "wide-id": script["file_id"] = 1 << 63
                if change == "zero-guid": script["guid"] = "0" * 32
                if change == "duplicate": candidate["loaded_scripts"].append(copy.deepcopy(script))
                if change == "source-hash": script["source_sha256"] = "d" * 64
                path.write_text(json.dumps(comparison))
                with self.subTest(change=change), patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                    with self.assertRaises(ValueError):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)

    def test_symlinked_script_evidence_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            source = root / RULES[0]["maintained_path"]
            target = root / "other.cs"
            target.write_bytes(source.read_bytes())
            source.unlink()
            source.symlink_to(target)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                with self.assertRaisesRegex(ValueError, "symlink"):
                    resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            run.assert_not_called()

    def test_changed_behavior_is_rejected_even_when_fields_and_layout_are_identical(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            for relative in RULES[0]["runtime_sources"]:
                source = root / relative
                original = source.read_bytes()
                source.write_bytes(original + b"\n// changed implementation\n")
                with self.subTest(source=relative), patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                    with self.assertRaisesRegex(ValueError, "behavior changed"):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
                    run.assert_not_called()
                source.write_bytes(original)

    def test_another_game_release_cannot_use_identical_layout_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            for identity in (None, {"critical_files": {}}, {"critical_files": {
                    "Contents/Frameworks/GameAssembly.dylib": "d" * 64,
                    "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat": RULES[0]["metadata_sha256"]}}):
                with self.subTest(identity=identity), patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                    with self.assertRaisesRegex(ValueError, "supplied release"):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=identity)
                    run.assert_not_called()
            source_path = work / "native-recovery/latest-schemas.json"
            original = json.loads(source_path.read_bytes())
            for key in ("binary_sha256", "metadata_sha256", "analysis_binary_sha256"):
                value = copy.deepcopy(original)
                value["source"][key] = "d" * 64
                source_path.write_text(json.dumps(value))
                with self.subTest(key=key), patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                    with self.assertRaisesRegex(ValueError, "supplied release"):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            source_path.write_text(json.dumps(original))
            Path(summary["schema_path"]).write_text("{}")
            with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                with self.assertRaisesRegex(ValueError, "source proof|layout proof"):
                    resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)

    def test_rewrite_changes_only_exact_script_pointer_and_preserves_other_bytes(self):
        binding = {"exported_guid": "a" * 32, "exported_file_id": 11500000,
                   "target_guid": "b" * 32, "target_file_id": -4294967297}
        pointer = "m_Script: {fileID: 11500000, guid: " + "a" * 32 + ", type: 3}"
        raw = ("%YAML 1.1\r\n--- !u!114 &11400000\r\nMonoBehaviour:\r\n  " + pointer + "\r\n  m_guid: " + "a" * 32 + "\r\n  m_Name: \u03a9\r\n").encode()
        result, count = rewrite_script_pointers(raw, [binding])
        self.assertEqual(1, count)
        self.assertEqual(raw.replace(pointer.encode(), ("m_Script: {fileID: -4294967297, guid: " + "b" * 32 + ", type: 3}").encode()), result)
        self.assertEqual((result, 0), rewrite_script_pointers(result, [binding]))
        self.assertEqual(raw, rewrite_script_pointers(raw, [])[0])

    def test_wrong_local_id_pointer_type_and_extra_data_are_not_silently_rewritten(self):
        binding = {"exported_guid": "a" * 32, "exported_file_id": 11500000,
                   "target_guid": "b" * 32, "target_file_id": 11500000}
        for body in ("fileID: 1, guid: " + "a" * 32 + ", type: 3", "fileID: 11500000, guid: " + "a" * 32 + ", type: 2",
                     "fileID: 11500000, guid: " + "a" * 32 + ", type: 3, extra: 1"):
            with self.subTest(body=body), self.assertRaisesRegex(ValueError, "pointer"):
                rewrite_script_pointers(("--- !u!114 &11400000\nMonoBehaviour:\n  m_Script: {" + body + "}\n").encode(), [binding])
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            rewrite_script_pointers(b"", [binding, binding])

    def test_comments_strings_and_other_document_types_are_not_script_fields(self):
        binding = {"exported_guid": "a" * 32, "exported_file_id": 11500000,
                   "target_guid": "b" * 32, "target_file_id": 11500000}
        pointer = "m_Script: {fileID: 11500000, guid: " + "a" * 32 + ", type: 3}"
        for text in ("# " + pointer + "\n", 'm_Text: "' + pointer + '"\n',
                     "--- !u!114 &1\nMonoBehaviour:\n  m_Text: |\n    " + pointer + "\n"):
            with self.subTest(text=text):
                self.assertEqual((text.encode(), 0), rewrite_script_pointers(text.encode(), [binding]))
        for text in ("--- !u!1 &1\nGameObject:\n  " + pointer + "\n",
                     '--- !u!114 &1\nMonoBehaviour:\n  m_Name: "multiline\n  ' + pointer + '\n  rest"\n'):
            with self.subTest(text=text), self.assertRaisesRegex(ValueError, "document|quoted"):
                rewrite_script_pointers(text.encode(), [binding])


if __name__ == "__main__":
    unittest.main()

import copy
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.scriptbindings import RULES, PACKAGE_RULES, GUID, resolve_bindings, rewrite_script_pointers
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

    def two_rule_fixture(self, directory):
        root, work, export, mapping, comparison, path, summary = self.fixture(directory)
        rule = RULES[1]
        for base, relative, guid in ((root, rule["maintained_path"], "d" * 32),
                                     (export, rule["export_path"], "c" * 32)):
            source = base / relative
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_text("source evidence")
            Path(str(source) + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n")
        for relative in rule["runtime_sources"]:
            source = root / relative
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes((Path(__file__).resolve().parents[2] / relative).read_bytes())
        mapping["assets"].append({"path": rule["export_path"], "guid": "c" * 32, "code_quarantined": True})
        comparison["candidates"].append({
            "assembly": rule["assembly"], "full_name": rule["full_name"],
            "status": "compatible_layout", "issues": [], "original_token": "0x02000423",
            "loaded_scripts": [{"path": rule["maintained_path"], "guid": "d" * 32,
                "file_id": 11500000, "class_resolved": True,
                "source_sha256": hashlib.sha256((root / rule["maintained_path"]).read_bytes()).hexdigest()}]})
        comparison["source_fingerprint"] = summary["source_fingerprint"] = artifact_fingerprint(root)
        path.write_text(json.dumps(comparison))
        return root, work, export, mapping, comparison, path, summary

    def test_two_approved_types_keep_distinct_script_and_authored_identities(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.two_rule_fixture(directory)
            before = artifact_fingerprint(root)
            with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                result = resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            self.assertEqual(2, len(result["bindings"]))
            raw = ("--- !u!114 &11400000\nMonoBehaviour:\n"
                   "  m_Script: {fileID: 11500000, guid: " + "a" * 32 + ", type: 3}\n"
                   "  m_guid: " + "a" * 32 + "\n"
                   "--- !u!114 &11400001\nMonoBehaviour:\n"
                   "  m_Script: {fileID: 11500000, guid: " + "c" * 32 + ", type: 3}\n"
                   "  m_guid: " + "c" * 32 + "\n  m_sceneName: s_boot\n").encode()
            rewritten, count = rewrite_script_pointers(raw, result["bindings"])
            prefix = "  m_Script: {fileID: 11500000, guid: "
            expected = raw.replace((prefix + "a" * 32).encode(), (prefix + "b" * 32).encode())
            expected = expected.replace((prefix + "c" * 32).encode(), (prefix + "d" * 32).encode())
            self.assertEqual(2, count)
            self.assertEqual(expected, rewritten)
            self.assertIn(("  m_guid: " + "a" * 32 + "\n").encode(), rewritten)
            self.assertIn(("  m_guid: " + "c" * 32 + "\n").encode(), rewritten)
            self.assertEqual(before, artifact_fingerprint(root))

    def test_one_unverified_type_aborts_a_multi_type_binding(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.two_rule_fixture(directory)
            comparison["candidates"][1].update(status="blocked", issues=[{"reason": "field mismatch"}])
            path.write_text(json.dumps(comparison))
            before = artifact_fingerprint(root)
            with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                with self.assertRaisesRegex(ValueError, "layout is unresolved"):
                    resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
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


class UguiScriptBindingTests(unittest.TestCase):
    def fixture(self, directory):
        root, work, export, mapping, comparison, path, summary = ScriptBindingTests.fixture(self, directory)
        package = root / "Library/PackageCache/com.unity.ugui@1.0.0"
        # Test resolver failures without downloading package or game content.
        # Actual package/native/Unity compatibility has separate integration gates.
        (root / "Packages").mkdir(exist_ok=True)
        (root / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.ugui": "1.0.0"}}))
        lock = {"version": "1.0.0", "depth": 0, "source": "builtin",
                "dependencies": {"com.unity.modules.ui": "1.0.0", "com.unity.modules.imgui": "1.0.0"}}
        (root / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": {"com.unity.ugui": lock}}))
        hashes = {}
        for relative in PACKAGE_RULES[0]["runtime_sources"]:
            suffix = relative[len("Packages/com.unity.ugui/"):]
            target = package / suffix
            target.parent.mkdir(parents=True, exist_ok=True)
            data = ("// Distinct resolver fixture " + suffix + "\n").encode()
            target.write_bytes(data)
            hashes[relative] = hashlib.sha256(data).hexdigest()
        descriptor = json.dumps({"name": "com.unity.ugui", "version": "1.0.0"}).encode()
        (package / "package.json").write_bytes(descriptor)
        for index, rule in enumerate(PACKAGE_RULES):
            suffix = rule["maintained_path"][len("Packages/com.unity.ugui/"):]
            (package / (suffix + ".meta")).write_text("fileFormatVersion: 2\nguid: " + hashlib.sha256(suffix.encode()).hexdigest()[:32] + "\n")
        patches = [patch.dict("lucidlib.scriptbindings.UGUI_RUNTIME_SOURCES", hashes, clear=True),
                   patch("lucidlib.scriptbindings.UGUI_PACKAGE_SHA256", hashlib.sha256(descriptor).hexdigest())]
        for guard in patches:
            guard.start()
            self.addCleanup(guard.stop)
        for index, rule in enumerate(PACKAGE_RULES):
            guid = str(index + 1) * 32
            original = export / rule["export_path"]
            original.parent.mkdir(parents=True, exist_ok=True)
            original.write_text("// Original export declaration fixture\n")
            original.with_name(original.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n")
            mapping["assets"].append({"path": rule["export_path"], "guid": guid, "code_quarantined": True})
            suffix = rule["maintained_path"][len("Packages/com.unity.ugui/"):]
            source = package / suffix
            target_guid = GUID.findall(source.with_name(source.name + ".meta").read_text())[0]
            comparison["candidates"].append({"assembly": rule["assembly"], "full_name": rule["full_name"],
                "status": "compatible_layout", "issues": [], "original_token": "0x02000001",
                "loaded_scripts": [{"path": rule["maintained_path"], "guid": target_guid, "file_id": 11500000,
                    "class_resolved": True, "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                    "source_path": str(source), "package_name": "com.unity.ugui", "package_version": "1.0.0",
                    "package_builtin": True, "package_resolved_path": str(package)}]})
        comparison["source_fingerprint"] = summary["source_fingerprint"] = artifact_fingerprint(root)
        path.write_text(json.dumps(comparison))
        return root, work, export, mapping, comparison, path, summary, package

    def component_document(self, index):
        own = (
            "  m_IgnoreLayout: 0\n  m_MinWidth: -1\n  m_MinHeight: -1\n  m_PreferredWidth: 120\n  m_PreferredHeight: 60\n  m_FlexibleWidth: -1\n  m_FlexibleHeight: -1\n  m_LayoutPriority: 1\n",
            "  m_HorizontalFit: 0\n  m_VerticalFit: 2\n",
            "  m_AspectMode: 4\n  m_AspectRatio: 1.77\n")[index]
        return ("--- !u!114 &" + str(index + 1) + "\nMonoBehaviour:\n"
                "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                "  m_GameObject: {fileID: 100}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                "  m_Script: {fileID: 11500000, guid: " + str(index + 1) * 32 + ", type: 3}\n"
                "  m_Name:\n  m_EditorClassIdentifier:\n" + own).encode()

    def test_corrupt_package_component_fields_abort_even_beside_valid_documents(self):
        bindings = [{**rule, "exported_guid": str(i + 1) * 32, "exported_file_id": 11500000,
                     "target_guid": str(i + 4) * 32, "target_file_id": 11500000} for i, rule in enumerate(PACKAGE_RULES)]
        cases = []
        for i in range(3):
            valid = self.component_document(i)
            cases += [valid + b"  unknown: 1\n", valid + b"  m_Enabled: 1\n",
                      valid.replace(b"  m_Enabled: 1", b"  m_Enabled: 2"),
                      valid.replace(b"  m_GameObject: {fileID: 100}", b"  m_GameObject: {fileID: 0}"),
                      valid.replace(b"  m_Script:", b"    m_Script:")]
        cases += [self.component_document(0).replace(b"m_MinWidth: -1", b"m_MinWidth: " + value)
                  for value in (b"Infinity", b"NaN", b"1e100", b"nope", b"1_0", "١".encode())]
        cases += [self.component_document(0).replace(b"m_LayoutPriority: 1", b"m_LayoutPriority: 2147483648"),
                  self.component_document(1).replace(b"m_VerticalFit: 2", b"m_VerticalFit: 99"),
                  self.component_document(2).replace(b"m_AspectMode: 4", b"m_AspectMode: -1")]
        cases += [self.component_document(0).replace(b"m_LayoutPriority: 1", "m_LayoutPriority: ١".encode()),
                  self.component_document(0).replace(b"fileID: 100", "fileID: ١".encode()),
                  self.component_document(0).replace(b"fileID: 11500000", "fileID: ١١٥٠٠٠٠٠".encode()),
                  self.component_document(0).replace(b"--- !u!114 &1", b"--- !u!114 &1 stripped"),
                  self.component_document(0).replace(b"MonoBehaviour:", b"MonoBehaviour: ")]
        for corrupt in cases:
            with self.subTest(document=corrupt):
                with self.assertRaises(ValueError):
                    rewrite_script_pointers(self.component_document(0) + corrupt, bindings)

    def test_all_three_package_scripts_use_loaded_identity_and_preserve_authored_values(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            before = artifact_fingerprint(root)
            with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                result = resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            self.assertEqual(4, len(result["bindings"]))
            raw = b""
            for index in range(3):
                raw += self.component_document(index)
            actual, count = rewrite_script_pointers(raw, result["bindings"])
            expected = raw
            for binding in result["bindings"][1:]:
                expected = expected.replace(("  m_Script: {fileID: 11500000, guid: " + binding["exported_guid"]).encode(),
                    ("  m_Script: {fileID: 11500000, guid: " + binding["target_guid"]).encode())
            self.assertEqual(3, count)
            self.assertEqual(expected, actual)
            self.assertEqual((actual, 0), rewrite_script_pointers(actual, result["bindings"]))
            self.assertEqual(before, artifact_fingerprint(root))

    def test_package_resolution_source_descriptor_and_meta_changes_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            files = [root / "Packages/manifest.json", root / "Packages/packages-lock.json", package / "package.json"]
            files += [package / relative[len("Packages/com.unity.ugui/"):] for relative in PACKAGE_RULES[0]["runtime_sources"]]
            files += [package / (rule["maintained_path"][len("Packages/com.unity.ugui/"):] + ".meta") for rule in PACKAGE_RULES]
            for source in files:
                original = source.read_bytes()
                if source.name in ("manifest.json", "packages-lock.json"):
                    value = json.loads(original)
                    if source.name == "manifest.json": value["dependencies"]["com.unity.ugui"] = "2.0.0"
                    else: value["dependencies"]["com.unity.ugui"]["source"] = "local"
                    source.write_text(json.dumps(value))
                elif source.suffix == ".meta": source.write_text("guid: " + "f" * 32 + "\n")
                else: source.write_bytes(original + b"\n// changed package input\n")
                with self.subTest(path=source), patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                    with self.assertRaises(ValueError):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
                source.write_bytes(original)

    def test_relative_project_root_joins_absolute_loaded_package_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            previous = Path.cwd()
            try:
                os.chdir(root.parent)
                with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                    result = resolve_bindings(Path(root.name), work, export, mapping, input_fingerprint=self.input_fingerprint)
                self.assertEqual(4, len(result["bindings"]))
            finally:
                os.chdir(previous)

    def test_symlinked_project_root_is_rejected_before_editor(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            alias = root.with_name("aliased project")
            alias.symlink_to(root, target_is_directory=True)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                with self.assertRaisesRegex(ValueError, "symlinked project root"):
                    resolve_bindings(alias, work, export, mapping, input_fingerprint=self.input_fingerprint)
            run.assert_not_called()

    def test_loaded_package_must_be_the_pinned_physical_builtin_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            original = copy.deepcopy(comparison)
            mutations = {"source_path": str(root / "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutElement.cs"),
                         "package_resolved_path": str(root / "Packages/com.unity.ugui"),
                         "package_name": "com.example.ugui", "package_version": "2.0.0", "package_builtin": False}
            for key, value in mutations.items():
                for missing in (False, True):
                    altered = copy.deepcopy(original)
                    script = altered["candidates"][1]["loaded_scripts"][0]
                    if missing: script.pop(key)
                    else: script[key] = value
                    path.write_text(json.dumps(altered))
                    with self.subTest(field=key, missing=missing), patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                        with self.assertRaisesRegex(ValueError, "physical source"):
                            resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)

    def test_ambiguous_package_json_keys_are_rejected_before_editor(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            manifest = root / "Packages/manifest.json"
            lock = root / "Packages/packages-lock.json"
            builtin = json.dumps(json.loads(lock.read_text())["dependencies"]["com.unity.ugui"])
            cases = [(manifest, '{"dependencies": {}, "dependencies": {"com.unity.ugui": "1.0.0"}}'),
                     (manifest, '{"dependencies": {"com.unity.ugui": "2.0.0", "com.unity.ugui": "1.0.0"}}'),
                     (lock, '{"dependencies": {}, "dependencies": {"com.unity.ugui": ' + builtin + '}}'),
                     (lock, '{"dependencies": {"com.unity.ugui": {}, "com.unity.ugui": ' + builtin + '}}'),
                     (lock, '{"dependencies": {"com.unity.ugui": ' + builtin.replace('"source": "builtin"', '"source": "local", "source": "builtin"') + '}}')]
            for source, content in cases:
                before = source.read_bytes()
                source.write_text(content)
                with self.subTest(source=source, content=content), patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                    with self.assertRaisesRegex(ValueError, "Duplicate key"):
                        resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
                run.assert_not_called()
                source.write_bytes(before)

    def test_package_cache_symlink_is_rejected_before_editor(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary, package = self.fixture(directory)
            target = package.with_name("other-package")
            package.rename(target)
            package.symlink_to(target, target_is_directory=True)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                with self.assertRaisesRegex(ValueError, "symlink"):
                    resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()

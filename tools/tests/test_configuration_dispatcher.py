"""Input-free regression tests for the configuration extraction dispatch seam."""

import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import scriptbindings, verification
from lucidlib.verification import artifact_fingerprint
import test_assets
import test_scriptbindings
from test_configurationbindings import asset, binding, document, game_document, runtime_document


class ConfigurationDispatcherTests(unittest.TestCase):
    def test_all_three_documents_keep_payload_bytes_and_are_idempotent(self):
        rows = [binding(i) for i in range(3)]
        before = copy.deepcopy(rows)
        documents = [document(0, "  ActiveConfig: " + asset(12) + "\n"), runtime_document(), game_document()]
        for index, raw in enumerate(documents):
            with self.subTest(owner=rows[index]["full_name"]):
                row = rows[index]
                old = ("  m_Script: {fileID: 11500000, guid: " + row["exported_guid"] + ", type: 3}").encode()
                new = ("  m_Script: {fileID: 11500000, guid: " + row["target_guid"] + ", type: 3}").encode()
                actual, count = scriptbindings.rewrite_prepared_script_pointers(raw, rows)
                self.assertEqual(1, count)
                self.assertEqual(raw.replace(old, new), actual)
                self.assertEqual((actual, 0), scriptbindings.rewrite_prepared_script_pointers(actual, rows))
                if index:
                    # The old scalar function still rejects these real list shapes.
                    with self.assertRaisesRegex(ValueError, "field shape"):
                        scriptbindings.rewrite_script_pointers(raw, rows)
                else:
                    self.assertEqual((actual, count), scriptbindings.rewrite_script_pointers(raw, rows))
        self.assertEqual(before, rows)

    def test_the_existing_five_bindings_keep_scalar_and_ugui_behavior(self):
        legacy = []
        for index, rule in enumerate(scriptbindings.RULES[:5]):
            legacy.append(dict(rule, exported_guid="abcde"[index] * 32,
                               exported_file_id=11500000, target_guid=str(index + 5) * 32,
                               target_file_id=11500000))
        raw = []
        for index in range(2):
            raw.append(("--- !u!114 &" + str(index + 1) + "\nMonoBehaviour:\n"
                        "  m_Script: {fileID: 11500000, guid: " + legacy[index]["exported_guid"] + ", type: 3}\n"
                        "  m_Name: Existing definition\n  m_guid: " + "f" * 32 + "\n").encode())
        fixture = test_scriptbindings.UguiScriptBindingTests("runTest")
        for index in range(3):
            raw.append(fixture.component_document(index).replace((str(index + 1) * 32).encode(),
                                                                  legacy[index + 2]["exported_guid"].encode()))
        content = b"".join(raw)
        expected = scriptbindings.rewrite_script_pointers(content, legacy)
        actual = scriptbindings.rewrite_prepared_script_pointers(content, legacy + [binding(i) for i in range(3)])
        self.assertEqual(5, expected[1])
        self.assertEqual(expected, actual)
        bad = content.replace(b"m_HorizontalFit: 0", b"m_HorizontalFit: 99")
        for rewrite in (scriptbindings.rewrite_script_pointers, scriptbindings.rewrite_prepared_script_pointers):
            with self.subTest(rewrite=rewrite.__name__), self.assertRaisesRegex(ValueError, "integer or enum"):
                rewrite(bad, legacy + [binding(i) for i in range(3)])

    def test_unselected_configuration_policy_does_not_change_scalar_strings(self):
        row = dict(scriptbindings.RULES[0], exported_guid="a" * 32,
                   exported_file_id=11500000, target_guid="b" * 32, target_file_id=11500000)
        raw = ("--- !u!114 &1\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + "a" * 32 + ", type: 3}\n"
               "  m_Text: \"configuration " + binding(1)["exported_guid"] + "\\tvalue\"\n"
               "  m_Name: unchanged\ttext\n").encode()
        self.assertEqual(scriptbindings.rewrite_script_pointers(raw, [row]),
                         scriptbindings.rewrite_prepared_script_pointers(raw, [row, binding(1)]))
        self.assertEqual((b"ordinary text", 0), scriptbindings.rewrite_prepared_script_pointers(b"ordinary text", [binding(1)]))

    def test_duplicate_export_guid_across_routes_rejects_without_mutation(self):
        configuration = binding(1)
        scalar = dict(scriptbindings.RULES[0], exported_guid=configuration["exported_guid"],
                      exported_file_id=11500000, target_guid="9" * 32, target_file_id=11500000)
        rows = [configuration, scalar]
        before = copy.deepcopy(rows)
        raw = runtime_document()
        with self.assertRaisesRegex(ValueError, "Duplicate exported script binding"):
            scriptbindings.rewrite_prepared_script_pointers(raw, rows)
        self.assertEqual(before, rows)
        self.assertEqual(raw, runtime_document())

    def test_only_the_exact_finite_owner_pair_receives_configuration_lists(self):
        raw = runtime_document()
        for changed in (dict(binding(1), assembly="Other.Runtime"),
                        dict(binding(1), full_name="Hardlight.OtherConfiguration")):
            with self.subTest(owner=changed), self.assertRaisesRegex(ValueError, "field shape"):
                scriptbindings.rewrite_prepared_script_pointers(raw, [changed])
        self.assertEqual(1, scriptbindings.rewrite_prepared_script_pointers(raw, [binding(1)])[1])

    def test_malformed_selected_documents_do_not_escape_through_the_scalar_route(self):
        raw = runtime_document()
        cases = [raw + b"  m_unknown: []\n", raw.replace(b"  m_Script:", b"   m_Script:"),
                 raw.replace(b"MonoBehaviour:", b"Other:"), raw.replace(b"fileID: 11500000", b"fileID: 11400000"),
                 raw.replace(b"--- !u!114 &11400000", b"--- !u!114 &11400001"),
                 raw.replace(b"  - ", b"   - "), raw + raw]
        for content in cases:
            rows = [binding(i) for i in range(3)]
            before = copy.deepcopy(rows)
            with self.subTest(content=content), self.assertRaises(ValueError):
                scriptbindings.rewrite_prepared_script_pointers(content, rows)
            self.assertEqual(before, rows)


class ConfigurationResolverTests(unittest.TestCase):
    def fixture(self, directory):
        # These are tool proof inputs only; no real supplied bundle, native
        # metadata or loaded Unity layout is substituted or claimed qualified.
        root, work, export, mapping, comparison, path, summary = test_scriptbindings.ScriptBindingTests.fixture(self, directory)
        for index, rule in enumerate(scriptbindings.CONFIGURATION_RULES):
            original = export / rule["export_path"]
            original.parent.mkdir(parents=True, exist_ok=True)
            original.write_text("// Synthetic quarantined configuration declaration.\n")
            original.with_name(original.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + binding(index)["exported_guid"] + "\n")
            for relative in rule["runtime_sources"]:
                source = root / relative
                source.parent.mkdir(parents=True, exist_ok=True)
                source.write_bytes((Path(__file__).resolve().parents[2] / relative).read_bytes())
            source = root / rule["maintained_path"]
            source.with_name(source.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + binding(index)["target_guid"] + "\n")
            mapping["assets"].append(dict(path=rule["export_path"], guid=binding(index)["exported_guid"], code_quarantined=True))
            comparison["candidates"].append(dict(assembly=rule["assembly"], full_name=rule["full_name"],
                status="compatible_layout", issues=[], original_token="0x02000001",
                loaded_scripts=[dict(path=rule["maintained_path"], guid=binding(index)["target_guid"], file_id=11500000,
                    class_resolved=True, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest())]))
        comparison["source_fingerprint"] = summary["source_fingerprint"] = artifact_fingerprint(root)
        path.write_text(json.dumps(comparison))
        return root, work, export, mapping, comparison, path, summary

    def test_three_configuration_rules_join_exact_loaded_script_identities(self):
        with tempfile.TemporaryDirectory(prefix="Lucid configuration resolver ") as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            before = artifact_fingerprint(root)
            with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary) as run:
                actual = scriptbindings.resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            run.assert_called_once_with(root, work)
            self.assertEqual([rule["full_name"] for rule in (scriptbindings.RULES[0],) + scriptbindings.CONFIGURATION_RULES],
                             [row["full_name"] for row in actual["bindings"]])
            self.assertEqual([binding(i)["target_guid"] for i in range(3)], [row["target_guid"] for row in actual["bindings"][1:]])
            self.assertEqual(before, artifact_fingerprint(root))

    def test_absent_configuration_export_does_not_invent_a_binding_or_launch_editor(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                actual = scriptbindings.resolve_bindings(root, work, export, {"assets": []})
            run.assert_not_called()
            self.assertEqual("not_applicable", actual["status"])
            self.assertEqual([], actual["bindings"])

    def test_each_configuration_source_and_missing_source_aborts_before_layout(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            for relative in scriptbindings.CONFIGURATION_RULES[0]["runtime_sources"]:
                source = root / relative
                before = source.read_bytes()
                source.write_bytes(before + b"\n// Changed tool fixture source.\n")
                with self.subTest(source=relative), patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                    with self.assertRaisesRegex(ValueError, "behavior changed"):
                        scriptbindings.resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
                run.assert_not_called()
                source.write_bytes(before)
            source = root / scriptbindings.CONFIGURATION_RULES[1]["maintained_path"]
            source.unlink()
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                with self.assertRaisesRegex(ValueError, "missing"):
                    scriptbindings.resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)
            run.assert_not_called()

    def test_configuration_release_layout_source_and_meta_failures_remain_strict(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            with patch("lucidlib.scriptbindings.run_layout_inventory") as run:
                with self.assertRaisesRegex(ValueError, "supplied release"):
                    scriptbindings.resolve_bindings(root, work, export, mapping, input_fingerprint={"critical_files": {}})
            run.assert_not_called()
            for change in ("layout", "class", "path", "guid", "hash"):
                altered = copy.deepcopy(comparison)
                candidate = altered["candidates"][2]
                target = candidate["loaded_scripts"][0]
                if change == "layout": candidate.update(status="blocked", issues=[dict(reason="field mismatch")])
                if change == "class": target["class_resolved"] = False
                if change == "path": target["path"] = "Assets/Recovered/RuntimeConfiguration.cs"
                if change == "guid": target["guid"] = "9" * 32
                if change == "hash": target["source_sha256"] = "9" * 64
                path.write_text(json.dumps(altered))
                with self.subTest(change=change), patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary):
                    with self.assertRaises(ValueError):
                        scriptbindings.resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint)


class ConfigurationRosterTests(unittest.TestCase):
    def test_three_genuine_editor_cases_are_individually_required(self):
        names = (
            "ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.ActiveConfigurationPreservesItsSerializedRuntimeReferenceAndGetter",
            "ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.RuntimeConfigurationKeepsTwelveOrderedSlotsAndCreatesReadonlyWrappers",
            "ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.GameLevelsPreservesBaseOverridesAndAuthoredReferenceOrderWithoutCaching",
        )
        for name in names:
            with self.subTest(name=name):
                self.assertEqual(1, verification.EDITMODE_TESTS.count(name))
        with tempfile.TemporaryDirectory() as directory:
            xml = Path(directory) / "editmode.xml"
            for absent in names:
                cases = ''.join('<test-case fullname="' + name + '" result="Passed" />' for name in names if name != absent)
                xml.write_text('<test-run result="Passed" failed="0">' + cases + '</test-run>')
                result = verification.test_verdict(xml, names)
                self.assertEqual("incomplete", result["status"])
                self.assertEqual([absent], result["missing_required_tests"])
            cases = ''.join('<test-case fullname="' + name + '" result="Passed" />' for name in names)
            xml.write_text('<test-run result="Passed" failed="0">' + cases + '</test-run>')
            self.assertEqual("complete", verification.test_verdict(xml, names)["status"])


if __name__ == "__main__":
    unittest.main()

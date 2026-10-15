import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import verification
from lucidlib.verification import artifact_fingerprint, test_verdict, verified_receipt


class VerificationTests(unittest.TestCase):
    def xml(self, path, names, result="Passed"):
        # Parameterized fullnames carry quotes, so names are escaped as attributes.
        from xml.sax.saxutils import quoteattr
        cases = ''.join('<test-case fullname=' + quoteattr(name) + ' result="Passed" />' for name in names)
        path.write_text('<test-run result="' + result + '" failed="0">' + cases + '</test-run>')

    def test_identity_hashes_content_separately_from_prepared_assets(self):
        with tempfile.TemporaryDirectory(prefix="Lucid project ") as temporary:
            root = Path(temporary)
            (root / "Assets/Recovered").mkdir(parents=True)
            (root / "Assets/Source.cs").write_text("maintained code")
            asset = root / "Assets/Recovered/scene.unity"
            asset.write_text("first asset")
            source = artifact_fingerprint(root)
            prepared = artifact_fingerprint(root, True)
            asset.write_text("changed asset")
            self.assertEqual(source, artifact_fingerprint(root))
            self.assertNotEqual(prepared, artifact_fingerprint(root, True))
            (root / "Assets/Source.cs").write_text("changed code")
            self.assertNotEqual(source, artifact_fingerprint(root))

    def test_prepared_identity_includes_asset_directories_named_like_build_caches(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for base in ("Assets/Recovered", "Assets/StreamingAssets"):
                for name in ("obj", "bin", "__pycache__"):
                    path = root / base / name / "mesh.bundle"
                    path.parent.mkdir(parents=True)
                    path.write_bytes(b"original bundle")
                    before = artifact_fingerprint(root, True)
                    path.write_bytes(b"changed bundle")
                    self.assertNotEqual(before, artifact_fingerprint(root, True))
            source_before = artifact_fingerprint(root)
            for base in ("Assets", "Packages", "tools"):
                for name in ("obj", "bin", "__pycache__"):
                    path = root / base / name / "temporary.cs"
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text("build cache")
            self.assertEqual(source_before, artifact_fingerprint(root))

    def test_identity_rejects_symlink_instead_of_hashing_outside_project(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "Assets").mkdir()
            (root / "outside.cs").write_text("outside")
            (root / "Assets/Link.cs").symlink_to(root / "outside.cs")
            with self.assertRaisesRegex(ValueError, "symlink"):
                artifact_fingerprint(root)

    def test_passing_unrelated_suite_does_not_prove_required_game_behavior(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            self.xml(path, ["Unrelated.RequiredTest"])
            result = test_verdict(path, ["Game.OriginalStartup"])
            self.assertEqual("incomplete", result["status"])
            self.assertEqual(["Game.OriginalStartup"], result["missing_required_tests"])
            self.xml(path, ["Game.OriginalStartup"])
            self.assertEqual("complete", test_verdict(path, ["Game.OriginalStartup"])["status"])

    def test_unregistered_project_test_keeps_the_run_incomplete(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"

            def run(project, package):
                path.write_text(
                    '<test-run result="Passed" failed="0">'
                    '<test-suite type="Assembly" name="/p/Library/ScriptAssemblies/ProjectLucid.EditMode.dll" result="Passed">' +
                    ''.join('<test-case fullname="' + name + '" result="Passed" />' for name in project) +
                    '</test-suite><test-suite type="Assembly" name="Unity.Package.Tests.dll" result="Passed">' +
                    ''.join('<test-case fullname="' + name + '" result="Passed" />' for name in package) +
                    '</test-suite></test-run>')
                return test_verdict(path, ["Game.Required", "Game.Case(1)"])

            # A passing, skipped or parameterized project case must be named in the registry; a
            # package's own tests are not project tests.
            self.assertEqual("complete", run(["Game.Required", "Game.Case(1)"], ["Package.Stub"])["status"])
            for extra in ("Game.Extra", "Game.Case(2)"):
                result = run(["Game.Required", "Game.Case(1)", extra], [])
                self.assertEqual("incomplete", result["status"])
                self.assertEqual(["Unregistered project tests: " + extra], result["errors"])

    def test_passing_root_cannot_hide_failed_descendants(self):
        controls = (
            '<test-case fullname="Other.Test" result="Failed"/>',
            '<test-case fullname="Game.Required" result="Failed"/>',
            '<test-suite fullname="Failed.Fixture" result="Failed"/>',
        )
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for failure in controls:
                with self.subTest(failure=failure):
                    path.write_text('<test-run result="Passed" failed="0">'
                                    '<test-suite result="Passed">'
                                    '<test-case fullname="Game.Required" result="Passed"/>'
                                    + failure + '</test-suite></test-run>')
                    self.assertEqual("incomplete", test_verdict(path, ["Game.Required"])["status"])

    def test_required_pass_cannot_hide_a_conflicting_duplicate_outcome(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for outcome in ("Failed", "Skipped", "Inconclusive", "Unknown", ""):
                for reverse in (False, True):
                    with self.subTest(outcome=outcome, reverse=reverse):
                        cases = ['<test-case fullname="Game.Required" result="Passed"/>',
                                 '<test-case fullname="Game.Required" result="' + outcome + '"/>']
                        path.write_text('<test-run result="Passed" failed="0">'
                                        + ''.join(reversed(cases) if reverse else cases) + '</test-run>')
                        verdict = test_verdict(path, ["Game.Required"])
                        self.assertEqual("incomplete", verdict["status"])
                        self.assertEqual(["Game.Required"], verdict["missing_required_tests"])

    def test_optional_skipped_and_inconclusive_cases_do_not_replace_required_passes(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for outcome in ("Skipped", "Inconclusive"):
                with self.subTest(outcome=outcome):
                    optional = '<test-case fullname="Optional.Test" result="' + outcome + '"/>'
                    path.write_text('<test-run result="Passed" failed="0">'
                                    '<test-case fullname="Game.Required" result="Passed"/>'
                                    + optional + '</test-run>')
                    self.assertEqual("complete", test_verdict(path, ["Game.Required"])["status"])
                    self.assertEqual("incomplete", test_verdict(path, ["Optional.Test"])["status"])

    def test_unrecognised_case_or_suite_outcome_is_not_successful_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for node in ('<test-case fullname="Optional.Test"/>',
                         '<test-case fullname="Optional.Test" result="Unknown"/>',
                         '<test-suite result="Unknown"/>'):
                with self.subTest(node=node):
                    path.write_text('<test-run result="Passed" failed="0">'
                                    '<test-case fullname="Game.Required" result="Passed"/>'
                                    + node + '</test-run>')
                    self.assertEqual("incomplete", test_verdict(path, ["Game.Required"])["status"])

    def test_declared_outcome_counts_must_match_descendant_case_records(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for summary in ('total="2"', 'passed="2"', 'skipped="1"', 'inconclusive="1"',
                            'passed="-1"', 'total="not-a-count"'):
                for summary_owner in ("run", "suite"):
                    with self.subTest(summary=summary, owner=summary_owner):
                        run_summary = ' ' + summary if summary_owner == "run" else ''
                        suite_summary = ' ' + summary if summary_owner == "suite" else ''
                        path.write_text('<test-run result="Passed" failed="0"' + run_summary + '>'
                                        '<test-suite result="Passed"' + suite_summary + '>'
                                        '<test-case fullname="Game.Required" result="Passed"/>'
                                        '</test-suite></test-run>')
                        self.assertEqual("incomplete", test_verdict(path, ["Game.Required"])["status"])
            path.write_text('<test-run result="Passed" failed="0">'
                            '<test-suite result="Passed" failed="1">'
                            '<test-case fullname="Game.Required" result="Passed"/>'
                            '</test-suite></test-run>')
            self.assertEqual("incomplete", test_verdict(path, ["Game.Required"])["status"])

    def test_consistent_nested_counts_and_repeated_passes_remain_supported(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            path.write_text('<test-run result="Passed" testcasecount="99" total="4" passed="2" failed="0" '
                            'skipped="1" inconclusive="1"><test-suite result="Passed" testcasecount="99" total="4" '
                            'passed="2" failed="0" skipped="1" inconclusive="1">'
                            '<test-case fullname="Game.Required" result="Passed"/>'
                            '<test-suite result="Passed" total="3" passed="1" failed="0" '
                            'skipped="1" inconclusive="1">'
                            '<test-case fullname="Game.Required" result="Passed"/>'
                            '<test-case fullname="Optional.Skip" result="Skipped"/>'
                            '<test-case fullname="Optional.Inconclusive" result="Inconclusive"/>'
                            '</test-suite></test-suite></test-run>')
            verdict = test_verdict(path, ["Game.Required"])
            self.assertEqual("complete", verdict["status"])
            self.assertEqual(1, verdict["passed"])

    def test_receipt_rechecks_failed_xml_even_with_its_matching_digest(self):
        # This is inert supplied evidence, not an actual generated Unity receipt.
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            reports = work / "reports"
            reports.mkdir()
            from xml.sax.saxutils import quoteattr
            names = verification.PLAYMODE_TESTS
            cases = ''.join('<test-case fullname=' + quoteattr(name) + ' result="Passed"/>' for name in names)
            xml = reports / "playmode.xml"
            for failure in ('<test-case fullname="Unrelated.Test" result="Failed"/>',
                            '<test-case fullname=' + quoteattr(names[0]) + ' result="Skipped"/>',
                            '<test-suite result="Failed"/>'):
                with self.subTest(failure=failure):
                    xml.write_text('<test-run result="Passed" failed="0">' + cases + failure + '</test-run>')
                    identity = {"source_fingerprint": "synthetic-source", "prepared_asset_fingerprint": "synthetic-assets"}
                    receipt = {"generated_by": "ProjectLucid.run_tests", "status": "complete",
                               "unity_version": verification.UNITY_VERSION, **identity,
                               "verdict": {"xml_sha256": hashlib.sha256(xml.read_bytes()).hexdigest()}}
                    (reports / "playmode-verification.json").write_text(json.dumps(receipt))
                    with self.assertRaisesRegex(ValueError, "incomplete"):
                        verified_receipt(work, "playmode", identity)

    def test_run_tests_retains_outcome_diagnostics_without_launching_unity(self):
        from xml.sax.saxutils import quoteattr
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work = root / "cache"
            names = verification.PLAYMODE_TESTS
            cases = ''.join('<test-case fullname=' + quoteattr(name) + ' result="Passed"/>' for name in names)
            controls = ('<test-case fullname="Other.Test" result="Failed"/>',
                        '<test-suite fullname="Teardown.Fixture" result="Failed"/>',
                        '<test-case fullname=' + quoteattr(names[0]) + ' result="Skipped"/>')
            for contradiction in controls:
                with self.subTest(contradiction=contradiction):
                    def supplied_xml(command, **kwargs):
                        Path(command[command.index("-testResults") + 1]).write_text(
                            '<test-run result="Passed" failed="0">' + cases + contradiction + '</test-run>')
                        Path(command[command.index("-logFile") + 1]).write_text(
                            "Unity Editor version: " + verification.UNITY_VERSION)
                        return SimpleNamespace(returncode=0)
                    with patch("lucidlib.bootstrap.CACHE_ROOT", work), patch.object(
                            verification, "find_editor", return_value=root / "inert-editor"), patch.object(
                            verification.subprocess, "run", side_effect=supplied_xml) as process:
                        report = verification.run_tests(root, work, "playmode")
                    process.assert_called_once()
                    self.assertEqual("incomplete", report["status"])
                    self.assertTrue(report["verdict"]["errors"])
                    self.assertTrue(all(error in report["errors"] for error in report["verdict"]["errors"]))
                    self.assertEqual(hashlib.sha256((work / "reports/playmode.xml").read_bytes()).hexdigest(),
                                     report["verdict"]["xml_sha256"])
                    saved = json.loads((work / "reports/playmode-verification.json").read_text())
                    self.assertEqual(report, saved)

    def test_ui_and_codec_retry_cases_are_required_by_runs_and_receipts(self):
        # Supplied XML exercises qualification policy without launching Unity.
        from xml.sax.saxutils import quoteattr
        required = tuple("ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests." + name for name in (
            "VerificationAssemblyDeclaresAndResolvesPinnedUI",
            "PlayModeAssemblyDeclaresAndResolvesPinnedUI",
            "CoroutineScrollAssemblyDeclaresAndResolvesPinnedUI",
        )) + (
            "ProjectLucid.Tests.RecoveredBehaviorTests.CodecVerificationRetryAfterOutputFailureCountsOnlyItsOwnChecks",
        )
        for name in required:
            self.assertEqual(1, verification.EDITMODE_TESTS.count(name))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work = root / "cache"
            carrier = root / "Assets/Tests/Editor/ProjectLucidFontReferenceConversionCarrier.cs.meta"
            carrier.parent.mkdir(parents=True)
            carrier.write_text("guid: " + "a" * 32 + "\n")
            converter = root / "tools/lucidlib/fontreferences.py"
            converter.parent.mkdir(parents=True)
            converter.write_text("# inert converter identity for supplied XML\n")
            identity = {"source_fingerprint": "synthetic-source", "prepared_asset_fingerprint": "synthetic-assets"}
            xml = root / "supplied.xml"
            self.xml(xml, verification.EDITMODE_TESTS)
            self.assertEqual("complete", test_verdict(xml, verification.EDITMODE_TESTS)["status"])
            for name in required:
                for outcome in (None, "Skipped", "Failed"):
                    with self.subTest(name=name, outcome=outcome):
                        cases = ''.join('<test-case fullname=' + quoteattr(case) + ' result="Passed"/>'
                                        for case in verification.EDITMODE_TESTS if case != name)
                        if outcome is not None:
                            cases += '<test-case fullname=' + quoteattr(name) + ' result=' + quoteattr(outcome) + '/>'
                        xml.write_text('<test-run result="Passed" failed="0">' + cases + '</test-run>')

                        def supplied_xml(command, **kwargs):
                            Path(command[command.index("-testResults") + 1]).write_bytes(xml.read_bytes())
                            Path(command[command.index("-logFile") + 1]).write_text(
                                "Unity Editor version: " + verification.UNITY_VERSION)
                            return SimpleNamespace(returncode=0)

                        with patch("lucidlib.bootstrap.CACHE_ROOT", work), patch.object(
                                verification, "find_editor", return_value=root / "inert-editor"), patch.object(
                                verification, "current_identity", return_value=identity), patch.object(
                                verification.subprocess, "run", side_effect=supplied_xml) as process:
                            report = verification.run_tests(root, work, "editmode")
                        process.assert_called_once()
                        self.assertEqual("incomplete", report["status"])
                        self.assertEqual([name], report["verdict"]["missing_required_tests"])
                        # A caller cannot bless the same incomplete XML by changing the receipt status.
                        report["status"] = "complete"
                        (work / "reports/editmode-verification.json").write_text(json.dumps(report))
                        with self.assertRaisesRegex(ValueError, "incomplete"):
                            verified_receipt(work, "editmode", identity)

    def test_diagnostics_keep_document_order_despite_bottom_up_summary_checks(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            path.write_text('<test-run result="Passed" failed="0">'
                            '<test-case fullname="First.Failure" result="Failed"/>'
                            '<test-suite fullname="Last.Failure" result="Failed"/></test-run>')
            verdict = test_verdict(path, [])
            self.assertEqual("incomplete", verdict["status"])
            self.assertEqual('Inconsistent test-run failed count for <unnamed>: \'0\'; observed 1', verdict["errors"][0])
            self.assertEqual('Failed test-case: First.Failure', verdict["errors"][1])
            self.assertEqual('Failed test-suite: Last.Failure', verdict["errors"][2])

    def test_malformed_or_entity_xml_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "tests.xml"
            for value in ('<test-run', '<!DOCTYPE test-run [<!ENTITY x "data">]><test-run />'):
                path.write_text(value)
                with self.assertRaises(ValueError):
                    test_verdict(path, [])

    def test_changed_content_invalidates_generated_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            (work / "reports").mkdir()
            receipt = {"generated_by": "ProjectLucid.run_tests", "status": "complete",
                       "unity_version": "2022.3.54f1", "source_fingerprint": "older",
                       "prepared_asset_fingerprint": "same"}
            (work / "reports/editmode-verification.json").write_text(json.dumps(receipt))
            with self.assertRaisesRegex(ValueError, "stale"):
                verified_receipt(work, "editmode", {"source_fingerprint": "newer", "prepared_asset_fingerprint": "same"})


if __name__ == "__main__":
    unittest.main()

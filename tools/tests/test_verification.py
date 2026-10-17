import hashlib
import json
from pathlib import Path
import re
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


class IsolationVerdictTests(unittest.TestCase):
    """Supplied recorder lines and XML exercise the verdict; no Unity runs here."""

    NONCE = "a" * 32
    CASE = "ProjectLucid.Tests.Sample.Case(1)"

    def write(self, directory, mode="editmode", lines=None):
        from xml.sax.saxutils import quoteattr
        names = verification.ISOLATION_HOOK_CONTROLS[mode] + (self.CASE,)
        xml = directory / "tests.xml"
        xml.write_text('<test-run result="Passed" failed="0"><test-suite type="Assembly" name="ProjectLucid.EditMode.dll" result="Passed">' +
                       ''.join('<test-case fullname=' + quoteattr(name) + ' result="Passed"/>' for name in names) +
                       '</test-suite></test-run>')
        if lines is None:
            lines = [self.line(name) for name in names]
        report = directory / (mode + "-isolation.jsonl")
        report.write_text("".join(line + "\n" for line in lines))
        return report, xml

    def line(self, test, status="clean", changes=(), nonce=None):
        return json.dumps({"nonce": self.NONCE if nonce is None else nonce, "test": test,
                           "status": status, "changes": list(changes)})

    def control_lines(self, mode="editmode"):
        return [self.line(name) for name in verification.ISOLATION_HOOK_CONTROLS[mode]]

    def test_clean_report_passes(self):
        with tempfile.TemporaryDirectory() as temporary:
            for mode in ("editmode", "playmode"):
                with self.subTest(mode=mode):
                    report, xml = self.write(Path(temporary), mode)
                    verdict = verification.isolation_verdict(report, self.NONCE, xml, mode)
                    self.assertEqual("complete", verdict["status"], verdict["errors"])
                    self.assertEqual(3, verdict["checked"])
                    self.assertEqual(([], [], []), (verdict["leaks"], verdict["allowlisted"], verdict["side_effects"]))
                    self.assertEqual(hashlib.sha256(report.read_bytes()).hexdigest(), verdict["sha256"])

    def test_leak_fails_and_names_the_test_and_each_change(self):
        with tempfile.TemporaryDirectory() as temporary:
            changes = ["added system row 'X'", "changed action row AppShutdown subscribers"]
            report, xml = self.write(Path(temporary), lines=self.control_lines() + [self.line(self.CASE, "leak", changes)])
            verdict = verification.isolation_verdict(report, self.NONCE, xml, "editmode")
            self.assertEqual("incomplete", verdict["status"])
            self.assertEqual([{"test": self.CASE, "changes": changes}], verdict["leaks"])
            self.assertEqual(["Isolation leak in " + self.CASE + ": " + "; ".join(changes)], verdict["errors"])

    def test_missing_report_fails_rather_than_passing(self):
        with tempfile.TemporaryDirectory() as temporary:
            report, xml = self.write(Path(temporary))
            report.unlink()
            with self.assertRaisesRegex(ValueError, "Isolation report missing"):
                verification.isolation_verdict(report, self.NONCE, xml, "editmode")

    def test_allowlisted_leak_passes_but_is_listed(self):
        with tempfile.TemporaryDirectory() as temporary:
            report, xml = self.write(Path(temporary), lines=self.control_lines() +
                                     [self.line(self.CASE, "leak", ["added system row 'X'"])])
            allowlist = {self.CASE: {"reason": "synthetic reason; issue #0", "changes": ("added system row 'X'",)}}
            verdict = verification.isolation_verdict(report, self.NONCE, xml, "editmode", allowlist)
            self.assertEqual("complete", verdict["status"], verdict["errors"])
            self.assertEqual([], verdict["leaks"])
            self.assertEqual([{"test": self.CASE, "changes": ["added system row 'X'"], "reason": "synthetic reason; issue #0"}],
                             verdict["allowlisted"])

    def test_allowlisted_leak_with_any_other_change_list_fails(self):
        # An extra line, a missing line and a reordering: each is a leak, not the allowlisted one.
        expected = ["added system row 'X'", "added system row 'X' untyped reference"]
        allowlist = {self.CASE: {"reason": "synthetic reason; issue #0", "changes": tuple(expected)}}
        with tempfile.TemporaryDirectory() as temporary:
            for changes in (expected + ["added system row 'Hardlight.SystemConfiguration'"],
                            expected[:-1], expected[1:] + expected[:1]):
                with self.subTest(changes=changes):
                    report, xml = self.write(Path(temporary), lines=self.control_lines() +
                                             [self.line(self.CASE, "leak", changes)])
                    verdict = verification.isolation_verdict(report, self.NONCE, xml, "editmode", allowlist)
                    self.assertEqual("incomplete", verdict["status"])
                    self.assertEqual([], verdict["allowlisted"])
                    self.assertEqual([{"test": self.CASE, "changes": changes}], verdict["leaks"])
                    self.assertEqual(["Isolation leak in " + self.CASE + " differs from its allowlisted changes: " +
                                      "; ".join(changes)], verdict["errors"])
            report, xml = self.write(Path(temporary), lines=self.control_lines() + [self.line(self.CASE, "leak", expected)])
            verdict = verification.isolation_verdict(report, self.NONCE, xml, "editmode", allowlist)
            self.assertEqual("complete", verdict["status"], verdict["errors"])
            self.assertEqual([self.CASE], [entry["test"] for entry in verdict["allowlisted"]])

    def test_allowlist_cannot_hide_unchecked_tests_or_outlive_its_leak(self):
        allowlist = {self.CASE: {"reason": "synthetic reason", "changes": ("x",)}}
        with tempfile.TemporaryDirectory() as temporary:
            for status, changes, expected in (
                    ("unchecked", ["no start snapshot: the hook did not see this test start"],
                     "Isolation unchecked for " + self.CASE + ": no start snapshot: the hook did not see this test start"),
                    ("clean", [], "Allowlisted isolation leak no longer leaks; remove its entry: " + self.CASE)):
                with self.subTest(status=status):
                    report, xml = self.write(Path(temporary), lines=self.control_lines() + [self.line(self.CASE, status, changes)])
                    verdict = verification.isolation_verdict(report, self.NONCE, xml, "editmode", allowlist)
                    self.assertEqual("incomplete", verdict["status"])
                    self.assertEqual([expected], verdict["errors"])

    def test_every_project_case_and_hook_control_needs_its_own_line_from_this_run(self):
        control = verification.ISOLATION_HOOK_CONTROLS["editmode"][0]
        others = [self.line(name) for name in verification.ISOLATION_HOOK_CONTROLS["editmode"][1:]]
        with tempfile.TemporaryDirectory() as temporary:
            for lines, expected in (
                    (self.control_lines(), ["Isolation report has no line for: " + self.CASE]),
                    (others + [self.line(self.CASE)],
                     ["Isolation hook control has no single clean line: " + control,
                      "Isolation report has no line for: " + control]),
                    (others + [self.line(self.CASE), self.line(control, nonce="b" * 32)],
                     ["Isolation report line 3 is not from this run: " + control,
                      "Isolation hook control has no single clean line: " + control,
                      "Isolation report has no line for: " + control]),
                    (self.control_lines() + [self.line(self.CASE), self.line(control)],
                     ["Isolation hook control has no single clean line: " + control]),
                    (self.control_lines() + [self.line(self.CASE), "{}", "not json", self.line(self.CASE, "clean", ["x"]),
                                             self.line(self.CASE, "leaked", ["x"]), self.line(self.CASE, "leak", [])],
                     ["Malformed isolation report line " + str(number) for number in (4, 5, 6, 7, 8)])):
                with self.subTest(expected=expected[0]):
                    report, xml = self.write(Path(temporary), lines=lines)
                    verdict = verification.isolation_verdict(report, self.NONCE, xml, "editmode")
                    self.assertEqual("incomplete", verdict["status"])
                    self.assertEqual(expected, verdict["errors"])

    def test_allowlist_is_empty_and_permanent_effects_are_side_effects(self):
        # The allowlist is temporary only; reviewed permanent production effects are side effects.
        self.assertEqual({}, verification.ISOLATION_ALLOWLIST)

    # The native recorder's change list for an initializer that appends one
    # null row: run19 (928e689) line 773 for 'Hardlight.TimeManager', without
    # the SystemConfiguration lines the timing fixture now releases.
    @staticmethod
    def native_row_changes(row):
        added = "added system row '" + row + "'"
        typed = added + " typed reference '" + row + "'"
        return ["changed system row order", added,
                added + " untyped reference",
                added + " untyped reference OnSystemStartup observers",
                added + " untyped reference OnSystemShutdown observers",
                added + " untyped reference deferred InvokeOnValid work",
                typed, typed + " OnSystemStartup observers", typed + " OnSystemShutdown observers",
                typed + " deferred InvokeOnValid work", added + " typed reference names"]

    TIMING = "ProjectLucid.Tests.OriginalGameTimeScaledUtilitiesPreservationTests"
    TIMING_PRESERVATION = "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests"
    GAP = "ProjectLucid.Tests.OriginalPerformanceDependencyGapTests"
    PRESERVATION = "ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests"
    DESTROY_PLAY = "ProjectLucid.Tests.OriginalPerformanceDependencyDestroyPlayTests"

    def test_side_effects_pin_the_native_changes_modes_and_registered_fixtures(self):
        effects = verification.ISOLATION_SIDE_EFFECTS
        self.assertEqual({"TimeScaledUtilities..cctor", "PerformanceDependency..cctor"}, set(effects))
        self.assertEqual(tuple(self.native_row_changes("Hardlight.TimeManager")),
                         effects["TimeScaledUtilities..cctor"]["changes"])
        self.assertEqual(tuple(self.native_row_changes("Hardlight.ScalablePerformance")),
                         effects["PerformanceDependency..cctor"]["changes"])
        self.assertEqual({"editmode": (self.TIMING, self.TIMING_PRESERVATION)}, effects["TimeScaledUtilities..cctor"]["fixtures"])
        self.assertEqual({"editmode": (self.GAP, self.PRESERVATION), "playmode": (self.DESTROY_PLAY,)},
                         effects["PerformanceDependency..cctor"]["fixtures"])
        for name, entry in effects.items():
            with self.subTest(side_effect=name):
                self.assertEqual({"reason", "changes", "fixtures"}, set(entry))
                self.assertTrue(isinstance(entry["reason"], str) and entry["reason"].strip())
                self.assertTrue(set(entry["fixtures"]) <= {"editmode", "playmode"})
                for mode, fixtures in entry["fixtures"].items():
                    registered = verification.EDITMODE_TESTS if mode == "editmode" else verification.PLAYMODE_TESTS
                    for fixture in fixtures:
                        self.assertTrue(any(test.startswith(fixture + ".") for test in registered), fixture)

    def side_effect_verdict(self, mode, cases):
        """The default verdict for the mode's controls plus supplied (test, status, changes) case lines."""
        with tempfile.TemporaryDirectory() as temporary:
            report, xml = self.write(Path(temporary), mode, lines=self.control_lines(mode) +
                                     [self.line(test, status, changes) for test, status, changes in cases])
            xml.write_text(xml.read_text().replace(
                '<test-case fullname="' + self.CASE + '" result="Passed"/>',
                ''.join('<test-case fullname="' + test + '" result="Passed"/>' for test, _, _ in cases)))
            return verification.isolation_verdict(report, self.NONCE, xml, mode)

    def registered_cases(self, mode, fixture):
        registered = verification.EDITMODE_TESTS if mode == "editmode" else verification.PLAYMODE_TESTS
        return [test for test in registered if test.startswith(fixture + ".")]

    def test_exact_side_effect_passes_once_from_any_case_of_an_allowed_fixture(self):
        for name, entry in verification.ISOLATION_SIDE_EFFECTS.items():
            for mode, fixtures in entry["fixtures"].items():
                for fixture in fixtures:
                    for first in self.registered_cases(mode, fixture):
                        with self.subTest(side_effect=name, first=first):
                            changes = list(entry["changes"])
                            verdict = self.side_effect_verdict(mode, [(first, "leak", changes)])
                            self.assertEqual("complete", verdict["status"], verdict["errors"])
                            self.assertEqual(([], []), (verdict["leaks"], verdict["allowlisted"]))
                            self.assertEqual([{"test": first, "side_effect": name, "changes": changes,
                                               "reason": entry["reason"]}], verdict["side_effects"])
        # Unlike an allowlist entry, a side effect an earlier ordering already caused may not occur at all.
        verdict = self.side_effect_verdict("editmode", [(self.GAP + ".DestroyRemovesOnlyItsOwnProfileAndRegistryHandlers",
                                                         "clean", [])])
        self.assertEqual(("complete", [], []), (verdict["status"], verdict["errors"], verdict["side_effects"]))

    def test_side_effect_seen_twice_in_one_report_fails(self):
        changes = list(verification.ISOLATION_SIDE_EFFECTS["PerformanceDependency..cctor"]["changes"])
        destroy = self.GAP + ".DestroyRemovesOnlyItsOwnProfileAndRegistryHandlers"
        for second in (self.GAP + ".HideFollowsValidityAndDestroyKeepsAValidObject",
                       self.PRESERVATION + ".CopyPreservesAliasesAndDestinationCallbacks"):
            with self.subTest(second=second):
                verdict = self.side_effect_verdict("editmode", [(destroy, "leak", changes), (second, "leak", changes)])
                self.assertEqual("incomplete", verdict["status"])
                self.assertEqual(["Isolation side effect PerformanceDependency..cctor seen more than once in one report: " +
                                  ", ".join(sorted([destroy, second]))], verdict["errors"])

    def test_side_effect_with_other_changes_or_from_a_disallowed_fixture_or_mode_is_a_leak(self):
        timing = list(verification.ISOLATION_SIDE_EFFECTS["TimeScaledUtilities..cctor"]["changes"])
        scalable = list(verification.ISOLATION_SIDE_EFFECTS["PerformanceDependency..cctor"]["changes"])
        first = self.TIMING + ".OriginalGameTimingRetainsImmediateAndDeferredFaultPrefixes"
        destroy_play = self.DESTROY_PLAY + ".InvalidProfileDestroysTheWholeGameObject"
        low_gap = "ProjectLucid.Tests.OriginalCoreLowGapTests.FixedWaitReadsFixedDeltaTimeBeforeTheCategoryTimescale"
        for mode, test, changes in (
                # Extra, missing and reordered changes from the allowed first case.
                ("editmode", first, timing + ["added system row 'Hardlight.SystemConfiguration'"]),
                ("editmode", first, timing[:-1]),
                ("editmode", first, timing[1:] + timing[:1]),
                # Another entry's exact changes from this fixture.
                ("editmode", first, scalable),
                # Exact changes from a fixture outside the allowed set.
                ("editmode", low_gap, timing),
                ("editmode", self.CASE, scalable),
                # Exact changes from an allowed fixture of another mode, and a mode the entry does not cover.
                ("playmode", self.GAP + ".DestroyRemovesOnlyItsOwnProfileAndRegistryHandlers", scalable),
                ("editmode", destroy_play, scalable),
                ("playmode", destroy_play, timing)):
            with self.subTest(mode=mode, test=test, last=changes[-1]):
                verdict = self.side_effect_verdict(mode, [(test, "leak", changes)])
                self.assertEqual("incomplete", verdict["status"])
                self.assertEqual([], verdict["side_effects"])
                self.assertEqual([{"test": test, "changes": changes}], verdict["leaks"])
                self.assertEqual(["Isolation leak in " + test + ": " + "; ".join(changes)], verdict["errors"])
        # The one occurrence is accepted, and a further leak in the same case still fails.
        verdict = self.side_effect_verdict("playmode", [(destroy_play, "leak", scalable), (destroy_play, "leak", ["x"])])
        self.assertEqual(["Isolation leak in " + destroy_play + ": x"], verdict["errors"])
        self.assertEqual([destroy_play], [entry["test"] for entry in verdict["side_effects"]])

    def test_recorder_source_writes_the_fields_and_variables_the_verdict_reads(self):
        # A source check that the C# side and this verdict agree; the native run proves the hook fires.
        root = Path(__file__).resolve().parents[2]
        source = (root / "Assets/Tests/IsolationShared/SharedStateIsolationRecorder.cs").read_text(encoding="utf-8")
        self.assertIn("[assembly: TestRunCallback(typeof(ProjectLucid.Tests.SharedStateIsolationRecorder))]", source)
        self.assertEqual(verification.ISOLATION_FIELDS, set(re.findall(r"public string(?:\[\])? (\w+);", source)))
        self.assertIn('ReportVariable = "LUCID_ISOLATION_REPORT"', source)
        self.assertIn('NonceVariable = "LUCID_ISOLATION_NONCE"', source)
        for mode, directory in (("editmode", "Editor"), ("playmode", "PlayMode")):
            required = verification.EDITMODE_TESTS if mode == "editmode" else verification.PLAYMODE_TESTS
            for fullname in verification.ISOLATION_HOOK_CONTROLS[mode]:
                fixture, method = fullname.split(".")[-2:]
                control = (root / "Assets/Tests" / directory / (fixture + ".cs")).read_text(encoding="utf-8")
                self.assertIn("namespace ProjectLucid.Tests", control)
                self.assertIn("public sealed class " + fixture, control)
                self.assertRegex(control, r"public (?:void|IEnumerator) " + method + r"\(\)")
                self.assertEqual(1, required.count(fullname))

    def supplied_run(self, root, work, mode, write_isolation):
        from xml.sax.saxutils import quoteattr
        names = verification.EDITMODE_TESTS if mode == "editmode" else verification.PLAYMODE_TESTS
        seen = {}

        def supplied(command, env, **kwargs):
            seen.update(env)
            Path(command[command.index("-testResults") + 1]).write_text(
                '<test-run result="Passed" failed="0"><test-suite type="Assembly" name="ProjectLucid.PlayMode.dll" result="Passed">' +
                ''.join('<test-case fullname=' + quoteattr(name) + ' result="Passed"/>' for name in names) +
                '</test-suite></test-run>')
            Path(command[command.index("-logFile") + 1]).write_text("Unity Editor version: " + verification.UNITY_VERSION)
            if write_isolation:
                # As a native run: each side effect once, from the first case of an
                # allowed fixture, and an allowlisted test leaking exactly its changes.
                allowlist, pending, lines = verification.ISOLATION_ALLOWLIST, dict(verification.ISOLATION_SIDE_EFFECTS), []
                for name in names:
                    changes = list(allowlist[name]["changes"]) if name in allowlist else []
                    effect = next((key for key, entry in pending.items()
                                   if any(name.startswith(fixture + ".") for fixture in entry["fixtures"].get(mode, ()))), None)
                    if not changes and effect is not None:
                        changes = list(pending.pop(effect)["changes"])
                    lines.append(self.line(name, "leak" if changes else "clean", changes,
                                           nonce=env["LUCID_ISOLATION_NONCE"]) + "\n")
                Path(env["LUCID_ISOLATION_REPORT"]).write_text("".join(lines))
            return SimpleNamespace(returncode=0)

        identity = {"source_fingerprint": "synthetic-source", "prepared_asset_fingerprint": "synthetic-assets"}
        with patch("lucidlib.bootstrap.CACHE_ROOT", work), patch.object(
                verification, "find_editor", return_value=root / "inert-editor"), patch.object(
                verification, "current_identity", return_value=identity), patch.object(
                verification.subprocess, "run", side_effect=supplied):
            return verification.run_tests(root, work, mode), seen, identity

    def test_run_tests_requires_this_runs_isolation_report_and_receipts_recheck_it(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            work = root / "cache"
            report_path = work / "reports/playmode-isolation.jsonl"
            report_path.parent.mkdir(parents=True)
            # A passing report from an earlier run is removed before the Editor starts.
            report_path.write_text("".join(self.line(name) + "\n" for name in verification.PLAYMODE_TESTS))
            report, seen, identity = self.supplied_run(root, work, "playmode", write_isolation=False)
            self.assertFalse(report_path.exists())
            self.assertEqual(str(report_path), seen["LUCID_ISOLATION_REPORT"])
            self.assertRegex(seen["LUCID_ISOLATION_NONCE"], "^[0-9a-f]{32}$")
            self.assertEqual("incomplete", report["status"])
            self.assertEqual("complete", report["verdict"]["status"])
            self.assertEqual("incomplete", report["isolation"]["status"])
            self.assertEqual(["Isolation report missing: " + str(report_path) + "; the per-test detector did not run"],
                             report["errors"])

            report, seen, identity = self.supplied_run(root, work, "playmode", write_isolation=True)
            self.assertEqual("complete", report["status"], report["errors"])
            self.assertEqual(seen["LUCID_ISOLATION_NONCE"], report["isolation"]["nonce"])
            self.assertEqual(len(verification.PLAYMODE_TESTS), report["isolation"]["checked"])
            self.assertEqual([(self.DESTROY_PLAY + ".InvalidProfileDestroysTheWholeGameObject", "PerformanceDependency..cctor")],
                             [(entry["test"], entry["side_effect"]) for entry in report["isolation"]["side_effects"]])
            verified_receipt(work, "playmode", identity)

            with report_path.open("a") as stream:
                stream.write(self.line(verification.PLAYMODE_TESTS[0], "leak", ["added system row 'X'"],
                                       nonce=seen["LUCID_ISOLATION_NONCE"]) + "\n")
            with self.assertRaisesRegex(ValueError, "isolation report is incomplete or differs"):
                verified_receipt(work, "playmode", identity)
            report_path.unlink()
            with self.assertRaisesRegex(ValueError, "Isolation report missing"):
                verified_receipt(work, "playmode", identity)
            receipt_path = work / "reports/playmode-verification.json"
            receipt = json.loads(receipt_path.read_text())
            del receipt["isolation"]
            receipt_path.write_text(json.dumps(receipt))
            with self.assertRaisesRegex(ValueError, "no isolation verdict"):
                verified_receipt(work, "playmode", identity)


if __name__ == "__main__":
    unittest.main()

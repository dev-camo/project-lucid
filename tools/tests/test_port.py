"""Synthetic workflow/filesystem controls; they do not prove a playable port."""
import json
import os
from pathlib import Path
import platform
import plistlib
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import port
from fake_msvcrt import RegionMsvcrt


class SyntheticStages:
    def __init__(self, case):
        self.case = case
        self.calls = []
        self.fail = None
        self.action = None
        self.export = None

    def result(self, name, status, **values):
        self.calls.append(name)
        if self.action:
            self.action(name)
        return {"status": "failed" if self.fail == name else status,
                "errors": ["synthetic full-game gate failure"] if self.fail == name else [], **values}

    def inspect(self, app):
        report = self.result("inspect", "complete", identity={"unity_version": port.verification.UNITY_VERSION},
                             addressables={"complete": self.fail != "inspect", "missing_bundles": []})
        return report

    def bootstrap(self, work, selected):
        self.case.assertEqual(["assetripper", "cpp2il", "dumper"], selected)
        return self.result("bootstrap", "ready")

    def declarations(self, app, work, mode):
        self.case.assertEqual("schemas", mode)
        folder = work / "recovery" / uuid.uuid4().hex
        folder.mkdir(parents=True)
        (folder / "declarations.dll").write_bytes(b"synthetic declarations")
        return self.result("declarations", "ready", output_dir=str(folder))

    def native(self, app, work):
        return self.result("native-schema", "ready")

    def extract(self, app, work):
        folder = work / "assets/runs" / uuid.uuid4().hex / "ExportedProject"
        (folder / "Assets").mkdir(parents=True)
        (folder / "Assets/content.asset").write_bytes(b"supplied synthetic export")
        self.export = folder
        return self.result("asset-export", "exported", project_path=str(folder),
                           input_fingerprint=port.assets.fingerprint_manifest(port.inspection._manifest(app)))

    def export_identity(self, report):
        self.case.assertEqual(report["input_fingerprint"],
                              port.assets.fingerprint_manifest(port.inspection._manifest(self.case.app)))
        return self.case.app

    def validate(self, root, work, stage="release"):
        name = "extraction-validation" if stage == "extraction" else (
            "release-recheck" if "build" in self.calls else "release-validation")
        return self.result(name, "complete", generated_by="ProjectLucid.validate_project", stage=stage,
                           **port.verification.current_identity(root))

    def prepare(self, root, work):
        folder = root / "Assets/Recovered"
        folder.mkdir(parents=True, exist_ok=True)
        (folder / "content.asset").write_bytes(b"prepared synthetic content")
        return self.result("prepare", "prepared")

    def player(self, root, work, target):
        return self.result("player-code", "compiled", target=target)

    def measure(self, root, work, app, target):
        return self.result("source-measurement", "ready", target=target)

    def tests(self, root, work, mode):
        reports = work / "reports"
        reports.mkdir(parents=True, exist_ok=True)
        xml, log = reports / (mode + ".xml"), reports / (mode + ".log")
        xml.write_text("synthetic XML for " + mode)
        log.write_text("synthetic full test invocation " + mode)
        (reports / (mode + "-verification.json")).write_text(json.dumps({"mode": mode}))
        (reports / (mode + "-isolation.jsonl")).write_text('{"attempt": "final"}\n')
        return self.result(mode, "complete", xml=str(xml), log=str(log))

    def audit(self, root, work):
        return self.result("audit", "complete", unresolved_references=0)

    def build(self, root, work, target, *, destination):
        self.case.assertIn("release-validation", self.calls)
        entry = Path(destination)
        if target == "macos":
            (entry / "Contents/MacOS").mkdir(parents=True)
            (entry / "Contents/MacOS/Project Lucid").write_bytes(b"synthetic executable")
            (entry / "Contents/Info.plist").write_bytes(plistlib.dumps({"CFBundleExecutable": "Project Lucid"}))
            data = entry / "Contents/Resources/Data"
        else:
            entry.write_bytes(b"synthetic executable")
            data = entry.parent / "ProjectLucid_Data"
            (entry.parent / ("UnityPlayer.dll" if target == "windows" else "UnityPlayer.so")).write_bytes(b"synthetic engine")
        (data / "Managed").mkdir(parents=True)
        (data / "Managed/Game.Runtime.dll").write_bytes(b"synthetic maintained assembly")
        (data / "globalgamemanagers").write_bytes(b"synthetic scene data")
        (data / "additional asset.bin").write_bytes(b"adjacent physical content")
        return self.result("build", "complete", target=target, exit_code=0, output_dir=str(entry))


class PortTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="port driver spaces ")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve() / "project with spaces"
        (self.root / "tools").mkdir(parents=True)
        (self.root / "Assets").mkdir()
        (self.root / "Assets/maintained.cs").write_bytes(b"maintained source")
        self.app = self.root / "input/Fixture.app"
        self.app.mkdir(parents=True)
        (self.app / "original.bin").write_bytes(b"immutable supplied bytes")
        self.work = self.root / ".cache/project-lucid"
        self.output = self.root / "output with spaces"
        self.lock = self.root / "tools/tool-lock.json"
        self.lock.write_text(json.dumps({"schema_version": 1,
                                        "platform": platform.system().lower() + "-" + platform.machine().lower(),
                                        "rid": "synthetic", "artifacts": {}}))
        self.editor = self.root / "synthetic-editor"
        self.editor.write_bytes(b"synthetic installed editor identity")
        self.stages = SyntheticStages(self)
        self.original_input = port.inspection._manifest(self.app)
        self.original_source = port.verification.artifact_fingerprint(self.root)
        for obj, name, value in [
            (port.bootstrap, "REPO_ROOT", self.root), (port.bootstrap, "CACHE_ROOT", self.work),
            (port.bootstrap, "LOCK_FILE", self.lock), (port.verification, "find_editor", lambda: self.editor),
            (port.playercode, "_engine_identity", lambda path: [{"path": str(path), "sha256": port.inspection.sha256_file(path)}]),
            (port.inspection, "inspect_bundle", self.stages.inspect), (port.bootstrap, "bootstrap", self.stages.bootstrap),
            (port.recovery, "recover_code", self.stages.declarations), (port.native, "native_schema", self.stages.native),
            (port.assets, "extract_assets", self.stages.extract), (port.assets, "_validate_export_identity", self.stages.export_identity),
            (port.assets, "prepare_assets", self.stages.prepare), (port.playercode, "run_player_code", self.stages.player),
            (port.codeprogress, "run_code_progress", self.stages.measure), (port.verification, "run_tests", self.stages.tests),
            (port.verification, "run_audit", self.stages.audit), (port.project, "validate_project", self.stages.validate),
            (port.project, "build_project", self.stages.build),
        ]:
            patcher = patch.object(obj, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def run_port(self, target="macos", output=None):
        return port.run_port(self.root, self.work, self.root / "input", target, self.output if output is None else output)

    def old_selection(self, target="macos"):
        port._output_owner(self.output, self.root, create=True)
        parent = self.output / target
        parent.mkdir()
        (parent / "previous-player").mkdir()
        (parent / "previous-player/keep.bin").write_bytes(b"previous successful player")
        previous = b'{"run":"previous-player","keep":"exact old pointer bytes"}\n'
        (parent / "latest.json").write_bytes(previous)
        return previous

    def assert_unchanged(self):
        self.assertEqual(self.original_input, port.inspection._manifest(self.app))
        self.assertEqual(self.original_source, port.verification.artifact_fingerprint(self.root))

    def test_every_pinned_host_reaches_the_next_preflight_gate(self):
        maintained_lock = Path(port.__file__).resolve().parents[1] / "tool-lock.json"
        self.lock.write_bytes(maintained_lock.read_bytes())
        self.original_source = port.verification.artifact_fingerprint(self.root)
        hosts = [("Darwin", "arm64"), ("Linux", "x86_64"), ("Windows", "AMD64")]
        for system, machine in hosts:
            with self.subTest(system=system, machine=machine), \
                    patch.object(port.bootstrap.platform, "system", return_value=system), \
                    patch.object(port.bootstrap.platform, "machine", return_value=machine), \
                    patch.object(port.verification, "find_editor",
                                 side_effect=RuntimeError("stop at engine identity")) as editor:
                result = self.run_port()
            editor.assert_called_once_with()
            self.assertEqual("failed", result["status"])
            self.assertEqual("preflight", result["failed_stage"])
            self.assertEqual("stop at engine identity", result["error"])
            self.assertEqual([], self.stages.calls)
            self.assertFalse(self.output.exists())
        self.assert_unchanged()

    def test_unsupported_host_is_rejected_before_engine_or_stage_execution(self):
        maintained_lock = Path(port.__file__).resolve().parents[1] / "tool-lock.json"
        self.lock.write_bytes(maintained_lock.read_bytes())
        self.original_source = port.verification.artifact_fingerprint(self.root)
        with patch.object(port.bootstrap.platform, "system", return_value="Linux"), \
                patch.object(port.bootstrap.platform, "machine", return_value="aarch64"), \
                patch.object(port.verification, "find_editor") as editor:
            result = self.run_port()
        editor.assert_not_called()
        self.assertEqual("failed", result["status"])
        self.assertEqual("preflight", result["failed_stage"])
        self.assertIn("detected linux-aarch64", result["error"])
        self.assertIn("darwin-arm64, windows-amd64, linux-x86_64", result["error"])
        self.assertEqual([], self.stages.calls)
        self.assertFalse(self.output.exists())
        self.assert_unchanged()

    def test_every_early_stage_failure_returns_nonzero_without_build_or_output(self):
        for stage in ("inspect", "bootstrap", "declarations", "native-schema", "asset-export", "extraction-validation",
                      "prepare", "player-code", "source-measurement", "editmode", "playmode", "audit", "release-validation"):
            with self.subTest(stage=stage):
                self.stages.calls.clear()
                self.stages.fail = stage
                result = self.run_port()
                self.assertNotEqual(0, result["exit_code"])
                self.assertEqual(stage, result["failed_stage"])
                self.assertNotIn("build", self.stages.calls)
                self.assertFalse(self.output.exists())
                self.assertTrue((Path(result["run_dir"]) / "failure.log").is_file())
        self.assert_unchanged()

    def test_all_targets_publish_full_physical_files_without_touching_input_or_source(self):
        for target in port.OUTPUTS:
            with self.subTest(target=target):
                self.stages.calls.clear()
                result = self.run_port(target)
                self.assertEqual(0, result["exit_code"], result)
                pointer = json.loads((self.output / target / "latest.json").read_text())
                version = self.output / pointer["payload"]
                self.assertEqual(version / port.OUTPUTS[target], Path(result["output_dir"]))
                self.assertEqual(pointer["files"], port._tree(version, internal_links=True))
                names = {r["path"] for r in pointer["files"]}
                self.assertTrue(any(n.endswith("additional asset.bin") for n in names))
                self.assertTrue(any(n.endswith("Managed/Game.Runtime.dll") for n in names))
                self.assertIn("port-receipt.json", names)
                self.assertFalse(any(p.is_symlink() for p in version.rglob("*")))
                self.assertEqual(["inspect", "bootstrap", "declarations", "native-schema", "asset-export",
                                  "extraction-validation", "prepare", "player-code", "source-measurement", "editmode",
                                  "playmode", "audit", "release-validation", "build", "release-recheck"], self.stages.calls)
        self.assert_unchanged()

    def test_real_release_validator_blocks_mocked_complete_subsystem_reports(self):
        # Reconstruct the module without any Unity launch to exercise the real guard.
        import importlib.util
        original_path = Path(port.project.__file__)
        spec = importlib.util.spec_from_file_location("lucidlib._port_guard_probe", original_path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        synthetic = self.stages.validate
        def guarded(root, work, stage="release"):
            return synthetic(root, work, stage) if stage == "extraction" else module.validate_project(root, work, stage)
        with patch.object(port.project, "validate_project", guarded):
            result = self.run_port()
        self.assertEqual("release-validation", result["failed_stage"])
        self.assertNotIn("build", self.stages.calls)
        saved = json.loads(Path(result["stages"][-1]["report"]["path"]).read_text())
        self.assertEqual("failed", saved["status"])
        self.assertTrue(any("reference" in e.lower() or "verification" in e.lower() for e in saved["errors"]))

    def test_failed_full_play_retains_xml_log_and_old_selection(self):
        previous = self.old_selection()
        self.stages.fail = "playmode"
        result = self.run_port()
        self.assertEqual("playmode", result["failed_stage"])
        self.assertNotIn("build", self.stages.calls)
        self.assertEqual(previous, (self.output / "macos/latest.json").read_bytes())
        retained = list((Path(result["run_dir"]) / "evidence/playmode").glob("*"))
        self.assertTrue(any(p.name.endswith("playmode.xml") for p in retained))
        self.assertTrue(any(p.name.endswith("playmode.log") for p in retained))

    def test_compile_status_cannot_satisfy_full_build(self):
        original = self.stages.build
        def compile_only(*args, **kwargs):
            result = original(*args, **kwargs)
            result["status"] = "compiled"
            return result
        with patch.object(port.project, "build_project", compile_only):
            result = self.run_port("windows")
        self.assertEqual("build", result["failed_stage"])
        self.assertNotEqual(0, result["exit_code"])
        self.assertFalse((self.output / "windows/latest.json").exists())

    def test_mutated_input_is_detected_before_any_build(self):
        def mutate(stage):
            if stage == "bootstrap":
                (self.app / "original.bin").write_bytes(b"mutation of input")
        self.stages.action = mutate
        result = self.run_port()
        self.assertEqual("bootstrap", result["failed_stage"])
        self.assertIn("input", result["error"])
        self.assertNotIn("build", self.stages.calls)

    def test_mutated_source_is_detected_before_any_build(self):
        def mutate(stage):
            if stage == "prepare":
                (self.root / "Assets/maintained.cs").write_bytes(b"changed source")
        self.stages.action = mutate
        result = self.run_port()
        self.assertEqual("prepare", result["failed_stage"])
        self.assertIn("source changed", result["error"])
        self.assertNotIn("build", self.stages.calls)

    def test_tool_lock_and_installed_engine_changes_stop_before_build(self):
        for artifact in (self.lock, self.editor):
            with self.subTest(artifact=artifact.name):
                original = artifact.read_bytes()
                self.stages.calls.clear()
                self.stages.action = lambda stage: artifact.write_bytes(b"changed") if stage == "bootstrap" else None
                result = self.run_port()
                self.assertEqual("bootstrap", result["failed_stage"])
                self.assertNotIn("build", self.stages.calls)
                artifact.write_bytes(original)

    def test_changed_export_seal_prevents_publication(self):
        self.stages.action = lambda stage: (self.stages.export / "Assets/content.asset").write_bytes(b"mutated raw") if stage == "prepare" else None
        result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertIn("Recovery/export output changed", result["error"])
        self.assertNotIn("build", self.stages.calls)
        self.assertFalse((self.output / "macos/latest.json").exists())

    def test_missing_transform_tree_cannot_reuse_a_ready_status(self):
        with patch.object(port.recovery, "recover_code", lambda *a, **k: {"status": "ready", "output_dir": str(self.work / "missing")}):
            result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertNotIn("build", self.stages.calls)

    def test_second_success_keeps_prior_version(self):
        first = self.run_port("linux")
        first_entry = Path(first["output_dir"])
        second = self.run_port("linux")
        self.assertEqual(0, first["exit_code"])
        self.assertEqual(0, second["exit_code"])
        self.assertNotEqual(first["run"], second["run"])
        self.assertTrue(first_entry.is_file())
        self.assertTrue((first_entry.parent / "ProjectLucid_Data/additional asset.bin").is_file())

    def test_interrupted_pointer_promotion_keeps_previous_selected_payload(self):
        previous = self.old_selection()
        atomic = port._atomic_json
        def interrupt(path, value):
            if Path(path).name == "latest.json":
                raise KeyboardInterrupt()
            return atomic(path, value)
        with patch.object(port, "_atomic_json", interrupt):
            result = self.run_port()
        self.assertEqual("interrupted", result["status"])
        self.assertEqual(130, result["exit_code"])
        self.assertEqual(previous, (self.output / "macos/latest.json").read_bytes())
        self.assertTrue((self.output / "macos" / result["run"] / "ProjectLucid.app").is_dir())
        self.assertTrue((self.output / "macos/previous-player/keep.bin").is_file())

    def test_pointer_replace_failure_retains_old_bytes_and_removes_temp_pointer(self):
        previous = self.old_selection("windows")
        replace = port.os.replace
        def fail_replace(source, destination):
            if Path(destination).name == "latest.json":
                raise OSError("synthetic promotion failure")
            return replace(source, destination)
        with patch.object(port.os, "replace", fail_replace):
            result = self.run_port("windows")
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual(previous, (self.output / "windows/latest.json").read_bytes())
        self.assertEqual([], list((self.output / "windows").glob(".latest.json.*.tmp")))

    def test_unsafe_output_overlap_is_rejected_before_stages(self):
        for output in (self.root, self.root.parent, self.app, self.app / "nested", self.work / "nested",
                       self.root / "Assets/build", self.root / "tools/build", self.root / "input/new", self.root / ".hidden/output"):
            with self.subTest(output=output):
                result = self.run_port(output=output)
                self.assertNotEqual(0, result["exit_code"])
                self.assertEqual([], self.stages.calls)

    def test_unowned_output_and_parent_traversal_are_rejected(self):
        self.output.mkdir()
        (self.output / "unrelated.txt").write_bytes(b"keep unrelated data")
        result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual(b"keep unrelated data", (self.output / "unrelated.txt").read_bytes())
        result = self.run_port(output=self.root / "safe/../outside")
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual([], self.stages.calls)

    def test_symlinked_output_ancestor_and_pointer_are_rejected(self):
        outside = self.root / "outside"
        outside.mkdir()
        (self.root / "output-link").symlink_to(outside, target_is_directory=True)
        result = self.run_port(output=self.root / "output-link/nested")
        self.assertNotEqual(0, result["exit_code"])
        self.assertFalse((outside / "nested").exists())
        self.old_selection("linux")
        latest = self.output / "linux/latest.json"
        latest.unlink()
        target = outside / "pointer.txt"
        target.write_bytes(b"external pointer data")
        latest.symlink_to(target)
        result = self.run_port("linux")
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual(b"external pointer data", target.read_bytes())

    def test_player_bundle_link_must_remain_inside_physical_version(self):
        original = self.stages.build
        for external in (True, False):
            with self.subTest(external=external):
                self.stages.calls.clear()
                def linked(*args, **kwargs):
                    result = original(*args, **kwargs)
                    stage = Path(kwargs["destination"]).parent
                    (stage / "content-link").symlink_to(self.app / "original.bin" if external else "ProjectLucid_Data/additional asset.bin")
                    return result
                with patch.object(port.project, "build_project", linked):
                    result = self.run_port("windows")
                self.assertEqual(external, result["exit_code"] != 0, result)

    def test_wrong_target_or_destination_cannot_publish_build(self):
        original = self.stages.build
        for field, value in (("target", "windows"), ("output_dir", str(self.app)), ("exit_code", True)):
            with self.subTest(field=field):
                def wrong(*args, **kwargs):
                    result = original(*args, **kwargs)
                    result[field] = value
                    return result
                with patch.object(port.project, "build_project", wrong):
                    result = self.run_port("linux")
                self.assertNotEqual(0, result["exit_code"])
                self.assertFalse((self.output / "linux/latest.json").exists())

    def import_receipt(self, root, work, mode, *, change_prepared=True, missing=False, failed=False):
        from xml.sax.saxutils import quoteattr
        reports = work / "reports"
        reports.mkdir(parents=True, exist_ok=True)
        required = port.verification.EDITMODE_TESTS if mode == "editmode" else port.verification.PLAYMODE_TESTS
        names = required[:-1] if missing else required
        xml = reports / (mode + ".xml")
        xml.write_text('<test-run result="' + ("Failed" if failed else "Passed") + '" failed="' +
                       ("1" if failed else "0") + '">' + ''.join('<test-case fullname=' + quoteattr(n) +
                       ' result="Passed"/>' for n in names) + '</test-run>')
        log = reports / (mode + ".log")
        log.write_text("all required synthetic test cases, with prepared import")
        if change_prepared:
            (root / "Assets/Recovered" / ("import-" + uuid.uuid4().hex + ".meta")).write_bytes(b"new imported metadata")
        receipt = {"schema_version": 1, "generated_by": "ProjectLucid.run_tests", "mode": mode,
                   "status": "incomplete", "exit_code": 0, "unity_version": port.verification.UNITY_VERSION,
                   "xml": str(xml), "log": str(log), "verdict": port.verification.test_verdict(xml, required),
                   "errors": ["Project content changed during testing; rerun with the imported project"],
                   **port.verification.current_identity(root)}
        (reports / (mode + "-verification.json")).write_text(json.dumps(receipt))
        (reports / (mode + "-isolation.jsonl")).write_text('{"attempt": "first"}\n')
        return receipt

    def test_successful_import_drift_reruns_full_suite_and_uses_stable_prepared_identity(self):
        original = self.stages.tests
        count = 0
        def tests(root, work, mode):
            nonlocal count
            if mode == "editmode":
                count += 1
                if count == 1:
                    self.stages.calls.append(mode)
                    return self.import_receipt(root, work, mode)
            return original(root, work, mode)
        with patch.object(port.verification, "run_tests", tests):
            result = self.run_port()
        self.assertEqual(0, result["exit_code"], result)
        self.assertEqual(2, self.stages.calls.count("editmode"))
        statuses = [r["status"] for r in result["stages"] if r["stage"].startswith("editmode")]
        self.assertEqual(["incomplete", "complete"], statuses)
        self.assertEqual(port.verification.current_identity(self.root), result["identity"])
        for name in ("editmode", "editmode-rerun"):
            paths = list((Path(result["run_dir"]) / "evidence" / name).glob("*"))
            self.assertTrue(any(p.name.endswith("editmode.xml") for p in paths))
            self.assertTrue(any(p.name.endswith("editmode-verification.json") for p in paths))
        # The rerun overwrites the shared isolation file, so each attempt keeps its own copy (#457).
        for name, attempt in (("editmode", "first"), ("editmode-rerun", "final")):
            kept = list((Path(result["run_dir"]) / "evidence" / name).glob("*-editmode-isolation.jsonl"))
            self.assertEqual(['{"attempt": "%s"}\n' % attempt], [p.read_text() for p in kept])

    def test_contradictory_import_xml_does_not_trigger_retry_or_build(self):
        from xml.sax.saxutils import quoteattr
        previous = self.old_selection()
        controls = ('<test-case fullname="Other.Test" result="Failed"/>',
                    '<test-suite result="Failed"/>',
                    '<test-case fullname=' + quoteattr(port.verification.EDITMODE_TESTS[0]) + ' result="Skipped"/>')
        for contradiction in controls:
            with self.subTest(contradiction=contradiction):
                self.stages.calls.clear()
                def tests(root, work, mode):
                    self.stages.calls.append(mode)
                    report = self.import_receipt(root, work, mode)
                    xml = Path(report["xml"])
                    xml.write_text(xml.read_text().replace('</test-run>', contradiction + '</test-run>'))
                    # Mimic a stale permissive predicate, with the actual new XML digest.
                    report["verdict"] = {"status": "complete", "result": "Passed",
                                         "passed": len(port.verification.EDITMODE_TESTS), "missing_required_tests": [],
                                         "xml_sha256": port.inspection.sha256_file(xml)}
                    return report
                with patch.object(port.verification, "run_tests", tests):
                    result = self.run_port()
                self.assertEqual("editmode", result["failed_stage"])
                self.assertEqual(1, self.stages.calls.count("editmode"))
                self.assertNotIn("build", self.stages.calls)
                self.assertEqual(previous, (self.output / "macos/latest.json").read_bytes())

    def test_import_retry_is_bounded_to_two_full_invocations(self):
        previous = self.old_selection()
        def tests(root, work, mode):
            self.stages.calls.append(mode)
            return self.import_receipt(root, work, mode)
        with patch.object(port.verification, "run_tests", tests):
            result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual("editmode-rerun", result["failed_stage"])
        self.assertEqual(2, self.stages.calls.count("editmode"))
        self.assertNotIn("playmode", self.stages.calls)
        self.assertNotIn("build", self.stages.calls)
        self.assertEqual(previous, (self.output / "macos/latest.json").read_bytes())

    def test_other_incomplete_test_results_cannot_trigger_import_retry(self):
        for control in ("process-failure", "other-error", "missing-required", "failed-verdict", "no-prepared-change", "source-change"):
            with self.subTest(control=control):
                self.stages.calls.clear()
                source_file = self.root / "Assets/maintained.cs"
                original_source = source_file.read_bytes()
                def tests(root, work, mode):
                    self.stages.calls.append(mode)
                    result = self.import_receipt(root, work, mode, change_prepared=control != "no-prepared-change",
                                                 missing=control == "missing-required", failed=control == "failed-verdict")
                    if control == "process-failure":
                        result["exit_code"] = 1
                    elif control == "other-error":
                        result["errors"].append("Another failure")
                    elif control == "source-change":
                        source_file.write_bytes(b"unexpected imported source")
                    return result
                with patch.object(port.verification, "run_tests", tests):
                    result = self.run_port()
                self.assertNotEqual(0, result["exit_code"])
                self.assertEqual(1, self.stages.calls.count("editmode"))
                self.assertNotIn("build", self.stages.calls)
                source_file.write_bytes(original_source)

    def test_missing_required_full_play_never_retries(self):
        original = self.stages.tests
        def tests(root, work, mode):
            if mode != "playmode":
                return original(root, work, mode)
            self.stages.calls.append(mode)
            return self.import_receipt(root, work, mode, missing=True)
        with patch.object(port.verification, "run_tests", tests):
            result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual(1, self.stages.calls.count("playmode"))
        self.assertNotIn("build", self.stages.calls)

    @unittest.skipUnless(hasattr(os, "mkfifo"), "needs FIFOs")
    def test_project_lock_fifo_fails_promptly(self):
        lock = self.root / ".cache/project-lucid/locks/port.lock"
        lock.parent.mkdir(parents=True, exist_ok=True)
        os.mkfifo(lock)
        script = ("import sys; from pathlib import Path; sys.path.insert(0, sys.argv[1]);"
                  "from lucidlib import port; "
                  "port._project_lock(Path(sys.argv[2])).__enter__()")
        try:
            result = subprocess.run([sys.executable, "-c", script, str(Path(port.__file__).parents[1]),
                                     str(self.root)], capture_output=True, timeout=30,
                                    stdin=subprocess.DEVNULL)
        except subprocess.TimeoutExpired:
            self.fail("port lock blocked on a FIFO")
        self.assertNotEqual(0, result.returncode)
        self.assertIn(b"regular file", result.stderr)

    def test_live_project_lock_prevents_another_attempt(self):
        with port._project_lock(self.root):
            result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertIn("Another port attempt", result["error"])
        self.assertEqual([], self.stages.calls)

    def test_windows_project_lock_uses_msvcrt_and_excludes_another_attempt(self):
        fake = RegionMsvcrt()
        with patch.multiple(port.bootstrap, fcntl=None, msvcrt=fake):
            with port._project_lock(self.root):
                self.assertEqual(1, len(fake.held))
                with self.assertRaisesRegex(ValueError, "Another port attempt"):
                    with port._project_lock(self.root):
                        self.fail("a second owner must not enter the project lock")
                result = self.run_port()
            self.assertEqual({}, fake.held)
        self.assertNotEqual(0, result["exit_code"])
        self.assertIn("Another port attempt", result["error"])
        self.assertEqual([], self.stages.calls)
        self.assertEqual([fake.LK_NBLCK, fake.LK_NBLCK, fake.LK_NBLCK, fake.LK_UNLCK], fake.calls)

    def test_windows_project_lock_is_released_when_the_attempt_fails(self):
        fake = RegionMsvcrt()
        with patch.multiple(port.bootstrap, fcntl=None, msvcrt=fake):
            with self.assertRaisesRegex(RuntimeError, "attempt failed"):
                with port._project_lock(self.root):
                    raise RuntimeError("attempt failed")
            self.assertEqual({}, fake.held)
            with port._project_lock(self.root):
                self.assertEqual(1, len(fake.held))
        self.assertEqual({}, fake.held)

    def test_host_without_a_file_lock_fails_preflight_before_any_stage(self):
        with patch.multiple(port.bootstrap, fcntl=None, msvcrt=None):
            result = self.run_port()
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual("preflight", result["failed_stage"])
        self.assertEqual("This host has no supported file lock", result["error"])
        self.assertEqual([], self.stages.calls)
        self.assertFalse(self.output.exists())

    def test_missing_physical_data_or_game_assembly_prevents_selection(self):
        original = self.stages.build
        def missing(*args, **kwargs):
            result = original(*args, **kwargs)
            (Path(kwargs["destination"]).parent / "ProjectLucid_Data/Managed/Game.Runtime.dll").unlink()
            return result
        with patch.object(port.project, "build_project", missing):
            result = self.run_port("linux")
        self.assertNotEqual(0, result["exit_code"])
        self.assertFalse((self.output / "linux/latest.json").exists())

    def test_changed_prepared_content_after_build_keeps_previous_selection(self):
        previous = self.old_selection("linux")
        self.stages.action = lambda stage: (self.root / "Assets/Recovered/content.asset").write_bytes(b"changed during build") if stage == "build" else None
        result = self.run_port("linux")
        self.assertNotEqual(0, result["exit_code"])
        self.assertEqual(previous, (self.output / "linux/latest.json").read_bytes())


if __name__ == "__main__":
    unittest.main()

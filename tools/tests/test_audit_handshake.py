"""Receipt-publication checks use small real content trees and a fake Editor."""

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap, verification


class AuditHandshakeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="Lucid audit project ")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.work = self.root / ".cache/project-lucid"
        (self.root / "Assets/Recovered").mkdir(parents=True)
        (self.root / "Assets/Source.cs").write_text("original source")
        self.asset = self.root / "Assets/Recovered/scene.unity"
        self.asset.write_bytes(b"first asset")
        self.latest = self.work / "reports/unity-reference-audit.json"
        self.latest.parent.mkdir(parents=True)
        self.previous = b'{"previous": "accepted audit"}\n'
        self.latest.write_bytes(self.previous)
        for target, value in (("REPO_ROOT", self.root), ("CACHE_ROOT", self.work)):
            patch = mock.patch.object(bootstrap, target, value)
            patch.start()
            self.addCleanup(patch.stop)
        patch = mock.patch.object(verification, "find_editor", return_value=self.root / "Fake Editor")
        patch.start()
        self.addCleanup(patch.stop)
        self.commands = []
        self.action = None
        self.editor_exit = 0
        self.omit_output = False

    def fake_editor(self, command, **kwargs):
        self.commands.append(command)
        self.assertEqual(self.latest.read_bytes(), self.previous)
        pending = Path(command[command.index("-lucidAuditOutput") + 1])
        log = Path(command[command.index("-logFile") + 1])
        log.write_text("Fake matching Editor invocation\n")
        context = None
        fast = "-lucidAuditIdentityContext" in command
        if fast:
            context = Path(command[command.index("-lucidAuditIdentityContext") + 1])
            data = context.read_bytes()
            self.assertEqual(command[command.index("-lucidAuditIdentityDigest") + 1], hashlib.sha256(data).hexdigest())
            lines = data.decode("utf-8").splitlines()
            self.assertEqual(lines[0], "ProjectLucid.audit-identity-context-v1")
            values = dict(line.split("=", 1) for line in lines[1:])
            identity = {key: values[key] for key in ("source_fingerprint", "prepared_asset_fingerprint")}
        else:
            identity = {"source_fingerprint": verification.artifact_fingerprint(self.root),
                        "prepared_asset_fingerprint": verification.artifact_fingerprint(self.root, True)}
        report = {"status": "pending-identity-verification" if fast else "complete",
                  "reference_status": "complete", "identity_status": "pending-wrapper" if fast else "complete",
                  "identity_mode": "wrapper-prepost-v1" if fast else "editor-content-sha256-v1",
                  "unity_version": verification.UNITY_VERSION, "scanned_assets": 1,
                  "unresolved_references": 0, "missing_guids": [], "invalid_script_bindings": [], "shader_errors": [],
                  "identity_nonce": values["nonce"] if fast else None,
                  "identity_context_sha256": hashlib.sha256(data).hexdigest() if fast else None, **identity}
        if self.action:
            self.action(report, context, pending)
        if not self.omit_output and not pending.exists():
            pending.write_text(json.dumps(report))
        return subprocess.CompletedProcess(command, self.editor_exit)

    def run_audit(self):
        with mock.patch.object(verification.subprocess, "run", side_effect=self.fake_editor):
            return verification.run_audit(self.root, self.work)

    def assert_preserved(self):
        self.assertEqual(self.latest.read_bytes(), self.previous)
        self.assertFalse((self.work / "locks/reference-audit.lock").exists())

    def test_only_fresh_pending_report_is_promoted_after_two_native_hashes(self):
        with mock.patch.object(verification, "current_identity", wraps=verification.current_identity) as identity:
            result = self.run_audit()
        self.assertEqual(identity.call_count, 2)
        self.assertEqual(result["status"], "complete")
        receipt = json.loads(self.latest.read_text())
        self.assertEqual(receipt["generated_by"], "ProjectLucid.run_audit")
        self.assertEqual(receipt["identity_verified_by"], "native-python-prepost-v1")
        self.assertEqual(receipt["identity_status"], "complete")
        staged = Path(receipt["staged_report_path"])
        self.assertEqual(json.loads(staged.read_text())["status"], "pending-identity-verification")
        self.assertEqual(receipt["staged_report_sha256"], hashlib.sha256(staged.read_bytes()).hexdigest())
        self.assertEqual(self.asset.read_bytes(), b"first asset")
        self.assertIn(str(self.root), self.commands[0])

    def test_matching_identity_does_not_hide_unresolved_references(self):
        def diagnostics(report, *_):
            report.update(reference_status="incomplete", unresolved_references=2,
                          missing_guids=[{"guid": "a" * 32, "examples": ["Assets/Recovered/scene.unity"]}],
                          shader_errors=["Assets/Shaders/Test.shader: failed import"])
        self.action = diagnostics
        self.assertEqual(self.run_audit()["status"], "incomplete")
        self.assertEqual(json.loads(self.latest.read_text())["unresolved_references"], 2)

    def test_source_drift_and_same_size_asset_drift_with_restored_mtime_reject_publication(self):
        for source in (False, True):
            with self.subTest(source=source):
                target = self.root / "Assets/Source.cs" if source else self.asset
                original = target.read_bytes()
                stat = target.stat()
                def mutate(*_):
                    target.write_bytes(b"x" * len(original))
                    os.utime(target, ns=(stat.st_atime_ns, stat.st_mtime_ns))
                self.action = mutate
                with self.assertRaisesRegex(ValueError, "content changed"):
                    self.run_audit()
                self.assert_preserved()
                target.write_bytes(original)

    def test_wrong_nonce_digest_version_and_supplied_hash_never_promote(self):
        for key, value in (("identity_nonce", "0" * 32), ("identity_context_sha256", "0" * 64),
                           ("unity_version", "2022.3.1f1"), ("prepared_asset_fingerprint", "0" * 64),
                           ("source_fingerprint", "0" * 64), ("identity_mode", "unknown"),
                           ("identity_status", "complete"), ("status", "complete")):
            with self.subTest(key=key):
                self.action = lambda report, *_, key=key, value=value: report.update({key: value})
                with self.assertRaises(ValueError):
                    self.run_audit()
                self.assert_preserved()

    def test_source_mutation_during_native_identity_pass_is_detected(self):
        original_identity = verification.current_identity
        target = self.root / "Assets/Source.cs"
        original_source = target.read_bytes()
        for mutate_at in (1, 2):
            with self.subTest(identity_pass=mutate_at):
                calls = 0
                def identity_then_mutate(root):
                    nonlocal calls
                    identity = original_identity(root)
                    calls += 1
                    if calls == mutate_at:
                        target.write_bytes(b"changed during prepared hash")
                    return identity
                with mock.patch.object(verification, "current_identity", side_effect=identity_then_mutate):
                    with self.assertRaisesRegex(ValueError, "Maintained source changed"):
                        self.run_audit()
                self.assert_preserved()
                target.write_bytes(original_source)

    def test_source_mutation_after_report_validation_cannot_be_published(self):
        original_check = verification._audit_check_report
        def check_then_mutate(*args, **kwargs):
            original_check(*args, **kwargs)
            (self.root / "Assets/Source.cs").write_text("changed before publication")
        with mock.patch.object(verification, "_audit_check_report", side_effect=check_then_mutate):
            with self.assertRaisesRegex(ValueError, "before reference audit publication"):
                self.run_audit()
        self.assert_preserved()

    def test_evidence_mutation_after_report_validation_cannot_be_published(self):
        original_check = verification._audit_check_report
        for flag in ("-lucidAuditOutput", "-lucidAuditIdentityContext"):
            with self.subTest(flag=flag):
                def check_then_mutate(*args, **kwargs):
                    original_check(*args, **kwargs)
                    command = self.commands[-1]
                    path = Path(command[command.index(flag) + 1])
                    path.write_bytes(path.read_bytes() + b"changed")
                with mock.patch.object(verification, "_audit_check_report", side_effect=check_then_mutate):
                    with self.assertRaisesRegex(ValueError, "evidence changed before publication"):
                        self.run_audit()
                self.assert_preserved()

    def test_malformed_diagnostic_types_and_inconsistent_counts_reject(self):
        for change in ({"scanned_assets": True}, {"unresolved_references": "0"},
                       {"missing_guids": [{"guid": 1, "examples": ["asset"]}]},
                       {"invalid_script_bindings": [None]}, {"shader_errors": {}},
                       {"unresolved_references": 1}, {"reference_status": "incomplete"}):
            with self.subTest(change=change):
                self.action = lambda report, *_, change=change: report.update(change)
                with self.assertRaises(ValueError):
                    self.run_audit()
                self.assert_preserved()

    def test_duplicate_json_fields_and_nonfinite_constants_reject(self):
        for tail in (',"identity_nonce":"duplicate"}', ',"unused":NaN}'):
            with self.subTest(tail=tail):
                self.action = lambda report, _, pending, tail=tail: pending.write_text(json.dumps(report)[:-1] + tail)
                with self.assertRaises(ValueError):
                    self.run_audit()
                self.assert_preserved()

    def test_changed_context_rejects_even_when_report_echoes_original_digest(self):
        self.action = lambda _, context, __: context.write_bytes(context.read_bytes() + b"changed")
        with self.assertRaisesRegex(ValueError, "context changed"):
            self.run_audit()
        self.assert_preserved()

    def test_pending_output_symlink_does_not_follow_or_overwrite_external_file(self):
        external = self.root / "outside.json"
        external.write_bytes(b"external evidence")
        self.action = lambda _, __, pending: pending.symlink_to(external)
        with self.assertRaisesRegex(ValueError, "symbolic link"):
            self.run_audit()
        self.assertEqual(external.read_bytes(), b"external evidence")
        self.assert_preserved()

    def test_editor_failure_or_absent_fresh_output_preserves_latest(self):
        for exit_code, omit in ((7, False), (0, True)):
            with self.subTest(exit_code=exit_code):
                self.editor_exit, self.omit_output = exit_code, omit
                self.assertEqual(self.run_audit()["status"], "failed")
                self.assert_preserved()

    def test_interruption_before_publication_preserves_previous_and_releases_lock(self):
        with mock.patch.object(verification, "write_json", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                self.run_audit()
        self.assert_preserved()

    def test_another_audit_lock_is_not_removed_or_bypassed(self):
        lock = self.work / "locks/reference-audit.lock"
        lock.mkdir(parents=True)
        with self.assertRaisesRegex(ValueError, "Another reference audit"):
            self.run_audit()
        self.assertTrue(lock.exists())
        self.assertEqual(self.latest.read_bytes(), self.previous)
        self.assertEqual(self.commands, [])

    def test_non_bmp_and_literal_backslash_paths_use_direct_hash_fallback(self):
        # The existing Python and C# ordering algorithms diverge here. Do not
        # silently certify a Python-supplied hash as a managed equivalent.
        values = ["\ue000.asset", "\U00010000.asset"]
        self.assertNotEqual(sorted(values), sorted(values, key=lambda value: value.encode("utf-16-be")))
        for name in ("extra\U00010000.asset", "literal\\name.asset"):
            with self.subTest(name=name):
                path = self.asset.parent / name
                path.write_bytes(b"extra")
                self.assertEqual(self.run_audit()["status"], "complete")
                self.assertNotIn("-lucidAuditIdentityContext", self.commands[-1])
                receipt = json.loads(self.latest.read_text())
                self.assertEqual(receipt["identity_mode"], "editor-content-sha256-v1")
                self.latest.write_bytes(self.previous)
                path.unlink()

    def test_direct_hash_mismatch_is_rejected_for_unsupported_path_ordering(self):
        (self.asset.parent / "extra\U00010000.asset").write_bytes(b"extra")
        self.action = lambda report, *_: report.update(prepared_asset_fingerprint="0" * 64)
        with self.assertRaisesRegex(ValueError, "does not match"):
            self.run_audit()
        self.assert_preserved()

    def test_non_object_json_is_rejected_without_publication(self):
        self.action = lambda _, __, pending: pending.write_text("[]")
        with self.assertRaises(ValueError):
            self.run_audit()
        self.assert_preserved()


if __name__ == "__main__":
    unittest.main()

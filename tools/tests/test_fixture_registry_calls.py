"""Flag direct shared-registry mutation in maintained test fixtures.

Fixtures that register into or remove ProcessManager rows, add
SystemConfiguration entries, create the default FSMManager or auto-register a
row through GetSystemSafe must do so through the ownership helpers, so that a
borrowed row, observer or subscription is never replaced or run. Each file
with such a direct call today is listed below with its exact call count and
the reason it is allowed; a new direct call fails until it is moved onto a
helper or justified here. The SharedStateIsolation test-assembly guard checks
the same state at run time.
"""

import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCANNED = ("Assets/Tests", "Assets/Editor/RecoveredProof")

# The lease helpers themselves are the one place these calls belong.
HELPERS = {
    "Assets/Editor/RecoveredProof/VerificationFixtureOwnership.cs",
    "Assets/Tests/PlayMode/LifecycleSystemRowLease.cs",
    "Assets/Tests/HapticsShared/HLHapticsOwnedProviderLease.cs",
}

PROOF = "ProcessManager semantics proof: it clears or copies the registry and puts back the same rows"
OWNED = "registers only into rows it first admits as absent or idle through the ownership helpers"
UNLEASED = "not yet on a lease helper; the SharedStateIsolation guard reports any state it leaves"

# path: (direct call count, reason)
ALLOWED = {
    "Assets/Editor/RecoveredProof/ActorAnimationVerification.cs": (1, UNLEASED),
    "Assets/Editor/RecoveredProof/AddressableManagerVerification.cs": (2, UNLEASED),
    "Assets/Editor/RecoveredProof/FSMConstructionVerification.cs": (1, OWNED),
    "Assets/Editor/RecoveredProof/FSMFileLoadingVerification.cs": (5, OWNED),
    "Assets/Editor/RecoveredProof/FSMManagerVerification.cs": (11, PROOF),
    "Assets/Editor/RecoveredProof/FSMScriptableLoadingVerification.cs": (3, OWNED),
    "Assets/Editor/RecoveredProof/LevelManagerSystemsVerification.cs": (14, PROOF),
    "Assets/Editor/RecoveredProof/LoggingHostVerification.cs": (9, OWNED),
    "Assets/Editor/RecoveredProof/OriginalTimeScaledUtilitiesVerification.cs": (17, PROOF),
    "Assets/Editor/RecoveredProof/ProcessActionVerification.cs": (11, PROOF),
    "Assets/Editor/RecoveredProof/ProcessManagerCheckedGetVerification.cs": (9, PROOF),
    "Assets/Editor/RecoveredProof/ProcessUnregistrationVerification.cs": (26, PROOF),
    "Assets/Editor/RecoveredProof/SystemConfigurationVerification.cs": (3, OWNED),
    "Assets/Editor/RecoveredProof/SystemRefVerification.cs": (1, OWNED),
    "Assets/Editor/RecoveredProof/TimeSchedulerVerification.cs": (4, UNLEASED),
    "Assets/Editor/RecoveredProof/UIOriginalLeafVerification.cs": (1, OWNED),
    "Assets/Tests/CoroutineScrollShared/OriginalCoroutineEngineVerification.cs": (1, PROOF),
    "Assets/Tests/Editor/CloudFacadeHandlerTests.cs": (1, OWNED),
    "Assets/Tests/Editor/FSMFixtureManagerRowOwnershipTests.cs": (4, OWNED),
    "Assets/Tests/Editor/FSMManagerVerificationOwnershipTests.cs": (52, OWNED),
    "Assets/Tests/Editor/OfflineAchievementLifecycleTests.cs": (10, OWNED),
    "Assets/Tests/Editor/OriginalDispatchUtilitiesTests.cs": (2, UNLEASED),
    "Assets/Tests/Editor/OriginalHLOutputDiagnosticGapTests.cs": (2, OWNED),
    "Assets/Tests/Editor/OriginalLegalModuleTests.cs": (1, OWNED),
    "Assets/Tests/Editor/OriginalLevelManagerSystemsGapTests.cs": (5, OWNED),
    "Assets/Tests/Editor/OriginalPerformanceDependencyGapTests.cs": (2, UNLEASED),
    "Assets/Tests/Editor/OriginalPerformanceDependencyPreservationTests.cs": (2, UNLEASED),
    "Assets/Tests/Editor/OriginalStringTimeoutFSMTests.cs": (1, OWNED),
    "Assets/Tests/Editor/OriginalUIClickableTests.cs": (1, UNLEASED),
    "Assets/Tests/Editor/OriginalUIWidgetRetainLastSelectedBehaviourTests.cs": (3, UNLEASED),
    "Assets/Tests/Editor/SharedStateIsolationGuardTests.cs": (3, OWNED),
    "Assets/Tests/Editor/SystemConfigurationVerificationOwnershipTests.cs": (1, OWNED),
    "Assets/Tests/Editor/VerificationFixtureOwnershipTests.cs": (18, OWNED),
    "Assets/Tests/PlayMode/CloudFacadeTests.cs": (2, PROOF),
    "Assets/Tests/PlayMode/FSMComponentTests.cs": (3, OWNED),
    "Assets/Tests/PlayMode/LevelManagerSystemsLifecycleTests.cs": (2, PROOF),
    "Assets/Tests/PlayMode/LoggingHostTests.cs": (5, OWNED),
    "Assets/Tests/PlayMode/OriginalHLPhysicsLifecycleTests.cs": (1, OWNED),
    "Assets/Tests/PlayMode/OriginalHLSimpleScrollPlayTests.cs": (4, OWNED),
    "Assets/Tests/PlayMode/OriginalHLUIEffectManagerGapTests.cs": (1, UNLEASED),
    "Assets/Tests/PlayMode/OriginalLevelManagerSystemsGapPlayTests.cs": (1, PROOF),
    "Assets/Tests/PlayMode/OriginalLowGapPlayTests.cs": (2, OWNED),
    "Assets/Tests/PlayMode/OriginalStackedCameraGapTests.cs": (2, UNLEASED),
    "Assets/Tests/PlayMode/OriginalUIVisibilityGroupMemberGapPlayTests.cs": (2, UNLEASED),
    "Assets/Tests/PlayMode/OriginalUIWidgetRetainLastSelectedPlayTests.cs": (8, OWNED),
    "Assets/Tests/PlayMode/ToggleCursorVisibilityInputTypeTests.cs": (4, OWNED),
}

CALL = re.compile(r"\b(?:RegisterSystem|UnregisterSystem|AddConfig)\s*(?:<[^<>();]*>)?\s*\(|"
                  r"\bFSMManager\s*\.\s*GetManager\s*\(")
SAFE_GET = re.compile(r"\bGetSystemSafe\s*(?:<[^<>();]*>)?\s*\(")
TOKENS = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|\$?"(?:[^"\\\n]|\\.)*"|\'(?:[^\'\\\n]|\\.)+\'', re.S)


def code_only(source):
    """Blank comments and string or character literals, keeping line breaks."""
    return TOKENS.sub(lambda match: re.sub(r"[^\n]", " ", match.group(0)), source)


def arguments(code, start):
    """The text between the call's parentheses, from just after '('."""
    depth = 1
    for index in range(start, len(code)):
        if code[index] == "(":
            depth += 1
        elif code[index] == ")":
            depth -= 1
            if depth == 0:
                return code[start:index]
    return code[start:]


def direct_calls(source):
    """Lines of direct registry calls. GetSystemSafe counts unless autoRegister is false."""
    code = code_only(source)
    lines = [code.count("\n", 0, match.start()) + 1 for match in CALL.finditer(code)]
    for match in SAFE_GET.finditer(code):
        args = arguments(code, match.end())
        if re.search(r"(?:autoRegister\s*:\s*|,\s*)false\s*$", args.strip()):
            continue
        lines.append(code.count("\n", 0, match.start()) + 1)
    return sorted(lines)


def scan(root=ROOT):
    found = {}
    for top in SCANNED:
        for path in sorted((root / top).rglob("*.cs")):
            relative = path.relative_to(root).as_posix()
            if relative in HELPERS:
                continue
            lines = direct_calls(path.read_text(encoding="utf-8-sig"))
            if lines:
                found[relative] = lines
    return found


class FixtureRegistryCallTests(unittest.TestCase):
    def test_direct_registry_calls_are_on_helpers_or_listed(self):
        found = scan()
        unlisted = {path: lines for path, lines in found.items() if path not in ALLOWED}
        self.assertEqual({}, unlisted, "Direct registry calls outside the lease helpers; "
                         "use VerificationFixtureOwnership or LifecycleSystemRowLease")
        changed = {path: (len(found.get(path, ())), allowed[0])
                   for path, allowed in ALLOWED.items() if len(found.get(path, ())) != allowed[0]}
        self.assertEqual({}, changed, "Allowed direct call counts changed (found, listed); "
                         "move new calls onto a helper or update the listed reason and count")

    def test_every_listed_file_states_a_reason(self):
        for path, (count, reason) in ALLOWED.items():
            with self.subTest(path=path):
                self.assertGreater(count, 0)
                self.assertIn(reason, (PROOF, OWNED, UNLEASED))
                self.assertTrue((ROOT / path).is_file())

    def test_scanner_ignores_comments_and_strings_and_reads_auto_register(self):
        source = (
            '// ProcessManager.RegisterSystem(x);\n'
            'var name = "RegisterSystem(";\n'
            '/* FSMManager.GetManager(); */\n'
            'ProcessManager.GetSystemSafe<App>(name, false);\n'
            'ProcessManager.GetSystemSafe<App>(autoRegister: false);\n'
            'ProcessManager.GetSystemSafe<App>();\n'
            'ProcessManager.RegisterSystem(host, name);\n'
            'SystemConfiguration.AddConfig<SystemConfigurationAsset>(asset);\n'
            'FSMManager . GetManager();\n'
            'ProcessManager.UnregisterSystem<App>();\n'
        )
        self.assertEqual([6, 7, 8, 9, 10], direct_calls(source))

    def test_new_direct_call_in_an_unlisted_file_is_reported(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "Assets/Tests/Editor/NewFixture.cs"
            target.parent.mkdir(parents=True)
            target.write_text("class T { void F() { ProcessManager.RegisterSystem(new S()); } }\n", encoding="utf-8")
            helper = root / "Assets/Editor/RecoveredProof/VerificationFixtureOwnership.cs"
            helper.parent.mkdir(parents=True)
            helper.write_text("class H { void F() { ProcessManager.UnregisterSystem(name); } }\n", encoding="utf-8")
            self.assertEqual({"Assets/Tests/Editor/NewFixture.cs": [1]}, scan(root))


GUARD = "Assets/Tests/IsolationShared/SharedStateIsolationAttribute.cs"
GUARD_ASSEMBLY = "ProjectLucid.SharedStateIsolation"
GUARDED = {
    "Assets/Tests/Editor/ProjectLucid.EditMode.asmdef": "Assets/Tests/Editor/SharedStateIsolationGuard.cs",
    "Assets/Tests/PlayMode/ProjectLucid.PlayMode.asmdef": "Assets/Tests/PlayMode/SharedStateIsolationGuard.cs",
}
ALLOWLIST_ENTRY = re.compile(r'\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\}')


def guard_allowlist(source):
    """(fullname, reason) pairs of the guard's TemporaryAllowlist initializer."""
    body = re.search(r"TemporaryAllowlist\s*=\s*new\s+Dictionary<string,\s*string>\s*\{(.*?)\n\s*\};", source, re.S)
    if body is None:
        raise AssertionError("TemporaryAllowlist initializer not found")
    return ALLOWLIST_ENTRY.findall(code_only_keep_strings(body.group(1)))


def code_only_keep_strings(source):
    """Blank comments only, so allowlist literals stay readable."""
    return re.sub(r"//[^\n]*|/\*.*?\*/", lambda match: re.sub(r"[^\n]", " ", match.group(0)), source, flags=re.S)


class SharedStateGuardTests(unittest.TestCase):
    def test_both_test_assemblies_apply_the_guard(self):
        import json
        for asmdef, attribute in GUARDED.items():
            with self.subTest(asmdef=asmdef):
                references = json.loads((ROOT / asmdef).read_text(encoding="utf-8-sig"))["references"]
                self.assertIn(GUARD_ASSEMBLY, references)
                code = code_only_keep_strings((ROOT / attribute).read_text(encoding="utf-8-sig"))
                self.assertEqual(1, len(re.findall(r"\[\s*assembly\s*:\s*ProjectLucid\.Tests\.SharedStateIsolation\s*\]", code)))

    def test_temporary_allowlist_names_registered_tests_with_reasons(self):
        import sys
        sys.path.insert(0, str(ROOT / "tools"))
        from lucidlib import verification
        registered = set(verification.EDITMODE_TESTS) | set(verification.PLAYMODE_TESTS)
        entries = guard_allowlist((ROOT / GUARD).read_text(encoding="utf-8-sig"))
        self.assertEqual(len(entries), len({name for name, _ in entries}), "duplicate allowlist entries")
        for name, reason in entries:
            with self.subTest(name=name):
                self.assertIn(name, registered)
                self.assertTrue(reason.strip())

    def test_allowlist_parser_reads_entries_and_ignores_comments(self):
        source = ('TemporaryAllowlist = new Dictionary<string, string>\n        {\n'
                  '            // { "Commented.Out", "no" },\n'
                  '            { "A.B.C(1)", "leaves row X until #1" },\n'
                  '        };')
        self.assertEqual([("A.B.C(1)", "leaves row X until #1")], guard_allowlist(source))


if __name__ == "__main__":
    unittest.main()

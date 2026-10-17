"""Flag direct shared-registry mutation in maintained test fixtures.

Fixtures that register into or remove ProcessManager rows, add
SystemConfiguration entries, create the default FSMManager or auto-register a
row through GetSystemSafe must do so through the ownership helpers, so that a
borrowed row, observer or subscription is never replaced or run. Each file
with such a direct call today is listed below with its exact call count and
the reason it is allowed; a new direct call fails until it is moved onto a
helper or justified here. No run-time check covers this state around every
test yet (see SharedStateSnapshot), so an UNLEASED file is a known gap.
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
UNLEASED = "not yet on a lease helper; a known isolation gap with no run-time check"

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
    "Assets/Tests/Editor/SharedStateSnapshotTests.cs": (4, OWNED),
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


def code_only_keep_strings(source):
    """Blank comments only, so string literals stay readable."""
    return re.sub(r"//[^\n]*|/\*.*?\*/", lambda match: re.sub(r"[^\n]", " ", match.group(0)), source, flags=re.S)


CHECKED_GET = "Assets/Editor/RecoveredProof/ProcessManagerCheckedGetVerification.cs"
CHECKED_GET_CONTROL = "Assets/Tests/Editor/VerificationFixtureOwnershipTests.cs"
CHECKED_GET_CASE = "CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem"


def checked_get_rows(source):
    """(PrefixRows as listed, prefix + "<literal>" suffixes CheckEngine uses, every prefix + use)."""
    code = code_only_keep_strings(source)
    listed = re.search(r"PrefixRows\s*=\s*Array\.AsReadOnly\(\s*new\s*\[\s*\]\s*\{([^}]*)\}", code)
    if listed is None:
        raise AssertionError("PrefixRows initializer not found")
    engine = code[code.index("void CheckEngine()"):]
    # A diagnostic such as "System '" + prefix + "throw' is null" names its row
    # by the suffix's leading identifier.
    used = [re.match(r"\w*", suffix).group(0) for suffix in re.findall(r'\bprefix\s*\+\s*"([^"]*)"', engine)]
    # The admission loop itself is the one non-literal use.
    uses = re.findall(r"\bprefix\s*\+", re.sub(r"foreach\s*\(\s*string\s+row\s+in\s+PrefixRows\s*\)[^;]*\bprefix\s*\+\s*row\b", "", engine))
    return re.findall(r'"([^"]*)"', listed.group(1)), used, uses


class CheckedGetAdmissionTests(unittest.TestCase):
    # RestoreRows in CheckEngine is shallow, so a row missing from PrefixRows
    # is never admitted and a borrowed one would be overwritten (#250).
    def test_prefix_rows_match_the_rows_check_engine_uses(self):
        listed, used, uses = checked_get_rows((ROOT / CHECKED_GET).read_text(encoding="utf-8-sig"))
        self.assertEqual(len(listed), len(set(listed)), "duplicate PrefixRows entries")
        self.assertEqual(len(uses), len(used), "every prefix + name in CheckEngine is a literal")
        self.assertEqual(set(used), set(listed))

    def test_borrowed_row_control_has_a_case_for_the_host_and_each_prefix_row(self):
        listed, _, _ = checked_get_rows((ROOT / CHECKED_GET).read_text(encoding="utf-8-sig"))
        source = (ROOT / CHECKED_GET_CONTROL).read_text(encoding="utf-8-sig")
        header = re.search(r"((?:\s*\[TestCase\(\d+\)\])+)\s*public void " + CHECKED_GET_CASE + r"\(", source)
        self.assertIsNotNone(header)
        cases = [int(value) for value in re.findall(r"\[TestCase\((\d+)\)\]", header.group(1))]
        self.assertEqual(list(range(len(listed) + 1)), cases)
        import sys
        sys.path.insert(0, str(ROOT / "tools"))
        from lucidlib import verification
        for case in cases:
            self.assertIn("ProjectLucid.Tests.VerificationFixtureOwnershipTests.%s(%d)" % (CHECKED_GET_CASE, case),
                          verification.EDITMODE_TESTS)

    def test_row_reader_reports_a_row_missing_from_the_list(self):
        source = ('internal static readonly IReadOnlyList<string> PrefixRows = Array.AsReadOnly(new[] { "a" });\n'
                  'private static void CheckEngine() { foreach (string row in PrefixRows) Admit(prefix + row);\n'
                  'Get(prefix + "a"); Log("System \'" + prefix + "b\' is null"); Get(prefix + name); }\n')
        listed, used, uses = checked_get_rows(source)
        self.assertEqual(["a"], listed)
        self.assertEqual(["a", "b"], used)
        self.assertEqual(3, len(uses))


if __name__ == "__main__":
    unittest.main()

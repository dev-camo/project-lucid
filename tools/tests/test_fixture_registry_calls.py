"""Flag direct shared-registry mutation in maintained test fixtures.

Fixtures that register into or remove ProcessManager rows, add
SystemConfiguration entries, create the default FSMManager or auto-register a
row through GetSystemSafe must do so through the ownership helpers, so that a
borrowed row, observer or subscription is never replaced or run. Each file
with such a direct call today is listed below with its exact call count and
the reason it is allowed; a new direct call fails until it is moved onto a
helper or justified here. SharedStateIsolationRecorder compares this state
around every test and the verdict fails on a leak it reports, so an UNLEASED
file has no lease but is checked at run time. The recorder does not check the
FSMStateChangeAction pool (see SharedStateSnapshot).
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
UNLEASED = "not yet on a lease helper; only the per-test isolation report checks it"

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
    "Assets/Editor/RecoveredProof/TimeSchedulerVerification.cs": (4, OWNED),
    "Assets/Editor/RecoveredProof/UIOriginalLeafVerification.cs": (1, OWNED),
    "Assets/Tests/CoroutineScrollShared/OriginalCoroutineEngineVerification.cs": (1, PROOF),
    "Assets/Tests/Editor/CloudFacadeHandlerTests.cs": (1, OWNED),
    "Assets/Tests/Editor/FSMFixtureManagerRowOwnershipTests.cs": (4, OWNED),
    "Assets/Tests/Editor/FSMManagerVerificationOwnershipTests.cs": (52, OWNED),
    "Assets/Tests/Editor/OfflineAchievementLifecycleTests.cs": (10, OWNED),
    "Assets/Tests/Editor/OriginalAnimatorHelperTests.cs": (2, OWNED),
    "Assets/Tests/Editor/OriginalDispatchUtilitiesTests.cs": (2, UNLEASED),
    "Assets/Tests/Editor/OriginalGameInputFixtures.cs": (3, OWNED),
    "Assets/Tests/Editor/OriginalHLOutputDiagnosticGapTests.cs": (2, OWNED),
    "Assets/Tests/Editor/OriginalLegalModuleTests.cs": (1, OWNED),
    "Assets/Tests/Editor/OriginalLevelManagerSystemsGapTests.cs": (5, OWNED),
    "Assets/Tests/Editor/OriginalPerformanceDependencyGapTests.cs": (2, UNLEASED),
    "Assets/Tests/Editor/OriginalPerformanceDependencyPreservationTests.cs": (2, UNLEASED),
    "Assets/Tests/Editor/OriginalPersistentLevelObjectTests.cs": (2, OWNED),
    "Assets/Tests/Editor/OriginalProceduralMeshPlacementTests.cs": (5, OWNED),
    "Assets/Tests/Editor/OriginalStringTimeoutFSMTests.cs": (1, OWNED),
    "Assets/Tests/Editor/OriginalUIClickableTests.cs": (1, UNLEASED),
    "Assets/Tests/Editor/OriginalUIWidgetRetainLastSelectedBehaviourTests.cs": (3, UNLEASED),
    "Assets/Tests/Editor/OwnedCharacterManagers.cs": (2, OWNED),
    "Assets/Tests/Editor/OwnedCoreErrorLog.cs": (2, OWNED),
    "Assets/Tests/Editor/SharedStateSnapshotTests.cs": (4, OWNED),
    "Assets/Tests/Editor/SystemConfigurationVerificationOwnershipTests.cs": (1, OWNED),
    "Assets/Tests/Editor/VerificationFixtureOwnershipTests.cs": (19, OWNED),
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
STRING_START = re.compile(r'\$@"|@\$"|\$"|@"|"')


def code_only(source):
    """Blank comments and string or character literals, keeping line breaks.

    The expression holes of interpolated strings ($"...", $@"..." and @$"...")
    are code and stay; only their literal text, format specifiers and {{ }}
    escapes are blanked.
    """
    out = list(source)
    _scan_code(source, 0, out, in_hole=False)
    return "".join(out)


def _blank(out, start, end):
    for index in range(start, min(end, len(out))):
        if out[index] != "\n":
            out[index] = " "


def _scan_code(source, index, out, in_hole):
    """Blank the literals in code from index. In a hole, return the index of its closing '}'."""
    depth = 0
    while index < len(source):
        char = source[index]
        if source.startswith("//", index):
            end = source.find("\n", index)
            end = len(source) if end < 0 else end
        elif source.startswith("/*", index):
            end = source.find("*/", index + 2)
            end = len(source) if end < 0 else end + 2
        elif char == "'":
            end = index + 1
            while end < len(source) and source[end] not in "'\n":
                end += 2 if source[end] == "\\" else 1
            end += 1
        elif (start := STRING_START.match(source, index)) is not None:
            index = _scan_string(source, index, start.group(0), out)
            continue
        else:
            if in_hole:
                if char in "([{":
                    depth += 1
                elif char in ")]}":
                    if depth == 0 and char == "}":
                        return index
                    depth -= 1
                elif char == ":" and depth == 0 and not source.startswith("::", index) and source[index - 1] != ":":
                    # A format specifier is literal text up to the hole's closing brace.
                    end = source.find("}", index)
                    end = len(source) if end < 0 else end
                    _blank(out, index, end)
                    return end
            index += 1
            continue
        _blank(out, index, end)
        index = end
    return len(source)


def _scan_string(source, index, prefix, out):
    """Blank one string literal from its prefix; return the index just after it."""
    verbatim = "@" in prefix
    interpolated = "$" in prefix
    _blank(out, index, index + len(prefix))
    index += len(prefix)
    while index < len(source):
        char = source[index]
        if char == '"':
            if verbatim and source.startswith('""', index):
                _blank(out, index, index + 2)
                index += 2
                continue
            _blank(out, index, index + 1)
            return index + 1
        if char == "\\" and not verbatim:
            _blank(out, index, index + 2)
            index += 2
            continue
        if char == "\n" and not verbatim:
            return index
        if interpolated and source.startswith(("{{", "}}"), index):
            _blank(out, index, index + 2)
            index += 2
            continue
        if interpolated and char == "{":
            _blank(out, index, index + 1)
            close = _scan_code(source, index + 1, out, in_hole=True)
            _blank(out, close, close + 1)
            index = close + 1
            continue
        _blank(out, index, index + 1)
        index += 1
    return index


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

    def test_scanner_reads_interpolation_holes_and_blanks_their_literal_text(self):
        source = (
            'var a = $"{ProcessManager.RegisterSystem(host)}";\n'
            'var b = $"RegisterSystem( {name} FSMManager.GetManager()";\n'
            'var c = $"{{ProcessManager.UnregisterSystem(name)}}";\n'
            'var d = $@"line\n{ProcessManager.UnregisterSystem(name)} ""q""";\n'
            'var e = @$"{new[] { 1 }.Length} {FSMManager.GetManager()}";\n'
            'var f = $"{Get("}")} {Get(\'}\')} {$"{SystemConfiguration.AddConfig(x)}"}";\n'
            'var g = $"{count:D2} RegisterSystem( {{ {ProcessManager.GetSystemSafe<App>()} }}";\n'
            'var h = $"{{{{RegisterSystem(x)}}}} {{{name}}}";\n'
            'var i = $"{global::ProcessManager.RegisterSystem(host)}";\n'
        )
        self.assertEqual([1, 5, 6, 7, 8, 10], direct_calls(source))

    def test_a_parenthesized_conditional_in_a_hole_is_scanned_as_code(self):
        # C# rejects an unparenthesized conditional in a hole (CS8361), so the ':' of a
        # real ternary always sits inside parentheses; its arms must still be scanned.
        source = 'var a = $"{(flag ? value : ProcessManager.RegisterSystem(host))}";\n'
        self.assertEqual([1], direct_calls(source))

    def test_interpolated_literal_text_is_blanked_exactly(self):
        source = 'x = $"a{{RegisterSystem(}}{y:RegisterSystem(}b{z}";\n'
        code = code_only(source)
        self.assertEqual(len(source), len(code))
        self.assertEqual(["x", "=", "y", "z", ";"], code.split())
        self.assertEqual(source.index("{y") + 1, code.index("y"))
        self.assertEqual(source.index("{z") + 1, code.index("z"))

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

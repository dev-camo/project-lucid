import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / "docs" / "offline-hooks.md"
SWITCH = "OfflineAdapters.Active"
TAG = re.compile(r"Offline hook (H-\d{3})\b")
ROW = re.compile(r"^\| (H-\d{3}) \| `([^`]+)` \| `([^`]+)` \| (.+) \|$")
# The only place where the original cannot compile, so a compile-time condition stays (decision D2).
ALLOWED_DEFINES = {
    "Packages/com.hardlight.hlunitycore/Runtime/Logging/HLUnityCoreNativeBridge.cs": "PROJECT_LUCID_ORIGINAL_UNITY_CORE_NATIVE_BRIDGE",
}


def runtime_sources():
    for top in ("Assets/Scripts", "Packages"):
        for path in sorted((ROOT / top).rglob("*.cs")):
            yield path.relative_to(ROOT).as_posix(), path.read_text(encoding="utf-8").split("\n")


def source_hooks():
    """Hook IDs by file, plus every use of the switch with the tag that documents it."""
    tags = {}
    uses = []
    for rel, lines in runtime_sources():
        for number, line in enumerate(lines):
            for hook_id in TAG.findall(line):
                tags.setdefault(hook_id, []).append((rel, number + 1))
            code = line.split("//")[0]
            if SWITCH in code and not line.lstrip().startswith("//"):
                window = lines[max(0, number - 8):number + 1]
                found = [hook for text in window for hook in TAG.findall(text)]
                uses.append((rel, number + 1, found[-1] if found else None))
    return tags, uses


def registry_rows():
    rows = {}
    for line in REGISTRY.read_text(encoding="utf-8").split("\n"):
        match = ROW.match(line)
        if match:
            assert match.group(1) not in rows, "duplicate registry id " + match.group(1)
            rows[match.group(1)] = (match.group(2), match.group(3), match.group(4))
    return rows


class OfflineHookRegistryTests(unittest.TestCase):
    def test_every_use_of_the_switch_is_documented_by_a_tag(self):
        _, uses = source_hooks()
        self.assertTrue(uses)
        for rel, number, hook_id in uses:
            with self.subTest(at="%s:%d" % (rel, number)):
                self.assertIsNotNone(hook_id, "OfflineAdapters.Active without an 'Offline hook H-nnn' comment")

    def test_registry_and_source_name_the_same_hooks_in_the_same_files(self):
        tags, _ = source_hooks()
        rows = registry_rows()
        self.assertEqual(sorted(tags), sorted(rows))
        for hook_id, places in tags.items():
            with self.subTest(hook=hook_id):
                self.assertEqual(len({rel for rel, _ in places}), 1, "a hook id lives in one file")
                self.assertEqual(places[0][0], rows[hook_id][0])
                self.assertEqual(len(places), 1, "an id is tagged once")

    def test_registered_members_exist_in_their_files(self):
        for hook_id, (rel, members, text) in registry_rows().items():
            with self.subTest(hook=hook_id):
                source = (ROOT / rel).read_text(encoding="utf-8")
                for member in re.split(r",\s*", members):
                    name = member.split(" ")[0]
                    self.assertIn(name, source)
                self.assertGreater(len(text), 20, "the row says what the original did and what the adapter does")

    def test_hook_ids_are_unique_and_registry_lists_the_switch_file(self):
        rows = registry_rows()
        ids = sorted(rows, key=lambda value: int(value[2:]))
        self.assertEqual(ids, sorted(rows))
        self.assertTrue((ROOT / "Packages/com.hardlight.hlunitycore/Runtime/Offline/OfflineAdapters.cs").is_file())
        self.assertIn("OfflineAdapters.cs", REGISTRY.read_text(encoding="utf-8"))

    def test_no_compile_time_original_branch_remains_except_where_the_original_cannot_compile(self):
        found = {}
        for rel, lines in runtime_sources():
            for number, line in enumerate(lines):
                if "PROJECT_LUCID_ORIGINAL_" in line and line.lstrip().startswith(("#if", "#elif", "#define")):
                    found.setdefault(rel, set()).update(re.findall(r"PROJECT_LUCID_ORIGINAL_\w+", line))
        self.assertEqual({rel: sorted(defs) for rel, defs in found.items()},
                         {rel: [define] for rel, define in ALLOWED_DEFINES.items()})

    def test_the_switch_is_a_non_const_property_returning_true(self):
        text = (ROOT / "Packages/com.hardlight.hlunitycore/Runtime/Offline/OfflineAdapters.cs").read_text(encoding="utf-8")
        self.assertIn("public static bool Active => true;", text)
        self.assertNotIn("const", re.sub(r"//.*", "", text))


if __name__ == "__main__":
    unittest.main()

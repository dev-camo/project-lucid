"""The tracked desktop PlayerSettings keep the shipped 1.10.1 player values (#575)."""
from pathlib import Path
import re
import unittest

REPO = Path(__file__).resolve().parents[2]
SETTINGS = REPO / "ProjectSettings" / "ProjectSettings.asset"

# From the 2026-10-05 baseline export of the 6c89-acb input (ExportedProject/ProjectSettings/ProjectSettings.asset)
# and its Info.plist (CFBundleShortVersionString 1.10.1). Application.version reaches saves, challenge maintenance
# and What's New, so the version must be the shipped one.
SHIPPED = {
    "bundleVersion": "1.10.1",
    "runInBackground": "0",
    "fullscreenMode": "2",
    "forceSingleInstance": "1",
    "defaultScreenWidth": "1024",
    "defaultScreenHeight": "768",
}

# Deliberate Project Lucid values: the identity avoids colliding with the original, and both input handlers stay
# active for the Input System adapters (docs/offline-hooks.md).
LUCID = {
    "companyName": "Project Lucid",
    "productName": "Project Lucid",
    "activeInputHandler": "2",
}


def top_level_values(text):
    """The two-space-indented scalar keys of the PlayerSettings document."""
    return {match.group(1): match.group(2).strip() for match in re.finditer(r"^  ([A-Za-z]\w*): ?(.*)$", text, re.M)}


class ProjectSettingsTests(unittest.TestCase):
    def setUp(self):
        self.values = top_level_values(SETTINGS.read_text(encoding="utf-8"))

    def test_shipped_player_values_are_kept(self):
        for key, expected in SHIPPED.items():
            with self.subTest(key=key):
                self.assertEqual(expected, self.values.get(key))

    def test_lucid_identity_and_input_handler_are_kept(self):
        for key, expected in LUCID.items():
            with self.subTest(key=key):
                self.assertEqual(expected, self.values.get(key))

    def test_reader_sees_a_changed_value(self):
        # Negative control for the reader itself: a regressed version line is reported, not skipped.
        text = SETTINGS.read_text(encoding="utf-8").replace("  bundleVersion: 1.10.1\n", "  bundleVersion: 1.0\n")
        self.assertEqual("1.0", top_level_values(text)["bundleVersion"])


if __name__ == "__main__":
    unittest.main()

import sys
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import longpaths, project


def fake_winreg(value=None, error=None):
    module = MagicMock()
    module.HKEY_LOCAL_MACHINE = object()
    if error is not None:
        module.OpenKey.side_effect = error
    else:
        module.QueryValueEx.return_value = (value, 4)
    return module


class LongPathStatusTests(unittest.TestCase):
    def status(self, platform, registry=None):
        modules = {"winreg": registry} if registry else {}
        with patch.object(sys, "platform", platform), patch.dict(sys.modules, modules):
            return longpaths.long_paths_status()

    def test_status_reads_the_registry_only_on_windows(self):
        self.assertEqual(self.status("darwin"), "not-applicable")
        self.assertEqual(self.status("linux"), "not-applicable")
        self.assertEqual(self.status("win32", fake_winreg(1)), "enabled")
        self.assertEqual(self.status("win32", fake_winreg(0)), "disabled")
        self.assertEqual(self.status("win32", fake_winreg(error=FileNotFoundError())), "disabled")
        self.assertEqual(self.status("win32", fake_winreg(error=PermissionError())), "unreadable")

    def test_registry_is_never_written(self):
        registry = fake_winreg(0)
        self.status("win32", registry)
        self.assertFalse(registry.SetValueEx.called)
        self.assertFalse(registry.CreateKey.called)

    def run_doctor(self, status):
        with patch.object(project, "long_paths_status", return_value=status), \
                patch.object(project.shutil, "which", return_value=None):
            return project.doctor(Path("."), Path("work"))

    def test_doctor_reports_enabled_disabled_and_unreadable(self):
        enabled = self.run_doctor("enabled")
        self.assertEqual(enabled["long_paths"], "enabled")
        self.assertEqual(enabled["errors"], [])
        self.assertEqual(self.run_doctor("not-applicable")["errors"], [])
        disabled = self.run_doctor("disabled")
        self.assertEqual(disabled["long_paths"], "disabled")
        self.assertIn("Set-ItemProperty", disabled["errors"][0])
        self.assertIn("Enable Win32 long paths", disabled["errors"][0])
        unreadable = self.run_doctor("unreadable")
        self.assertEqual(unreadable["long_paths"], "unreadable")
        self.assertIn("Could not read", unreadable["errors"][0])


if __name__ == "__main__":
    unittest.main()

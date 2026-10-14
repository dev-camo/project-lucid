"""List discovery names without running the player/native guard test bodies."""

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import types
import unittest
from unittest.mock import patch


class _DiscoveryComplete(BaseException):
    pass


def _names(module):
    if getattr(module, "load_tests", None) is not None:
        raise AssertionError("List-only discovery refuses a load_tests hook")
    for value in vars(module).values():
        if isinstance(value, type) and issubclass(value, unittest.TestCase):
            for name in ("__init__", "__new__", "id"):
                if getattr(value, name) is not getattr(unittest.TestCase, name):
                    raise AssertionError("List-only discovery refuses custom TestCase " + name)
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromModule(module)
    if loader.errors:
        raise AssertionError("\n".join(loader.errors))

    def identifiers(tests):
        for test in tests:
            if isinstance(test, unittest.TestSuite):
                yield from identifiers(test)
            else:
                identifier = test.id()
                prefix = module.__name__ + "."
                if not identifier.startswith(prefix):
                    raise AssertionError("Unexpected discovered module: " + identifier)
                yield identifier[len(prefix):]

    names = sorted(identifiers(suite))
    if not names:
        raise AssertionError("List-only discovery found no tests")
    return names


def _list_names(path, mode):
    """Execute exact declarations under direct/import names, never the suite."""
    source = path.read_bytes()
    module = types.ModuleType("__main__" if mode == "direct" else "_discovery_import")
    module.__file__ = str(path)
    discovered = None

    def main(*args, **kwargs):
        nonlocal discovered
        if mode != "direct" or args or kwargs or discovered is not None:
            raise AssertionError("Unexpected unittest.main invocation")
        discovered = _names(module)
        # Model unittest.main's default exit at this exact declaration boundary.
        raise _DiscoveryComplete()

    def refuse(*args, **kwargs):
        raise AssertionError("List-only discovery cannot run tests or child processes")

    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    with patch.dict(sys.modules, {module.__name__: module}), \
            patch.object(unittest, "main", main), \
            patch.object(unittest.TestCase, "run", refuse), \
            patch.object(unittest.TestCase, "debug", refuse), \
            patch.object(subprocess, "run", refuse), \
            patch.object(subprocess, "Popen", refuse):
        try:
            exec(compile(source, str(path), "exec"), module.__dict__)
        except _DiscoveryComplete:
            pass
        if mode == "direct":
            if discovered is None:
                raise AssertionError("Direct entrypoint did not call unittest.main")
        else:
            discovered = _names(module)
    return {"names": discovered, "source_sha256": hashlib.sha256(source).hexdigest()}


class UnittestDiscoveryTests(unittest.TestCase):
    def collect(self, path, mode):
        # Child namespaces isolate imports, sys.path and the intercepted main.
        result = subprocess.run(
            [sys.executable, "-I", "-B", str(Path(__file__).resolve()),
             "--list-names", mode, str(path)],
            capture_output=True, text=True, timeout=20)
        self.assertEqual(0, result.returncode, result.stderr)
        return json.loads(result.stdout)

    def synthetic(self, source):
        temporary = tempfile.TemporaryDirectory(prefix="unittest names ")
        self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / "fixture.py"
        path.write_text(source, encoding="utf-8")
        return path

    def test_maintained_direct_and_import_discovery_names_match(self):
        for name in ("test_playercode.py", "test_runtimecontracts.py"):
            with self.subTest(module=name):
                path = Path(__file__).with_name(name)
                direct = self.collect(path, "direct")
                imported = self.collect(path, "import")
                self.assertEqual(direct["source_sha256"], imported["source_sha256"])
                self.assertEqual(direct["names"], imported["names"])

    def test_late_class_is_missing_at_direct_main(self):
        path = self.synthetic("""import unittest
class Early(unittest.TestCase):
    def test_early(self): raise AssertionError('body must not run')
if __name__ == '__main__': unittest.main()
class Late(unittest.TestCase):
    def test_late(self): raise AssertionError('body must not run')
""")
        direct = self.collect(path, "direct")["names"]
        imported = self.collect(path, "import")["names"]
        self.assertEqual(["Early.test_early"], direct)
        self.assertEqual(["Early.test_early", "Late.test_late"], imported)
        self.assertNotEqual(direct, imported)

    def test_equal_counts_cannot_hide_changed_identifiers(self):
        path = self.synthetic("""import unittest
class Replaced(unittest.TestCase):
    def test_before(self): raise AssertionError('body must not run')
if __name__ == '__main__': unittest.main()
class Replaced(unittest.TestCase):
    def test_after(self): raise AssertionError('body must not run')
""")
        direct = self.collect(path, "direct")["names"]
        imported = self.collect(path, "import")["names"]
        self.assertEqual(len(direct), len(imported))
        self.assertEqual(["Replaced.test_before"], direct)
        self.assertEqual(["Replaced.test_after"], imported)
        self.assertNotEqual(direct, imported)

    def test_final_guard_lists_names_without_running_setup_or_bodies(self):
        path = self.synthetic("""import unittest
class Owned(unittest.TestCase):
    def setUp(self): raise AssertionError('setup must not run')
    def test_owned(self): raise AssertionError('body must not run')
if __name__ == '__main__': unittest.main()
""")
        self.assertEqual(["Owned.test_owned"], self.collect(path, "direct")["names"])
        self.assertEqual(["Owned.test_owned"], self.collect(path, "import")["names"])

    def test_missing_main_and_empty_discovery_are_failures(self):
        missing = self.synthetic("""import unittest
class Owned(unittest.TestCase):
    def test_owned(self): raise AssertionError('body must not run')
""")
        with self.assertRaisesRegex(AssertionError, "did not call unittest.main"):
            self.collect(missing, "direct")
        empty = self.synthetic("""import unittest
if __name__ == '__main__': unittest.main()
""")
        for mode in ("direct", "import"):
            with self.subTest(mode=mode):
                with self.assertRaisesRegex(AssertionError, "found no tests"):
                    self.collect(empty, mode)

    def test_custom_loader_constructor_and_id_are_not_executed(self):
        for custom in ("__init__", "__new__", "id", "load_tests"):
            with self.subTest(custom=custom):
                if custom == "load_tests":
                    extra = "def load_tests(*args): raise AssertionError('hook must not run')\n"
                else:
                    extra = "    def " + custom + "(*args): raise AssertionError('body must not run')\n"
                source = "import unittest\nclass Owned(unittest.TestCase):\n"
                source += "    def test_owned(self): raise AssertionError('body must not run')\n"
                source += extra + "if __name__ == '__main__': unittest.main()\n"
                path = self.synthetic(source)
                for mode in ("direct", "import"):
                    with self.subTest(mode=mode):
                        with self.assertRaisesRegex(AssertionError, "List-only discovery refuses"):
                            self.collect(path, mode)


if __name__ == "__main__":
    if len(sys.argv) == 4 and sys.argv[1] == "--list-names":
        if sys.argv[2] not in ("direct", "import"):
            raise ValueError("Unknown discovery mode")
        print(json.dumps(_list_names(Path(sys.argv[3]).resolve(), sys.argv[2])))
    else:
        unittest.main()

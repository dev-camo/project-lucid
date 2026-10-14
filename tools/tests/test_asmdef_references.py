import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class AssemblyDefinitionReferenceTests(unittest.TestCase):
    def test_no_assembly_definition_names_the_obsolete_ugui_assembly(self):
        # The pinned uGUI 1.0.0 runtime assembly is UnityEngine.UI; Unity.ugui names nothing.
        definitions = sorted(path for top in ("Assets", "Packages") for path in (ROOT / top).rglob("*.asmdef"))
        self.assertTrue(definitions)
        for path in definitions:
            with self.subTest(path=path.relative_to(ROOT).as_posix()):
                references = json.loads(path.read_text(encoding="utf-8")).get("references", [])
                self.assertNotIn("Unity.ugui", references)


if __name__ == "__main__":
    unittest.main()

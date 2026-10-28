"""The committed identity table (tools/assetripper/identity-table.txt) is canonical and made for the pinned inputs.

Regenerating it needs the baseline export and a patched witness export, so that gate is a maintainer command
(tools/assetripper/TABLE.md): assetripper_identity_table.py build ... --expect tools/assetripper/identity-table.txt.
"""

import hashlib
import json
from pathlib import Path
import unittest

from tools import assetripper_identity_table as command
from tools.lucidlib import identitymap

REPO = Path(__file__).resolve().parents[2]
TABLE = REPO / "tools" / "assetripper" / "identity-table.txt"
# Observed on the 1.10.1 build (input 6c89-acb) against the baseline export 20261005T062956Z-cef5ba97.
COLLECTIONS, SCRIPTS, MIXER_ROWS, UNRESOLVED = 29285, 3777, 619, 2


class CommittedTableTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = TABLE.read_bytes().decode("utf-8")
        cls.table = identitymap.parse_table(cls.text)

    def test_table_is_canonical(self):
        # parse_table already refuses unsorted, duplicate, CRLF and badly escaped text; the round trip is exact.
        self.assertEqual(self.text, identitymap.format_table(self.table))

    def test_header_matches_the_pinned_input_assetripper_commit_and_patch(self):
        header = command.pinned_header()
        identitymap.require_header(self.table, header["input"], header["assetripper"], header["patch"])
        patch = (REPO / "tools" / "assetripper" / "lucid-identity.patch").read_bytes()
        self.assertEqual(hashlib.sha256(patch).hexdigest(), self.table["header"]["patch"])
        lock = json.loads((REPO / "tools" / "tool-lock.json").read_text(encoding="utf-8"))["artifacts"]["assetripper-source"]
        self.assertEqual(lock["version"], self.table["header"]["assetripper"])

    def test_row_counts_are_those_of_the_baseline(self):
        self.assertEqual((COLLECTIONS, SCRIPTS, UNRESOLVED), (
            len(self.table["collections"]), len(self.table["scripts"]), len(self.table["unresolved"])))
        self.assertEqual(MIXER_ROWS, sum(map(len, self.table["mixer"].values())))
        self.assertEqual(["Assets/Data/Audio/Main.mixer"], list(self.table["mixer"]))

    def test_every_baseline_guid_is_used_once(self):
        guids = [row["guid"] for row in self.table["collections"].values()] + [
            row["guid"] for row in self.table["scripts"].values()]
        self.assertEqual(len(guids), len(set(guids)))
        self.assertEqual(COLLECTIONS + SCRIPTS, len(guids))

    def test_scripts_keep_their_baseline_guid_and_the_collections_do_not_include_them(self):
        self.assertTrue(all(path.endswith(".cs") for path in self.table["scripts"]))
        self.assertFalse(any(path.endswith(".cs") for row in self.table["collections"].values() for path in row["paths"]))

    def test_order_witnessed_classes_are_listed(self):
        witnessed = [member for members in self.table["witnessed"] for member in members]
        known = {row["guid"] for row in self.table["collections"].values()}
        self.assertTrue(witnessed)
        self.assertLessEqual(set(witnessed), known)
        self.assertEqual(len(witnessed), len(set(witnessed)))
        self.assertTrue(all(len(members) >= 2 for members in self.table["witnessed"]))

    def test_file_is_smaller_than_the_size_the_owner_asked_to_be_consulted_about(self):
        self.assertLess(TABLE.stat().st_size, 15_000_000)


if __name__ == "__main__":
    unittest.main()

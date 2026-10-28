#!/usr/bin/env python3
"""Commands for the committed identity table (#552). Read-only on the baseline and the input.

  build      --witness W --baseline RUN --app APP --out TABLE [--report JSON] [--expect COMMITTED_TABLE]
             Proves RUN is the pinned baseline run (its input-manifest.json hashes to the pinned value), that
             the input is the pinned one and the witness export W equals the baseline apart from the
             known random fields, then writes the table keyed by W's original identities. Refuses on any
             unexplained difference.
  roundtrip  --witness W --baseline RUN --table TABLE --stage DIR   (RUN must be the pinned baseline run too)
             Runs identitymap.build_baseline_project on W with the table and checks every output file against
             the baseline files themselves (not only against the table).
  check      --table TABLE
             The table parses, is canonical, and its header matches the pinned input, AssetRipper commit and patch.

W is a patched export's "project" directory (ExportedProject, AuxiliaryFiles/export_identity.json); RUN is the
baseline run directory (project/ExportedProject, asset-map.json). Regenerate the committed table with `build`
against any witness export; the result must be byte-identical (see tools/assetripper/TABLE.md).
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from tools import assetripper_identity_compare as compare  # noqa: E402
from tools.lucidlib import exportidentity, identitymap, identitytable  # noqa: E402

REPO = Path(__file__).resolve().parents[1]
DEFAULT_TABLE = REPO / "tools" / "assetripper" / "identity-table.txt"
# sha256 of the baseline run's input-manifest.json (input 6c89-acb): the run 20261005T062956Z-cef5ba97.
INPUT_MANIFEST_SHA256 = "15268415fc3309c1aa8dafd72ec8e7559c117749043d5a38faa226a9dea4b6d5"


def pinned_header() -> dict:
    lock = json.loads((REPO / "tools" / "tool-lock.json").read_text(encoding="utf-8"))["artifacts"]["assetripper-source"]
    return {"schema": identitymap.SCHEMA, "input": INPUT_MANIFEST_SHA256, "assetripper": lock["version"],
            "patch": lock["patch_sha256"]}


def input_manifest_sha256(app: Path) -> str:
    """The sha256 the pipeline would record for `app` (assets.extract_assets writes this exact document)."""
    from tools.lucidlib.inspection import _manifest
    text = json.dumps({"files": _manifest(Path(app))}, indent=2, sort_keys=True) + "\n"
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def require_pinned_baseline(run: Path) -> str | None:
    """None when `run` is the pinned baseline run (its input-manifest.json hashes to the pinned value), else why not."""
    manifest = Path(run) / "input-manifest.json"
    if not manifest.is_file():
        return f"{run} has no input-manifest.json, so it is not the pinned baseline run"
    found = hashlib.sha256(manifest.read_bytes()).hexdigest()
    if found != INPUT_MANIFEST_SHA256:
        return f"the baseline run's input manifest is {found}, the pinned one is {INPUT_MANIFEST_SHA256}"
    return None


def baseline_catalog_keys(run: Path) -> list[str]:
    """The catalog's identifiers that look like a GUID (check_collisions compares fresh GUIDs with them)."""
    asset_map = json.loads((run / "asset-map.json").read_text(encoding="utf-8"))
    keys = {key for location in asset_map["catalog_locations"] for key in location.get("keys", [])
            if isinstance(key, str)}
    keys |= {item["internal_id"] for item in asset_map["catalog_identities"]}
    return sorted(keys)


def build(args) -> int:
    witness, run = Path(args.witness).resolve(), Path(args.baseline).resolve()
    header = pinned_header()
    why = require_pinned_baseline(run)
    if why:
        print(f"REFUSED: {why}")
        return 1
    found = input_manifest_sha256(args.app)
    if found != header["input"]:
        print(f"REFUSED: the input manifest is {found}, the pinned one is {header['input']}")
        return 1
    print("input manifest sha256 equals the pinned value", flush=True)
    result = compare.compare(witness, run / "project")
    print(json.dumps(result["counts"]), flush=True)
    if not result["ok"]:
        print("REFUSED: the witness differs from the baseline:", json.dumps(
            {key: result[key] for key in ("extra_in_patched", "missing_in_patched", "other_differences",
                                          "other_difference_count")}))
        return 1
    collections = exportidentity.load_identity(witness / compare.IDENTITY)
    table, report = identitytable.build_table(witness / "ExportedProject", collections,
                                              run / "project" / "ExportedProject", header)
    text = identitymap.format_table(table)
    Path(args.out).write_bytes(text.encode("utf-8"))
    report["compare"] = result["counts"]
    report["table"] = {"path": str(args.out), "bytes": len(text.encode("utf-8")), "lines": text.count("\n"),
                       "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
                       "collections": len(table["collections"]), "scripts": len(table["scripts"]),
                       "files": len(table["files"]), "mixer_rows": sum(map(len, table["mixer"].values())),
                       "unresolved": len(table["unresolved"]), "witnessed_classes": len(table["witnessed"]),
                       "witnessed_members": sum(map(len, table["witnessed"]))}
    asset_map = json.loads((run / "asset-map.json").read_text(encoding="utf-8"))
    report["asset_map_unresolved"] = {"asset_map": len(asset_map["unresolved_references"]),
                                      "table": len(table["unresolved"]),
                                      "table_contains_asset_map": set(asset_map["unresolved_references"]) <= table["unresolved"]}
    if args.report:
        Path(args.report).write_text(json.dumps(report, indent=1, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report["table"]))
    if args.expect:
        same = Path(args.expect).read_bytes() == text.encode("utf-8")
        print(f"regenerated table {'EQUALS' if same else 'DIFFERS FROM'} {args.expect}")
        return 0 if same else 1
    return 0


def roundtrip(args) -> int:
    witness, run = Path(args.witness).resolve(), Path(args.baseline).resolve()
    header = pinned_header()
    why = require_pinned_baseline(run)
    if why:
        print(f"REFUSED: {why}")
        return 1
    result = identitymap.build_baseline_project(
        witness / "ExportedProject", witness / compare.IDENTITY, Path(args.table), Path(args.stage),
        baseline_catalog_keys(run), header["input"], header["assetripper"], header["patch"])
    independent = identitytable.compare_stage_with_baseline(Path(args.stage), run / "project" / "ExportedProject")
    print(json.dumps({"edits": result["edits"], "gate": result["gate"], "order_witnessed": len(result["order_witnessed"]),
                      "stage_equals_baseline_files": independent}))
    return 0


def check(args) -> int:
    table = identitymap.parse_table(Path(args.table).read_bytes().decode("utf-8"))
    header = pinned_header()
    identitymap.require_header(table, header["input"], header["assetripper"], header["patch"])
    print(f"ok: {len(table['collections'])} collections, {len(table['scripts'])} scripts, {len(table['files'])} files")
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    for name, function in (("build", build), ("roundtrip", roundtrip), ("check", check)):
        sub = commands.add_parser(name)
        sub.set_defaults(function=function)
        if name != "check":
            sub.add_argument("--witness", required=True)
            sub.add_argument("--baseline", required=True)
        if name == "build":
            sub.add_argument("--app", required=True)
            sub.add_argument("--out", required=True)
            sub.add_argument("--report")
            sub.add_argument("--expect", help="a table the regenerated bytes must equal (the regeneration gate)")
        if name == "roundtrip":
            sub.add_argument("--stage", required=True)
        if name != "build":
            sub.add_argument("--table", default=str(DEFAULT_TABLE))
    args = parser.parse_args(argv)
    return args.function(args)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

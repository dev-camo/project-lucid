#!/usr/bin/env python3
"""Recover user-supplied game content and assess reconstruction readiness."""

import argparse
import json
from pathlib import Path
import sys
import subprocess

ROOT = Path(__file__).resolve().parent.parent


def write_report(work_dir, name, report):
    from lucidlib.bootstrap import managed_path, write_json
    destination = managed_path(work_dir, "reports", name)
    write_json(destination, report)
    return destination


def parser():
    result = argparse.ArgumentParser(description=__doc__)
    commands = result.add_subparsers(dest="command", required=True)
    for name in ("doctor", "bootstrap", "inspect", "recover-code", "native-build", "native-schema", "native-method", "script-layouts", "player-code", "player-schema", "runtime-contracts", "theme-evidence", "extract-assets", "shader-evidence", "prepare", "audit", "validate", "test", "build", "progress", "port"):
        command = commands.add_parser(name)
        command.add_argument("--work-dir", type=Path, default=ROOT / ".cache" / "project-lucid")
        command.add_argument("--json", action="store_true", help="print a compact JSON result")
        if name in ("inspect", "recover-code", "native-schema", "native-method", "extract-assets", "progress"):
            command.add_argument("--input", type=Path, default=ROOT / "input" / "SonicDreamTeam.app")
        if name == "port":
            command.add_argument("--input", type=Path, default=ROOT / "input")
            command.add_argument("--output", type=Path, default=ROOT / "output")
            command.add_argument("--target", choices=("macos", "windows", "linux"), required=True)
        if name == "shader-evidence":
            command.add_argument("--input", type=Path, required=True, help="an exported Shader .asset or export asset directory")
        if name == "bootstrap":
            command.add_argument("--tool", action="append", choices=("assetripper", "cpp2il", "dumper"))
        if name == "recover-code":
            command.add_argument("--mode", choices=("schemas", "bodies", "analysis"), default="schemas")
        if name in ("native-schema", "native-method"):
            command.add_argument("--assembly", required=name == "native-method", help="exact original assembly name")
        if name == "native-method":
            command.add_argument("--token", required=True, help="original MethodDef token, e.g. 0x060039b9")
        if name == "script-layouts":
            command.add_argument("--schema", type=Path, help="generated native schema; defaults to the latest successful run")
        if name == "validate":
            command.add_argument("--stage", choices=("extraction", "release"), default="release")
        if name == "test":
            command.add_argument("--mode", choices=("editmode", "playmode"), required=True)
        if name in ("runtime-contracts", "theme-evidence"):
            command.add_argument("--schema", type=Path, required=True, help="complete original constrained native schema")
            command.add_argument("--inventory", type=Path, required=True, help="current loaded Editor inventory against that schema")
        if name in ("build", "player-code", "player-schema", "runtime-contracts"):
            command.add_argument("--target", choices=("macos", "windows", "linux"), required=True)
        if name == "progress":
            command.add_argument("--target", choices=("macos", "windows", "linux"), default="macos")
            mode = command.add_mutually_exclusive_group()
            mode.add_argument("--check", action="store_true", help="check the generated table without game input or Unity")
            mode.add_argument("--stage", action="store_true", help="refresh and stage progress for the exact Git source index")
    return result


def main(argv=None):
    args = parser().parse_args(argv)
    try:
        if args.command == "progress" and args.check:
            from lucidlib.progressreport import check_progress
            report = check_progress(ROOT)
            if args.json:
                print(json.dumps(report, indent=2, sort_keys=True))
            else:
                totals = report["totals"]
                print("progress: {} / {} original methods have maintained source bodies".format(
                    totals["implemented_source_bodies"], totals["original_native_bodies"]))
            return 0
        from lucidlib.bootstrap import validate_work_dir
        work_dir = validate_work_dir(args.work_dir)
        if args.command == "inspect":
            from lucidlib.inspection import inspect_bundle
            report = inspect_bundle(args.input)
            destination = write_report(work_dir, "inspection.json", report)
            present = report["addressables"]["bundle_count"]
            missing = len(report["addressables"]["missing_bundles"])
            identity = report["identity"]
            summary = {"status": "failed" if missing else "complete", "version": identity["version"],
                       "unity_version": identity["unity_version"], "bundles_present": present,
                       "bundles_missing": missing, "report": str(destination)}
            print(json.dumps(summary, indent=2) if args.json else
                  "Sonic Dream Team {} / Unity {}\nbundles: {} present, {} missing\nreport: {}".format(
                      identity["version"], identity["unity_version"], present, missing, destination))
            return 1 if missing else 0
        if args.command == "bootstrap":
            from lucidlib.bootstrap import bootstrap
            report = bootstrap(work_dir, selected=args.tool)
        elif args.command == "recover-code":
            from lucidlib.recovery import recover_code
            report = recover_code(args.input, work_dir, mode=args.mode)
        elif args.command == "native-build":
            from lucidlib.native import build_native_harness
            report = build_native_harness(work_dir)
        elif args.command == "native-schema":
            from lucidlib.native import native_schema
            report = native_schema(args.input, work_dir, args.assembly)
        elif args.command == "native-method":
            from lucidlib.native import native_method
            report = native_method(args.input, work_dir, args.assembly, args.token)
        elif args.command == "script-layouts":
            from lucidlib.bindings import run_layout_inventory
            report = run_layout_inventory(ROOT, work_dir, args.schema)
        elif args.command == "player-code":
            from lucidlib.playercode import run_player_code
            report = run_player_code(ROOT, work_dir, args.target)
        elif args.command == "player-schema":
            from lucidlib.playerschema import run_player_schema
            report = run_player_schema(ROOT, work_dir, args.target)
        elif args.command == "runtime-contracts":
            from lucidlib.runtimecontracts import run_runtime_contracts
            report = run_runtime_contracts(ROOT, work_dir, args.target, args.schema, args.inventory)
        elif args.command == "theme-evidence":
            from lucidlib.runtimecontracts import compare_verified_theme_planes
            report = compare_verified_theme_planes(ROOT, work_dir, args.schema, args.inventory)
        elif args.command == "progress":
            from lucidlib.progressreport import generate_progress
            report = generate_progress(ROOT, work_dir, args.input, args.target, args.stage)
            if args.json:
                print(json.dumps(report, indent=2, sort_keys=True))
            else:
                totals = report["totals"]
                print("progress: {} / {} original methods have maintained source bodies".format(
                    totals["implemented_source_bodies"], totals["original_native_bodies"]))
            return 0
        elif args.command == "extract-assets":
            from lucidlib.assets import extract_assets
            report = extract_assets(args.input, work_dir)
        elif args.command == "shader-evidence":
            from lucidlib.shaders import extract_shader_evidence
            report = extract_shader_evidence(args.input, work_dir)
        elif args.command == "prepare":
            from lucidlib.assets import prepare_assets
            report = prepare_assets(ROOT, work_dir)
        elif args.command == "validate":
            from lucidlib.project import validate_project
            report = validate_project(ROOT, work_dir, stage=args.stage)
            if args.stage == "release":
                write_report(work_dir, "release-validation.json", report)
        elif args.command == "test":
            from lucidlib.verification import run_tests
            report = run_tests(ROOT, work_dir, args.mode)
        elif args.command == "audit":
            from lucidlib.verification import run_audit
            report = run_audit(ROOT, work_dir)
        elif args.command == "port":
            from lucidlib.port import run_port
            report = run_port(ROOT, work_dir, args.input, args.target, args.output)
        elif args.command == "build":
            from lucidlib.project import build_project
            report = build_project(ROOT, work_dir, args.target)
        else:
            from lucidlib.project import doctor
            report = doctor(ROOT, work_dir)
        destination = write_report(work_dir, args.command + ".json", report)
        if args.json:
            print(json.dumps(report, indent=2, sort_keys=True))
        else:
            print("{}: {}".format(args.command, report.get("status", "unknown")))
            for key in ("message", "error", "output_dir", "project_path", "log", "log_path"):
                if report.get(key):
                    print("{}: {}".format(key, report[key]))
            for item in report.get("errors", []):
                print("error: " + str(item))
            for item in report.get("limitations", []):
                print("limitation: " + str(item))
            print("report: " + str(destination))
        if args.command == "port":
            return int(report.get("exit_code", 1))
        return 1 if report.get("status") in ("failed", "error", "blocked", "incomplete") else 0
    except (ValueError, OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        print("error: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

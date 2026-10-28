#!/usr/bin/env python3
"""Run the source-built AssetRipper on an original .app with the pipeline's export settings.

Maintainer helper for #552 evidence runs: it writes only below --out (which must not
exist) and never launches anything from the input; the built tool reads the input.
Usage: assetripper_identity_export.py --ripper <AssetRipper.GUI.Free> --app <.app> --out <new dir>
"""

import argparse
from pathlib import Path
import shutil
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from tools.lucidlib import assets  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ripper", type=Path, required=True)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    # AssetRipper writes beside its executable, so each run gets its own copy of the build.
    runtime = out / "ripper"
    shutil.copytree(args.ripper.resolve().parent, runtime)
    process, base, console = assets._start_ripper(runtime / args.ripper.name, out)
    try:
        assets._request(base, "/Settings/Update", assets.EXPORT_SETTINGS)
        assets._request(base, "/LoadFolder", {"Path": str(args.app.resolve())})
        assets._request(base, "/Export/UnityProject", {"Path": str(out / "project"), "CreateSubfolder": "false"})
    finally:
        assets._stop_ripper(process, console)
        shutil.rmtree(runtime)
    log = (out / "assetripper.log").read_text(encoding="utf-8", errors="replace")
    complete = "Finished post-export" in log and assets.IL2CPP_BACKEND_LINE in log
    print(f"export {'complete' if complete else 'INCOMPLETE'}: {out / 'project'}")
    return 0 if complete else 1


if __name__ == "__main__":
    sys.exit(main())

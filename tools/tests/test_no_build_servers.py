import ast
import tempfile
import unittest
from unittest import mock
from pathlib import Path

import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap

LIB = Path(bootstrap.__file__).parent
REQUIRED = ["-nodeReuse:false", "-p:UseSharedCompilation=false"]


class NoBuildServerTests(unittest.TestCase):
    def test_arguments_disable_every_server(self):
        for flag in REQUIRED + ["--disable-build-servers", "-p:UseRazorBuildServer=false"]:
            self.assertIn(flag, bootstrap.NO_BUILD_SERVER_ARGS)

    def test_environment_disables_servers(self):
        with tempfile.TemporaryDirectory() as tmp, \
                mock.patch.object(bootstrap, "tool_work_dir", side_effect=lambda w: w), \
                mock.patch.object(bootstrap, "managed_path", side_effect=lambda w, *p: Path(w).joinpath(*p)):
            env = bootstrap.dotnet_environment(Path(tmp), Path(tmp) / "dotnet")
        self.assertEqual("0", env["DOTNET_CLI_USE_MSBUILD_SERVER"])
        self.assertEqual("1", env["MSBUILDDISABLENODEREUSE"])
        self.assertEqual("false", env["UseSharedCompilation"])

    def test_every_dotnet_build_or_publish_call_site_passes_the_arguments(self):
        # Each argv list that names a dotnet "build" or "publish" verb must splat NO_BUILD_SERVER_ARGS.
        sites = []
        for path in sorted(LIB.glob("*.py")):
            tree = ast.parse(path.read_text(encoding="utf-8"))
            for node in ast.walk(tree):
                if not isinstance(node, ast.List):
                    continue
                consts = [e.value for e in node.elts if isinstance(e, ast.Constant)]
                if any(v in ("build", "publish") for v in consts[:1] + consts[1:2]) and len(node.elts) > 3 \
                        and any(isinstance(e, ast.Call) for e in node.elts[:1]):
                    splats = [e for e in node.elts if isinstance(e, ast.Starred)
                              and isinstance(e.value, ast.Name) and e.value.id == "NO_BUILD_SERVER_ARGS"]
                    sites.append((path.name, node.lineno, bool(splats)))
        self.assertGreaterEqual(len(sites), 3, sites)
        self.assertEqual([], [s for s in sites if not s[2]], sites)

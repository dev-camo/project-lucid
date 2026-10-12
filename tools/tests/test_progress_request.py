"""Run the workflow's no-checkout payload writer against untrusted fork events."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import textwrap
import unittest


class ProgressRequestTests(unittest.TestCase):
    def run_writer(self, head):
        workflow = Path(__file__).resolve().parents[2] / '.github/workflows/progress-refresh-request.yml'
        code = workflow.read_text().split("python3 - <<'PY'\n", 1)[1].split('\n          PY', 1)[0]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            event = {'number': 42, 'pull_request': {'head': {'sha': head},
                     'body': 'private payload must not be copied',
                     'title': '$(touch injected)', 'head_ref': '../../escape'}}
            payload = root / 'event.json'
            payload.write_text(json.dumps(event))
            summary = root / 'summary'
            env = dict(os.environ, GITHUB_EVENT_PATH=str(payload),
                       GITHUB_STEP_SUMMARY=str(summary), GITHUB_SHA='b' * 40)
            result = subprocess.run([sys.executable, '-c', textwrap.dedent(code)],
                                    cwd=root, env=env, capture_output=True)
            artifact = root / 'request/progress-refresh.json'
            return result, json.loads(artifact.read_text()) if artifact.exists() else None, (
                summary.read_text() if summary.exists() else '')

    def test_request_uses_source_head_and_only_public_routing_fields(self):
        result, artifact, summary = self.run_writer('a' * 40)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(artifact, {'schema_version': 1, 'kind': 'progress-refresh-request',
                                   'pull_request': 42, 'head_sha': 'a' * 40,
                                   'build_performed': False})
        self.assertIn('not a build receipt', summary)
        self.assertNotIn('private payload', summary)
        self.assertNotIn('touch injected', summary)

    def test_invalid_head_never_produces_artifact(self):
        result, artifact, summary = self.run_writer('$(touch injected)')
        self.assertNotEqual(result.returncode, 0)
        self.assertIsNone(artifact)
        self.assertEqual(summary, '')

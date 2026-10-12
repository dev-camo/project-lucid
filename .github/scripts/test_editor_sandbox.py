"""Real namespace regressions use harmless fixtures, never Unity or credentials."""
import contextlib
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).parent))
import editor_sandbox as sandbox


class EditorSandboxTests(unittest.TestCase):
    def test_only_editor_subprocess_facade_is_replaced_and_restored(self):
        module = SimpleNamespace(subprocess=subprocess)
        with mock.patch.object(sandbox, 'licensing_broker', return_value=contextlib.nullcontext(('fixture', []))), \
             mock.patch.object(sandbox, 'prepare_home', return_value=Path('/fixture/source/home')):
            with self.assertRaisesRegex(ValueError, 'fixture failure'):
                with sandbox.isolated_editor(module, Path('/fixture/source')):
                    self.assertIsInstance(module.subprocess, sandbox.EditorProcess)
                    self.assertIs(subprocess.run, sandbox.subprocess.run)
                    raise ValueError('fixture failure')
        self.assertIs(subprocess, module.subprocess)

    def test_missing_license_broker_never_allows_unsandboxed_fallback(self):
        with self.assertRaisesRegex(ValueError, 'refuse unsandboxed'):
            sandbox.licensing_sockets('fixture-no-broker')

    def test_trusted_broker_uses_existing_auth_and_task_owned_pipe_then_stops(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            process = mock.Mock(pid=12345)
            process.poll.return_value = None
            with mock.patch.object(sandbox.subprocess, 'Popen', return_value=process) as spawn, \
                 mock.patch.object(sandbox.os, 'killpg') as stop, \
                 mock.patch.object(sandbox, 'licensing_sockets', return_value=[Path('/fixture/socket')]), \
                 mock.patch.dict(os.environ, {'GH_TOKEN': 'never-broker', 'GITHUB_OUTPUT': 'never-broker'}):
                with sandbox.licensing_broker(root) as (pipe, sockets):
                    self.assertTrue(pipe.startswith('Unity-LicenseClient-lucid-job-'))
                    self.assertEqual([Path('/fixture/socket')], sockets)
                command = spawn.call_args.args[0]
                self.assertEqual([str(sandbox.LICENSE_CLIENT), '--namedPipe', pipe, '--disable-auto-shutdown'], command)
                self.assertNotIn('GH_TOKEN', spawn.call_args.kwargs['env'])
                self.assertNotIn('GITHUB_OUTPUT', spawn.call_args.kwargs['env'])
                self.assertEqual([mock.call(12345, sandbox.signal.SIGTERM), mock.call(12345, sandbox.signal.SIGKILL)],
                                 stop.call_args_list)
                process.wait.assert_called_once_with(timeout=5)

    def test_failed_broker_start_stops_without_launching_editor(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            process = mock.Mock(pid=12345)
            process.poll.return_value = 1
            with mock.patch.object(sandbox.subprocess, 'Popen', return_value=process), \
                 mock.patch.object(sandbox.os, 'killpg') as stop:
                with self.assertRaisesRegex(ValueError, 'refuse unsandboxed'):
                    with sandbox.licensing_broker(root):
                        self.fail('A failed broker cannot admit the Editor')
                self.assertEqual(2, stop.call_count)

    def test_editor_launcher_closes_channels_and_clears_environment(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory).resolve()
            home = source / 'home'
            home.mkdir()
            with mock.patch.object(sandbox.subprocess, 'run') as invoke:
                launcher = sandbox.EditorProcess(source, home, [], 'fixture-pipe')
                launcher.run([str(sandbox.EDITOR), '-batchmode'], timeout=1800, stdout=None, stderr=None)
                call = invoke.call_args
                self.assertEqual({}, call.kwargs['env'])
                self.assertTrue(call.kwargs['close_fds'])
                self.assertEqual(subprocess.DEVNULL, call.kwargs['stdin'])
                self.assertIn('--unshare-all', call.args[0])
                self.assertIn('--disable-userns', call.args[0])
                self.assertIn('-nographics', call.args[0])
                self.assertIn('-licensingIpc', call.args[0])
                with self.assertRaises(ValueError):
                    launcher.run(['/bin/sh'], timeout=1, stdout=None, stderr=None)
                with self.assertRaises(ValueError):
                    launcher.run([str(sandbox.EDITOR)], timeout=1, stdout=None, stderr=None, pass_fds=(1,))
                with self.assertRaises(AttributeError):
                    launcher.Popen(['/bin/sh'])

    def test_private_home_copies_package_cache_without_account_or_license_data(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            source, host_home = root / 'source', root / 'host-home'
            source.mkdir()
            cache = host_home / '.config/unity3d/cache'
            cache.mkdir(parents=True)
            (cache / 'package').write_text('public package fixture')
            (cache.parent / 'account-token').write_text('private fixture')
            with mock.patch.object(sandbox.Path, 'home', return_value=host_home):
                home = sandbox.prepare_home(source)
            self.assertEqual('public package fixture', (home / '.config/unity3d/cache/package').read_text())
            self.assertFalse((home / '.config/unity3d/account-token').exists())

    def test_package_cache_symlink_cannot_copy_private_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            cache = root / 'host-home/.config/unity3d/cache'
            cache.mkdir(parents=True)
            (root / 'private').write_text('private fixture')
            (cache / 'link').symlink_to(root / 'private')
            with mock.patch.object(sandbox.Path, 'home', return_value=root / 'host-home'):
                with self.assertRaisesRegex(ValueError, 'Symlinked'):
                    sandbox.prepare_home(root / 'source')

    @unittest.skipUnless(sys.platform == 'linux', 'requires the Linux namespace boundary')
    def test_real_sandbox_cannot_overwrite_controller_or_seal_channels_or_read_host_processes(self):
        self.assertTrue(shutil.which('bwrap'), 'Install bubblewrap to run the enforced-boundary regression')
        with tempfile.TemporaryDirectory() as directory, socket.socket() as network:
            root = Path(directory).resolve()
            source = root / 'source'
            home = source / 'home'
            home.mkdir(parents=True)
            controller, output, credential = root / 'controller.py', root / 'GITHUB_OUTPUT', root / '.credentials'
            for path in (controller, output, credential):
                path.write_text('protected fixture')
            cached_tool = source / '.cache/project-lucid/tools/dotnet/dotnet'
            cached_tool.parent.mkdir(parents=True)
            cached_tool.write_text('trusted executable fixture')
            (source / 'escape').symlink_to(controller)
            network.bind(('127.0.0.1', 0))
            network.listen()
            code = '''import os,socket
from pathlib import Path
for name in PATHS:
    try: Path(name).write_text('forged fixture')
    except OSError: pass
assert not Path(CREDENTIAL).exists()
assert not Path('/proc/'+str(HOST_PID)+'/environ').exists()
assert 'GITHUB_OUTPUT' not in os.environ and 'GH_TOKEN' not in os.environ
connection=socket.socket()
assert connection.connect_ex(('127.0.0.1',HOST_PORT)) != 0
Path(RESULT).write_text('isolated fixture passed')
'''
            code = ('PATHS=' + repr([str(controller), str(output), str(source / 'escape'), str(cached_tool)]) + '\n'
                    'CREDENTIAL=' + repr(str(credential)) + '\nHOST_PID=' + str(os.getpid()) + '\n'
                    'HOST_PORT=' + str(network.getsockname()[1]) + '\nRESULT=' + repr(str(home / 'result')) + '\n' + code)
            command = sandbox.sandbox_command(source, home, ['/usr/bin/python3', '-c', code], [])
            with mock.patch.dict(os.environ, {'GITHUB_OUTPUT': str(output), 'GH_TOKEN': 'private fixture'}):
                result = subprocess.run(command, env=os.environ.copy(), stdin=subprocess.DEVNULL,
                                        close_fds=True, timeout=15, capture_output=True)
                self.assertEqual(0, result.returncode, result.stderr.decode())
            for path in (controller, output, credential):
                self.assertEqual('protected fixture', path.read_text())
            self.assertEqual('trusted executable fixture', cached_tool.read_text())
            self.assertEqual('isolated fixture passed', (home / 'result').read_text())


if __name__ == '__main__':
    unittest.main()

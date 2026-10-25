"""Owned inert processes exercise registration before service execution."""
import json
import itertools
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest import mock

import process_registry as registry
import direct_progress as shared


class ProcessRegistryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.marker = self.root / 'compiler-group.json'
        self.root_id = os.getpid()
        self.marker.write_text(json.dumps({'group': self.root_id}))
        self.ready = self.root / 'service-ready'
        self.command = [sys.executable, '-I', '-c',
                        'from pathlib import Path; Path(' + repr(str(self.ready)) + ').write_text("ready")']

    def test_success_registers_both_markers_before_releasing_exact_command(self):
        local = self.root / 'local-group.json'
        command = [sys.executable, '-I', '-c',
                   'import json,os; from pathlib import Path; '
                   'global_row=json.loads(Path(' + repr(str(self.marker)) + ').read_bytes()); '
                   'local_row=json.loads(Path(' + repr(str(local)) + ').read_bytes()); '
                   'assert os.getpid() in global_row["groups"]; '
                   'assert local_row == {"group":os.getpid(),"groups":[os.getpid()]}; '
                   'Path(' + repr(str(self.ready)) + ').write_text("ready")']
        process = registry.spawn_registered(command, [(self.marker, self.root_id), (local, None)],
                                             cwd=self.root, env={}, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            _, errors = process.communicate(timeout=10)
            self.assertEqual(0, process.returncode, errors)
            self.assertEqual('ready', self.ready.read_text())
            self.assertEqual({'group': self.root_id, 'groups': sorted([self.root_id, process.pid])},
                             json.loads(self.marker.read_bytes()))
            self.assertEqual({'group': process.pid, 'groups': [process.pid]}, json.loads(local.read_bytes()))
        finally:
            if process.poll() is None:
                process.kill()
            process.communicate(timeout=10)

    def test_registration_failure_never_releases_service_and_reaps_gate(self):
        spawned = []
        real = subprocess.Popen

        def spawn(*args, **kwargs):
            process = real(*args, **kwargs)
            spawned.append(process)
            return process

        failure = OSError('inert registration write failure')
        with mock.patch.object(registry.subprocess, 'Popen', side_effect=spawn), \
                mock.patch.object(registry, 'merge_registry', side_effect=failure):
            with self.assertRaises(OSError) as caught:
                registry.spawn_registered(self.command, [(self.marker, self.root_id)], cwd=self.root, env={})
        self.assertIs(failure, caught.exception)
        self.assertEqual(1, len(spawned))
        self.assertIsNotNone(spawned[0].returncode)
        self.assertFalse(self.ready.exists())

    def test_registry_preserves_root_and_union_and_rejects_foreign_or_linked_state(self):
        registry.merge_registry(self.marker, self.root_id, [self.root_id + 1])
        registry.merge_registry(self.marker, self.root_id, [self.root_id + 2])
        self.assertEqual({'group': self.root_id, 'groups': [self.root_id, self.root_id + 1, self.root_id + 2]},
                         json.loads(self.marker.read_bytes()))
        with self.assertRaises(ValueError):
            registry.merge_registry(self.marker, self.root_id + 3, [])
        outside = self.root / 'untouched'
        outside.write_bytes(b'untouched')
        self.marker.unlink()
        self.marker.symlink_to(outside)
        with self.assertRaises(ValueError):
            registry.merge_registry(self.marker, self.root_id, [])
        self.assertEqual(b'untouched', outside.read_bytes())

    def test_release_requires_eof_and_rejects_a_later_second_byte(self):
        reader, writer = os.pipe()
        command = [sys.executable, '-I', str(Path(registry.__file__).resolve()), str(reader), '--', *self.command]
        process = subprocess.Popen(command, pass_fds=(reader,), start_new_session=True,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        os.close(reader)
        try:
            os.write(writer, b'G')
            deadline = time.monotonic() + 1
            while process.poll() is None and time.monotonic() < deadline:
                time.sleep(0.01)
            self.assertIsNone(process.poll(), 'A single release byte without EOF must remain gated')
            self.assertFalse(self.ready.exists())
            os.write(writer, b'X')
            os.close(writer)
            writer = None
            process.communicate(timeout=10)
            self.assertEqual(71, process.returncode)
            self.assertFalse(self.ready.exists())
        finally:
            if writer is not None:
                os.close(writer)
            if process.poll() is None:
                process.kill()
            process.communicate(timeout=10)

    def test_abrupt_parent_exit_preserves_custody_before_and_after_release(self):
        for stage in ('before_registration', 'after_registration', 'after_ready'):
            with self.subTest(stage=stage):
                root = self.root / stage
                root.mkdir()
                marker, ready, stop = root / 'compiler-group.json', root / 'ready', root / 'stop'
                service = ('import time; from pathlib import Path; '
                           'Path(' + repr(str(ready)) + ').write_text("ready"); '
                           '\nwhile not Path(' + repr(str(stop)) + ').exists(): time.sleep(.01)\n')
                script = '''import json,os,subprocess,sys,time
from pathlib import Path
sys.path.insert(0, SCRIPTS)
import process_registry as registry
root=Path(ROOT)
marker=root/'compiler-group.json'
marker.write_text(json.dumps({'group':os.getpid()}))
reader,writer=os.pipe()
child=subprocess.Popen([sys.executable,'-I',REGISTRY,str(reader),'--',sys.executable,'-I','-c',SERVICE],
                       pass_fds=(reader,),start_new_session=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
os.close(reader)
(root/'child.json').write_text(json.dumps({'group':child.pid}))
if STAGE=='before_registration': os._exit(0)
registry.merge_registry(marker,os.getpid(),[child.pid])
if STAGE=='after_registration': os._exit(0)
os.write(writer,b'G');os.close(writer)
while not (root/'ready').exists(): time.sleep(.01)
os._exit(0)
'''
                constants = {'SCRIPTS': str(Path(registry.__file__).resolve().parent), 'ROOT': str(root),
                             'REGISTRY': str(Path(registry.__file__).resolve()), 'SERVICE': service, 'STAGE': stage}
                script = '\n'.join(name + '=' + repr(value) for name, value in constants.items()) + '\n' + script
                process = subprocess.Popen([sys.executable, '-I', '-c', script], start_new_session=True,
                                           stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                try:
                    _, errors = process.communicate(timeout=10)
                    self.assertEqual(0, process.returncode, errors)
                    if stage == 'after_ready':
                        self.assertTrue(ready.exists())
                        with self.assertRaisesRegex(ValueError, 'still active'):
                            shared.require_stopped_marker(marker)
                    else:
                        self.assertFalse(ready.exists())
                finally:
                    stop.write_text('stop owned service')
                    if process.poll() is None:
                        process.kill()
                    process.communicate(timeout=10)
                    child_marker = root / 'child.json'
                    if child_marker.exists():
                        child = json.loads(child_marker.read_bytes())['group']
                        deadline = time.monotonic() + 10
                        while shared.group_running(child) and time.monotonic() < deadline:
                            time.sleep(.01)
                        shared.require_stopped_marker(child_marker)
                shared.require_stopped_marker(marker)
                if stage != 'after_ready':
                    self.assertFalse(ready.exists(), 'EOF gate must never execute even after parent exit')

    def test_concurrent_registration_and_stop_union_use_one_stable_lock(self):
        registry.merge_registry(self.marker, self.root_id, [])
        lock = self.marker.with_name(self.marker.name + '.lock')
        inode = lock.stat().st_ino
        code = ('import os,sys; from pathlib import Path; sys.path.insert(0,'
                + repr(str(Path(registry.__file__).resolve().parent)) + '); import process_registry; '
                'process_registry.merge_registry(Path(' + repr(str(self.marker)) + '),'
                + str(self.root_id) + ',[os.getpid()]); print(os.getpid())')
        processes = []
        try:
            for _ in range(3):
                processes.append(subprocess.Popen([sys.executable, '-I', '-c', code], start_new_session=True,
                                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True))
            groups = [self.root_id]
            for process in processes:
                output, errors = process.communicate(timeout=10)
                self.assertEqual(0, process.returncode, errors)
                groups.append(int(output))
            self.assertEqual({'group': self.root_id, 'groups': sorted(groups)},
                             json.loads(self.marker.read_bytes()))
            # Stop-time observations merge into the same registry; they do not replace it.
            registry.merge_registry(self.marker, self.root_id, [self.root_id])
            self.assertEqual(sorted(groups), json.loads(self.marker.read_bytes())['groups'])
            self.assertEqual(inode, lock.stat().st_ino)
        finally:
            for process in processes:
                if process.poll() is None:
                    process.kill()
                process.communicate(timeout=10)

    def test_stop_preserves_registered_orphans_without_signaling_historical_ids(self):
        historical, current = self.root_id + 2, self.root_id + 1
        registry.merge_registry(self.marker, self.root_id, [historical])
        process = mock.Mock(pid=self.root_id)
        with mock.patch.object(shared, 'freeze_compiler_groups', return_value=[self.root_id, current]), \
                mock.patch.object(shared, 'signal_group') as signal, \
                mock.patch.object(shared, 'group_running', return_value=False):
            shared.stop_compiler_group(process, self.marker)
        self.assertEqual([self.root_id, current, historical], json.loads(self.marker.read_bytes())['groups'])
        self.assertEqual({self.root_id, current}, {call.args[0] for call in signal.call_args_list})
        with mock.patch.object(shared, 'group_running', side_effect=lambda group: group == historical):
            with self.assertRaisesRegex(ValueError, 'still active'):
                shared.require_stopped_marker(self.marker)

    def test_stopping_a_descendant_holding_registry_lock_does_not_deadlock(self):
        lock = self.marker.with_name(self.marker.name + '.lock')
        ready = self.root / 'lock-held'
        command = [sys.executable, '-I', '-c',
                   'import fcntl,time; from pathlib import Path; '
                   'lock=open(' + repr(str(lock)) + ',"a+b"); fcntl.flock(lock,fcntl.LOCK_EX); '
                   'Path(' + repr(str(ready)) + ').write_text("held"); time.sleep(60)']
        process = subprocess.Popen(command, start_new_session=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.marker.write_text(json.dumps({'group': process.pid}))
        finished, failures = threading.Event(), []

        def stop():
            try:
                shared.stop_compiler_group(process, self.marker)
            except BaseException as error:
                failures.append(error)
            finally:
                finished.set()

        thread = threading.Thread(target=stop, daemon=True)
        try:
            deadline = time.monotonic() + 10
            while not ready.exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(.01)
            self.assertTrue(ready.exists(), 'Actual child must own the lock before stopping begins')
            thread.start()
            self.assertTrue(finished.wait(5), 'Frozen lock owner must be stopped before registry lock acquisition')
            self.assertEqual([], failures)
            self.assertIsNotNone(process.returncode)
            shared.require_stopped_marker(self.marker)
        finally:
            if process.poll() is None:
                process.kill()  # Exact still-owned child; no historical registry ID is signaled.
            if thread.ident is not None:
                thread.join(timeout=10)
                self.assertFalse(thread.is_alive())
            process.communicate(timeout=10)

    def test_failed_teardown_still_records_newly_observed_owned_groups(self):
        current, historical = self.root_id + 1, self.root_id + 2
        registry.merge_registry(self.marker, self.root_id, [historical])
        failure = subprocess.TimeoutExpired(['owned child'], 10)
        process = mock.Mock(pid=self.root_id)
        process.communicate.side_effect = [None, failure]
        with mock.patch.object(shared, 'freeze_compiler_groups', return_value=[self.root_id, current]), \
                mock.patch.object(shared, 'signal_group'), \
                mock.patch.object(shared, 'group_running', return_value=False):
            with self.assertRaises(subprocess.TimeoutExpired) as caught:
                shared.stop_compiler_group(process, self.marker)
        self.assertIs(failure, caught.exception)
        self.assertEqual([self.root_id, current, historical], json.loads(self.marker.read_bytes())['groups'])
        process = mock.Mock(pid=self.root_id)
        with mock.patch.object(shared, 'freeze_compiler_groups', return_value=[self.root_id, current + 2]), \
                mock.patch.object(shared, 'signal_group'), \
                mock.patch.object(shared, 'group_running', return_value=True), \
                mock.patch.object(shared.time, 'monotonic', side_effect=itertools.chain([0], itertools.repeat(11))):
            with self.assertRaisesRegex(ValueError, 'did not stop'):
                shared.stop_compiler_group(process, self.marker)
        self.assertEqual([self.root_id, current, historical, current + 2],
                         json.loads(self.marker.read_bytes())['groups'])

    def test_registered_run_keeps_local_proof_and_parent_registry_separate(self):
        local = self.root / 'precheck-group.json'
        code = ('import json,os; from pathlib import Path; '
                'row=json.loads(Path(' + repr(str(self.marker)) + ').read_bytes()); '
                'assert os.getpid() in row["groups"]; print(os.getpid())')
        with (self.root / 'errors').open('wb') as errors:
            output = shared.run_compiler([sys.executable, '-I', '-c', code], self.root, errors, local,
                                         registry=(self.marker, self.root_id))
        group = int(output)
        self.assertEqual({'group': group, 'groups': [group]}, json.loads(local.read_bytes()))
        self.assertEqual({'group': self.root_id, 'groups': sorted([self.root_id, group])},
                         json.loads(self.marker.read_bytes()))
        shared.require_stopped_marker(local)

    def test_run_refuses_a_live_previous_registry_before_starting_another_command(self):
        process = subprocess.Popen([sys.executable, '-I', '-c', 'import time; time.sleep(30)'],
                                   start_new_session=True)
        self.marker.write_text(json.dumps({'group': process.pid, 'groups': [process.pid]}))
        original = self.marker.read_bytes()
        try:
            with (self.root / 'errors').open('wb') as errors:
                with self.assertRaisesRegex(ValueError, 'still active'):
                    shared.run_compiler(self.command, self.root, errors, self.marker)
            self.assertEqual(original, self.marker.read_bytes())
            self.assertFalse(self.ready.exists())
        finally:
            process.terminate()
            process.wait(timeout=10)


if __name__ == '__main__':
    unittest.main()

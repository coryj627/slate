"""Private controls for witnessed descendants; no native app or shared-service signals."""
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

SPEC = importlib.util.spec_from_file_location('owned_pilot', Path(__file__).resolve().parents[1] / 'mac-ci-pilot.py')
pilot = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(pilot)


def evidence(name, value):
    folder = os.environ.get('MAC_PILOT_CONTROL_EVIDENCE')
    if folder:
        pilot.write_json(Path(folder) / (name + '.json'), value)


def until(predicate, seconds=3):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        result = predicate()
        if result:
            return result
        time.sleep(.01)
    raise AssertionError('controlled fixture readiness timed out')


class FakeProc:
    pid = 900001
    def poll(self): return -15
    def wait(self, timeout=None): return -15


def row(pid, parent, birth, uid=None, presence='live'):
    return {'pid': pid, 'ppid': parent, 'pgid': pid, 'uid': os.geteuid() if uid is None else uid,
            'birth': [birth, 0], 'presence': presence}


class OwnershipIdentityControls(unittest.TestCase):
    def test_parent_replaced_during_child_read_is_never_adopted_or_signalled(self):
        state = {900001: row(900001, 1, 10), 900002: row(900002, 900001, 20)}
        calls = []
        def reader(pid):
            if pid == 900002:
                state[900001] = row(900001, 1, 19)  # replacement between parent checks
            return dict(state[pid])
        owner = pilot.OwnedProcesses(FakeProc(), reader=reader, rows=lambda: {900002: 900001}, killer=lambda *x: calls.append(x))
        owner.observe()
        self.assertNotIn(900002, owner.owned)
        result = owner.terminate(grace=0)
        self.assertEqual(calls, [])
        self.assertFalse(result['all_observed_owned_stopped'])
        self.assertIn(900001, result['unknown_or_reused_owned_pids_after'])
        evidence('parent-replacement-during-read', result)

    def test_child_birth_cannot_precede_its_immediate_parent(self):
        state = {900001: row(900001, 1, 10), 900002: row(900002, 900001, 20),
                 900003: row(900003, 900002, 15)}
        owner = pilot.OwnedProcesses(FakeProc(), reader=lambda p: dict(state[p]), rows=lambda: {900002: 900001, 900003: 900002}, killer=lambda *x: None)
        owner.observe()
        self.assertIn(900002, owner.owned)
        self.assertNotIn(900003, owner.owned)
        evidence('immediate-parent-birth', owner.snapshot())

    def test_reused_pid_and_changed_uid_are_not_signalled(self):
        for changed in [row(900001, 1, 11), row(900001, 1, 10, uid=os.geteuid() + 1)]:
            with self.subTest(changed=changed):
                state = {900001: row(900001, 1, 10)}
                calls = []
                owner = pilot.OwnedProcesses(FakeProc(), reader=lambda p: dict(state[p]), rows=lambda: {}, killer=lambda *x: calls.append(x))
                state[900001] = changed
                result = owner.terminate(grace=0)
                self.assertEqual(calls, [])
                self.assertFalse(result['all_observed_owned_stopped'])
                evidence('reused-or-other-uid-' + str(changed['uid']) + '-' + str(changed['birth'][0]), result)

    def test_identity_exception_is_unknown_and_never_signalled(self):
        failing = False
        def reader(pid):
            if failing: raise OSError('controlled denied identity read')
            return row(pid, 1, 10)
        calls = []
        owner = pilot.OwnedProcesses(FakeProc(), reader=reader, rows=lambda: {}, killer=lambda *x: calls.append(x))
        failing = True
        result = owner.terminate(grace=0)
        self.assertEqual(calls, [])
        self.assertFalse(result['all_observed_owned_stopped'])
        self.assertIn(900001, result['unknown_or_reused_owned_pids_after'])
        evidence('identity-read-failure', result)

    def test_ancestry_failure_records_incomplete_capture(self):
        def failed_rows(): raise subprocess.TimeoutExpired('ps', .5)
        owner = pilot.OwnedProcesses(FakeProc(), reader=lambda p: row(p, 1, 10), rows=failed_rows, killer=lambda *x: None)
        result = owner.observe()
        self.assertTrue(any('sampling failed' in x['reason'] for x in result['issues']))
        evidence('ancestry-timeout', result)


class ControlledFamily:
    """Only this test's Popen children; start/release files give scheduling barriers."""
    def __init__(self):
        self.temp = tempfile.TemporaryDirectory(prefix='slate-owned-control-')
        self.root = Path(self.temp.name)
        self.tokens = {}
        self.cleanup = []
        child = """import json,os,signal,time
from pathlib import Path
signal.signal(signal.SIGTERM, signal.SIG_IGN)
Path(os.environ['CONTROL_READY']).write_text(json.dumps({'pid':os.getpid(),'ppid':os.getppid(),'pgid':os.getpgrp()}))
while True: time.sleep(.1)
"""
        leader = """import os,subprocess,sys,time
from pathlib import Path
root=Path(os.environ['CONTROL_ROOT'])
while not (root/'start').exists(): time.sleep(.01)
subprocess.Popen([sys.executable,'-c',CHILD],start_new_session=True)
while not (root/'release').exists(): time.sleep(.01)
""".replace('CHILD', repr(child))
        env = {**os.environ, 'CONTROL_ROOT': str(self.root), 'CONTROL_READY': str(self.root/'ready.json')}
        self.leader = subprocess.Popen([sys.executable, '-c', leader], env=env, start_new_session=True)
        self.remember(self.leader.pid)
        self.owner = pilot.OwnedProcesses(self.leader)

    def remember(self, pid):
        identity = pilot.process_identity(pid)
        if identity.get('presence') != 'live': raise AssertionError(identity)
        self.tokens[pid] = identity
        return identity

    def spawn(self):
        (self.root/'start').touch()
        until(lambda: (self.root/'ready.json').exists())
        self.child = json.loads((self.root/'ready.json').read_text())['pid']
        child = self.remember(self.child)
        if child['pgid'] == self.leader.pid: raise AssertionError('fixture did not escape the old group')
        return self.child

    def witness(self):
        until(lambda: self.owner.observe() and self.child in self.owner.owned)

    def reparent(self):
        (self.root/'release').touch()
        self.leader.wait(timeout=3)
        until(lambda: pilot.process_identity(self.child).get('ppid') != self.leader.pid)

    def stop_fixture(self):
        # Fixture cleanup is explicit and identity checked even for deliberate misses.
        for pid, known in reversed(list(self.tokens.items())):
            if pid == self.leader.pid: self.leader.poll()
            now = pilot.process_identity(pid)
            match = pilot.OwnedProcesses.token(now) == pilot.OwnedProcesses.token(known)
            event = {'pid': pid, 'identity_before': now, 'expected_identity': known, 'token_matched': match}
            if match and now.get('presence') == 'live':
                try:
                    os.kill(pid, signal.SIGKILL)
                    event['signal'] = signal.SIGKILL
                except ProcessLookupError: event['already_gone'] = True
            self.cleanup.append(event)
        try: self.leader.wait(timeout=3)
        except subprocess.TimeoutExpired: pass
        for pid in self.tokens:
            until(lambda p=pid: pilot.process_identity(p).get('presence') in ('absent', 'zombie'))
        evidence('fixture-finally-' + str(self.leader.pid), {'cleanup': self.cleanup,
                 'after': [pilot.process_identity(pid) for pid in self.tokens]})
        self.temp.cleanup()


@unittest.skipUnless(sys.platform == 'darwin' or sys.platform.startswith('linux'), 'requires birth identity reader')
class OwnershipProcessControls(unittest.TestCase):
    def test_old_group_only_cleanup_leaves_escaped_child_alive(self):
        family = ControlledFamily()
        try:
            child = family.spawn()
            result = pilot.terminate_group(family.leader, grace=.1)
            actual = pilot.process_identity(child)
            self.assertTrue(result['group_has_no_live_members'], result)
            self.assertEqual(actual['presence'], 'live', actual)
            evidence('old-group-negative', {'cleanup': result, 'escaped_child_after': actual})
        finally: family.stop_fixture()

    def test_observed_escaped_term_ignoring_reparented_child_is_stopped(self):
        family = ControlledFamily()
        foreign = None
        try:
            child = family.spawn()
            family.witness()
            foreign = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'], start_new_session=True)
            foreign_identity = family.remember(foreign.pid)
            family.reparent()
            result = family.owner.terminate(grace=.1)
            self.assertTrue(result['all_observed_owned_stopped'], result)
            self.assertIn(child, result['pids_after_term_grace'])
            self.assertTrue(any(x['pid'] == child and x['signal'] == signal.SIGKILL and x['result'] == 'sent' for x in result['signals']))
            self.assertNotIn(foreign.pid, family.owner.owned)
            self.assertEqual(pilot.process_identity(foreign.pid)['presence'], 'live')
            self.assertFalse(result['unobserved_descendants_proved_absent'])
            evidence('owned-escape-reparent-positive', {'cleanup': result, 'unrelated_control_after': pilot.process_identity(foreign.pid), 'unrelated_expected': foreign_identity})
        finally:
            family.stop_fixture()
            if foreign: foreign.wait(timeout=3)

    def test_unobserved_early_reparenting_is_an_explicit_miss(self):
        family = ControlledFamily()
        try:
            child = family.spawn()
            family.reparent()  # no owner observation while the parent was alive
            result = family.owner.terminate(grace=.1)
            self.assertNotIn(child, family.owner.owned)
            self.assertEqual(pilot.process_identity(child)['presence'], 'live')
            self.assertFalse(result['unobserved_descendants_proved_absent'])
            evidence('unobserved-reparent-miss', {'cleanup': result, 'missed_child_after': pilot.process_identity(child)})
        finally: family.stop_fixture()


if __name__ == '__main__': unittest.main()

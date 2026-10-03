#!/usr/bin/env python3
"""PRIVATE single-case actual CI cancellation proposal; never a full acceptance gate.

Uses existing diagnostic qualification and native Finder case unchanged. No TCC,
Finder, volume, cache, account, real-vault, or application-source changes.
"""
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

spec = importlib.util.spec_from_file_location('finder_diagnostic', Path(__file__).with_name('mac-finder-diagnostic.py'))
d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d)
d.TOTAL_SECONDS = 480  # eight-minute wrapper cap within ten-minute whole job


class CancellationDiagnostic(d.Diagnostic):
    def run(self, name, argv, limit, observe=True, required=True, cwd=None, env=None):
        limit = min(limit, self.remaining())
        if limit <= 0: raise TimeoutError('overall cancellation diagnostic budget exhausted')
        record = {'argv': argv, 'started_utc': d.utc(), 'timeout_seconds': limit, 'status': 'running'}
        self.summary['phases'][name] = record
        self.persist()
        proc = owner = None
        started = time.monotonic()
        try:
            with (self.evidence / (name+'.stdout.log')).open('w') as out, (self.evidence / (name+'.stderr.log')).open('w') as err, (self.evidence / (name+'.processes.jsonl')).open('w') as samples:
                proc = subprocess.Popen(argv, cwd=cwd or self.source, env=env or self.env, stdout=out, stderr=err, start_new_session=True)
                owner = self.pilot.OwnedProcesses(proc)
                record['anchor_pid'] = proc.pid
                while proc.poll() is None:
                    ownership = owner.observe()
                    samples.write(json.dumps({'utc': d.utc(), 'ownership': ownership})+'\n')
                    samples.flush()
                    age = self.sample(name, proc, samples) if observe else 0
                    if name == 'native-case' and age >= 15 and 'cancel_ready' not in self.summary:
                        verified = []
                        for request in self.native_requests.values():
                            pid = request['pid']
                            if request['phase'] == name and request['observed_age_seconds'] >= 15 and pid in owner.owned:
                                identity = owner.current(pid)
                                if identity.get('presence') == 'live': verified.append({'request': request, 'identity': identity})
                        if verified:
                            self.summary['cancel_ready'] = {'utc': d.utc(), 'observed_native_osascripts': verified,
                                                          'action': 'root must cancel this actual GitHub run; no self-cancel substitute'}
                            self.persist()
                            print('CANCEL_PROBE_READY '+json.dumps(self.summary['cancel_ready'],sort_keys=True),flush=True)
                    if time.monotonic()-started >= limit or age >= d.OSA_SECONDS: raise TimeoutError('phase diagnostic watchdog; this is not an actual CI cancellation result')
                    if out.tell()+err.tell() > d.MAX_OUTPUT_BYTES: raise RuntimeError('output bound reached')
                    time.sleep(.5)
                record.update(exit_code=proc.returncode,status='success' if proc.returncode == 0 else 'failed')
        except BaseException as exc:
            record.update(status='cancelled' if isinstance(exc,d.Interrupted) else 'failed',error=str(exc))
            if proc:
                owner = owner or self.pilot.OwnedProcesses(proc)
                repeated = []
                handlers = {sig: signal.getsignal(sig) for sig in (signal.SIGINT,signal.SIGTERM)}
                record['process_cleanup'] = {**owner.snapshot(),'cleanup_complete':False,'status':'running'}
                try:
                    for sig in handlers: signal.signal(sig,lambda signum,frame: repeated.append({'signal':signum,'utc':d.utc()}))
                    self.persist()
                    record['process_cleanup'] = owner.terminate()
                except BaseException as cleanup_exc:
                    record['process_cleanup'] = {**owner.snapshot(),'signals':list(owner.signals),'cleanup_complete':False,
                                                 'all_observed_owned_stopped':False,'error':str(cleanup_exc)}
                finally:
                    record['process_cleanup']['repeated_signals']=repeated
                    self.persist()
                    for sig,handler in handlers.items(): signal.signal(sig,handler)
                record['exit_code']=proc.returncode
            if isinstance(exc,(d.Interrupted,d.BudgetExpired,KeyboardInterrupt)): raise
        finally:
            record.update(ended_utc=d.utc(),elapsed_seconds=time.monotonic()-started)
            if owner: record['ownership_final']={**owner.snapshot(),'observed_current':[owner.current(pid) for pid in owner.owned]}
            self.persist()
        if required and record['status'] != 'success': raise RuntimeError(name+' did not succeed; evidence retained')
        return record


def main():
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--evidence',type=Path,required=True)
    args=parser.parse_args()
    signal.signal(signal.SIGINT,d.interrupted)
    signal.signal(signal.SIGTERM,d.interrupted)
    signal.signal(signal.SIGALRM,d.budget_expired)
    diag=CancellationDiagnostic(args.source.resolve(),args.evidence.resolve())
    diag.summary['scope']='single existing native case and actual CI cancellation only; not full native/VoiceOver acceptance'
    diag.summary['ordinary_probe']={'status':'excluded','reason':'no second Finder request in cancellation probe'}
    signal.setitimer(signal.ITIMER_REAL,max(.01,diag.remaining()))
    try:
        diag.qualify()
        diag.native()
        diag.summary.update(status='inconclusive',error='native phase finished without actual GitHub cancellation')
    except BaseException as exc:
        diag.summary.update(status='cancelled' if isinstance(exc,d.Interrupted) else 'failed',error=str(exc))
    finally:
        signal.setitimer(signal.ITIMER_REAL,0)
        diag.finish()
    print(json.dumps({'status':diag.summary['status'],'evidence':str(diag.evidence)},sort_keys=True),flush=True)
    return 1  # a cancelled diagnostic is never a passing full-suite gate


if __name__ == '__main__': sys.exit(main())

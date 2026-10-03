"""PRIVATE proposed read-only Windows observer; product command and payload stay fixed.
Windows ABI execution has not been exercised on this Mac. Portable tests cover policy.
"""
import argparse,ctypes,hashlib,json,ntpath,os,re,shutil,subprocess,sys,time
from dataclasses import dataclass
from pathlib import Path

MAX_IDENTITIES=64
INTERVAL_SECONDS=5.0
MAX_OBSERVATION_SECONDS=3600.0
TAIL_SECONDS=10.0
TESTHOST_EXE_SHA='289a45b8fdcb0353f056432870494c56d8537e69a4312261625554e413228819'
SOURCE='98c73f45ac2b81518ce652a0f6612bd3ef83c5e1'
MANIFEST_SHA='93a097242cb3dc0383c8db245411054189e9c7cc2571e88be8f7e420ad67b9dd'
CANDIDATE_GIB={'namespace-4x8':(6,10),'namespace-4x16':(14,18),'hosted-2022':(14,18)}
def capacity_valid(value,candidate):
 require(candidate in CANDIDATE_GIB,'unknown resource candidate')
 lo,hi=CANDIDATE_GIB[candidate]
 return type(value) is int and lo*1024**3<=value<=hi*1024**3
DLL_RELATIVE='apps/slate-windows/tests/SlateWindows.Tests/bin/Release/net10.0-windows/SlateWindows.Tests.dll'
HOST_RELATIVE='apps/slate-windows/tests/SlateWindows.Tests/bin/Release/net10.0-windows/testhost.exe'
DWORD=ctypes.c_uint32; WORD=ctypes.c_uint16; SIZE_T=ctypes.c_uint64; HANDLE=ctypes.c_void_p
class FILETIME(ctypes.Structure):_fields_=[('low',DWORD),('high',DWORD)]
class PMC_EX(ctypes.Structure):
 _fields_=[('cb',DWORD),('PageFaultCount',DWORD)]+[(n,SIZE_T) for n in ('PeakWorkingSetSize','WorkingSetSize','QuotaPeakPagedPoolUsage','QuotaPagedPoolUsage','QuotaPeakNonPagedPoolUsage','QuotaNonPagedPoolUsage','PagefileUsage','PeakPagefileUsage','PrivateUsage')]
class MEMORY_STATUS(ctypes.Structure):
 _fields_=[('dwLength',DWORD),('dwMemoryLoad',DWORD)]+[(n,ctypes.c_uint64) for n in ('ullTotalPhys','ullAvailPhys','ullTotalPageFile','ullAvailPageFile','ullTotalVirtual','ullAvailVirtual','ullAvailExtendedVirtual')]
class PROCESS_ENTRY(ctypes.Structure):
 _fields_=[('dwSize',DWORD),('cntUsage',DWORD),('th32ProcessID',DWORD),('th32DefaultHeapID',SIZE_T),('th32ModuleID',DWORD),('cntThreads',DWORD),('th32ParentProcessID',DWORD),('pcPriClassBase',ctypes.c_int32),('dwFlags',DWORD),('szExeFile',WORD*260)]

def require(c,msg):
 if not c:raise ValueError(msg)
def file_sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def normalized(p):return os.path.normcase(os.path.realpath(p))
def ft(x):return int(x.low)+(int(x.high)<<32)
def native_last_error():return getattr(ctypes,'get_last_error',lambda:0)()
def wait_status_live(status,error=0):
 if type(status) is int and status==0:return False # WAIT_OBJECT_0: confirmed process exit.
 if type(status) is int and status==258:return True # WAIT_TIMEOUT: still live.
 raise OSError(error,'Process wait failed/unknown status: '+repr(status))
def validate_counter(c):
 keys=('workingSetBytes','peakWorkingSetBytes','privateCommitBytes','peakPrivateCommitBytes','kernel100ns','user100ns')
 require(set(c)==set(keys) and all(type(c[k]) is int and c[k]>=0 for k in keys),'invalid process counter')
 require(c['peakWorkingSetBytes']>=c['workingSetBytes'] and c['peakPrivateCommitBytes']>=c['privateCommitBytes'],'invalid high-water/current relation')
def validate_machine(m,candidate='namespace-4x8'):
 require(capacity_valid(m['totalPhysicalBytes'],candidate),'unexpected physical capacity')
 require(type(m['availablePhysicalBytes']) is int and 0<=m['availablePhysicalBytes']<=m['totalPhysicalBytes'],'invalid available physical memory')
 require(all(type(m[k]) is int and m[k]>=0 for k in ('idle100ns','kernel100ns','user100ns')),'invalid machine CPU times')
@dataclass(frozen=True)
class Identity:
 pid:int
 birth100ns:int
 image:str
 parent_pid:int=0

class NativeWindows:
 """Only query/read rights. No process injection, signal, scheduler or timer calls."""
 def __init__(self,candidate='namespace-4x8'):
  require(candidate in CANDIDATE_GIB,'unknown resource candidate');self.candidate=candidate
  require(os.name=='nt' and ctypes.sizeof(ctypes.c_void_p)==8,'Windows64 observer required')
  require(ctypes.sizeof(FILETIME)==8 and ctypes.sizeof(PMC_EX)==80 and ctypes.sizeof(MEMORY_STATUS)==64 and ctypes.sizeof(PROCESS_ENTRY)==568,'native64 ABI mismatch')
  self.k=ctypes.WinDLL('kernel32',use_last_error=True);self.handles={};self.unprovedOpenExclusions=[];self.unprovedOpenExclusionCount=0
  signatures={
   'OpenProcess':([DWORD,ctypes.c_int,DWORD],HANDLE),
   'CloseHandle':([HANDLE],ctypes.c_int),
   'GetProcessTimes':([HANDLE]+[ctypes.POINTER(FILETIME)]*4,ctypes.c_int),
   'GetSystemTimes':([ctypes.POINTER(FILETIME)]*3,ctypes.c_int),
   'K32GetProcessMemoryInfo':([HANDLE,ctypes.POINTER(PMC_EX),DWORD],ctypes.c_int),
   'GlobalMemoryStatusEx':([ctypes.POINTER(MEMORY_STATUS)],ctypes.c_int),
   'QueryFullProcessImageNameW':([HANDLE,DWORD,ctypes.c_void_p,ctypes.POINTER(DWORD)],ctypes.c_int),
   'CreateToolhelp32Snapshot':([DWORD,DWORD],HANDLE),
   'Process32FirstW':([HANDLE,ctypes.POINTER(PROCESS_ENTRY)],ctypes.c_int),
   'Process32NextW':([HANDLE,ctypes.POINTER(PROCESS_ENTRY)],ctypes.c_int),
   'WaitForSingleObject':([HANDLE,DWORD],DWORD),
  }
  for name,(args,result) in signatures.items():getattr(self.k,name).argtypes=args;getattr(self.k,name).restype=result
 def _handle(self,pid):
  if pid not in self.handles:
   h=self.k.OpenProcess(0x1000|0x0010|0x100000,False,pid) # QUERY_LIMITED_INFORMATION | VM_READ | SYNCHRONIZE (zero-time read-only wait)
   if not h:
    self.unprovedOpenExclusionCount+=1
    if len(self.unprovedOpenExclusions)<MAX_IDENTITIES:self.unprovedOpenExclusions.append({'pid':pid,'win32Error':native_last_error(),'scope':'unproved identity handle-open failure; not confirmed process exit'})
    return None
   self.handles[pid]=h
  return self.handles[pid]
 def _times(self,h):
  c,e,k,u=FILETIME(),FILETIME(),FILETIME(),FILETIME()
  if not self.k.GetProcessTimes(h,ctypes.byref(c),ctypes.byref(e),ctypes.byref(k),ctypes.byref(u)):return None
  return ft(c),ft(k),ft(u)
 def _live(self,h):
  status=self.k.WaitForSingleObject(h,0)
  return wait_status_live(status,native_last_error())
 def _query_failed_or_exited(self,h,pid,operation,error):
  if not self._live(h):return None
  raise OSError(error,operation+' failed on still-live process PID '+str(pid))
 def identity(self,pid):
  h=self._handle(pid)
  # Only a never-proved handle-open race is eligible for exclusion. Existing
  # owned/root/observer handles never convert unknown/API error into exit.
  if not h:return None
  if not self._live(h):return None
  ts=self._times(h)
  if ts is None:return self._query_failed_or_exited(h,pid,'GetProcessTimes',native_last_error())
  size=DWORD(32768);buffer=ctypes.create_unicode_buffer(size.value)
  if not self.k.QueryFullProcessImageNameW(h,0,buffer,ctypes.byref(size)):
   return self._query_failed_or_exited(h,pid,'QueryFullProcessImageNameW',native_last_error())
  if not self._live(h):return None
  return Identity(pid,ts[0],buffer.value)
 def entries(self):
  h=self.k.CreateToolhelp32Snapshot(2,0) # TH32CS_SNAPPROCESS; selection uses IDs/parent IDs only
  if h is None or h==ctypes.c_void_p(-1).value:raise OSError('process snapshot failed')
  out={};e=PROCESS_ENTRY();e.dwSize=ctypes.sizeof(e)
  try:
   ok=self.k.Process32FirstW(h,ctypes.byref(e))
   if not ok:raise OSError('empty/failed process snapshot')
   while ok:
    out[int(e.th32ProcessID)]=int(e.th32ParentProcessID)
    ok=self.k.Process32NextW(h,ctypes.byref(e))
   # ERROR_NO_MORE_FILES is the only accepted terminal snapshot status.
   if ctypes.get_last_error()!=18:raise OSError('partial process snapshot')
  finally:self.k.CloseHandle(h)
  return out
 def counters(self,identity):
  h=self._handle(identity.pid);before=self.identity(identity.pid)
  if before is None:return None
  if before.birth100ns!=identity.birth100ns:raise OSError('Owned process birth identity changed before counters')
  p=PMC_EX();p.cb=ctypes.sizeof(p)
  if not self.k.K32GetProcessMemoryInfo(h,ctypes.byref(p),p.cb):
   return self._query_failed_or_exited(h,identity.pid,'GetProcessMemoryInfo',native_last_error())
  ts=self._times(h)
  if ts is None:return self._query_failed_or_exited(h,identity.pid,'GetProcessTimes after memory query',native_last_error())
  after=self.identity(identity.pid)
  if after is None:return None
  require(after.birth100ns==identity.birth100ns and ts[0]==identity.birth100ns,'identity changed during counters')
  out={'workingSetBytes':int(p.WorkingSetSize),'peakWorkingSetBytes':int(p.PeakWorkingSetSize),'privateCommitBytes':int(p.PrivateUsage),'peakPrivateCommitBytes':int(p.PeakPagefileUsage),'kernel100ns':ts[1],'user100ns':ts[2]};validate_counter(out);return out
 def machine(self):
  ms=MEMORY_STATUS();ms.dwLength=ctypes.sizeof(ms);i,k,u=FILETIME(),FILETIME(),FILETIME()
  if not self.k.GlobalMemoryStatusEx(ctypes.byref(ms)) or not self.k.GetSystemTimes(ctypes.byref(i),ctypes.byref(k),ctypes.byref(u)):raise OSError('machine counters unavailable')
  out={'totalPhysicalBytes':int(ms.ullTotalPhys),'availablePhysicalBytes':int(ms.ullAvailPhys),'idle100ns':ft(i),'kernel100ns':ft(k),'user100ns':ft(u)};validate_machine(out,self.candidate);return out
 def close(self):
  for h in self.handles.values():self.k.CloseHandle(h)
  self.handles.clear()

class OwnedCollector:
 def __init__(self,backend,root,expected_host,hasher=file_sha):
  self.b=backend;self.root=root;self.expected_host=normalized(expected_host);self.hasher=hasher
  self.owned={root.pid:root};self.admissions=[{'pid':root.pid,'birth100ns':root.birth100ns,'parentPid':None,'parentBirth100ns':None,'image':root.image,'role':'launchedDotnetRoot'}];self.hosts={};self.retired=set();self.identityErrors=[]
 def discover(self):
  entries=self.b.entries()
  # At most64 identities, with multiple rounds to admit existing grandchildren.
  for _ in range(MAX_IDENTITIES):
   added=False
   for pid,parent in entries.items():
    if pid in self.owned or pid in self.retired or parent not in self.owned:continue
    owner=self.owned[parent];before=self.b.identity(parent)
    if before is None or before.birth100ns!=owner.birth100ns:continue
    child=self.b.identity(pid)
    if child is None or child.birth100ns<owner.birth100ns:continue
    # Verify current parent relationship again while the child handle remains live.
    current=self.b.entries()
    after=self.b.identity(parent);child_after=self.b.identity(pid)
    if current.get(pid)!=parent or after is None or child_after is None or after.birth100ns!=owner.birth100ns or child_after.birth100ns!=child.birth100ns:continue
    require(len(self.owned)<MAX_IDENTITIES,'owned identity budget exceeded')
    self.owned[pid]=Identity(pid,child.birth100ns,child.image,parent)
    ishost=normalized(child.image)==self.expected_host
    if ishost:require(self.hasher(child.image)==TESTHOST_EXE_SHA,'owned testhost image bytes mismatch');self.hosts[pid]=child.birth100ns
    self.admissions.append({'pid':pid,'birth100ns':child.birth100ns,'parentPid':parent,'parentBirth100ns':owner.birth100ns,'image':child.image,'parentChildParentVerified':True,'role':'verifiedPayloadTesthost' if ishost else 'ownedDescendant','imageSha256':TESTHOST_EXE_SHA if ishost else None})
    added=True
   if not added:break
 def sample(self,offset):
  self.discover();rows=[]
  for pid,ident in self.owned.items():
   if pid in self.retired:continue
   now=self.b.identity(pid)
   if now is None:self.retired.add(pid);continue
   if now.birth100ns!=ident.birth100ns:
    self.retired.add(pid);self.identityErrors.append('Previously owned PID changed birth without confirmed exit: '+str(pid));continue # Exclude reused identity and fail qualification.
   c=self.b.counters(ident)
   if c is None:continue # Process exited during read; its unread tail remains excluded.
   validate_counter(c);rows.append(dict(c,pid=pid,birth100ns=ident.birth100ns,isVerifiedTesthost=self.hosts.get(pid)==ident.birth100ns))
  machine=self.b.machine();validate_machine(machine,getattr(self.b,'candidate','namespace-4x8'))
  return {'offsetMilliseconds':offset,'processes':rows,'machine':machine,'sampledTreeWorkingSetSumBytes':sum(x['workingSetBytes'] for x in rows),'sampledTreePrivateCommitSumBytes':sum(x['privateCommitBytes'] for x in rows)}

def final_observations(backend,owned,observer):
 remaining=[];unknown=[];errors=[]
 for ident in owned:
  try:
   current=backend.identity(ident.pid)
   if current is not None:
    if current.birth100ns!=ident.birth100ns:raise OSError('Owned birth identity changed at stop')
    remaining.append({'pid':ident.pid,'birth100ns':ident.birth100ns})
  except Exception as failure:
   unknown.append({'pid':ident.pid,'birth100ns':ident.birth100ns,'error':type(failure).__name__+': '+str(failure)})
   errors.append('Final owned liveness unknown PID '+str(ident.pid)+': '+str(failure))
 try:
  own=backend.counters(observer)
  if own is None:raise OSError('Final live observer counters unavailable')
  validate_counter(own)
 except Exception as failure:
  own=None;errors.append('Final observer counters unknown: '+type(failure).__name__+': '+str(failure))
 return remaining,unknown,own,errors

def command_expected(results):return ['dotnet','test',DLL_RELATIVE,'--filter','FullyQualifiedName~ConnectionsLeafTests.TheModelOf','--logger','trx;LogFileName=model.trx','--results-directory',str(results),'--blame-hang-timeout','45m','--blame-hang-dump-type','mini']
def validate_report(root,run,attempt,harness,shard,expected_observer_sha=None,expected_physical_memory=None,candidate='namespace-4x8'):
 root=Path(root);meta=json.loads((root/'resource-summary.json').read_text());events=(root/'resource-events.ndjson').read_bytes()
 expected={'schemaVersion':1,'sourceRevision':SOURCE,'manifestSha256':MANIFEST_SHA,'executionRunId':run,'executionAttempt':attempt,'harnessRevision':harness,'shardIndex':shard,'shardCount':2,'executionCandidate':candidate,'observerComplete':True,'modelExitCode':0,'observationBudgetExhausted':False,'observerErrors':[],'remainingOwnedLiveProcessesAtStop':[],'unknownOwnedLivenessAtStop':[],'productCommandUnchanged':True}
 require(all(meta.get(k)==v for k,v in expected.items()),'resource identity/completion/model status mismatch')
 require(meta['eventsSha256']==hashlib.sha256(events).hexdigest() and meta['eventsBytes']==len(events),'resource event integrity mismatch')
 require(meta['actualProductArgv']==command_expected(meta['resultsDirectory']),'product argv changed')
 require(meta['productEnvironment']=={'SLATE_MODEL_SHARD_INDEX':str(shard),'SLATE_MODEL_SHARD_COUNT':'2','SLATE_MODEL_REPORT_DIR':meta['reportDirectory'],'SLATE_MODEL_ONLY':''},'product environment changed')
 raw=dict(meta['actualProductEnvironment']);require(raw.get('SLATE_MODEL_ONLY') in ('',None),'model-only flag changed');raw['SLATE_MODEL_ONLY']='';require(raw==meta['productEnvironment'] and meta['observerLocatorRemovedFromProductEnvironment'] is True,'actual product flags/observer locator changed')
 require(meta['observerIntervalSeconds']==5.0 and meta['observerMaxSeconds']==3600.0 and meta['observerMaxIdentities']==64,'observer bounds changed')
 require(meta['observerScriptSha256']==(expected_observer_sha or file_sha(__file__)),'observer script integrity witness missing/changed')
 rows=[json.loads(x) for x in events.splitlines()];require(len(rows)>=2 and len(rows)==meta['samplesWritten'],'missing/partial resource samples')
 admissions=meta['admissions'];require(1<=len(admissions)<=64 and admissions[0]['role']=='launchedDotnetRoot','missing actual launched root')
 known={};hosts=set()
 require(sum(x.get('role')=='launchedDotnetRoot' for x in admissions)==1,'multiple/unproved launched roots')
 capacity=meta['physicalCapacityBytes'];require(capacity_valid(capacity,candidate) and (expected_physical_memory is None or capacity==expected_physical_memory),'resource capacity differs from independently verified provenance')
 for x in admissions:
  ident=(x['pid'],x['birth100ns']);require(type(x['pid']) is int and x['pid']>0 and type(x['birth100ns']) is int and x['birth100ns']>0 and ident not in known,'invalid/duplicate owned identity')
  if x['role']!='launchedDotnetRoot':
   require(x['role'] in ('verifiedPayloadTesthost','ownedDescendant'),'unrecognized process role')
   parent=(x['parentPid'],x['parentBirth100ns']);require(parent in known and x['parentChildParentVerified'] is True and x['birth100ns']>=parent[1],'unproved descendant ownership')
  if x['role']=='verifiedPayloadTesthost':require(x['imageSha256']==TESTHOST_EXE_SHA and ntpath.normcase(ntpath.normpath(x['image']))==ntpath.normcase(ntpath.normpath(meta['expectedTesthostPath'])),'testhost path/hash witness changed');hosts.add(ident)
  known[ident]=x
 require(hosts,'actual owned payload testhost PID+birth witness missing')
 previous={};host_counts={h:0 for h in hosts};last=-1;previous_machine=None
 for row in rows:
  require(type(row['offsetMilliseconds']) in (int,float) and row['offsetMilliseconds']>last and 0<=row['offsetMilliseconds']<=3610*1000,'invalid sample timestamp');last=row['offsetMilliseconds'];validate_machine(row['machine'],candidate)
  machine=row['machine'];require(machine['totalPhysicalBytes']==capacity,'capacity changed within observation')
  if previous_machine is None:require(machine.get('busyFractionSincePrevious') is None,'first CPU fraction has no baseline')
  else:
   delta={k:machine[k]-previous_machine[k] for k in ('idle100ns','kernel100ns','user100ns')};den=delta['kernel100ns']+delta['user100ns'];require(min(delta.values())>=0 and den>0 and 0<=den-delta['idle100ns']<=den,'machine CPU counters/delta invalid');require(type(machine.get('busyFractionSincePrevious')) in (int,float) and abs(machine['busyFractionSincePrevious']-(den-delta['idle100ns'])/den)<=1e-12,'machine CPU fraction incorrect')
  previous_machine=machine
  seen=set()
  for x in row['processes']:
   ident=(x['pid'],x['birth100ns']);require(ident in known and ident not in seen,'unowned/duplicate sampled process');seen.add(ident);c={k:x[k] for k in ('workingSetBytes','peakWorkingSetBytes','privateCommitBytes','peakPrivateCommitBytes','kernel100ns','user100ns')};validate_counter(c);require(c['workingSetBytes']<=capacity,'current resident process counter exceeds physical capacity')
   if ident in previous:require(all(c[k]>=previous[ident][k] for k in ('peakWorkingSetBytes','peakPrivateCommitBytes','kernel100ns','user100ns')),'cumulative counters decreased')
   previous[ident]=c;require(x['isVerifiedTesthost']==(ident in hosts),'testhost flag differs from admitted identity')
   if ident in hosts:host_counts[ident]+=1
  require(row['sampledTreeWorkingSetSumBytes']==sum(x['workingSetBytes'] for x in row['processes']) and row['sampledTreePrivateCommitSumBytes']==sum(x['privateCommitBytes'] for x in row['processes']),'sampled tree sums changed')
 require(any(n>=2 for n in host_counts.values()),'actual model testhost not measured twice')
 require(type(meta['observerLoopWallSeconds']) in (int,float) and 0<=meta['observerLoopWallSeconds']<=meta['modelWallSeconds'] and meta['modelWallSeconds']>0 and meta['observerLoopWallSeconds']/meta['modelWallSeconds']<=.10,'observer wall-overhead qualification missing/excessive')
 return {'verified':True,'ownedIdentities':len(known),'verifiedTesthostIdentities':len(hosts),'samples':len(rows),'minimumSampledAvailablePhysicalBytes':min(x['machine']['availablePhysicalBytes'] for x in rows),'maximumSampledTreePrivateCommitBytes':max(x['sampledTreePrivateCommitSumBytes'] for x in rows),'peakWorkingSetByIdentity':{f'{pid}:{birth}':max(x['peakWorkingSetBytes'] for row in rows for x in row['processes'] if (x['pid'],x['birth100ns'])==(pid,birth)) for pid,birth in previous},'scope':'Observed process peak lower bounds and sequential within-pass tree sums; sampled minimum available physical is an upper bound on the actual minimum; no full-lifetime/machine peak or adoption'}

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--resources-root',required=True);ap.add_argument('--results-directory',required=True);ap.add_argument('--report-directory',required=True);ap.add_argument('--execution-run',required=True);ap.add_argument('--execution-attempt',required=True);ap.add_argument('--harness-revision',required=True);ap.add_argument('--shard',type=int,required=True);ap.add_argument('--candidate',choices=tuple(CANDIDATE_GIB),required=True);ap.add_argument('command',nargs=argparse.REMAINDER);args=ap.parse_args();cmd=args.command[1:] if args.command[:1]==['--'] else args.command
 require(args.execution_run.isdecimal() and args.execution_attempt.isdecimal() and re.fullmatch(r'[0-9a-f]{40}',args.harness_revision) and args.shard in (0,1),'execution inputs invalid');require(cmd==command_expected(args.results_directory),'product argv changed');raw_env={k:os.environ.get(k) for k in ('SLATE_MODEL_SHARD_INDEX','SLATE_MODEL_SHARD_COUNT','SLATE_MODEL_REPORT_DIR','SLATE_MODEL_ONLY')};env=dict(raw_env)
 if env['SLATE_MODEL_ONLY'] is None:env['SLATE_MODEL_ONLY']=''
 require(env=={'SLATE_MODEL_SHARD_INDEX':str(args.shard),'SLATE_MODEL_SHARD_COUNT':'2','SLATE_MODEL_REPORT_DIR':args.report_directory,'SLATE_MODEL_ONLY':''},'product env changed')
 require(file_sha('apps/slate-windows/pilot-binaries-manifest.json')==MANIFEST_SHA and file_sha(HOST_RELATIVE)==TESTHOST_EXE_SHA,'frozen manifest/testhost bytes changed')
 provenance=json.loads((Path(args.report_directory)/'consumer-provenance.txt').read_text(encoding='utf-8-sig'));capacity=provenance['physicalMemoryBytes'];require(provenance['sourceRevision']==SOURCE and provenance['executionRunId']==args.execution_run and provenance['executionAttempt']==args.execution_attempt and provenance['harnessRevision']==args.harness_revision and provenance['shardIndex']==args.shard and provenance['executionCandidate']==args.candidate and capacity_valid(capacity,args.candidate),'prior independently verified provenance differs')
 resource=Path(args.resources_root);resource.mkdir(parents=True,exist_ok=False);backend=NativeWindows(args.candidate);observer_identity=backend.identity(os.getpid());require(observer_identity is not None,'observer identity missing');observer_before=backend.counters(observer_identity);require(observer_before is not None,'observer own initial counters missing')
 dotnet=shutil.which('dotnet');require(dotnet is not None,'dotnet executable missing');errors=[];exhausted=False;written=0;loop_wall=0;start=time.monotonic();product_process_env=os.environ.copy();product_process_env.pop('SLATE_RESOURCE_OBSERVER_PYTHON',None);proc=subprocess.Popen(cmd,executable=dotnet,shell=False,env=product_process_env,creationflags=0);root=None
 try:
  root=backend.identity(proc.pid)
 except Exception as failure:errors.append('Initial launched-root identity unknown: '+type(failure).__name__+': '+str(failure))
 collector=OwnedCollector(backend,root,HOST_RELATIVE) if root is not None and proc.poll() is None else None
 if collector is None:errors.append('launched dotnet PID+birth witness missing')
 exit_seen=None;previous_machine=None;next_tick=start
 try:
  with (resource/'resource-events.ndjson').open('wb') as out:
   while collector is not None:
    now=time.monotonic();model_exit=proc.poll()
    if model_exit is not None and exit_seen is None:exit_seen=now
    if now-start>=MAX_OBSERVATION_SECONDS:exhausted=True;errors.append('observation budget exhausted');break
    begin=time.monotonic()
    try:
     row=collector.sample((begin-start)*1000)
     if collector.identityErrors:raise OSError('; '.join(collector.identityErrors))
     m=row['machine'];m['busyFractionSincePrevious']=None
     if previous_machine is not None:
      d={k:m[k]-previous_machine[k] for k in ('idle100ns','kernel100ns','user100ns')};den=d['kernel100ns']+d['user100ns'];require(min(d.values())>=0 and den>0 and 0<=den-d['idle100ns']<=den,'machine CPU delta invalid');m['busyFractionSincePrevious']=(den-d['idle100ns'])/den
     previous_machine=dict(m);out.write((json.dumps(row,sort_keys=True,separators=(',',':'))+'\n').encode());out.flush();written+=1
    except Exception as failure:errors.append(type(failure).__name__+': '+str(failure));break
    loop_wall+=time.monotonic()-begin
    if model_exit is not None:
     if not row['processes']:break # Counter rows omit only confirmed exits; final liveness is independently rechecked.
     if now-exit_seen>=TAIL_SECONDS:break
    next_tick+=INTERVAL_SECONDS;time.sleep(max(0,next_tick-time.monotonic()))
  # Observer never terminates a product process. An expired/failed observer stops
  # sampling, waits for the original command under the unchanged job deadline,
  # then makes resource qualification fail independently.
  model_exit=proc.wait();end=time.monotonic();remaining,unknown,observer_after,final_errors=final_observations(backend,collector.owned.values() if collector else [],observer_identity);errors.extend(final_errors);events=(resource/'resource-events.ndjson').read_bytes()
  meta={'schemaVersion':1,'sourceRevision':SOURCE,'manifestSha256':MANIFEST_SHA,'executionRunId':args.execution_run,'executionAttempt':args.execution_attempt,'harnessRevision':args.harness_revision,'shardIndex':args.shard,'shardCount':2,'executionCandidate':args.candidate,'observerScriptSha256':file_sha(__file__),'observerComplete':not exhausted and not errors and not remaining and not unknown,'modelExitCode':model_exit,'physicalCapacityBytes':capacity,'observationBudgetExhausted':exhausted,'observerErrors':errors,'remainingOwnedLiveProcessesAtStop':remaining,'unknownOwnedLivenessAtStop':unknown,'unprovedHandleOpenExclusions':backend.unprovedOpenExclusions,'unprovedHandleOpenExclusionCount':backend.unprovedOpenExclusionCount,'unprovedOpenExclusionsTruncated':backend.unprovedOpenExclusionCount>len(backend.unprovedOpenExclusions),'productCommandUnchanged':True,'actualProductArgv':cmd,'resolvedDotnetExecutable':dotnet,'resultsDirectory':args.results_directory,'reportDirectory':args.report_directory,'productEnvironment':env,'actualProductEnvironment':raw_env,'observerLocatorRemovedFromProductEnvironment':'SLATE_RESOURCE_OBSERVER_PYTHON' not in product_process_env,'observerPythonVersion':sys.version,'observerPythonExecutable':sys.executable,'observerPythonExecutableSha256':file_sha(sys.executable),'rootOwnershipWitness':'Popen-created PID remains live under Popen.poll after independent GetProcessTimes birth read','expectedTesthostPath':str(Path(HOST_RELATIVE).resolve()),'observerIntervalSeconds':INTERVAL_SECONDS,'observerMaxSeconds':MAX_OBSERVATION_SECONDS,'observerMaxIdentities':MAX_IDENTITIES,'samplesWritten':written,'eventsSha256':hashlib.sha256(events).hexdigest(),'eventsBytes':len(events),'admissions':collector.admissions if collector else [],'modelWallSeconds':(exit_seen or end)-start,'modelWallTimingQualification':'Observed root completion includes up to one5-second polling interval; use TRX/canonical family timings for product latency','observationWallSeconds':end-start,'observerLoopWallSeconds':loop_wall,'observerOwnCpuSeconds':((observer_after['kernel100ns']+observer_after['user100ns'])-(observer_before['kernel100ns']+observer_before['user100ns']))/10_000_000 if observer_after else None,'observerOwnObservedPeakWorkingSetBytes':observer_after['peakWorkingSetBytes'] if observer_after else None,'qualifications':['Observed process peaks are lower bounds on full lifetime if an unread tail remains.','Short-lived children between snapshots are excluded; parent PID alone never admits them.','Previously admitted still-same-birth children remain included if their parent exits; new children without live parent proof are excluded.','Sampled working-set sums can count shared pages multiple times; privateCommit is not swap/diskIO or resident private bytes.','Machine available physical is sampled; minimum is an upper bound on the actual lifetime minimum; unobserved short-lived process count is unknown.','Read-only observer affects allocation load; no causal RAM-pressure/latency/adoption conclusion.','No observer process termination, injection, priority, global timer, security or cache changes.']}
  (resource/'resource-summary.json').write_text(json.dumps(meta,indent=2)+'\n')
  try:validate_report(resource,args.execution_run,args.execution_attempt,args.harness_revision,args.shard,expected_physical_memory=capacity,candidate=args.candidate)
  except Exception as failure:print('Resource qualification failed:',failure,file=sys.stderr);return model_exit if model_exit else 1
  return model_exit
 finally:backend.close()
if __name__=='__main__':raise SystemExit(main())

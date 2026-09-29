# WineTestRunner

A throwaway harness that runs the Windows unit facts on a Linux machine under
Wine. It was built to investigate #1285 in a cloud session with no Windows
box, and it is kept so that investigation can be reproduced. It is not in
`SlateWindows.slnx`, CI never builds it, and it ships in no build.

The runner is a self-contained win-x64 exe that references
`SlateWindows.Tests`, so the unit assembly and every dependency, including the
WPF runtime, sit beside it. It drives xunit 2.9.3's own execution engine
in-process (`AssemblyRunner`) for one class, or one fact of that class. Each
invocation is a fresh Windows process, so a loop of invocations is a loop of
isolated runs.

## Fidelity limits

- **Wine is not Windows.** Scheduling, file I/O and timer resolution differ.
  A green loop here is evidence, not a substitute for a `dotnet test` loop on
  Windows.
- **Workspace persistence fails under Wine.** `AnchoredVaultStore.Open` does
  handle-relative opens that Wine does not support, so every
  `WorkspaceViewModel.PersistCore` logs `WorkspacePersistFailed (IOException)`
  and returns at once. On Windows that write takes real time inside every
  `OpenPath`, before the panels change note. A race that depends on the
  length of `OpenPath` is therefore much harder to hit here:
  `probes/slow-persist.patch` puts that time back for an experiment.
- **One class per run.** The runner does not run the whole unit project, and
  facts that need a desktop session, FlaUI or real window activation have not
  been tried.
- **xunit semantics match `dotnet test`.** The same engine runs the tests with
  the same `AsyncTestSyncContext`, whose `Post` hands callbacks to the thread
  pool, and the same fact order within a class.

## One-time setup (Ubuntu 24.04)

```bash
sudo apt-get install -y wine64 wine mingw-w64 xvfb
rustup target add x86_64-pc-windows-gnu
cargo install --git https://github.com/NordSecurity/uniffi-bindgen-cs \
  --tag v0.11.0+v0.31.0 uniffi-bindgen-cs --locked
```

The .NET 10 SDK comes from Microsoft's Debian 12 repository, extracted rather
than installed. Its 10.0.4xx band satisfies `apps/slate-windows/global.json`
(10.0.302, `rollForward: latestFeature`).

```bash
base=https://packages.microsoft.com/debian/12/prod
curl -sS "$base/dists/bookworm/main/binary-amd64/Packages.gz" | gunzip > /tmp/pmc-Packages
mkdir -p /tmp/dotnet-x
for p in dotnet-sdk-10.0 dotnet-runtime-10.0 aspnetcore-runtime-10.0 dotnet-host \
         dotnet-hostfxr-10.0 dotnet-targeting-pack-10.0 aspnetcore-targeting-pack-10.0 \
         dotnet-apphost-pack-10.0 netstandard-targeting-pack-2.1; do
  f=$(awk -v p="$p" '$1=="Package:"{keep=($2==p)} keep&&$1=="Filename:"{print $2}' \
      /tmp/pmc-Packages | sort -V | tail -1)
  curl -sS -o "/tmp/$p.deb" "$base/$f" && dpkg-deb -x "/tmp/$p.deb" /tmp/dotnet-x
done
sudo mv /tmp/dotnet-x/usr/share/dotnet /opt/dotnet
export DOTNET_ROOT=/opt/dotnet PATH=/opt/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Initialize a Wine prefix once. Disabling `mscoree` here only skips the Mono
install prompt during setup; at run time it must stay enabled, because Wine's
loader needs it to map IL-only assemblies (`run-loop.sh` sets this).

```bash
export WINEPREFIX=$HOME/.wine-slate WINEDEBUG=-all
WINEDLLOVERRIDES="mscoree=;mshtml=" xvfb-run -a wineboot -i
```

## Build

From the repo root, cross-compile the core, then generate the bindings from
that DLL, as `generate-bindings.ps1` does on Windows. The generator's warning
about CSharpier is harmless.

```bash
cargo build -p slate-uniffi --release --locked --target x86_64-pc-windows-gnu
g=apps/slate-windows/src/SlateUniffi/generated; mkdir -p "$g"
uniffi-bindgen-cs --library target/x86_64-pc-windows-gnu/release/slate_uniffi.dll \
  --out-dir "$g" --config apps/slate-windows/uniffi.toml
cp target/x86_64-pc-windows-gnu/release/slate_uniffi.dll "$g/"

dotnet build apps/slate-windows/tools/WineTestRunner/WineTestRunner.csproj \
  -c Release -p:EnableWindowsTargeting=true
```

`EnableWindowsTargeting` must be a global property so that it reaches every
referenced `net10.0-windows` project. The output lands in
`bin/Release/net10.0-windows/win-x64/`, inside the repo, which the unit facts
need: they find the repo root by walking up to `demo-vault`.

## Run

```bash
Xvfb :77 -screen 0 1280x1024x24 &   # once per machine session
WINEPREFIX=$HOME/.wine-slate wineserver -p   # optional: keeps Wine warm between runs
apps/slate-windows/tools/WineTestRunner/run-loop.sh 50 /tmp/runs \
  SlateWindows.Tests.CitationAsyncInterleavingTests
```

Pass a fact name as a fourth argument to run one fact. Each run writes
`run-N.log` with a `PASS`/`FAIL` line per fact and a `RESULT` line. The loop
prints one line per run, then `TOTAL`, and exits non-zero when any run
failed. One run of `CitationAsyncInterleavingTests` takes about 33 seconds.

## Reproducing #1285's evidence

- **The probes.** Copy `probes/CitationSummaryInterleavingProbe.cs` into
  `apps/slate-windows/tests/SlateWindows.Tests/`, rebuild, and run
  `SlateWindows.Tests.CitationSummaryInterleavingProbe`. The file's header
  lists the expected result of each fact; two fail by design. Delete the copy
  afterwards.
- **Windows-like `OpenPath` timing.** Apply the patch (`git apply
  apps/slate-windows/tools/WineTestRunner/probes/slow-persist.patch`), rebuild,
  and loop `OriginalTestVerbatim` against the fixed
  `ADeferredSummaryDoesNotAnswerForADifferentNote`. Revert the patch with
  `git checkout -- apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Persistence.cs`.

## Recorded results (2026-09-29)

| Run | Code under test | Result |
| --- | --- | --- |
| Class, 10 runs, quiet | pre-fix | 10 of 10 passed |
| Class, 20 runs, CPU stress (12 workers on 4 cores) | pre-fix | 20 of 20 passed |
| `OriginalTestVerbatim`, 20 runs, slow-persist patch | pre-fix | 10 failed: 8 at the final `Assert.Null` with cited.md's counts, 2 at `Assert.True(IsLoading)` |
| Both held facts, 30 runs each, slow-persist patch | fixed (21cfc8e) | 30 of 30 each |
| Class, 50 consecutive runs, quiet | fixed (21cfc8e) | 50 of 50 passed (15 of 15 facts each) |

Across four runs, the timing probe put the park-to-move window at 2 to 12 ms,
ending 40 to 110 ms into the fact, while the seed settled 217 to 375 ms in.
That is why the pre-fix fact never failed here without the patch.

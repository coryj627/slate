# WB-1 — The test suites and the field passes stop writing the developer's `%LOCALAPPDATA%\Slate`

Issues: #1331. Size M. Lanes: windows (unit project and the FlaUI shell gate). AT: — (no speech changes). Wave: W1, slot 1. Depends on: nothing. Parallel with: WB-2, WB-3 (no shared files). Decision: none.

## Goal

A developer who runs `dotnet test` on either Windows project, or launches the app for an NVDA field pass, finds their own recent vaults, window placement, preferences, palette recents, per-vault file recents and sidebar write journal exactly as they left them. One device-state root, `SlateDeviceState.Root`, honours `SLATE_LOCAL_APP_DATA` the way `HostLog.LogDirectory` already honours `SLATE_LOG_DIR`; every store resolves through it; the test hosts and the shell gate point it at a per-run temporary root; a wrapper proves, after the whole run and its child processes have ended, that the real profile did not change; and a launch script seeds such a root for field passes (plan §4 rule 4). Production behaviour with the variable unset is byte-for-byte unchanged.

## Current state on main

- Six stores resolve `Environment.SpecialFolder.LocalApplicationData` themselves (the issue counted five; `SidebarWriteJournal` is the sixth, added since filing): `RecentVaultsStore.cs:69-72` (`DefaultFilePath`), `WindowStateStore.cs:153-156`, `AppPreferencesStore.cs:61-64`, `Commands/CommandPaletteRecentsStore.cs:56-59` (a `{ get; }` initialised once at type load, not an expression property), `FileRecentsStore.cs:32-35` (`localAppDataRoot ?? Path.Combine(LocalApplicationData, "Slate", "file-recents")`), `SidebarWriteJournal.cs:300-303` (`ProductionDirectory`, installed by `App.xaml.cs:138` `InstallProductionLocation()`; a test process keeps a private temp folder, `:296-298`). All paths are under `apps/slate-windows/src/SlateWindows/`.
- `HostLog.cs:112-116`: `SLATE_LOG_DIR` else `%LOCALAPPDATA%\Slate\logs`. `Reading/ReadingSurface.cs:739` reads `SLATE_LOG_DIR` only to decide whether to write UIA diagnostics — unchanged by this chunk.
- Production constructions that take the default path: `VaultLifecycleViewModel.cs:222` (`new RecentVaultsStore()`), `:1191` (`new AppPreferencesStore()`), `WindowPlacementManager.cs:22` (`new WindowStateStore()`), `Commands/PaletteCommandSource.cs:46` (`new CommandPaletteRecentsStore()`); `FilesSidebarViewModel.cs:528,557` and `QuickSwitcherViewModel.cs:56,68` build `FileRecentsStore(vaultRoot, session.RootIdentity(), localAppDataRoot)`.
- The shell gate launches the real exe 36 times with only `SLATE_LOG_DIR` set: `ShellAccessibilityTests.cs:116-137` (`StartShellProcess`) and the inline `ProcessStartInfo` sites (31 in `ShellAccessibilityTests.cs`, 3 in `.Rescan.cs`, 1 each in `.Palette.cs` and `.SidebarSettings.cs`; `grep -c 'startInfo.Environment\["SLATE_LOG_DIR"\]'`). `ShellAccessibilityTests.Announcements.cs:59-66` and `ShellAccessibilityTests.cs:938` document that recents are device-wide. `windows.yml:845-854` sets only `SLATE_REQUIRE_UI_AUTOMATION` and the evidence dir for the gate job; the unit lane's `Test` step is `:527-534`.
- Bare unit constructions whose file recents land in the real profile: `MoveToLoadingTests.cs:291` (`new FilesSidebarViewModel(session, announcements.Enqueue, treeUiContext: context)`), `SidebarAnnouncementEtiquetteTests.cs:26` and `:119`, `SidebarTagFilterTests.cs:351` (`ControlledSidebar`). `QuickSwitcherRankCoordinatorTests.cs:352-360` already passes a `device-state-…` path (the issue listed it; it is fixed).
- Pins that will move: `CommandRegistrationTests.cs:470-478` `DefaultRecentsPath_IsTheGlobalDeviceLocalLocation` and `SidebarWriteJournalTests.cs:700-710` `ATestProcessNeverJournalsIntoTheUsersAppData` both compute the expected path from `LocalApplicationData` directly.
- No `SlateDeviceState` type exists; `scripts/windows/` does not exist (`scripts/` holds the Python and shell tools). `.NET`'s `GetFolderPath(LocalApplicationData)` reads the known-folder API, not the `LOCALAPPDATA` variable, so a profile cannot be redirected by environment for a negative test; a surrogate folder must be passed explicitly.

## Reference

None; the issue's text is the behavioural spec. The precedent is `HostLog.LogDirectory` (`HostLog.cs:109-116`): an environment override read at use, else the profile path.

## Design

- **New** `apps/slate-windows/src/SlateWindows/SlateDeviceState.cs`, `internal static class SlateDeviceState`: `internal const string Variable = "SLATE_LOCAL_APP_DATA"`; `internal static string Resolve(string? overrideValue)` (pure: a non-empty value → `Path.GetFullPath(value)`, else `Path.Combine(LocalApplicationData, "Slate")`); `internal static string Root => Resolve(Environment.GetEnvironmentVariable(Variable))`, read at each use (cheap; a store captures its path at construction as today).
- The six stores compose from `SlateDeviceState.Root`: `recent-vaults.json`, `window-state.json`, `preferences.json`, `command-palette-recents.json` (the `{ get; }` at `CommandPaletteRecentsStore.cs:56` becomes `=>`), `file-recents\`, `sidebar-write-journal`. `FileRecentsStore` keeps its `localAppDataRoot` parameter (callers that pass one win). `HostLog.LogDirectory` becomes `SLATE_LOG_DIR ?? Path.Combine(SlateDeviceState.Root, "logs")`.
- **Test hosts.** `SlateWindows.Tests` gets a `[ModuleInitializer]` in **new** `tests/SlateWindows.Tests/Support/DeviceStateForTests.cs` that, unless `SLATE_LOCAL_APP_DATA` is already set, creates `%TEMP%\slate-tests-<pid>-<guid>` and sets the variable for the process (`DeviceStateForTests.Root`). The AccessibilityTests project gets the same in **new** `tests/SlateWindows.AccessibilityTests/ShellLaunch.cs`, created lazily by `ShellLaunch.ApplyDeviceState(ProcessStartInfo)` so VSTest assembly mode (`windows.yml:845-848`, no project file) is covered without relying on a module initialiser; `StartShellProcess` (`:116-137`) and the 35 inline sites call it. Journeys that depend on recents (`Announcements.cs:57-66`, `ShellAccessibilityTests.cs:938`) keep passing — the per-run root starts empty, so a unique name reads bare, which is what they assert; their comments say the root is the run's own.
- **The whole-run guard is outside the process.** **New** `scripts/windows/guard-device-state.ps1 -Profile <folder> -- <command…>`: snapshots `<folder>` recursively (relative path, length, last-write ticks), runs the command (any `dotnet test …`, including the gate dll), waits for it and every child it spawned to exit (the shell gate's `SlateWindows.exe` processes are children of the test host; the script also waits on any surviving `SlateWindows.exe` whose command line names `DeviceStateForTests.Root`), snapshots again and exits 1 listing every created, changed or removed file. `-Profile` defaults to `%LOCALAPPDATA%\Slate`. `windows.yml`'s unit `Test` step (`:527-534`) and the gate step (`:845-854`) run through it, so CI proves every run, not one fact. An in-process `DeviceStateGuardTests` fact still exists as the fast local signal, but the wrapper is the witness.
- The four bare constructions pass `localAppDataRoot: fixture-local path` (the `QuickSwitcherRankCoordinatorTests.cs:356` shape).
- **Pins rewritten:** `DefaultRecentsPath_IsTheGlobalDeviceLocalLocation` asserts `CommandPaletteRecentsStore.DefaultFilePath == Path.Combine(SlateDeviceState.Root, "command-palette-recents.json")` and gains a sibling over `Resolve(null)` for the unset branch; `ATestProcessNeverJournalsIntoTheUsersAppData` asserts `ProductionDirectory` is under `SlateDeviceState.Root` and not under the real profile when the override is set.
- **Field-pass launcher**, **new** `scripts/windows/launch-for-field-pass.ps1`: `param([string]$Vault, [switch]$Seed, [switch]$Keep, [string]$Exe)`; creates `$env:TEMP\slate-field-pass-<yyyyMMdd-HHmmss>`, sets `SLATE_LOCAL_APP_DATA` and `SLATE_LOG_DIR` beneath it, with `-Seed` writes a `recent-vaults.json` holding `$Vault` (the `RecentVaultsStore` schema: path, display name, last-opened), starts `$Exe` (default `apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows/SlateWindows.exe`, the layout `windows.yml:848` uses for the gate dll) with `$Vault` as the argument, prints the root, and on exit deletes it unless `-Keep`.
- Deliberately not changed: `ReadingSurface.cs:739`, the stores' file formats and names, `InstallProductionLocation`'s call site, production defaults when the variable is unset.

## Tests

- `SlateDeviceStateTests.ResolveWithoutAnOverrideIsTheProfilesSlateFolder` — `Resolve(null)` and `Resolve("")` equal `%LOCALAPPDATA%\Slate`. Mutation: drop the empty-string branch.
- `SlateDeviceStateTests.ResolveWithAnOverrideIsThatFolderMadeAbsolute` — a relative override is made absolute. Mutation: return the value unfullpathed.
- `SlateDeviceStateTests.EveryStoreDefaultIsUnderTheRoot` — the six default paths and `HostLog.LogDirectory` (with `SLATE_LOG_DIR` unset) start with `SlateDeviceState.Root`. Mutation: revert any one store to `LocalApplicationData`.
- `DeviceStateGuardTests.TheRealLocalAppDataIsUntouchedSoFar` — compares the module initialiser's snapshot of the real profile with a fresh one at the fact's run time (a partial, fast signal; the wrapper is the whole-run witness and the fact's summary says so).
- `DeviceStateCensus.EveryTestConstructionOfARecentsOwnerPassesItsRoot` (Roslyn, over `tests/**/*.cs`): every `new FilesSidebarViewModel(` / `new QuickSwitcherViewModel(` passes `localAppDataRoot:` or a positional device-state argument; every `new RecentVaultsStore(` / `WindowStateStore(` / `AppPreferencesStore(` / `CommandPaletteRecentsStore(` in tests passes a path. Mutation: remove the argument at `MoveToLoadingTests.cs:291`.
- `ShellLaunchCensus.EveryShellLaunchAppliesTheDeviceState` (AccessibilityTests, Roslyn): every `new ProcessStartInfo(SlateWindowsExe())` is followed by `ShellLaunch.ApplyDeviceState(startInfo)` or is inside `StartShellProcess`. Mutation: delete the call at one inline site.
- `scripts/tests/test_guard_device_state.py` (new): the wrapper's comparator against a **surrogate** profile — `guard-device-state.ps1 -Profile <surrogate> -- pwsh -c "Set-Content <surrogate>\recent-vaults.json x"` exits 1 naming the file; with a no-op command it exits 0. Nothing in the negative run touches the real profile. Mutation: make the wrapper skip the second snapshot.
- FlaUI journey (gate, `[Trait("gate","W-C")]`): `ShellGate_WritesItsRecentsUnderTheRunsOwnRoot` — launch through `StartShellProcess`, wait for the main window, close it, assert `recent-vaults.json` appeared under `DeviceStateForTests.Root`. Mutation: launch without the variable (asserted against the surrogate root, not the profile).
- Facts fail first (§4 rule 1): write the census, the pins and the wrapper's negative run before touching the stores.

## Docs and registers

- `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md`: the launch recipe paragraph (the script, `-Seed`, `-Keep`), replacing "back up `%LOCALAPPDATA%\Slate` first".
- Wherever `SLATE_LOG_DIR` is documented for developers (`grep -rn SLATE_LOG_DIR apps/slate-windows/*.md docs/plans/18_windows_port/*.md`): one row for `SLATE_LOCAL_APP_DATA` and one for the wrapper.
- `.github/workflows/windows.yml:527-534` and `:845-854`: both test invocations run under `scripts/windows/guard-device-state.ps1`.
- Comments at `ShellAccessibilityTests.Announcements.cs:59-66` and `ShellAccessibilityTests.cs:938`.
- No contract rows, matrices, designations or ledgers.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb1'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$gate  = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$unit  = 'FullyQualifiedName~SlateDeviceStateTests|FullyQualifiedName~DeviceStateGuardTests|FullyQualifiedName~DeviceStateCensus|FullyQualifiedName~CommandRegistrationTests.DefaultRecentsPath|FullyQualifiedName~SidebarWriteJournalTests.ATestProcess'
$n = (dotnet test $tests -c Release --no-build --list-tests --filter $unit | Select-String '^\s{4}\S').Count; if ($n -lt 7) { throw "filter selects $n facts" }
dotnet test $tests -c Release --no-build --filter $unit
# the whole-run witness, both projects, real profile:
pwsh scripts/windows/guard-device-state.ps1 -- dotnet test $tests -c Release --no-build
pwsh scripts/windows/guard-device-state.ps1 -- dotnet test $gate -c Release --no-build --filter "FullyQualifiedName~ShellGate_WritesItsRecents|FullyQualifiedName~ShellLaunchCensus"   # NVDA closed, desktop free
python -I -m unittest discover -s scripts/tests -p "test_guard_device_state.py"
$changed = @(git diff --name-only HEAD; git ls-files --others --exclude-standard) | Where-Object { $_ -like '*.cs' }
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $changed
pwsh scripts/windows/launch-for-field-pass.ps1 -Vault C:\path\to\a\scratch\vault -Seed -Keep   # prints the root; recent-vaults.json is there, the real one unchanged
```

Touched C# files (the `--include` set): `src/SlateWindows/SlateDeviceState.cs`, `RecentVaultsStore.cs`, `WindowStateStore.cs`, `AppPreferencesStore.cs`, `Commands/CommandPaletteRecentsStore.cs`, `FileRecentsStore.cs`, `SidebarWriteJournal.cs`, `HostLog.cs`; `tests/SlateWindows.Tests/Support/DeviceStateForTests.cs`, `SlateDeviceStateTests.cs`, `DeviceStateGuardTests.cs`, `Censuses/DeviceStateCensus.cs`, `MoveToLoadingTests.cs`, `SidebarAnnouncementEtiquetteTests.cs`, `SidebarTagFilterTests.cs`, `CommandRegistrationTests.cs`, `SidebarWriteJournalTests.cs`; `tests/SlateWindows.AccessibilityTests/ShellLaunch.cs`, `ShellLaunchCensus.cs`, `ShellAccessibilityTests.cs`, `.Rescan.cs`, `.Palette.cs`, `.SidebarSettings.cs`, `.Announcements.cs`.

CI lanes: `windows.yml` (unit, model shards, FlaUI gate — the two test steps wrapped). AT pass: none — no speech changes.

## Acceptance

- [ ] The eight facts and two censuses above, each seen failing first or under its mutation; the wrapper's negative run against a surrogate exits 1.
- [ ] `grep -rn "SpecialFolder.LocalApplicationData" apps/slate-windows/src` returns only `SlateDeviceState.cs`.
- [ ] Every shell-gate launch carries `SLATE_LOCAL_APP_DATA`; both CI test steps run under the wrapper and are green.
- [ ] `scripts/windows/launch-for-field-pass.ps1` exists and the NVDA recipe names it.
- [ ] `Closes #1331`.
- [ ] Not delivered: nothing expected.

## Brief

Read `apps/slate-windows/src/SlateWindows/HostLog.cs:105-120`; the six store sites `RecentVaultsStore.cs:69-72`, `WindowStateStore.cs:153-156`, `AppPreferencesStore.cs:61-64`, `Commands/CommandPaletteRecentsStore.cs:56-59`, `FileRecentsStore.cs:20-40`, `SidebarWriteJournal.cs:296-320`; `App.xaml.cs:130-140`; `apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:100-140` and `:938`, `.Announcements.cs:55-66`; the bare constructions `MoveToLoadingTests.cs:291`, `SidebarAnnouncementEtiquetteTests.cs:26` and `:119`, `SidebarTagFilterTests.cs:351`; the pins `CommandRegistrationTests.cs:470-478`, `SidebarWriteJournalTests.cs:700-710`; `.github/workflows/windows.yml:527-534` and `:845-854`. Build: `SlateDeviceState` (`Variable`, `Resolve(string?)`, `Root`) and route the six stores and `HostLog.LogDirectory` through it; `Support/DeviceStateForTests.cs` (module initialiser: per-run `%TEMP%` root, variable set, profile snapshot); `ShellLaunch.ApplyDeviceState(ProcessStartInfo)` applied at `StartShellProcess` and all 35 inline launches; `localAppDataRoot:` at the four bare constructions; the two pins rewritten to compare against `SlateDeviceState.Root`; `scripts/windows/guard-device-state.ps1 -Profile <folder> -- <command>` (snapshot, run, wait for children, snapshot, exit 1 on any difference) wired into `windows.yml`'s two test steps; `scripts/windows/launch-for-field-pass.ps1 -Vault -Seed -Keep -Exe`. Facts: `SlateDeviceStateTests.ResolveWithoutAnOverrideIsTheProfilesSlateFolder` (null/empty → `%LOCALAPPDATA%\Slate`), `…ResolveWithAnOverrideIsThatFolderMadeAbsolute`, `…EveryStoreDefaultIsUnderTheRoot` (six paths + logs start with `Root`), `DeviceStateGuardTests.TheRealLocalAppDataIsUntouchedSoFar` (snapshot diff empty at run time), `DeviceStateCensus.EveryTestConstructionOfARecentsOwnerPassesItsRoot` (Roslyn over the test sources), `ShellLaunchCensus.EveryShellLaunchAppliesTheDeviceState`, `scripts/tests/test_guard_device_state.py` (the wrapper exits 1 for a write into a surrogate profile, 0 for none), gate journey `ShellGate_WritesItsRecentsUnderTheRunsOwnRoot`. Mutations: a store reverted to `LocalApplicationData`; the argument removed at `MoveToLoadingTests.cs:291`; one inline launch without `ApplyDeviceState`; the wrapper skipping its second snapshot — each must fail its fact. Registers: the NVDA recipe in `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md` names the launcher; the developer doc row for `SLATE_LOCAL_APP_DATA`. No decisions pending; nothing deferred. Done when `grep -rn SpecialFolder.LocalApplicationData apps/slate-windows/src` hits only `SlateDeviceState.cs`, both projects run clean under the wrapper against the real profile, `dotnet format --verify-no-changes --include <changed .cs>` is clean, and `windows.yml` is green with the wrapped steps. `Closes #1331`. Commit: `test(windows): the test suites and field passes write device state under SLATE_LOCAL_APP_DATA, never the developer's profile`.

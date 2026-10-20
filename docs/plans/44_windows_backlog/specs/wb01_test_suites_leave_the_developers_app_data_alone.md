# WB-1 — The test suites and the field passes stop writing the developer's `%LOCALAPPDATA%\Slate`

Issues: #1331. Size M. Lanes: windows (unit project and the FlaUI shell gate). AT: — (no speech changes). Wave: W1, slot 1. Depends on: nothing. Parallel with: WB-2, WB-3 (no shared files). Decision: none.

## Goal

A developer who runs `dotnet test` on either Windows project, or launches the app for an NVDA field pass, finds their own recent vaults, window placement, preferences, palette recents, per-vault file recents and sidebar write journal exactly as they left them. One device-state root, `SlateDeviceState.Root`, honours `SLATE_LOCAL_APP_DATA` the way `HostLog.LogDirectory` already honours `SLATE_LOG_DIR`; every store resolves through it; the test hosts and the shell gate point it at a per-run temporary root; and a launch script seeds such a root for field passes (plan §4 rule 4). Production behaviour with the variable unset is byte-for-byte unchanged.

## Current state on main

- Six stores resolve `Environment.SpecialFolder.LocalApplicationData` themselves (the issue counted five; `SidebarWriteJournal` is the sixth, added since filing): `RecentVaultsStore.cs:69-72` (`DefaultFilePath`), `WindowStateStore.cs:153-156`, `AppPreferencesStore.cs:61-64`, `Commands/CommandPaletteRecentsStore.cs:56-59` (a `{ get; }` initialised once at type load, not an expression property), `FileRecentsStore.cs:32-35` (`localAppDataRoot ?? Path.Combine(LocalApplicationData, "Slate", "file-recents")`), `SidebarWriteJournal.cs:300-303` (`ProductionDirectory`, installed by `App.xaml.cs:138` `InstallProductionLocation()`; a test process keeps a private temp folder, `:296-298`). All paths are under `apps/slate-windows/src/SlateWindows/`.
- `HostLog.cs:112-116`: `SLATE_LOG_DIR` else `%LOCALAPPDATA%\Slate\logs`. `Reading/ReadingSurface.cs:739` reads `SLATE_LOG_DIR` only to decide whether to write UIA diagnostics — unchanged by this chunk.
- Production constructions that take the default path: `VaultLifecycleViewModel.cs:222` (`new RecentVaultsStore()`), `:1191` (`new AppPreferencesStore()`), `WindowPlacementManager.cs:22` (`new WindowStateStore()`), `Commands/PaletteCommandSource.cs:46` (`new CommandPaletteRecentsStore()`); `FilesSidebarViewModel.cs:528,557` and `QuickSwitcherViewModel.cs:56,68` build `FileRecentsStore(vaultRoot, session.RootIdentity(), localAppDataRoot)`.
- The shell gate launches the real exe 36 times with only `SLATE_LOG_DIR` set: `ShellAccessibilityTests.cs:116-137` (`StartShellProcess`) and the inline `ProcessStartInfo` sites (31 in `ShellAccessibilityTests.cs`, 3 in `.Rescan.cs`, 1 each in `.Palette.cs` and `.SidebarSettings.cs`; `grep -c 'startInfo.Environment\["SLATE_LOG_DIR"\]'`). `ShellAccessibilityTests.Announcements.cs:59-66` and `ShellAccessibilityTests.cs:938` document that recents are device-wide. `windows.yml:845-854` sets only `SLATE_REQUIRE_UI_AUTOMATION` and the evidence dir for the gate job.
- Bare unit constructions whose file recents land in the real profile: `MoveToLoadingTests.cs:291` (`new FilesSidebarViewModel(session, announcements.Enqueue, treeUiContext: context)`), `SidebarAnnouncementEtiquetteTests.cs:26` and `:119`, `SidebarTagFilterTests.cs:351` (`ControlledSidebar`). `QuickSwitcherRankCoordinatorTests.cs:352-360` already passes a `device-state-…` path (the issue listed it; it is fixed).
- Pins that will move: `CommandRegistrationTests.cs:470-478` `DefaultRecentsPath_IsTheGlobalDeviceLocalLocation` and `SidebarWriteJournalTests.cs:700-710` `ATestProcessNeverJournalsIntoTheUsersAppData` both compute the expected path from `LocalApplicationData` directly.
- No `SlateDeviceState` type exists; `scripts/windows/` does not exist (`scripts/` holds the Python and shell tools).

## Reference

None; the issue's text is the behavioural spec. The precedent is `HostLog.LogDirectory` (`HostLog.cs:109-116`): an environment override read at use, else the profile path.

## Design

- **New** `apps/slate-windows/src/SlateWindows/SlateDeviceState.cs`, `internal static class SlateDeviceState`: `internal const string Variable = "SLATE_LOCAL_APP_DATA"`; `internal static string Resolve(string? overrideValue)` (pure: a non-empty value → `Path.GetFullPath(value)`, else `Path.Combine(LocalApplicationData, "Slate")`); `internal static string Root => Resolve(Environment.GetEnvironmentVariable(Variable))`, read at each use (cheap; a store captures its path at construction as today).
- The six stores compose from `SlateDeviceState.Root`: `recent-vaults.json`, `window-state.json`, `preferences.json`, `command-palette-recents.json` (the `{ get; }` at `CommandPaletteRecentsStore.cs:56` becomes `=>`), `file-recents\`, `sidebar-write-journal`. `FileRecentsStore` keeps its `localAppDataRoot` parameter (callers that pass one win). `HostLog.LogDirectory` becomes `SLATE_LOG_DIR ?? Path.Combine(SlateDeviceState.Root, "logs")`.
- **Test hosts.** `SlateWindows.Tests` gets a `[ModuleInitializer]` (`Support/DeviceStateForTests.cs`, new) that, unless `SLATE_LOCAL_APP_DATA` is already set, creates `%TEMP%\slate-tests-<pid>-<guid>` and sets the variable for the process, snapshots the real `%LOCALAPPDATA%\Slate` (relative path, length, last-write ticks) into `DeviceStateForTests.Snapshot`, and registers `AppDomain.ProcessExit` cleanup. The AccessibilityTests project does the same for its own process and passes the variable into every launched exe: `StartShellProcess` (`:116-137`) and the 35 inline sites set `startInfo.Environment["SLATE_LOCAL_APP_DATA"] = DeviceStateForTests.Root` (one helper, `ShellLaunch.ApplyDeviceState(startInfo)`, so the next inline launch cannot forget it; a census pins it, below). Journeys that depend on recents (`Announcements.cs:57-66`, `ShellAccessibilityTests.cs:938`) keep passing — the per-run root starts empty, so a unique name reads bare, which is what they assert; their comments are updated to say the root is the run's own.
- The four bare constructions pass `localAppDataRoot: fixture-local path` (the `QuickSwitcherRankCoordinatorTests.cs:356` shape).
- **Pins rewritten:** `DefaultRecentsPath_IsTheGlobalDeviceLocalLocation` asserts `CommandPaletteRecentsStore.DefaultFilePath == Path.Combine(SlateDeviceState.Root, "command-palette-recents.json")` and gains a sibling over `Resolve(null)` for the unset branch; `ATestProcessNeverJournalsIntoTheUsersAppData` asserts `ProductionDirectory` is under `SlateDeviceState.Root` and not under the real profile when the override is set.
- **Field-pass launcher**, new `scripts/windows/launch-for-field-pass.ps1`: `param([string]$Vault, [switch]$Seed, [switch]$Keep, [string]$Exe)`; creates `$env:TEMP\slate-field-pass-<yyyyMMdd-HHmmss>`, sets `SLATE_LOCAL_APP_DATA` and `SLATE_LOG_DIR` beneath it, with `-Seed` writes a `recent-vaults.json` holding `$Vault` (the `RecentVaultsStore` schema: path, display name, last-opened), starts `$Exe` (default `apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows/SlateWindows.exe`, the layout `windows.yml:848` uses for the gate dll) with `$Vault` as the argument, prints the root, and on exit deletes it unless `-Keep`. Documented in the field-pass recipe (below).
- Deliberately not changed: `ReadingSurface.cs:739`, the stores' file formats and names, `InstallProductionLocation`'s call site, production defaults when the variable is unset.

## Tests

- `SlateDeviceStateTests.ResolveWithoutAnOverrideIsTheProfilesSlateFolder` — `Resolve(null)` and `Resolve("")` equal `%LOCALAPPDATA%\Slate`. Mutation: drop the empty-string branch.
- `SlateDeviceStateTests.ResolveWithAnOverrideIsThatFolderMadeAbsolute` — a relative override is made absolute. Mutation: return the value unfullpathed.
- `SlateDeviceStateTests.EveryStoreDefaultIsUnderTheRoot` — the six default paths and `HostLog.LogDirectory` (with `SLATE_LOG_DIR` unset) start with `SlateDeviceState.Root`. Mutation: revert any one store to `LocalApplicationData`.
- `DeviceStateGuardTests.TheRealLocalAppDataIsUntouchedByThisRun` — compares `DeviceStateForTests.Snapshot` with a fresh snapshot of the real `%LOCALAPPDATA%\Slate` (created, changed or removed files fail it, listed). Mutation: construct `new RecentVaultsStore()` with the override unset in a scratch fact and `Save` — the guard names `recent-vaults.json`.
- `DeviceStateCensus.EveryTestConstructionOfARecentsOwnerPassesItsRoot` (Roslyn, over `tests/**/*.cs`): every `new FilesSidebarViewModel(` / `new QuickSwitcherViewModel(` passes `localAppDataRoot:` or a positional device-state argument; every `new RecentVaultsStore(` / `WindowStateStore(` / `AppPreferencesStore(` / `CommandPaletteRecentsStore(` in tests passes a path. Mutation: remove the argument at `MoveToLoadingTests.cs:291`.
- `ShellLaunchCensus.EveryShellLaunchAppliesTheDeviceState` (AccessibilityTests, Roslyn): every `new ProcessStartInfo(SlateWindowsExe())` is followed by `ShellLaunch.ApplyDeviceState(startInfo)` or is `StartShellProcess`. Mutation: delete the call at one inline site.
- FlaUI journey (gate, `[Trait("gate","W-C")]`): `ShellGate_WritesNothingToTheDevelopersLocalAppData` — snapshot the real `%LOCALAPPDATA%\Slate`, run the Announcements launch (`StartShellProcess`), wait for the main window, close it, compare snapshots; also asserts `recent-vaults.json` appeared under `DeviceStateForTests.Root`. Mutation: launch without the variable.
- Facts fail first (§4 rule 1): write the guard and the census before touching the stores.

## Docs and registers

- `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md`: add the launch recipe paragraph (the script, `-Seed`, `-Keep`), replacing "back up `%LOCALAPPDATA%\Slate` first".
- `apps/slate-windows/README.md` (or the project's developer notes, wherever `SLATE_LOG_DIR` is documented — `grep -rn SLATE_LOG_DIR apps/slate-windows/*.md docs/`): one row for `SLATE_LOCAL_APP_DATA`.
- Comments at `ShellAccessibilityTests.Announcements.cs:59-66` and `ShellAccessibilityTests.cs:938`.
- No contract rows, matrices, designations or ledgers.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb1'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SlateDeviceStateTests|FullyQualifiedName~DeviceStateGuardTests|FullyQualifiedName~DeviceStateCensus|FullyQualifiedName~CommandRegistrationTests.DefaultRecentsPath|FullyQualifiedName~SidebarWriteJournalTests.ATestProcess"
# the whole unit project once, then diff the real profile by hand:
Get-ChildItem "$env:LOCALAPPDATA\Slate" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 5
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj -c Release --no-build --filter "FullyQualifiedName~ShellGate_WritesNothing|FullyQualifiedName~ShellLaunchCensus"   # NVDA closed, desktop free
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <every touched .cs>
pwsh scripts/windows/launch-for-field-pass.ps1 -Vault C:\path\to\a\scratch\vault -Seed -Keep   # prints the root; confirm recent-vaults.json is there and the real one is unchanged
```

CI lanes: `windows.yml` (unit, model shards, FlaUI gate). AT pass: none — no speech changes.

## Acceptance

- [ ] Seven facts and two censuses above, each seen failing first or under its mutation.
- [ ] `grep -rn "SpecialFolder.LocalApplicationData" apps/slate-windows/src` returns only `SlateDeviceState.cs`.
- [ ] Every shell-gate launch carries `SLATE_LOCAL_APP_DATA`; the FlaUI guard journey is in the gate.
- [ ] `scripts/windows/launch-for-field-pass.ps1` exists and the NVDA recipe names it.
- [ ] `Closes #1331`.
- [ ] Not delivered: nothing expected; if the AccessibilityTests project cannot host a `[ModuleInitializer]` under VSTest assembly mode (`windows.yml:845-848` runs the dll without the project), the per-process root is created lazily by `ShellLaunch.ApplyDeviceState` instead and the PR body says so.

## Brief

Read `HostLog.cs:105-120`, the six store sites listed under "Current state", `App.xaml.cs:130-140`, `ShellAccessibilityTests.cs:100-140`, the four bare constructions and the two pins. Add `SlateDeviceState` (`Variable`, `Resolve`, `Root`), route the six stores and the log directory through it, add the two module initialisers and `ShellLaunch.ApplyDeviceState`, apply it at all 36 launches, pass `localAppDataRoot` at the four constructions, rewrite the two pins, add the facts and censuses, write `scripts/windows/launch-for-field-pass.ps1`, and update the NVDA recipe. Done when the unit project and the gate leave `%LOCALAPPDATA%\Slate` untouched (the guard facts prove it), the censuses hold, and the lane is green. Commit: `test(windows): the test suites and field passes write device state under SLATE_LOCAL_APP_DATA, never the developer's profile`.

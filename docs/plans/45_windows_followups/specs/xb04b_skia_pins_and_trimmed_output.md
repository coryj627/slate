# XB-4b — SkiaSharp and HarfBuzzSharp are pinned directly, and the build output carries no native symbols or non-Windows libraries

Issues: #1574. Size S–M. Lanes: windows (all four jobs; the artifact size is the witness). AT: —. Wave: X2, slot 2. Depends on: XB-4a merged (the SDK pin is settled before the package graph moves); XD7; the cooldown for HarfBuzzSharp's release. Parallel with: XB-2, XB-5. Decision: XD7.

## Goal

`SlateWindows.csproj` names SkiaSharp and HarfBuzzSharp at releases past the cooldown, so a transitive major can never arrive under a Svg.Skia minor again; the app's output (and so the FlaUI artifact) carries only Windows native libraries and no native `.pdb` symbols; the sizes before and after are in the PR.

## Current state on main

- `apps/slate-windows/src/SlateWindows/SlateWindows.csproj` (43 lines): `net10.0-windows`, `UseWPF` (`:5-8`); packages `AvalonEdit 6.3.1.120` (`:30`), `Svg.Skia 5.2.3` (`:31`), `WpfMath 2.1.0` (`:39`); `ProjectReference` `SlateUniffi` (`:40`). No `RuntimeIdentifier`, `SelfContained`, `Directory.Build.props`, `Directory.Packages.props`, `packages.lock.json` or `nuget.config` anywhere under `apps/slate-windows` (only `global.json` is tracked).
- SkiaSharp and HarfBuzzSharp are transitive through Svg.Skia 5.2.3 (#1542): SkiaSharp resolves to 4.148.0 (the first stable v4; RAW/DNG decoding still compiled in, removed in 4.150.5–4.153.1), HarfBuzzSharp to 14.2.0 (from 8.3.1.3, unassessed).
- Output growth: `runtimes/` 386 → 412 MiB, about 319 MiB of native `.pdb` and about 55 MiB of linux/osx libraries; the `flaui-binaries` artifact 288.2 → 305.3 MB (+5.9 %). Upload paths `windows.yml:577-580` (app, AccessibilityTests, GridConformanceHost `bin/Release/**`), download `:854-858`, consumer `:865-871`.
- Consumers of the app's output: `tests/SlateWindows.Tests/SlateWindows.Tests.csproj:66` (`ProjectReference` to the app), `:65` SlateUniffi, tools (`:70-78`); `tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj:59-60` (`ReferenceOutputAssembly="false"`), `:65`. Both test projects are `Exe`, `net10.0-windows`, no RID. `SlateUniffi.csproj:17` copies `generated\slate_uniffi.dll`.
- SVG decoding tests that prove the package still works: `SvgPreviewGuardTests` (`:44`, `:96`), `SvgDecodeOutOfProcessTests` (`:66`, `:83`, via `tools/SvgDecodeProbe`), `W2EditorInteractionRedTeamTests.SvgPreview_*` (`:82`, `:119`), `ReadingDiagramTests` (decode path at `ReadingDocumentBuilder.cs:931`).
- Cooldown: SkiaSharp 4.153.1 is dated 2026-09-30 (past); HarfBuzzSharp 14.2.0's NuGet date is read when the PR opens; if inside 7 days, the newest release at least 7 days old is taken instead.

## Reference

AGENTS.md "Dependencies" (after XB-0). NuGet's RID-specific asset selection (`RuntimeIdentifier` restricts `runtimes/<rid>/native` copies to that RID); XD7.

## Design

> **Delivered shape (2026-10-24): XD7's fallback, the exclusion.** A `RuntimeIdentifier` on the app trims the app's output only. Every project that references the app (`SlateWindows.Tests`, `GridConformanceHost`, `SvgDecodeProbe`, the benchmarks) resolves its own package assets, so `GridConformanceHost` still put 435 MiB of `runtimes/` into the `flaui-binaries` artifact, and the census below, which reads the test project's output, could not pass. The RID also moved the exe to `bin/Release/net10.0-windows/win-x64/`, away from the path the shell gate, two censuses and the CI pilot launch it from. And it still copied `libSkiaSharp.pdb` (91.5 MiB), flattened into the output root. What shipped is one target in `apps/slate-windows/Directory.Build.targets`, which every project there imports. After `ResolvePackageAssets` it removes from `RuntimeTargetsCopyLocalItems` every asset whose RID is not the apphost's (`DefaultAppHostRuntimeIdentifier`, the build machine's own: `win-x64` on every runner today, `win-arm64` on an ARM64 machine, where `generate-bindings.ps1` builds an ARM64 `slate_uniffi.dll`), `win` or `any`, and every `.pdb`. That item list feeds both the copy and `deps.json`, so the manifest never names a file the output lacks. Step 2's x64-only keep-set would have dropped the ARM64 Skia libraries on an ARM64 build, and the diagrams would have failed to decode with no error shown. The census checks all five outputs a test or the `flaui-binaries` artifact uses (the app, `SlateWindows.Tests`, `GridConformanceHost`, `SvgDecodeProbe`, `SlateWindows.AccessibilityTests`), and that no project escapes the import. The RID is not applied anywhere.

1. **Direct pins.** `SlateWindows.csproj` gains `<PackageReference Include="SkiaSharp" Version="4.153.1" />` and `<PackageReference Include="HarfBuzzSharp" Version="<newest ≥7 days old>" />` beside `Svg.Skia`, each with a comment naming the release date and #1574. NuGet's nearest-wins then takes the direct versions over Svg.Skia's lower bounds (confirm `dotnet list package --include-transitive` shows them resolved at the pins and nothing else moved; paste the diff of that listing in the PR).
2. **Trim the output (XD7).** First shape: `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` on `SlateWindows.csproj` (and `<SelfContained>false</SelfContained>` explicit). Build the solution; if the two test projects or the tools fail to reference a RID-specific app (NETSDK1150/1201 family), the fallback is a target in the app project after `ResolveAssemblyReferences`/`_ComputeResolvedCopyLocalPublishAssets` that removes from `ReferenceCopyLocalPaths` every item whose `DestinationSubDirectory` starts with `runtimes\` and is not under `runtimes\win-x64\` (keep `win-x86`/`win-arm64` only if something runs them: nothing does today; the ARM64 cross-compile is Rust-only, `windows.yml:340-344`), plus every `runtimes\**\*.pdb`. Whichever ships, the PR says which and why; the other is not left half-applied.
3. **Measure.** Before and after, on the same machine: `Get-ChildItem -Recurse apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows*/runtimes | Measure-Object Length -Sum`, the same for the two other artifact roots, and the artifact size from `gh api repos/{owner}/{repo}/actions/artifacts` for the PR's run. All six numbers in the PR body.
4. Not changed: `Svg.Skia`'s version; AvalonEdit; WpfMath; the test projects' packages; `global.json`.

## Tests

- `tests/SlateWindows.Tests/Censuses/BuildOutputCensus.cs` (new): `TheNativeRuntimesCarryOnlyWindowsLibrariesAndNoSymbols` — from `AppContext.BaseDirectory` (the test project's own output, which receives the app's copy-local set through the `ProjectReference`), assert `runtimes/` contains only `win-*` directories and no `*.pdb` under `runtimes/`; `ThePackageGraphPinsSkiaSharpAndHarfBuzzSharpDirectly` — read the app csproj (path from the test's `EvidenceCompilation` manifest or a relative walk) and assert both `PackageReference`s exist with a four-part/three-part version. Red first on `main` (no pins; `.pdb` and `linux` present).
- The SVG and diagram suites named above stay green (they are the functional witnesses that the pinned Skia still decodes).
- The FlaUI gate on the lane (it runs the shipping exe from the trimmed artifact).

## Docs and registers

- `SlateWindows.csproj` (two pins, the RID or the trim target, comments).
- `docs/plans/18_windows_port/` has no package register; nothing to update. BENCHMARKS.md: nothing.
- PR body: the six sizes, the `dotnet list package` diff, the release dates.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb4b'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0 (before: record sizes; after: again)
Get-ChildItem -Recurse apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows*/runtimes -File | Measure-Object Length -Sum
dotnet list apps/slate-windows/src/SlateWindows/SlateWindows.csproj package --include-transitive | Select-String 'SkiaSharp|HarfBuzzSharp|Svg'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~BuildOutputCensus|FullyQualifiedName~SvgPreviewGuardTests|FullyQualifiedName~SvgDecodeOutOfProcessTests|FullyQualifiedName~W2EditorInteractionRedTeamTests.SvgPreview|FullyQualifiedName~ReadingDiagramTests"
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf&kind!=benchmark"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/Censuses/BuildOutputCensus.cs
```

CI lanes: `windows.yml` (all four; the FlaUI gate proves the trimmed artifact runs). AT: none.

## Acceptance

- [ ] Both packages pinned directly at releases ≥7 days old, dates in the csproj comments and the PR.
- [ ] `runtimes/` holds only `win-*` and no `.pdb`; the census pins it; sizes before/after and the artifact size in the PR.
- [ ] The SVG, diagram and FlaUI suites green on the trimmed output.
- [ ] `Closes #1574`. Not delivered: nothing (if the RID shape failed, the trim-target shape shipped and the reason is recorded).

## Brief

Read `apps/slate-windows/src/SlateWindows/SlateWindows.csproj`, `tests/SlateWindows.Tests/SlateWindows.Tests.csproj:25-80`, `tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj:10-70`, `src/SlateUniffi/SlateUniffi.csproj`, `.github/workflows/windows.yml:566-582`, `:854-871`, and one existing census for shape (`tests/SlateWindows.Tests/Censuses/ParityHarnessCensus.cs:60-90`). Check NuGet for HarfBuzzSharp's newest release ≥7 days old (and SkiaSharp 4.153.1, 2026-09-30). Build: direct `PackageReference`s for SkiaSharp and HarfBuzzSharp with dated comments; `RuntimeIdentifier` `win-x64` + `SelfContained false` on the app project, falling back to a `ReferenceCopyLocalPaths` trim target (non-`win-x64` `runtimes\**` and all `runtimes\**\*.pdb`) if the solution's test or tool projects refuse the RID; new `Censuses/BuildOutputCensus.cs` with `TheNativeRuntimesCarryOnlyWindowsLibrariesAndNoSymbols` and `ThePackageGraphPinsSkiaSharpAndHarfBuzzSharpDirectly` (red on `main`); `git add` the new file then `python3 scripts/apply-license-header.py`. Measure `runtimes/` for the three artifact roots before and after, and the artifact size on the PR's run; paste the `dotnet list package --include-transitive` lines. Run the SVG/diagram suites and the full unit run; `dotnet format --include` on the new file. Done when `windows.yml`'s four jobs are green, the census passes, the six sizes are in the PR. `Closes #1574`. Commit: `chore(windows): skiasharp and harfbuzzsharp are pinned directly, and the build output carries only windows native libraries without symbols`.

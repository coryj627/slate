# XB-0 — Close-outs: #1572 closes on its own reproduction, the cooldown rules are already on `main` (#1605), #1562 closes on its recorded pass, and the stale notes are corrected

Issues: #1572 #1562 (Refs #1493). Size S. Lanes: none (the #1572 run is local; the rest are issue comments). AT: —. Wave: X0. Depends on: nothing. Parallel with: nothing (X0 has one slot). Decision: XD8 (decided 2026-10-24: "yes, close #1562 with #1595 and #1591 carrying the criterion").

## Goal

Three issues that already have their fix or their evidence close with a comment naming it; the written `AGENTS.md` rules (the 7-day cooldown and the one-at-a-time merge) reach `main` so every later chunk works under them; and two issue notes that no longer match the tree are corrected so the next reader starts from the right place.

## Current state on main

- **#1572.** `apps/slate-windows/tests/SlateWindows.Tests/Support/ShellCompilation.cs:22-43`: `ShellCompilation` builds its `CSharpCompilation` once (`Built`, `:24`) from "the test project's recorded compile references" (`:13`, `:94` names #1600); `SelectReferences` (`:36-43`) takes every recorded reference but the shell's own assembly. The `AppDomain.CurrentDomain.GetAssemblies()` source the issue describes is gone (#1601, f267867b, which closed #1600 and never mentions #1572). The issue's reproduction is `AtNavigationMapCensus.UnrelatedSameNamedFieldsCannotManufacturePatternClaims` (`tests/SlateWindows.Tests/Censuses/AtNavigationMapCensus.cs:331`) run alone in a fresh process.
- **#1569 / #1565 step 3.** Done: #1605 merged as 1aeb0115 on 2026-10-23. `main`'s `AGENTS.md` has the `## Dependencies` section and the one-at-a-time merge procedure under "Git and PRs".
- **#1562.** Open, labels `a11y test windows`. The owner's comment of 2026-10-23 records the pass on `main` b53a56cc: 96/96 speech, 32/32 braille, 16/16 UI Automation cells equal to the golden; the raw-LaTeX criterion fails on line reading (#1595) and Tab (#1591's Windows comment). The comment ends "Whether #1595 and #1591 carry that, and this issue closes, is the owner's call."
- **#1493.** The body says the pin is `=9.0.0-rc.5`; `Cargo.toml:125-132` is on rc.6 since #1553 (ed01221e). crates.io's newest `notify` is 9.0.0-rc.6 (checked 2026-10-23). Plan 44 §8 watches it.

## Reference

Plan 44's WB-0 (the close-by-comment pattern: cite the fixing merge, the anchors, the run). AGENTS.md "Git and PRs" (one PR closes its issues; PR body shape).

## Design

1. **#1572 close-out.** No code. On `main` at the current head, regenerate bindings into a per-worktree target, build Release, then run the census alone in a fresh process:
   ```powershell
   dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AtNavigationMapCensus"
   ```
   and once more with the narrowest filter (`…~AtNavigationMapCensus.UnrelatedSameNamedFieldsCannotManufacturePatternClaims`). Both pass with no `CS0234`. Close with a comment: f267867b (#1601) replaced the `GetAssemblies()` source with the recorded references (`ShellCompilation.cs:13`, `:36-43`, `:94`); the two commands and their `Passed!` lines; "fixed by #1601, which closed #1600 only".
2. **The cooldown PR: done.** #1605 merged as 1aeb0115 on 2026-10-23 (`Closes #1569`, `Refs #1565`); every chunk in this plan works under its two rules.
3. **#1562 close-out (XD8).** Comment: the recorded cells pass; the raw-LaTeX criterion is carried by #1595 (line reading) and #1591 (Tab, the Windows comment), both in plan 45 XB-9; close.
4. **#1493 correction.** Comment: "`main` is on `=9.0.0-rc.6` since #1553 (ed01221e); the bump this issue tracks starts from rc.6. crates.io newest on 2026-10-23: 9.0.0-rc.6." No edit to the body (the owner's).
5. **Follow-up issues this plan names**, filed only if absent (`gh issue list --search`):
   - "windows: the SVG preview guard weighs stylesheet rules × elements and marker vertices × subtree, so the commonest slow documents are refused before decoding" — `Refs #1581`; XB-10b's "To decide" bullet 4.
   - A mac twin of XD2's on-request source landing, only if XD2 picks a new chord (then "mac: the math source is spoken on request, never on focus (the XD2 twin)", `Refs #1591`). If XD2 reuses the existing Ctrl+Enter family, #1591's mac half already carries it and nothing is filed.
6. Deliberately not done: no `AGENTS.md` text beyond the branch's commit (plan 44's own `AGENTS.md` edits, if any, come later); no change to `.github/dependabot.yml` (XB-12).

## Tests

None new. The #1572 run is the witness; the PR for item 2 carries no code.

## Docs and registers

- `AGENTS.md` (the branch's diff).
- Plan 45 §2.1 and §2.2 already record the facts; no edit unless the run contradicts them.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb0'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AtNavigationMapCensus"
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AtNavigationMapCensus.UnrelatedSameNamedFieldsCannotManufacturePatternClaims"
```

CI lanes: none. AT: none.

## Acceptance

- [ ] #1572 closed with the two commands, their results and f267867b named.
- [x] The cooldown PR: #1605, merged as 1aeb0115.
- [ ] #1562 closed per XD8 (or left open with the reason, under "no").
- [ ] #1493 carries the rc.6 correction.
- [ ] The SVG guard-weights follow-up exists; the mac twin exists only if XD2 needs it.
- [ ] Not delivered: nothing.

## Brief

Fresh worktree on `main`. (1) Regenerate bindings (`$env:CARGO_TARGET_DIR='C:\dev\slate\target-xb0'; ./apps/slate-windows/generate-bindings.ps1 -Locked`), build Release (`dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release`, `Error(s)=0`), then run `dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AtNavigationMapCensus"` and the same with `…~AtNavigationMapCensus.UnrelatedSameNamedFieldsCannotManufacturePatternClaims`; both must pass with no `CS0234`. Close #1572 with a comment citing f267867b (#1601; `Support/ShellCompilation.cs:13`, `:36-43`, `:94` take the recorded references) and the two `Passed!` lines. (2) The cooldown PR is done (#1605). (3) Under XD8 "yes": comment and close #1562 ("recorded cells pass; the raw-LaTeX criterion is carried by #1595 and #1591, plan 45 XB-9"). (4) Comment on #1493 that `main` is on rc.6 since ed01221e and crates.io's newest is 9.0.0-rc.6. (5) `gh issue list --search "SVG preview guard weighs"`; if absent, file "windows: the SVG preview guard weighs stylesheet rules × elements and marker vertices × subtree, so the commonest slow documents are refused before decoding" with `Refs #1581`. File the mac XD2 twin only if XD2 picked a new chord. Done when all five are visible on GitHub. No commit: every item is a comment, a close or a local run.

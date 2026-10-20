# WB-32 — The uniffi 0.32 bump is a day's work when the C# generator tags: a local spike against NordSecurity/uniffi-bindgen-cs#176 says exactly what breaks

Issues: Refs #998 (closes with the dependency bump once an upstream tag exists; §5 closure matrix). Size S. Lanes: windows (local only; nothing merges but the report). AT: none. Wave: any free slot. Depends on: nothing. Parallel with: anything. Decision: none.

## Goal

uniffi 0.32 reworked the external-bindings pipeline, and the pinned C#
generator (`v0.11.0+v0.31.0`) cannot read a 0.32 dylib, so #980's bump
was closed as blocked. Upstream PR #176 ("Upgrade to uniffi-rs 0.32.0",
head `cf0aa7a8` on `dennisameling/uniffi-bindgen-cs`, branch
`upgrade/uniffi-rs-0.32.0`, mergeable, updated 2026-10-14) is the
opening. This spike builds that generator locally, bumps the workspace
to 0.32 on a throwaway branch, regenerates the C# binding, builds and
tests `apps/slate-windows`, and writes down what the real bump will
need — so that when a `v0.x+v0.32.0` tag ships, the bump is mechanical.
Nothing from the spike merges except its report.

## Current state on main

Verified against the worktree; the plan's "the pin is now one file"
holds for the tag, with one correction: the tag is read in two places
(the workflow and the composite action), both from the one file.

- `apps/slate-windows/uniffi-bindgen-cs.version`: `v0.11.0+v0.31.0`.
- `.github/workflows/windows.yml:489-513` and `:716-740` read the file,
  validate it against `^v\d+\.\d+\.\d+\+v\d+\.\d+\.\d+$` (`:495`, `:721`),
  cache `~/.cargo/bin/uniffi-bindgen-cs.exe` keyed by the tag, and
  install with `cargo install --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag '<tag>' uniffi-bindgen-cs --locked`
  (`:513`, `:740`). `.github/actions/windows-native-build/action.yml:45-58`
  is the same three steps. The issue's `windows.yml:63/69` and
  `generate-bindings.ps1:10` references are stale: the script's install
  command is now the comment at `generate-bindings.ps1:12-14`.
- `apps/slate-windows/generate-bindings.ps1:30-52`: `cargo build -p slate-uniffi --release`
  (honours `CARGO_TARGET_DIR`), then `uniffi-bindgen-cs --library <dll> --out-dir src/SlateUniffi/generated --config apps/slate-windows/uniffi.toml`
  (`uniffi.toml`: `access_modifier = "public"`), copies the DLL beside
  the binding. The generated directory is git-ignored.
- `Cargo.toml:24` `uniffi = { version = "0.31", features = ["cli"] }`;
  `Cargo.lock` resolves `uniffi`, `uniffi_bindgen`, `uniffi_core`,
  `uniffi_internal_macros` at `0.31.2`. `crates/slate-uniffi/Cargo.toml:16-17`
  ships the `uniffi-bindgen` bin (`src/bin/uniffi-bindgen.rs`) the mac
  build uses (`scripts/build-mac-app.sh:5-6`, `:69-70`).
- The issue's second blocker (askama 0.16 vs the Namespace mac cache
  path) targeted `swift-tests.yml`'s `cache: rust`; today only
  `rust.yml:92-94` and `nightly.yml:77-79` use `nscloud-cache-action`
  with `cache: rust`, so the mac lane's exposure has to be re-checked in
  the spike, not assumed.
- No Renovate or Dependabot config exists under `.github` (the issue's
  "interim action" was never needed; `#980` stays closed).
- The issue's own measurements (byte-identical `slate_uniffiFFI.h`,
  checksums all changed, `EmbedResolution`/`PropertyValue` become
  `indirect`) date from the 0.32.0 bump attempt; the spike re-measures
  them on today's `lib.rs` (which has grown since: `code_block_preamble`,
  `VaultRootIdentity`, the graph records).

## Reference

None; the issue's text and `18_windows_port/specs/w0_spec.md`'s
"Decision consequences" (the generator tag is upgraded in lockstep with
the uniffi dependency) are the spec. `CONTRIBUTING` "The FFI bindings
workflow" for the regeneration rule (never a dylib with mismatched
bindings; §11 risk).

## Design

A throwaway branch `claude/wb32-uniffi-032-spike` from `main`, never
opened as a PR; a report file is the only artefact that lands (through
WB-0 or a docs-only PR). Exact steps:

1. **Build the generator from the PR's head** into a private cargo
   home so the pinned `v0.11.0+v0.31.0` binary in `~/.cargo/bin` stays
   untouched (the lanes and every other worktree need it):
   ```
   $env:CARGO_TARGET_DIR='C:\dev\slate\target-wb32'
   cargo install --git https://github.com/dennisameling/uniffi-bindgen-cs --rev cf0aa7a8fb59358a846708730a58a02bf206ac05 uniffi-bindgen-cs --locked --root C:\dev\slate\tools\bindgen-032
   C:\dev\slate\tools\bindgen-032\bin\uniffi-bindgen-cs.exe --version
   ```
   If `--locked` fails on the fork's lockfile, record it and retry
   without `--locked`; the report names which.
2. **Bump** on the branch: `Cargo.toml:24` → `uniffi = { version = "0.32", features = ["cli"] }`;
   `cargo update -p uniffi -p uniffi_bindgen -p uniffi_core -p uniffi_internal_macros`;
   `cargo build -p slate-uniffi --release --locked`; `cargo test -p slate-uniffi`.
   Record every compile error (the issue expects none: proc-macro only,
   no `.udl`).
3. **Regenerate** with the spike generator on the PATH ahead of the
   pinned one, through the shipped script so its arguments are the
   lanes':
   ```
   $env:Path = "C:\dev\slate\tools\bindgen-032\bin;$env:Path"
   ./apps/slate-windows/generate-bindings.ps1 -Locked
   ```
   Diff the generated `src/SlateUniffi/generated/*.cs` against a
   0.31-generated copy taken from `main` beforehand (same
   `CARGO_TARGET_DIR`): count added, removed and renamed public
   symbols, note any signature change (the checksum constants will all
   change; that is expected), and record whether the Swift notes from
   the issue (`indirect` enums) have a C# analogue.
4. **Build and test** the Windows app and every test project:
   ```
   dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
   dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release
   dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --no-build --configuration Release   # desktop free, NVDA closed
   dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes
   ```
   Also `cargo test -p slate-core` and `make ci` on the branch (the
   bump touches the lockfile every Rust lane reads).
5. **Measure the mac half locally only as far as a Windows machine
   can:** `cargo build -p slate-uniffi --bin uniffi-bindgen` and
   `uniffi-bindgen generate --library <dll> --language swift --out-dir <tmp>`
   to diff `slate_uniffiFFI.h` and `slate_uniffi.swift` against a 0.31
   generation (the issue's "byte-identical header" claim, re-measured);
   whether `swift build` still succeeds is the mac follow-up's.
6. **Write the report** at
   `docs/plans/44_windows_backlog/reports/uniffi_032_spike_<date>.md`.

Not changed on `main` by this chunk: `Cargo.toml`, `Cargo.lock`, the
version file, the workflows, `generate-bindings.ps1`. The real bump,
when a tag exists, is: the three-crate `cargo update`, `Cargo.toml:24`,
the version file (one line; the regex at `windows.yml:495`/`:721` and
`action.yml:46` accepts only a tag of the form `vA.B.C+vX.Y.Z`, so a
`--rev` fork pin cannot ride the lanes without widening that regex and
the `--tag` install — the report says whether the owner wants that
widened as an interim), and the measured source edits.

## Tests

No new facts land: the spike runs the existing suites on the throwaway
branch. The report records, per suite, the pass/fail counts and every
failure's name; a failure caused by the bump (not a known flake from
#1323's list) is reproduced twice before it is recorded as the bump's.
If the spike finds a generated-binding difference that an existing
census should have caught (a public-symbol rename with no failing
fact), the report names the census to extend in the real bump.

## Docs and registers

- The report (above), with: the generator's exact rev and build
  command; the lockfile diff summary (crate versions before/after);
  the compile errors (if any) and their fixes; the generated-C# symbol
  diff (added/removed/renamed, checksum-only changes counted
  separately); the build and test outcome per project; the Swift
  header/binding diff; the askama/Namespace cache exposure as re-read
  from today's `swift-tests.yml`; the lane changes the real bump needs
  (version file, regex if an interim fork pin is wanted, cache key);
  an estimate for the bump PR; and the watch rule (§8: weekly check for
  a `v0.x+v0.32.0` tag on NordSecurity/uniffi-bindgen-cs).
- #998: a comment linking the report. Plan 44 §8's #998 row cites it.
- No contract, census, matrix, `chords.json` or designation change.

## Verification

The commands in Design steps 1–5, run locally with a private
`CARGO_TARGET_DIR` and a private `--root` for the generator; CI runs
nothing (no PR from the branch). Before deleting the branch:
`Remove-Item Env:Path`-style restoration is not needed (the PATH edit
is per shell), but confirm `uniffi-bindgen-cs --version` in a fresh
shell still reports the pinned 0.31 build, and delete
`C:\dev\slate\target-wb32` and `C:\dev\slate\tools\bindgen-032`.

## Acceptance

- [ ] The generator built from `cf0aa7a8` (or the fork's newer head, recorded) and its `--version` captured.
- [ ] Bump, regenerate, build and test run to completion; every outcome in the report, including failures.
- [ ] The report lists the exact edits the real bump needs and the lane-regex question for the owner.
- [ ] `main` untouched by the spike; the pinned generator still the one in `~/.cargo/bin`.
- [ ] PR body (the docs PR that lands the report): `Refs #998`; "not delivered: the bump itself — waits for an upstream tag (§8)".

## Brief

Read `apps/slate-windows/uniffi-bindgen-cs.version`,
`apps/slate-windows/generate-bindings.ps1:1-52`, `apps/slate-windows/uniffi.toml`,
`.github/workflows/windows.yml:489-513`, `.github/actions/windows-native-build/action.yml:40-58`,
`Cargo.toml:24`, the `uniffi*` entries in `Cargo.lock`,
`crates/slate-uniffi/Cargo.toml`, `scripts/build-mac-app.sh:60-80`, and
#998 in full. On a throwaway branch, build the C# generator from
NordSecurity/uniffi-bindgen-cs#176's head into a private `--root`,
bump uniffi to 0.32, regenerate through the shipped script, build and
test every Windows project and the Rust gates, diff the generated C#
and Swift against 0.31, and write the report with the contents listed
under Docs. Done when the report is on `main` through a docs PR,
#998 links it, and the local toolchain is back to the pinned
generator. Commit (docs PR): `docs(plans): the uniffi 0.32 spike
records what the bump needs when the c# generator tags`.

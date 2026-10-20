# Slate

Accessibility-first, Obsidian-vault-compatible knowledge workspace. One Rust
core, one FFI surface, one native app per platform:

- `crates/slate-core`: domain logic and accessible representations. No platform code.
- `crates/slate-uniffi`: the only FFI boundary. Every frontend generates its bindings from it.
- `crates/slate-cli`: the `slate` command-line tool.
- `apps/slate-mac`: SwiftUI + AppKit. `apps/slate-windows`: WPF + AvalonEdit + UIA.

The locked architecture is `docs/plans/05_locked_architecture_decisions.md`.
Read it before changing a boundary. Where prose and an executable spec (the
tests) disagree, the tests and the code win.

## Architecture rules

- Only `crates/` is shared. A frontend never imports from another frontend's
  directory; anything a second platform would need goes in `slate-core`.
- Accessible output (labels, announcements, spoken text) is produced in
  `slate-core` and rendered by the host, not composed in the UI layer.
- An FFI change happens in `crates/slate-uniffi`. Run `make regenerate-bindings`
  locally, and `make regenerate-bindings-windows` when a Windows toolchain is
  available; otherwise `windows.yml` proves the C# side. Update both
  platforms' consumers in the same PR and keep both native lanes green.

## Commands

- `make ci`: fmt-check, clippy with warnings as errors, tests, bench compile,
  license headers. Run before every push that touches `crates/`.
- `make test-fast`: the CI-shaped nextest sweep, about 5x faster than
  `make test`. Needs `cargo-nextest`.
- Mac tests: `./scripts/build-mac-app.sh --skip-a11y-check`, then from
  `apps/slate-mac`: `DYLD_LIBRARY_PATH="$PWD/../../target/debug" swift test --filter <TestClass>`.
  Without `DYLD_LIBRARY_PATH` the tests cannot load the Rust dylib.
- Mac a11y-check: `./scripts/build-mac-app.sh` (without `--skip-a11y-check`).
  It needs `a11y-check` installed (README's brew tap) and silently skips it,
  exiting 0, when it is missing. Locally it gates on errors only; the score of
  100 is enforced by `a11y-check.yml`.
- Windows (PowerShell, no make): `./apps/slate-windows/generate-bindings.ps1`, then
  `dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release` and
  `dotnet test apps/slate-windows/SlateWindows.slnx --configuration Release`.
  Run `dotnet format apps/slate-windows/SlateWindows.slnx` before pushing
  (CI runs it with `--verify-no-changes`).
- New `.rs`, `.swift` or `.cs` file: `git add` it first, then run
  `python3 scripts/apply-license-header.py`. The script only touches tracked
  files.

## Gotchas

- The generated bindings are git-ignored and never edited or committed:
  `apps/slate-mac/Sources/SlateMac/slate_uniffi.swift`,
  `apps/slate-mac/Sources/slate_uniffiFFI/slate_uniffiFFI.h`,
  `apps/slate-windows/src/SlateUniffi/generated/`. If Swift does not see an
  FFI change, the staged bindings are stale: rerun `make regenerate-bindings`.
- The Windows generator always builds the Release DLL. Never substitute
  `target/debug/slate_uniffi.dll` for app or test builds.
- On a machine without the Windows toolchain, a Windows test is never seen
  failing locally. Before merging one, push the change with its behaviour
  switched off to a throwaway draft PR or scratch branch, show the new tests
  fail, then delete the probe.
- Adding a Mac command, chord, right-pane leaf, settings tab, help doc or CLI
  verb changes `docs/plans/18_windows_port/parity_matrix.md`. Rerun
  `python3 scripts/generate-parity-matrix.py`; `windows.yml` fails on drift
  (`--check`).
- GitHub closes an issue when any form of close, fix or resolve stands before
  its number, even mid-sentence. A PR that only references an issue keeps those
  words away from the number. Check `gh pr view N --json closingIssuesReferences`
  before merging.
- Two merges that touch the same files were never tested together. Wait for
  `main`'s CI on the combined head before building on it.

## Accessibility gates

- `a11y-check` stays at 100.
- A change to what VoiceOver speaks or how it navigates gets a VoiceOver field
  pass (`scripts/vo.sh` with `docs/runbooks/voiceover-feature-test.md`), with
  the spoken phrases recorded in the PR. XCTest proves attributes, not speech.
- A surface that a watcher, feed or timer can republish gets a test that the
  reader's element and focus survive the republish.

## Tests

- Write the failing test first for every behaviour you add or fix.
- Swift tests reuse the Windows test name where a Windows twin exists.
- Run only the lanes the change touches, and batch follow-up pushes. CI lanes
  are path-filtered, but any change under `apps/slate-mac/`, `crates/` or
  `docs/plans/` also starts the Windows lane, and lane runs cost money.
- Report verification as the command you ran and its result, not as a claim.

## Git and PRs

- Agents work in a fresh worktree on `<agent>/<slug>` from `main`
  (`claude/…`, `codex/…`).
- Commit message and PR title: `type(scope): one lower-case sentence saying
  what is now true`. Example: `fix(mac): the media card's activation hint says
  what activation does`. Common scopes: `core`, `mac`, `windows` (also with `docs`,
  `test` and `ci` types); plan and runbook docs use `docs(plans)` and
  `docs(runbooks)`.
- PR body: `Closes #N` for each issue it closes, the tests added, the contract
  register rows closed, VoiceOver phrases where a pass was required, and
  `not delivered: <reason>` for anything not done or not verified.
- One PR closes its issues and nothing else. A defect found on the way is
  filed as an issue unless it blocks the PR. A defect in the PR's own feature
  is fixed on its branch.
- A PR closes the register rows it affects in the `docs/plans/*_contracts.md`
  documents in the same change. A frozen contracts record is append-only:
  add only the owner's recorded call.
- Merges are pinned: `gh pr merge N --match-head-commit <sha>`.

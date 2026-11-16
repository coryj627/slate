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
- A Windows test that reads a census-only path (a Mac source, or a plan or
  help document in `census-paths`) carries `[Trait("tier", "census")]` on its
  class, and so does every test that calls a helper that reads one.
  `scripts/check_windows_census_trait.py` fails the lane otherwise.
- GitHub closes an issue when any form of close, fix or resolve stands before
  its number, even mid-sentence. A PR that only references an issue keeps those
  words away from the number. Check `gh pr view N --json closingIssuesReferences`
  before merging.

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
- Run only the lanes the change touches, and batch follow-up pushes. Every
  lane reports on every PR but runs its jobs only for its own paths (its
  other jobs report as skipped, which counts as passed). A change under
  `crates/` starts the whole Windows lane. A PR that touches only
  `apps/slate-mac/` and the plan and help documents the Windows censuses
  read (`census-paths` in `windows.yml`) runs its census tier: the app
  build and the tests tagged `[Trait("tier", "census")]`, every other
  Windows job skipped. Anything else on the lane's list, and every push to
  `main`, runs it in full. Lane runs cost money.
- Report verification as the command you ran and its result, not as a claim.

## Dependencies

- The cooldown: every version a change brings in is a release at least 7
  days old when the PR opens, so a release yanked or compromised in its
  first days never reaches `main`. It holds for bot and hand-picked versions
  alike, and for manifest and lockfile entries, actions, toolchains, tools
  and image pins.
- The cooldown's only exceptions are a security fix and a case the owner
  approves. The PR names the exception and its reason.
- Release dates come from the release list (crates.io, NuGet, GitHub
  releases). "Latest" in a PR means the newest release past the cooldown on
  that list when the PR opens.

## Git and PRs

- Agents work in a fresh worktree on `<agent>/<slug>` from `main`
  (`claude/…`, `codex/…`).
- Commit message and PR title: `type(scope): one lower-case sentence saying
  what is now true`. Example: `fix(mac): the media card's activation hint says
  what activation does`. Common scopes: `core`, `mac`, `windows` (also with `docs`,
  `test` and `ci` types); plan and runbook docs use `docs(plans)` and
  `docs(runbooks)`.
- PR body: `Closes #N` for each issue it closes, the tests added, the contract
  register rows closed, VoiceOver phrases where a pass was required, each
  version it adds or moves with its release date, a line under "Found and
  fixed here" for each defect fixed on the way, `not delivered: <reason>`
  for anything not done or not verified, and a last line with the count:
  found on the way, fixed here, deferred, filed.
- One PR closes its issues. A defect found on the way is sized by what its
  fix touches:
  - Small: the fix stays in files the PR already changes, plus their tests.
    Fix it in the PR.
  - Medium: the fix reaches three or fewer other non-test files in the same
    subsystem (one top-level folder of an app, or one module of a crate;
    the files of one partial class count as one subsystem wherever they
    sit). Fix it in the PR, or defer it to the plan's list of deferred
    finds; outside a plan, or in a plan that keeps no such list, file it.
  - High: the fix reaches more than three other non-test files, crosses
    subsystems, or needs a design, an owner decision, an FFI change or a
    machine the agent does not have. File it. If it blocks the PR, its fix
    merges first, as its own PR.

  A fix that needs an owner decision, an FFI change, a machine the agent
  does not have, or a new or changed spoken sentence that needs a full
  screen-reader pass is never small or medium.
- Each fix taken in the PR has its failing test first and a line under
  "Found and fixed here" in the PR body. Stop taking them when they would
  more than double the non-test files the PR changes.
- A defect in the PR's own feature is fixed on its branch, whatever its size.
- A PR closes the register rows it affects in the `docs/plans/*_contracts.md`
  documents in the same change. A frozen contracts record is append-only:
  add only the owner's recorded call.
- Merges go one at a time, so every merge is tested on the code it lands on:
  1. The PR's branch contains the current `main`, and every check
     `gh pr checks N` lists passes or skips. `main`'s ruleset enforces the
     up-to-date branch and the `pr gate` check; the lanes are yours to
     check until the ruleset requires them (#1565).
  2. Merge pinned to the head those checks ran on:
     `gh pr merge N --squash --match-head-commit <head-sha>`.
  3. Before the next merge, every run on the merge commit finishes green
     (`gh run list --commit <merge-sha>`, once the push has started them).
     Each `main` push runs in its own concurrency group, so no run is
     dropped (#1565); waiting keeps a red run traceable to one merge. A red
     `main` comes back to green before anything else merges.

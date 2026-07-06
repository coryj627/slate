# W0 executable spec — Foundation: the C# binding, the scaffold, and the pre-port canonicalization (W0.5)

Issues: W0-1 ([#714](https://github.com/coryj627/slate/issues/714)) · W0-2 ([#603](https://github.com/coryj627/slate/issues/603)) · W0-3 ([#715](https://github.com/coryj627/slate/issues/715)) · W0-4 ([#716](https://github.com/coryj627/slate/issues/716)) · W0.5-1 ([#717](https://github.com/coryj627/slate/issues/717)) · W0.5-2 ([#718](https://github.com/coryj627/slate/issues/718)) · W0.5-3 ([#719](https://github.com/coryj627/slate/issues/719)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 1–5, 16–17; DoD §W-E). Grounding: [`../../07_portability_review.md`](../../07_portability_review.md) §4.1 (the binding asymmetry), [`../../13_repo_structure.md`](../../13_repo_structure.md) (scaffold conventions).

**Execution order: W0-1 → { W0-2 ∥ W0-3 } · W0.5-1 ∥ W0.5-2 ∥ W0.5-3 (independent, any time) · W0-4 at unpark.**
**Pre-unpark eligibility:** W0-1 and all three W0.5 issues may be worked while W is parked (program §Entry criteria). W0-2/W0-3/W0-4 wait for unpark.

Baseline facts (verified 2026-07-06, this worktree):

- The FFI is **proc-macro uniffi** (`#[uniffi::export]` / `#[derive(uniffi::Object)]`), no `.udl` file — `crates/slate-uniffi/src/lib.rs`. Generator compatibility must be evaluated against proc-macro mode, not UDL mode.
- Objects with Arc lifetime: `VaultSession` (lib.rs:282), `CancelToken` (lib.rs:984), `DocumentBuffer` (lib.rs:2811).
- **Two** foreign-callback traits (`#[uniffi::export(with_foreign)]`): `ScanProgressListener` (lib.rs:1824) and `CommandAction` (lib.rs:3607). Both must marshal in the spike — `CommandAction` is how command invocation re-enters the host, so it is on the hot path of W5, not an edge case.
- Host logging facade: `crates/slate-uniffi/src/host_logging.rs` (#674) — non-fatal diagnostics route through it; the C# host must install a sink like the mac host does.
- **`slate-core` has never been compiled for Windows** and contains real unix-only paths: `sync_detect.rs` uses `std::os::unix::ffi::OsStrExt` outside test code (:197, :226, :257, :764) plus `#[cfg(unix)]` symlink-handling test scaffolding. Expect `#[cfg(windows)]` work in W0-1 before anything links.
- License-header gate covers `.rs`/`.swift` only (`.github/workflows/license-headers.yml` paths; `scripts/apply-license-header.py` scopes `git ls-files '*.rs' '*.swift'`) — `.cs` files are currently invisible to it.
- Bindings are generated + git-ignored; `make regenerate-bindings` currently delegates to `./scripts/build-mac-app.sh --bindings-only` (Makefile:60) — mac-only today; W0-3 generalizes it.
- CI is per-area path-filtered workflows (`.github/workflows/`: `rust.yml`, `swift-tests.yml`, `a11y-check.yml`, `audit.yml`, `license-headers.yml`).
- Swift-only logic pockets for W0.5 (the 07 §3 drift rows, plus one shipped after that review):
  - Palette ranking: `CommandPaletteModel.fuzzyScore(query:target:)` (CommandPaletteModel.swift:300) + section ordering + recents policy (`CommandPaletteRecentsStore` load/save/add/remove, CommandPaletteRecentsStore.swift:28).
  - Quick-switcher ranking: `QuickSwitcherModel.score(query:row:)` (QuickSwitcherModel.swift:195) + recents blending in `load(files:recents:)` (:91).
  - Announcements: `AnnouncementPosting` protocol + `AnnouncementPriority` (AnnouncementPosting.swift:9/:31) is only the *poster*; the trigger conditions and message strings are scattered at call sites (`CanvasAnnouncer`, palette/switcher filter announcements, mutation announcements, etc.).

---

## W0-1 · Binding spike: `uniffi-bindgen-cs` vs `csbindgen` shim — PR 1

**Question the spike answers:** which path gives Slate a C# binding with correct object lifetime, foreign callbacks, error mapping, and cancellation, at the lowest sustained maintenance cost — judged on evidence, not doctrine.

### Normative rules

0. **First act:** `cargo check`/`cargo test` for `x86_64-pc-windows-msvc` on the workspace. It will not pass clean (see baseline: `sync_detect.rs` unix paths); the resulting `#[cfg]` gating PRs are core-side prerequisites of this issue and keep the mac test suite green.
1. The spike binds a **fixed probe surface**, not the whole API: `VaultSession` open/close (handle lifetime), a scan with `ScanProgressListener` (foreign callback, called from a Rust thread), `CancelToken` cancellation mid-scan, one `CommandAction` registration + invocation round-trip (host re-entry), one `VaultError`-returning call (error mapping), one `DocumentBuffer` create → `apply_edit` → read-back (the keystroke hot path).
2. Both candidates are evaluated against the same probe on the same runner: (a) **`uniffi-bindgen-cs`** (NordSecurity) in proc-macro mode — verify version compatibility with the workspace's uniffi-rs version first; incompatibility that requires pinning/downgrading uniffi is itself a finding; (b) **`csbindgen`** plus the hand-written C-ABI shim the probe requires — the shim LOC and its unsafe surface are the finding.
3. Scored dimensions (recorded in the PR, becomes gap_analysis evidence): correctness under the §W-E stress patterns (GC pressure, callback concurrency, cancel latency), generated-code ergonomics (exceptions vs result codes, IDisposable story), maintenance posture (upstream activity, uniffi version coupling), and "one FFI definition feeds all generators" (ADR 13 preference).
4. The spike runs on a **GitHub-hosted Windows runner** in a throwaway workflow; no `apps/slate-windows/` directory is created (parking discipline) — probe code lives in `examples/csharp-probe/` (mirrors the existing `examples/swift-cli/` precedent).
5. Deliverable: a decision record appended to this spec (§Decision, below, initially "OPEN") + the probe kept compiling in CI (it becomes the seed of W0-3's smoke tests).

- [ ] Probe surface bound under both candidates; stress patterns pass or the failure is characterized
- [ ] Decision recorded here + gap_analysis; losing path's evidence preserved
- [ ] Probe workflow green on windows runner

**§Decision: OPEN** (filled by W0-1).

## W0-2 · Scaffold: `apps/slate-windows/`, CI, CODEOWNERS — PR 2 *(absorbs #603)*

1. Create `apps/slate-windows/` per ADR 13: .NET solution (version: current LTS at execution, pinned here in the PR), `src/SlateWindows/` app project + `tests/SlateWindows.Tests/` xUnit project; no UI beyond a window that opens a vault via the W0-1 binding and prints scan progress (the "hello, core" proof).
2. AvalonEdit currency check (07 §4.2): record maintained fork/version + .NET compatibility in this PR's description; a negative finding here is a program-level alarm, not something to route around silently.
3. `windows.yml` path-filtered workflow (`apps/slate-windows/**` + `crates/**`): build core (x64 + ARM64 cross-compile check), generate bindings, build app, run tests. Runner selection recorded (GitHub-hosted default; Namespace only if Windows profiles exist by then).
4. `.github/CODEOWNERS` gains the `/apps/slate-windows/` line (owner until a platform maintainer exists).
5. `make regenerate-bindings` grows a platform dimension (e.g. `make regenerate-bindings PLATFORM=windows`) or a sibling target; `CONTRIBUTING.md` updated with the Windows local-dev path — including the **no-make story** (the Makefile and `scripts/*.sh` assume a unix shell; document the PowerShell/`dotnet` equivalents a Windows-only contributor actually runs). Contributors changing the FFI must regenerate **both** platforms' bindings-check in CI (a drift check that the generated C# compiles, mirroring the Swift bindings check).
6. SPDX convention extended to C#: `license-headers.yml` paths + `apply-license-header.py` scope gain `*.cs` (or the exclusion is recorded here as a decision) — otherwise the repo's header discipline silently stops at the new language boundary.

- [ ] Scaffold + hello-core app + xUnit harness
- [ ] `windows.yml` green incl. ARM64 build; CODEOWNERS; CONTRIBUTING + Makefile updated
- [ ] AvalonEdit + .NET pins recorded

## W0-3 · Full-surface binding + §W-E safety censuses — PR 3

1. Bind the **entire** `slate-uniffi` surface with the W0-1 winner; the generated binding is git-ignored like Swift's (ADR 13).
2. Port the probe's stress patterns into permanent §W-E censuses (xUnit, `[Trait("census", …)]`): handle lifetime under GC pressure (open/close/drop thousands of sessions + buffers, finalizer vs Dispose paths), callback concurrency (progress events during UI-thread-simulated load), cancellation latency (cancel mid-scan of a large fixture vault → bounded stop), error mapping totality (every `VaultError` arm reaches C# as a typed exception, none as a panic/abort).
3. `slate-cli` builds and its test suite runs on the Windows runner (program decision 19: this is the extent of Windows CLI scope). Path-handling test additions live core-side (decision 9) — file here only what the run exposes.
4. Host logging: C# sink installed for `host_logging` diagnostics; test proves non-fatal core diagnostics surface in the app log.

- [ ] Full binding compiles + smoke passes; §W-E censuses green under load
- [ ] `slate-cli` green on Windows runner
- [ ] Logging sink wired + tested

## W0-4 · Parity matrix + pinned budgets (at unpark) — PR 4

1. Generate `docs/plans/18_windows_port/parity_matrix.md` from the **shipped mac app**: command-registry dump (id, section, chord, spoken hotkey), leaf/panel/tab inventory, Settings surface walk, help-doc index, `slate.cli.v1` verb list, file-type handlers (`.md`/`.base`/`.canvas`/`.excalidraw` as shipped). Each row: surface · capability · consuming W issue · status. The generator script lands in `scripts/` so the matrix is re-runnable (matrix drift = re-run, diff, re-triage).
2. Pin §W-B keystroke budgets from the then-current `BENCHMARKS.md` mac baselines at 100 KB / 1 MB / 8 MB (mac p50 numbers + an explicitly-chosen marshalling allowance, recorded with rationale — not "same as mac" hand-waving).
3. Confirm entry criteria 1–3 held (T residual, P canonical graph representation, queue state); record the actual shipped-milestone set the matrix was generated against.
4. Feature-conditional rows (program §moving-target) for unshipped milestones are dropped with one-line notes.

- [ ] Matrix generated + committed + script re-runnable
- [ ] §W-B budgets pinned with rationale
- [ ] Entry-criteria snapshot recorded

---

## W0.5 · Pre-port canonicalization (mac-side, pre-unpark-eligible)

*Shared shape for all three: new core API → mac consumes it → censuses/tests prove mac behavior unchanged (or the intentional delta is listed in the PR) → the Swift original is deleted, not left as a fallback. These PRs are reviewed as mac PRs (swift-tests + a11y-check gates apply).*

### W0.5-1 · Command-palette ranking + recents → `slate-core` — PR 5

1. New core module (e.g. `crates/slate-core/src/palette.rs`) owning: fuzzy scoring (port `fuzzyScore`'s semantics or deliberately improve them — either way the **core becomes the reference**, with golden tests capturing ranking for a pinned query corpus), section ordering, recents blending policy, and the recents **store** (SQLite or the existing prefs-adjacent JSON — decided in the PR; the mac `CommandPaletteRecentsStore` file store is replaced, with a one-time migration).
2. FFI: a query→ranked-commands call on the existing registry surface; ranked results carry section grouping and match-range data for per-platform bolding.
3. Mac: `CommandPaletteModel` becomes a thin view model over the FFI; `fuzzyScore` and its tests move to Rust (goldens preserved); palette behavior tests stay green unchanged.

### W0.5-2 · Quick-switcher ranking → `slate-core` — PR 6

1. Same shape over file rows: `QuickSwitcherModel.score` semantics → core (one ranking engine if palette + switcher genuinely share semantics — decided by reading the two scorers side-by-side in the PR, not assumed), recents blending from the existing file-recents data.
2. Mac `QuickSwitcherModel` consumes the FFI; activation/model/view tests green unchanged.

### W0.5-3 · Canonical a11y-event vocabulary → `slate-core` — PR 7

1. Inventory pass over every `post(_:priority:)` call site (AnnouncementPosting.swift's implementors + callers: canvas announcer, palette/switcher filter counts, mutation announcements, embed/task/property events…) → a canonical `A11yEvent` enum in core: event kind, parameters, **message template**, priority. Message rendering (`event → String`) lives in core; chord placeholders render per-platform (decision 12).
2. Mac: `AnnouncementPosting` keeps the *poster* role but every trigger site posts a rendered `A11yEvent`. A census proves the mac announcement corpus (event → text) is unchanged against a recorded golden set (or deltas are itemized).
3. The vocabulary is the §W-D parity anchor: Windows `RaiseNotificationEvent` will consume the same events with the same rendered text.
4. Scope guard: this issue **moves** strings, it does not redesign verbosity policy (canvas verbosity settings etc. keep their semantics; their strings just get canonical IDs).

- [ ] (each) core API + FFI; mac consumes; Swift logic deleted; goldens/censuses prove parity

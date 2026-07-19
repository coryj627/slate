# W0 executable spec — Foundation: the C# binding, the scaffold, and the pre-port canonicalization (W0.5)

Issues: W0-1 ([#714](https://github.com/coryj627/slate/issues/714)) · W0-2 ([#603](https://github.com/coryj627/slate/issues/603)) · W0-3 ([#715](https://github.com/coryj627/slate/issues/715)) · W0-4 ([#716](https://github.com/coryj627/slate/issues/716)) · W0.5-1 ([#717](https://github.com/coryj627/slate/issues/717)) · W0.5-2 ([#718](https://github.com/coryj627/slate/issues/718)) · W0.5-3 ([#719](https://github.com/coryj627/slate/issues/719)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 1–5, 16–17; DoD §W-E). Grounding: [`../../07_portability_review.md`](../../07_portability_review.md) §4.1 (the binding asymmetry), [`../../13_repo_structure.md`](../../13_repo_structure.md) (scaffold conventions).

**Execution order: W0-1 → W0-2 → W0-3 · W0.5-1 ∥ W0.5-2 ∥ W0.5-3 (independent, any time) · W0-4 at unpark.** *(Ordering corrected 2026-07-12: W0-3's xUnit censuses, Windows-runner CI, and app-log sink live in artifacts W0-2 creates — the previous `{ W0-2 ∥ W0-3 }` parallelism was unexecutable.)*
**Pre-unpark eligibility:** W0-1 and all three W0.5 issues may be worked while W is parked (program §Entry criteria). W0-2/W0-3/W0-4 wait for unpark.

Baseline facts (verified 2026-07-06; re-verified + refreshed 2026-07-12 — anchors below are current at `9192ca5`):

- The FFI is **proc-macro uniffi** (`#[uniffi::export]` / `#[derive(uniffi::Object)]`), no `.udl` file — `crates/slate-uniffi/src/lib.rs`; workspace uniffi **0.31** (the version-compat input for the spike). Generator compatibility must be evaluated against proc-macro mode, not UDL mode.
- Objects with Arc lifetime: `VaultSession` (lib.rs:291), `CancelToken` (lib.rs:1129), `DocumentBuffer` (lib.rs:3422), `CommandRegistry` (lib.rs:4272).
- **Three** foreign-callback traits (`#[uniffi::export(with_foreign)]`): `ScanProgressListener` (lib.rs:1976), `VaultEventListener` (lib.rs:2077 — `on_error`/`on_file_change`/`on_index_phase`; added post-authoring by the O follow-ups, PRs #791/#846), and `CommandAction` (lib.rs:4223). All three must marshal in the spike — `CommandAction` is how command invocation re-enters the host (the W5 hot path), and `VaultEventListener` is a **sustained background-thread, multi-method callback**: the hardest marshalling case of the three. Standing host obligation (any platform): install a `VaultEventListener` and a `host_logging` sink at startup, as the mac host does.
- Host logging facade: `crates/slate-uniffi/src/host_logging.rs` (#674) — non-fatal diagnostics route through it; the C# host must install a sink like the mac host does.
- **`slate-core` has never been compiled for Windows**, but its own code appears cfg-clean already: the `sync_detect.rs` `OsStrExt` uses (:197, :226, :767) sit inside platform-gated fns with `#[cfg(not(unix))]` fallbacks (landed with Milestone M, PRs #634/#635), `libc` is a `cfg(unix)` dependency (`slate-core/Cargo.toml:78`), and `vault/fs.rs:417` already carries a `cfg(windows)` arm. The genuinely unverified surface is **everything under `x86_64-pc-windows-msvc`** — no CI has ever built any Windows target, so both the dependency tree *and* any remaining first-party target-specific code are unproven; the *named* blockers above are simply already gated. *(Correction 2026-07-12: this bullet previously predicted `#[cfg(windows)]` work in `sync_detect.rs`; that gating was already in place at authoring.)*
- License-header gate covers `.rs`/`.swift` only (`.github/workflows/license-headers.yml` paths; `scripts/apply-license-header.py` scopes `git ls-files '*.rs' '*.swift'`) — `.cs` files are currently invisible to it.
- Bindings are generated + git-ignored; `make regenerate-bindings` currently delegates to `./scripts/build-mac-app.sh --bindings-only` (Makefile:60) — mac-only today; W0-3 generalizes it.
- CI is per-area path-filtered workflows (`.github/workflows/`: `rust.yml`, `swift-tests.yml`, `a11y-check.yml`, `audit.yml`, `license-headers.yml`).
- Swift-only logic pockets for W0.5 (the 07 §3 drift rows, plus one shipped after that review):
  - Palette ranking: `CommandPaletteModel.fuzzyScore(query:target:)` (CommandPaletteModel.swift:302) + section ordering + recents policy (`CommandPaletteRecentsStore` load/save/add/remove, CommandPaletteRecentsStore.swift:28).
  - Quick-switcher ranking: `QuickSwitcherModel.score(query:row:)` (QuickSwitcherModel.swift:195) + recents blending in `load(files:recents:)` (:91).
  - Announcements: `AnnouncementPosting` protocol (AnnouncementPosting.swift:30) + `AnnouncementPriority` (:9) is only the *poster*; the trigger conditions and message strings are scattered across the **`postAccessibilityAnnouncement` call expressions — 126 across 28 files** as of 2026-07-12 (`rg -o 'postAccessibilityAnnouncement\(' apps/slate-mac/Sources/SlateMac` = 127 in 28 files, minus the WelcomeView.swift:186 definition), plus the direct `post(_:priority:)` sites — which include the poster implementation and internal forwarding, i.e. only a handful of *independent* triggers. Scope the W0.5-3 inventory to the call-expression set, and **re-run the query at execution time** rather than trusting this count.

---

## W0-1 · Binding spike: `uniffi-bindgen-cs` vs `csbindgen` shim — PR 1

**Question the spike answers:** which path gives Slate a C# binding with correct object lifetime, foreign callbacks, error mapping, and cancellation, at the lowest sustained maintenance cost — judged on evidence, not doctrine.

### Normative rules

0. **First act:** `cargo check`/`cargo test` for `x86_64-pc-windows-msvc` on the workspace. The previously named blockers are already cfg-gated (see baseline); the first msvc build may still surface dependency-tree failures **or** remaining first-party target gaps — treat either as a spike finding, and any `#[cfg]` gating PRs that prove necessary are core-side prerequisites of this issue and keep the mac test suite green.
1. The spike binds a **fixed probe surface**, not the whole API: `VaultSession` open/close (handle lifetime), a scan with `ScanProgressListener` (foreign callback, called from a Rust thread), a `VaultEventListener` subscription that receives all three event kinds across an operation (sustained multi-method callback), `CancelToken` cancellation mid-scan, one `CommandAction` registration + invocation round-trip (host re-entry), one `VaultError`-returning call (error mapping), one `DocumentBuffer` create → `apply_edit` → read-back (the keystroke hot path).
2. Both candidates are evaluated against the same probe on the same runner: (a) **`uniffi-bindgen-cs`** (NordSecurity) in proc-macro mode — verify version compatibility with the workspace's uniffi-rs version first; incompatibility that requires pinning/downgrading uniffi is itself a finding; (b) **`csbindgen`** plus the hand-written C-ABI shim the probe requires — the shim LOC and its unsafe surface are the finding.
3. Scored dimensions (recorded in the PR, becomes gap_analysis evidence): correctness under the §W-E stress patterns (GC pressure; callback concurrency across **all three** traits incl. `VaultEventListener`'s three methods; listener registration/unregistration lifetime; `CommandAction` success *and* error round-trips; cancel latency), generated-code ergonomics (exceptions vs result codes, IDisposable story), maintenance posture (upstream activity, uniffi version coupling), and "one FFI definition feeds all generators" (ADR 13 preference).
4. The spike runs on a **GitHub-hosted Windows runner** in a throwaway workflow; no `apps/slate-windows/` directory is created (parking discipline) — probe code lives in `examples/csharp-probe/` (mirrors the existing `examples/swift-cli/` precedent).
5. Deliverable: a decision record appended to this spec (§Decision, below, initially "OPEN") + the probe kept compiling in CI (it becomes the seed of W0-3's smoke tests).

- [x] Probe surface bound under both candidates; stress patterns pass or the failure is characterized
- [x] Decision recorded here + gap_analysis; losing path's evidence preserved
- [ ] Probe workflow green on windows runner *(lane added; first green run lands with this PR's CI)*

**§Decision: `uniffi-bindgen-cs`** *(recorded 2026-07-18; evidence gathered on native Windows 11 ARM64, rustc 1.95.0 `aarch64-pc-windows-msvc`, .NET 10.0.10, debug builds; x64 twin runs in `.github/workflows/csharp-probe.yml`)*

Both candidates bound the full rule-1 probe surface and passed all ten probe sections, including every §W-E stress pattern — the decision is made on cost structure, not capability. Probes: `examples/csharp-probe/` (winner) and `examples/csharp-probe/{shim,ShimProbe}/` (counter-candidate), identical section/assertion structure so the two evidence blocks diff cleanly.

Scored dimensions (rule 3):

1. **Correctness under §W-E stress — tie.** Both candidates: three-kind `VaultEventListener` delivery in one session (incl. `on_error` from a genuinely failed background compaction — read-only oplog ⇒ the rewrite's rename-over fails with Windows semantics, dispatched from the compaction worker thread), scan-progress choreography exact (1 Started / N monotonic FileIndexed / 1 terminal Finished, dispatched inline on the scanning thread), mid-scan cancel in 0–2 ms with the terminal `Cancelled` event observed, `CommandAction` success + error + registry re-entry without deadlock, GC pressure over thousands of handles with no native fault (Dispose-during-in-flight-scan included), 400-cycle listener register/unregister churn with foreign handles released (399/400 collectable after GC in both). Debug-build `apply_edit` round-trip: 112 µs/edit (uniffi) vs 101 µs/edit (raw P/Invoke) — ~10% per-call overhead difference, re-measured properly against release-build §W-B budgets at W0-4.
2. **Generated-code ergonomics — uniffi.** uniffi lifts all 17 `VaultError` variants as typed exception subclasses with structured fields, nested enums (`ScanProgress`, `EditorSpanKind` incl. `Code(TokenKind)`) as full type hierarchies, records/`IDisposable` objects throughout; foreign exceptions map back typed (`CommandException.ActionFailed` round-trips, message truncation marker intact), and an *untyped* C# exception escaping a callback surfaces as a catchable `PanicException` — no abort. The shim path flattens errors to `(status code, display string)` — typed fields survive only where an out-param was hand-plumbed (`WriteConflict` in the probe; every further variant is bespoke work), action-error messages ride a hand-chosen fixed 1 KiB buffer, the span array is hand-marshalled with the nested token enum collapsed, and the exception fence inside every `[UnmanagedCallersOnly]` trampoline is app code — forgetting one is UB, not an exception.
3. **Maintenance posture — uniffi, decisively.** The probe slice alone (a sliver of the surface W0-3 binds in full) cost the counter-candidate **821 lines of hand-written boundary Rust (61 `unsafe` occurrences) + 667 lines of hand-written C# wrapper**, against **133 generated extern lines**; uniffi generated **22,922 lines covering the entire current FFI with zero hand-written binding code**. Under the shim every FFI change is a three-file hand edit (core → shim → wrapper) plus re-derived free/lifetime contracts (the probe needed a delayed-free reaper just to dodge the unregister/in-flight-dispatch race that uniffi's handle map owns internally). uniffi's coupling cost is real but bounded and *pinned*: the bindgen tag must match the workspace's uniffi minor (v0.11.0+v0.31.0 ↔ uniffi 0.31, verified exact); upstream (NordSecurity) tracks uniffi releases actively, and a stale-generator scenario degrades to "hold the uniffi upgrade," not "hand-port the delta."
4. **One FFI definition feeds all generators (ADR 13) — uniffi.** The same proc-macro definitions generate Swift and C#; one generator-driven core-side constraint surfaced (a record field PascalCasing into its enclosing type name, CS0542 — fixed by the `Locator.value` rename, Swift-inert, this PR). The shim is by construction a second, hand-maintained definition of the surface.

Consequences for W0-2/W0-3: the binding assembly compiles the generated file with `AllowUnsafeBlocks` (LibraryImport marshalling); `uniffi-bindgen-cs` installs are pinned to the tag matching the workspace uniffi and upgraded in lockstep with it; CSharpier is optional (formatter warning only). Losing path's evidence is preserved in-tree (`shim/` + `ShimProbe/`, both exercised by the throwaway workflow) until W0-3 supersedes the probe with the full-surface §W-E censuses; the winner probe seeds those censuses per rule 5.

## W0-2 · Scaffold: `apps/slate-windows/`, CI, CODEOWNERS — PR 2 *(absorbs #603)*

1. Create `apps/slate-windows/` per ADR 13: .NET solution (version: current LTS at execution, pinned here in the PR), `src/SlateWindows/` app project + `tests/SlateWindows.Tests/` xUnit project; no UI beyond a window that opens a vault via the W0-1 binding and prints scan progress (the "hello, core" proof).
2. AvalonEdit currency check (07 §4.2): record maintained fork/version + .NET compatibility in this PR's description; a negative finding here is a program-level alarm, not something to route around silently.
3. `windows.yml` path-filtered workflow (`apps/slate-windows/**` + `crates/**`): build core (x64 + ARM64 cross-compile check), generate bindings, build app, run tests, and **`dotnet format --verify-no-changes`** (the program's `dotnet format` pre-push promise becomes an enforced gate here; the local command goes in CONTRIBUTING per item 5). Runner selection recorded (GitHub-hosted default; Namespace only if Windows profiles exist by then).
4. `.github/CODEOWNERS` **already carries** the `/apps/slate-windows/` line (`.github/CODEOWNERS:19`, added ahead of time) — verify rather than re-add; update the owner only when a platform maintainer exists.
5. `make regenerate-bindings` grows a platform dimension (e.g. `make regenerate-bindings PLATFORM=windows`) or a sibling target; `CONTRIBUTING.md` updated with the Windows local-dev path — including the **no-make story** (the Makefile and `scripts/*.sh` assume a unix shell; document the PowerShell/`dotnet` equivalents a Windows-only contributor actually runs). Contributors changing the FFI must regenerate **both** platforms' bindings-check in CI (a drift check that the generated C# compiles, mirroring the Swift bindings check).
6. SPDX convention extended to C#: `license-headers.yml` paths + `apply-license-header.py` scope gain `*.cs` (or the exclusion is recorded here as a decision) — otherwise the repo's header discipline silently stops at the new language boundary.

- [x] Scaffold + hello-core app + xUnit harness
- [x] `windows.yml` green incl. ARM64 build; CODEOWNERS verified (`.github/CODEOWNERS:19`, pre-existing); CONTRIBUTING + Makefile updated *(lane added; first green run lands with this PR's CI)*
- [x] AvalonEdit + .NET pins recorded *(2026-07-19: .NET 10 — current LTS — pinned via `apps/slate-windows/global.json` (`10.0.100`, rollForward latestFeature); AvalonEdit currency check: 6.3.1.120, released 2025-04-13 by icsharpcode/grunwald, targets net462 + net6.0-windows + net8.0-windows with computed net10.0-windows compatibility — actively maintained, no program-level alarm. SPDX convention extended to `.cs`; csharp-probe.yml retires at W0-3 per the §Decision evidence-preservation clause, not here.)*

## W0-3 · Full-surface binding + §W-E safety censuses — PR 3

1. Bind the **entire** `slate-uniffi` surface with the W0-1 winner; the generated binding is git-ignored like Swift's (ADR 13).
2. Port the probe's stress patterns into permanent §W-E censuses (xUnit, `[Trait("census", …)]`): handle lifetime under GC pressure (open/close/drop thousands of sessions + buffers, finalizer vs Dispose paths), callback concurrency (progress **and vault events** during UI-thread-simulated load), cancellation latency (cancel mid-scan of a large fixture vault → bounded stop), error mapping totality (every `VaultError` arm reaches C# as a typed exception, none as a panic/abort).
3. `slate-cli` builds and its test suite runs on the Windows runner (program decision 19: this is the extent of Windows CLI scope). Path-handling test additions live core-side (decision 9) — file here only what the run exposes.
4. Host logging: C# sink installed for `host_logging` diagnostics; test proves non-fatal core diagnostics surface in the app log.
5. **§W-A harness skeleton lands here:** the serialize → artifact → diff scaffolding, run over the probe fixtures (with its mac twin), so W2-2/W3/W4 acceptance rows have a harness to run against from their first PR. Includes a **minimal general-Markdown fixture set** (headings/lists/links/tags/code/math/embeds — `tests/fixtures/` today covers only `{bases, canvas, dql, oplog}`) sufficient for the editor-span/structure/search/backlink rows. W8-4 completes and hardens it (full read-side surface, full corpus, exhaustive normalization list, two-platform CI wiring).

- [ ] Full binding compiles + smoke passes; §W-E censuses green under load
- [ ] `slate-cli` green on Windows runner
- [ ] Logging sink wired + tested
- [ ] §W-A skeleton harness runs on the probe fixtures (mac twin included)

## W0-4 · Parity matrix + pinned budgets (at unpark) — PR 4

1. Generate `docs/plans/18_windows_port/parity_matrix.md` from the **shipped mac app**: command-registry dump (id, section, chord, spoken hotkey — driven via the mac app/test target: the registry is populated at runtime by the host, so `slate-cli` cannot emit it), leaf/panel/tab inventory, Settings surface walk, help-doc index, `slate.cli.v1` verb list, file-type handlers (`.md`/`.base`/`.canvas`/`.excalidraw` as shipped). Each row: surface · capability · consuming W issue · status. The generator script lands in `scripts/` so the matrix is re-runnable (matrix drift = re-run, diff, re-triage).
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

1. New core module (e.g. `crates/slate-core/src/palette.rs`) owning fuzzy scoring (port `fuzzyScore`'s semantics or deliberately improve them — either way the **core becomes the reference**, with golden tests capturing ranking for a pinned query corpus), section ordering, recents blending policy, ordered-recents state transitions (add/remove/dedupe/cap), and the versioned serialization shape. **Filesystem location and atomic file I/O stay in thin host adapters** because these are global, device-local app recents — never per-vault prefs or SQLite. The mac adapter migrates/continues `~/Library/Application Support/Slate/command-palette-recents.json`; Windows uses `%LOCALAPPDATA%\Slate\command-palette-recents.json`. Swift ranking/recency policy is deleted; platform-path plumbing is not reclassified as core product logic.
2. FFI: a query→ranked-commands call on the existing registry surface; ranked results carry section grouping and match-range data for per-platform bolding.
3. Mac: `CommandPaletteModel` becomes a thin view model over the FFI; `fuzzyScore` and its tests move to Rust (goldens preserved); palette behavior tests stay green unchanged.

### W0.5-2 · Quick-switcher ranking → `slate-core` — PR 6

1. Same shape over file rows: `QuickSwitcherModel.score` semantics → core (one ranking engine if palette + switcher genuinely share semantics — decided by reading the two scorers side-by-side in the PR, not assumed), recents blending from the existing file-recents data.
2. Mac `QuickSwitcherModel` consumes the FFI; activation/model/view tests green unchanged.

### W0.5-3 · Canonical a11y-event vocabulary → `slate-core` — PR 7

1. Inventory pass over every announcement site — the vocabulary lives behind **`postAccessibilityAnnouncement` (≈126 call expressions across 28 files as of 2026-07-12; re-run `rg -o 'postAccessibilityAnnouncement\('` at execution; defined at WelcomeView.swift:186)** plus the direct `post(_:priority:)` sites (canvas announcer, palette/switcher filter counts, mutation announcements, embed/task/property events…) → a canonical `A11yEvent` enum in core: event kind, parameters, **message template**, priority. Message rendering (`event → String`) lives in core; chord placeholders render per-platform (decision 12).
2. Mac: `AnnouncementPosting` keeps the *poster* role but every trigger site posts a rendered `A11yEvent`. A census proves the mac announcement corpus (event → text) is unchanged against a recorded golden set (or deltas are itemized).
3. The vocabulary is the §W-D parity anchor: Windows `RaiseNotificationEvent` will consume the same events with the same rendered text. The recorded golden set produced here **is** §W-D's "canonical a11y-event corpus" — it does not pre-exist this issue.
4. Scope guard: this issue **moves** strings, it does not redesign verbosity policy (canvas verbosity settings etc. keep their semantics; their strings just get canonical IDs).
5. Trigger ownership stays at the interaction sites: core defines and renders typed events, while the mac and Windows hosts decide when those events fire. Existing mac scenario tests plus the Windows FlaUI twins prove trigger parity; neither host may invent alternate text.

- [ ] (each) core API + FFI; mac consumes; Swift logic deleted; goldens/censuses prove parity

# W8 executable spec — Settings, theming, packaging & parity close-out

Issues: W8-1 ([#751](https://github.com/coryj627/slate/issues/751)) · W8-2 ([#752](https://github.com/coryj627/slate/issues/752)) · W8-3 ([#753](https://github.com/coryj627/slate/issues/753)) · W8-4 ([#754](https://github.com/coryj627/slate/issues/754)) · W8-5 ([#755](https://github.com/coryj627/slate/issues/755)) · W8-6 ([#756](https://github.com/coryj627/slate/issues/756)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 10, 11, 15, 16, 20; DoD §W-A/§W-B/§W-F/§W-G).

**Execution order: { W8-1 ∥ W8-2 ∥ W8-3 } → W8-4 → W8-5 → W8-6.** (The §W-A harness **skeleton is a W0-3 deliverable** — w0 spec, item 5 — which W2-2/W3/W4 rows run against; this issue hardens and completes it: full read-side surface, exhaustive normalization list, two-platform CI wiring.)

## W8-1 · Settings & prefs — PR 1

1. Settings UI parity over the same prefs data (`PrefsJsonStore` JSON shape is the cross-platform contract — source of truth: `apps/slate-mac/Sources/SlateMac/PrefsJsonStore.swift` + its tests; this PR lands a written schema doc beside this spec so the contract stops living only in Swift), incl. math/code prefs, editor-intelligence bucket (V/X toggles if shipped), canvas verbosity, **history retention (O's `set_history_prefs` tab)**, appearance. A synced vault's prefs read identically on both platforms.
2. Windows-only settings section (theme/high-contrast behavior, file associations) — additive keys, mac-ignorable.

## W8-2 · Theming & contrast — PR 2

1. Token-based dark/light theme plus reactive `SystemParameters.HighContrast` switching; system font-size respect (per-monitor DPI already in W1-1). Contrast-theme resources bind semantic roles to compatible dynamic Windows `SystemColors` pairs — no hard-coded Contrast-theme colors — and update while the app is running. **Mechanism (decision 2 addendum, 2026-07-19): the Fluent theme dictionaries** — dark/light via `Fluent.Light/Dark.xaml` driven by Slate theme state (seeded W1-1 item 8), Contrast via the automatic `Fluent.HC.xaml` switch; this issue finalizes the token values over that base, hardens the reactive-switching tests, and re-runs the Fluent currency check (experimental upstream — adopt `ThemeMode` here iff it has exited experimental).
2. **APCA Lc ≥ 75 gate ported for Slate-owned dark/light pairs**: the shared contrast spec (R milestone's shared-spec artifact, or its Swift-test predecessor translated) runs over those WPF token pairs in CI. Contrast-theme acceptance instead verifies all four built-in Windows themes plus a user-customized theme, compatible system foreground/background pairings, selected/disabled states, visible boundaries, and no color-only meaning; user-controlled system colors are not an APCA gate.

## W8-3 · MSIX packaging + auto-update — PR 3

1. Signed MSIX, x64 + ARM64; auto-update channel; file-type associations (`.base`/`.canvas` register; `.md` optional per user choice); jump-list recents; single-instance handoff verified from shell launches.
2. Uninstall semantics are unambiguous: vault data is never touched by uninstall; the prefs location is documented, with its retain-vs-remove behavior stated.

## W8-4 · Differential parity harness (§W-A) — PR 4

1. Three-job CI pipeline: mac + windows jobs run the same fixture corpus (`crates/slate-core/tests/fixtures/**` — today only `{bases, canvas, dql, oplog}`; the editor-span/structure/search/backlink rows need new Markdown fixtures or the bench generators, added under this issue) plus a generated vault (the existing generators: `crates/slate-core/benches/common/mod.rs` — `generate_vault`/`generate_tasks_vault`; **deterministic by construction, parameterized only by `file_count` — no seed knob exists today**: add one here if corpus variation is wanted, else pin the fixed corpus) through every read-side FFI surface, emit canonical serializations; a diff job compares. Normalization list (path separators; upstream-designated engine-dependent surfaces, e.g. PD OCR text per program decision 4/§W-A) lives here and is exhaustive. Line endings are deliberately **not** normalized: LF, CRLF, and mixed-ending fixtures must preserve identical bytes and produce identical bytes after the same edit sequence (decision 9); anything not on the list must match byte-for-byte.
2. Runs on every PR touching `crates/**` or either app's consumption layer (path filters); failure is release-blocking from the moment it lands.

## W8-5 · Performance gates (§W-B) — PR 5

1. BenchmarkDotNet keystroke-path suite at 100 KB / 1 MB / 8 MB against the W0-4 pinned budgets; flatness assertion (no size-correlated growth beyond profile); scan/index first-open marshalling overhead measured. Numbers land in `BENCHMARKS.md` with runner class, release-gated.

## W8-6 · Docs, E2E, matrix close-out — PR 6

1. Help docs per decision 20 (shared prose, per-platform chord tables); Windows onboarding in `CONTRIBUTING.md`.
2. E2E authoring-loop suite (the T E2E precedent, cross-surface: vault open → edit → panels → canvas → search → save/undo chain) via FlaUI.
3. §W-F: `parity_matrix.md` at zero (every row shipped or owner-waived-with-reason). §W-G audit recorded — mechanically: a dependency deny-list step in `windows.yml` that fails on any WebView2/webview package in the solution's lock file/manifest, plus a committed grep-audit (script in `scripts/`) over `apps/slate-windows/` for re-implemented core logic. The human JAWS + NVDA full pass is the release residual (mirrors T's convention — recorded, and the milestone stays open until done).

- [ ] (each) gates green; W8-6 closes the milestone **only after** the human JAWS + NVDA residual is recorded

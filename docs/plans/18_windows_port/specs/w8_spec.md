# W8 executable spec — Settings, theming, packaging & parity close-out

Issues: W8-1 ([#751](https://github.com/coryj627/slate/issues/751)) · W8-2 ([#752](https://github.com/coryj627/slate/issues/752)) · W8-3 ([#753](https://github.com/coryj627/slate/issues/753)) · W8-4 ([#754](https://github.com/coryj627/slate/issues/754)) · W8-5 ([#755](https://github.com/coryj627/slate/issues/755)) · W8-6 ([#756](https://github.com/coryj627/slate/issues/756)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 10, 11, 15, 16, 20; DoD §W-A/§W-B/§W-F/§W-G).

**Execution order: { W8-1 ∥ W8-2 ∥ W8-3 } → W8-4 → W8-5 → W8-6.** (W8-4's harness should exist much earlier in skeleton form — W2-2 and W4 reference §W-A rows; this issue hardens and completes it.)

## W8-1 · Settings & prefs — PR 1

1. Settings UI parity over the same prefs data (`PrefsJsonStore` JSON shape is the cross-platform contract — source of truth: `apps/slate-mac/Sources/SlateMac/PrefsJsonStore.swift` + its tests; this PR lands a written schema doc beside this spec so the contract stops living only in Swift), incl. math/code prefs, editor-intelligence bucket (V/X toggles if shipped), canvas verbosity, appearance. A synced vault's prefs read identically on both platforms.
2. Windows-only settings section (theme/high-contrast behavior, file associations) — additive keys, mac-ignorable.

## W8-2 · Theming & contrast — PR 2

1. Token-based theme: dark/light + `SystemParameters.HighContrast` reactive switching; system font-size respect (per-monitor DPI already in W1-1).
2. **APCA Lc ≥ 75 gate ported**: the shared contrast spec (R milestone's shared-spec artifact, or its Swift-test predecessor translated) runs over the WPF token pairs in CI, both appearances + high-contrast — measured, not eyeballed.

## W8-3 · MSIX packaging + auto-update — PR 3

1. Signed MSIX, x64 + ARM64; auto-update channel; file-type associations (`.base`/`.canvas` register; `.md` optional per user choice); jump-list recents; single-instance handoff verified from shell launches.
2. Install/uninstall leaves no vault data behind ambiguities (prefs location documented).

## W8-4 · Differential parity harness (§W-A) — PR 4

1. Two-job CI pipeline: mac + windows jobs run the same fixture corpus (`crates/slate-core/tests/fixtures/**`) plus a **seeded** randomized vault (the existing generators: `crates/slate-core/benches/common/mod.rs` — `generate_vault`/`generate_tasks_vault`, given a fixed seed) through every read-side FFI surface, emit canonical serializations; a diff job compares. Normalization list (path separators etc.) lives here and is exhaustive — anything not on it must match byte-for-byte.
2. Runs on every PR touching `crates/**` or either app's consumption layer (path filters); failure is release-blocking from the moment it lands.

## W8-5 · Performance gates (§W-B) — PR 5

1. BenchmarkDotNet keystroke-path suite at 100 KB / 1 MB / 8 MB against the W0-4 pinned budgets; flatness assertion (no size-correlated growth beyond profile); scan/index first-open marshalling overhead measured. Numbers land in `BENCHMARKS.md` with runner class, release-gated.

## W8-6 · Docs, E2E, matrix close-out — PR 6

1. Help docs per decision 20 (shared prose, per-platform chord tables); Windows onboarding in `CONTRIBUTING.md`.
2. E2E authoring-loop suite (the T E2E precedent, cross-surface: vault open → edit → panels → canvas → search → save/undo chain) via FlaUI.
3. §W-F: `parity_matrix.md` at zero (every row shipped or owner-waived-with-reason). §W-G audit recorded. The human JAWS + NVDA full pass is the release residual (mirrors T's convention — recorded, and the milestone stays open until done).

- [ ] (each) gates green; W8-6 closes the milestone modulo the human AT residual

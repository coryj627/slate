# 18 — Windows Port Program (Milestone W): the same core, a second native witness

**Status:** 📝 Specs drafted (2026-07-06); implementation **parked** until the entry criteria below are met. GH [milestone 22](https://github.com/coryj627/slate/milestone/22), issues [#714–#756](https://github.com/coryj627/slate/milestone/22) (W0: #714–#719 + #603 · W1: #720–#723 · W2: #724–#727 + #381 · W3: #728–#732 · W4: #733–#740 · W5: #741–#744 · W6: #745–#746 · W7: #747–#750 · W8: #751–#756). Pre-existing tracked issues absorbed by this program: [#603](https://github.com/coryj627/slate/issues/603) (repo onboarding → W0-2) and [#381](https://github.com/coryj627/slate/issues/381) (AvalonEdit consumes canonical spans → W2-2).
Authority chain: `../05_locked_architecture_decisions.md` (§1.1–1.3 a11y doctrine, §2.2/§2.3/§2.5 stack + FFI, §3 platform order, §5.4 Windows, §6.4 UIA, §7.1 editor model) → [`../07_portability_review.md`](../07_portability_review.md) (the reuse/a11y review this program executes) → [`../13_repo_structure.md`](../13_repo_structure.md) (monorepo ADR) → the [milestone 22 description](https://github.com/coryj627/slate/milestone/22) (vendored verbatim: [`01_milestone_brief.md`](01_milestone_brief.md)) → this program. **Every deliberate divergence from the milestone text or the portability review is recorded in [`specs/gap_analysis.md`](specs/gap_analysis.md)**; the program supersedes both where they differ (`../06_v1_milestones.md` rule).

**Strategic goal.** Ship the Windows app at **100% capability parity** with the macOS app *as it exists at port start*, on the **same `slate-core` Rust backend** — zero re-implementation of Markdown classification, structure, vault, search, citations, tasks, properties, query, canvas, graph, or ranking logic in C# — hosted in **WPF + AvalonEdit** with full **UIA** accessibility such that **JAWS and NVDA get the same semantics VoiceOver gets**. The port is the first second witness to the "one core, many native frontends" architecture: it either proves the §1.1–1.2 doctrine (accessibility artifacts produced in Rust, consumed per-platform) or exposes where the mac app quietly cheated. Every place the mac app kept logic in Swift is a port cost paid twice — which is why W0.5 pushes the known cases down into `slate-core` *before* the port begins.

Everything here inherits the Presentation-Ready DoD (`../08_ui_parity/00_program.md` §A–§G) in spirit; where a §A–§G item is macOS-tooling-specific (a11y-check, APCA measurement harness, VoiceOver passes), the Windows equivalent is pinned in decision 11 and §W-C below. One PR per issue, fmt/clippy pre-push (Rust) and `dotnet format` pre-push (C#), censuses for correctness invariants.

---

## The moving-target problem, solved structurally

W is sequenced **after** the majority of the standing queue (N Bases, O local history, P graph, R themes, S explain-function, V autocomplete, X LaTeX aids, FL files sidebar, XD Excalidraw viewer) ships on macOS. The parity surface therefore **cannot be frozen in this document**. The program handles that in three ways:

1. **Workstreams are organized by architectural consumption pattern, not by feature list** — shell/workspace (W1), editor surface (W2), content rendering (W3), panels & data surfaces (W4), commands/search/templates (W5), structural surfaces (W6), UIA program (W7), close-out (W8). A mac feature that ships between now and port start lands as **new rows in an existing workstream**, never a new phase.
2. **The parity matrix is generated, not written** (W0-4): at port start, an inventory pass over the shipped mac app — command registry dump, leaf/panel/tab inventory, Settings surface, help docs, `slate.cli.v1` surface — produces `parity_matrix.md`, the row-level checklist every W issue burns down. §W-F gates close-out on it.
3. **Feature-conditional rows:** specs reference milestone-shipped capabilities (e.g. "Bases grid", "graph view") conditionally — if a milestone is descoped or unshipped at port start, its rows drop out of the matrix with a one-line note, and nothing else in the program moves.

## Entry criteria (the unpark gate)

W remains parked until **all** of:

1. **Milestone T residual closed** — the human AT smoke pass on canvas is recorded (GH milestone 20 closes).
2. **Milestone P shipped with the graph's accessible textual representation canonical in Rust** (07 §3 / §5.4 — this is the single most expensive thing to get wrong before a port; the milestone-22 description makes it an explicit pre-condition).
3. **The standing queue is majority-shipped** — owner call at unpark time; the parity matrix (W0-4) absorbs whatever the actual state is.
4. **W0.5 canonicalization landed** (palette ranking, quick-switcher ranking, announcement vocabulary — spec w0 §W0.5; these are mac-side refactors, verifiable with today's mac test suite, and are **explicitly allowed to be worked before unpark**).
5. **W0-1 binding spike concluded** (also pre-unpark-eligible: it needs a Windows CI runner and `crates/slate-uniffi`, not a Windows app).

Items 4–5 are deliberately front-loadable: they de-risk the port while the mac queue finishes, without violating the parking decision (no WPF code, no `apps/slate-windows/` app work).

---

## Locked scope decisions

| # | Area | Decision |
|---|------|----------|
| 1 | Identity | **Milestone W — Windows port.** Specs in `docs/plans/18_windows_port/`; phase prefixes W0–W8 (W0 includes the pre-port W0.5 block). App dir `apps/slate-windows/` per ADR 13 (one native app per `apps/slate-<platform>/`; shares **only** `crates/slate-uniffi`; never reaches into `apps/slate-mac/`). |
| 2 | Host stack | **WPF, not WinUI 3** (05 §5.4: AvalonEdit's 15+ years of UIA hardening with JAWS/NVDA; churn is the enemy of screen-reader reliability). **AvalonEdit** is the editor surface under declarative WPF chrome. **No webview anywhere** (05 §1.3 prohibition — includes WebView2; §W-G audits it). .NET version: **current LTS at port start**, pinned in W0-2 alongside an AvalonEdit maintenance/compat currency check (07 §4.2). |
| 3 | Binding | The C# binding is **the #1 reuse risk** (07 §4.1) and is decided by **spike, not doctrine** (W0-1): `uniffi-bindgen-cs` (NordSecurity) vs hand-written `csbindgen` shim, judged on the **callback + object-handle + cancellation** patterns (`VaultSession`/`DocumentBuffer` Arc lifetimes, `ScanProgressListener` foreign callback, `CancelToken`) — not on free functions. Preference order if the spike ties: the generator that keeps **one FFI definition feeding all platforms** (ADR 13 §Consequences). The losing path is recorded in gap_analysis with the evidence. |
| 4 | Zero re-implementation | **No C# implementation of anything `slate-core` produces**: Markdown/syntax classification (spans come from the #377/#381 span API), structure snapshots, vault scan/index, FTS, citations, tasks, properties, Bases query execution, canvas model/apply, graph model/metrics, palette/switcher ranking, announcement text. Enforced twice: the **differential parity census** (§W-A, machine-checked) and PR review against a pinned "C# may contain" list (UI state machines, view models, UIA peers, marshalling, platform I/O adapters — and nothing else). |
| 5 | Pre-port canonicalization (W0.5) | Push the three known Swift-only logic pockets into `slate-core` before the port: **command-palette fuzzy ranking + recents policy** (`CommandPaletteModel`), **quick-switcher ranking** (`QuickSwitcherModel` — shipped after the portability review, same drift), **a11y announcement vocabulary** (trigger conditions + strings behind `postAccessibilityAnnouncement`). Each lands as: core API + mac consumes it + census/tests prove mac behavior unchanged. Windows then *consumes*, never re-derives. (07 §3 table rows Q + cross-cutting; ADR 13 explicitly lists these as pre-Windows work.) |
| 6 | UIA doctrine | Same canonical artifacts, second consumer (05 §1.2/§6.4): editor semantic spans → **custom `AutomationPeer` ranges** with semantic descriptions; math → MathML + **MathCAT** speech behind a math peer; grids → **WPF DataGrid** (UIA-native, 05 §8.7 behavior matrix); announcements → **`AutomationPeer.RaiseNotificationEvent`** fed by the W0.5 canonical vocabulary (same trigger, same text as macOS). **JAWS + NVDA are the reference ATs; Narrator is smoke-only.** Heading navigation is *not* ported from the mac outline workaround — Windows ATs navigate headings natively via UIA; the outline sidebar ships as a feature, not as the a11y crutch (07 §3 row B). |
| 7 | Math & diagrams | Math renders via **WPFMath/xaml-math** from the canonical `{LaTeX, MathML, speech, braille}` artifact (05 §6.2); the 2–4-week math-peer budget from 05 §5.4 is planned as its own issue (W3-2), not absorbed. Mermaid/diagrams render the **canonical Rust-produced SVG** via **SharpVectors** with the canonical description as the accessible name — the JS mermaid engine is never run on Windows (K pipeline doctrine; 07 §3 "Exemplary"). Code blocks render from canonical `{source, syntax_tokens, semantic_spans}`. |
| 8 | Editor semantics | AvalonEdit's `TextDocument` is a **view**; the Rust `DocumentBuffer` rope is the source of truth. Keystrokes → edit deltas → `DocumentBuffer` (same delta feed as mac); spans return via the #381 span API into a `DocumentColorizingTransformer`; AvalonEdit never re-tokenizes. The mac drift-guard invariant (buffer-vs-editor divergence detection) gets a C# twin census (§W-E). Undo/redo routes through the core op-log exactly as the mac editor does. |
| 9 | Vault & paths | Same vault, byte-identical semantics, via the existing `VaultProvider` seam. Windows-specific path handling (drive letters, `\\?\` long paths, reserved names `CON`/`NUL`/…, case-insensitive-case-preserving semantics, CRLF discipline — Slate writes LF, tolerates CRLF on read per existing core behavior) is **adapter-level in the provider, never in UI code**. Sync detection: the M-1 provider registry gains **OneDrive/Dropbox marker probes on Windows** (07 §3 sync note); the *watcher* equivalent of `SyncMarkerWatcher` is per-platform by design (`FileSystemWatcher`, bounded scope like #638). |
| 10 | Performance | The shared keystroke path must stay **flat O(edit) through C# marshalling** (§W-B): p50 budgets pinned at W0-4 from the then-current `BENCHMARKS.md` mac baselines (same fixture corpus: 100 KB / 1 MB / 8 MB), release-gated. Scan/index first-open budgets: same numbers as mac (they're core-side; the C# cost is marshalling only, measured). |
| 11 | A11y tooling gate | macOS's a11y-check has no Windows twin, so pin: **Accessibility Insights for Windows automated checks (axe-windows engine) = 0 failures** on every shipped surface, run in CI via **FlaUI-driven** smoke scenarios; plus the §W-C UIA conformance matrix (control type, Name, patterns, notifications per surface) reviewed at each wave close; plus human JAWS + NVDA passes as the release residual (mirrors T's human-AT-residual convention). APCA Lc ≥ 75 applies to the WPF theme tokens, both appearances + `SystemParameters.HighContrast` (07 §3 row R: the check is a shared spec by then — W consumes it). |
| 12 | Commands & input | The command registry, sections, and ranking are core-side (W0.5). Windows maps chords by **platform convention** (⌘→Ctrl, ⌥→Alt), declared in one table in W5-1 — no per-view ad-hoc bindings; access-key (`_File`) menus per WPF convention; spoken-hotkey strings come from the canonical vocabulary with per-platform chord substitution. The three command-drift tests (registration forward, menu-scrape reverse, help-table) get Windows twins. |
| 13 | Data surfaces | Bases grid, tasks, properties, citations tables, canvas table, local-history lists all render on **one accessible grid substrate** (W4-1): WPF DataGrid wrapped to the 05 §8.7 behavior matrix (headers announced on entry, cell-by-cell arrows, keyboard sort/filter, row actions, summary row addressable, CSV/Markdown export commands) — the `AccessibleDataGrid` v2 role, played by the platform-native control. Feature grids are thin configurations of it, exactly like mac. |
| 14 | Structural surfaces | Canvas (T parity) and Graph (P parity) port as **consumers of the canonical structural representations**: canvas scene/outline/table + `canvas_apply` FFI; graph's accessible textual representation (entry criterion 2). The mac interaction model (mode stack, Esc-commits, navigator commands, announcer grammar) is the **behavioral spec**; W6 re-hosts it in WPF with UIA equivalents. If the canonical layer turns out to be missing anything (i.e. the mac app derived it in Swift), that gap goes **into core first** (a W6-blocking core issue), never into C#. |
| 15 | Windows-only obligations | First-class citizens, not afterthoughts: single-instance activation + file-type association (`.md` optional, `.base`/`.canvas` registered), taskbar jump list (recent vaults), Windows IME correctness in AvalonEdit (CJK composition — smoke-tested with at least one IME), High Contrast themes (not just dark/light), per-monitor-v2 DPI awareness. Each has a parity-matrix row and a W issue home (w1/w2/w8 specs). |
| 16 | Packaging | **Signed MSIX**, x64 + ARM64, with auto-update. CI: path-filtered `windows.yml` (pattern: existing per-area workflows) building core → bindings → app → tests on a Windows runner; runner selection (GitHub-hosted vs Namespace, if Windows profiles exist by then) decided in W0-2 — **not load-bearing for any other decision**. |
| 17 | Testing stack | Rust censuses stay the correctness backbone (they're host-independent). C#: **xUnit** for unit/VM tests, **FlaUI** for UIA automation smoke, the §W-A differential harness for cross-platform equivalence, BenchmarkDotNet for §W-B marshalling numbers. No new Rust test conventions. |
| 18 | l10n | Out of W scope; `../14_l10n.md` owns string externalization. W introduces no new user-facing strings in core (the vocabulary work in W0.5 *moves* strings; net-new strings live in WPF resources so l10n can find them later). |
| 19 | Not in W | iOS/Android (later milestones per 05 §3); any Obsidian-parity feature not shipped on mac at port start (the matrix defines truth); plugin/extension APIs (Milestone EX); a Windows CLI re-package (`slate-cli` already builds and tests on Windows in W0-3 — distribution beyond that is out of scope). |
| 20 | Docs & help | Every W4/W6 surface that has a mac help doc gets the same doc with per-platform chord tables (source shared, chords substituted — no forked prose). The AT smoke checklist convention (T's `at_smoke_checklist.md`) is replicated for JAWS/NVDA per surface in W7-4. |

**Reserved enhancements (each a future decision, no W issue):** W-E1 Windows Narrator full support beyond smoke · W-E2 Windows Search/indexer integration · W-E3 share-target/context-menu shell extensions · W-E4 portable (non-MSIX) distribution · W-E5 Windows-side CLI distribution/packaging · W-E6 touch/pen interaction pass (keyboard + AT is the v1 contract).

---

## Phase map, waves & dependencies

```
Wave 0 (pre-unpark OK)  W0-1 binding spike ─▶ W0-3 full binding + CI
                        W0-2 scaffold/CI/CODEOWNERS (needs W0-1 verdict)
                        W0.5-1 palette ranking→core ∥ W0.5-2 switcher ranking→core ∥ W0.5-3 announcement vocabulary→core
                        W0-4 parity matrix + budgets (at unpark)
Wave 1 (shell)          W1-1 app shell + vault lifecycle ─▶ W1-2 files sidebar ─ W1-3 workspace tabs/splits/leaves ─ W1-4 quick switcher
Wave 2 (editor)         W2-1 AvalonEdit⇄DocumentBuffer host ─▶ W2-2 span consumer (#381) ─▶ W2-3 in-editor interactions ─ W2-4 autocomplete* ─ W2-5 LaTeX aids*
Wave 3 (content)        W3-1 reading view ─ W3-2 math + math peer ─ W3-3 diagrams/SVG ─ W3-4 code blocks ─ W3-5 embeds + Excalidraw viewer*
Wave 4 (panels/data)    W4-1 grid substrate ─▶ W4-2 link/outline/embed panels ─ W4-3 tasks ─ W4-4 properties ─ W4-5 citations ─ W4-6 Bases* ─ W4-7 local history* ─ W4-8 sync diagnostics
Wave 5 (commands)       W5-1 palette + chord table ─ W5-2 search overlay ─ W5-3 templates ─ W5-4 file mgmt / bulk rename
Wave 6 (structural)     W6-1 canvas ─ W6-2 graph*
Wave 7 (UIA program)    W7-1 editor peer ─ W7-2 notifications ─ W7-3 spoken hotkeys + AT nav model ─ W7-4 JAWS/NVDA matrix + checklists
Wave 8 (close-out)      W8-1 settings/prefs ─ W8-2 theming/contrast ─ W8-3 MSIX ─ W8-4 differential harness ─ W8-5 perf gates ─ W8-6 docs/E2E/matrix close
```
\* = feature-conditional row (ships iff the mac milestone shipped; see "moving-target").

| Wave | Issues | Gate |
|------|--------|------|
| 0 — Foundation | W0-1 → { W0-2 ∥ W0-3 } · W0.5-1/2/3 independent · W0-4 last | W0-1, W0.5-* may run **pre-unpark**; W0-4 runs at unpark (it snapshots the real mac surface) |
| 1 — Shell | W1-1 → { W1-2 ∥ W1-3 ∥ W1-4 } | Wave 0 complete |
| 2 — Editor | W2-1 → W2-2 → { W2-3 ∥ W2-4 ∥ W2-5 } | W1-1 (a window to host in) |
| 3 — Content | { W3-1..W3-5 } mostly parallel | W2-1 (buffer host); W3-5 embeds need W3-1 |
| 4 — Panels/data | W4-1 → { W4-2..W4-8 } | W1-3 (leaves to dock into); W3 for embedded content |
| 5 — Commands | { W5-1..W5-4 } | W1 shell; W0.5 ranking (for W5-1) |
| 6 — Structural | { W6-1 ∥ W6-2 } | W4-1 (canvas table/grid), W5-1 (navigator commands surface) |
| 7 — UIA program | W7-1 with W2; W7-2/3 with W5; W7-4 rolling, closes each wave | interleaved — each wave's close requires its W7 rows green |
| 8 — Close-out | W8-1..W8-6 | everything; §W-F needs the matrix at zero |

**Priority note:** if capacity forces a cut line inside a wave, cut feature-conditional rows (*) last-in-first-out and record the waiver in the matrix (§W-F). W8-4/W8-5/W8-6 are never cut — without the differential harness and perf gates the acceptance story collapses to "trust us."

---

## Definition of Done — W-specific deltas

*Census convention: as repo-standard (`census_*` fns, `SLATE_CENSUS_FULL=1`; see `../17_bases/00_program.md` DoD preamble). New here: censuses that compare **two platforms' outputs** run in CI as a two-job pipeline (mac job + windows job emit serialized artifacts; a comparison job diffs them).*

- **§W-A (differential parity census):** over the shared fixture corpus (and a generated randomized vault), the serialized outputs of every read-side FFI surface — editor spans, structure snapshots, search results, backlinks, task/property/citation rows, Bases result sets, canvas scenes/outlines, graph textual representation — are **byte-identical between macOS and Windows CI**. Any intentional platform difference (path separators in display strings) is normalized by the harness and the normalization list is part of the spec (w8).
- **§W-B (keystroke budget):** editor keystroke p50 through the C# binding stays within the pinned budget at 100 KB / 1 MB / 8 MB, and stays **flat** (no size-correlated growth beyond the mac profile). Recorded in `BENCHMARKS.md` with runner class, per repo convention.
- **§W-C (UIA conformance):** per-surface matrix rows (control type, Name/HelpText source, patterns, focus order, notifications) implemented + axe-windows 0 failures in CI + JAWS/NVDA human smoke recorded per surface before its wave closes.
- **§W-D (announcement parity):** census over the canonical a11y-event corpus — for every event, mac NSAccessibility announcement text == Windows `RaiseNotificationEvent` text (chord substitutions aside), and both fire on the same triggers.
- **§W-E (binding safety):** C#-side censuses — handle lifetime under GC pressure (no leak, no use-after-free), `CancelToken` mid-scan cancellation, concurrent `ScanProgressListener` callbacks, buffer-vs-editor drift guard — green under load, mirroring the Rust census discipline.
- **§W-F (matrix zero):** every `parity_matrix.md` row is shipped-and-checked or owner-waived-with-reason at W8-6. No silent rows.
- **§W-G (doctrine audit):** dependency audit proves no WebView2/webview; the "C# may contain" review gate (decision 4) has been applied to every PR; a final grep-audit pass over `apps/slate-windows/` for re-implemented core logic is recorded.

Specs: [w0 (foundation + W0.5)](specs/w0_spec.md) · [w1 (shell/workspace)](specs/w1_spec.md) · [w2 (editor)](specs/w2_spec.md) · [w3 (content rendering)](specs/w3_spec.md) · [w4 (panels & data)](specs/w4_spec.md) · [w5 (commands/search/templates)](specs/w5_spec.md) · [w6 (canvas & graph)](specs/w6_spec.md) · [w7 (UIA program)](specs/w7_spec.md) · [w8 (settings, packaging, parity close-out)](specs/w8_spec.md) · [gap analysis](specs/gap_analysis.md)

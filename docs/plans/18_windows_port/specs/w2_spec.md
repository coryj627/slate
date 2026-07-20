# W2 executable spec — Editor surface: AvalonEdit over the shared `DocumentBuffer`

Issues: W2-1 ([#724](https://github.com/coryj627/slate/issues/724)) · W2-2 ([#381](https://github.com/coryj627/slate/issues/381)) · W2-3 ([#725](https://github.com/coryj627/slate/issues/725)) · W2-4* ([#726](https://github.com/coryj627/slate/issues/726)) · W2-5* ([#727](https://github.com/coryj627/slate/issues/727)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue. *(\* feature-conditional: W2-4 iff Milestone V shipped, W2-5 iff Milestone X shipped.)*
Program: [00_program.md](../00_program.md) (decisions 4, 8, 10, 15; DoD §W-A/§W-B/§W-E). Grounding: `../../07_portability_review.md` §2 (the convergence finding this spec cashes in), #404/#407 buffer architecture (stateful `DocumentBuffer`, delta feed, clean-break reconvergence, rope-native windows), #379 windowed highlighting.

**Execution order: W2-1 → W2-2 → { W2-3 ∥ W2-4 ∥ W2-5 }.**

**W0/W1 execution baseline (2026-07-19 refresh — facts the original spec predates):**

- **The buffer hot path through the shipped binding — what is actually proven:** `DocumentBuffer` exports `new`/`apply_edit`/`reset`/`len_utf16`/`byte_to_utf16`/`highlight_in_range` — **no content read-back exists**. The permanent W0-3 censuses exercise `ApplyEdit`/`LenUtf16`; `ByteToUtf16` and `HighlightInRange` were probe-exercised in W0-1 (evidence in the w0 §Decision) and get permanent census coverage with W2-1. W0-1 measured the whole uniffi `apply_edit` round-trip at ~112 µs/edit (debug). `editor_highlight_spans(_in_range)` and the text offset free functions are bound and `public`. **Drift-guard reality check:** the shipped mac Tier-1 guard is *length-only* (`lenUtf16` vs store length, `reset` re-sync; the release guarantee is the Rust census suite) — item 3's full-text compare needs **#966** (`content_hash()`/snapshot FFI, a pre-unpark-executable core prerequisite) and W2-1 consumes it.
- **§W-B budgets are pinned, not pending** (W0-4 `parity_matrix.md` §W-B): p50 ≤ 0.5 ms (100 KB), ≤ 0.5 ms (1 MB), ≤ 1.0 ms (8 MB), flatness p50(8 MB) ≤ 4× p50(1 MB). W2-1's "first numbers" and W2-2's BenchmarkDotNet run are recorded **against those numbers**.
- **The §W-A skeleton already serializes editor spans** (and headings/blocks/search/links) over the `tests/fixtures/markdown/` corpus — incl. CRLF and mixed-ending fixtures — with committed goldens both platforms diff (`parity_golden/`, W0-3). W2-2's §W-A span rows **extend that harness and corpus** (editor-scale fixtures, windowed-request coverage), they do not build a new mechanism; serialization rules live in `CanonicalJson.cs` + the Swift twin, changed only together.
- **C# census conventions** (W0-3): `[Trait("census", …)]`, `CensusTier` moderate/full tiers, serialized test assembly, `Support/` recorders — W2-1's drift-guard and edit-storm censuses (§W-E) follow them.
- **Fluent theme (program decision 2 addendum):** AvalonEdit draws its own text surface — Fluent restyles the *chrome around it* (scrollbars, context menus, find UI, the W2-4 completion window). Editor text colors come from the W1-1 Slate tokens (which own every text-bearing surface and carry the two-layer Contrast behavior); the Mica policy from W1-1 item 8 applies — the editor surface always sits on a solid token-backed background. §W-C editor-chrome assertions run against the Fluent templates.
- **V/X status at the W0-4 snapshot:** unshipped (matrix dropped-rows table) — W2-4/W2-5 activate only if V/X ship before port start; re-run the matrix generator at wave start to re-check.

- `DocumentBuffer` (slate-uniffi lib.rs:3422; anchor current 2026-07-12) is the stateful editor backend: edit deltas in, spans/structure out, O(edit) (BENCHMARKS: 8 MB keystroke ≈ 245 µs core-side). The mac consumers to mirror: `NoteEditorView` coordinator (delta feed + drift guard + windowed `applyHighlight`), `EditorSpanMapping` (UTF-16 ↔ byte offset mapping), `EditorTextConversions`.
- The release guarantee is census-side, not assertion-side: buffer-vs-stateless, comment-index, and structure censuses — the C# host must not weaken this (its drift guard twin is §W-E).
- AvalonEdit specifics: its `TextDocument` has its own offset model (UTF-16); `DocumentColorizingTransformer` applies per-line visual styling during render — the natural consumer for windowed span requests (visible range + margin, the mac windowing strategy).

## W2-1 · AvalonEdit ⇄ DocumentBuffer host — PR 1

1. Editor host: AvalonEdit `TextEditor` wired so every text change produces an edit delta to `DocumentBuffer` (single keystrokes, IME composition commits, paste, drag, undo/redo). AvalonEdit is the *view*; the rope is truth (decision 8).
2. Offset discipline: one mapping module (UTF-16 code units ⇄ byte offsets), property-tested against multibyte/astral fixtures — the C# twin of `EditorSpanMapping` (same fixture corpus: the ASCII/2-byte/3-byte(中)/astral(😀) cases documented in `EditorSpanMappingTests.swift:15`; §W-A row).
3. Drift guard, two automatic tiers — no undefined "suspect" trigger: (a) the per-edit **length guard** (the mac Tier-1 mechanism, cheapest); (b) an **automatic content-hash compare** via the **#966** `content_hash()` FFI on the debounced highlight/idle cadence **and unconditionally before every save** — so a silently dropped same-length delta is caught within one cadence and can never reach disk. On mismatch: reconverge (buffer `reset` from the editor text, the truth re-assert path) **before** the save proceeds, and count the event (a drift census metric, §W-E). **Revision-gated ordering:** the editor snapshot/hash, the buffer hash (and any `reset`), and the save-snapshot acquisition execute under one serialized gate against a **monotonic editor revision** — in WPF, one dispatcher section over the document revision counter; if the revision moves between compare and save acquisition, the check redoes (or the save aborts and reschedules), and hash/reset errors fail the save closed, never fall through. Censuses: (a) silently inject a length-preserving divergence with **no** flag set → automatic detection within one cadence + reset-before-save; (b) inject a flag-free same-length edit **between** the compare and the save acquisition → the save never persists unverified content (redo or abort observed); alongside the randomized edit storms. *(Corrected 2026-07-19 twice: the original "buffer read-back vs `TextDocument`" was unimplementable — no read-back FFI, and the mac guard it cited is length-only; the first correction's "on-suspect" compare had no defined trigger.)*
4. Undo/redo is focus-routed across the same three domains as mac: AvalonEdit's `UndoStack` owns note-editor text undo (the analogue of the responder-chain `NSUndoManager`); canvas and structural file operations keep their separate core-backed undo stacks. Ctrl+Z/Ctrl+Y text mutations must travel through the same AvalonEdit-change → `DocumentBuffer` delta feed as typing, then save through the normal core save/op-log pipeline — there is no invented core text-undo API.
5. Save flow: debounce/save-state parity with the mac typing-save flow (dirty tracking, atomic write via core).
6. IME: CJK composition correctness smoke (decision 15) — composition events must not feed partial deltas.

- [ ] Delta feed + offsets + drift guard censuses green (incl. edit storms, IME)
- [ ] Undo/save parity; §W-B first numbers recorded (pre-optimization)

## W2-2 · Canonical span consumer (#381 — the reuse payoff) — PR 2

1. `DocumentColorizingTransformer` renders from windowed span requests to `DocumentBuffer` (visible range + margin; re-request on scroll/resize/edit — the mac windowing model). **Zero C# tokenization** (§W-G).
2. Span kinds map to the editor theme palette (same role taxonomy as `EditorSyntaxPalette`); theme values come from the **provisional token set seeded in W1-1** — W8-2 finalizes and contrast-gates those tokens later, it does not first create them.
3. Per-keystroke recompute stays inside the **pinned** §W-B budgets (0.5 / 0.5 / 1.0 ms p50 at 100 KB / 1 MB / 8 MB, flatness ≤ 4× — `parity_matrix.md` §W-B); BenchmarkDotNet run recorded against them.
4. §W-A row: span streams for the fixture corpus byte-identical mac↔windows — extend the shipped W0-3 harness (its per-file `spans` artifact already covers the base corpus; add editor-scale fixtures and windowed-request coverage to both twins + goldens in the same PR).
5. Semantic span data is retained on the host for W7-1 (the UIA peer consumes the same window the colorizer paints).

- [ ] Colorizer over windowed spans; zero tokenization; budgets green
- [ ] §W-A span rows green

## W2-3 · In-editor interactions — PR 3

1. Parity set (matrix rows): wikilink follow (Ctrl+Click + keyboard command) with anchor/subpath handling via core resolution; tag activation → search scope; citation hover/popover data; embed affordances (expansion per the shipped embed state machine); checkbox toggles writing through core; code-fence and math-region behaviors as shipped on mac at port start.
2. Every interaction consumes core APIs already exercised by mac (link resolution, embeds, tasks) — no C# re-derivation (§W-G).

- [ ] Interaction parity rows green incl. keyboard-only paths; §W-C editor rows via FlaUI

## W2-4 · Autocomplete (Milestone V parity)* — PR 4

1. Consumes V's core completion engine (providers, ranking, trigger model) — the WPF completion window is chrome (Fluent-styled per decision 2 addendum; §W-C list/keyboard assertions run against the Fluent templates); V's acceptance semantics (incl. its a11y announcement contract, which V ships via the canonical vocabulary) hold verbatim. *(That premise is an obligation on Milestone V — recorded as gap row G15 and filed on V as [#888](https://github.com/coryj627/slate/issues/888). The W1-4 switcher-count correction, #963, is the cautionary precedent: verify V's announcements actually render through core before consuming them.)*

## W2-5 · LaTeX authoring aids (Milestone X parity)* — PR 5

1. Consumes X's core engines (snippets, guarded concealment, bypass invariant). X's toggle gates only the aids; K-milestone rendering stays default — same on Windows.
2. Guarded concealment interacts with the colorizer (W2-2) — the same span-role contract X pinned for mac applies.

- [ ] (each*) matrix rows green; engine consumed, not re-implemented

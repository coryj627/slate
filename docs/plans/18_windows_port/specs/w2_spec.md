# W2 executable spec — Editor surface: AvalonEdit over the shared `DocumentBuffer`

Issues: W2-1 ([#724](https://github.com/coryj627/slate/issues/724)) · W2-2 ([#381](https://github.com/coryj627/slate/issues/381)) · W2-3 ([#725](https://github.com/coryj627/slate/issues/725)) · W2-4* ([#726](https://github.com/coryj627/slate/issues/726)) · W2-5* ([#727](https://github.com/coryj627/slate/issues/727)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue. *(\* feature-conditional: W2-4 iff Milestone V shipped, W2-5 iff Milestone X shipped.)*
Program: [00_program.md](../00_program.md) (decisions 4, 8, 10, 15; DoD §W-A/§W-B/§W-E). Grounding: `../../07_portability_review.md` §2 (the convergence finding this spec cashes in), #404/#407 buffer architecture (stateful `DocumentBuffer`, delta feed, clean-break reconvergence, rope-native windows), #379 windowed highlighting.

**Execution order: W2-1 → W2-2 → { W2-3 ∥ W2-4 ∥ W2-5 }.**

Baseline facts:

- `DocumentBuffer` (slate-uniffi lib.rs:2811) is the stateful editor backend: edit deltas in, spans/structure out, O(edit) (BENCHMARKS: 8 MB keystroke ≈ 245 µs core-side). The mac consumers to mirror: `NoteEditorView` coordinator (delta feed + drift guard + windowed `applyHighlight`), `EditorSpanMapping` (UTF-16 ↔ byte offset mapping), `EditorTextConversions`.
- The release guarantee is census-side, not assertion-side: buffer-vs-stateless, comment-index, and structure censuses — the C# host must not weaken this (its drift guard twin is §W-E).
- AvalonEdit specifics: its `TextDocument` has its own offset model (UTF-16); `DocumentColorizingTransformer` applies per-line visual styling during render — the natural consumer for windowed span requests (visible range + margin, the mac windowing strategy).

## W2-1 · AvalonEdit ⇄ DocumentBuffer host — PR 1

1. Editor host: AvalonEdit `TextEditor` wired so every text change produces an edit delta to `DocumentBuffer` (single keystrokes, IME composition commits, paste, drag, undo/redo). AvalonEdit is the *view*; the rope is truth (decision 8).
2. Offset discipline: one mapping module (UTF-16 code units ⇄ byte offsets), property-tested against multibyte/astral fixtures — the C# twin of `EditorSpanMapping` (same fixture corpus: the ASCII/2-byte/3-byte(中)/astral(😀) cases documented in `EditorSpanMappingTests.swift:15`; §W-A row).
3. Drift guard: periodic + on-suspect full-text compare (buffer read-back vs `TextDocument`) behind the same clean-break reconvergence semantics the mac coordinator uses; census under randomized edit storms (§W-E).
4. Undo/redo routes through the core op-log (parity with mac ⌘Z routing; Ctrl+Z/Ctrl+Y).
5. Save flow: debounce/save-state parity with the mac typing-save flow (dirty tracking, atomic write via core).
6. IME: CJK composition correctness smoke (decision 15) — composition events must not feed partial deltas.

- [ ] Delta feed + offsets + drift guard censuses green (incl. edit storms, IME)
- [ ] Undo/save parity; §W-B first numbers recorded (pre-optimization)

## W2-2 · Canonical span consumer (#381 — the reuse payoff) — PR 2

1. `DocumentColorizingTransformer` renders from windowed span requests to `DocumentBuffer` (visible range + margin; re-request on scroll/resize/edit — the mac windowing model). **Zero C# tokenization** (§W-G).
2. Span kinds map to the editor theme palette (same role taxonomy as `EditorSyntaxPalette`; theme values from W8-2 tokens).
3. Per-keystroke recompute stays inside the §W-B budget at all fixture sizes; BenchmarkDotNet run recorded.
4. §W-A row: span streams for the fixture corpus byte-identical mac↔windows (serialized via the harness).
5. Semantic span data is retained on the host for W7-1 (the UIA peer consumes the same window the colorizer paints).

- [ ] Colorizer over windowed spans; zero tokenization; budgets green
- [ ] §W-A span rows green

## W2-3 · In-editor interactions — PR 3

1. Parity set (matrix rows): wikilink follow (Ctrl+Click + keyboard command) with anchor/subpath handling via core resolution; tag activation → search scope; citation hover/popover data; embed affordances (expansion per the shipped embed state machine); checkbox toggles writing through core; code-fence and math-region behaviors as shipped on mac at port start.
2. Every interaction consumes core APIs already exercised by mac (link resolution, embeds, tasks) — no C# re-derivation (§W-G).

- [ ] Interaction parity rows green incl. keyboard-only paths; §W-C editor rows via FlaUI

## W2-4 · Autocomplete (Milestone V parity)* — PR 4

1. Consumes V's core completion engine (providers, ranking, trigger model) — the WPF completion window is chrome; V's acceptance semantics (incl. its a11y announcement contract, which V ships via the canonical vocabulary) hold verbatim.

## W2-5 · LaTeX authoring aids (Milestone X parity)* — PR 5

1. Consumes X's core engines (snippets, guarded concealment, bypass invariant). X's toggle gates only the aids; K-milestone rendering stays default — same on Windows.
2. Guarded concealment interacts with the colorizer (W2-2) — the same span-role contract X pinned for mac applies.

- [ ] (each*) matrix rows green; engine consumed, not re-implemented

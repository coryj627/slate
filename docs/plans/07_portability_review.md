# 07 — Portability & Accessibility Review (V1 milestones)

**Status (2026-05-30):** Review complete. Origin: the editor large-file
performance investigation, which surfaced that the editor implementation has
drifted from the locked architecture in a way that intersects performance, the
accessibility doctrine, and Windows portability. Editor follow-ups tracked as
`editor`-labelled issues [#374](https://github.com/coryj627/slate/issues/374)–[#381](https://github.com/coryj627/slate/issues/381).

**Scope:** the V1 milestones (A–R, per `06_v1_milestones.md`) reviewed through
two lenses — **accessibility** and **code reuse when porting to Windows**.

**One-paragraph summary.** Accessibility and Windows reuse are not gaps in this
project; they are locked founding axes (`05` §1.1–1.3, §3). The strategy is
sound — better than most projects ever articulate. The risk is not the plan; it
is **implementation drift from the plan at the editor**, plus an
**under-scoped C# binding layer**. Both are fixable now, cheaply, and both were
on a collision course with the large-file work.

---

## 1. The reuse boundary

The FFI line is the seam, and it is clean and deliberate:

| Layer | Windows fate |
|---|---|
| `slate-core` (Rust domain logic) | **Reused verbatim** |
| `slate-uniffi` (FFI surface) | Reused; **re-bound** for C# (see §4) |
| `slate-mac` (SwiftUI / AppKit) | **Full rewrite** — WPF, accepted per §1.3 |

`05` §1.3 explicitly accepts the 2–3× native-per-platform UI cost as the price
of stable accessibility (webview shells rejected: their a11y depends on browser
engine versions outside the project's control). The shipped split honours this:
domain logic (scan, index, parsers, FTS5, templates, query engine) genuinely
lives in Rust. **No action — affirm.**

The accessibility doctrine makes the boundary do double duty:

- **§1.1** — *Accessibility is owned by the data model and the Rust backend, not
  the UI. The UI consumes accessibility artifacts the backend produces; it does
  not generate them.*
- **§1.2** — one canonical structure in Rust, many accessible representations
  consumed per-platform. Math → `{LaTeX, mathml, speech, braille}`; code →
  `{source, syntax_tokens, semantic_spans}`.

When followed, this is the strongest possible position for a Windows port: the
a11y artifacts are already in the reusable layer.

---

## 2. The convergence finding — the editor

This is the spine of the review. Three things one would otherwise treat as
separate are a single drift:

- **§7.1 locks the editor model as a rope (`ropey`/`crop`) + a persistent op
  log.** Shipped: the Mac editor holds the document as a Swift `String` in
  **three copies** (`currentNoteText`, `savedBaselineText`, `NSTextStorage`),
  with a coarse one-entry-per-save op log.
- **§1.2 locks code blocks as canonical `{source, syntax_tokens,
  semantic_spans}` produced in Rust.** Shipped: syntax classification is **~21
  regex passes in Swift** (`EditorSyntaxSpans.swift`) plus an embed scan.

These are not cosmetic. They cause all three problems simultaneously:

1. **Performance** — the measured per-keystroke highlight cost:

   | doc size | lines | total/keystroke |
   |---:|---:|---:|
   | 100 KB | 2,185 | 6.5 ms |
   | 1 MB | 21,511 | 70.6 ms |
   | **2 MB** | **42,823** | **≈182 ms** |
   | 8 MB | 170,143 | ≈1,683 ms |

   Super-linear past ~1 MB; smooth-typing ceiling ≈100 KB; the whole-document
   re-highlight runs synchronously on the main thread on every keystroke.

2. **Accessibility doctrine** — syntax/semantic spans *are* an a11y artifact
   under §1.2/§6.4 (Windows exposes code semantics via a UIA `AutomationPeer`).
   Generating them in the UI layer is precisely the §1.1 anti-pattern.

3. **Windows reuse** — AvalonEdit must either reimplement all ~21 regex passes
   in C#, or consume a Rust span API **that does not exist yet**. (AvalonEdit's
   `DocumentColorizingTransformer` wants exactly such a span list.)

**Punchline.** The "Rust span API" is not a performance *option* to weigh
against a Swift-side fix — **it is the locked architecture** (§7.1 + §1.2). A
Swift-only highlight fix is a knowingly-throwaway macOS deviation. The Zed and
CotEditor references gathered during the investigation are the implementation
guide: Zed (`crates/rope`, `crates/language/src/syntax_map.rs`) for the rope +
incremental tree-sitter (with fenced-code **injection**, the exact case the
regex classifier punts on); CotEditor (`SyntaxController`,
`NSLayoutManager+SyntaxHighlight`) for the per-platform apply layer (temporary
attributes, debounce, background parse, range-scoped re-highlight).

**Tracked by:** [#377](https://github.com/coryj627/slate/issues/377) (keystone — canonical spans),
[#378](https://github.com/coryj627/slate/issues/378) (rope), [#379](https://github.com/coryj627/slate/issues/379) (incremental scoping),
[#380](https://github.com/coryj627/slate/issues/380) (retire Swift highlighter), [#376](https://github.com/coryj627/slate/issues/376) (interim Swift
stopgap — optional), [#375](https://github.com/coryj627/slate/issues/375) (benchmark regression guard),
[#381](https://github.com/coryj627/slate/issues/381) (Windows consumer — reuse payoff).

---

## 3. Milestone-by-milestone grading

**Clean** — logic canonical in Rust, UI is honest per-platform rework, no drift:
**A** (scan/index), **C** (links), **D** (properties parse/type-inference),
**E** (FTS5 — ports verbatim), **G** (tasks), **H** (templates), **L** (citation
formatting), **M** (CLI is 100% portable; sync detection mostly portable —
see note), **N** (query engine + AST).

**Exemplary** — the doctrine done right, ports for ~free: **K (content
pipelines).** Math → `{LaTeX, MathML, speech, braille}`, Mermaid →
`{source, svg, description}`, code → AT preamble — all produced in Rust per
§6.2–6.4; Windows consumes the same artifacts behind a UIA peer. **K is the
model every other milestone should look like.** *(Note 2026-10-30, plan 44
WB-21b, #1526: the preamble at the reader's verbosity — the first line, or
the whole block over the token stream — is core's sibling function
`code_block_preamble_with_verbosity`; Windows consumes it, and mac's
consumption is the follow-up.)*

**Drift / Windows-risk:**

| Milestone | Issue | Recommendation |
|---|---|---|
| **F — Editor** | The convergence drift (§2). | Move text model + spans to `slate-core` (§7.1/§1.2). Highest-leverage fix. |
| **Q — Command palette** | Shipped after `05`; fuzzy filter + registry live in Swift (`CommandPaletteModel`), not retrofitted to §1.2. | Push fuzzy-match + command registry to Rust so Windows reuses ranking/recents, not just re-skins them. |
| **B — Heading nav** | Heading *data* is canonical (good); "outline sidebar as the heading-nav surface" is a macOS workaround for `NSTextView` exposing one text element. | Don't assume it ports — Narrator navigates headings via UIA. Per-platform a11y design pass over shared heading-level data. |
| **P — Graph view** | Hardest a11y surface; `06` line 46 commits to "accessible-equivalents." Risk: building the navigable textual representation in Swift. | Produce the accessible graph representation in Rust (canonical) from the start, or Windows rebuilds it. |
| **R — Themes/contrast** | APCA Lc>75 is a sound reusable *policy*, but enforced only in the Swift test target (`APCAContrast.swift`). | Make the APCA check + token contrast-pairs a shared spec; OS-pref overrides (high-contrast/font-size, `05` line 681) stay per-platform but planned. |
| *(cross-cutting)* **Announcements** | `postAccessibilityAnnouncement` trigger logic + strings live in Swift. | Move toward a canonical a11y-event vocabulary so Windows UIA `RaiseNotificationEvent` fires the same notifications with the same text. |

**Sync detection (M) note:** iCloud-marker detection is macOS-specific; §7.2
lists OneDrive/Dropbox/Git, so the detection *registry* should be canonical with
per-platform provider probes — verify iCloud isn't the only code path.

---

## 4. Stress-testing two locked tech bets (2026)

### 4.1 The csbindgen ↔ UniFFI asymmetry — the #1 concrete reuse risk

`05` §2.3 pairs **uniffi-rs** (Swift/Kotlin) with **csbindgen** (C#). These are
not equivalent generators. UniFFI generates the *object model* (`VaultSession`
as an object with methods, Arc lifetime) **and** foreign callbacks
(`ScanProgressListener` via `with_foreign`) and the error-enum mapping — all for
free on Swift. **csbindgen is a raw P/Invoke generator**; it does not replicate
that high-level interface/callback codegen. On Windows we would hand-write the
C-ABI shim for: opaque-handle lifetime, the scan-progress callback marshalling
(Rust→C# function pointers / `GCHandle`), `VaultError` mapping, and the
`CancelToken`. The Mac side got all of that generated.

**Recommendations** (`05` §3.2 already plans "FFI smoke tests on Swift and C# in
months 0–3" — this sharpens it):

- Make the C# smoke test exercise the **callback + object-handle + cancellation**
  patterns specifically (not a free function) — that is exactly where csbindgen
  diverges and where a nasty surprise would hide.
- **Evaluate `uniffi-bindgen-cs` (NordSecurity) vs. csbindgen.** Slate's API is
  object- and callback-heavy — precisely what UniFFI's model handles and raw
  csbindgen does not. A UniFFI C# backend might collapse the shim layer to
  near-zero and keep *one* interface definition feeding all platforms. Worth a
  one-day spike before the choice ossifies. *(Verify current maintenance status
  of both.)*

### 4.2 WPF + AvalonEdit — sound, with a currency check

The a11y rationale (`05` §5.4: AvalonEdit's 15+ years of UIA hardening with
JAWS/NVDA; WinUI 3 has no equivalent accessible long-document editor) is exactly
right for an a11y-first product. **Churn is the enemy of screen-reader
reliability**, so WPF/AvalonEdit's stability is a feature, not a liability.
Caveats to verify *at port time*: WPF is in low-feature-investment mode at
Microsoft (stable, not evolving — fine here); AvalonEdit is community-maintained
(confirm recent commit activity + .NET 8/9 compatibility). Note AvalonEdit has
its own rope-like `TextDocument` and highlighting engine — another argument for
canonical spans in Rust (feed AvalonEdit's colorizer the same span list
`NSTextView` gets).

---

## 5. Non-editor follow-ups (to be filed separately)

These came out of the review but are **not** editor-scoped, so they are not in
the `editor` issue set. Suggested labels in brackets:

1. **De-risk the C# binding** — callback + handle + cancellation smoke test;
   spike `uniffi-bindgen-cs` vs. csbindgen. *[`backend`]* — §4.1.
2. **Command palette → Rust** — move fuzzy-match + command registry to canonical
   so Windows reuses it. *[`backend`, `swift-ui`]* — Milestone Q.
3. **APCA contrast as a shared spec** — not a Swift-test-only check.
   *[`a11y`, `test`]* — Milestone R.
4. **Graph accessible representation in Rust** — canonical navigable form from
   day one. *[`a11y`, `backend`]* — Milestone P.
5. **Accessible-event vocabulary** — canonical announcement triggers/text for
   UIA reuse. *[`a11y`]* — cross-cutting.
6. **Heading-nav per-platform a11y pass** — shared heading-level data, native
   navigation. *[`a11y`]* — Milestone B.
7. **Sync-detection registry** — ensure iCloud isn't the only path.
   *[`backend`]* — Milestone M.

---

## 6. Prioritized recommendations + issue index

1. **Treat the editor as the convergence point.** Land §7.1/§1.2 for real: rope
   + syntax/semantic spans in `slate-core`. One move fixes the 182 ms cliff,
   satisfies the a11y doctrine, and makes the editor portable.
   → [#377](https://github.com/coryj627/slate/issues/377) (keystone), [#378](https://github.com/coryj627/slate/issues/378), [#379](https://github.com/coryj627/slate/issues/379), [#380](https://github.com/coryj627/slate/issues/380). Optional stopgap: [#376](https://github.com/coryj627/slate/issues/376). Guard: [#375](https://github.com/coryj627/slate/issues/375).
2. **De-risk the C# binding now** (§4.1, §5.1).
3. **Retrofit Q to the doctrine** (§5.2).
4. **Make APCA a shared spec** (§5.3).
5. **For P / accessible query builder (§8.6) / accessible conflict diff (§7.3):**
   ensure the accessible representation is canonical/Rust from day one (§5.4).

Net: the strategy is strong. Protect it by closing the editor drift and
hardening the C# binding before the Windows port begins.

---

## References

- `05_locked_architecture_decisions.md` — §1.1–1.3 (a11y doctrine, native UI),
  §2.2–2.3 (per-platform UI, FFI tooling), §3 (platform order), §5.4 (Windows),
  §6.2–6.4 (content pipelines), §7.1 (editor model), §7.3 (accessible conflict
  resolution), §8.6–8.7 (accessible query builder / data grid).
- `06_v1_milestones.md` — milestone decomposition A–R.
- Editor issues: [#374](https://github.com/coryj627/slate/issues/374)–[#381](https://github.com/coryj627/slate/issues/381).
- Reference implementations: [CotEditor](https://github.com/coteditor/CotEditor)
  (TextKit apply layer), [Zed](https://github.com/zed-industries/zed) (rope +
  incremental tree-sitter).

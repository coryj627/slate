# V1 executable spec — Accessible surface: FFI, combobox popup, insertion & snippets, a11y closure

Issues: V1-1 ([#574](https://github.com/coryj627/slate/issues/574)) · V1-2 ([#575](https://github.com/coryj627/slate/issues/575)) · V1-3 ([#576](https://github.com/coryj627/slate/issues/576)) · V1-4 ([#577](https://github.com/coryj627/slate/issues/577)).
Milestone: [GH 29](https://github.com/coryj627/slate/milestone/29). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 5–7; DoD §V-A/§V-B). Inherits the U/P Presentation-Ready DoD (a11y-check 100/100, APCA Lc ≥ 75 both appearances).

**Execution order: V1-1 → V1-2 → V1-3 → V1-4.** Wave gate: V0-6 green (a real engine to surface).

## Baseline facts (verified 2026-07-04, this worktree)

- **Editor view:** `apps/slate-mac/Sources/SlateMac/NoteEditorView.swift` — `NoteEditorView: NSViewRepresentable` (~:34); `makeNSView` builds the `NSScrollView` + `SlateEditorTextView` (~:117) and sets `textView.delegate = context.coordinator` (~:172). `Coordinator: NSObject, NSTextViewDelegate, NSTextStorageDelegate` (~:287). Per-keystroke: `textStorage(_:didProcessEditing:)` (~:520–537) accumulates `dirtyRange` (~:527) and feeds `documentBuffer.applyEdit` (~:533); `textDidChange` (~:917–930) routes text back through the binding; `textViewDidChangeSelection → reportCaretLocation()` (~:904–915) with `onCaretUTF16Change` callback (~:85). `scheduleHighlight(debounced:)` debounces ~40 ms and dispatches off-main (~:610–651). `SlateEditorTextView: NSTextView` overrides `performKeyEquivalent(with:)` (~:1144; ⌘E embed preview ~:1152) — the seam for popup-open key ownership.
- **Caret rect:** position the popup via `NSTextView.firstRect(forCharacterAt:)` / `layoutManager.boundingRect(forGlyphRange:)` at `textView.selectedRange().location`.
- **uniffi conventions** (`crates/slate-uniffi/src/lib.rs`): record = `#[derive(Debug, Clone, PartialEq, uniffi::Record)]` + `From<core::T>` (~:24–46). `VaultSession` is `#[derive(uniffi::Object)]` (~:245) with `#[uniffi::export] impl` (~:250) and `#[uniffi::constructor]` (~:255). `DocumentBuffer` FFI object already exists (~:2560–2623: `apply_edit` ~:2577, `byte_to_utf16` ~:2600, `highlight_in_range` ~:2609). `RangedHighlight` (~:2527) / `EditorSpan` (~:2485) / `EditorSpanKind` (~:2438) are the record-shape precedent. Callback trait precedent `ScanProgressListener` `#[uniffi::export(with_foreign)]` (~:1579). **Bindings regenerate** via `scripts/build-mac-app.sh` (~:62–74): `cargo run -p slate-uniffi --bin uniffi-bindgen -- generate … --language swift`, copied into `apps/slate-mac/Sources/SlateMac/slate_uniffi.swift` + `…/slate_uniffiFFI/`.
- **Accessible list + announcements:** `AccessibleDataGrid.swift` — per-cell `.accessibilityLabel("<col>: <val>")` (~:82), `.accessibilityElement(children: .contain)` (~:55), summary `.isSummaryElement` (~:97). `CommandPaletteView.swift` — `List { Section {…} header: {…} }` registers rotor stops (~:179–191), `.accessibilityAddTraits(.isHeader)` (~:219), selection announced at `.medium` (~:117), initial-selection suppression via `isInitialLoad` (~:61–66). `ArrowKey` enum (up=126/down=125, ~:316–354), modifier pass-through mask (~:374), `isDismissKey()` keyCode 53 (~:405–437), `NSEvent.addLocalMonitorForEvents(matching: .keyDown)` (~:446–501), IME guard `hasMarkedText()` (~:453). `QuickSwitcherView.swift` — `.medium` selection + count announce (~:98/105), `lastKeyboardNavAt` hover/keyboard debounce (~:50–53). `AccessibilityExtensions.swift` — `.accessibilityIsSelected(_:)` helper (avoid empty `OptionSet`, #324) (~:55–62).
- **a11y-check gate:** `.github/workflows/a11y-check.yml` — `MIN_SCORE: 100` (~:38), 34 rules / 19 WCAG 2.2 criteria over `apps/slate-mac/Sources/SlateMac/`; any error fails.

---

## V1-1 · uniffi completion surface + bindings (#574) — PR 1

Records (mirror + `From` per convention):

```rust
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CompletionItem {
    pub label: String,
    pub replacement: String,          // literal text, or a snippet template if is_snippet
    pub replace_start_utf16: u32,
    pub replace_end_utf16: u32,
    pub kind: CompletionKind,         // uniffi::Enum: Word|Latex|LatexEnv|Callout|WikilinkTarget|Anchor|Tag|FrontmatterKey|FrontmatterValue|Custom
    pub detail: Option<String>,       // VoiceOver secondary text
    pub is_snippet: bool,
}
pub struct CompletionResponse {
    pub items: Vec<CompletionItem>,
    pub site: CompletionSiteKind,     // uniffi::Enum mirror of CompletionSite (payload flattened)
    pub replace_start_utf16: u32,     // common default range (per-item may override)
    pub replace_end_utf16: u32,
    pub audio_summary: String,        // pre-rendered; format below
}
```

`VaultSession` method (sync; caller dispatches off-main per the AppState pattern — never called on the main thread from Swift):

```rust
fn completions(&self, context_text: String, caret_utf16: u32, cfg: CompletionConfig) -> Result<CompletionResponse, VaultError>
```

- `context_text` is the current buffer text (or a bounded window around the caret sufficient for `max_look_back` + the enclosing frontmatter/link/callout span — the Swift side sends what the classifier needs; V1-2 decides the window). All offsets are **UTF-16** at the boundary (matches `DocumentBuffer` convention).
- `audio_summary` (normative): `"{n} suggestions. {first_label}, {first_kind}, 1 of {n}."` — e.g. `"5 suggestions. \\alpha, LaTeX, 1 of 5."`; `"No suggestions."` when empty; singular `"1 suggestion."`. `{first_kind}` is a human word per `CompletionKind` (Word→"word", WikilinkTarget→"link", Latex→"LaTeX", Tag→"tag", …).
- **No callback interface in V1** (query-at-keystroke; the Swift side already knows every edit it makes). A push/streaming listener is a V-next upgrade only if evidence shows the sync query is too slow at scale (V0-6 budgets say it won't be).

Regenerate bindings; add Swift binding **smoke tests** (call `completions` on a fixture session, assert shape + `audio_summary`). fmt/clippy clean.

## V1-2 · Accessible completion popup — combobox, key capture, announcements (#575) — PR 2

The milestone's a11y-critical core. New `apps/slate-mac/Sources/SlateMac/Completion/CompletionPopup*.swift` (view + controller + model), plus wiring in `NoteEditorView.Coordinator`.

**Structure (locked decision 5).** A borderless floating `NSPanel`/child view anchored at the caret rect (`firstRect(forCharacterAt:)`), **non-activating** — the `SlateEditorTextView` keeps first-responder and key focus. Accessibility:
- The text view advertises combobox semantics (it owns an expanded popup while suggestions are showing).
- The list is a listbox; each row is a per-item element with `.accessibilityLabel(item.label + (detail.map { ", \($0)" } ?? ""))`, `.accessibilityIsSelected(isSelected)` (the helper, not an empty-`OptionSet` ternary — #324), and a kind-derived hint.
- The list container uses `.accessibilityElement(children: .contain)`; row count is exposed so VoiceOver can say "i of n".

**Key ownership (locked decision 7).** While the popup is open, install a `.keyDown` `NSEvent` local monitor (the `CommandPaletteView` pattern): Up/Down move selection (wrap or clamp — match QuickSwitcher), Tab/Enter insert the selection (V1-3), Esc dismisses, PageUp/Down page. Modified chords (any of shift/ctrl/opt/cmd on an arrow) pass through to the text view; `hasMarkedText()` composition passes through untouched. When the popup is closed the monitor is removed — bare typing, arrows, Esc behave exactly as today. The monitor must be torn down on blur / selection-leaves-token / document switch to avoid capturing keys after dismissal.

**Trigger (locked decision 6).** Auto-trigger: on `textDidChange`, if the token length ≥ `minWordTriggerLength` and the site is completable, query `completions` off-main (reuse the `scheduleHighlight` off-main dispatch discipline) and show/refresh. Manual trigger: the ⌃Space command (V2-3) forces a query regardless of length. Auto-select first item per `autoFocus`.

**Announcements (DoD §V-B).** On open/refresh, post the `audio_summary` at `.medium`; on selection move, post `"{label}, {kind}, {i} of {n}"` at `.medium`. **Coalesce** — a newer announcement supersedes an in-flight one (do not queue) so typing at speed can't garble. Suppress the redundant "1 of n" echo at open when `autoFocus` already announced the first item (the `isInitialLoad` precedent). Honor the announcement-verbosity setting. Hover-vs-keyboard debounce via a `lastKeyboardNavAt` timestamp (QuickSwitcher parity) so mouse motion doesn't fight arrowing.

**Tests:** unit — model selection movement (wrap/clamp, paging), trigger gating by length/site, monitor install/teardown lifecycle. a11y-check must stay 100 on the new files.

## V1-3 · Insertion + snippet placeholder fields (#576) — PR 3

**Plain insertion.** Apply the selected `CompletionItem` by replacing `[replace_start_utf16, replace_end_utf16)` in the text view with `replacement`, as a **single coalesced undo group** (`textView.undoManager` grouping) so one ⌘Z reverts the whole completion. After replacement, feed the delta to `documentBuffer.applyEdit` exactly as a keystroke would (keep the mirror honest) and re-run highlight. Honor `insertSpaceAfterComplete` / `insertPeriodAfterSpaces`.

**Snippet insertion** (`is_snippet`). Parse the template's tab-stops (`#` = stop, `~` = final cursor, `\n` = newline; V0-4 defines the grammar). Insert the resolved text, then enter a **snippet session**:
- Park the caret/selection at the first `#` stop; announce `"field 1 of {k}"`.
- Tab / Shift-Tab move to next/previous stop (announced); typing over a stop replaces its placeholder; the `~` stop (or end) ends the session.
- Esc ends the session leaving text as-is. The session owns Tab only while active (composes with V1-2's monitor — snippet session takes precedence over popup navigation when both could be live; in practice the popup is dismissed on insertion, so the snippet session is the sole Tab owner).

**Chained re-trigger.** After inserting a `WikilinkTarget`, if the caret is now at an `Anchor` site (user typed `#`), re-query so `[[Target#` immediately offers headings. Same for a frontmatter key → value hop.

**Tests:** UI/unit — insertion offsets correct at emoji/CJK (UTF-16), undo reverts atomically, snippet field navigation + type-to-replace, `insertSpaceAfterComplete`/period behaviors, chained re-trigger fires.

## V1-4 · Accessibility closure — VoiceOver, APCA, a11y-check (#577) — PR 4

The DoD gate for the surface (§V-A, §V-B, and U §A–§G).

- **VoiceOver end-to-end** (recorded in the PR): for **every** provider — word, LaTeX (+ snippet field traversal), callout, wikilink target, heading anchor, tag, frontmatter key + value — trigger, hear the count, arrow through hearing each item + kind + "i of n", insert with Enter and with Tab, dismiss with Esc, and blacklist the current suggestion (V2-2 hotkey) — all keyboard-only, no mouse. Any gap is a blocker.
- **APCA** (measure, don't eyeball — project standard): selected and unselected rows, primary + `detail` text, in **both** light and dark appearances, APCA Lc ≥ 75; use `selectedContentBackgroundColor`/`selectedMenuItemTextColor` (the palette precedent) and assert in a unit test that samples/compares, not a screenshot.
- **a11y-check 100/100** with zero errors on the new `Completion/*` views.
- **Keyboard/mouse parity drift test:** enumerate every mouse-reachable popup action and assert a keyboard equivalent exists (mirrors the Graph/Canvas projection-equivalence drift tests).

**Tests:** the APCA sampling test, the parity drift test, and the a11y-check CI gate. VoiceOver script is a documented manual pass (human-residual, recorded like the U milestone's VO pass).

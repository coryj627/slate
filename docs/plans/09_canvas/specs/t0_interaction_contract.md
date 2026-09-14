# T0 — Canvas interaction contract (cross-cutting, normative)

Applies to **every** canvas issue. Implemented primarily by #518 (announcement coordinator + verbosity + Where-am-I); enforced by tests in each consuming issue. When a wave spec and this contract disagree, this contract wins — fix the spec.

---

## 1. Announcement grammar

All user-audible strings are assembled by the #518 coordinator from these grammars. No canvas code calls `postAccessibilityAnnouncement` directly.

### 1.1 Card reference

`⟨card⟩ := ⟨Type⟩ card "⟨title⟩"` — Types: *Text*, *File*, *Image*, *Link*, *Group* (group phrased as `Group "⟨label⟩"`).
Title derivation: text → first line; file → note title (frontmatter `title`, else humanized filename — **never a raw path**); file+subpath → `⟨note title⟩ › ⟨heading⟩`; image/media → frontmatter alt/title, else humanized filename with type prefix ("Image: architecture diagram"); link → **URL host, plus first path segment when the host alone is ambiguous** (JSON Canvas link nodes carry no title and Slate does not fetch pages; the full URL is in the AX detail). Untitled duplicates disambiguate with a stable ordinal ("Untitled 3") — same string feeds Voice Control ("click Untitled 3"). **Ordinal stability:** assigned from document order at canvas load and held for the session (moves don't renumber; a new untitled card takes the next free ordinal). Color names (backend-pinned, t1): presets 1–6 = red, orange, yellow, green, cyan, purple; hex = "custom color".

### 1.2 Navigation / selection (verbosity matrix)

| Level | Moved-to announcement |
|---|---|
| terse | `⟨title⟩` |
| standard | `⟨card⟩, ⟨n⟩ of ⟨m⟩ in ⟨group‖canvas⟩` |
| verbose | standard + `, ⟨k⟩ connections` + color name if set + `, marked` if marked |

Group entry/exit: `Entering group "⟨label⟩", ⟨m⟩ cards` / `Leaving group "⟨label⟩"`. These phrases describe a change of group context, regardless of whether the move came from an arrow, next/previous command, or explicit group-navigation command. A successful move that crosses a group boundary owes the boundary information followed by its destination at the selected verbosity; an ordinary move within the same group owes only its destination. Use the document's group identities and authoritative counts, not a lookup by displayed label. For nested crossings, describe departures from the inside out and arrivals from the outside in.

Boundary and destination form **one logical navigation announcement**. The coordinator must preserve their order and required group context when coalescing to the final destination (§1.5); sending two competing navigation events that discard the boundary is not conformant. Compare the last delivered group context with the final destination's context, using their common ancestor. A rapid A → B → C therefore describes A → C, not just B → C; A → B → A owes no stale departure or entry from the omitted intermediate move. A failed move announces its refusal and owes no arrival. This applies to outline, table and visual-surface movement alike.

Connection traversal: `⟨direction phrase⟩ ⟨card⟩` where direction phrase ∈ {`Connects to`, `Linked with` (undirected/bidirectional), `Connected from`} per JSON-Canvas `fromEnd`/`toEnd`; labelled edges append `, labelled "⟨label⟩"`.

### 1.3 Action confirmations

Pattern: `⟨Verb past⟩ ⟨object⟩ ⟨relative detail⟩` — "Created text card below 'Research'", "Connected 'Research' to 'Ideas', labelled 'supports'", "Moved into group 'Q3'", "Deleted 3 cards — ⌘Z to undo". Destructive confirmations always carry the undo hint at standard+ verbosity. Undo/redo announce the op name: "Undid: move 'Research'".

### 1.4 Where am I? (#518, ⌃⌘I)

One pull-based readback, always verbose-grade regardless of setting:
`⟨card⟩, in ⟨group path⟩, ⟨n⟩ of ⟨m⟩, ⟨k⟩ connections (⟨in⟩ in, ⟨out⟩ out), ⟨color⟩, ⟨marked?⟩, ⟨mode if active⟩, ⟨filter if active: "3 of 40 shown"⟩`. Also rendered in a focusable transient panel so braille users read it at leisure.

When there is no selected card, read the first card in the **current displayed projection**, after filtering, rather than the first unfiltered document row. This fallback is a read: it neither writes the shared selection nor moves the reader to a card. A valid selected card retains precedence even when a filter hides it; filtering does not make that selection stale or authorize replacing it with a fallback. With no selection, a current filter with zero displayed cards answers `NoCardsMatchFilter`; an actually empty unfiltered canvas answers its empty-canvas status. A pending, failed or unavailable answer keeps its truthful state-specific refusal; it is not evidence of an empty result. Existing refusals for a stale or otherwise unresolvable selected card remain in force.

Escape dismisses an open Where-am-I panel before the ordinary ladder, with the focus-restoration rule in M5.

### 1.5 Timing rules

- **Coalescing:** events of the same class within ~150–250 ms collapse; final state wins (held-arrow nudge announces the resting position, not every step). A retained navigation announcement includes its required group-boundary context and final destination (§1.2). Coalescing may omit intermediate moves, but must not erase the context needed to understand the final arrival.
- **Bulk:** an action over N marked cards emits exactly **one** summary.
- **No doubling:** viewport auto-pan (follow-selection) is silent — the selection announcement suffices. Live-region priority: navigation = polite; errors/conflicts = assertive.

### 1.6 Filter changes and structural navigation (#1171)

The filter is a view, never a document mutation. A user edit that changes an active filter to inactive, including backspacing the last character to empty, announces `canvasFilterCleared(total)` exactly once using the same grammar as explicit Clear Filter. The count describes the restored unfiltered projection. Initial empty-field binding and an edit that does not clear an active filter do not manufacture a clearance. An explicit clear and its text-change observer are one action, not two announcements. Where the restored count cannot be answered, use the existing state-specific refusal instead of claiming a count.

Ordinary next/previous traversal stays within the displayed filtered set. **Structural navigation may leave that set, but may not seat an invisible target.** Enter/exit group, follow/jump to a connection and trace path first resolve their target against the current document. If the target is excluded by an active filter, clear that filter, make the target available in the projection, announce the clearance, and then land selection and reader focus together. Expand ancestors or materialize the destination as needed by the surface. A visible target preserves the filter; a missing, refused or stale target changes neither filter nor selection. A delayed answer cannot clear a newer filter or land in another document.

Trace path follows the same rule before leaving the displayed set and clears at most once for the invocation. The clearance must be heard before the structural arrival or path result; announcement coalescing must not replace one with the other. This exception serves an explicit structural destination and does not broaden ordinary filtered traversal or change document contents.

## 2. Mode-stack contract (move, resize, connect)

- **M1 Entry:** command/chord/visible control → announcement names the mode, the object, and the exits: "Move mode — 'Research'. Arrows to move, Return to place, Escape to cancel."
- **M2 Exit:** Return commits (confirmation per §1.3); Esc cancels and **restores prior state**, announced ("Move cancelled — card returned").
- **M3 Queryable:** while active, the canvas container's `accessibilityValue` carries `⟨Mode⟩: ⟨card⟩` — state is inspectable (braille rule §3), not merely announced.
- **M4 Owning-context departure = auto-cancel:** leaving the owning canvas context (tab switch, pane-focus chord or another actual document departure) cancels the mode with restoration + announcement. Opening a command palette, menu or context menu is temporary access to that context's controls and **does not cancel** its mode; Commit Mode, Cancel Mode and resize presets remain reachable through M6. Dismissing the overlay restores access to the owning context. If an overlay command actually changes that context, the resulting departure cancels normally. No mode is stranded in an inactive document; no keyboard trap (WCAG 2.1.2).
- **M5 Escape disposition:** an **open Where-am-I panel** (§1.4) consumes Escape before the ordinary ladder, even when focus is outside the panel. Close only the panel; leave an active mode and filter untouched. Restore the prior focus only if focus was inside the panel; dismissing an unfocused panel must not relocate the reader. Panel dismissal adds no synthetic announcement: the panel's closure and conditional focus restoration expose its result. Otherwise the ladder is **active mode → active filter (#373, clears) → canvas surface → workspace tab**. The surface rung dismisses the applicable surface transient/detail region or returns from the filter field/result summary; it does not give every open transient the panel's special priority. Shell menus and modal overlays consume their own Escape before canvas handling. Each press has exactly one disposition, never both panel dismissal and a ladder effect. Ladder state changes retain their operation-specific announcements, and the resulting state remains inspectable (§3).
- **M6 Visible controls:** every mode is enterable/committable/cancelable via on-screen controls (context menu / toolbar) — Switch Control and Voice Control never depend on the keyboard-only path.
- **M7 One mode at a time:** entering a mode while one is active commits nothing — it is rejected with an announcement naming the active mode.
- **M8 Embedded-editor carve-out:** the inline text-card editor (#368) is *not* a spatial mode — **Esc commits** the text and returns focus to the card (matching the app's note-editing convention and interview decision 7); M2's cancel-on-Esc does not apply. Discarding an edit is the editor's own undo (⌘Z) before Esc. Every doc that references editor Esc behavior must say "commits", never cite M2.

## 3. Inspectability rule (braille)

Any state that is announced is also readable from element state: marked → in the card's AX value everywhere it appears; active mode → container value (M3); dirty/conflict → the tab's AX value (extends U1's "edited"); filter → the filter field's value + result summary element; last error → a focusable error region (never announcement-only). The marks list (#524) and Where-am-I panel (§1.4) are the pull-based counterparts to the push announcements.

## 4. Per-AT test matrix (every canvas-UI PR ticks its row)

| AT | Automated | Manual smoke (#365 close-out checklist) |
|---|---|---|
| VoiceOver | AX labels/values/traits/actions via XCTest; rotor membership; announcement strings vs grammar | Cursor walk of each surface; Quick Nav on; rotor jumps |
| Full Keyboard Access | Key-loop position tests; focus-ring presence | Tab-through with FKA on; no unreachable control |
| Voice Control | Label uniqueness test (no duplicate speakable names per surface) | "Show numbers" on renderer; dictate 5 core commands |
| Switch Control | M6 visible-control existence tests | Enter/commit/cancel each mode via switches |
| Braille | §3 inspectability assertions (state in AX values) | Display connected: marks, mode, Where-am-I readable |

## 5. Error & conflict surfacing

Parse warnings (#359 tolerant contract): "Canvas loaded. ⟨n⟩ unsupported items are preserved in the file but not shown" — polite, plus a focusable detail row in the outline footer. Save conflict (#366): assertive announcement + focusable error region with recovery actions (Reload / Overwrite / Save a Copy), mirroring the note-conflict discipline. Missing file-card target: card stays navigable, labelled "⟨title⟩ — file not found", with a "Locate…" action.

## 6. Decision and conformance record (#1171)

The six decisions above resolve the clauses filed from the W6-1 close-out. They supersede the contradictory or absent t0 wording recorded under CD-41, CD-47 and "Mac details recorded while reading" in [`34_canvas_contracts.md`](../../34_canvas_contracts.md). That register remains the historical record of the hosts that were inspected; a normative decision is not a claim that its implementation has shipped.

| Decision | Reason | Implementation evidence or obligation |
|---|---|---|
| M4 preserves palette/menu access | Cancelling on entry would make the visible mode lifecycle commands required by M6 unreachable. | Both hosts already preserve overlays. Existing mode-departure tests cover cancellation versus preservation; retain visible-control reachability checks. |
| An open Where-am-I panel owns the first Escape | Closing a readback must not destroy the user's filter or cancel a mode. Open state decides priority; focus decides restoration. | Both hosts implement panel-first priority and silent dismissal. Windows tests focused/unfocused panels and conditional restoration. Mac's close handler clears the panel; its conditional restoration and no-relocation behavior still require rendered-focus verification and any resulting fix. |
| Last-backspace clearance speaks once | Keystroke-driven widening needs the same observable result as the Clear control. | Both hosts currently return early when the needle becomes inactive. Add active-to-empty, initial-empty, repeated-empty, explicit-clear deduplication and unavailable-state tests when updating the observers/coordinator. |
| No-selection readback uses a displayed fallback | Without a selection, the fallback should describe a displayed card without silently creating a selection. A valid selected card keeps precedence. | Both hosts currently fall back to the unfiltered first row. Add a hidden first document row with a later visible match, zero matches, empty canvas, unchanged selection, valid selected-but-filtered-out precedence, and pending/failed-answer cases. |
| Structural navigation reveals an excluded target | Explicit structural intent may widen the view; reader focus and selection must still agree. | Both hosts currently can select a hidden destination. Cover successful reveal, already-visible target, refused/stale target, newer-filter protection, collapsed ancestors, and focus/selection agreement across projections. |
| Group context survives in the final arrival | A hierarchy transition is meaningful regardless of which control moved the reader. | Both hosts can emit a boundary and then overwrite it with the same-class moved-to event; the table path also needs parity. Update shared announcement composition/coalescing and both consumers together. Test actual delivered output after the coalescing window, nested/repeated-label groups, rapid A → B → C and A → B → A moves, and all verbosity levels. |

The first two rows adjudicate the existing overlay-preservation and panel-priority rules; the Mac focus-restoration obligation is qualified above. The remaining four are coordinated implementation obligations. This contract-only change does not mark new runtime tests or manual AT checks as passed. Consumers must use core-owned announcement vocabulary and the same final behavior on both hosts.

# T0 — Canvas interaction contract (cross-cutting, normative)

Applies to **every** canvas issue. Implemented primarily by #518 (announcement coordinator + verbosity + Where-am-I); enforced by tests in each consuming issue. When a wave spec and this contract disagree, this contract wins — fix the spec.

---

## 1. Announcement grammar

All user-audible strings are assembled by the #518 coordinator from these grammars. No canvas code calls `postAccessibilityAnnouncement` directly.

### 1.1 Card reference

`⟨card⟩ := ⟨Type⟩ card "⟨title⟩"` — Types: *Text*, *File*, *Image*, *Link*, *Group* (group phrased as `Group "⟨label⟩"`).
Title derivation: text → first line; file → note title (frontmatter `title`, else humanized filename — **never a raw path**); file+subpath → `⟨note title⟩ › ⟨heading⟩`; image/media → frontmatter alt/title, else humanized filename with type prefix ("Image: architecture diagram"); link → page title if known, else host. Untitled duplicates disambiguate with a stable ordinal ("Untitled 3") — same string feeds Voice Control ("click Untitled 3").

### 1.2 Navigation / selection (verbosity matrix)

| Level | Moved-to announcement |
|---|---|
| terse | `⟨title⟩` |
| standard | `⟨card⟩, ⟨n⟩ of ⟨m⟩ in ⟨group‖canvas⟩` |
| verbose | standard + `, ⟨k⟩ connections` + color name if set + `, marked` if marked |

Group entry/exit: `Entering group "⟨label⟩", ⟨m⟩ cards` / `Leaving group "⟨label⟩"`. Connection traversal: `⟨direction phrase⟩ ⟨card⟩` where direction phrase ∈ {`Connects to`, `Linked with` (undirected/bidirectional), `Connected from`} per JSON-Canvas `fromEnd`/`toEnd`; labelled edges append `, labelled "⟨label⟩"`.

### 1.3 Action confirmations

Pattern: `⟨Verb past⟩ ⟨object⟩ ⟨relative detail⟩` — "Created text card below 'Research'", "Connected 'Research' to 'Ideas', labelled 'supports'", "Moved into group 'Q3'", "Deleted 3 cards — ⌘Z to undo". Destructive confirmations always carry the undo hint at standard+ verbosity. Undo/redo announce the op name: "Undid: move 'Research'".

### 1.4 Where am I? (#518, ⌃⌘I)

One pull-based readback, always verbose-grade regardless of setting:
`⟨card⟩, in ⟨group path⟩, ⟨n⟩ of ⟨m⟩, ⟨k⟩ connections (⟨in⟩ in, ⟨out⟩ out), ⟨color⟩, ⟨marked?⟩, ⟨mode if active⟩, ⟨filter if active: "3 of 40 shown"⟩`. Also rendered in a focusable transient panel so braille users read it at leisure.

### 1.5 Timing rules

- **Coalescing:** events of the same class within ~150–250 ms collapse; final state wins (held-arrow nudge announces the resting position, not every step).
- **Bulk:** an action over N marked cards emits exactly **one** summary.
- **No doubling:** viewport auto-pan (follow-selection) is silent — the selection announcement suffices. Live-region priority: navigation = polite; errors/conflicts = assertive.

## 2. Mode-stack contract (move, resize, connect)

- **M1 Entry:** command/chord/visible control → announcement names the mode, the object, and the exits: "Move mode — 'Research'. Arrows to move, Return to place, Escape to cancel."
- **M2 Exit:** Return commits (confirmation per §1.3); Esc cancels and **restores prior state**, announced ("Move cancelled — card returned").
- **M3 Queryable:** while active, the canvas container's `accessibilityValue` carries `⟨Mode⟩: ⟨card⟩` — state is inspectable (braille rule §3), not merely announced.
- **M4 Focus departure = auto-cancel:** leaving the canvas (tab switch, palette, pane-focus chord) cancels the mode with restoration + announcement. No mode survives without focus; no keyboard trap (WCAG 2.1.2).
- **M5 Esc ladder (innermost first):** active mode → active filter (#373, clears) → canvas surface → workspace tab. Each Esc consumes exactly one rung; each rung's effect is announced.
- **M6 Visible controls:** every mode is enterable/committable/cancelable via on-screen controls (context menu / toolbar) — Switch Control and Voice Control never depend on the keyboard-only path.
- **M7 One mode at a time:** entering a mode while one is active commits nothing — it is rejected with an announcement naming the active mode.

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

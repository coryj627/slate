# HIG Full-Corpus Conformance Audit — Slate macOS

**Date:** 2026-07-11 · **Baseline:** main @ 8ec2786 (post-#847) + PR #866 · **Method:** all 156 distilled HIG concept files triaged to the 65 macOS-applicable ones; six parallel per-concept auditors each read their assigned distilled files in full, then audited the app against them, quoting exact rules. Every load-bearing finding was independently re-verified against the code before disposition.

**Disposition legend:** ✅ conforms · 🔧 fixed in this PR · 📋 issue filed · 📝 documented deviation (rationale recorded, no change) · ➖ not applicable to Slate.

## Conformance matrix

### Foundations (tier 1)

| Concept | Verdict | Disposition |
|---|---|---|
| accessibility | ✅ (2 stragglers) | 🔧 24→28pt tab-strip buttons (the two #866 missed) |
| color | minor | 🔧 CitationsPanel raw `.orange` → gated `warningText` + "Unresolved" badge + AX prefix (the one literal the U5-3 sweep missed) |
| dark-mode | 📝 | Pinned opaque sRGB pairs forgo desktop-tint transparency — deliberate APCA-determinism trade (DesignTokens doc) |
| design-principles | ✅ | — |
| icons | ✅ (1 nit) | 📝 `moveTo` = `arrow.turn.down.right` vs the table's `folder` (glyph legitimately occupied by the folder role) |
| images | ➖ | SF Symbols only; alt-text via the symbol layer |
| inclusion | ✅ | — |
| layout | ✅ | Token scale throughout; 680pt reading measure |
| materials | ✅ | Glass on chrome only; `.regularMaterial` on the text-heavy overlay |
| motion | ✅ | Both animations brief/easeOut/Reduce-Motion-gated |
| branding / privacy | ✅ | System accent honored in content surfaces; no logo chrome |
| right-to-left | minor | 🔧 disclosure-chevron rotation now layout-direction-aware (tree + history day groups); ⌘⌫/arrow physical semantics correctly unmirrored |
| sf-symbols | ✅ | Per-surface rendering modes pinned (U5-1) |
| typography | ✅ | Dynamic-Type roles; 10pt floor respected; AppKit surfaces track Text Size |

### Menus & keyboard

| Concept | Verdict | Disposition |
|---|---|---|
| the-menu-bar | partial | 🔧 Title-Case strays; verb-first "Show Tasks Review"/"Show Citation Summary"; 📋 #868 state-reflecting titles (blocked on a workspace-observability seam); ✅ Open Recent/Help/disabled-not-hidden all conform |
| menus | partial | 🔧 "New Folder" ellipsis dropped (creation is immediate); "Move To…" canonical spelling; 📋 #868 |
| context-menus | partial | 🔧 tree menu regrouped to ≤3 separator groups; Reveal in Finder/Copy Path given menu-bar + palette homes (redundancy rule); tab menu Split items now hidden (not dimmed) at capacity — see note † |
| edit-menus | ✅ | Find + Bulk Rename homed under Edit (#847) |
| dock-menus | ➖ | Optional; none needed |
| keyboards | partial | 📝 ⇧⌘P shadows Page Setup (acceptable while no print path — revisit with #869); 📝 canvas ⌃⌘ family (documented #520 allocation; ⌃⌘C copy-formatting shadow is canvas-scoped); chord reallocation (⌘O/⌘T/⇧⌘T/⌘R) is **decided on #863** — implementation is its own PR, this one stays chord-neutral |
| pop-up-buttons | ✅ | `.pickerStyle(.menu)` used for selection only |
| pull-down-buttons | partial | 🔧 HistoryPanel single-item ellipsis menu → direct "Show markers" checkbox |
| undo-and-redo | partial | 📋 #867 action-name titles (needs the same observability seam as #868) |
| focus-and-selection | ✅ | Region routing census-tested; FKA clean |

† Tab-menu hide-vs-dim: applied where the rule's exception list doesn't cover it.

### Windows, structure & modality

| Concept | Verdict | Disposition |
|---|---|---|
| windows | partial | 🔧 native window-tabbing opted out (custom tab strip + system tabs = two tab metaphors); 📋 #872 last-vault launch restore (parity/spirit call) |
| split-views | mostly ✅ | 📋 #882 right-pane hide affordance (needs custom collapse — NavigationSplitView can't hide detail) |
| sidebars | 📝 | Bottom utility bar carries only menu-redundant actions (documented deviation) |
| panels | ✅/📝 | Right-pane leaves = sanctioned split-view inspector alternative; palette/switcher-as-sheets recorded as documented deviation (true floating panels are awkward on the SwiftUI macOS-15 floor) |
| sheets | partial | 📋 #878 CitationPopover → anchored popover; 📋 #879 TasksReview → window/leaf; one-modal-at-a-time is de-facto (single anchor) — noted, no arbiter refactor |
| popovers | deviation | 📋 #878 |
| alerts | mostly ✅ | 🔧 fragment titles Title-Cased; button caps unified ("Keep Mine", "Reload from Disk"); 📋 #881 compaction alert → non-interrupting channel (honoring o_spec O-2 "never silent") |
| modality | partial | Covered by #878/#879 |
| going-full-screen / multitasking / boxes | ✅ | System behavior by omission |
| tab-views | ✅ | Settings: 5 labeled tabs |
| toolbars | partial | 📋 #880 customization + Save grouping; ✅ every item menu-redundant |

### Controls & data entry

| Concept | Verdict | Disposition |
|---|---|---|
| buttons | mostly ✅ | 🔧 Welcome "Open Vault…" now `.borderedProminent` (the app's only unprominent primary; "style, not size"); 📝 BulkRename Apply unroled (Preview-gate is the documented mitigation) |
| toggles | partial | 🔧 Bibliography "Watch" switch shows its label (was `.labelsHidden` with no disambiguating context) |
| pickers | ✅ | 📋 #858 (existing) gains the DatePicker-inconsistency evidence |
| segmented-controls | ✅/📋 | 📋 #883 TasksReview chips vs native segmented (design call) |
| text-fields / text-views / labels | ✅ | Visible labels; hygiene-hardened YAML editor |
| search-fields | deviation | 🔧 clear-text button added (core anatomy); close button switched to the plain-xmark dismiss glyph so clear/close no longer share a shape; 📝 palette fields stay clear-less (Spotlight idiom) |
| entering-data | ✅ | Defaults prefilled; on-submit validation is the documented explicit-save model |
| disclosure-controls | ✅ | — |
| progress-indicators | partial | 🔧 scan no longer transitions circular→linear mid-operation; "Loading…" → "Loading folder…" |
| color-wells | 📝 | Six-preset semantic palette beats NSColorWell for the never-color-alone contract (canvas interop enum) — conformant deviation |
| combo-boxes / steppers / sliders / token-fields | ✅/➖ | No misuse; steppers value-paired |

### Content operations & system integration

| Concept | Verdict | Disposition |
|---|---|---|
| file-management | partial | 📋 #877 autosave decision record; 📋 #870 Finder exchange; conflict machinery ✅ |
| drag-and-drop | partial | 📋 #870 file-URL flavors; 📋 #871 undo for moves/renames; (#851/#852 already filed: drop highlight, multi-select) |
| feedback / loading / onboarding / notifications / collections / scroll-views | ✅ | Announcement discipline, determinate-when-knowable, no forced tutorial, in-app progress over notifications |
| launching | partial | 📋 #872 |
| offering-help | partial | 🔧 verb-first tooltips ("Open the project README", "Open Settings (⌘,)", "Show all open tabs") |
| searching | partial | 📋 #874 find-in-note (chord decision vs #422); 📋 #876 recent searches |
| lists-and-tables | partial | 🔧 Bases columns user-resizable (`.userResizingMask`) |
| outline-views | partial | 📋 #873 expansion persistence; (#850 already filed: type-select/Return-F2) |
| settings | partial | 📋 #862 (existing) gains per-pane title + last-pane restore |
| printing | gap | 📋 #869 File ▸ Print… (Milestone-E-adjacent) |
| collaboration-and-sharing | deferred | Comment on #824 — Share rides the export pipeline |
| app-icons | gap | 📋 #875 (pre-release requirement) |

### Accessibility, input & writing

| Concept | Verdict | Disposition |
|---|---|---|
| accessibility (checklist beyond gates) | ✅ | 🔧 the two 24pt stragglers |
| voiceover | ✅ | Full rotor service (headings h1–h6, links, custom canvas rotor) |
| pointing-devices | ✅ | Resize cursor on custom divider; (#864: link cursor) |
| gestures | ✅ | No gesture-only paths |
| writing — capitalization | fail → 🔧 | Title Case: "Refresh Sync Diagnostics", "Show History Panel", "Bases: Quick Filter", "Move To…"; alert buttons "Keep Mine"/"Reload from Disk"/"Remove from Recent Vaults"/"Keep in List" |
| writing — ellipsis | ✅ | 🔧 one inconsistency ("New Folder") removed |
| writing — alert copy | ✅ | Questions + verb buttons throughout; OK only on the informational alert |
| writing — terminology | ✅ | 🔧 "document"→"note" (CitationSummary), "file"→"note" (OutlineSidebar empty state); shortcut spellings normalized to "Command-X"/"Escape" everywhere |

## Documented-deviations register

1. Pinned opaque surfaces (no desktop tinting) — APCA determinism (DesignTokens).
2. Increase-Contrast token no-op — APCA |Lc|>75 floor; code/canvas surfaces do adapt (DesignTokens doc, #847).
3. Explicit-save model vs autosave-by-default — decision record #877; conflict machinery is built around deliberate writes.
4. Palette/switcher/search as sheets/overlay, not floating panels — SwiftUI macOS-15 floor; Esc/focus semantics preserved.
5. Bottom utility bar — every action menu-redundant (sidebars.md bottom-edge concern mitigated).
6. Canvas six-preset color buttons over NSColorWell — never-color-alone + JSON-Canvas interop enum.
7. ⇧⌘P over Page Setup — no print path today; re-evaluate with #869.
8. Canvas ⌃⌘ chord family — documented #520 allocation, canvas-scoped enablement.
9. Tab navigation in the View menu (HIG: Window menu) — documented #454 grouping.
10. `moveTo` glyph — `folder` occupied by the folder role.

## Not applicable (excluded at triage)

91 of 156 concepts: iOS/watchOS/tvOS/visionOS-only surfaces (tab bars, action sheets, Digital Crown, ornaments…), frameworks Slate doesn't ship (HealthKit, CarPlay, SiriKit, StoreKit, Game Center, ARKit, widgets, Live Activities…), and content types it doesn't render (charts, maps, AR, video). Full list derivable from the routing index platforms frontmatter.

## Issue ledger from this audit

#867 undo action names · #868 state-reflecting menu titles · #869 Print · #870 Finder drag exchange · #871 structural-op undo · #872 launch vault restore · #873 expansion persistence · #874 find-in-note · #875 app icon · #876 search recents · #877 autosave decision · #878 CitationPopover · #879 TasksReview container · #880 toolbar customization · #881 compaction channel · #882 right-pane hide · #883 chips vs segmented — plus evidence comments on #824, #858, #862. Chord reallocation: decided on #863 (implementation separate).

# W7 executable spec — The UIA accessibility program (cross-cutting): the four issues, re-specified

Issues: W7-1 ([#747](https://github.com/coryj627/slate/issues/747)) · W7-2 ([#748](https://github.com/coryj627/slate/issues/748)) · W7-3 ([#749](https://github.com/coryj627/slate/issues/749)) · W7-4 ([#750](https://github.com/coryj627/slate/issues/750)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). Program: [00_program.md](../00_program.md) (decisions 6, 11, 12; DoD §W-C/§W-D). **Issue = unit of acceptance; PR = unit of review** (the W6 convention). One PR per issue, except W7-2 (its slice A shipped with Wave 1; this spec covers **slice B**, one PR) and W7-4 (rolling: one reconciliation PR per wave close, plus one authoring PR now — §5).

**Revision 2026-09-16.** This text supersedes the 2026-07-06 spec and the issues' 2026-08-09 delivery notes wherever they differ (the old text is in git history at `fe6cad02`). It was written after reading the shipped Windows tree, the mac tree, AvalonEdit's and NVDA's sources, and the W6 close-out artifacts; §0 lists the facts that changed the scope. This wave is **the load-bearing parity** — everything else is furniture if JAWS/NVDA can't drive it.

**Behavioral source (normative, in this order):** program decision 6 (same canonical artifacts, second consumer; JAWS + NVDA reference, Narrator smoke) and decision 11 (the tooling gate) → `05_locked_architecture_decisions.md` §1.1–1.2 (accessibility artifacts produced in Rust, consumed per platform) and §6.4 "Per-platform AT navigation" (Windows: "custom `AutomationPeer` ranges with semantic descriptions exposed via UIA properties") → the shipped precedents named per issue below (the W3-1 text-provider decorator, the W6 announcers, the W6-2 trigger ledger, the W5-1 chord table) → the mac test suites and the mac sources **only where the mac actually implements the behaviour** (§0 item 3 records where it does not — a mac gap is a mac issue, never a Windows parity target).

---

## 0. Read this first — facts established 2026-09-16 (supersede the issue bodies where they differ)

1. **The wave is unblocked and runs as a wave of its own.** W0–W5 shipped; W6-1 (#745) shipped; W6-2 (#746) is code-complete (PR F [#1221](https://github.com/coryj627/slate/pull/1221), 2026-09-15) and open only for its human AT residual. Every W7 dependency is on `main`: W2-2 (the span consumer, PR #1037), W5-1 (the chord table, merged 2026-08-13), W0.5-3 (the vocabulary), the canvas and graph announcer families (W6-1 PR 0a, W6-2 PR 0a). The program's wave-7 interleaving ("W7-1 with W2, W7-3 with W5, the census with W5") **did not happen** except for two parts: the dispatcher core (`apps/slate-windows/src/SlateWindows/AccessibilityNotificationDispatcher.cs`, with Wave 1) and the rolling W7-4 instrument. The program's 2026-09-16 gate snapshot records this; nothing behavioural follows from it.
2. **What each issue already has** (verified in the tree, not in the issue text):
   - *W7-1:* a peer exists — `SlateTextEditorAutomationPeer : TextEditorAutomationPeer` (`SlateTextEditor.cs:447`): it redirects the `TextArea` peer's `EventsSource` to itself, has no children (`GetChildrenCore() => null`), and hands `PatternInterface.Text` to the base. **AvalonEdit's own Text pattern is the baseline**: `TextAreaAutomationPeer` (AvalonEdit `Editing/TextAreaAutomationPeer.cs`, read at `master` 2026-09-16; the pinned package is 6.3.1.120 — re-verify at implementation) implements `ITextProvider` itself, reports control type `Document`, and raises exactly one automation event, `TextPatternOnTextSelectionChanged`, on `Caret.PositionChanged` and `SelectionChanged`. Its `TextRangeProvider` moves by `Character/Format/Word/Line/Paragraph/Document`, **returns `null` from `GetAttributeValue` for every attribute**, returns no children, and resolves `GetEnclosingElement` to the `TextArea` peer. So today a screen reader can read the editor by line/word/character and track the caret, and hears **no** semantics — no heading level, no link, no code. The colorizer (`EditorHighlighting.cs:134`, `AvalonCanonicalSpanColorizer`) already paints from the canonical windowed spans through `AvalonDocumentBufferSession` (`HighlightInRange` `:178`; the read-only `InspectInRange` `:186`; `EditorHighlightWindow` `:715`), so the "one source, two consumers" precondition is real. The decorator precedent is the reading view's: `Reading/HeadingStyleText.cs` (`HeadingStyleTextProvider` `:39`, `HeadingStyleTextRange` `:103`, `MixedAttributeValue` at `:155`), wrapped in by `ReadingSurfacePeer.GetPattern` (`ReadingSurface.cs:703`).
   - *W7-2:* slice A is the 53-line dispatcher: `Post(A11yEvent)` renders through `A11yRender` and `Post(RenderedAnnouncement)` raises `RaiseNotificationEvent(AutomationNotificationKind.Other, High → ImportantMostRecent | Medium → MostRecent, text, "slate-accessibility-announcement")` (`:41–51`); it is the ONLY `RaiseNotificationEvent` caller in the tree. The corpus is 525 rows over 199 top-level event kinds (461 medium, 64 high), text-parity-pinned on both hosts (`Censuses/A11yCorpusCensus.cs`; `A11yCorpusCensusTests.swift:597`) with Rust-side mirror tripwires. **The coalescers already exist**: `Canvas/CanvasAnnouncer.cs` and `Graph/GraphAnnouncer.cs` implement the 200 ms class-keyed latest-wins windows whose class keys are pinned in ONE list at `crates/slate-core/src/a11y.rs:123–150`; `ScanAnnouncementGate.cs` is the 350 ms scan-progress rate guard (mac twin `AppState.swift:8305`). **The trigger-parity mechanism already exists for two families**: `scripts/graph_trigger_ledger.py` + `Censuses/GraphTriggerParityCensus.cs` (and the canvas pair) derive every structural key from the binding, resolve the mac and Windows construction sites, the facts, and the end-to-end observer, and admit a platform gap only under an owner designation. Host-composed residue: **52** `// W0.5-3 residue:` markers on Windows, **28** pinned on mac (`A11yResidueCensusTests.swift:31`).
   - *W7-3:* `Commands/ChordTable.cs` (1,760 lines) projects to `apps/slate-windows/chords.json` (schemaVersion 3): 189 command rows, 69 chorded, **every chorded row already carries `windowsSpoken`**, plus 96 `chordSurface` rows for surface interactions that are not app commands — including the 32 Reading-scoped `windows.reading.*` rows of the W3-1 navigator (`ChordTable.cs:1538–1563`). The two producers exist (`Commands/HotkeyChords.cs`: `MacHotkeySpoken.Spoken` `:63`, `WindowsHotkeySpoken.Spoken` `:167` — walked over the *Windows* chord, token order inverted from mac) with facts (`ChordTableTests.cs:40,80,101`). Consumers: menus resolve `InputGestureText="{local:ChordText id}"` (`ChordTextExtension.cs`), which WPF's `MenuItemAutomationPeer` publishes as UIA `AcceleratorKey` (display form; ATs pronounce it themselves); palette rows expose `Name = "{Label}, {WindowsHotkeySpoken.Spoken(HotkeyHint)}"` (`CommandPaletteViewModel.cs:92–95`). `ChordTable.WindowsSpokenFor` (`:464`) has **no callers**. The mac custom-rotor inventory is small — three SwiftUI rotors on the canvas outline (`Canvas/CanvasOutlineView.swift:158,163,168`) — but the **custom-action** inventory is not: 18 `accessibilityAction(named:)`/`NSAccessibilityCustomAction` sites across the sidebar, tag tree, Bases, graph and canvas, and three `accessibilityCustomContent` sites (math source, mermaid source, graph "Connects to"). The help-doc chord tables and their drift test are **W8-6's by the owner's 2026-08-13 call** (`CommandDriftTests.cs:28–31`; `28_palette_contracts.md` P13(c)).
   - *W7-4:* `w_c_matrix.md` has 46 table rows (W1 through W6); `SlateWindows.AccessibilityTests` has **30** FlaUI journeys (`ShellAccessibilityTests.cs`; the issue's "16" is stale); the CI job is `shell accessibility gate (windows x64)` (`.github/workflows/windows.yml:706–749`, `windows-latest`, 30-day evidence artifacts); there is exactly **one** axe waiver (#1115, `IsFluentCollapsedScrollBarPart`, pinned both ways by `AxeWaiverTests.cs`) — not a per-surface suppression system; canvas and graph rows are pinned to the shell by `WcMatrixCanvasEvidenceCensus` / `WcMatrixGraphEvidenceCensus`, **no other wave's rows are**. Human evidence recorded: **W3-1 NVDA only** (`reports/w3_1_nvda_field_verification.md`, 2026-07-26/27). Checklist files exist for **W6-1 and W6-2 only** (`reports/w6_1_canvas_at_checklist.md`, `reports/w6_2_graph_at_checklist.md`). JAWS is installed on the owner machine, unmeasured; Narrator is smoke scope (decision 6).
3. **Four corrections that change scope** (each a `gap_analysis.md` row):
   - **G29 — the mac editor exposes no span semantics to VoiceOver.** `NoteEditorView.swift` keeps `NSTextView` native by the issue-#63 rule, `applyHighlight` applies colour only, and heading navigation is delegated to the Outline sidebar. The old W7-1 text ("as VoiceOver does via the mac span consumer") was false. W7-1 is **Windows-first**; the mac twin is filed as [#1224](https://github.com/coryj627/slate/issues/1224) (the G20/G22 convergence posture — Windows ships the stricter behaviour, mac converges).
   - **G30 — the scan-progress family is host-composed on both hosts.** `ScanAnnouncementGate.cs` builds three `A11yEvent.HostComposed` strings (`:25,40,50`) and mac's `announceScan` (`AppState.swift:23163–23176`) builds the same three; `a11y.rs`'s module doc ("no engine-level vocabulary remains outstanding") is wrong for this family. W7-2 slice B converts it (§3.3 B2).
   - **G31 — the shipped Medium → `MostRecent` mapping interrupts speech under NVDA.** NVDA's `event_UIA_notification` (`source/NVDAObjects/UIA/__init__.py`, `master` 2026-09-16) calls `speech.cancelSpeech()` for **both** `ImportantMostRecent` and `MostRecent` ("no distinction is made between important and non-important"); only `All`, `ImportantAll` and `CurrentThenMostRecent` queue. Mac `.medium` queues politely. So every polite Windows announcement today cuts whatever NVDA was saying — not parity. W7-2 slice B corrects the mapping (§3.3 B1). NVDA's handler ignores `NotificationKind` and `activityId` entirely and drops notifications from a non-foreground app module.
   - **G32 — W7-3's help-doc deliverable moved to W8-6.** By the owner's 2026-08-13 call the `docs/help/` per-platform chord tables and their drift test belong to #756. W7-3 ships the **source** table W8-6 renders from (§4.3) and touches `docs/help/` not at all.
4. **The contracts documents are 37, 38 and 39.** The issue bodies' `34_editor_peer_contracts.md` and `35_dispatcher_contracts.md` collided with the canvas (34) and graph (35) documents written after them; 36 is the cancellable-file-management document. This wave uses **`docs/plans/37_editor_peer_contracts.md`** (W7-1), **`docs/plans/38_notification_dispatcher_contracts.md`** (W7-2 slice B, incl. the §W-D ledger), **`docs/plans/39_at_navigation_contracts.md`** (W7-3, incl. the map as its first register). W7-4 has no contracts document: it is an instrument, and its registers are `w_c_matrix.md` and the `reports/` checklists (§5.2).
5. **Upstream facts the designs rest on** (pin the excerpts in the contracts docs so a reviewer need not re-fetch): NVDA reads text-range attributes into format fields in `_getFormatFieldAtRange` — `UIA_LinkAttributeId` → `link`, `UIA_StyleIdAttributeId` → `heading-level`, `UIA_StyleNameAttributeId` → `style` (spoken only under "report style", off by default — the G27 precedent), `UIA_AnnotationTypesAttributeId` → `invalid-spelling`/`comment`/… . AvalonEdit's provider facts are in item 2. JAWS has no readable source: every JAWS claim below is a **human-checklist row**, never assumed.

---

## 1. Architecture — the shape the wave shares

```
apps/slate-windows/src/SlateWindows/
  EditorSemanticTextProvider.cs      W7-1  decorator over AvalonEdit's ITextProvider (the W3-1 HeadingStyleText shape):
                                           attributes + FindAttribute answered from the buffer session; everything else forwarded
  EditorSemanticTextRange.cs         W7-1  the ITextRangeProvider decorator: GetAttributeValue / FindAttribute / Mixed rules
  SlateTextEditor.cs                 W7-1  the peer wraps PatternInterface.Text; raises TextPatternOnTextChanged per edit batch
  AccessibilityNotificationDispatcher.cs
                                     W7-2B the mapping corrected (Medium → All); kind + activity id pinned; nothing else changes
  ScanAnnouncementGate.cs            W7-2B constructs the typed scan events (no HostComposed); the guard stays host-side (2.5 s on Windows since the contract 38 D-4 amendment; mac 350 ms)
  (no new announcer)                 W7-2B the coalescers are the shipped CanvasAnnouncer/GraphAnnouncer + per-surface debounces (§3.3 B3)
  Commands/ChordTable.cs             W7-3  unchanged unless the audit finds a missing row; the table is already the single source
tests/SlateWindows.Tests/
  Censuses/A11yTriggerParityCensus.cs     W7-2B the whole-corpus twin of GraphTriggerParityCensus
  Censuses/ChordSpeechAuditCensus.cs      W7-3  every spoken/accelerator string on every surface derives from the table
  Censuses/AtNavigationMapCensus.cs       W7-3  the map is complete against the mac source and true of the Windows shell
  Censuses/WcMatrixEvidenceCensus.cs      W7-4  every automation id in the shell → a matrix row (generalises the canvas/graph twins)
tests/SlateWindows.AccessibilityTests/
  ShellAccessibilityTests.cs         W7-1  EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean
                                     W7-3  SpokenChords_MenusPaletteAndOverlays_MatchTheTable
scripts/
  a11y_trigger_ledger.py             W7-2B the whole-corpus generalisation of graph_trigger_ledger.py
crates/slate-core/src/a11y.rs + tests/fixtures/a11y/corpus.json + crates/slate-uniffi + both corpus mirrors
                                     W7-2B the scan family (four-place rule + the Windows mirror)
docs/plans/
  37_editor_peer_contracts.md · 38_notification_dispatcher_contracts.md · 39_at_navigation_contracts.md
docs/plans/18_windows_port/
  at_navigation_map.md               W7-3  the deliverable table (the first register of 39)
  w_c_matrix.md · reports/*_at_checklist.md · reports/_at_pass_template.md   W7-4
```

Rules that bind every W7 PR (each becomes a numbered contract in its document):

- **R-1 One source, two consumers.** The peer answers from the SAME `AvalonDocumentBufferSession` the colorizer paints from; no second span request path, no C# classification (decision 4, §W-G). A span kind's *meaning* (which UIA attribute it maps to) is host-side by design — it is a UIA idiom, not product logic — and is the table in §2.4.
- **R-2 Text and priority are core's; etiquette is the host's.** No `HostComposed` construction survives this wave except the owner-designated residue register (§3.3 B4). Timing (coalescing windows, rate guards) stays host-side with its class keys pinned core-side (`a11y.rs:123`).
- **R-3 One derivation for spoken chords.** Every speakable chord string on every surface is `WindowsHotkeySpoken.Spoken(row.WindowsChord)` for a row in `commands` ∪ `chordSurface`, or a key name on the recorded allowlist (§4.4). No literal.
- **R-4 Stock ATs are the gate.** Every automated and human acceptance runs against stock NVDA and JAWS settings; W-E7 layers are never load-bearing (G21).
- **R-5 The #1088 rule.** No automation peer is claimed by two parents; a structural peer never re-hands a child another peer owns. Pinned by a forward-and-backward cross-process walk wherever a peer tree changes.
- **R-6 Human cells flip only with a named run** (tester, AT + version, OS build, commit, corpus, method — the `w3_1_nvda_field_verification.md` header). Unexecuted = the release residual, listed as such, never green by omission.

---

## 2. W7-1 · Editor AutomationPeer — TextPattern + semantic ranges (#747) — one PR

### 2.1 Goal

JAWS and NVDA, reading the **editing** surface by line/word/character or by say-all, hear the semantics the canonical spans carry — heading level, link, and the remaining kinds by style name — with caret and selection tracking and document-change events that keep the reader in sync. Windows-first (G29); the mac twin is #1224.

### 2.2 What stands today (verified)

§0 item 2. In one line: AvalonEdit gives units, selection tracking and `RangeFromPoint`; nothing gives attributes, nothing raises text-changed, and the semantic spans are already on the host for the colorizer.

### 2.3 Design — the shape

1. **Wrap, don't replace.** `SlateTextEditorAutomationPeer.GetPattern(Text)` returns `new EditorSemanticTextProvider(baseProvider, session)` where `baseProvider` is what the base returns today (AvalonEdit's `TextEditorAutomationPeer` hands the `TextArea` peer's provider — confirm in the 6.3.1.120 source at implementation) and `session` is the editor's `AvalonDocumentBufferSession` (the `HighlightSession` dependency property, `SlateTextEditor.cs:19`). The decorator forwards `DocumentRange`, `GetSelection`, `GetVisibleRanges`, `RangeFromChild`, `RangeFromPoint`, `SupportedTextSelection` and wraps each returned range in `EditorSemanticTextRange`, exactly as `HeadingStyleTextProvider` does. The range decorator forwards every `ITextRangeProvider` member to AvalonEdit's range except `GetAttributeValue`, `FindAttribute` and (for wrapping) `Clone`/`Move*`/`GetEnclosingElement`.
2. **Attribute answers come from the spans of the queried range, on demand.** For a range `[s, e)` (UTF-16, from the wrapped provider's offsets — use AvalonEdit's range `GetText`-free offset accessors; if the 6.3.1.120 `TextRangeProvider` exposes no offsets, derive them through `ScreenPoint`-free means: `CompareEndpoints` against document-start clones, the technique `HeadingStyleTextRange.ParagraphRanges` uses), the decorator asks `session.InspectInRange(s, e)` (`:186`, read-only — **never** `HighlightInRange`, which drives the paint window) and maps `EditorSemanticSpan`s to attribute values by the table in §2.4. Cost is O(spans in range) through the same FFI the colorizer uses (`editor_highlight_spans_in_range`); a document-range query short-circuits on the first differing value (returns `MixedAttributeValue`). Hyperlink validation may cache only a current-revision canonical window; the colorizer's window is never read by the peer. Every edit invalidates semantic validation, including distant structural effects.
3. **Mixed rule.** A range whose spans do not all agree on an attribute returns `TextPattern.MixedAttributeValue` (`HeadingStyleText.cs:155` precedent). A range with no span for an attribute returns the attribute's *not-supported* default (`AutomationElement.NotSupported`) — never `null`, which is what AvalonEdit returns today and what makes NVDA treat the whole document as attribute-less.
4. **`FindAttribute`.** Implemented for `StyleId` (forward and backward) over the queried range's spans; other attributes return `null` (unsupported) as the wrapped provider does. This is what lets an AT client jump heading-to-heading through the Text pattern when it chooses to; the app-owned chords remain the reading view's (W3-1) and are **not** added to the editor in this issue (owner decision §7.2).
5. **Events.** Keep AvalonEdit's `TextPatternOnTextSelectionChanged` (already raised, surfaced through the redirected `EventsSource`). Add `TextPatternOnTextChanged`, raised **once per edit batch** from the highlight coordinator's tick (`AvalonHighlightingCoordinator`, 40 ms `DefaultDebounce`, `EditorHighlighting.cs:191`) — never per keystroke — and only after `EndPeerUpdate` (`AvalonDocumentBufferSession.cs:408`), so a listener never observes a half-applied delta. Check whether AvalonEdit's `TextEditorAutomationPeer` already raises `ValuePattern.ValueProperty` changes on `TextChanged`; if it does, leave it; if not, do not add it (NVDA's focus-mode reading does not consume it, and a per-keystroke property-changed storm is a known AT-lag source).
6. **Threading and revision.** WPF marshals UIA provider calls onto the dispatcher, and the buffer session is dispatcher-affine (W2-1); the decorator asserts `Dispatcher.CheckAccess()` in debug and never touches the FFI off-thread. Between `BeginPeerUpdate` and `EndPeerUpdate`, and during IME composition, attribute queries answer *not-supported* for the affected range rather than reading a stale revision; the drift guard (W2-1) is what makes the idle answer exact.
7. **Ordinary Hyperlink children (owner-authorized revision, 2026-09-18).** Follow E-9 in `37_editor_peer_contracts.md`: six canonical link kinds expose ordinary Hyperlink descendants, range `GetChildren` / `GetEnclosingElement` / `RangeFromChild`, Invoke and meaningful canonical read-only Value. Nested spans form a consistent containment tree. Stable events reset WPF child caches. Local queries use revision-local canonical validation and indexed identity candidates without full root enumeration; explicit full tree requests may build the complete offscreen inventory. The generic range-valued Link attribute and its COM exporter are retired after the measured prototype. No new editor chords.
8. **Braille and IME are consumers, not features:** NVDA braille renders `link`/`heading-level` from the same format fields; IME composition (W2-1) must not storm `TextChanged` — one event at commit.

### 2.4 The span → UIA attribute table (candidate; the contracts doc freezes it after the per-AT measurement of §2.5)

| `EditorSpanKind` (`crates/slate-uniffi/src/lib.rs:6573`) | UIA text attribute(s) | What NVDA does at defaults | Required for acceptance |
|---|---|---|---|
| `Heading { level }` | `StyleIdAttribute` = `StyleId.Heading1…Heading6`; `StyleNameAttribute` = "Heading N" | speaks "heading level N" in line reading (`heading-level`) — the W3-1 precedent | **yes** |
| `Wikilink`, `Link`, `Embed` | Hyperlink child + Invoke + meaningful read-only Value; `StyleName` = "Wikilink" / "Link" / "Embed" | speaks "link" (`link`) on entering the range | **yes** |
| `Image` | Hyperlink child + Invoke + meaningful read-only Value ; `StyleName` = "Image" | "link" | yes |
| `Tag`, `Citation` | Hyperlink child + Invoke; `StyleName` (owner accepted both as links) | "link" / silent | owner call |
| `InlineCode`, `CodeFence`, `Code { token }` | `StyleName` = "Code" (+ `FontNameAttribute` if the editor renders a monospace face for them) | silent at defaults; "style Code" under report-style | no (parity: mac is silent too) |
| `BlockQuote` | `StyleId` = `StyleId.Quote`; `StyleName` = "Quote" | silent at defaults (G27) | no |
| `Emphasis` / `Strong` / `Strikethrough` | `IsItalicAttribute` / `FontWeightAttribute` = 700 / `StrikethroughStyleAttribute` = Single | spoken under "report font attributes" | no |
| `Comment`, `Frontmatter` | `StyleName` = "Comment" / "Frontmatter" | silent at defaults | no |

The **two required rows** are the parity differentiator the doctrine asks for (05 §6.4: semantic descriptions on ranges). Everything else is faithful exposure at zero extra risk. The table is a UIA-idiom mapping (allowed C#, decision 4); the span kinds and ranges never originate host-side.

### 2.5 Facts and evidence

- **Unit (xUnit, `SlateWindows.Tests`)** — `EditorSemanticTextRangeTests`: over a fixture note containing **every** `EditorSpanKind` variant (add one to the shared markdown corpus if none has all sixteen; the W2-2 §W-A `spans` artifact then pins it cross-platform for free): attribute value at a known offset per kind; Mixed across a boundary; not-supported on plain text; `FindAttribute` forward/backward for `StyleId`, including no-match; native Hyperlink identity, hierarchy, lifetime, destination, real activation and range roundtrips; a document-range `StyleId` query is Mixed after one FFI call; queries during a peer update and during composition answer not-supported; `TextChanged` raised once for a 20-keystroke burst inside one tick, never inside `BeginPeerUpdate…EndPeerUpdate`; the dispatcher-affinity assertion. Use `WorkPump`/an injected clock — never wall-clock sleeps.
- **Census** — `EditorPeerDoctrineCensus`: no `HostComposed`, no Markdown classification, no second FFI span entry point under the editor files; the mapping table in the contracts doc equals the switch in `EditorSemanticTextRange` (parse the source; the `CanvasLabelClassCensus` precedent).
- **Bench (§K convention)** — `GetAttributeValue(StyleId)` on a line range at 100 KB / 1 MB / 8 MB, full Hyperlink inventory at 8 MB plus local post-edit and dense-link cases; budgets pinned in the contracts doc from the first measurement (BenchmarkDotNet, `BENCHMARKS.md` "Milestone W7-1" row) with a flatness assertion — the §W-B discipline applied to reads.
- **FlaUI (CI's shell gate)** — `EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean`: open the fixture note in editing mode; `TextPattern.DocumentRange`; move by `Line`, `Word`, `Character`; `GetAttributeValue(StyleId)` and Hyperlink range/child roundtrips at the known offsets cross-process; `FindAttribute` both directions; `RangeFromPoint`; subscribe to `TextSelectionChanged` and `TextChanged`, type ten characters, assert one `TextChanged` and the selection events, and assert `TextChanged` precedes the following selection event; the **forward AND backward** UIA3 walk of the editor subtree completes and is identical (the #1088 pin, R-5); axe scan. Journey traps recorded on the W5/W6 journeys apply (foreground re-assert, async settle, Value-pattern text entry).
- **Human (W7-4 rows, `reports/w2_editor_at_checklist.md`)** — NVDA and JAWS separately: line/word/character reading speaks heading level and "link" at the right places; say-all (`NVDA+Down`, `Insert+Down`) reads the whole note in order and speaks the required semantics; arrowing by character across a wikilink boundary announces entry/exit as the AT's convention has it; selection with `Shift+Arrow` is announced; braille shows the link/heading markers (NVDA braille viewer suffices); IME: one composition commit = one reading update; JAWS-specific: whether JAWS honours `StyleId` in line reading and Hyperlink children at all (unknown — the row's outcome is the finding).

### 2.6 Traps (recorded on earlier waves; they recur here)

- `null` from `GetAttributeValue` is not "unsupported" to every client; return `AutomationElement.NotSupported`.
- A decorator that forgets to wrap the ranges returned by `Move*`/`Clone` leaks the undecorated range (the W3-1 first cut did exactly this).
- `HighlightInRange` moves the paint window; the peer must use `InspectInRange`.
- The CRLF format gate: run `dotnet format` after every edit batch (the tree is mixed-EOL).
- Local FlaUI is unreliable on a busy desktop; CI's shell gate arbitrates (`FluentShell` local failures are environment-only).

### 2.7 Acceptance

- [ ] `37_editor_peer_contracts.md` landed before round 1 with the frozen attribute table and the excerpts of AvalonEdit's and NVDA's sources it rests on
- [ ] Unit facts, the doctrine census, the bench row and the FlaUI journey green in CI; the #1088 walk pinned
- [ ] `w_c_matrix.md` "Editor document" row amended (patterns: `Text` with `StyleId`/`StyleName` attributes, `FindAttribute(StyleId)` and Hyperlink descendants; events: selection-changed + text-changed; evidence names the new facts); `reports/w2_editor_at_checklist.md` rows added (W7-4)
- [ ] G29 recorded; #1224 referenced from the contracts doc's "mac details recorded while reading" register
- [ ] Red team per `24_red_team_protocol.md`, codex pass, CI green, codoki

---

## 3. W7-2 · Notification dispatcher — slice B: mapping, etiquette, the §W-D census (#748) — one PR

### 3.1 Goal

Every announcement Windows makes is spoken with mac's **etiquette** (polite queues, urgent interrupts, bursts collapsed the same way) and the §W-D census proves, for the full corpus, that both hosts fire the same event under the same trigger — the text half is already proven by the corpus goldens.

### 3.2 What stands today

§0 item 2 (W7-2). The dispatcher core, the corpus censuses, the coalescers, the scan gate, the two family ledgers, the residue markers.

### 3.3 Design — four parts, one PR

**B1 — The priority mapping, corrected and pinned (G31).** `A11yPriority` has two tiers (`a11y.rs:65`; mac `AnnouncementPosting.swift:9–19` maps them 1:1 onto `NSAccessibilityPriorityLevel.medium/.high`). Under NVDA (`event_UIA_notification`, §0 item 3), `MostRecent` cancels current speech; so:

| Tier | Today | After B1 | Why |
|---|---|---|---|
| `High` | `ImportantMostRecent` | `ImportantMostRecent` (unchanged) | NVDA cancels current speech and speaks it (mac `.high` interrupts); Narrator honours the "supersede pending important" semantics; a High mid-say-all is spoken at `Spri.NOW` (NVDA #17986) |
| `Medium` | `MostRecent` | **`All`** | the only processing level NVDA queues without cancelling; mac `.medium` queues politely; the host coalescers (B3) are what keep the queue short, exactly as on mac. `CurrentThenMostRecent` is the alternative (finish current, drop older pending) — §7.3 |

`NotificationKind` stays `Other` (NVDA ignores it; nothing maps better), the activity id stays the single `slate-accessibility-announcement` (irrelevant under `All`; a per-family id would only refine Narrator's superseding of important ones — §7.3). Fact: `AccessibilityNotificationDispatcherTests` — exhaustive over the enum: for each tier the exact `(kind, processing, text, activityId)` raised (record the raise through a test peer); plus a source census that the dispatcher remains the only `RaiseNotificationEvent` caller and that MainWindow threads both seams (`AnnouncementSeamCensus` already does the latter — extend, don't duplicate).

**B2 — The scan-progress family moves to core (G30).** Three events, copy moved verbatim (the W0.5-3 rule — no redesign): `VaultScanStarted { total_files }` → "Scanning vault. {N} file|files to index.", `VaultScanProgress { indexed, total }` → "Indexed {i} of {N} file|files.", `VaultScanFinished { files_indexed }` → "Scan complete. {N} file|files indexed."; all `Medium`; pluralisation through core's existing count copy (the corpus already renders "1 recent file"/"2 recent files"). Four-place rule + the Windows mirror: `a11y.rs` (variants, `priority`, `render`, `corpus()` rows — singular and plural for each), `tests/fixtures/a11y/corpus.json` (regenerate with `SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y`, re-run clean), the `slate-uniffi` mirror, the Swift corpus mirror and `A11yCorpusCensus.cs` (the Rust tripwires fail `cargo test` until both mirrors list the new events). Mac: `announceScan` posts the typed event; its residue marker at `AppState.swift:23173` goes (`pinnedResidueSites` 28 → 27). Windows: `ScanAnnouncementGate` returns the typed events; its three markers go (52 → 49). **The 350 ms guard and the forced start/finish stay host-side** (timing; the mac twin is `scanAnnouncementMinInterval`, `AppState.swift:8305`); `ScanAnnouncementGateTests` (six facts) keep passing with the event identity asserted instead of text. `cancelled`/`failed` remain silent on both hosts (mac `handleScanProgress`, `:23150–23156`) — parity, not an omission.

**B3 — The etiquette audit: no new mechanism.** The issue's "land the MECHANISM now" predates W6; the mechanism shipped twice. Slice B **audits** every family that coalesces or rate-limits on mac, confirms the Windows twin, and pins each with a deterministic fact (injected clock / `WorkPump`, never sleeps). The register (a table in `38_…contracts.md`, one row per family: mac site, Windows site, rule, fact):

| Family | Mac rule (site) | Windows twin (site) | Fact to pin / gap to close |
|---|---|---|---|
| scan progress | 350 ms min-interval, start/finish forced (`AppState.swift:8305, 23163`) | `ScanAnnouncementGate` (2.5 s on Windows, forced; contract 38 D-4 as amended) | `ScanAnnouncementGateTests` — re-point at the B2 events |
| canvas navigation / filter | 200 ms latest-wins per class; High flushes and drops (`CanvasAnnouncer.swift`) | `Canvas/CanvasAnnouncer.cs` | shipped (`CanvasAnnouncerTests`, `CanvasAnnouncerCensus`) — cite, don't redo |
| graph navigation / filter / forceValue / settle | 200 ms latest-wins; filter fire-time gate; settle after convergence (`GraphAnnouncer.swift:185–238`) | `Graph/GraphAnnouncer.cs` | shipped (`GraphAnnouncerTests`, `GraphAnnouncerCensus`) — cite |
| sidebar filter count | 200 ms query debounce + `(query,total)` dedup (`Sidebar/SidebarFilterModel.swift:93,128`) | `FilesSidebarViewModel.Filter.cs:16` (200 ms) — **verify the dedup twin exists** | a fact: same query re-run → one announcement |
| palette filter count | announce on `filterAnnouncementText` change only (`CommandPaletteView.swift:141–148`) | `CommandPaletteViewModel` — verify change-only | a fact: unchanged count → silent |
| search results summary | 150 ms debounce + dedup against the last summary (`SearchOverlay.swift:103–113`) | `Search/SearchOverlayViewModel` — verify both | a fact |
| quick switcher count | 60 ms ranking debounce, announce on publish, initial count once (`QuickSwitcherModel.swift:69,131`) | `QuickSwitcherViewModel.cs:46` (`debounceRanking`) | a fact: N keystrokes inside the window → one count |
| Bases refresh announcements | per-refresh dedup of repeated messages (`Bases/AppState+Bases.swift:1886–1966`) | `Bases/…` — verify | a fact |
| sync-marker watcher | 2.5 s debounce, 4× ceiling — a *refresh* debounce, not an announcement coalescer (`SyncMarkerWatcher.swift`) | `SyncMarkerWatcher.cs:65` | out of scope for this register (already pinned by `SyncMarkerWatcherTests`); listed so the audit is exhaustive |

A family with **no** Windows twin is a finding: close it in this PR with the shipped shape (a class-keyed window like the announcers, or a change-only guard like the palette), never a new abstraction over all of them — the per-surface shape is the mac's, and R-2 pins only the class keys core-side. Any coalescing class added must join the ONE list at `a11y.rs:123` and its Rust-side mirror check.

**B4 — The §W-D census: the whole-corpus trigger ledger.** Generalise `scripts/graph_trigger_ledger.py` → `scripts/a11y_trigger_ledger.py`: the key set is every top-level `A11yEvent` arm in the generated binding (199 kinds today) with the canvas and graph families **folded in by reference** to their own ledgers (their rows already exist and are censused; do not duplicate them). Per key: role (posted / label / dialog-copy), mac site(s) (`postAccessibilityAnnouncement(.x` / `.post(.x` / `A11yEvent.x(`), Windows site(s) (`new A11yEvent.X(`), Windows fact(s), "observed end to end by" (the FlaUI journey or E2E fact that sees it delivered through the production seam, or `unit-observed:` with the fact), and designations. **Designation rules copy the graph's F4/FD-8**: a platform may lack a site only under an owner-recorded reason in the script's `DESIGNATED` map (e.g. a Windows-only affordance with no mac twin — the editor tag filter, G22's link default — or a mac-only one). `Censuses/A11yTriggerParityCensus.cs` validates the pasted table against both trees exactly as `GraphTriggerParityCensus` does (member-level construction, not string presence). Scale for planning: 149 mac `postAccessibilityAnnouncement(` sites, 292 Windows `new A11yEvent.` constructions (156 distinct kinds constructed on Windows today — the ledger will surface which of the other kinds Windows never fires and why). **The residue register**: every `HostComposed` site (52 markers Windows / 28 mac after B2: 49 / 27) is either converted in this PR or rowed as *designated residue* with its engine named (the D-14 refusal family, the dialog-guidance copy that serves double duty, the structural-mutation builder) — this is the owner's register, off-limits for review re-litigation once recorded.

### 3.4 Contracts to pin (candidates for `38_notification_dispatcher_contracts.md`)

D-1 the exact UIA parameters per tier (B1) · D-2 one `RaiseNotificationEvent` caller · D-3 the scan family's identity, copy and priority, both hosts (B2) · D-4 the guard's timing is host-side and injected (B2) · D-5 the etiquette register, one row per family, each with a deterministic fact (B3) · D-6 class keys live in one list core-side (B3) · D-7 the ledger's row grammar and designation rules (B4) · D-8 the residue register is owner-recorded and exhaustive (B4) · D-9 corpus changes are deliberate (regenerate, re-run clean, review the diff as a §W-D delta).

### 3.5 Facts and evidence

Unit: `AccessibilityNotificationDispatcherTests` (B1), `ScanAnnouncementGateTests` (B2, re-pointed), one fact per etiquette row (B3), `A11yTriggerParityCensus` + `A11yCorpusCensus` (B4). Rust: the scan events' render/priority tests and the corpus tripwires. Mac: the corpus census and the residue census re-pinned (27). FlaUI: no new journey — the **existing** journeys are the "observed end to end" column; add assertions where a family's delivery is observed nowhere today. Human (`reports/w7_2_notification_etiquette_checklist.md`, W7-4 form): under NVDA and JAWS separately — a burst of three polite announcements (e.g. three sidebar filter counts) is heard **in full**, none cut; a High announcement (`PaletteCommandUnavailable`) cuts current speech; a polite announcement during say-all does not stop say-all (NVDA queues it; JAWS: the outcome is the finding); the scan announcements on a 2,000-file vault are ~3/s and end with the summary. Narrator: smoke only.

### 3.6 Traps

- The Rust corpus tripwires read the two host mirror files; forgetting one fails `cargo test`, not the host build — read the failure.
- Regenerating the corpus fixture fails by design on the regenerating run; run again.
- `dotnet build` does not rebuild the DLL: a core change needs `cargo build --release -p slate-uniffi` + `generate-bindings.ps1` before any Windows fact sees it; a fresh worktree has no generated bindings.
- A test-injected sink cannot catch a production seam left at its no-op default (the W6-1 PR A lesson) — the seam census reads shipping call expressions.

### 3.7 Acceptance

- [ ] `38_notification_dispatcher_contracts.md` landed before round 1 (D-1…D-9 with the NVDA excerpt pinned)
- [ ] Mapping fact exhaustive over the enum; the dispatcher the sole raiser (census)
- [ ] Scan family core-side on both hosts; corpus regenerated deliberately; residue pins 49 / 27
- [ ] Etiquette register complete with a deterministic fact per row; any missing twin closed
- [ ] The ledger generated, pasted, censused; every key posted/label/designated on both platforms; the residue register recorded
- [ ] `w_c_matrix.md` header prose gains the etiquette contract line; the notification-contract column reconciled against the ledger; `reports/w7_2_notification_etiquette_checklist.md` rows added (W7-4)
- [ ] G30, G31 recorded; red team, codex, CI green, codoki

---

## 4. W7-3 · Spoken hotkeys + the AT navigation map (#749) — one PR

### 4.1 Goal

Every surface that speaks a chord speaks the table's derivation of it (R-3), drift is loud, and the mac AT-navigation affordances — native rotors, custom rotors, custom actions, custom content — each have a named, verified Windows mechanism in one table that W8-6 renders help from.

### 4.2 What stands today

§0 item 2 (W7-3). The table, both producers, the two live consumers (menus via `AcceleratorKey`, palette rows via `Name`), the three chord-table facts, the three drift tests, the reading navigator's 32 `chordSurface` rows, `WindowsSpokenFor` unused. **The remaining drift surface is hand-written**: HelpText literals naming keys and chords — `MainWindow.xaml:2176` (Quick Open: "Control Enter opens a new tab. Control Alt Enter opens a split."), `:2294` (search), `:2571` (palette), `:3675`/`:3733` (template prompts) — and any C# help string of the same shape. These are exactly the "per-surface hand-written speech strings" the issue forbids.

### 4.3 Design

1. **The chord-speech audit (deliverable 1).** `Censuses/ChordSpeechAuditCensus`: parse every string literal in `apps/slate-windows/src/SlateWindows/**/*.{xaml,cs}` (the `CSharpSource`/`XDocument` precedents of `CommandDriftTests`); any literal matching the spoken-chord grammar `((Control|Alt|Shift|Windows)\s)+[A-Z0-9]|Left Bracket|…` must be, or contain as a token, `WindowsHotkeySpoken.Spoken(row.WindowsChord)` for a row in `commands` ∪ `chordSurface`; bare key names (`Enter`, `Return`, `Escape`, `Tab`, `Space`, `Up`, `Down`, `Home`, `End`, `Page Up`, `Page Down`, `Delete`, `Backspace`, `F2`) are allowed by a recorded allowlist (§7.5). Where a literal names a chord of a *surface interaction* that has no `chordSurface` row yet (Quick Open's Ctrl+Enter / Ctrl+Alt+Enter are the likely finds), add the row — the table's coverage rule is "every chord the app delivers" (P12). Then **replace the literals** with composition through the table (a `ChordText`-style markup extension for spoken strings, `{local:SpokenChord id}`, so a XAML HelpText reads `"Up and Down move selection. Enter opens. {SpokenChord windows.quickOpen.openInNewTab} opens a new tab…"` — or an equivalent view-model property; one derivation either way). Live half: FlaUI `SpokenChords_MenusPaletteAndOverlays_MatchTheTable` — every menu item's `AcceleratorKey` equals its row's display chord; every palette row's `Name` ends with its spoken string; the Quick Open, search and palette HelpTexts equal the composed strings; axe.
2. **The AT navigation map (deliverable 2).** `docs/plans/18_windows_port/at_navigation_map.md` — the markdown table IS the source (parsed by the census, the `WcMatrix*EvidenceCensus` precedent). Four groups, one row each:
   - *Native VoiceOver rotors per surface* (headings, links, lists, tables, form controls, landmarks, text): reading view → the W3-1 chords (`windows.reading.next/previous{Heading|HeadingLevel1–6|Link|List|Table|Embed|CodeBlock}`, Ctrl+Alt+H/K/U(L)/T/E/C ± Shift, `Reading/ReadingNavigator.cs:63–84`) + the W-E7 layers where installed; editor → W7-1's `StyleId` attributes and Hyperlink children in line reading + the Outline panel (W4-2) for heading jumps (mac parity: the Outline sidebar; no editor chords — §7.2); panels, grids, trees → native UIA `Tree`/`Grid`/`Table` patterns on the substrate (W4-1) with the cell/row keyboard; landmarks → `AutomationLandmark` (W1).
   - *Mac custom rotors* — the three canvas outline rotors Cards / Groups / Connections (`CanvasOutlineView.swift:158–171`) → the Windows canvas outline tree's kind filter and the navigator's next/previous card/group commands and the card's connection rows (name the `slate.canvas.*` rows from `chords.json`; cite the W6-1 checklist item that says "rotor equivalents: not applicable — UIA has no rotor; the tree's levels are the navigation structure").
   - *Mac custom actions* — all 18 sites, each → the Windows mechanism: sidebar/tag-tree Expand/Collapse (`FileTreeSidebar.swift:7103`, `SidebarTagTreeView.swift:193`) → `ExpandCollapse` pattern + Right/Left; grid row actions (`AccessibleDataGrid.swift:714`, `BaseListRenderer.swift:514`) → the substrate's row-action menu (`Shift+F10` / Application key, W4-1); Bases dashboard Remove section / Pick replacement (`DashboardViews.swift:168–169`) and builder Select Row (`BaseQueryBuilderSheet.swift:1042`) → their W4-6 buttons/rows; graph Switch to Table / node actions / Pin–Unpin (`GraphDiagramView.swift:652,812,820`) → the diagram's actions menu and the tier-B summary Invoke (W6-2 PR D); canvas Open / Toggle Mark / Jump to Card / Edit Connection / Delete Connection (`CanvasOutlineView.swift:226–227,360–364`) → the canvas navigator commands (W6-1 PR C).
   - *Mac custom content* — math Source (`MathView.swift:70–71`), mermaid Source (`MermaidView.swift:62`), graph "Connects to" (`GraphDiagramView.swift:887`) → `MathMlUiaProperty` + HelpText (W3-2, G23), the diagram description (W3-3), `GraphNeighborsContent` HelpText (W6-2).
   Each row: mac affordance (file:line) · Windows mechanism (pattern / automation id / menu) · chord row id (if any) · **status** ∈ {verified (FlaUI or unit fact named), pending-AT (checklist row named), designated (owner reason)} · checklist reference. `Censuses/AtNavigationMapCensus`: (a) completeness — every `.accessibilityRotor(`, `.accessibilityAction(named:`, `NSAccessibilityCustomAction(`, `.accessibilityCustomContent(` site under `apps/slate-mac/Sources` has a row (read through `SwiftSource`, the `MacCatalogParityTests` precedent; the disposition list checked for staleness both ways); (b) truth — every named chord row, automation id and pattern exists in the Windows tree; (c) every status cites something that exists.
3. **Help docs (deliverable 3 — re-scoped, G32).** W7-3 does not touch `docs/help/`. It ships the source (the map + `chords.json`) and records in the map's header what W8-6 renders from it (per-platform chord tables, the mapping rows) and where its drift test lives (W8-6). Recording that hand-off here is the whole of item 3.

### 4.4 Contracts to pin (candidates for `39_at_navigation_contracts.md`)

N-1 one derivation (R-3) · N-2 the key-name allowlist, exhaustive · N-3 surface-interaction chords have `chordSurface` rows (P12 coverage) · N-4 menus advertise display chords (`AcceleratorKey`), names/help compose spoken chords — the two forms are the table's two columns, never mixed · N-5 the map's four groups and row grammar · N-6 completeness against the mac source, both directions · N-7 the W8-6 hand-off recorded.

### 4.5 Evidence and acceptance

- [ ] `39_at_navigation_contracts.md` landed before round 1; the map committed as its first register
- [ ] `ChordSpeechAuditCensus` green with zero unexplained literals; the found literals replaced by composition; new `chordSurface` rows delivered with evidence (the generator's `--check` stays green)
- [ ] `AtNavigationMapCensus` green (complete both ways; every status cited)
- [ ] FlaUI `SpokenChords_MenusPaletteAndOverlays_MatchTheTable` green in CI
- [ ] `w_c_matrix.md` Name/HelpText-source cells updated where surfaces gained composed strings; `reports/w5_commands_at_checklist.md` gains the spoken-chord rows (NVDA/JAWS read a menu accelerator, a palette row name, the Quick Open help) — W7-4
- [ ] G32 recorded; red team (a table-completeness pass), codex, CI green, codoki

---

## 5. W7-4 · The §W-C instrument: matrix, CI gate, checklists, human passes (#750) — rolling

### 5.1 What stands today (verified)

§0 item 2 (W7-4). The instrument is live and has rolled through six waves; what it lacks is (a) machine pinning for the W1–W5 rows, (b) checklist files for every wave but W6, (c) recorded human evidence beyond W3-1's NVDA pass, and (d) rows for this wave's three feature issues.

### 5.2 Obligations, concrete

1. **One authoring PR now (before the W7 feature PRs merge):**
   - `Censuses/WcMatrixEvidenceCensus.cs` — the generalisation of the canvas/graph twins: every automation id the shell sets (`AutomationProperties.AutomationId=` and `SetAutomationId(` literals, composed prefixes, XAML ids in `MainWindow.xaml`/`WorkspaceTemplates.xaml`) maps to a matrix row; every row has ten cells; every backticked evidence name resolves in the test tree; every axe label is scanned by a journey; human cells are `Pending…` until a named run. The canvas/graph manifests fold into it (keep their censuses as thin delegations or retire them — the contracts note says which).
   - `reports/_at_pass_template.md` — the recording form, lifted from `w3_1_nvda_field_verification.md`'s header (tester, AT + version, OS edition + build, branch + verified commit, corpus, method, transcript location), so every pass is comparable.
   - The missing checklist files, in the W6 form (spec item · check · UIA route · observable outcome · automated twin · Narrator · NVDA · JAWS): `reports/w1_shell_at_checklist.md` (W1-1…W1-4), `reports/w2_editor_at_checklist.md` (W2-1…W2-5; W7-1's rows are added by #747), `reports/w3_content_at_checklist.md` (W3-2…W3-5; W3-1's record stands and its remaining optional item is listed), `reports/w4_panels_at_checklist.md` (W4-1…W4-8), `reports/w5_commands_at_checklist.md` (W5-1…W5-4; W7-3's rows added by #749), `reports/w7_2_notification_etiquette_checklist.md` (added by #748). The rows come from each issue's spec acceptance lines and the matrix row's "focus order and keyboard route" cell — not invented.
   - The parity matrix gains **four surface rows** consumed by W7-1…W7-4 (today it has zero W7 rows, so §W-F cannot see this wave): extend `scripts/generate-parity-matrix.py`'s primary-surfaces table from `chords.json` `deliveryEvidence` groups the W7 PRs fill (the W6-2 PR F issue-level-evidence precedent, generator `:733`).
2. **Per wave close (one reconciliation PR against this issue), the rule restated:** every row's evidence cell names tests that exist; every pattern claim matches the shipped peers; every automation id has a row (the census makes all three mechanical); the one axe waiver is re-justified or removed; new human evidence is recorded or the pending set restated. W7's own close adds the editor row amendment (W7-1), the etiquette line (W7-2) and the Name-source cells (W7-3).
3. **The AT backlog burn-down**, owner-scheduled, recorded per R-6. Order that respects the feature PRs: pass W1, W3-2…W3-5, W4, W5 rows **now** (nothing in W7 changes them except W7-3's spoken names on the palette/menu rows — pass those after #749); pass the editor rows **after #747**; pass the etiquette rows **after #748**; canvas and graph rows per their own checklists (W6 residuals, still owned by #745/#746's sign-off). JAWS is the owner-provided prerequisite (program §Working independently); Narrator smoke rows are required only at W8-6 unless the owner says otherwise (§7.6).
4. **W8 hand-off.** At milestone close the matrix + the recorded passes are W8-6's input (WGA-9); "close modulo the residual" is not a state.

### 5.3 The backlog (state on 2026-09-16)

| Rows (matrix surface) | Checklist file | NVDA | JAWS | Narrator | Blocked on |
|---|---|---|---|---|---|
| W1 shell, sidebar, workspace, Quick Open (rows 1–10) | `w1_shell_at_checklist.md` — **to write** | Pending | Pending | Pending (smoke) | — |
| W2 editor document + interactions (rows 11–12) | `w2_editor_at_checklist.md` — **to write** | Pending | Pending | Pending | #747 |
| W3-1 reading view | `w3_1_nvda_field_verification.md` (record) | **verified 2026-07-26/27** (Cory Joseph, NVDA 2026.1.1) | Pending | Pending | — |
| W3-2…W3-5 math, diagrams, code, embeds | `w3_content_at_checklist.md` — **to write** | Pending | Pending | Pending | — |
| W4-1…W4-8 panels and data | `w4_panels_at_checklist.md` — **to write** | Pending | Pending | Pending | — |
| W5-1…W5-4 commands, search, templates, file management | `w5_commands_at_checklist.md` — **to write** | Pending | Pending | Pending | #749 for the spoken-name rows |
| W6-1 canvas (8 rows) | `w6_1_canvas_at_checklist.md` (exists, 10 items) | Pending | Pending | Pending | — (owned by #745 sign-off) |
| W6-2 graph (5 rows) | `w6_2_graph_at_checklist.md` (exists, 11 items) | Pending | Pending | Pending | — (owned by #746 sign-off) |
| W7-2 notification etiquette | `w7_2_notification_etiquette_checklist.md` — by #748 | Pending | Pending | Pending | #748 |

### 5.4 Acceptance (rolling)

- [ ] Authoring PR: `WcMatrixEvidenceCensus` green over every row; template + the five missing checklist files committed; four W7 surface rows in the parity matrix
- [ ] Each wave close: rows present and reconciled by the census; axe gate green on every PR of the wave; the waiver re-audited; new evidence recorded or the pending set restated
- [ ] Final (W8-6): every row's NVDA and JAWS cells carry a named run; Narrator smoke recorded; WGA-9 satisfied

---

## 6. Cross-cutting process, order and gates

### 6.1 Execution order

**W7-4 authoring PR → W7-1 → W7-3 → W7-2 slice B → W7-4 wave-close reconciliation**, with the AT backlog burned down in parallel per §5.2 item 3. Rationale: the authoring PR unblocks human passes on five waves immediately; W7-1 is the largest and the only one that changes what an existing row is verified against; W7-3 and W7-2B touch disjoint code (chords/XAML vs dispatcher/core/ledger) and **may run in parallel on separate branches** if capacity allows — they collide only in `w_c_matrix.md` and `chords.json`, both mergeable; W7-2B last because its ledger must see W7-1's and W7-3's final trigger sites. Confirm or amend in §7.7.

### 6.2 Contracts and review

Per [`24_red_team_protocol.md`](../../24_red_team_protocol.md): the contracts document (37/38/39) lands as its own commit **before** round 1, with numbered contracts and code citations, the accepted-risk / recorded-divergence / owner-decision registers (off-limits for re-litigation), and a note for any contract whose surface does not exist yet. Contract numbering is per document (`E-1…` editor, `D-1…` dispatcher, `N-1…` navigation). Rounds are invariant-targeted, `xhigh`, with stop rules 4/5; each round's record appends to the document. A codex adversarial pass follows the rounds (the W6 loop); codoki on the PR.

### 6.3 Definition of done per PR (in addition to the wave DoD)

- `cargo fmt --check` + clippy for any core change (W7-2B); `dotnet format apps/slate-windows/SlateWindows.slnx --include <files>` after **every** edit batch (the CRLF gate); regenerate bindings after any core change before running Windows facts.
- Unit suites + censuses local; FlaUI journeys arbitrated by CI's shell gate; a shell-wide fix re-runs the whole journey suite.
- Read every CI lane's log and match **failures**, not names (a lane skipped behind a failing upstream hides its assertions).
- Pre-review self-QA: journeys assert rendered content and real input paths; mutation-verify each census (reinstate the bug, watch it fail); interleaving facts only in suites that run the production scheduling mode.

### 6.4 Evidence ledger (what each gate means for W7)

| Gate | W7 artifact | Lands in |
|---|---|---|
| §W-A | the all-kinds span fixture's `spans` artifact (W2-2 harness rows extended) | W7-1 |
| §W-C | the editor journey + row amendment (W7-1); the spoken-chord journey + Name cells (W7-3); `WcMatrixEvidenceCensus` over every row; the checklists (W7-4) | W7-1, W7-3, W7-4 |
| §W-D | the scan family in the corpus + both mirrors; the whole-corpus trigger ledger + census; the residue register; the etiquette register | W7-2B |
| §W-G | doctrine censuses: no classification / no HostComposed under the editor (W7-1); no literal spoken strings (W7-3) | W7-1, W7-3 |
| §K | attribute-read and `FindAttribute` benches, `BENCHMARKS.md` row | W7-1 |
| Matrix | four W7 surface rows; `chords.json` delivery evidence per PR; generator `--check` in CI | W7-4 authoring, each PR |
| Human AT | the checklists, the recorded passes (R-6) | rolling; final at W8-6 |

---

## 7. Owner decisions required (record the answer in the relevant contracts doc; none blocks the W7-4 authoring PR)

1. **W7-1 — tags and citations as links?** Both are activatable (Ctrl+Enter, W2-3); exposing `LinkAttribute` makes NVDA say "link" at defaults. Recommendation: yes for both, measured on NVDA first; record the JAWS outcome.
2. **W7-1 — editor structural-navigation chords?** The mac editor has none (Outline sidebar); the reading view has the W3-1 chords. Recommendation: **no** in W7-1 (parity + G21's key-namespace argument); note it as a reserved enhancement only if a tester asks.
3. **W7-2 — `All` vs `CurrentThenMostRecent` for Medium; one activity id vs per-family.** Recommendation: `All` + one id (the simplest mac-faithful queue; per-family ids buy only Narrator refinements).
4. **W7-2 — the residue register's designations** (which `HostComposed` sites stay, with reasons): the D-14 refusal family, dialog-guidance copy, the structural-mutation builder are the expected keeps; everything else converts.
5. **W7-3 — the key-name allowlist** (§4.3 item 1) and whether the reading surface's own HelpText should list its navigator chords (a new composed string; recommendation: yes, from the `chordSurface` rows).
6. **W7-4 — burn-down order and Narrator scope per wave** (§5.2 item 3); confirmation that the JAWS licence is in hand for the passes.
7. **§6.1 order** — confirm, or run W7-3 ∥ W7-2B.

---

## 8. Wave DoD (the checkboxes the issues close against)

- [ ] W7-1: TextPattern semantic attributes + events shipped; FlaUI + unit + bench + doctrine census green; #1088 pin; NVDA and JAWS editor rows recorded (JAWS outcome may be a finding, not a fail)
- [ ] W7-2: mapping corrected and pinned; scan family core-side on both hosts; etiquette register complete; whole-corpus trigger ledger + census green; residue register recorded; etiquette rows recorded per AT
- [ ] W7-3: chord-speech audit green; literals replaced; the AT navigation map committed, censused complete and true; spoken-chord journey green; W8-6 hand-off recorded
- [ ] W7-4: authoring PR merged (census, template, five checklists, four matrix rows); every wave's rows reconciled at its close; all NVDA/JAWS cells recorded by W8-6 (WGA-9)

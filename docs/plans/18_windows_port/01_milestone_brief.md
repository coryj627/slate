# Milestone 22 description — vendored verbatim (2026-07-06)

*This file vendors the [GH milestone 22](https://github.com/coryj627/slate/milestone/22) description as it stood when the program was drafted, so the program's divergences (see [`specs/gap_analysis.md`](specs/gap_analysis.md)) diff against a pinned text, not a moving one. The program ([`00_program.md`](00_program.md)) supersedes this text where they differ.*

---

⏸ **PARKED — do not start.** A 100%-parity port needs the macOS feature set **complete first**, so this is sequenced **after Canvas (Milestone T) and Graph (Milestone P) ship on macOS**. Graph is also the hardest a11y surface (`07` §3): make its accessible textual representation **canonical-in-Rust during Milestone P**, not rebuilt in C#, so the port reuses it. Revisit once T + P are delivered and the Mac alpha is feature-complete.

---

**Goal:** Ship the Windows app at **100% capability parity** with the macOS alpha, on the **same `slate-core` Rust backend** (zero re-implementation of Markdown/structure/vault/search/citation logic in C#) and a **WPF + AvalonEdit** host with full **UIA** accessibility (JAWS/NVDA) matching the VoiceOver semantics. Grounding: `docs/plans/05_locked_architecture_decisions.md` (§2.2/§2.5/§3/§5.4/§6.4) + `docs/plans/07_portability_review.md` (the portability + a11y reuse review). Per `05` §3 — months 9–12; Mac + Windows are the two desktop targets, both required before mobile.

### Layer fates (`07` §2)
- `slate-core` — **reused as-is** (cross-platform Rust). `slate-uniffi` (FFI) — **reused, re-bound for C#**. Only the macOS **Swift UI is rewritten** as a WPF host. This milestone is host + bindings + UIA, **not new backend logic**.

### Locked stack (`05` §2.2 / §2.5 / §5.4)
- **WPF** — *not* WinUI 3 (AvalonEdit's 15+ yrs of UIA hardening with JAWS/NVDA wins for an a11y-first product; WinUI would mean a custom accessible editor from scratch).
- **AvalonEdit** — the embedded native editor surface under declarative WPF chrome.
- **WPFMath/xaml-math** (math) + **SharpVectors** (SVG/diagrams) + a custom **UIA `AutomationPeer`** for math (MathML + MathCAT speech).
- **WPF DataGrid** (UIA-native) for accessible grids; `SystemParameters.HighContrast`; **no webview shell** (prohibited).

### W0 · The C# binding — the #1 reuse risk, do this FIRST (`07` §4.1)
`05` §2.3 named **csbindgen**, but it's a *raw P/Invoke* generator — it does **not** replicate uniffi-rs's object-model + foreign-callback codegen, which Slate leans on heavily (`VaultSession`, `DocumentBuffer`, `CommandRegistry` objects + Arc lifetimes; the `ScanProgressListener` callback; `CancelToken` cancellation). Raw csbindgen means hand-writing the C-ABI shim for opaque-handle lifetime, callback marshalling, and cancellation.
- **First action:** a C# smoke test exercising the **callback + object-handle + cancellation** patterns (not a free function — that's where csbindgen breaks down), and a **`uniffi-bindgen-cs` (NordSecurity) vs csbindgen** spike — pick whichever collapses the shim. *Then* bind the full surface. Native build x64 + ARM64 + CI.

### W0.5 · Pre-port canonicalization — push Swift-only logic into Rust (`07` §3)
Some macOS logic shipped in **Swift, not `slate-core`**; pushing it down first is what makes the port *reuse* instead of *re-skin*:
- **Command palette** — fuzzy-match + ranking + recents live in `CommandPaletteModel` (Swift). Push to Rust so Windows reuses ranking/recents, not just the registry.
- **A11y announcements** — `postAccessibilityAnnouncement` trigger logic + strings live in Swift. Move to a **canonical a11y-event vocabulary** so Windows `UIA RaiseNotificationEvent` fires the same notifications with the same text.

### Parity workstreams — every macOS capability on the locked stack
- **W1 · App shell + vault** — WPF window + split-view chrome; vault picker, recent vaults, file-list sidebar, welcome; Windows path handling over the shared `VaultProvider`. *(SlateMacApp, MainSplitView, VaultPicker, RecentVault(s), FileListSidebar, WelcomeView.)*
- **W2 · Editor surface (AvalonEdit)** — wrap AvalonEdit over the stateful `DocumentBuffer` (edit deltas, drift guard, debounced highlight); syntax from the canonical Rust spans via a `DocumentColorizingTransformer` (**#381** — the reuse payoff: AvalonEdit consumes spans instead of reimplementing the ~21 classification passes in C#); wikilinks/tags/citations-inline/embeds/code/math in-editor. *(NoteEditorView, NoteContentView, EditorSyntaxPalette, EditorEmbedSpans, EmbedView.)*
- **W3 · Rendering** — math (WPFMath + the math UIA peer; budget 2–4 wks per §5.4), diagrams/Mermaid (SharpVectors SVG/image), code-block highlighting (Rust code-internal spans). *(MathView/MathPrefs, MermaidView, CodeBlockView/CodePrefs.)*
- **W4 · Knowledge panels** — backlinks, outgoing links, outline, embeds, tasks (+ review), properties (+ editor rows + add-property), citations (panel, popover, summary, bibliography). *(BacklinksPanel, OutgoingLinksPanel, OutlineSidebar, EmbedsPanel, TasksPanel/TasksReviewView, PropertiesPanel/PropertyEditorRow/AddPropertySheet, CitationsPanel/CitationPopover/CitationSummarySheet/BibliographyPanel.)*
- **W5 · Commands, search, templates, bulk ops** — command palette (+ recents) over the now-canonical Rust registry/ranking (W0.5), search overlay, template picker/prompt, bulk rename. *(CommandPaletteView/Model/RecentsStore/SlateCommands, SearchOverlay, TemplatePicker/TemplatePromptSheet, BulkRenameSheet.)*
- **W6 · Accessibility (UIA) — the load-bearing parity** — custom `AutomationPeer` ranges exposing the editor's semantic spans to JAWS/NVDA (`05` §6.4); accessible WPF DataGrid (`05` §8.7); math AutomationPeer; spoken hotkeys; AT navigation (Mac custom-rotor model → UIA); the canonical a11y-event notifications from W0.5. *(AccessibilityExtensions, AccessibleDataGrid, HotkeySpoken.)*
- **W7 · Settings, prefs, theming** — settings UI; prefs store (JSON parity with PrefsJsonStore); dark/light + high-contrast (`SystemParameters.HighContrast`), system font size; the **APCA Lc>75** contrast gate ported to the WPF UI. *(SettingsView, PreferencesStore/PrefsJsonStore, Math/CodePrefs.)*
- **W8 · Packaging + parity verification** — signed MSIX + auto-update; Windows CI (build/test + an a11y-check equivalent); plus the acceptance gates below.

### Acceptance — 100% parity
- Every macOS capability above is available and functional on Windows.
- **Zero** C# re-implementation of Markdown classification, structure, vault, search, citations, tasks, properties, or command ranking — all from `slate-core`.
- **Accessibility parity:** JAWS + NVDA get the same semantics VoiceOver gets (editor spans, math, data grids, navigation, notifications); APCA Lc>75 holds.
- **Behavioral parity:** the same vault yields the same spans/structure/search/backlinks on both platforms (cross-platform differential check).
- **Performance parity:** the editor keystroke path (the shared `DocumentBuffer`) holds its flat O(edit) profile through the C# marshalling.
- Ships as a signed MSIX on x64 + ARM64.

# W7 executable spec — The UIA accessibility program (cross-cutting)

Issues: W7-1 ([#747](https://github.com/coryj627/slate/issues/747)) · W7-2 ([#748](https://github.com/coryj627/slate/issues/748)) · W7-3 ([#749](https://github.com/coryj627/slate/issues/749)) · W7-4 ([#750](https://github.com/coryj627/slate/issues/750)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue, except W7-4 (a rolling gate: one PR per wave-close against the same issue) and **W7-2 (two slices against the one issue: the dispatcher core lands with Wave 1, the priority-mapping/coalescing completion + §W-D census land with Wave 5)** — both per the W6 stacked-series convention: the issue stays the unit of acceptance.
Program: [00_program.md](../00_program.md) (decisions 6, 11; DoD §W-C/§W-D). This wave is **the load-bearing parity** — everything else is furniture if JAWS/NVDA can't drive it.

*Interleaving rule (program wave table): W7-1 lands with Wave 2 (editor); **W7-2's dispatcher core lands with Wave 1** (shell announcements consume it from W1-1 on) while its full §W-D census closes with Wave 5; W7-3 with Wave 5; W7-4 rows close per wave — this spec exists so the UIA work has one owner-view, not so it happens last.*

## W7-1 · Editor AutomationPeer: semantic ranges — PR 1

1. Custom peer for the AvalonEdit host exposing TextPattern plus **semantic span descriptions** (05 §6.4): span kind/role surfaced on ranges so JAWS/NVDA convey wikilinks, tags, citations, embeds, code, math regions as VoiceOver does via the mac span consumer.
2. Consumes the same windowed span data W2-2 renders (one source, two consumers — colorizer + peer).
3. Caret/selection events, reading-by-line/word/character correctness, and span-boundary announcements verified per AT (JAWS + NVDA behave differently around TextPattern — both are acceptance targets).

- [ ] TextPattern + semantic exposure verified with JAWS + NVDA scripts (FlaUI where automatable, human checklist where not)

## W7-2 · Notification wiring: canonical events → UIA — PR 2

1. One notification dispatcher: canonical `A11yEvent` (W0.5-3) → `RaiseNotificationEvent` with priority mapping (`AnnouncementPriority` → UIA NotificationProcessing/Kind) and throttling/coalescing parity with mac etiquette (scan progress, filter counts, canvas announcer coalescing). **Sequencing: the dispatcher core ships with Wave 1** (W1-1 consumes it); this issue completes the priority mapping, coalescing parity, and the census.
2. §W-D census: full event corpus → same text, same trigger conditions, both platforms.

- [ ] Dispatcher + §W-D census green

## W7-3 · Spoken hotkeys + AT navigation model — PR 3

1. Spoken-hotkey parity (`HotkeySpoken` semantics): command surfaces expose speakable chord strings from the chord table (W5-1) via HelpText/AcceleratorKey properties consistently.
2. AT navigation model: the mac custom-rotor/navigation affordances re-expressed in UIA idiom (headings/links/landmarks native per W3-1; app-specific navigation — e.g. canvas rotors, panel cycling — as documented keyboard commands + peers). The mapping table (mac rotor → Windows mechanism) is the deliverable and feeds the help docs.

- [ ] Chord speech audit green; navigation mapping table shipped + verified

## W7-4 · JAWS/NVDA conformance matrix + per-surface checklists — PR 4 (rolling)

1. The §W-C matrix instrument: per surface — control types, Name/HelpText sources, patterns, focus order, notifications, per-AT smoke result (JAWS, NVDA, Narrator-smoke). Lives in this directory, updated at each wave close; wave close requires its rows green.
2. axe-windows in CI via FlaUI launch scenarios (decision 11) — 0 failures gate, per-surface suppressions documented inline (the a11y-baseline convention, ported).
3. Human AT passes recorded per the T convention (checklist files + results); the final full pass is W8-6's release residual.

- [ ] Matrix instrument + CI gate live from Wave 1; all waves' rows green by W8

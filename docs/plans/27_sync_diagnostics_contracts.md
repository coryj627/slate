# W4-8 sync diagnostics — contracts before implementation

Issue [#740](https://github.com/coryj627/slate/issues/740) (spec §W4-8). The register
discipline of `24_red_team_protocol.md` / `26_history_contracts.md` applies: everything
below is DECIDED before the first line of implementation; reviews verify the code
against these registers and do not re-litigate them. Research basis: the 2026-08-09
core/mac report (sync_detect.rs probe census, SyncDiagnosticsPanel.swift string
inventory, SyncMarkerWatcher.swift design) and the Windows substrate brief (both
attached to the arc log).

**Load-bearing research facts this register builds on:** the Dropbox/OneDrive probes
ALREADY exist core-side with normative recommendation copy and full fixture coverage
(`sync_detect.rs:404/:459`); `DetectSync()`/`LivesyncConfig()` are bound, generated,
and unconsumed on Windows; core's vault watcher is a Milestone-A stub and the scanner
skips dot-entries, so no marker event ever reaches the host today; core `a11y.rs` has
NO sync event — the mac announcement is designated `.hostComposed` residue
(`AppState.swift:11702`, pinned in the residue census).

## Contracts

**SD1 — Core executes, the host consumes.** Every provider display name,
recommendation sentence, evidence path, multi-sync warning, and summary sentence is
core's verbatim (`DetectedSyncProvider.DisplayName/Recommendation/EvidencePaths`,
`SyncDetectionReport.MultiSyncWarning/AudioSummary`). The host composes ONLY the
recorded label families in SD10. Never re-render, truncate, or re-case core copy.

**SD2 — Five mutually-exclusive states,** chosen by a pure static selector with the
mac precedence: unsupported → error → loading → empty → populated
(`SyncDiagnosticsPanel.state`, mac `:37-44`). Unsupported renders
`report.AudioSummary` ("Sync detection isn't available for this vault type.");
empty renders `report.AudioSummary` ("No sync systems detected."). See SDD-2.

**SD3 — The populated surface, in order.** (1) Header row: the count label
"Sync, {n} {system|systems} detected" (SD10 golden) as a Level3-style header +
a "Refresh" button (visible label "Refresh", accessible name "Refresh sync
diagnostics" — WCAG 2.5.3 contiguous-prefix rule). (2) The multi-sync warning row
FIRST when `MultiSyncWarning` is non-null: one combined accessible element,
"Warning: {core sentence}", warning-brush, focusable. (3) Per-provider rows in
core's report order: each row is ONE combined accessible element named
"{DisplayName}: {risk word}. {Recommendation}" (risk badge = glyph + word, never
color alone), followed by a SEPARATE operable "Evidence" expander containing one
focusable line per `EvidencePaths` entry, verbatim, text-selectable where the
platform allows. (4) The LiveSync config section ONLY when a `.LiveSync` provider
is detected AND `LivesyncConfig()` returned: header "LiveSync configuration", then
either six config rows (each one combined element "{label}: {value}"), or the
malformed line "LiveSync config could not be read: {reason}", or "LiveSync plugin
present; no config found." — all SD10 goldens except {reason}, which is core's.

**SD4 — Detection lifecycle (mac parity; no reveal refresh).** Detection runs at
exactly three triggers: (a) vault open, in the post-scan continuation, ARM-THEN-PROBE
(the watcher is live before the initial probe — a marker landing between them must
not be invisible forever); (b) the explicit refresh (menu command or the panel
button); (c) a watcher fire. Revealing the leaf does NOT re-run detection — the mac
loads once per vault open and the watcher covers staleness; the W4-6/W4-7 Windows
reveal-refresh convention deliberately does not apply here (recorded: the data is
vault-scoped and watcher-fresh, not note-scoped and event-starved). Vault switch
clears the report/config/error and stops the watcher; vault close and dispose tear
the watcher down.

**SD5 — Publish discipline.** Both FFI calls (`DetectSync` + `LivesyncConfig`) run
in ONE off-dispatcher hop (the mac single detached task). Publishes are guarded by
(shutdown, session identity, monotonic load sequence) — a stale probe result never
clobbers a newer one; the sequence is bumped synchronously on the caller thread
before the work starts. FFI failure publishes the error state with core's message
relayed ("Could not load sync diagnostics: {message}") and a Retry button; it never
crashes the leaf and never wipes a previously rendered report except by replacing
the whole state (mac behavior: error replaces).

**SD6 — Announcement contract (exactly one family).** The ONLY sync announcement is
core's pre-rendered `report.AudioSummary`, relayed as
`A11yEvent.HostComposed(AudioSummary, High)` and gated to: (a) fire only when
`MultiSyncWarning` is non-null OR any provider is High risk; (b) at most once per
vault path per session (the compaction-relay gate pattern). Low/Medium-only and
empty reports are silent; manual refresh completion is silent; watcher republish is
silent unless (a)+(b) admit. This is the W0.5-3 residue designation the mac pins
(`AppState.swift:11702`) carried to Windows with a matching residue comment — a
canonical-event conversion would be a four-place mirror change (core enum + corpus +
uniffi + both hosts) and is deliberately OUT of W4-8 (recorded follow-up candidate,
owner's future call). Leaf switches announce only the generic `LeafPanelShown`
(the ActiveLeaf setter already does this); no sync-specific reveal event exists in
core and none is added.

**SD7 — The refresh command.** `slate.diagnostics.refreshSync`, CHORDLESS (mac:
"refresh is a rare, deliberate action"), surfaced as the WorkspaceMenu item
"Refresh _Sync Diagnostics" plus the panel's own Refresh button. It re-runs
detection idempotently and does NOT reveal the leaf (mac parity — the mac command
only refreshes; no `showPanel` command exists in the registry for this leaf, and
inventing one would create a command row mac doesn't have). CanExecute requires an
open session. CORRECTION RECORDED: the issue body's 2026-08-09 delivery-spec
sentence "reveals the leaf and re-runs detection" is superseded by this register —
research showed the mac command never reveals.

**SD8 — The marker watcher (the #638 twin).** Bounded scope: exactly three
NON-recursive watches — the vault root, `.obsidian`, `.obsidian/plugins` —
entry-level events only (create/delete/rename; content writes to arbitrary files in
the root count as entry churn only where the OS reports them on the watched dir
itself), never a recursive watch, never content reading. Debounce: 2.5 s trailing
quiet period PLUS a max-latency ceiling at 4× the debounce anchored on the burst's
FIRST event (the #638 anti-starvation finding — continuous sub-interval churn must
not defer detection forever); both intervals injectable for tests. Identity: each
armed watch carries a monotonic generation; a fired handler compares its captured
generation before acting (the ABA rule); re-arm when a watched child dir appears;
drop-and-re-arm-via-parent when a watched dir is deleted/renamed. Lifecycle: owned
by the vault lifecycle (armed before the initial probe, stopped at vault
close/switch/dispose); `Stop` is idempotent and a stopped watcher NEVER invokes the
callback; callbacks marshal to the UI context and re-check liveness there. A fire
triggers exactly one debounced re-detection through SD5's guarded pipeline.

**SD9 — The core Windows-probe slice (no FFI change).** In
`crates/slate-core/src/sync_detect.rs` only: (1) `RealFs::home()` falls back to
`USERPROFILE` when `HOME` is unset (Windows processes set only the former; every
`$HOME`-prefix arm silently no-ops today — pinned degradation, now repaired).
(2) The private `FsProbe` trait gains `env_var(&self, name) -> Option<String>` and
`read_to_string_bounded(&self, path, max_bytes) -> Option<String>`; `FakeFs` gains
matching builders (env map + file contents) so no fixture ever reads the real
environment. (3) OneDrive gains an env-var arm: the vault's canonical path lying
under any of `%OneDrive%`, `%OneDriveConsumer%`, `%OneDriveCommercial%` detects with
that env path as evidence. (4) Dropbox gains an `info.json` discovery arm:
`%LOCALAPPDATA%\Dropbox\info.json` then `%APPDATA%\Dropbox\info.json`, bounded read
(64 KiB), tolerant parse of the documented shape (`personal`/`business` →
`path`), detects when the vault's canonical path lies under any listed root, with
the info.json path AND the matching root as evidence; unparseable or oversized
content = silently negative arm (SDR-3). (5) The verbatim-path defect is fixed at
the evidence boundary: every path pushed into `evidence_paths` is display-normalized
— `\\?\` and `\\?\UNC\` prefixes stripped (`\\?\C:\x` → `C:\x`,
`\\?\UNC\srv\share` → `\\srv\share`) — in ONE helper used by every arm that
formats a canonicalized path. (6) Fixtures: every new arm gets FakeFs tests
(positive, negative, env-unset, malformed info.json, oversized info.json) with
Windows-shaped paths where the arm is Windows-specific; the real-fs tests gain a
Windows assertion that no evidence path starts with `\\?\`; the lookalike census
gains entries for the new markers. Existing probe arms, report structs, and the
FFI surface are untouched.

**SD10 — Host label goldens (by designation, the TaskStatusPhrase pattern).** A
`SyncPhrase` static class carries every host-composed string, pinned by
`SyncPhraseTests` with a lock-step comment naming the mac twin
(`SyncDiagnosticsPanel.swift`); both platforms must change together. The inventory:
risk words "Low risk"/"Medium risk"/"High risk"; the count header
"Sync, {n} {system|systems} detected"; "Refresh" / "Refresh sync diagnostics";
"Loading sync diagnostics"; "Could not load sync diagnostics: {message}"; "Retry";
"Warning: {warning}" (prefix only); "Evidence"; "LiveSync configuration";
"Server host"/"Database"/"Live sync"/"Sync on save"/"Sync on start"/
"End-to-end encryption"; "Unknown"/"On"/"Off"; "LiveSync config could not be
read: {reason}" (prefix only); "LiveSync plugin present; no config found.". The
leaf title "Sync" is the registry entry (already shipped).

**SD11 — Close-out.** `chords.json` gains the `syncDiagnostics` group (VM + view +
refresh-command anchors; `SyncDiagnosticsPanelTests` + the journey), the command map
`"slate.diagnostics.refreshSync": "syncDiagnostics"`, and the issue map
`"#740": "syncDiagnostics"`. `generate-parity-matrix.py` gains `W4_8_STATUS`,
`W4_8_COMMANDS`, the `W4_DELIVERED_COMMANDS` fold, `LEAF_DELIVERED["syncDiagnostics"]`,
and `"#740"` in `expected_issues` (the chords/issue edits land together or generation
fails hard); the matrix is regenerated, never hand-edited. `w_c_matrix.md` gains the
W4-8 row directly after the History row. The FlaUI journey
`SyncDiagnostics_LeafReportAndRefresh_AreClean` (`[Trait("gate","W-C")]`) covers:
non-interactive fallback, rail/menu reach, the populated report over a fixture vault
with planted markers (a `.git` dir and a `.stfolder` are deterministic,
provider-real markers), per-row peered names, the Evidence expander, refresh Invoke
with settle, and `AssertAxeClean(process, "sync-diagnostics")`.

## Invariants

- **SDINV-1** No FFI shape change and no binding regeneration anywhere in W4-8.
- **SDINV-2** Detection degrades, never errors: probe-level failures produce absent
  arms; FFI-level failure produces the error state; nothing throws into the UI.
- **SDINV-3** Every publish passes the (shutdown, session, sequence) guard; a stale
  result can never overwrite a newer one, deterministically provable through the
  interleave seam.
- **SDINV-4** The leaf never announces unsolicited: the SD6 gate is the only sync
  announcement, and reveal/refresh/republish are otherwise silent.
- **SDINV-5** Watcher callbacks never run detection on a worker thread, never fire
  after Stop, and never outlive the vault session (teardown drains).
- **SDINV-6** Evidence paths render verbatim from core (post-SD9 normalization),
  each focusable; the host never edits them.
- **SDINV-7** Trigger set is closed: {vault open, explicit refresh, watcher fire} —
  nothing else re-runs detection (no reveal hook, no note-change coupling, no
  vault-event coupling).
- **SDINV-8** All sync work joins the workspace/lifecycle teardown drains; after
  Dispose no publish, announcement, or watcher callback lands.

## Divergences (recorded)

- **SDD-1** The watcher is three non-recursive `FileSystemWatcher` instances (vs
  mac's `DispatchSource` fds) with identical scope, debounce, ceiling, generation
  identity, and teardown semantics; OS-level event vocabulary differs
  (Created/Deleted/Renamed/Changed vs write/delete/rename) and is normalized to
  "entry churn" before the debouncer.
- **SDD-2** Windows renders the empty and unsupported states from
  `report.AudioSummary` (a relay) where mac duplicates both sentences in Swift with
  lock-step comments — byte-identical output, one fewer golden pair; recorded as a
  deliberate improvement, not drift.
- **SDD-3** Windows joins the panel-scheduler drain discipline
  (`PanelWorkScheduler` + the workspace drain) in place of mac's actor/task model —
  the SD5 guards are the shared contract.

## Risks / accepted

- **SDR-1** `FileSystemWatcher` buffer overflow (`InternalBufferOverflow`) is
  treated as a fire (re-detect), never an error surface.
- **SDR-2** Watchers on provider-virtualized roots (OneDrive files-on-demand,
  network shares) may under-report; the bounded scope minimizes exposure and the
  explicit refresh covers the gap; an arm failure at watcher start is non-fatal
  (recorded once in the log, detection still runs at open + manual refresh).
- **SDR-3** `info.json` is an undocumented-but-stable Dropbox convention; the arm
  is fail-silent on any parse/size anomaly and never contributes false positives
  (census-protected).
- **SDR-4** Env-var probes read the real process environment only in `RealFs`;
  every test goes through `FakeFs`'s env map (the census must stay hermetic).
- **SDR-5** The once-per-vault announce gate is an in-session set keyed by vault
  path (the compaction-relay pattern); a vault switch re-admits — mac-identical.
- **SDR-6** `LivesyncConfig()` cost rides the same off-dispatcher hop as
  `DetectSync()`; if either throws, the pair fails to the error state together
  (mac-identical single-task semantics).

# Mac backlog plan: open, unmilestoned issues

Status: adopted 2026-10-09; on ice until the self-hosted mac runner existed;
resumed 2026-10-10 at Wave 1 with plan 42 Phases 0 to 5 complete (the mac
lanes run on the Mac Studio, Namespace only as fallback). Owner decisions D1
to D5 are recorded in §5 and the §6 housekeeping is done. Source: every open
issue with no milestone (76 on 2026-10-08), read in full and cross-checked
against `main` at c247b865. No implementation PR has started.

## 1. Triage

### 1.1 In scope: 38 issues

| Group | Issues | Count |
|---|---|---|
| a11y-inspect findings + sync panel | #445 #446 #447 #1103 | 4 |
| Canvas register (W6-1 close-out) | #961 #1165 #1166 #1167 #1168 #1169 #1170 | 7 |
| Graph register (W6-2 close-out) | #1216 #1217 #1218 #1219 #1220 | 5 |
| Palette + search convergence | #1105 #1113 | 2 |
| Core-owned copy and outcomes on mac | #1053 #1286 #1122 #1140 #1147 | 5 |
| Cancellation discipline | #1287 #1324 | 2 |
| Graph authority decisions | #1189 #1190 | 2 |
| Legacy bugs (July) | #784 #785 #786 | 3 |
| AT structure (VoiceOver) | #1048 #1224 | 2 |
| Core infrastructure with a mac half | #940 #944 #1125 #1267 #1293 #1295 | 6 |

### 1.2 Excluded: 38 issues

Windows host (33): #1112 #1259 #1260 #1261 #1266 #1277 #1281 #1285 #1288
#1290 #1294 #1296 #1301 #1305 #1306 #1307 #1308 #1311 #1312 #1313 #1314
#1315 #1316 #1317 #1319 #1320 #1321 #1322 #1323 #1325 #1329 #1330 #1331.
(#1301 carries one mac note; see §1.3.)

Not mac, or no action (5):

- #480 quick-xml advisories: already resolved in practice. `cargo tree`
  shows quick-xml 0.41.0 through a pinned citationberg git rev and
  `.cargo/audit.toml` has no active ignores. What remains is the
  fixed-registry removal in `docs/runbooks/citation-dependency-policy.md`.
  Retitle to that, or close.
- #640 `slate query` CLI verb: its blocker (Milestone N) is closed, so it is
  unblocked. CLI work, not mac.
- #642 l10n umbrella: tracking only, by its own scope.
- #998 uniffi 0.32: still blocked. NordSecurity/uniffi-bindgen-cs's newest
  tag is `v0.11.0+v0.31.0`; `Cargo.toml` pins uniffi 0.31. Blocker 2
  touches the mac CI cache path but has documented workarounds. Nothing
  to do until upstream ships.
- #1051 accent/raised Lc 74.46: the Windows palette (`Slate.Light.xaml`,
  W8-2). Mac's tokens have no raised surface and gate their own pairs in
  `DesignTokens.swift` / `APCAContrast.swift`.

### 1.3 Corrections: where `main` differs from the issue text

- #447: the `MainSplitView` "Sidebar tab" Picker no longer exists (the U4
  utility rail replaced it) and `Workspace/RightPaneView.swift:364` already
  announces `.leafPanelShown`. The 4.1.3 finding is resolved by design and
  one of the two Picker findings is moot. One real finding remains
  (`FilesCitingSheet` count).
- #446: `AccessibleDataGrid` already takes `accessibilityLabel:`
  (`AccessibleDataGrid.swift:220`) but with the rename-specific default.
  The fix shrinks to: make it required, pass it from `BulkRenameSheet`.
- #784: mac has no unsaved untitled buffer. ⌘N creates `Untitled.md` on
  disk through `create_exclusive` with auto-suffixing
  (`AppState.swift:16495`, `uniqueUntitledName`). The issue's acceptance
  criteria (untitled N tabs, exit autosave, zero-padded collisions) do not
  fit the shipped model. Re-triaged 2026-10-09 (D3): quit is not safe.
  There is no autosave (the `NoteEditorView.swift:274` comment is
  aspirational), saving is explicit, `applicationShouldTerminate` returns
  `.terminateNow` when no sidebar work is pending, and the
  `willTerminateNotification` observer (`AppState.swift:4172`) saves only
  the workspace layout. Unsaved edits in open tabs are discarded on quit.
  Retitled to that residual, labelled tester-feedback, scheduled as PR 15b.
- #785: the rename path publishes `.rename` and reloads the tree
  (`AppState.swift:16807` `renameEntry`), and `FileTreeSidebarTests` already
  covers expansion remap through rename. No fact asserts the renamed row
  stays present and selected. Likely fixed; needs the pin.
- #786: confirmed by code. `.failed` renders a bare placeholder
  (`Bases/BaseContainerView.swift:218`), and
  `baseDocumentAvailabilityDisabledReason` returns the unavailable reason
  for `.failed` (`Bases/AppState+Bases.swift:157`), which disables Edit
  filters (`BaseContainerView.swift:122`). The trap is real.
- #1324: ten `cancel: CancelToken()` sites remain, in `AppState.swift`,
  `Graph/GraphDiagramView.swift`, `Bases/BaseDocument.swift`,
  `Bases/BaseEmbedDocument.swift`, `Bases/DashboardDocument.swift`,
  `Canvas/AppState+CanvasCreate.swift`. `FileTreeSidebar.swift` no longer
  has one; `GraphDiagramView.swift` was not in the issue's list.
- #1113 item 7: the `AppState` comment is now at `:9577`.
- #1170: the stale hint lives in `Canvas/CanvasOutlineView.swift:478`, not
  the container view.
- #1140 / #1147: mac has zero call sites of `createExclusiveReporting` and
  `canonicalPath`; core exports both (`crates/slate-uniffi/src/lib.rs:1454`,
  `:1195`).
- #1301 (Windows) records a mac divergence with no mac issue:
  `Sidebar/SidebarOrganization.swift` collapses duplicate shortcut entries
  at decode (`:1034`) and drops a converging entry on rename (`:800`),
  where Windows now treats one note in two slots as two items. Filed as
  #1332.

## 2. How the work is cut

- A chunk is one PR: two to five issues that share files and a contracts
  document, roughly 300 to 1500 changed lines, one mac-lane run. Small
  issues are batched because macOS runner minutes are the expensive pool
  and `swift-tests.yml` runs on every PR touching `apps/slate-mac/**`.
- Every chunk closes its register lines in `docs/plans/*_contracts.md`
  (CD-, C-D, SD-, PD-, AR-, TGC- entries) in the same PR, the way the
  Windows close-outs did.
- Verification lanes: `swift-tests.yml` (XCTest on the Mac Studio runner,
  about 2 minutes warm; Namespace Tahoe when the Studio is offline),
  `a11y-check.yml` (static score floor, same routing), `rust.yml` for core,
  `windows.yml` whenever an FFI signature changes, and a VoiceOver field
  pass through `scripts/vo.sh` with
  `docs/runbooks/voiceover-feature-test.md` for anything that changes what
  VoiceOver speaks or navigates.
- Sizes: S under 300 lines, one sitting. M 300 to 1500 lines, a day or two
  including review rounds. L over 1500 lines, or a design or spike first.
- Decisions are separated from implementation and listed in §5.

## 3. Chunks

### Wave 1: register close-outs (7 PRs, independent, 18 issues)

**PR 1. a11y-inspect close-out** (#446 #447 #445 #1103). Size S.
- `AddPropertySheet.swift:40` and `:58`: `.accessibilityHidden(true)` on
  the "Key" and "Type" caption texts.
- `AccessibleDataGrid.swift:220`: make the label required; pass
  "Property rename preview, data grid" from `BulkRenameSheet`.
- `BibliographyPanel.swift:362-391` (`FilesCitingSheet`): put the file
  count in a traversable summary Text and drop the container label that
  competes with `children: .contain`.
- `SyncDiagnosticsPanel.swift:180`:
  `.accessibilityLabel("Evidence, \(provider.displayName)")`, pinned
  lock-step with Windows' `SyncPhrase.EvidenceFor` (SD10 rule).
- Accessibility Inspector pass on the `BibliographyPanel` segmented Picker,
  then dismiss that finding; the `MainSplitView` findings are moot (§1.3).
- Tests: `AccessibleDataGridTests`, `BibliographyPanelTests`, a sync panel
  label fact, the a11y residue and corpus censuses.
- Docs: `27_sync_diagnostics_contracts.md` SDD-4 closed. Comment the
  dismissals on #446/#447 so the scanner's next run reconciles; #445
  updates itself.

**PR 2. Canvas: safe activation and spoken names** (#1169 #1170 #1168
#1165). Size S to M. Do #1169 first; it is the security item.
- #1169: gate the default-app open in `CanvasContainerView` on core's
  `media_class`; refuse non-media in-vault targets audibly with the arm
  Windows uses; Markdown keeps opening in-app. Fact: a `setup.exe` file
  card never reaches `externalOpener` (`AppState.swift:8395` injection).
- #1170: `CanvasOutlineView.swift:478` hint becomes "Opens the media file
  in its default app." (CD-36).
- #1168: `CanvasOutlineView.swift:222` builds `CanvasCardRef` from
  `speakable_name` (CD-30); the renderer already does
  (`CanvasRendererView.swift:438`). Confirm the outline row carries the
  field from core.
- #1165: register `canvasColorMarked` (`AppState+CanvasActions.swift:838`)
  in `SlateCommands`: id, palette row, menu item, hint. D-4 then lets
  Windows ship Color Marked; leave a note on the Windows side.
- Tests: `CanvasOutlineTests`, `CommandRegistryTests`,
  `CanvasScenarioTests` for the refusal.
- Docs: `34_canvas_contracts.md` CD-30/36/38 and the "Mac details" register.

**PR 3. Canvas: state after mutation and paint order** (#961 #1166 #1167).
Size M.
- #961: `CanvasRendererView.swift:295` `rebuildVisible` stamps
  `zPosition = documentIndex` on every (re)materialized card layer. Fact:
  materialize, pan away, pan back; the overlap paints
  last-in-document-order on top.
- #1166: `CanvasDocument.swift:683` `reloadAfterMutation` reconciles
  `selection.selected` against the refreshed outline and drops an
  unresolvable selection. Fact: undo a create, then Duplicate refuses;
  never "Duplicated 0 cards" and no empty undo entry.
- #1167: `AppState+CanvasNavigation.swift:179` `canvasSelectAdjacent`
  routes through the read-state mapping so loading, degraded, failed,
  retarget-failed and empty canvases speak the corpus arm (CD-46).
- Tests: `CanvasRendererTests`, `CanvasNavigatorTests`,
  `CanvasAnnouncerTests`.
- Docs: `34_canvas_contracts.md` CD-46 and the register.

**PR 4. Graph menu parity** (#1219 #1216). Size S.
- #1219: Graph menu items Orphans, Unresolved, Most Linked beside "Fit
  Graph" (`SlateMacApp.swift:722`), bound to the existing ids
  (`SlateCommands.swift:245-247`).
- #1216: Graph, then Verbosity submenu plus palette ids, setting
  `graphAnnouncer.verbosity`; persistence already lands through
  `AppState+GraphConfig.swift:69`.
- Tests: `CommandRegistryTests`, the menu census, a config round-trip.
- Docs: `35_graph_contracts.md` §0a and §C register; `w6_2_graph_spec.md`
  D-6 recorded delivered.

**PR 5. Graph behaviours** (#1217 #1218 #1220). Size M.
- #1217: `Graph/ConnectionsPanel.swift:30` loads on activation changes
  while mounted (`onChange` of `connectionsLeafActiveForView` and
  `selectedFilePath`) and renders its loading text from
  `connectionsLoading` (`AppState.swift:2968`).
- #1218: `Graph/AppState+GraphConfig.swift:63-66` persists the user's own
  flags, held apart from a preset's transient backend filter (Windows rule
  W / Term W7). Fact: a needle typed under a preset leaves the flags in
  `.slate/graph.json` unchanged.
- #1220: `Graph/GraphTableView.swift:87` seeds a restored Diagram silently
  (Term M1 / DD-18); delete the unused `labelFadeZoom`
  (`GraphDiagramView.swift:174`).
- Tests: `ConnectionsPanelTests`, graph config tests, table view tests.
- Docs: `35_graph_contracts.md` B-D11, C-D7, §D register.

**PR 6. Command palette: Home, End, Page Up, Page Down** (#1105). Size S
to M.
- `CommandPaletteModel`: `selectFirst`, `selectLast`, `movePage(delta)`
  with page size 10, the same no-selection start rule and cross-section
  cycle as Windows.
- `CommandPaletteView.swift:492` / `:513`: handle the four keys
  bare-modifier only; remove only `.function` from the passthrough set;
  keep the IME marked-text guard and the Control+Option+Down Quick Nav
  regression (`CommandPaletteViewTests.swift:274-285`).
- Update `CommandPaletteViewTests.swift:236-252`, which pins the current
  passthrough.
- Docs: `28_palette_contracts.md` PD-1 retired.

**PR 7. Search overlay: arrows, Esc focus restore, truthful comments**
(#1113). Size M.
- Up/Down move the result selection, Return opens it (SD-1).
- Capture the first responder at open and restore it on Esc (SD-2),
  reusing the palette's restore logic.
- D2 decided: implement ⌘Return (§5 has the VoiceOver check). The
  `SearchOverlay.swift:571` guard admits ⌘ for Return only, and only while a
  result or recent row is focused, which makes the `AppState.swift:9577`
  comment true.
- Fix the stale comments at `SearchOverlay.swift:8`, `:11`, `:29`, `:511`.
- Tests: overlay facts for arrows, restore and ⌘Return; update the runbook
  step at `voiceover-feature-test.md:141` ("three Tabs to the first row").
- Docs: `29_search_overlay_contracts.md` SD-1/SD-2 retired.

### Wave 2: core ownership on mac (5 PRs, 7 issues)

**PR 8. Core-rendered copy on mac** (#1053 #1286 #1122). Size M. Low risk:
every string is byte-identical today.
- #1053: `CodeBlockView.swift:97` `preambleLabel` calls
  `codeBlockPreamble(language:source:)` (`lib.rs:7312`);
  `CodeBlockViewTests` stay as consumers of the canonical string.
- #1286: `EmbedView.swift:73`, `:94`, `:114`, `:169` map each resolution to
  `ResolvedEmbed` and call `resolvedEmbedTitle` (`lib.rs:10955`); delete
  `imageEmbedTitle`; `BaseEmbedDocument`'s base-card title too (AR-23).
  Add a provenance census (no host literal in a resolved-embed label) and
  an overlong heading / alt witness proving VoiceOver reads core's bounded
  text.
- #1122: the template picker-open and created sites post
  `TemplatePickerOpened` / `TemplateNoteCreated`
  (`AppState.swift:23929-23939` `announceTemplate`); drop the W0.5-3
  residue markers there. Availability and busy reasons stay host-composed.
- Tests: the existing exact-wording tests prove faithfulness;
  `A11yResidueCensusTests` shrinks.
- Docs: `40_nvda_matrix_remediation_contracts.md` AR-23 closed;
  `30_templates_contracts.md` TR-5 mac half; the
  `07_portability_review.md:114` claim becomes true.

**PR 9. create_exclusive_reporting on mac** (#1140). Size M.
- `performCreateNoteFromTemplate`, `createNote`
  (`AppState.swift:16556`) and Restore As call
  `createExclusiveReporting` (`lib.rs:1454`). The `PublishedUnindexed` arm
  finishes as a create: the ordinary created sentence plus the caveat,
  open the note, never retry under another name.
- Facts driven by `SLATE_TEST_FAULT_AFTER_WRITE` (path substring), the
  Windows shape.
- Docs: `30_templates_contracts.md` TR-5.

**PR 10. Canonical identity I6 / I8 on mac** (#1147). Size M to L. After
PR 9.
- I6: after every Created or Renamed publication, re-seat missing tabs via
  `canonicalPath` (`lib.rs:1195`): retarget, and reload only clean tabs.
- I8: a hashless save goes through `createExclusiveReporting`;
  `DestinationExists` is the conflict presentation.
- Mirror the `CanonicalIdentityTests` pins (volume-probed on APFS; vacuous
  on a case-sensitive volume).
- Docs: `32_canonical_file_identity_contracts.md` I6/I8 mac column.

**PR 11. resolve_embed takes a CancelToken** (#1287). Size M. Core, FFI
and mac; both native lanes.
- `lib.rs:2051` `resolve_embed` gains `cancel: Arc<CancelToken>`; bindings
  regenerate on both hosts (Windows has no callers but its lane must stay
  green; bump `bindings_contract_version` if policy requires).
- `AppState.swift:12068` (`loadCurrentNoteEmbedResolutions` batch) and
  `:12145` (`requestReadingEmbedResolution`) hold one token per batch or
  request, cancelled through `withTaskCancellationHandler` and on
  selection change; `.cancelled` is a dropped result, not an unresolved
  row.
- Docs: AR-24 closed.

**PR 12. One token per operation** (#1324). Size M. After PR 11.
- The ten remaining `cancel: CancelToken()` sites get one token tied to
  the task or the owner (document, view, sheet). `canvasMediaPaths`
  (`AppState+CanvasCreate.swift`) and the `openableDocuments` loop
  (`AppState.swift` ~11506) move off the main actor first (locked 05
  §4.1).
- A source census over every mac source with a reviewed allowlist (the
  `BaseQueryBuilderTests` pattern).

### Wave 3: the graph decision and the July bugs (4 PRs, 5 issues)

**PR 13. Graph authority model** (#1189 #1190). Size M. D1 decided:
converge.
- Adopt the Windows shape. A rows request stages its
  candidate snapshot and installs it atomically with its rows; a rows-only
  result with no held snapshot refuses and re-fetches as a pair. Rewrite
  the frozen facts (`GraphTableViewTests.swift:144-235`, T1's
  needle-during-pair fact) with the owner's record in
  `35_graph_contracts.md` TGC-13 / TGC-17, so `receiveGraphTableRows`
  (`AppState+GraphTable.swift:326`) and the rows failure arm (`:465-480`)
  can no longer leave old rows under a new authority.
- Rejected alternative (D1): ratify the divergence as an accepted mac
  risk.

**PR 14. Rename keeps its row** (#785). Size S.
- Add the fact: rename from the tree, then the row is present under its
  new name, selected, with no stale old-path row and the filter respected.
  Close, or retitle to `mac:` if any criterion fails.

**PR 15. A failed Base stays editable** (#786). Size M.
- `Bases/BaseContainerView.swift:218`: render an actionable error (what
  failed, that editing repairs it) instead of a bare placeholder.
- `Bases/AppState+Bases.swift:157`: `.failed` with the file present must
  not disable definition editing; only a missing or unwritable file does.
- Tests: a saved empty `.base`, an incomplete definition, an unloadable
  selected view, edit from the error state, no blank surface.

**PR 15b. Quit with unsaved edits saves or asks** (#784, retitled). Size S
to M.
- `SlateMacApp.swift:30` `applicationShouldTerminate` returns
  `.terminateLater` while any open tab has unsaved changes and routes
  through the Close Vault save-all / discard flow
  (`AppState.swift:10928` `resolveVaultCloseSaveAll`), so quit speaks and
  behaves like Close Vault; `.terminateNow` only when nothing is dirty and
  the sidebar fence is clear.
- Facts: quit with a dirty tab saves it or presents the sheet and never
  discards silently; quit with clean tabs terminates immediately; the
  sidebar fence still settles.
- The 2026-10-09 finding is by code reading; the PR's first step confirms
  the loss at runtime on current `main`.

### Wave 4: AT structure (2 PRs, 2 issues)

**PR 16. Reading view native list semantics** (#1048, G20). Size L.
- Spike first (half a day): which AX shape SwiftUI and AppKit honour for a
  list role with nested items (an `NSAccessibilityList` representation or
  a custom `NSAccessibilityElement` container). Then, per D5, research how
  other macOS apps and frameworks expose nested lists to VoiceOver before
  choosing the shape. Then implement at
  `Reading/ReadingView.swift:610` and `:639`, keeping the authored
  `list_marker` ordinals and task-row checkbox semantics; the value string
  may remain as a supplement.
- VoiceOver field pass (`scripts/vo.sh`, the runbook); re-run the §W-A
  goldens; update `w_c_matrix.md`.

**PR 17. Editor span semantics for VoiceOver** (#1224, G29). Size M to L.
- `NoteEditorView.swift:980` `applyHighlight` adds `.link` on wikilink,
  link and embed ranges; heading level through the accessibility
  attributed-string attributes where AppKit honours them, otherwise record
  the platform limit. Keep issue #63's no-override rule.
- Tests over the attributed string per span kind; the a11y-check score
  must hold; VoiceOver field pass (link rotor).
- Docs: `gap_analysis.md` G29 closed.

### Wave 5: core infrastructure with a mac half (7 PRs including the D4 matrix doc, 6 issues)

**PR 18. A save resolves only its own links** (#1295). Size S to M. Core
only; any time. About 100 lines of core and 150 of tests per the issue.

**PR 19. Mac rescan baseline** (#1333, split from #1267). Size M.
- Files sidebar Refresh command and the foreground rescan on window
  activation, consuming core's rescan and delta ledger from W7-7 PR 7
  (R-9); speak `VaultRescanFinished` / `VaultRescanIncomplete` only when
  something changed. Mac has no rescan trigger today.

**PR 20. notify watcher in core** (#1267). Size L. After PR 19.
- Debounced, recursive, `.slate` and hidden paths filtered; feeds the same
  rescan path; Refresh and foreground stay the fallback; coalesce a sync
  client's burst into one announcement.

**PR 21. Change feed** (#1293). Size L. After PR 20.

**PR 22. Descriptor-relative session interior** (#940). Size L. Core plus
the mac host: `/dev/fd/<rootfd>`-relative config, lock and SQLite paths;
audit FSEvents, path display and canonicalization; the deterministic
A, B, A regression. Sequence after PR 19/20 because both touch root
identity.

**PR 23. Durable sidebar write journal** (#944). Size M to L.
- Typed intent enum for the organization commands and
  `SidebarStructuralTransform`; an identity-keyed write-ahead journal under
  Application Support written before acknowledgement and before the
  filesystem operation commits; replay on open through the existing
  chained drain; non-lossy overflow; the relaunch regression. Touches the
  `AppState.swift:5136` termination fence.

**PR 24. #1125 Track B (and Track A): the capability matrix and
feasibility doc.** Doc S; implementation sized after the policy decision.
D4: test more and build the matrix before any policy call.
- A capability matrix for macOS and Linux (rename APIs, open-descriptor
  identity checks on APFS, case-only renames, directories, provider
  combinations) and the Trash-handoff feasibility. Then decide: implement
  a conditional inverse, refuse those inverses, or document the limit. No
  implementation PR before the decision.

## 4. Delivery order

1. Wave 1, PRs 1 to 7, in parallel; they touch disjoint files. To save
   mac-runner minutes, PR 2 + PR 3 and PR 4 + PR 5 can each be one PR.
   D1, D2 and D3 are decided (§5), so nothing in Waves 1 to 3 waits.
   Checkpoint A: 18 issues closed.
2. Wave 2: PR 8; PR 9 then PR 10; PR 11 then PR 12. Checkpoint B: 25.
3. Wave 3: PR 13, PR 14, PR 15, PR 15b. Checkpoint C: 30.
4. Wave 4: PR 16 and PR 17, each with a VoiceOver session before merge.
   Checkpoint D: 32.
5. Wave 5: PR 18 any time; PR 19 then PR 20 then PR 21; PR 22 after PR 20;
   PR 23 independent; PR 24 (the D4 matrix) in parallel. Checkpoint E: 38.

## 5. Owner decisions (recorded 2026-10-09)

- D1 (#1189 #1190): **converge** to the Windows authority model. PR 13
  rewrites the frozen facts with this record in `35_graph_contracts.md`.
- D2 (#1113 item 7): **implement ⌘Return**, checked first against
  VoiceOver. No VoiceOver command is a plain ⌘Return: every VoiceOver
  command carries the VO modifier (Control-Option or Caps Lock); the
  nearest are VO-Return (select a list item) and VO-Command-Return (start
  multiple selection). Quick Nav uses unmodified arrows and the Keyboard
  Commander is off by default. Inside Slate the only ⌘Return binding is
  the properties-source Apply button (`NotePropertiesHeader.swift:489`,
  window-scoped), so the overlay's monitor claims ⌘Return only while a
  result or recent row is focused and passes it through otherwise.
- D3 (#784): **pre-authorized**: close if quit is safe, retitle if not. The
  check found quit unsafe (§1.3), so #784 is retitled to the quit residual,
  labelled tester-feedback, and scheduled as PR 15b.
- D4 (#1125 Track B): **test more and build the capability matrix first**;
  the policy choice waits for that doc (PR 24).
- D5 (#1048): **complete the spike, then research how other macOS apps
  expose nested lists to VoiceOver** before choosing the container shape;
  PR 16 carries both steps ahead of its implementation.

## 6. Housekeeping (done 2026-10-09)

- #1332 filed: SidebarOrganization collapses duplicate shortcut slots and
  drops a converging rename, from #1301's note.
- #1333 filed: the mac Refresh and foreground rescan baseline, split from
  #1267; PR 19.
- #480 retitled to the registry migration with the on-main state recorded;
  it stays blocked on an upstream registry release.
- #640 unblocked (Milestone N closed): label removed, title updated.
- Finding dispositions posted on #446 and #447 for the scanner's next run.
- #784 retitled to the quit residual and labelled tester-feedback (D3).

## 7. Risks

- Mac lane cost: batch, and run only the lanes a change touches.
- PR 11 changes an FFI signature: both native lanes, regenerated bindings,
  never a 0.31 dylib with mismatched bindings.
- D1 rewrites frozen facts; record the owner's call in the contracts doc
  before the code changes.
- PR 16 and PR 17 need a human VoiceOver pass; the XCTest facts prove the
  attributes, not what VoiceOver speaks.
- PR 19, PR 20 and PR 22 all touch root identity; keep them sequential.

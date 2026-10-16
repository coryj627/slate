# PR 1 — a11y-inspect close-out and the sync panel's Evidence label

Issues: #446, #447, #445 (updates itself), #1103. Size S. Lanes: swift,
a11y, windows. Depends on nothing. Parallel with PRs 2 to 7.

## Goal

Close the four real findings the scanner and the Windows axe run left on
mac, dismiss the rest with recorded reasons, and bring the sync panel's
Evidence disclosure back to the lock-step golden it shares with Windows.

## Current state on main

- `apps/slate-mac/Sources/SlateMac/AddPropertySheet.swift:40`
  (`Text("Key")`) and `:58` (`Text("Type")`): caption texts beside a
  `TextField` (`.accessibilityLabel("Property key")`, `:44`) and a
  `Picker` (`.accessibilityLabel("Property type")`, `:67`). VoiceOver
  reads each caption a second time as a standalone element (#446,
  1.3.1, medium).
- `AccessibleDataGrid.swift:220`: `accessibilityLabel: String =
  "Property rename preview, data grid"` is an init default on a generic
  grid. Seven call sites pass their own label (`ReadingView.swift:774`,
  `GraphTableView.swift:339`, `BaseContainerView.swift:250`,
  `DashboardViews.swift:426`, `BaseEmbedView.swift:264`,
  `BaseQueryBuilderSheet.swift:610`, `CanvasTableView.swift:64`); one
  relies on the default: `BulkRenameSheet.swift:107`
  (`AccessibleDataGrid<RenameGridRow>(columns:rows:summary:)`) (#446,
  1.3.1, low).
- `BibliographyPanel.swift:356-394` (`FilesCitingSheet`): the outer
  `VStack` carries `.accessibilityElement(children: .contain)` (`:389`)
  and a container `.accessibilityLabel("Files citing this entry.
  \(CountCopy.counted(paths.count, "file", "files")).")` (`:390-392`).
  With `.contain`, the container label competes with child traversal, so
  the count is heard twice or not at all depending on navigation mode
  (#447, 4.1.2, medium).
- `BibliographyPanel.swift:70`: the segmented `Picker("", …)` already
  carries `.accessibilityLabel("Bibliography view")` and an
  `.accessibilityValue`. The scanner's own text says its rule fires
  generically (#447, low): verify, then dismiss.
- `MainSplitView` findings in #447: moot. The "Sidebar tab" Picker no
  longer exists (the U4 utility rail replaced it) and
  `Workspace/RightPaneView.swift:364` posts `.leafPanelShown(title:)`,
  the 4.1.3 status message the finding asked for.
- `SyncDiagnosticsPanel.swift:180`: `DisclosureGroup("Evidence")` with
  no accessibility label; two detected providers expose identical
  focusable "Evidence" siblings (#1103).
- Dismissed by the owner on 2026-10-09 (comments on #446 and #447): the
  three `button-group-missing-container-label` hits
  (`AddPropertySheet`, `BulkRenameSheet`, `CitationSummarySheet`): the
  buttons are self-labelling and no visible group label exists.

## Windows reference

- `apps/slate-windows/src/SlateWindows/Panels/SyncPhrase.cs:77`:
  `EvidenceFor(displayName) => Evidence + ", " + displayName`, applied at
  `SyncDiagnosticsSurfaceView.cs:441`. The accessible name is
  "Evidence, {DisplayName}"; the visible header stays "Evidence" (a
  contiguous prefix, WCAG 2.5.3). Contract 27 SDD-4 records it as a
  Windows-only golden until mac lands this.
- `Grids/AccessibleDataGrid.cs:790`: `string accessibilityLabel` is a
  required parameter of `Bind`, applied through
  `AutomationProperties.SetName(_grid, accessibilityLabel)` (`:904`).

## Design

1. Captions that duplicate a control's own label are hidden from AT:
   `.accessibilityHidden(true)` on the "Key" and "Type" captions. The
   visible layout is unchanged.
2. The grid's container label is required. Delete the default at
   `AccessibleDataGrid.swift:220`; pass
   `accessibilityLabel: "Property rename preview, data grid"` at
   `BulkRenameSheet.swift:107`. The other seven sites already comply.
3. `FilesCitingSheet` carries its count as a traversable child: a summary
   `Text` under the header reading "{N} file(s) citing this entry."
   (built from `CountCopy.counted` so the plural stays shared), and the
   container keeps `.contain` with no label of its own. The empty arm
   (`:366-371`) is already a traversable `Text`; leave it.
4. The Evidence disclosure's accessible name is
   `"Evidence, \(provider.displayName)"` via `.accessibilityLabel` on the
   `DisclosureGroup`'s label view (keep the visible text "Evidence").
   Pin the string lock-step with Windows: a Swift test that asserts the
   exact format, with a comment naming `SyncPhrase.EvidenceFor` as the
   twin (the SD10 pattern), and a matching comment on the Windows side is
   not required by this PR (the C# test already names the mac file).
5. The Bibliography segmented Picker: one Accessibility Inspector pass
   (label spoken as "Bibliography view", value as the segment). Record
   the result in the PR and dismiss the finding.

## Changes

- `AddPropertySheet.swift`: two `.accessibilityHidden(true)` modifiers.
- `AccessibleDataGrid.swift:220`: remove the default value.
- `BulkRenameSheet.swift:107-116`: pass the label.
- `BibliographyPanel.swift:356-394`: summary `Text`; drop the container
  label; keep `.accessibilityElement(children: .contain)`.
- `SyncDiagnosticsPanel.swift:180`: the composed accessible name.
- Issue comments on #446 and #447 for the scanner's next run: which
  findings were fixed (file and line), which dismissed and why; #445
  reconciles itself on the next scan.

## Tests

- `AccessibleDataGridTests`: construction without a label does not
  compile (a comment in the test, not a fact) and the rename grid's label
  is "Property rename preview, data grid".
- `BibliographyPanelTests`: `FilesCitingSheet` with three paths exposes a
  summary text "3 files citing this entry." and no container label; with
  one path "1 file …"; the empty arm unchanged.
- `SyncDiagnosticsPanelTests`: new fact
  `testEvidenceDisclosureIsNamedForItsProvider` asserting
  "Evidence, Dropbox" for a provider named Dropbox, with the lock-step
  comment.
- `A11yResidueCensusTests` and `A11yCorpusCensusTests` unchanged (no new
  host-composed sentence: the Evidence name is a label, not an
  announcement).
- `a11y-check` score stays 100.

## Accessibility

VoiceOver on the Add Property sheet reads the field and the picker once
each; on the Files Citing sheet it reaches the count as its own line; on
the sync panel each disclosure names its provider. No VoiceOver field
pass is required (labels only), but the Accessibility Inspector pass for
the Picker is.

## Docs and registers

- `docs/plans/27_sync_diagnostics_contracts.md` SDD-4 (`:212`): closed;
  note that `SyncPhrase.EvidenceFor` returns to the SD10 lock-step rule.
- No canvas, graph or palette rows.

## Acceptance

- [ ] The four fixes above are in, with the facts named.
- [ ] #446 and #447 carry the disposition comment (fixed / dismissed with
      reason, per finding).
- [ ] The Inspector pass on the Bibliography Picker is recorded in the PR.
- [ ] `a11y-check` 100; swift-tests green.

## Risks

Making the grid label required is a signature change; every call site
already passes one except the rename sheet, so the compiler is the
census.

## Brief

Fix the four real a11y findings on mac and record the dismissals. Read
the files under "Current state on main" first. (1) Hide the "Key" and
"Type" captions in `AddPropertySheet.swift` from AT. (2) Make
`AccessibleDataGrid`'s `accessibilityLabel` required and pass
"Property rename preview, data grid" from `BulkRenameSheet.swift:107`.
(3) In `FilesCitingSheet`, move the file count into a traversable summary
`Text` and remove the container `.accessibilityLabel` that competes with
`children: .contain`. (4) Name the sync panel's Evidence disclosure
"Evidence, {provider.displayName}", visible text unchanged, pinned by a
test that names `SyncPhrase.EvidenceFor` as its twin. Verify the
Bibliography segmented Picker with Accessibility Inspector and record the
result. Add the facts listed under "Tests". Close SDD-4 in contract 27.
Comment #446 and #447 with the per-finding disposition. Do not touch the
`MainSplitView` findings (moot) or the three dismissed button-group
findings. Done when swift-tests and a11y-check are green and the PR body
lists the facts, the Inspector result and the register row closed.

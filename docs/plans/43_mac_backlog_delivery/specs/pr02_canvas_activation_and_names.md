# PR 2 — Canvas: safe activation, truthful hint, speakable names, Color Marked registered

Issues: #1169 (security, first), #1170, #1168, #1165. Size S to M. Lanes:
swift, a11y, windows. Depends on nothing. Parallel with PRs 1, 3, 5, 6,
7; shares `SlateMacApp.swift` and `SlateCommands.swift` with PR 4 (land
this first).

## Goal

A canvas is untrusted input. Mac must refuse to hand a non-media file
card to the default app, say so with the corpus sentence Windows speaks,
tell the truth in the media card's hint, spell the outline row's name the
way the renderer and Windows do, and register `canvasColorMarked` so the
command exists on both hosts.

## Current state on main

- `Canvas/CanvasContainerView.swift:160-200` `activate(nodeId:kind:
  title:)`. The `"file", "image"` arm opens Markdown in-app (`:167-176`),
  and otherwise hands ANY existing in-vault target to
  `appState.externalOpener` (`:177-183`), announcing
  `.canvasOpened(title:target: .defaultApp)`; when the opener declines
  it speaks `.canvasFileNotFound` (`:184-186`), which is the wrong answer
  for a file that exists. `externalOpener` is injectable
  (`AppState.swift:8319`, default `NSWorkspace.shared.open`, `:8395`).
- Core exports the media classification: `canvas_media_class(path) ->
  Option<CanvasMediaClass>` (`crates/slate-uniffi/src/lib.rs:12993`,
  core `canvas/model.rs:682`); the Swift binding is
  `canvasMediaClass(path:)` (`slate_uniffi.swift:36007`); the refusal
  reason `CanvasBlockedReason.fileTypeNotOpenable(target:)` exists
  (`:23058`). Mac uses neither. The corpus already renders the refusal:
  "setup.exe is not a note or media file, so it was not opened from the
  canvas." (`tests/fixtures/a11y/corpus.json:2258`, `:2263`, priority
  high), and the mac corpus census lists both rows
  (`A11yCorpusCensusTests.swift:515-516`) under the Windows-only
  designation contract 34 records at `:3637` ("consumed on mac when #1169
  adopts the gate (E13D-2)").
- `Canvas/CanvasOutlineView.swift:478`: the image hint reads "Media cards
  open with canvas actions, arriving in a later milestone slice." while
  activation opens media today (#1170).
- `Canvas/CanvasOutlineView.swift:222`: the row label is
  `CanvasCardRef(kind: row.kind, title: row.title).phrase`; the outline
  row carries `speakableName` from core (`CanvasOutlineRow` has
  `title` and `speakableName`), and the renderer already spells its peer
  names from `node.speakableName` (`CanvasRendererView.swift:438`)
  (#1168).
- `Canvas/AppState+CanvasActions.swift:838` `canvasColorMarked(preset:)`
  is implemented and tested, with no command id, palette row or menu item
  (#1165). Registry entries follow the shape at `SlateCommands.swift:1538`
  (`register(SlateCommandID.x, label:, section:, hint:) { … }`).

## Windows reference

`apps/slate-windows/src/SlateWindows/Canvas/CanvasDocumentViewModel.cs:
2110-2160` `ActivateFileCard`:
1. target empty or not in the vault → `CanvasFileNotFound`, refused;
2. not Markdown and not openable media → `CanvasBlocked(FileTypeNotOpenable
   (target))`, refused, never launched;
3. media → the surface opener; success → `CanvasOpened(title,
   DefaultApp)`;
4. media whose open fails → `CanvasActionFailed(CanvasAction, target)`,
   never file-not-found (the file exists; telling the user it is missing
   is a wrong answer).
The media hint on Windows is "Opens the media file in its default app."
(`:5472`). Register rows: CD-38 (the shell-execution gate), CD-36 (the
hint), CD-30 (`speakable_name` in the outline), owner decision D-4
(`canvasColorMarked`: Windows ships Color Marked only if mac registers
it; parity is the registry).

## Design

1. **Gate (#1169).** In `activate`, after the Markdown arm: if the target
   is missing → `.canvasFileNotFound` (unchanged). Else if
   `canvasMediaClass(path: target) == nil` → announce
   `.canvasBlocked(reason: .fileTypeNotOpenable(target: target))` and do
   NOT call `externalOpener`. Else call the opener; success →
   `.canvasOpened(…, .defaultApp)`; failure → the generic failed-action
   arm (`CanvasActionFailed { action: CanvasAction, detail: target }`;
   confirm the Swift case names in the generated enum), never
   `.canvasFileNotFound`. Keep the `"link"` arm as is.
2. **Hint (#1170).** `CanvasOutlineView.swift:478` → "Opens the media
   file in its default app." (byte-identical to Windows).
3. **Names (#1168).** `CanvasOutlineView.swift:222` builds the label from
   `row.speakableName` (`CanvasCardRef(kind: row.kind, title:
   row.speakableName)`). Two cards titled "Research" become "Research"
   and "Research 2" (or whatever 0b-5's algorithm returns), the same
   spelling the renderer already uses, so Voice Control "click Research"
   is unambiguous on every surface.
4. **Registry (#1165).** A command id in the canvas section (follow the
   neighbouring canvas ids' naming in `SlateCommands.swift`), label
   "Canvas: Color Marked Cards…" (match the existing per-card Color verb's
   label style), a hint, a palette row, and a menu item in the same menu
   as the other canvas verbs. The closure routes to the same flow the
   per-card Color verb uses, applied to the marked set
   (`canvasColorMarked(preset:)`; read it and the per-card verb's
   registration to decide whether the command opens the palette sheet or
   applies the current preset). Availability mirrors the marked-set verbs
   (disabled with a reason when nothing is marked, never silent).

## Changes

- `Canvas/CanvasContainerView.swift:177-186`: the gate and the two
  outcome arms.
- `Canvas/CanvasOutlineView.swift:222`, `:478`.
- `SlateCommands.swift`: the id and the `register` call;
  `SlateMacApp.swift`: the menu item beside the other canvas verbs.
- `Tests/SlateMacTests/A11yCorpusCensusTests.swift:515-516`: the two rows
  move from designated to consumed (whatever shape the census uses to
  record a mac trigger site).

## Tests

- `CanvasScenarioTests.testASetupExeFileCardIsRefusedAndNeverReachesTheOpener`:
  a canvas with `{"type":"file","file":"setup.exe"}` and the file present
  in the vault; inject `externalOpener` that fails the test if called;
  activation speaks the corpus sentence for `setup.exe`.
- `CanvasScenarioTests.testAMediaCardWhoseOpenFailsSpeaksAFailedActionNotFileNotFound`.
- `CanvasScenarioTests.testAMarkdownCardStillOpensInTheTab` (regression).
- `CanvasOutlineTests`: the image hint equals "Opens the media file in
  its default app."; two cards titled alike carry distinct labels built
  from `speakableName`.
- `CommandRegistryTests`: the new id is registered in the canvas section
  with a hint; the menu census (if `SlateCommandsTests` or
  `MenusObservabilityTests` pins menu ids) includes it.
- `A11yCorpusCensusTests`: the two `fileTypeNotOpenable` rows are
  consumed by a mac site.

## Accessibility

The refusal is spoken at high priority through the canvas announcer
(the corpus arm). The hint change alters what VoiceOver reads on media
rows; a short VoiceOver check on one media card (hint spoken, activation
opens it) goes in the PR description.

## Docs and registers

`docs/plans/34_canvas_contracts.md`: the issue-reconciliation rows for
CD-30 (`:19367`), CD-36 (`:19373`), CD-38 (`:19375`) marked closed on mac;
the `:3637` designation line for `CanvasBlocked/FileTypeNotOpenable`
updated to name the mac site; the "Mac details recorded while reading"
item "`canvasColorMarked` is unregistered" (`:15636`) marked resolved;
`09_canvas/at_smoke_checklist.md` gains the refusal step if it lists
activation. Leave a note on the Windows side's D-4 record that mac has
registered the command (Windows may now ship Color Marked).

## Acceptance

- [ ] A non-media in-vault file card never reaches `externalOpener`; the
      corpus sentence is spoken.
- [ ] Media opens as before; an open failure speaks the failed-action arm.
- [ ] The hint, the outline names and the registry entry are in with the
      facts above.
- [ ] Registers updated; swift-tests, a11y-check and windows green.

## Risks

The gate changes behaviour for non-media files users may have opened
from a canvas before (PDFs are media? `canvas_media_class` decides; read
`canvas/model.rs:682` and say in the PR what is and is not openable).
That is the Windows behaviour and the owner's recorded direction.

## Brief

Make the mac canvas's file-card activation as safe as Windows', and fix
the three register items that sit in the same files. Read
`Canvas/CanvasContainerView.swift:160-200`, the Windows
`ActivateFileCard` at `CanvasDocumentViewModel.cs:2110-2160`, and the
core media classification. Gate the default-app open on
`canvasMediaClass(path:)`; refuse non-media audibly with
`.canvasBlocked(reason: .fileTypeNotOpenable(target:))`; make a failed
media open speak the failed-action arm, not file-not-found. Change the
media hint at `CanvasOutlineView.swift:478` to "Opens the media file in
its default app." Build the outline row label from `speakableName`
(`:222`). Register `canvasColorMarked` (id, palette row, menu item,
hint). Write the facts under "Tests" first (the `setup.exe` fact injects
an opener that must never be called). Flip the corpus census designation
for the two `fileTypeNotOpenable` rows. Close CD-30, CD-36, CD-38 and the
`canvasColorMarked` register item in contract 34. Do not change the link
arm, the vocabulary or core. Done when the facts pass, the three lanes
are green, and the PR body says what `canvas_media_class` admits.

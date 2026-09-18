# W2 EDITOR — manual AT checklist (#750)

Source: [wave spec](../specs/w2_spec.md) acceptance lines and the
[conformance matrix](../w_c_matrix.md) keyboard routes. Run on a disposable
fixture vault. Use [_at_pass_template.md](_at_pass_template.md) for a named,
dated record and transcript. NVDA and JAWS are independent passes with stock
settings; Narrator is W8-6 smoke scope. Automated twins do not prove speech.

**Tester:** Pending · **AT:** Pending (exact version per run) · **OS:** Pending (edition and build)
**Build:** Pending (branch and verified commit) · **Corpus:** Pending (fixture vault and notes)
**Method:** Pending (Speech Viewer / JAWS history, braille viewer where required)
**Run date:** Pending · **Evidence reference:** Pending

| # | Spec item | Check | How (the UIA route) | Observable outcome | Automated twin | Narrator | NVDA | JAWS |
|---|---|---|---|---|---|---|---|---|
| 1 | W2-1 | Document, selection and undo | Open a fixture note; move by character, word and line; select text; type, undo and redo; Ctrl+S. | Document name, text, selection and caret remain synchronized; save feedback is heard. | `AvalonDocumentBufferCensus` | Pending | Pending | Pending |
| 2 | W2-1 | IME composition | Using an installed IME, compose and commit text; cancel another composition; undo the committed edit. | Composition does not duplicate committed text or lose caret tracking. | `AvalonDocumentBufferCensus` | Pending | Pending | Pending |
| 3 | W2-2 | Canonical span display | Open a note with headings, links, code and math; inspect while editing and scrolling. | Highlighting follows the buffer without a stale span or text mismatch; spoken semantic additions have their own W7 rows below. | `CanonicalHighlightCensus` | Pending | Pending | Pending |
| 4 | W2-3 | Semantic activation and preview | At a link, tag, citation and embed use Ctrl+Enter; use Ctrl+E for preview and Escape to return. | The correct target or canonical unresolved result is announced; preview content is readable and focus returns. | `W2EditorInteractionTests` | Pending | Pending | Pending |
| 5 | W2-4 / W2-5 | Feature-conditional inventory | Inspect the parity matrix entries for autocomplete and LaTeX authoring aids before claiming these routes. | Dropped or reserved features stay explicitly designated; no nonexistent popup is called verified. | `CommandDriftTests` | Pending | Pending | Pending |
| 6 | W7-1 | Semantic line, word and character reading | Open the shared editor_semantics.md fixture in editing mode; read headings, links, embeds, tags and citations by line, word and character. | At stock settings the required heading/link semantics are heard at the correct text; record each reader separately. | `EveryCanonicalKindHasItsContractAttributeThroughTheNativePeer`; `EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean` | Pending | Pending | Pending |
| 7 | W7-1 | Say-all outside the viewport | From the start use NVDA+Down or JAWS Insert+Down through the whole fixture, including offscreen text. | All text is read in order with heading and link semantics; the paint viewport does not limit speech. | `MixedPlainAndOffscreenReadsUseOneCanonicalQueryAndPreserveThePaintWindow` | Pending | Pending | Pending |
| 8 | W7-1 | Link boundaries and selection | Arrow by character into and out of a wikilink; extend and reverse selection with Shift+Arrow; delete the selected link and undo. | Entry/exit and selection follow the reader’s conventions without stale text or doubled focus. | `NativeRangeOffsetsSurviveUnicodeMovementCloningAndRangeOperands`; `RetainedSelectionTracksDeletionUndoAndSubsequentMovement`; `EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean` | Pending | Pending | Pending |
| 9 | W7-1 | Braille semantic fields | Use NVDA Braille Viewer, or the reader’s braille output, on a heading and link. | Heading/link markers track the current range; record the output and braille configuration. | `EveryCanonicalKindHasItsContractAttributeThroughTheNativePeer` | Pending | Pending | Pending |
| 10 | W7-1 | IME semantic updates | Compose Japanese text, update it, commit it, then cancel a second composition while the reader tracks the caret. | One committed batch produces one reading update; intermediate composition does not produce stale semantic speech. | `CompositionReadsStayUnavailableUntilTheCommittedNativeEdit`; `PeerUpdatesRefuseQueriesAndTwentyEditsPublishOneOrderedBatch` | Pending | Pending | Pending |
| 11 | W7-1 | Per-reader attribute consumption | With stock NVDA and stock licensed JAWS, record whether Heading 1/2 and Link are consumed during line reading; separately record any enabled style-reporting pass. | JAWS StyleId/Hyperlink support is measured, not assumed; a failure is a named Finding with transcript, not Verified. | `EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean` | Pending | Pending | Pending |

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.

Prototype evidence (not a production pass): [NVDA 2026.1.1 run and raw logs](https://github.com/coryj627/slate/blob/5d3b2a7f3bf0f73fba23c9d2028aa3a2210236ac/apps/slate-windows/tools/EditorHyperlinkPrototype/NVDA-VALIDATION.md). Production now uses ordinary Hyperlink children. Retest link destinations with NVDA+K, object navigation/Invoke, selected-link deletion and Say All using the owner's sound-device setup. The deterministic retained-selection regression is fixed; audible timing and licensed JAWS remain Pending.

Production agent-operated evidence: [2026-09-18 NVDA OneCore run](w7_1_nvda_production_verification.md)
verifies complete Say All termination, required semantic speech output, native
object Invoke, destination reporting and selected-link deletion/undo. It records
the EOF-loop finding and passing retest. Human audio confirmation, the complete
manual reading routes, braille, live IME and licensed JAWS remain Pending.

# Hyperlink prototype: NVDA validation, 18 September 2026

**Verdict: the native Hyperlink-child model works with NVDA for structured
reading and object activation. This is not a complete accessibility
acceptance pass.** Destination reporting is missing, deletion produces an
incorrect selection announcement, and a complete Say All run remains
blocked by the audio environment.

Tested prototype implementation: `ecb12fe4` on
`codex/w7-747-hyperlink-prototype`. No production code changed during this
rerun. PR #1227 remains open and unchanged at
`c562a62107eaf3545c28cfc131d03ca52363a679`.

## Method and environment

- Windows 11 25H2, build 26200.9457; Release prototype; .NET 10.
- Installed NVDA 2026.1.1 AMD64, file version 2026.1.1.55980.
- Agent-driven Computer Use keyboard/mouse actions against the actual
  editor, with NVDA Speech Viewer and input/output logging at level 12.
  The transcripts are NVDA output, not speech inferred from a UIA tree.
- The installed `nvda_noUIAccess.exe` ran with an isolated configuration
  under the prototype's ignored `target/nvda-validation-config` folder.
  Third-party add-ons were disabled. No user profile settings were edited.
- This machine has no audio render endpoint. NVDA's ordinary OneCore
  startup failed to initialize audio. The isolated run used the built-in
  **No speech** (`silence`) synthesizer. These results establish generated
  speech and braille text, not audible output or speech timing.
- The first run used the desktop keyboard layout. The second used the
  laptop layout because Computer Use sent numpad digits instead of NVDA's
  desktop object-navigation keys. The accidental digit was undone, and
  Num Lock was returned to its initial off state.
- Headings and links were enabled throughout. Font attribute reporting
  was enabled for speech and braille in the second run. The exact final
  isolated profile is in [evidence/nvda-test-profile.ini](evidence/nvda-test-profile.ini).
- The isolated profile opened NVDA's usage-data question. It was left
  unanswered. The viewers' controls were disabled by that dialog, but
  their text continued to update while the editor and NVDA commands
  remained operational. No privacy preference was selected.

## Results

| Check | Result | Observed evidence |
| --- | --- | --- |
| In-process range conformance | Pass | 46 assertions, including both-direction comparisons/movement, anchor identity, deletion and undo |
| Cross-process conformance | Pass | 132 assertions across CUIAutomation and CUIAutomation8; eight canonical children, exclusions, offscreen content, Invoke and stale-child rejection |
| Headings | Pass | `heading level 1`, `# Heading one`; `heading level 2`, `## Heading two` |
| All six link kinds | Pass | Wikilink, embed, tag, citation, HTTPS link and image each announced as `link`, with source text and surrounding prose |
| Quoted and initially offscreen links | Pass | Quoted link announced; Ctrl+End then Up reads `Offscreen`, `link`, `[[FarAway]]`, `ends.` |
| Code/comment lookalikes | Pass | Both `[[hidden comment]]` and `[[hidden code]]` read as ordinary source text without a link role |
| Character/word boundaries | Pass | Entering wikilink announces `link`; word movement reads `Target`; leaving announces `out of link`, `after` |
| Selection without edits | Pass | Ctrl+Shift+Left selects `]] `; Shift+Left extends to `t]] ` and announces `t selected` |
| Insert before link and undo | Pass | Pasted `prefix ` shifts the link; read-line still announces it. Undo restores original text. An ordinary `x` key is echoed and inserted, then undone |
| Italic/bold/strikethrough | Pass with font reporting enabled | Read-line announces each style and its exit. Inline-code source remains readable; no separate code-role announcement was established |
| NVDA object navigation and Invoke | Pass for diagnostic callback | NVDA+Shift+Down reaches `[[Target]] link`; NVDA+Enter logs `Invoked link at 63` at 13:46:23 UTC |
| Delete and restore semantic child | Pass for document/tree state | Deleting `[[Target]] ` leaves `Before after.` and seven children; Undo restores the line and an eighth child |
| Selection speech during deletion | **Fail, reproduced twice** | NVDA incorrectly announces `after.\nEmbe unselected` after deleting the selected `[[Target]] `. Document content remains correct |
| Destination reporting | **Missing** | NVDA+K says `Link has no apparent destination` on both the wikilink and an ordinary HTTPS link |
| Say All through the full fixture | **Incomplete** | No-speech run emits the first three lines and does not progress. Complete reading with a working speech synthesizer remains unverified |
| Braille software output | Partial pass | NVDA log carries `h1`, `h2`, and `lnk` markers. Braille Viewer exposes the current website-link text. Physical display, routing and hardware acceptance were not tested |

The fixture was restored to its original text and eight children after
the run. No actual vault or website navigation was performed: the host
records Invoke rather than opening a destination.

## Findings before production work

1. Supply meaningful destination metadata through the appropriate native
   Hyperlink interface. The experimental peer currently has no Value
   pattern. Do not invent destination URIs for tags, citations or vault
   links merely to satisfy NVDA+K.
2. Diagnose the deletion announcement against unmodified AvalonEdit and
   a normal speech synthesizer. The underlying cause has **not** been
   attributed to the new child peers. In the No-speech run, NVDA also logs
   a `languageHandling.normalizeLanguage` error (`NoneType.replace`) on
   both deletions, then continues speaking. Preserve that distinction from
   a confirmed editor-provider defect.
3. Repeat Say All and speech timing on an audio-capable desktop. Ordinary
   line navigation and offscreen access passing do not establish that Say
   All passes. NVDA's built-in [silence driver](https://github.com/nvaccess/nvda/blob/master/source/synthDrivers/silence.py)
   does not implement the normal synthesizer completion notifications;
   its behavior is consistent with the observed stall, but a live-synth
   control run is still required.
4. Replace whole-document metadata rebuilding before production use.
   The rerun measured 6.540 / 54.298 / 470.678 ms after an edit at
   100 KiB / 1 MiB / 8 MiB. Cached queries were below 0.002 ms. These are
   exploratory costs, not a passing production benchmark gate.
5. Complete the existing production review for structure-change events,
   stale-object lifetimes, full-app navigation, IME, licensed JAWS,
   hardware braille and Mac convergence. No results for those are claimed.

## Evidence

- [In-process rerun](evidence/rerun-in-process.txt)
- [Cross-process rerun](evidence/rerun-uia.txt)
- [Timing rerun](evidence/rerun-timings.txt)
- [Raw NVDA desktop run](evidence/nvda-desktop-run.txt)
- [Raw NVDA laptop/formatting run](evidence/nvda-laptop-run.txt)
- [Host Invoke log excerpt at the NVDA activation timestamp](evidence/nvda-host-invoke.txt)
- [Final Speech Viewer and Braille Viewer capture](evidence/nvda-viewer-capture.txt)

The raw logs retain startup warnings, the incomplete Say All attempt,
keyboard-layout recovery, and deletion errors so the result can be
reviewed without treating those failures as passes.

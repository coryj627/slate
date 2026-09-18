# W7-1 production NVDA verification — 2026-09-18

This is an agent-operated production run, separate from the earlier throwaway
prototype and from the owner's human NVDA and licensed JAWS sign-offs.

**Tester:** Codex, through the approved Windows Computer Use tools
**AT:** NVDA 2026.1.1 AMD64, Windows OneCore synthesizer, add-ons disabled;
laptop keyboard layout, heading/link reporting enabled, other reading settings
unchanged. No W-E7 layer or custom AT add-on.
**OS:** Windows 11 25H2, build 26200.9457
**Build:** `codex/w7-747-editor-peer`, Release; Say All and object Invoke at
`9d8e4c601ebd11b76194440341a2ab72d880048d`; selection/deletion/undo and destination
retest at `f46b4a073b52796ef8a5dd50754007e1f44693f2`
**Corpus:** Disposable `target/w747-nvda-production/vault/validation.md`, with
`Target.md` and `FarAway.md`; headings 1/2, all six link kinds, quoted link,
literal-suppressed links, 25 offscreen lines and a final marker. Mixed LF/CRLF.
The shared all-kind `editor_semantics.md` remains the automated corpus.
**Method:** Real injected keyboard/mouse input; NVDA speech debug transcript,
live OneCore callbacks, and observed Slate UIA document/selection/target changes.
**Run date:** 2026-09-18; transcript timestamps below are the machine's local
clock, configured America/New_York. Raw logs are local ignored evidence under
`target/w747-nvda-production/logs/`; the bounded excerpts below contain only the
disposable fixture interaction.
**Audio:** NVDA successfully initialized OneCore with Line 1 (Virtual Audio
Cable) available. Human confirmation that speech is audible is still Pending.
Synthesizer callbacks and text output do not substitute for that confirmation.

| Check | Observed result | Evidence / limits |
|---|---|---|
| Required semantics | Verified in NVDA speech output | Heading 1/2 and all six link kinds were announced in the complete Say All run. |
| Say All outside viewport | Verified | Full fixture read in order, including `[[FarAway]]` and the final marker, then normal stop. |
| Native object navigation and Invoke | Verified | NVDA+Shift+Down selected `[[Target]]` as a link; NVDA+Enter said `invoke`; the editor became `Target.md editor` containing `# Target`. |
| Meaningful link destination | Verified | NVDA+K said `Target` at the wikilink on the final code build. The earlier production HTTPS check said `https://example.org`. |
| Selection, selected-link deletion and undo | Verified | Selection text matched the token; deletion announced leaving the link and the following character; undo restored the token and its selection announcement. |
| Character/word semantic boundaries | Partial | Ctrl+Right announced link entry, character selection reversal announced the removed space, and deletion announced exit. The full human character/word/line checklist remains Pending. |
| Braille viewer | Pending | Viewers opened, but the separate NVDA Usage Data Collection dialog requires the owner's choice before continuation. No braille result is inferred from speech. |
| IME composition and cancel with AT | Pending | Automated composition/batching facts pass; no live installed-IME pass recorded here. |
| Licensed JAWS | Pending | Owner's separate run remains required. |

## Bounded transcript

The first production Say All at `bf02c531` read all text but then emitted repeated
empty `say-all:lineReached` callbacks at EOF. It was stopped explicitly. A-9 in
the contracts document records the native AvalonEdit movement cause, the red
regressions and the correction. The following corrected run closes that finding:

```text
06:46:29.746  heading level 1; # Heading one
06:46:38.930 onward  NVDA Say All: heading levels, Before; link; [[Target]]; after.
                Embedded; link; ![[Target]]; tag; link; #project;
                citation; link; [@smith2020]
                link; [Website](https://example.org); and;
                link; ![Picture](picture.png)
06:48:03.421  Plain offscreen line 25.
06:48:05.444  Offscreen; link; [[FarAway]]; ends.
06:48:07.868  Final validation marker.
06:48:10.410  CallbackCommand(name=say-all:stop)
06:48:51.148  [[Target]]; link
06:49:03.207  invoke
              Observed result: Target.md editor; # Target
```

Final code build selection/destination retest:

```text
06:52:57.615  validation.md editor; document; Before; link; [[Target]]; after.
06:53:13.530  link
06:53:13.531  left bracket
06:53:20.677  [[ selected
06:53:26.471  Target selected
06:53:32.451  ]]  selected
              Observed selection: [[Target]] followed by one space
06:53:42.881  out of link; a
              Observed document: Before after.
06:53:52.636  [[Target]]  selected
              Observed undo: Before [[Target]] after.
06:54:05.155  space unselected
              Observed reversed selection: [[Target]]
06:54:13.546  [[Target]] unselected
06:54:23.574  Target
```

The deleted-link pass did not repeat the prototype's stale following-line
selection text. The last movement correction additionally passed 112 focused
facts and the real cross-process UIA3/axe journey, including the entire
read/advance/collapse loop. Both independent A-9 round-two reviews were clear.
These automated and agent-operated observations leave the human checklist cells
Pending until a named human run supplies the remaining acceptance evidence.

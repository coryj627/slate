# Editor Hyperlink prototype — throwaway

Question: can native Hyperlink child peers expose source-editor links to
NVDA while keeping AvalonEdit's ordinary text ranges mutually compatible?

This branch is isolated from PR #1227. It uses the actual Slate editor,
AvalonEdit 6.3.1.120, and the same Release Rust buffer/semantic queries as
that PR. It disables the generic source-span `UIA_LinkAttributeId` mapping
and supplies virtual Hyperlink peers through the editor's accessibility
tree, `GetChildren`, `GetEnclosingElement`, and `RangeFromChild`.

The diagnostic host's Invoke callback records the requested source offset.
It does not open websites, resolve a vault, or validate navigation outcomes.
The experimental peer calls the existing `EditorInteractionCoordinator`
when hosted by the full app, but that integration is separately unverified.

## Run

From the prototype worktree root, after generating/staging the normal
Release C# binding and native library:

```powershell
dotnet run --project apps/slate-windows/tools/EditorHyperlinkPrototype/EditorHyperlinkPrototype.csproj --configuration Release
```

The editable fixture is disposable and held in memory. The controls reset
it, insert a prefix, remove the first link, or move the caret. Diagnostics
are written beneath the executable's `evidence` directory.

The executable also accepts:

```text
--self-check <output-file>         In-process range/identity checks
--bench <output-file>              Initial, cached, and post-edit query costs
--probe <server.hwnd> <output-file> Cross-process UIA checks against open host
```

The UIA probe runs both CUIAutomation and CUIAutomation8. Each verifies eight
canonical links, offscreen inclusion, code/comment exclusion, exact source
text, heading StyleId, Hyperlink role, range ownership, no recursive child,
both-direction comparison/endpoint movement, identity after insertion,
Invoke callback, and rejection of a deleted child. It edits only the
diagnostic host's in-memory fixture and resets it on success.

## Initial engineering results

Windows 11, .NET 10.0.12, NVDA installation version 2026.1.1.55980.

- In-process checks: passed.
- Cross-process CUIAutomation and CUIAutomation8: passed.
- No exported Link-attribute adapter is exercised. Ordinary WPF ranges
  work in both directions without a special clone workaround.
- A simple whole-document metadata refresh is deliberately used here.
  At 100 KiB / 1 MiB / 8 MiB, measured post-edit rebuilds were
  6.831 / 44.818 / 428.699 ms; cached queries were below 0.002 ms each.
  This is exploratory timing, not the production benchmark gate. Large
  document edit costs need an incremental design before production use.
- NVDA Speech Viewer rerun on 18 September: headings, all six link kinds,
  character/word boundaries, selection, insertion/undo, formatting and
  object activation passed their bounded checks. Code/comment lookalikes
  stayed plain text, and the initially offscreen link was reachable.
- This is not a full acceptance pass: destination reporting is missing,
  selection deletion produces an incorrect announcement, and full Say All
  remains incomplete in this machine's no-audio environment. An isolated
  No-speech profile supplied actual NVDA transcripts; no audible output
  was verified. See [NVDA-VALIDATION.md](NVDA-VALIDATION.md) for the result
  matrix, exact settings, raw evidence and follow-up work.
- The automated rerun passed 46 in-process and 132 cross-process
  assertions. Latest 8 MiB post-edit metadata rebuilding cost 470.678 ms.
- JAWS, physical braille, and actual IME input: not executed.

## NVDA validation sequence

Use the installed NVDA with Speech Viewer and record the actual settings
and transcripts. Do not infer speech from the UIA tree.

1. Focus the editor, go to the top, and read lines through both headings
   and all six link kinds. Verify each link's role, source text and exit.
2. Read words and characters across a wikilink boundary. Select with
   Shift+Arrow and verify normal selection speech.
3. Run say-all through the fixture, including the offscreen link. Confirm
   code/comment lookalikes remain plain text and text is neither repeated
   nor omitted.
4. Insert text before a link, undo, remove a link, and restore the fixture.
   Verify current speech and caret behavior after each operation.
5. Inspect object navigation/activation and record whether it reaches the
   link child; distinguish its callback from actual vault navigation.
6. Check the NVDA braille viewer if available; do not claim hardware braille
   acceptance. Actual IME and licensed JAWS remain separate checks.

## Limits

This is not production code and does not change the frozen W7 contract.
No production integration, spec revision, or merge is authorized by a
passing prototype alone. The prototype keeps the existing heading/style
mapping. Child identity uses Avalon anchors plus canonical span matching;
overlap, document replacement, and exhaustive stale-object lifetimes need
production review. Structure-change event coverage is not yet complete.

See the repository's
[architecture research](../../../../docs/plans/18_windows_port/reports/w7_1_editor_accessibility_architecture_research.md)
for the official UIA model and existing WPF/Chromium/NVDA precedents.

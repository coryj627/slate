# WB-21b — Core renders the code preamble at three verbosities over the token stream, and Windows speaks the one the reader picked

Issues: Refs #1526 (closes with the mac follow-up WB-0 files: "mac: consume `code_block_preamble_with_verbosity`, Settings samples through core, VoiceOver pass per mode"). Size M. Lanes: rust, windows, swift (compile). AT: NVDA per mode. Wave: W8 (after WB-20; BEFORE WB-22 — both edit the `MainWindow.xaml` Editor menu, plan §6). Depends on: WB-21 (the two menu items it restores), WB-20 (`ReadingDocumentBuilder.cs`). Parallel with: WB-23, WB-24 only; never WB-22, which starts after this chunk merges. Decision: WD13 (pending; recommended: both modes over `source` and the `SyntaxToken` stream as quoted under Reference; under another answer — a different first-line rule, token separators, or a cap — the renderer's body rules and the facts' expected strings change, the signature does not).

## Goal

Settings offered "Preamble + first line" and "Preamble + all tokens" and
nothing rendered them; WB-21 removed the two modes on Windows (RD6).
This chunk gives core a sibling renderer that takes the verbosity and
renders both modes over `CodeBlock.source` and its `SyntaxToken` stream,
never `semantic_spans`, so a reader who picks "first line" hears the
block's first line after the preamble and one who picks "all tokens"
hears the whole block in source order with nothing omitted or
reordered. Windows consumes it in the reading view and puts the two
items back in the Editor menu; `code_block_preamble` and mac are
unchanged, so mac keeps compiling and speaking what it speaks today.

## Current state on main

Verified against the worktree; `report_a11y.md`'s lines hold.

- `crates/slate-core/src/code.rs:28-42` `TokenKind` (incl. `Other(String)`),
  `:47-51` `SyntaxToken { start_byte, end_byte, kind }`, `:63-68`
  `SemanticSpan` (declaration names only, `capture_semantic` `:387-412`
  — a JSON block or an expression has none; the plan's §2.2 correction
  holds), `:86-93` `CodeBlock { source, language, tokens, semantic_spans, line, byte_offset }`.
- `:204-224` `code_block_preamble(language: Option<&str>, source: &str) -> String`
  — "Code block, {language|plain text}, {N} line|lines."; goldens
  `:704` `preamble_matches_the_mac_goldens`, `:727` `preamble_edge_rules`.
- `:227-274` `highlight_code`: an oversized block (`> CODE_BLOCK_MAX_BYTES`)
  yields one `Other("oversized")` token over the source (`:239-250`);
  an unknown or missing language yields one `Other(tag|"text")` token
  over the source, or none for an empty source (`passthrough_code_block`
  `:303-325`); grammars emit one token per leaf, sorted by `start_byte`
  (`walk_tree` `:331-345`). Leaves do not cover whitespace between them.
- FFI: `crates/slate-uniffi/src/lib.rs:7700-7708`
  `#[uniffi::export] pub fn code_block_preamble(language: Option<String>, source: String) -> String`;
  `:7142-7160` the FFI `SyntaxToken` record (`From<core::code::SyntaxToken>`),
  `:7195-7200` the FFI `CodeBlock`.
- Windows: `Reading/ReadingDocumentBuilder.cs:376-380` `CodeFenceBlock(fence, block, codeBlocks, context)`;
  `:381-383` matches the saved `CodeBlock` by byte containment; `:395-405`
  `coherent` (saved source equals the live interior, LF-normalised);
  `:409` `string preamble = SlateUniffiMethods.CodeBlockPreamble(language, source);`
  `:422-425` `highlightable` reads `matched.Tokens`; `:436`
  `AutomationProperties.SetName(paragraph, preamble)`; `:438` the
  preamble is also in-range text. `ReadingListBuildContext` (`:23`) is
  the per-build state the chunks share; `Build(...)` overloads `:121-168`;
  the caller `Reading/ReadingContentViewModel.cs:1598-1611` (`new ReadingListBuildContext()`,
  `codeBlocks = fetched.CodeBlocks`). The MathPrefs precedent:
  `WorkspaceViewModel.cs:2158-2166` `EditorPreferences.MathPrefsChanged`
  → every `tab.Reading?.InvalidateForPrefsChange()`.
- Windows setting (after WB-21): `EditorInteractions.cs:59-65`
  `CodeVerbosityNames` (one key), `:185-205` the setter command and the
  `A11yEvent.CodePreambleVerbosity(display)` announcement (`a11y.rs:1038`,
  rendered `:2283` "Code preamble verbosity: {name}."), `:375-400`
  persistence; `MainWindow.xaml:537-556` the submenu (one item after
  WB-21; three before it — restore the two, same AutomationIds and
  `CommandParameter`s).
- Mac: `CodeBlockView.swift:99` calls `codeBlockPreamble(language:source:)`;
  `CoreRenderedCopyCensusTests.swift:53` `settingsPreambleSamples`. Both
  untouched here (the follow-up).

## Reference

WD13's recommendation, kept as written: both modes are defined "over
`CodeBlock.source` and `tokens` (the `SyntaxToken` stream, sorted by
`start_byte`), never `semantic_spans`. First line: the source's first
line verbatim (blank or not …; a leading blank line is spoken as 'blank
line'). All tokens: the whole source in source-byte order, each token
spoken as its text (punctuation as its characters, comments and strings
verbatim), with every source byte no token covers … spoken as plain
text in place, so nothing is omitted or reordered; an unknown language
… reads the source verbatim." RD6 and #1389's comment for the setting's
return. Contract `07_portability_review.md`: "code → AT preamble —
produced in Rust" (core owns the copy). `pr08` of plan 43 (#1053) hoisted
the preamble.

## Design

**Core (`code.rs`).** New:

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CodePreambleVerbosity { PreambleOnly, FirstLine, AllTokens }

pub fn code_block_preamble_with_verbosity(
    language: Option<&str>, source: &str, tokens: &[SyntaxToken], verbosity: CodePreambleVerbosity,
) -> String
```

Rules: `PreambleOnly` returns exactly `code_block_preamble(language, source)`.
`FirstLine`: preamble, then one space, then the bytes before the first
`\n` (the whole source when it has none); an empty first line renders
the literal `blank line`; an empty source renders the preamble alone (0
lines, nothing to speak). `AllTokens`: preamble, one space, then a walk
of `tokens` sorted by `start_byte`, each clamped to `[cursor, source.len()]`
and to char boundaries: the uncovered bytes before a token are emitted
as they are, then the token's bytes, then the remainder after the last
token — so for a well-formed stream (tree-sitter leaves, the passthrough
or oversized single token, or no tokens at all) the body equals the
source byte for byte, including newlines; nothing is translated (core
does not spell punctuation; the AT's punctuation level decides what is
heard). Empty source: preamble alone. The oversized single-token case
renders the whole source — WD13 names no cap and this spec adds none;
the Risks line below records it for the owner. The function never reads
`semantic_spans`.

**FFI (`lib.rs`).** New `#[derive(uniffi::Enum)] pub enum CodePreambleVerbosity { PreambleOnly, FirstLine, AllTokens }`
and `#[uniffi::export] pub fn code_block_preamble_with_verbosity(language: Option<String>, source: String, tokens: Vec<SyntaxToken>, verbosity: CodePreambleVerbosity) -> String`
beside `code_block_preamble`, whose signature does not change (§2.2:
a sibling, so mac compiles untouched). Both bindings regenerate; the
Swift lane compiles.

**Windows.** `ReadingListBuildContext` gains
`internal CodePreambleVerbosity CodePreambleVerbosity { get; init; } = CodePreambleVerbosity.PreambleOnly;`.
`CodeFenceBlock` (`:409`) calls
`CodeBlockPreambleWithVerbosity(language, source, coherent ? matched!.Tokens : [], context.CodePreambleVerbosity)`
for the paragraph's `AutomationProperties.Name` (`:436`); the in-range
preamble run (`:438`) keeps the short `CodeBlockPreamble` (the code
itself follows in range; speaking it twice would double every line).
`ReadingContentViewModel` gets `internal Func<CodePreambleVerbosity>? CodePreambleVerbosityProvider { get; set; }`
(new), read when the context is built (`:1598`) and in the rebuild path
(`:1757`). `EditorPreferencesViewModel` gets
`internal event Action? CodePreambleVerbosityChanged;` (new) raised from
the setter, `internal CodePreambleVerbosity CurrentCodePreambleVerbosity`
mapping the three keys; `WorkspaceViewModel` wires the provider at tab
creation and, on the event, `tab.Reading?.InvalidateForPrefsChange()`
for every tab (the `:2158-2166` shape). `CodeVerbosityNames` regains its
two keys and `MainWindow.xaml` its two `CheckMenuItem`s (byte-for-byte
the pre-WB-21 ones). Not changed: the chord-table row
`windows.editor.setCodePreambleVerbosity` (stays `Unreg`,
parameterised), `AppPreferencesStore`, the `CodePreambleVerbosity`
announcement, mac.

## Tests

Rust (`code.rs` tests):
- `preamble_only_is_code_block_preamble` — identical for the goldens' inputs.
- `first_line_is_the_first_line_verbatim` — `fn main() {\n}\n` rust → `Code block, rust, 2 lines. fn main() {`.
- `first_line_blank_speaks_blank_line` — `\nx\n` → `… blank line`.
- `first_line_of_an_empty_source_is_the_preamble_alone`.
- `all_tokens_over_a_grammar_stream_equals_the_source` — rust, python (`x = 1 + 2`, no declarations), json (`{"a": [1, 2]}`), each through `highlight_code`: body == source.
- `all_tokens_keeps_comments_and_strings_verbatim` — `// note\nlet s = "a b";\n`.
- `all_tokens_emits_uncovered_ranges_in_place` — synthetic tokens `[2..4), [6..8)` over `abcdefghij` → body `abcdefghij`; dropping the gap emission → fails.
- `all_tokens_unknown_language_reads_the_source` — tag `brainfuck` through `highlight_code` (one `Other` token).
- `all_tokens_with_no_tokens_reads_the_source` and `all_tokens_clamps_overlapping_and_out_of_range_tokens` (synthetic `[0..6)`, `[4..20)` over a 10-byte source → source once).
- `all_tokens_renders_tokens_not_declarations` — `fn alpha() { beta() }\n` through `highlight_code` (rust; one `Function` semantic span naming `alpha`): the exact output is `Code block, rust, 1 line. fn alpha() { beta() }\n` — the declaration name appears once, in place, never alone, never appended. Mutation: render `semantic_spans` names (an implementation walking declarations would produce `… alpha`) → fails.
Mutations: drop the gap emission; sort by `end_byte`; render `semantic_spans` names; omit the blank-line literal.

Windows (`ReadingViewTests`, `W2EditorInteractionTests` — the class that holds the editor-preferences facts today):
- `ReadingViewTests.ACodeBlockPeerSpeaksTheFirstLineUnderThatVerbosity` — context verbosity FirstLine: paragraph Name is core's string; the in-range preamble text is unchanged (`:3515` shape).
- `ReadingViewTests.ACodeBlockPeerSpeaksEveryTokenUnderThatVerbosity` — Name == `CodeBlockPreamble + " " + interior` for a coherent fence; an incoherent fence (unsaved edit) still speaks the live interior (empty token list).
- `ReadingViewTests.ChangingTheCodeVerbosityReprojectsOpenReadingViews` — set the preference; the open projection's code Name changes without a text change; nothing announced but the setting line.
- `W2EditorInteractionTests.TheThreeCodeVerbosityModesAreBackAndEachIsAnnouncedOnce` (restores `ReadingViewTests.cs:3544-3552`'s pins) and `CheckItemGroupsTests.cs:114`, `CommandDriftTests.cs:349` back to three items.
- Census: no new `A11yEvent`; `A11yCorpusCensus` unchanged. The Swift lane compiles the regenerated binding (no mac test edit: no record field or event added).

## Docs and registers

- `w_c_matrix.md:21` (Code blocks row): the Name cell gains "preamble at the reader's verbosity (WB-21b); in-range text keeps the short preamble".
- Contract `07_portability_review.md` or plan 43 pr08's record: append "the verbosity renderer is core's sibling function; mac consumption is the follow-up".
- No `chords.json`, designation or trigger-ledger change. Help docs: the Windows settings page names the three modes again.
- WB-0 files the mac follow-up (title in the header line) and #1526 stays open until it merges.

## Verification

PowerShell, repo root. Each filter is listed first and the block throws
unless every required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed").

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb21b'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
$rustFacts = @('preamble_only_is_code_block_preamble', 'first_line_is_the_first_line_verbatim', 'first_line_blank_speaks_blank_line', 'first_line_of_an_empty_source_is_the_preamble_alone', 'all_tokens_over_a_grammar_stream_equals_the_source', 'all_tokens_keeps_comments_and_strings_verbatim', 'all_tokens_emits_uncovered_ranges_in_place', 'all_tokens_unknown_language_reads_the_source', 'all_tokens_with_no_tokens_reads_the_source', 'all_tokens_clamps_overlapping_and_out_of_range_tokens', 'all_tokens_renders_tokens_not_declarations')
$listed = cargo test -p slate-core --locked code::tests -- --list; Assert-Ok 'listing code::tests'
$missing = @($rustFacts | Where-Object { -not ($listed -match "(^|::)$([regex]::Escape($_)): test$") })
if ($missing.Count -gt 0) { throw "code::tests does not select: $($missing -join ', ')" }
cargo test -p slate-core --locked code::tests; Assert-Ok 'code::tests'   # the eleven preamble facts, each seen failing first
# the Rust gates (plan §4 rule 3; CONTRIBUTING.md:76-98 — no make on this machine):
cargo fmt --all -- --check; Assert-Ok 'cargo fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'cargo clippy'
cargo test --workspace --locked; Assert-Ok 'cargo test --workspace'
cargo bench --workspace --no-run; Assert-Ok 'cargo bench --workspace --no-run'   # the bench compile
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'license headers'   # Git Bash by full path: a bare `bash` in PowerShell here resolves to WSL
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.ReadingViewTests|FullyQualifiedName~SlateWindows.Tests.W2EditorInteractionTests|FullyQualifiedName~SlateWindows.Tests.CheckItemGroupsTests|FullyQualifiedName~SlateWindows.Tests.CommandDriftTests|FullyQualifiedName~A11yCorpusCensus'
$names = dotnet test $tests --no-build --configuration Release --list-tests --filter $filter; Assert-Ok 'test discovery'
$missing = @('ReadingViewTests.ACodeBlockPeerSpeaksTheFirstLineUnderThatVerbosity', 'ReadingViewTests.ACodeBlockPeerSpeaksEveryTokenUnderThatVerbosity', 'ReadingViewTests.ChangingTheCodeVerbosityReprojectsOpenReadingViews', 'W2EditorInteractionTests.TheThreeCodeVerbosityModesAreBackAndEachIsAnnouncedOnce') | Where-Object { -not ($names -match "\.$([regex]::Escape($_))\s*$") }
if (@($missing).Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
if (-not ($names -match 'A11yCorpusCensus\.')) { throw 'no A11yCorpusCensus fact discovered' }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'the Windows facts'
$files = @(
  'apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs',
  'apps/slate-windows/src/SlateWindows/Reading/ReadingContentViewModel.cs',
  'apps/slate-windows/src/SlateWindows/EditorInteractions.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ReadingViewTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/W2EditorInteractionTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/CheckItemGroupsTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/CommandDriftTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files; Assert-Ok 'dotnet format'
```

CI: `rust.yml`, the Windows lane, `swift-tests.yml` (compile). AT: NVDA
full pass per mode on one note with a rust fence, a python expression
fence, a json fence, a fence with a leading blank line and an unknown
language tag — object navigation to each block reads the expected Name
under Preamble only, "+ first line" ("Code block, rust, 2 lines. fn
main() {"; "… blank line") and "+ all tokens" (the whole block, source
order); say-all hears the short preamble once, then the code. Recorded
in the PR body; the PR stays a draft until then (§4 rule 4).

## Acceptance

- [ ] Rust facts and Windows facts fail first, then pass; mutations run.
- [ ] The Verification block runs clean in PowerShell: the eleven Rust facts and the four Windows facts discovered by name, and every command's `$LASTEXITCODE` 0 — the Rust gates of plan §4 rule 3 included (`cargo fmt`, `cargo clippy`, `cargo test --workspace`, `cargo bench --workspace --no-run`, the license-header check under Git Bash).
- [ ] `code_block_preamble`'s signature unchanged; mac builds in the Swift lane with no mac source edit.
- [ ] Both bindings regenerated from the same dylib (never mismatched).
- [ ] Menu items and keys restored; the setting re-projects open reading views.
- [ ] `w_c_matrix.md` row updated; NVDA pass per mode recorded.
- [ ] PR body: `Refs #1526` (never `Closes`), "not delivered: the mac consumer and the Settings samples — the WB-0 follow-up".
- [ ] Risk recorded for the owner: all-tokens over an oversized block speaks the whole source; no cap in WD13.

## Brief

Read `crates/slate-core/src/code.rs:28-93`, `:204-345`, `:700-740`;
`crates/slate-uniffi/src/lib.rs:7142-7200`, `:7700-7708`;
`apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs:23-40`,
`:121-168`, `:376-445`; `Reading/ReadingContentViewModel.cs:1590-1625`;
`WorkspaceViewModel.cs:2149-2166`; `EditorInteractions.cs:43-70`,
`:185-205`, `:370-400`; `MainWindow.xaml:537-556`;
`ReadingViewTests.cs:3400-3560`; `CONTRIBUTING.md:76-98`. Decision WD13
is pending (recommended: "preamble only" = `code_block_preamble`;
"first line" = preamble, a space, the source's first line verbatim,
`blank line` for an empty first line, the preamble alone for an empty
source; "all tokens" = preamble, a space, the whole source in byte order
walked over the `SyntaxToken` stream sorted by `start_byte`, uncovered
bytes emitted in place, tokens clamped, nothing translated, no cap —
never `semantic_spans`; under another answer the body rules and the
facts' expected strings change, the signature does not). Build, in core:
`pub enum CodePreambleVerbosity { PreambleOnly, FirstLine, AllTokens }`
and `pub fn code_block_preamble_with_verbosity(language: Option<&str>, source: &str, tokens: &[SyntaxToken], verbosity: CodePreambleVerbosity) -> String`
in `code.rs`, `code_block_preamble` untouched; in the FFI the
`uniffi::Enum` and `#[uniffi::export] pub fn code_block_preamble_with_verbosity(language: Option<String>, source: String, tokens: Vec<SyntaxToken>, verbosity: CodePreambleVerbosity) -> String`
beside the old function; both bindings regenerated from one dylib. In
Windows: `ReadingListBuildContext.CodePreambleVerbosity` (default
PreambleOnly) consumed at `ReadingDocumentBuilder.cs:409` for the
paragraph's `AutomationProperties.Name` only (the in-range run at `:438`
keeps the short preamble), `ReadingContentViewModel.CodePreambleVerbosityProvider`
read when the build context is made, `EditorPreferencesViewModel.CodePreambleVerbosityChanged`
and `CurrentCodePreambleVerbosity`, the workspace wiring the event to
`tab.Reading?.InvalidateForPrefsChange()` for every tab, the two keys
back in `CodeVerbosityNames` and the two `CheckMenuItem`s back in
`MainWindow.xaml` with their pre-WB-21 AutomationIds and parameters.
Rust facts in `code.rs`, failing first: `preamble_only_is_code_block_preamble`,
`first_line_is_the_first_line_verbatim` (`fn main() {\n}\n` → `Code block, rust, 2 lines. fn main() {`),
`first_line_blank_speaks_blank_line`, `first_line_of_an_empty_source_is_the_preamble_alone`,
`all_tokens_over_a_grammar_stream_equals_the_source` (rust, python
expression, json: body == source), `all_tokens_keeps_comments_and_strings_verbatim`,
`all_tokens_emits_uncovered_ranges_in_place` (synthetic gapped tokens
over `abcdefghij` → `abcdefghij`), `all_tokens_unknown_language_reads_the_source`,
`all_tokens_with_no_tokens_reads_the_source`, `all_tokens_clamps_overlapping_and_out_of_range_tokens`,
`all_tokens_renders_tokens_not_declarations` (`fn alpha() { beta() }\n` →
exactly the source after the preamble; a declaration-name renderer
fails). Windows facts: `ReadingViewTests.ACodeBlockPeerSpeaksTheFirstLineUnderThatVerbosity`,
`ReadingViewTests.ACodeBlockPeerSpeaksEveryTokenUnderThatVerbosity`
(coherent fence: Name == short preamble + space + interior; incoherent
fence: the live interior with an empty token list),
`ReadingViewTests.ChangingTheCodeVerbosityReprojectsOpenReadingViews`,
`W2EditorInteractionTests.TheThreeCodeVerbosityModesAreBackAndEachIsAnnouncedOnce`;
`CheckItemGroupsTests.cs:114` and `CommandDriftTests.cs:349` back to
three items; `A11yCorpusCensus` unchanged (no new event). Mutations to
run locally: drop the gap emission; sort tokens by `end_byte`; render
semantic names; omit `blank line`; put the verbose string in the
in-range run. Registers: the Code blocks row at
`docs/plans/18_windows_port/w_c_matrix.md:21`; a note on
`docs/plans/07_portability_review.md`; WB-0's mac follow-up named in the
PR body. Rust gates on PowerShell (plan §4 rule 3; CONTRIBUTING.md:76-98),
each followed by a `$LASTEXITCODE` check that throws: `cargo fmt --all -- --check`,
`cargo clippy --all-targets --workspace -- -D warnings`,
`cargo test --workspace --locked`, `cargo bench --workspace --no-run`
(the bench compile) and `& "$env:ProgramFiles\Git\bin\bash.exe"
scripts/check-license-headers.sh` (Git Bash by full path; a bare `bash`
in PowerShell here resolves to WSL). Done when those exit 0, the eleven
Rust facts are listed by `cargo test -p slate-core --locked code::tests
-- --list` (the block throws on a missing one) and pass, the four
Windows facts and an `A11yCorpusCensus` fact are discovered by
`--list-tests` under the filter (throwing on a missing one) and pass,
the Windows lane, `dotnet format --include` over the eight files and the
Swift compile lane are green, and
the NVDA pass per mode (rust, python, json, leading-blank-line and
unknown-language fences; object navigation reads the expected Name under
each mode; say-all hears the short preamble once) is in the PR body —
draft until then. Not delivered: the mac consumer and the Settings
samples (WB-0's follow-up); no cap for oversized blocks (risk recorded).
PR body: `Refs #1526`, never `Closes`. Commit: `feat(core,windows): the
code preamble renders at the reader's verbosity over the token stream`.

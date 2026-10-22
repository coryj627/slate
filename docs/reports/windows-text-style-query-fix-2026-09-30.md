# Windows text-style query fix (#1320), September 30, 2026

The synchronous screen-reader query cost reported in [#1320](https://github.com/coryj627/slate/issues/1320) was reproduced on this Windows machine and repaired. The production decorator now walks the FlowDocument's paragraphs and nested block containers, while retaining WPF's original paragraph expansion for the selected search result and its endpoint clamping. Existing semantic facts remain unchanged.

## Measured reproduction and result

The same real ReadingSurface/automation provider workload builds 10,050 plain paragraphs followed by a quote, then performs `GetAttributeValue(StyleName)` returning Mixed, a forward `FindAttribute(StyleName, Quote)`, and a backward `FindAttribute(StyleId, Quote)`. Document construction, FFI calls used for construction, and result text extraction are outside the measured region. Allocations are managed allocations on the query's STA thread.

| Current-revision baseline | After, focused run | After, complete reading class |
| --- | --- | --- |
| 10,137.6 ms | 16.2 ms | 10.1 ms |
| 4,960,969,504 allocated bytes | 7,404,840 allocated bytes | 7,404,840 allocated bytes |

Allocation fell about 670-fold (99.85%). The local timing samples improved approximately 626–1,004-fold, with JIT/warmth/run-order effects; these are local observations, not a CI latency distribution or a measured slow-runner promise. The optimized average including fixed setup is roughly 246 allocated bytes per traversal paragraph, rather than about 165,000. There is still bounded per-paragraph managed allocation.

The new allocation witness was written before the production fix, then run against the rebuilt current source. It failed with the baseline above, while its original Mixed/search semantic assertions passed. Its final automated gate is a 16-MiB allocation ceiling for the three queries. It records elapsed time without adding a hardware-specific time gate. The existing reading STA timeout and huge-document semantic witness remain intact.

## Implementation and compatibility

[HeadingStyleText.cs](../../apps/slate-windows/src/SlateWindows/Reading/HeadingStyleText.cs) resolves the query's first paragraph and end boundary through a constant number of WPF adaptor operations, then visits Paragraph, Section, List/ListItem, and Table/row/cell blocks in document order. It does not truncate a large document. A caret contributes its enclosing paragraph, and a caret search remains empty. A query near the end of a flat note starts at its owning block rather than scanning preceding paragraphs.

The exclusive-end comparison respects WPF insertion positions. Normalization is needed only for the paragraph containing that end: normalizing every paragraph was tested and caused unnecessary allocation, which the new allocation regression caught.

Forward search stops at its first matching paragraph. Backward search remembers the final matching paragraph and constructs only one result. Creating raw WPF result ranges uses the already cached `_start` reflection field on a fresh, private adaptor clone, followed by another clone to obtain WPF-owned mutable pointer copies. No query endpoint or document-owned pointer is modified, and no `_end`, provider-owner, or constructor reflection dependency was added. WPF's [TextRangeAdaptor source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/TextRangeAdaptor.cs) documents that Clone invokes its constructor and the constructor copies both pointers; public paragraph expansion and endpoint operations remain responsible for the result's range behavior.

The original public expansion is reused when the match is the first paragraph of the query. Differential testing found why this matters: a query starting at a quote's ContentEnd can find its newline; reconstructing that paragraph from ContentStart had instead returned an empty result. This distinction was repaired and retained as part of the boundary corpus. Reflection failure retains the prior degraded behavior, and public WPF-operation exceptions retain their normal behavior.

## Validation

[ReadingViewTests.cs](../../apps/slate-windows/tests/SlateWindows.Tests/ReadingViewTests.cs) adds five facts: measured allocation cost; clipping both ends without changing the query; nested sections/lists/table cells and heading levels 1–9; missing-adaptor-pointer fallback; and differential parity with a test-only copy of the original raw-WPF adaptor walker.

The differential oracle compares **192 boundary spans**, including degenerate points and ranges from/to document edges, ElementStart/ContentStart/insertion start/ContentEnd/ElementEnd, empty/plain/quote/heading paragraphs, nested containers, a BlockUIContainer, and styled paragraphs beginning with Hyperlink and InlineUIContainer. For each span it compares both style attributes, forward/backward quote and heading searches, match presence, exact start/end endpoints, and the unchanged query range. It exercises a different traversal mechanism from the optimized product implementation.

- Shared Release solution build: succeeded with zero warnings and errors.
- Focused style/allocation/differential tests: **11/11 passed**.
- Complete ReadingViewTests class: **72/72 passed**, 16.2014 seconds.
- Scoped `git diff --check`: passed.

The baseline command was:

```powershell
dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter FullyQualifiedName~HugeDocumentSyntheticQueriesHaveBoundedCost --logger "console;verbosity=detailed"
```

The final full-class command, from `C:\dev\slate\apps\slate-windows`, was:

```powershell
dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter FullyQualifiedName~ReadingViewTests --logger "console;verbosity=detailed" --logger "trx;LogFileName=wpf-reading-view-final.trx" --results-directory C:/Users/cory/AppData/Local/Temp/slate-windows-ci-2026-09-30/wpf
```

Native WPF runs used approved desktop/COM access; the shared solution build used `dotnet build SlateWindows.slnx -c Release --no-restore`. The resulting [TRX](C:/Users/cory/AppData/Local/Temp/slate-windows-ci-2026-09-30/wpf/wpf-reading-view-final.trx) is local temporary evidence. Root coordinates the full app suite, model census, and other shared checks independently; their completion must not be inferred from this class result.

This is an in-process provider validation. No human NVDA/JAWS speech acceptance or hosted-runner cold/warm benchmark was conducted by this subtask. The private WPF adaptor compatibility seam remains pinned by actual WPF facts and the missing-pointer fallback witness. The Mac handoff should retain these Windows measurements and arrange hosted-runner and actual assistive-technology verification alongside its native Mac work.

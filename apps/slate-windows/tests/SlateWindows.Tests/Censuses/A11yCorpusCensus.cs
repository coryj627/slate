// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-D parity census (W0.5-3, #719; #1114): the Windows half of the
// canonical a11y-event corpus check — the twin of the mac
// A11yCorpusCensusTests. Constructs every representative event from
// `slate_core::a11y::corpus()` in the SAME order, renders each through
// the FFI (`A11yRender` — the exact path the host's announcer speaks
// through), and asserts (identity, priority, text) against the
// committed artifact `tests/fixtures/a11y/corpus.json`. Each entry's
// `event` field (core's Debug identity, via `A11yEventIdentity`) is
// asserted too, so the mirror must construct the SAME semantic event —
// not merely one that happens to render identical text. With this and
// its mac twin green, both hosts speak identical announcements for
// identical events: the corpus is the cross-platform anchor.
//
// Corpus changes are DELIBERATE: regenerate the artifact core-side
// (`SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y` — that
// run fails by design after rewriting; re-run clean to prove the pin),
// update the mirrored construction below AND the mac mirror, and
// review the diff as a §W-D delta.

using System.Text.Json;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "a11y-corpus")]
public sealed class A11yCorpusCensus
{
    private static string RepoRoot
    {
        get
        {
            // tests/.../bin/<cfg>/net10.0-windows -> repo root is eight
            // levels above (the MutationHarnessCensus shape).
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                dir = Path.GetDirectoryName(dir)!;
            }
            return dir;
        }
    }

    private static string CorpusPath =>
        Path.Combine(RepoRoot, "tests", "fixtures", "a11y", "corpus.json");

    private sealed record CorpusEntry(string Event, string Priority, string Text);

    /// <summary>The C# mirror of <c>slate_core::a11y::corpus()</c> —
    /// same events, same sample values, same order (transliterated
    /// from the mac mirror, which is the hand-maintained twin).</summary>
    private static readonly A11yEvent[] Corpus =
    [
        new A11yEvent.FilesRegionFocused(),
        new A11yEvent.LeafPanelShown(Title: "Outline"),
        new A11yEvent.EditorPaneFocused(Ordinal: 2, Total: 3, Title: "notes.md", Prefix: ""),
        new A11yEvent.TabFocused(Prefix: "Now", Filename: "notes.md", Index: 1, Count: 4),
        new A11yEvent.TabClosed(ClosedTitle: "draft.md", Successor: "notes.md"),
        new A11yEvent.TabClosed(ClosedTitle: "draft.md", Successor: null),
        new A11yEvent.NoSplitPanesToResize(),
        new A11yEvent.PaneResized(Percent: 60),
        new A11yEvent.GraphOpensSinglePane(),
        new A11yEvent.RightPaneShown(),
        new A11yEvent.RightPaneHidden(),
        new A11yEvent.HistoryPanelShown(),
        new A11yEvent.ReopenTargetMissing(Filename: "gone.md"),
        new A11yEvent.ReopenedFile(Filename: "notes.md"),
        new A11yEvent.ReopenedNamed(Name: "Open tasks"),
        new A11yEvent.ReopenedGraph(),
        new A11yEvent.VaultOpened(VaultTitle: "Garden", SidebarNotice: ""),
        new A11yEvent.RemovedRecentVault(DisplayName: "Garden"),
        new A11yEvent.WelcomeShown(RecentVaultCount: 0),
        new A11yEvent.WelcomeShown(RecentVaultCount: 1),
        new A11yEvent.WelcomeShown(RecentVaultCount: 2),
        new A11yEvent.CommandPaletteNeedsVault(),
        new A11yEvent.SearchNeedsVault(),
        new A11yEvent.SearchResultsSummary(Count: 0),
        new A11yEvent.SearchResultsSummary(Count: 1),
        new A11yEvent.SearchResultsSummary(Count: 7),
        new A11yEvent.SearchFailed(Message: "the index is unavailable"),
        new A11yEvent.SearchResultOpened(Filename: "notes.md", Line: 12, Snippet: "the quick brown fox"),
        new A11yEvent.ExternalLinkUnsupported(Target: "ftp://example.com"),
        new A11yEvent.ExternalLinkOpened(),
        new A11yEvent.ExternalLinkFailed(Target: "https://example.com"),
        new A11yEvent.LinkUnresolved(Target: "Missing Note"),
        new A11yEvent.HelpOpened(),
        new A11yEvent.HelpFailed(),
        new A11yEvent.InternalNavigated(Kind: "Opened", Filename: "notes.md"),
        new A11yEvent.CitationNotLoaded(),
        new A11yEvent.NoResolvedEmbedAtCursor(),
        new A11yEvent.NoEmbedAtCursor(),
        new A11yEvent.HeadingNotFound(),
        new A11yEvent.HeadingScrollFailed(Heading: "Roadmap"),
        new A11yEvent.ScrolledToHeading(Heading: "Roadmap"),
        new A11yEvent.ScrolledToLine(Filename: "notes.md", Line: 40),
        new A11yEvent.OpenedAtLine(Filename: "notes.md", Line: 40),
        new A11yEvent.OpenedFile(Filename: "notes.md"),
        new A11yEvent.ShowingNote(DisplayName: "notes"),
        new A11yEvent.TaskToggleUnsaved(Filename: "notes.md"),
        new A11yEvent.TaskToggleConflict(Filename: "notes.md"),
        new A11yEvent.TasksReviewShown(FilterName: "Open tasks"),
        new A11yEvent.TasksFilterSet(FilterName: "All tasks"),
        new A11yEvent.NoteSaved(Filename: "notes.md"),
        new A11yEvent.SaveConflict(Filename: "notes.md"),
        new A11yEvent.RestoredVersionFrom(FormattedDate: "July 19, 2026 at 9:41 AM"),
        new A11yEvent.RestoredFile(Filename: "notes.md"),
        new A11yEvent.RestoredFileAs(SourceName: "notes.md", Filename: "notes-restored.md"),
        new A11yEvent.PrintNeedsNote(),
        new A11yEvent.PrintDialogOpened(Name: "notes.md"),
        new A11yEvent.BatchCheckStarted(FormattedCount: "1,024", ActionName: "Move"),
        new A11yEvent.SelectionCopied(),
        new A11yEvent.SidebarSettingsStillDefaults(Detail: "the file is malformed."),
        new A11yEvent.SidebarSettingsReloadedStaleRefs(),
        new A11yEvent.SidebarSettingsReloaded(),
        new A11yEvent.VaultClosed(),
        new A11yEvent.VaultClosedAllSaved(),
        new A11yEvent.VaultClosedChangesDiscarded(),
        new A11yEvent.PropertiesUpdated(),
        new A11yEvent.PropertyChanged(Key: "tags", Deleted: false),
        new A11yEvent.PropertyChanged(Key: "tags", Deleted: true),
        new A11yEvent.PropertyEditConflict(Filename: "notes.md"),
        new A11yEvent.PropertiesSourceRejected(Reason: "the YAML does not parse"),
        new A11yEvent.PropertyEditFailed(Detail: "io error"),
        new A11yEvent.PropertiesReloaded(),
        new A11yEvent.PropertiesReloadedBodyChanged(),
        new A11yEvent.NoteChangedAgain(Detail: null),
        new A11yEvent.NoteChangedAgain(Detail: "The note changed while saving."),
        new A11yEvent.PropertiesReloadFailed(Reason: "io error"),
        new A11yEvent.PropertyRetainedCopied(),
        new A11yEvent.PropertyRecoveryUnverified(DisplayName: "notes"),
        new A11yEvent.PropertyRetainedDiscarded(),
        new A11yEvent.PropertyRetainedReapplyFailed(Detail: null),
        new A11yEvent.PropertyReloadStillFailed(Reason: "io error"),
        new A11yEvent.PropertyLoadCurrentFailed(Reason: "io error"),
        new A11yEvent.AddPropertySheetShown(),
        new A11yEvent.SourceChangesDiscarded(),
        new A11yEvent.BulkRenameSheetShown(),
        new A11yEvent.RenameReloadFailed(Detail: null),
        new A11yEvent.RenameFailed(Detail: "io error"),
        new A11yEvent.RenameSummary(Applied: true, Renamed: 3, Skipped: 1, Failed: 0),
        new A11yEvent.RenameSummary(Applied: false, Renamed: 1, Skipped: 0, Failed: 0),
        new A11yEvent.RenameSummary(Applied: false, Renamed: 3, Skipped: 2, Failed: 0),
        new A11yEvent.DuplicateFilesOnly(),
        new A11yEvent.MathSpeechStyle(Name: "ClearSpeak"),
        new A11yEvent.MathVerbosity(Name: "Verbose"),
        new A11yEvent.MathBrailleCode(Name: "Nemeth"),
        new A11yEvent.CodePreambleVerbosity(Name: "Concise"),
        new A11yEvent.EditorTextSize(Percent: 110),
        new A11yEvent.SpellCheckToggled(Enabled: true),
        new A11yEvent.SpellCheckToggled(Enabled: false),
        new A11yEvent.CitationStyleChanged(Title: "APA"),
        new A11yEvent.CitationsCount(Count: 1),
        new A11yEvent.CitationsCount(Count: 3),
        new A11yEvent.OutlineCount(Count: 1),
        new A11yEvent.OutlineCount(Count: 5),
        new A11yEvent.FileListCount(Count: 1),
        new A11yEvent.FileListCount(Count: 12),
        new A11yEvent.ItemsSelected(Count: 4),
        new A11yEvent.ItemsSelected(Count: 1),
        new A11yEvent.NoItemsSelected(),
        new A11yEvent.TreeFolderSelected(Name: "Archive"),
        new A11yEvent.RowSelected(Name: "notes"),
        new A11yEvent.SwitcherRecentCount(Count: 2),
        new A11yEvent.SwitcherRecentCount(Count: 1),
        new A11yEvent.SwitcherRecentCount(Count: 0),
        new A11yEvent.SwitcherNoMatches(Query: "zzz"),
        new A11yEvent.SwitcherMatchCount(Count: 2, Query: "foo"),
        new A11yEvent.SwitcherMatchCount(Count: 1, Query: "foo"),
        new A11yEvent.PaletteCommandSelected(Label: "Save", DisabledReason: null),
        new A11yEvent.PaletteCommandSelected(Label: "Save", DisabledReason: "A structural operation is in progress."),
        new A11yEvent.PaletteFilterCount(Count: 0, Query: "zzz"),
        new A11yEvent.PaletteFilterCount(Count: 1, Query: "save"),
        new A11yEvent.PaletteFilterCount(Count: 4, Query: "e"),
        new A11yEvent.PaletteCommandFailed(Label: "Save", Detail: "disk full"),
        new A11yEvent.PaletteCommandFailed(Label: "Save", Detail: null),
        new A11yEvent.PaletteCommandNotFound(Id: "slate.nope"),
        new A11yEvent.PaletteCommandUnavailable(Reason: "A structural operation is in progress."),
        new A11yEvent.RecentSearchFocused(Query: "fox"),
        new A11yEvent.QuickSwitcherCount(Count: 2, Query: null),
        new A11yEvent.QuickSwitcherCount(Count: 1, Query: null),
        new A11yEvent.QuickSwitcherCount(Count: 2, Query: "foo"),
        new A11yEvent.QuickSwitcherCount(Count: 1, Query: "foo"),
        new A11yEvent.QuickSwitcherCount(Count: 0, Query: "zzz"),
        new A11yEvent.BaseViewMode(Mode: "cards"),
        new A11yEvent.BaseViewSwitcher(ViewCount: 1),
        new A11yEvent.BaseViewSwitcher(ViewCount: 2),
        new A11yEvent.BasesNewQueryBuilder(),
        new A11yEvent.BasesEditingFilters(ViewName: "Table"),
        new A11yEvent.BasesFiltersOpenFailed(Detail: "io error"),
        new A11yEvent.BasesPreviewFailed(Detail: "bad expression"),
        new A11yEvent.BasesBuilderSaved(),
        new A11yEvent.BasesViewSaveFailed(Detail: "io error"),
        new A11yEvent.BasesSavedQueryNameNeeded(),
        new A11yEvent.BasesSavedQueryCreated(Name: "Open tasks"),
        new A11yEvent.BasesSavedQueryCreateFailed(Detail: "io error"),
        new A11yEvent.BasesSavedQueryUpdated(Name: "Open tasks"),
        new A11yEvent.BasesSavedQueryUpdateFailed(Detail: "io error"),
        new A11yEvent.BasesViewSelected(Name: "Cards"),
        new A11yEvent.BasesSortSaveFailed(Detail: "io error"),
        new A11yEvent.BaseRefreshed(),
        new A11yEvent.BaseWhereAmI(Base: "Reading", View: null, QuickFilter: null),
        new A11yEvent.BaseWhereAmI(Base: "Reading", View: "Table", QuickFilter: null),
        new A11yEvent.BaseWhereAmI(Base: "Reading", View: "Table", QuickFilter: "CAFE"),
        new A11yEvent.BaseResultsPopover(AudioSummary: "12 results.", WhereAmI: null),
        new A11yEvent.BaseResultsPopover(AudioSummary: "12 results.", WhereAmI: "Base: Reading, quick filter: CAFE"),
        new A11yEvent.BaseQuickFilterResult(Shown: 0, Total: 0),
        new A11yEvent.BaseQuickFilterResult(Shown: 1, Total: 1),
        new A11yEvent.BaseQuickFilterResult(Shown: 1, Total: 2),
        new A11yEvent.BaseRowReorderRefused(Label: "Sort 1"),
        new A11yEvent.BaseRowReorderAtBoundary(Label: "Sort 1", AtFirst: true),
        new A11yEvent.BaseRowReorderAtBoundary(Label: "Sort 2", AtFirst: false),
        new A11yEvent.BaseRowReorderMoved(Label: "Sort 1", MovedUp: false, Position: 2, Count: 3),
        new A11yEvent.BaseRowReorderMoved(Label: "Status column", MovedUp: true, Position: 1, Count: 3),
        new A11yEvent.BaseQueryPreviewIdle(),
        new A11yEvent.BaseQueryPreviewLoading(),
        new A11yEvent.BaseQueryPreviewReady(AudioSummary: "12 results.", FirstResult: null),
        new A11yEvent.BaseQueryPreviewReady(AudioSummary: "12 results", FirstResult: "Alpha"),
        new A11yEvent.BaseQueryPreviewReady(AudioSummary: "12 results.", FirstResult: "Alpha"),
        new A11yEvent.BaseQueryPreviewFailed(Detail: "invalid expression"),
        new A11yEvent.BaseSortedByColumn(Column: "Status", Ascending: true),
        new A11yEvent.BaseSortedByColumn(Column: "Status", Ascending: false),
        new A11yEvent.BaseSortSavedToView(Column: "Status", Ascending: true),
        new A11yEvent.BaseSortSavedToView(Column: "Status", Ascending: false),
        new A11yEvent.BasesSavedQueryReferenceMissing(Reference: "Open tasks"),
        new A11yEvent.BasesSavedQueryMissing(),
        new A11yEvent.BasesQueriesRefreshFailed(Detail: "io error"),
        new A11yEvent.BasesSavedQueryEditing(Name: "Open tasks"),
        new A11yEvent.BasesSavedQueryEditFailed(Detail: "io error"),
        new A11yEvent.BasesSavedQueryRenameNameNeeded(),
        new A11yEvent.BasesSavedQueryRenamed(Name: "Open tasks"),
        new A11yEvent.BasesSavedQueryRenameFailed(Detail: "io error"),
        new A11yEvent.BasesSavedQueryDeleted(),
        new A11yEvent.BasesSavedQueryDeleteFailed(Detail: "io error"),
        new A11yEvent.BasesSavedQueryExportPathNeeded(),
        new A11yEvent.BasesSavedQueryExported(Name: "Open tasks.base"),
        new A11yEvent.BasesSavedQueryExportFailed(Detail: "io error"),
        new A11yEvent.BasesPathOutsideVault(),
        new A11yEvent.BasesDashboardNameNeeded(),
        new A11yEvent.BasesDashboardSaved(Name: "Reading"),
        new A11yEvent.BasesDashboardSaveFailed(Detail: "io error"),
        new A11yEvent.BasesDashboardUpdated(Name: "Reading"),
        new A11yEvent.BasesDashboardUpdateFailed(Detail: "io error"),
        new A11yEvent.BasesDashboardSectionStale(),
        new A11yEvent.BasesDashboardSectionRemoveFailed(Detail: "io error"),
        new A11yEvent.BasesDashboardSectionReplaceFailed(Detail: "io error"),
        new A11yEvent.BasesDashboardDeleted(),
        new A11yEvent.BasesDashboardDeleteFailed(Detail: "io error"),
        new A11yEvent.BasesDashboardEditFailed(Detail: "io error"),
        new A11yEvent.BasesDashboardMissing(),
        new A11yEvent.BasesDockUpdatedForNote(),
        new A11yEvent.BasesLinkCopied(Name: "Reading"),
        new A11yEvent.BasesBacklinksFor(Name: "Reading"),
        new A11yEvent.BasesViewCopyNoActiveBase(),
        new A11yEvent.BasesViewCopiedAsMarkdown(),
        new A11yEvent.BasesViewCopyFailed(Detail: "io error"),
        new A11yEvent.BasesRowSelectionNeeded(),
        new A11yEvent.BasesNoEditableProperty(),
        new A11yEvent.BasesCellReadOnly(FileMetadata: true),
        new A11yEvent.BasesCellReadOnly(FileMetadata: false),
        new A11yEvent.BasesCellSaved(Column: "Status", Value: "Done"),
        new A11yEvent.BasesCellCleared(Column: "Status"),
        new A11yEvent.BasesCellRowNoLongerMatches(),
        new A11yEvent.BasesCellEditFailed(Detail: "io error"),
        new A11yEvent.BasesCellEditCanceled(),
        new A11yEvent.BasesViewExported(),
        new A11yEvent.BasesViewExportFailed(Detail: "io error"),
        new A11yEvent.BasesDataviewConverted(),
        new A11yEvent.BasesDataviewConversionSaveFailed(Detail: "io error"),
        new A11yEvent.BasesQuickFilterChoiceCanceled(Verb: "Export"),
        new A11yEvent.BasesCellMustBeFiniteNumber(),
        new A11yEvent.BasesCellMustBeWholeNumber(),
        new A11yEvent.BasesCellMustBeFiniteDecimal(),
        new A11yEvent.BasesCellMustBeBoolean(),
        new A11yEvent.BasesCellMustBeDate(),
        new A11yEvent.BasesRefreshUpdated(AudioSummary: "1 note."),
        new A11yEvent.DataviewConversionFailed(Detail: "unsupported query"),
        new A11yEvent.CitationInsertUnavailable(),
        new A11yEvent.CitationWalkThrough(),
        new A11yEvent.CodeCopied(),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.Heading(), Forward: true),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.HeadingLevel(Level: 2), Forward: false),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.Link(), Forward: true),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.List(), Forward: false),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.Table(), Forward: true),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.Embed(), Forward: false),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.CodeBlock(), Forward: true),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.HeadingLevel(Level: 2), Text: "Lists and tasks"),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.Link(), Text: "Target Note"),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.List(), Text: "first bullet"),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.Table(), Text: "column a"),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.Embed(), Text: "Embedded note Target Note"),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.CodeBlock(), Text: "fn spoken_interior() -> usize { 42 }"),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.Math(), Forward: true),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.Math(), Text: "x equals negative b plus or minus the square root of b squared minus 4 a c, over 2 a"),
        new A11yEvent.ReadingNavNoTarget(Target: new ReadingNavTarget.Diagram(), Forward: true),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.Diagram(), Text: "Flowchart with 3 steps"),
        new A11yEvent.ReadingNavLanded(Target: new ReadingNavTarget.Embed(), Text: ""),
        new A11yEvent.GridSorted(Column: "Status", Ascending: true),
        new A11yEvent.GridSorted(Column: "Due", Ascending: false),
        new A11yEvent.GridRowMoved(Description: "Ship the plan. Status: Open. Due: Friday", FocusedCell: "Status: Open"),
        new A11yEvent.GridRowMoved(Description: "Ship the plan", FocusedCell: "Status: Open"),
        new A11yEvent.GridRowMoved(Description: "Done reviewing.", FocusedCell: "Status: Open"),
        new A11yEvent.GridRowMoved(Description: "Substatus: Opening", FocusedCell: "Status: Open"),
        new A11yEvent.GridRowMoved(Description: "Status: Open Questions remain", FocusedCell: "Status: Open"),
        new A11yEvent.GridCellMoved(Column: "Status", Value: "Open"),
        new A11yEvent.GridGroup(Label: "Open", RowCount: 1, Summary: null),
        new A11yEvent.GridGroup(Label: "Done", RowCount: 12, Summary: "Count: 12"),
        new A11yEvent.TemplatePickerOpened(Count: 0),
        new A11yEvent.TemplatePickerOpened(Count: 1),
        new A11yEvent.TemplatePickerOpened(Count: 7),
        new A11yEvent.TemplateNoteCreated(Name: "Meeting 2026-08-20.md", Template: "Meeting"),
        new A11yEvent.HostComposed(Text: "Composed by a host engine.", Priority: A11yPriority.High),
    ];

    [Fact]
    public void EveryCorpusEventRendersTheCommittedIdentityTextAndPriority()
    {
        Assert.True(File.Exists(CorpusPath), $"corpus artifact missing at {CorpusPath}");
        List<CorpusEntry> entries = JsonSerializer.Deserialize<List<CorpusEntry>>(
            File.ReadAllText(CorpusPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.True(
            Corpus.Length == entries.Count,
            $"the C# corpus mirror ({Corpus.Length}) and the committed artifact "
            + $"({entries.Count}) must stay in lockstep");

        for (int index = 0; index < entries.Count; index++)
        {
            A11yEvent @event = Corpus[index];
            CorpusEntry entry = entries[index];
            Assert.True(
                string.Equals(
                    SlateUniffiMethods.A11yEventIdentity(@event), entry.Event,
                    StringComparison.Ordinal),
                $"event identity mismatch at corpus[{index}]: the mirror constructed "
                + $"{SlateUniffiMethods.A11yEventIdentity(@event)} but the artifact pins "
                + $"{entry.Event}");
            RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(@event);
            Assert.True(
                string.Equals(rendered.Text, entry.Text, StringComparison.Ordinal),
                $"text mismatch at corpus[{index}] ({entry.Event}): rendered "
                + $"\"{rendered.Text}\", artifact \"{entry.Text}\"");
            string priority = rendered.Priority == A11yPriority.High ? "high" : "medium";
            Assert.True(
                string.Equals(priority, entry.Priority, StringComparison.Ordinal),
                $"priority mismatch at corpus[{index}] ({entry.Event}): rendered "
                + $"{priority}, artifact {entry.Priority}");
        }
    }

    /// <summary>The mirror is a census, not a sample: every entry is a
    /// distinct semantic event, so a copy-paste that repeats one would
    /// silently shadow the entry it displaced.</summary>
    [Fact]
    public void TheMirrorHasNoDuplicateEntries()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (A11yEvent @event in Corpus)
        {
            string identity = SlateUniffiMethods.A11yEventIdentity(@event);
            Assert.True(seen.Add(identity), $"duplicate corpus entry: {identity}");
        }
    }
}

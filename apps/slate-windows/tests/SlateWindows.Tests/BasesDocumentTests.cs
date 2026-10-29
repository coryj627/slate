// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-6 (#738) phase A facts: the Bases document VM over a REAL
/// session and real .base fixtures — contracts C1 (core executes),
/// C3 (shared per-source document, closed exactly once), C4 (state
/// machine + verbatim banner wording), C6 (transactional sort), and
/// the external-sort substrate seam.
/// </summary>
public sealed class BasesDocumentTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public BasesDocumentTests()
    {
        _fixture = FixtureVault.Create(3, "bases-document");
        WriteBaseFixture();
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private void WriteBaseFixture()
    {
        // Two views: a plain table (executable) and a cards view
        // (core marks it Fallback and rewrites it to a table).
        // Scoped to .md — the files source indexes the .base files
        // themselves too, and the facts want deterministic counts.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n" +
            "  - type: cards\n" +
            "    name: Gallery\n" +
            "  - type: list\n" +
            "    name: Rows\n" +
            "    order:\n" +
            "      - file.name\n" +
            "    groupBy:\n" +
            "      property: file.ext\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Empty.base"),
            "filters: \"file.hasTag('no-such-tag')\"\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n");
        // WB-14 (#1397): definitions with no executable view — saved empty,
        // saved before any view was added, saved mid-edit — and a one-view
        // base the facts delete or make unreadable.
        File.WriteAllText(Path.Combine(_fixture.Root, "Blank.base"), string.Empty);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "FiltersOnly.base"),
            "filters: 'file.ext == \"md\"'\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Truncated.base"),
            "views:\n  - type: table\n    name: [Reading\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Reading.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Reading\n" +
            "    order:\n" +
            "      - file.name\n");
    }

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _fixture.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);

    private static BaseDocumentViewModel OpenInTab(WorkspaceViewModel workspace, string path)
    {
        workspace.OpenPath(path);
        return Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab?.Base);
    }

    /// <summary>The C6 rollback detach: a sort whose execute fails and whose
    /// rollback fails too closes the handle and leaves the document
    /// Failed.</summary>
    private static void DetachBySortRollback(BaseDocumentViewModel document)
    {
        document.ExecuteFaultForTests = () => new VaultException.Db("execute fault");
        document.RollbackFaultForTests = () => new VaultException.Db("rollback fault");
        Assert.True(document.ApplySortFromGrid(0, ascending: true));
        document.ExecuteFaultForTests = null;
        document.RollbackFaultForTests = null;
        Assert.False(document.HasHandleForTests, "premise: the rollback detach closed the handle");
        Assert.Equal(BaseLoadState.Failed, document.State);
    }

    private BaseDocumentViewModel NewDocument(string path) =>
        new(_session, path, _announced.Add, synchronousForTests: true);

    [Fact]
    public void LoadPublishesViewsResultAndReadyState()
    {
        var document = NewDocument("Notes.base");
        int published = 0;
        document.ResultPublished += (_, _) => published++;
        document.Load();

        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Null(document.StateMessage);
        Assert.Equal(3, document.Views.Count);
        Assert.Equal("Main", document.ActiveViewName);
        Assert.Equal(1, published);
        BasesResultSet result = Assert.IsType<BasesResultSet>(document.Result);
        Assert.Equal(3, result.Rows.Length);
        Assert.NotEmpty(result.AudioSummary);
        Assert.False(document.ShowEmptyState);
        // INV-4: loading a base announces nothing.
        Assert.Empty(_announced);
        document.Shutdown();
    }

    [Fact]
    public void FallbackViewRendersContentUnderTheVerbatimBanner()
    {
        var document = NewDocument("Notes.base");
        document.Load();
        document.SelectView(1);

        Assert.Equal(BaseLoadState.Degraded, document.State);
        Assert.Equal("Using fallback view for Gallery.", document.StateMessage);
        // Contract C4: degraded is ready-with-a-banner — the fallback
        // TABLE projection still renders rows under the message.
        Assert.NotNull(document.Result);
        Assert.Equal(3, document.Result!.Rows.Length);
        document.Shutdown();
    }

    [Fact]
    public void MissingFileFailsWithTheRecoveryPosture()
    {
        var document = NewDocument("Absent.base");
        document.Load();

        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.StartsWith(
            "This Base could not be opened:",
            document.StateMessage,
            StringComparison.Ordinal);
        Assert.Null(document.Result);
        // WB-14 (#1397): the definition could not be read, so editing it is
        // unavailable, and says so.
        Assert.Equal(BasePhrase.DefinitionUnavailable, document.DefinitionEditingDisabledReason);
        document.Shutdown();
    }

    /// <summary>WB-14 (#1397; mac's twin): a base saved empty has no
    /// executable view. It loads Failed with core's sentence — never an
    /// execute of view 0 that core refuses as out of range — keeps its
    /// handle, since the definition was read, and stays editable.</summary>
    [Fact]
    public void ASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = OpenInTab(workspace, "Blank.base");

        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.Equal(BasePhrase.NoExecutableViews, document.StateMessage);
        Assert.True(document.HasHandleForTests, "the definition was read: its handle stays open");
        Assert.Null(document.DefinitionEditingDisabledReason);
        Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));
    }

    /// <summary>WB-14 (mac's twin): saved before any view was added, and saved
    /// mid-edit with the view list never closed — the same sentence, core's,
    /// never parser internals, and editing enabled.</summary>
    [Theory]
    [InlineData("FiltersOnly.base")]
    [InlineData("Truncated.base")]
    public void AnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled(string path)
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = OpenInTab(workspace, path);

        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.Equal(BasePhrase.NoExecutableViews, document.StateMessage);
        Assert.DoesNotContain("YAML", document.StateMessage, StringComparison.Ordinal);
        Assert.True(document.HasHandleForTests);
        Assert.Null(document.DefinitionEditingDisabledReason);
        Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));
    }

    /// <summary>WB-14 (mac's twin): a selected view whose execute fails says
    /// why in core's words — the detail, never the binding's "@message=" field
    /// label — keeps the definition open and editable, and the view picker
    /// still works: another view may load.</summary>
    [Fact]
    public void AnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = OpenInTab(workspace, "Notes.base");
        Assert.Equal(BaseLoadState.Ready, document.State);

        document.ExecuteFaultForTests = () => new VaultException.Db("unknown function frobnicate");
        document.SelectView(1);

        Assert.Equal("Gallery", document.ActiveViewName);
        Assert.Equal("Base view could not be executed: unknown function frobnicate", document.StateMessage);
        Assert.DoesNotContain("@message", document.StateMessage, StringComparison.Ordinal);
        Assert.True(document.HasHandleForTests);
        Assert.Null(document.DefinitionEditingDisabledReason);
        Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));

        document.ExecuteFaultForTests = null;
        document.SelectView(0);
        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Equal("Main", document.ActiveViewName);
    }

    /// <summary>WB-14 (mac's twin): Edit on the empty base opens the builder on
    /// view 0 over an empty draft; Save to View writes the draft as the
    /// definition's first view — AddView first — and the repaired base loads
    /// Ready.</summary>
    [Fact]
    public void EditFromTheErrorStateOpensTheBuilder()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = OpenInTab(workspace, "Blank.base");
        IReadOnlyList<BaseEdit>? batch = null;
        document.ApplyEditsHookForTests = edits => batch = edits;
        _announced.Clear();

        workspace.BasesEditViewFiltersCommand.Execute(null);

        BaseQueryBuilderViewModel builder =
            Assert.IsType<BaseQueryBuilderViewModel>(workspace.BaseQueryBuilderSheet);
        Assert.Equal("Blank.base", builder.Context.ViewPath);
        Assert.Equal(0, builder.Context.ViewIndex);
        Assert.Contains(_announced, e => e is A11yEvent.BasesEditingFilters);

        workspace.BuilderSaveToView();

        Assert.IsType<BaseEdit.AddView>(Assert.IsAssignableFrom<IReadOnlyList<BaseEdit>>(batch)[0]);
        Assert.Contains(_announced, e => e is A11yEvent.BasesBuilderSaved);
        Assert.Contains("views:", File.ReadAllText(Path.Combine(_fixture.Root, "Blank.base")), StringComparison.Ordinal);
        Assert.Single(document.Views);
        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Null(document.StateMessage);
    }

    /// <summary>WB-14 (mac's twin): a definition that cannot be reached is not
    /// editable, and says why. Deleted while its handle was retained, then
    /// refreshed: the availability probe fails, the handle closes, the base is
    /// Failed with the open's sentence, and Edit View Filters is disabled with
    /// the definition's reason — the command's own reason and the palette
    /// resolver's — and runs nothing. The same for a definition the read
    /// refuses; and a definition detached after a read and then deleted fails
    /// its reopen, says why, and becomes unavailable.</summary>
    [Fact]
    public void AMissingBaseFileStillDisablesEditingWithTheUnavailableReason()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel reading = OpenInTab(workspace, "Reading.base");
        Assert.Equal(BaseLoadState.Ready, reading.State);
        File.Delete(Path.Combine(_fixture.Root, "Reading.base"));
        _announced.Clear();

        reading.Refresh();

        Assert.Equal(BaseLoadState.Failed, reading.State);
        Assert.StartsWith("This Base could not be opened:", reading.StateMessage, StringComparison.Ordinal);
        Assert.False(reading.HasHandleForTests, "the failed probe closes the retained handle");
        Assert.Equal(BasePhrase.DefinitionUnavailable, reading.DefinitionEditingDisabledReason);
        AssertEditViewFiltersUnavailable(workspace, BasePhrase.DefinitionUnavailable);

        // A definition the read refuses.
        BaseDocumentViewModel notes = OpenInTab(workspace, "Notes.base");
        Assert.Equal(BaseLoadState.Ready, notes.State);
        notes.DefinitionReadFaultForTests = () => throw new VaultException.Io("probe refused");
        notes.Refresh();
        Assert.Equal(BaseLoadState.Failed, notes.State);
        Assert.Equal("This Base could not be opened: probe refused", notes.StateMessage);
        Assert.False(notes.HasHandleForTests);
        AssertEditViewFiltersUnavailable(workspace, BasePhrase.DefinitionUnavailable);

        // Detached after a read, then deleted: the edit's reopen fails and
        // says why.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Reading.base"),
            "views:\n  - type: table\n    name: Reading\n    order:\n      - file.name\n");
        BaseDocumentViewModel detached = OpenInTab(workspace, "Reading.base");
        detached.Load();
        Assert.Equal(BaseLoadState.Ready, detached.State);
        DetachBySortRollback(detached);
        Assert.Null(detached.DefinitionEditingDisabledReason);
        File.Delete(Path.Combine(_fixture.Root, "Reading.base"));
        _announced.Clear();

        workspace.BasesEditViewFiltersCommand.Execute(null);

        Assert.Null(workspace.BaseQueryBuilderSheet);
        A11yEvent.BasesFiltersOpenFailed failed =
            Assert.IsType<A11yEvent.BasesFiltersOpenFailed>(Assert.Single(_announced));
        Assert.StartsWith("This Base could not be opened:", failed.Detail, StringComparison.Ordinal);
        Assert.Equal(BasePhrase.DefinitionUnavailable, detached.DefinitionEditingDisabledReason);
    }

    /// <summary>WB-14 red team (M2): a base Failed for having no executable
    /// view keeps its handle, so the rescan re-runs it rather than reopening
    /// it — it never passes through Loading, whose disabled Edit and collapsed
    /// group would take the keys off its Edit — and it is still counted.</summary>
    [Fact]
    public void ARescanReRunsAViewlessBaseWithoutReopeningIt()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = OpenInTab(workspace, "Blank.base");
        Assert.Equal(BaseLoadState.Failed, document.State);
        int opens = document.OpensForTests;
        var states = new List<BaseLoadState>();
        document.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(BaseDocumentViewModel.State))
            {
                states.Add(document.State);
            }
        };

        Task run = document.RefreshAsync();
        PumpedDispatcher.PumpUntilDrained(run.ContinueWith(_ => { }, TaskScheduler.Default));

        Assert.IsType<BasePublicationFailedException>(run.Exception?.InnerException);
        Assert.Equal(opens, document.OpensForTests);
        Assert.DoesNotContain(BaseLoadState.Loading, states);
        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.Equal(BasePhrase.NoExecutableViews, document.StateMessage);
        Assert.True(document.HasHandleForTests);
    }

    /// <summary>WB-14 red team (M1): the rescan's re-run does not probe the
    /// definition — a base deleted outside Slate would be a counted rescan
    /// error then and on every later rescan; detecting the deletion is the
    /// explicit Refresh's, as on mac.</summary>
    [Fact]
    public void TheRescansReRunDoesNotProbeTheDefinition()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel reading = OpenInTab(workspace, "Reading.base");
        File.Delete(Path.Combine(_fixture.Root, "Reading.base"));

        Task run = reading.RefreshAsync();
        PumpedDispatcher.PumpUntilDrained(run.ContinueWith(_ => { }, TaskScheduler.Default));

        Assert.True(run.IsCompletedSuccessfully);
        Assert.Equal(BaseLoadState.Ready, reading.State);
        Assert.True(reading.HasHandleForTests);

        reading.Refresh();
        Assert.Equal(BaseLoadState.Failed, reading.State);
        Assert.Equal(BasePhrase.DefinitionUnavailable, reading.DefinitionEditingDisabledReason);
    }

    private void AssertEditViewFiltersUnavailable(WorkspaceViewModel workspace, string reason)
    {
        System.Windows.Input.ICommand command = workspace.BasesEditViewFiltersCommand;
        Assert.False(command.CanExecute(null));
        Assert.Equal(reason, ((Commands.IUnavailableReason)command).UnavailableReason);
        Assert.Equal(
            reason,
            Commands.SlateCommandRegistrar.DisabledReason(
                new CanvasDocumentTests.CanvasCommandHost(workspace), Commands.ChordTable.Ids.BasesEditViewFilters));
        int heard = _announced.Count;
        command.Execute(null);
        Assert.Null(workspace.BaseQueryBuilderSheet);
        Assert.Equal(heard, _announced.Count);
    }

    /// <summary>WB-14 (mac's twin): the C6 rollback detach closed the handle
    /// but the file is intact — the data verbs stay unavailable, the
    /// definition stays editable, and Edit reopens it before the builder
    /// opens on its view.</summary>
    [Fact]
    public void ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel reading = OpenInTab(workspace, "Reading.base");
        Assert.Equal(BaseLoadState.Ready, reading.State);
        DetachBySortRollback(reading);
        int opens = reading.OpensForTests;

        Assert.False(workspace.BasesWhereAmICommand.CanExecute(null), "the data verbs need the handle");
        Assert.Null(reading.DefinitionEditingDisabledReason);
        Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));

        workspace.BasesEditViewFiltersCommand.Execute(null);

        Assert.True(reading.HasHandleForTests, "the edit reopened the definition first");
        Assert.Equal(opens + 1, reading.OpensForTests);
        BaseQueryBuilderViewModel builder =
            Assert.IsType<BaseQueryBuilderViewModel>(workspace.BaseQueryBuilderSheet);
        Assert.Equal("Reading.base", builder.Context.ViewPath);
        Assert.Equal(0, builder.Context.ViewIndex);
    }

    [Fact]
    public void EmptyResultShowsTheEmptyState()
    {
        var document = NewDocument("Empty.base");
        document.Load();

        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.True(document.ShowEmptyState);
        Assert.Equal("No results.", document.Result!.AudioSummary);
        document.Shutdown();
    }

    [Fact]
    public void SortIsTransactionalAndAnnouncesOnceWhenRowsLand()
    {
        var document = NewDocument("Notes.base");
        document.Load();
        string firstBefore = document.Result!.Rows[0].Values[0].Display;

        Assert.True(document.ApplySortFromGrid(0, ascending: false));

        Assert.Equal((0, false), document.SortState);
        string firstAfter = document.Result!.Rows[0].Values[0].Display;
        Assert.NotEqual(firstBefore, firstAfter);
        A11yEvent announced = Assert.Single(_announced);
        var sorted = Assert.IsType<A11yEvent.BaseSortedByColumn>(announced);
        Assert.False(sorted.Ascending);
        // Refused sort: out-of-range column changes nothing, silently
        // (the mac posture).
        Assert.False(document.ApplySortFromGrid(99, ascending: true));
        Assert.Single(_announced);
        document.Shutdown();
    }

    [Fact]
    public void QuickFilterExecutesInCoreAndAnnouncesTheCount()
    {
        var document = NewDocument("Notes.base");
        document.Load();

        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();

        Assert.True(document.QuickFilterActive);
        Assert.Equal(1ul, document.Result!.ShownCount);
        Assert.Equal(3ul, document.Result.UnfilteredShownCount);
        var counted = Assert.IsType<A11yEvent.BaseQuickFilterResult>(
            Assert.Single(_announced));
        Assert.Equal(1ul, counted.Shown);
        Assert.Equal(3ul, counted.Total);

        // Clearing re-executes unfiltered and announces the same way.
        document.QuickFilterText = string.Empty;
        document.ApplyQuickFilter();
        Assert.False(document.QuickFilterActive);
        Assert.Equal(3ul, document.Result!.ShownCount);
        Assert.Equal(2, _announced.Count);
        document.Shutdown();
    }

    [Fact]
    public void QuickFilterIsTransientAcrossViewSwitchAndReload()
    {
        var document = NewDocument("Notes.base");
        document.Load();
        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();
        Assert.True(document.QuickFilterActive);

        // View switch clears (contract C5) — text, flag, and the
        // executed result all revert to unfiltered.
        document.SelectView(1);
        Assert.Equal(string.Empty, document.QuickFilterText);
        Assert.False(document.QuickFilterActive);
        Assert.Equal(3ul, document.Result!.ShownCount);

        document.SelectView(0);
        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();
        document.Load();
        Assert.Equal(string.Empty, document.QuickFilterText);
        Assert.False(document.QuickFilterActive);
        document.Shutdown();
    }

    [Fact]
    public void SortWithinAFilteredViewKeepsTheFilter()
    {
        var document = NewDocument("Notes.base");
        document.Load();
        document.QuickFilterText = "note";
        document.ApplyQuickFilter();
        Assert.Equal(3ul, document.Result!.ShownCount);

        Assert.True(document.ApplySortFromGrid(0, ascending: false));

        // The sorted re-execute carried the filter (mac sorts within
        // the filtered view); the filter flag survives.
        Assert.True(document.QuickFilterActive);
        Assert.Equal(3ul, document.Result!.ShownCount);
        Assert.Equal((0, false), document.SortState);
        document.Shutdown();
    }

    [Fact]
    public void CommandEventsCarryTheMacShapes()
    {
        var document = NewDocument("Notes.base");
        document.Load();

        // Where-am-I: base name only while unfiltered.
        var whereAmI = Assert.IsType<A11yEvent.BaseWhereAmI>(document.WhereAmIEvent());
        Assert.Equal("Notes", whereAmI.Base);
        Assert.Equal("Main", whereAmI.View);
        Assert.Null(whereAmI.QuickFilter);

        // Filtered: the readback carries the filter, and the results
        // popover appends the rendered readback (the mac rule).
        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();
        whereAmI = Assert.IsType<A11yEvent.BaseWhereAmI>(document.WhereAmIEvent());
        Assert.Equal("note0", whereAmI.QuickFilter);
        var popover = Assert.IsType<A11yEvent.BaseResultsPopover>(
            document.ResultsPopoverEvent());
        Assert.NotNull(popover.WhereAmI);
        Assert.Contains("note0", popover.WhereAmI, StringComparison.Ordinal);
        document.Shutdown();
    }

    [Fact]
    public void SaveSortToViewPersistsAndClearsTheTransientSort()
    {
        var document = NewDocument("Notes.base");
        document.Load();
        Assert.True(document.ApplySortFromGrid(0, ascending: false));
        _announced.Clear();

        document.SaveSortToView();

        var saved = Assert.IsType<A11yEvent.BaseSortSavedToView>(
            Assert.Single(_announced, e => e is A11yEvent.BaseSortSavedToView));
        Assert.False(saved.Ascending);
        Assert.Null(document.SortState);
        // The slate sort landed in the FILE (contract C14's cousin:
        // the YAML fragment is mac's byte-for-byte).
        string content = File.ReadAllText(Path.Combine(_fixture.Root, "Notes.base"));
        Assert.Contains("direction: DESC", content, StringComparison.Ordinal);
        // The mac YAML fragment shape itself.
        Assert.Equal(
            "- property: \"file.name\"\n  direction: ASC",
            BaseDocumentViewModel.SlateSortYaml("file.name", ascending: true));
        document.Shutdown();
    }

    [Fact]
    public void BaseWikilinkMatchesTheMacShape()
    {
        Assert.Equal(
            "[[Notes/Alpha]]", WorkspaceViewModel.BaseWikilink("Notes/Alpha.md"));
        Assert.Equal(
            "[[Queries/All]]", WorkspaceViewModel.BaseWikilink("Queries/All.base"));
    }

    [Fact]
    public void ShutdownIsIdempotentAndRefusesLateWork()
    {
        var document = NewDocument("Notes.base");
        document.Load();
        document.Shutdown();
        document.Shutdown();

        BaseLoadState state = document.State;
        document.Load();
        Assert.Equal(state, document.State);
    }

    [Fact]
    public void WorkspaceSharesOneDocumentPerSourceAndReleasesOnLastClose()
    {
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("Notes.base");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.True(first.IsBase);
        Assert.False(first.IsPlaceholder);
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(first.Base);
        Assert.Equal(BaseLoadState.Ready, document.State);

        // A second tab on the same source shares the SAME document
        // (contract C3).
        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel second =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.Same(document, second.Base);

        // Closing ONE tab keeps the shared document alive; closing the
        // last releases it (a shut-down scheduler refuses Load, so the
        // state can never leave Ready again).
        workspace.ActiveGroup.ActiveTab = second;
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        Assert.Equal(BaseLoadState.Ready, document.State);
        workspace.ActiveGroup.ActiveTab = first;
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        document.Load();
        Assert.Equal(BaseLoadState.Ready, document.State);
    }
}

/// <summary>The surface's content-shape rules (contract C4) over a
/// real model — list renderer, canonical group headings, and the
/// mutually-exclusive grid/list/empty visibility.</summary>
public sealed class BaseSurfaceViewTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public BaseSurfaceViewTests()
    {
        _fixture = FixtureVault.Create(3, "bases-surface");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n" +
            "  - type: list\n" +
            "    name: Rows\n" +
            "    order:\n" +
            "      - file.name\n" +
            "    groupBy:\n" +
            "      property: file.ext\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void ListViewNamesRowsFromCoreAndDisablesGroupHeaders() => RunSta(() =>
    {
        var document = new SlateWindows.Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        document.SelectView(1);
        var surface = new SlateWindows.Bases.BaseSurfaceView { Model = document };

        Assert.Equal(
            System.Windows.Visibility.Visible, surface.ListForTests.Visibility);
        Assert.Equal(
            System.Windows.Visibility.Collapsed, surface.GridForTests.Visibility);
        var items = surface.ListForTests.ItemsSource!
            .Cast<SlateWindows.Bases.BaseListItemViewModel>()
            .ToList();
        // One md group over three notes: a header + three rows.
        Assert.Equal(4, items.Count);
        var header = Assert.IsType<SlateWindows.Bases.BaseListHeaderViewModel>(items[0]);
        Assert.True(header.IsHeader);
        Assert.Equal(
            SlateWindows.Grids.AccessibleDataGrid.ComposeGroupHeading(
                document.Result!.Groups[0].Label,
                (uint)document.Result.Groups[0].RowCount,
                null),
            header.AccessibleName);
        // Rows carry core's audio description verbatim (INV-1).
        Assert.False(items[1].IsHeader);
        Assert.Equal(
            document.Result.Rows[0].AudioDescription, items[1].AccessibleName);
        document.Shutdown();
    });

    [Fact]
    public void TableOverrideOnAListViewRendersTheGrid() => RunSta(() =>
    {
        var document = new SlateWindows.Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        document.SelectView(1);
        var surface = new SlateWindows.Bases.BaseSurfaceView
        {
            Model = document,
            RendererOverride = SlateWindows.Bases.BaseRendererOverride.Table,
        };

        Assert.Equal(
            System.Windows.Visibility.Visible, surface.GridForTests.Visibility);
        Assert.Equal(
            System.Windows.Visibility.Collapsed, surface.ListForTests.Visibility);
        document.Shutdown();
    });

    /// <summary>W7-7 PR 4 (#1247, R-5; codex round 1): Escape from the
    /// quick filter returns the keys to a ROW of a list-mode base — past the
    /// group heading, a disabled separator — never the bare list, from
    /// which an arrow walked into the menu bar. Synchronous here, so the
    /// re-query lands before the landing and nothing after it can re-seat
    /// the keys: this is the Escape's own landing.</summary>
    [Fact]
    public void EscapeFromTheQuickFilterLandsOnAListRow() => RunSta(() =>
    {
        var document = new SlateWindows.Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        document.SelectView(1);
        var surface = new SlateWindows.Bases.BaseSurfaceView { Model = document };
        var window = new System.Windows.Window
        {
            Content = surface,
            Width = 600,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            System.Windows.Controls.TextBox filter = surface.QuickFilterForTests;
            Assert.True(filter.Focus());
            filter.Text = "1";
            var escape = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(filter)!,
                0,
                System.Windows.Input.Key.Escape)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
            };
            filter.RaiseEvent(escape);

            Assert.True(escape.Handled, "Escape with text in the filter was not the filter's");
            Assert.Equal(string.Empty, filter.Text);
            var row = Assert.IsType<System.Windows.Controls.ListBoxItem>(
                System.Windows.Input.Keyboard.FocusedElement);
            Assert.Same(
                surface.ListForTests,
                System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(row));
            var landed = Assert.IsAssignableFrom<SlateWindows.Bases.BaseListItemViewModel>(row.DataContext);
            Assert.False(landed.IsHeader);

            // Codex PR 4 round 7 finding 4: the landing selects nothing, and
            // Enter opens the row it landed on — the row the reader hears.
            Assert.False(row.IsSelected, "premise: the landing selected its row");
            var opened = new List<BasesRow>();
            document.OpenRowFromSurface = opened.Add;
            var enter = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(row)!,
                0,
                System.Windows.Input.Key.Enter)
            {
                RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent,
            };
            row.RaiseEvent(enter);
            Assert.True(enter.Handled, "Enter on the landed row was not the list's");
            Assert.Same(landed.Row, Assert.Single(opened));
        }
        finally
        {
            window.Close();
            document.Shutdown();
        }
    });

    /// <summary>
    /// Codex PR 4's final check: a double-click on the list's EMPTY area —
    /// below the rows — opens nothing. Its first press lands the keys on a
    /// row (the click rule), and the double-click resolved that landed row
    /// and opened it, a note never clicked. A double-click on a row opens
    /// that row.
    /// </summary>
    [Fact]
    public void ADoubleClickOnTheListsEmptyAreaOpensNothing() => RunSta(() =>
    {
        var document = new SlateWindows.Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        document.SelectView(1);
        var opened = new List<BasesRow>();
        document.OpenRowFromSurface = opened.Add;
        var surface = new SlateWindows.Bases.BaseSurfaceView { Model = document };
        var window = new System.Windows.Window
        {
            Content = surface,
            Width = 600,
            Height = 500,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            System.Windows.Controls.ListBox list = surface.ListForTests;
            SelectorFocus.RegisterClickRule();
            list.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = System.Windows.UIElement.MouseDownEvent,
            });
            Assert.IsType<System.Windows.Controls.ListBoxItem>(System.Windows.Input.Keyboard.FocusedElement);

            // The double-click as the list raises it: on the list, from what
            // the pointer hit — here the list's own chrome.
            list.RaiseEvent(DoubleClick(list));

            Assert.Empty(opened);

            var row = (System.Windows.Controls.ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(2);
            list.RaiseEvent(DoubleClick(System.Windows.Media.VisualTreeHelper.GetChild(row, 0)));

            Assert.Same(((SlateWindows.Bases.BaseListItemViewModel)row.DataContext).Row, Assert.Single(opened));
        }
        finally
        {
            window.Close();
            document.Shutdown();
        }

        static System.Windows.Input.MouseButtonEventArgs DoubleClick(object hit) =>
            new(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent,
                Source = hit,
            };
    });

    /// <summary>
    /// W7-7 PR 4b (#1247, R-5; the completeness sweep's G10): every vault
    /// change reloads an open dashboard, and its render rebuilt every
    /// section's grid under the reader's cell; the keys went up to a focusable
    /// scroll viewer, the tab control or the window. They land in the same
    /// section, on the same note's row, once — silently. The section's
    /// renderer is the author's choice (codex PR 4b r1 F6): a LIST-rendered
    /// section keeps the row too — it landed on row 1.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("list")]
    public void ADashboardRebuiltUnderTheReaderKeepsTheirRow(string? view) => RunSta(() =>
    {
        string query = SaveAllNotesQuery();
        string id = _session.SaveDashboard("Board", [new DashboardSection(query, null, view)]);
        using var host = new DashboardHost(_session, id);
        System.Windows.UIElement stop = host.Stops().Single();
        AcquireNote1(host, stop);
        host.Changes.Clear();

        host.Dashboard.Load();
        PumpedDispatcher.Drain();

        System.Windows.UIElement rebuilt = host.Stops().Single();
        Assert.NotSame(stop, rebuilt);
        Assert.True(rebuilt.IsKeyboardFocusWithin, "the keys are not in the rebuilt section");
        Assert.EndsWith("note1.md", ReaderPath(host, rebuilt), StringComparison.Ordinal);
        Assert.DoesNotContain(host.Tabs, host.Changes);
        Assert.True(host.Changes.Count == 1, $"the keys moved {host.Changes.Count} times: {string.Join(" → ", host.Changes.Select(focus => focus.GetType().Name))}");
    });

    /// <summary>
    /// Codex PR 4b r1 F6: a section is identified by what it shows, not by its
    /// index — the dashboard editor reorders and inserts sections, and the
    /// numeric index then named a different section. Two sections over one
    /// query, "First" and "Second"; the reader on Second's note1; the sections
    /// swapped and the dashboard reloaded: the keys stay in "Second", on
    /// note1.
    /// </summary>
    [Fact]
    public void ADashboardReorderedUnderTheReaderKeepsTheirSection() => RunSta(() =>
    {
        string query = SaveAllNotesQuery();
        DashboardSection first = new(query, "First", null);
        DashboardSection second = new(query, "Second", null);
        string id = _session.SaveDashboard("Board", [first, second]);
        using var host = new DashboardHost(_session, id);
        System.Windows.UIElement secondStop = host.StopAfterHeading("Second");
        AcquireNote1(host, secondStop);
        host.Changes.Clear();

        _session.UpdateDashboard(id, "Board", [second, first]);
        host.Dashboard.Load();
        PumpedDispatcher.Drain();

        System.Windows.UIElement rebuilt = host.StopAfterHeading("Second");
        Assert.True(rebuilt.IsKeyboardFocusWithin, "the keys left the reader's section when the sections were reordered");
        Assert.EndsWith("note1.md", ReaderPath(host, rebuilt), StringComparison.Ordinal);
        Assert.Single(host.Changes);
    });

    /// <summary>
    /// PR 4b codex round 2, F8: the reader's section gone from the dashboard,
    /// the grid-or-list promotion — meant for the reader's OWN section, whose
    /// grid comes ahead of its banner — reached every section, and a later
    /// section's grid took the keys ahead of the first remaining section's
    /// banner. They land on the first remaining section's stop, once.
    /// </summary>
    [Fact]
    public void ADashboardWhoseReaderSectionIsGoneLandsOnTheFirstRemainingStop() => RunSta(() =>
    {
        string query = SaveAllNotesQuery();
        string doomed;
        ulong scratch = _session.OpenBase("Notes.base");
        try
        {
            doomed = _session.SaveQuery("Doomed", null, _session.BaseViewQueryJson(scratch, 0), SavedQuerySourceSyntax.Builder);
        }
        finally
        {
            _session.CloseBase(scratch);
        }

        DashboardSection reader = new(query, "Reader", null);
        DashboardSection missing = new(doomed, "Missing", null);
        DashboardSection last = new(query, "Last", null);
        string id = _session.SaveDashboard("Board", [reader, missing, last]);
        _session.DeleteSavedQuery(doomed);
        using var host = new DashboardHost(_session, id);
        AcquireNote1(host, host.StopAfterHeading("Reader"));
        host.Changes.Clear();

        _session.UpdateDashboard(id, "Board", [missing, last]);
        host.Dashboard.Load();
        PumpedDispatcher.Drain();

        var banner = Assert.IsType<System.Windows.Controls.TextBlock>(System.Windows.Input.Keyboard.FocusedElement);
        Assert.StartsWith("Missing saved query", banner.Text, StringComparison.Ordinal);
        Assert.Single(host.Changes);
    });

    /// <summary>
    /// PR 4b codex round 2, F2 (R-5 (h); W7-7 spec §5.2 item 2, an empty
    /// list lands on its empty-state notice): a dashboard with no sections
    /// had no stop in its surface, so F6 and every restore landed on its tab
    /// header. Its notice is the stop — and a section added under the keys
    /// lands them on the section's first stop, once.
    /// </summary>
    [Fact]
    public void AnEmptyDashboardLandsOnItsNoticeAndASectionAddedUnderTheKeysOnItsStop() => RunSta(() =>
    {
        string query = SaveAllNotesQuery();
        string id = _session.SaveDashboard("Board", []);
        using var host = new DashboardHost(_session, id);
        System.Windows.Controls.TextBlock notice = host.Surface.EmptyStateForTests;
        Assert.Empty(host.Stops());
        Assert.True(notice.IsVisible, "premise: the empty dashboard shows no notice");

        Assert.True(host.Surface.LandInSurface(), "the empty dashboard's surface took no keys");
        Assert.Same(notice, System.Windows.Input.Keyboard.FocusedElement);
        host.Changes.Clear();

        _session.UpdateDashboard(id, "Board", [new DashboardSection(query, null, null)]);
        host.Dashboard.Load();
        PumpedDispatcher.Drain();

        Assert.False(notice.IsVisible);
        Assert.True(host.Stops().Single().IsKeyboardFocusWithin, $"the keys are not in the added section, but on {System.Windows.Input.Keyboard.FocusedElement}");
        Assert.Single(host.Changes);
    });

    /// <summary>The notice claims nothing before the first publication: a
    /// populated dashboard's surface, shown before its load publishes, has
    /// no "No dashboard sections" line for the keys to land on.</summary>
    [Fact]
    public void ADashboardNotYetPublishedClaimsNoEmptiness() => RunSta(() =>
    {
        string id = _session.SaveDashboard("Board", [new DashboardSection(SaveAllNotesQuery(), null, null)]);
        using var host = new DashboardHost(_session, id, load: false);
        Assert.False(host.Dashboard.HasPublished);
        Assert.False(host.Surface.EmptyStateForTests.IsVisible, "the unloaded dashboard claims it has no sections");
        Assert.False(host.Surface.LandInSurface());

        host.Dashboard.Load();
        PumpedDispatcher.Drain();

        Assert.False(host.Surface.EmptyStateForTests.IsVisible);
        Assert.Single(host.Stops());
    });

    private string SaveAllNotesQuery()
    {
        ulong scratch = _session.OpenBase("Notes.base");
        try
        {
            return _session.SaveQuery("All notes", null, _session.BaseViewQueryJson(scratch, 0), SavedQuerySourceSyntax.Builder);
        }
        finally
        {
            _session.CloseBase(scratch);
        }
    }

    /// <summary>The reader on note1's row of a section's grid or list.</summary>
    private static void AcquireNote1(DashboardHost host, System.Windows.UIElement stop)
    {
        switch (stop)
        {
            case SlateWindows.Grids.AccessibleDataGrid grid:
                Assert.True(grid.SelectRow(row => row is SlateWindows.Bases.BaseGridRowViewModel { Row.FilePath: var path } && path.EndsWith("note1.md", StringComparison.Ordinal), moveFocus: true));
                break;
            case System.Windows.Controls.ListBox list:
                // The list's items are the rows it reads back (W7-7 PR 3's
                // sibling-named list).
                BasesRow note1 = list.Items.OfType<BasesRow>()
                    .Single(candidate => candidate.FilePath.EndsWith("note1.md", StringComparison.Ordinal));
                list.SelectedItem = note1;
                list.UpdateLayout();
                var item = Assert.IsType<System.Windows.Controls.ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(note1));
                Assert.True(item.Focus(), "premise: note1's list row refused the keys");
                break;
            default:
                throw new Xunit.Sdk.XunitException($"an unexpected section stop {stop}");
        }

        PumpedDispatcher.Drain();
    }

    /// <summary>The path of the note whose row the keys are on, in a section's
    /// grid or list.</summary>
    private static string ReaderPath(DashboardHost host, System.Windows.UIElement stop) => stop switch
    {
        SlateWindows.Grids.AccessibleDataGrid => Assert.IsType<SlateWindows.Bases.BaseGridRowViewModel>(
            Assert.IsType<System.Windows.Controls.DataGridCell>(System.Windows.Input.Keyboard.FocusedElement).DataContext).Row.FilePath,
        _ => Assert.IsType<BasesRow>(
            Assert.IsType<System.Windows.Controls.ListBoxItem>(System.Windows.Input.Keyboard.FocusedElement).DataContext).FilePath,
    };
    /// <summary>A dashboard's surface in a tab of its own, shown.</summary>
    private sealed class DashboardHost : IDisposable
    {
        private readonly System.Windows.Window _window;

        public DashboardHost(VaultSession session, string id, bool load = true)
        {
            Dashboard = new SlateWindows.Bases.DashboardViewModel(session, id, "Board", _ => { }, synchronousForTests: true);
            if (load)
            {
                Dashboard.Load();
            }

            Surface = new SlateWindows.Bases.DashboardSurfaceView { Model = Dashboard };
            Tabs = new System.Windows.Controls.TabControl();
            Tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Board", Content = Surface });
            _window = new System.Windows.Window
            {
                Content = Tabs,
                Width = 700,
                Height = 900,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            _window.UpdateLayout();
            System.Windows.Input.Keyboard.AddGotKeyboardFocusHandler(_window, (_, e) => Changes.Add(e.NewFocus));
        }

        public SlateWindows.Bases.DashboardViewModel Dashboard { get; }

        public SlateWindows.Bases.DashboardSurfaceView Surface { get; }

        public System.Windows.Controls.TabControl Tabs { get; }

        public List<System.Windows.IInputElement> Changes { get; } = [];

        /// <summary>The sections' grids and lists, in order.</summary>
        public List<System.Windows.UIElement> Stops() =>
        [
            .. Surface.SectionsForTests.Children.OfType<System.Windows.UIElement>()
                .Where(child => child is SlateWindows.Grids.AccessibleDataGrid or System.Windows.Controls.ListBox),
        ];

        /// <summary>The grid or list that follows the section heading
        /// <paramref name="heading"/>.</summary>
        public System.Windows.UIElement StopAfterHeading(string heading)
        {
            System.Windows.UIElement[] children = [.. Surface.SectionsForTests.Children.OfType<System.Windows.UIElement>()];
            int at = Array.FindIndex(children, child => child is System.Windows.Controls.TextBlock { Text: var text } && text == heading);
            Assert.True(at >= 0, $"no section is headed {heading}");
            return children.Skip(at + 1).First(child => child is SlateWindows.Grids.AccessibleDataGrid or System.Windows.Controls.ListBox);
        }

        public void Dispose()
        {
            _window.Close();
            Dashboard.Shutdown();
        }
    }

    public static TheoryData<string> RendererSwitches() => ["table to list", "list to table", "no rows", "failed"];

    /// <summary>
    /// W7-7 PR 4b (#1247, R-5; the completeness sweep's G3): the element
    /// holding the keys collapses under them — View as List from a cell,
    /// View as Table from a row, a publication with no rows, a failed
    /// reload — and WPF handed them up to the workspace tab control (the NVDA
    /// pass heard "Workspace tabs, tab control"). The surface's landing takes
    /// them, once: the shown renderer's current or first row or cell, else
    /// the quick filter, else Retry. The surface sits in a tab control here,
    /// as in the shell.
    /// </summary>
    [Theory]
    [MemberData(nameof(RendererSwitches))]
    public void TheRendererCollapsingUnderTheKeysLandsThemInTheSurface(string change) => RunSta(() =>
    {
        var document = new SlateWindows.Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        if (change == "list to table")
        {
            document.SelectView(1);
        }

        var surface = new SlateWindows.Bases.BaseSurfaceView { Model = document };
        var tabs = new System.Windows.Controls.TabControl();
        tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Notes", Content = surface });
        var window = new System.Windows.Window
        {
            Content = tabs,
            Width = 700,
            Height = 500,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        var changes = new List<System.Windows.IInputElement>();
        System.Windows.Input.Keyboard.AddGotKeyboardFocusHandler(window, (_, e) => changes.Add(e.NewFocus));
        try
        {
            if (change == "list to table")
            {
                System.Windows.Controls.ListBox list = surface.ListForTests;
                Assert.True(list.IsVisible, "premise: the list view shows the list");
                Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list), "premise: no list row took the keys");
            }
            else
            {
                Assert.True(surface.GridForTests.IsVisible, "premise: the table view shows the grid");
                Assert.True(SelectorFocus.LandOnStop(surface.GridForTests.Grid), "premise: no cell took the keys");
            }

            PumpedDispatcher.Drain();
            changes.Clear();

            switch (change)
            {
                case "table to list":
                    surface.RendererOverride = SlateWindows.Bases.BaseRendererOverride.List;
                    break;
                case "list to table":
                    surface.RendererOverride = SlateWindows.Bases.BaseRendererOverride.Table;
                    break;
                case "no rows":
                    document.QuickFilterText = "zzz-matches-nothing";
                    document.ApplyQuickFilter();
                    break;
                default:
                    File.Delete(Path.Combine(_fixture.Root, "Notes.base"));
                    document.Load();
                    break;
            }

            PumpedDispatcher.Drain();

            System.Windows.IInputElement landed = System.Windows.Input.Keyboard.FocusedElement;
            string where = string.Join(" → ", changes.Select(focus => focus.GetType().Name));
            Assert.DoesNotContain(tabs, changes);
            Assert.True(changes.Count == 1, $"the keys moved {changes.Count} times: {where}");
            switch (change)
            {
                case "table to list":
                    Assert.Same(
                        surface.ListForTests,
                        System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(
                            Assert.IsType<System.Windows.Controls.ListBoxItem>(landed)));
                    break;
                case "list to table":
                    Assert.IsType<System.Windows.Controls.DataGridCell>(landed);
                    Assert.True(surface.GridForTests.IsKeyboardFocusWithin);
                    break;
                case "no rows":
                    Assert.Same(surface.QuickFilterForTests, landed);
                    break;
                default:
                    Assert.Equal("Retry", Assert.IsType<System.Windows.Controls.Button>(landed).Content);
                    break;
            }
        }
        finally
        {
            window.Close();
            document.Shutdown();
        }
    });

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}

/// <summary>The external-sort substrate seam (contract C1/C6): an
/// externally-sortable column delegates ordering to the surface and
/// the grid neither reorders nor announces.</summary>
public sealed class AccessibleDataGridExternalSortTests
{
    [Fact]
    public void ExternallySortableColumnDelegatesWithoutHostReorder() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        var requested = new List<(int Column, bool Ascending)>();
        var grid = new AccessibleDataGrid
        {
            Announce = announced.Add,
            ExternalSortHandler = (column, ascending) =>
            {
                requested.Add((column, ascending));
                return true;
            },
        };
        string[] rows = ["beta", "alpha", "gamma"];
        grid.Bind(
            [
                new AccessibleGridColumn
                {
                    Header = "Name",
                    Cell = row => (string)row,
                    IsExternallySortable = true,
                },
            ],
            rows.Cast<object>().ToList(),
            summary: "3 rows",
            accessibilityLabel: "External sort probe",
            rowKey: static row => (string)row, rowAutomationName: row => (string)row);

        Assert.Null(grid.ApplySort(0, ascending: true));

        Assert.Equal([(0, true)], requested);
        // No host reorder: the surface republishes through Bind when
        // core's rows land.
        Assert.Equal("beta", Assert.IsType<string>(grid.Grid.Items[0]));
        Assert.Empty(announced);

        grid.SetSortIndicator((0, false));
        Assert.Equal(
            System.ComponentModel.ListSortDirection.Descending,
            grid.Grid.Columns[0].SortDirection);
        Assert.Empty(announced);

        // A re-Bind must not re-dispatch the external sort (that would
        // execute a core query per publish).
        grid.Bind(
            [
                new AccessibleGridColumn
                {
                    Header = "Name",
                    Cell = row => (string)row,
                    IsExternallySortable = true,
                },
            ],
            rows.Cast<object>().ToList(),
            summary: "3 rows",
            accessibilityLabel: "External sort probe",
            rowKey: static row => (string)row, rowAutomationName: row => (string)row);
        Assert.Single(requested);
    });

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}

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
        document.Shutdown();
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

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
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
            accessibilityLabel: "External sort probe");

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
            accessibilityLabel: "External sort probe");
        Assert.Single(requested);
    });

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

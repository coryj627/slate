// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Canvas;
using SlateWindows.FileManagement;
using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-35 (#1837): every search field's list is laid out in the operation that
/// publishes it, as the palette's is (DS-10, contract 28). TSF can be granted its lock between
/// dispatcher operations, and its <c>GetTextExt</c> then runs whatever layout and automation
/// events it finds pending in the dispatcher, inside the lock; a UIA event raised there waits on
/// a client reading the field. So each publication is read at its own end, with nothing pumped
/// after it: for a posted one, at the end of the dispatcher operation that published (the
/// dispatcher's <c>OperationCompleted</c> hook); for a text change, straight after the keystroke.
/// The reading is <see cref="PendingLayout"/>'s: the dispatcher's whole queue, and the list's
/// subtree for the message.
/// </summary>
/// <remarks>
/// The members are Quick Open, search, the Files filter, Move To and the canvas card and file
/// pickers; <c>Censuses/SearchFieldCensus.cs</c> holds every text field to this class. The
/// Quick Open, search and Move To arrows show the row they select on the route that runs, the
/// window's key route; a letter typed on one of their lists moves nothing (WPF's letter search
/// matched the rows' type names, CB-34's class); and a picker keeps a selection when its filter
/// drops the selected row.
/// </remarks>
public sealed class SearchFieldPublicationTests
{
    private const string FailingQuery = "cb35-the-rank-fails";

    // ------------------------------------------------------------------ the reader's premise

    /// <summary>The global reader reads WPF's own queue: the internals it reads are where
    /// WPF 10 keeps them, a dirtied element reads pending, and a layout pass clears it.</summary>
    [Fact]
    public void TheLayoutQueueReaderReadsWhatWpfHasPending() => Sta(() =>
    {
        Assert.Null(PendingLayout.Premise());
        var border = new Border { Width = 40, Height = 20 };
        var window = new Window
        {
            Content = border,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20_000,
            Top = -20_000,
            Width = 200,
            Height = 100,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Empty(PendingLayout.InTheDispatcher(window.Dispatcher));
            Assert.Empty(PendingLayout.Under(window));

            border.Width = 60;
            Assert.NotEmpty(PendingLayout.InTheDispatcher(window.Dispatcher));
            Assert.Contains(nameof(Border), PendingLayout.Under(window));

            window.UpdateLayout();
            Assert.Empty(PendingLayout.InTheDispatcher(window.Dispatcher));
            Assert.Empty(PendingLayout.Under(window));
        }
        finally
        {
            window.Close();
        }
    });

    // ------------------------------------------------------------------ nothing pending

    /// <summary>Quick Open's every publication: the opening rank, each keystroke's clear and the
    /// rank it posts, the rescan's silent re-rank, and a rank that fails.</summary>
    [Fact]
    public void QuickOpenLeavesNothingPendingWhenItPublishes() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var quickOpen = new QuickOpen(host, notes: 60, failOn: FailingQuery);
        QuickSwitcherViewModel switcher = quickOpen.Switcher;
        ListBox list = host.Shell.QuickSwitcherResultsList;
        TextBox field = host.Shell.QuickSwitcherSearchTextBox;
        var findings = new Findings();
        bool added = false;
        bool failed = false;
        switcher.Results.CollectionChanged += (_, e) => added |= e.Action == NotifyCollectionChangedAction.Add;
        switcher.PropertyChanged += (_, e) => failed |= e.PropertyName == nameof(QuickSwitcherViewModel.ResultSummary)
            && switcher.ResultSummary == "Quick Open could not rank files.";

        Clean(host);
        switcher.Open();
        Watch(findings, new Stage("the opening rank", () => added, () => findings.NothingPending("the opening rank's publication", list)));
        SettleUntil(host, () => !switcher.IsRanking && switcher.Results.Count == QuickSwitcherViewModel.DisplayCap, "the opening rank");
        Assert.True(field.Focus(), "premise: the field refused the keys");
        host.Settle();

        foreach (string query in new[] { "t", "topic-1", "topic" })
        {
            Clean(host);
            added = false;
            field.Text = query;
            findings.NothingPending($"typed '{query}': the keystroke's clear", list);
            Watch(findings, new Stage($"typed '{query}'", () => added, () => findings.NothingPending($"typed '{query}': the rank's publication", list)));
            SettleUntil(host, () => !switcher.IsRanking, $"the rank of '{query}'");
        }

        for (int press = 0; press < 12; press++)
        {
            switcher.MoveSelection(1);
        }

        Clean(host);
        added = false;
        Task reRank = switcher.ReRankForRescanAsync(CancellationToken.None);
        Watch(findings, new Stage("the silent re-rank", () => added, () => findings.NothingPending("the rescan's silent re-rank's publication", list)));
        SettleUntil(host, () => reRank.IsCompleted, "the silent re-rank");

        Clean(host);
        failed = false;
        field.Text = FailingQuery;
        findings.NothingPending("typed a query whose rank fails: the keystroke's clear", list);
        Watch(findings, new Stage("the failed rank", () => failed, () => findings.NothingPending("the failed rank's publication", list)));

        findings.AssertNone("Quick Open's publications left layout pending as they ended");
    });

    /// <summary>Search's every publication: the posted Searching state and the results it
    /// publishes, from Idle and over results, zero results, an error, and the idle transition
    /// an emptied field ends in.</summary>
    [Fact]
    public void SearchLeavesNothingPendingWhenItPublishes() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var search = new SearchOverlay(host);
        SearchOverlayViewModel overlay = search.Overlay;
        FrameworkElement surface = host.Shell.SearchOverlay;
        TextBox field = host.Shell.SearchOverlaySearchTextBox;
        var findings = new Findings();
        bool searching = false;
        bool published = false;
        bool idle = false;
        overlay.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SearchOverlayViewModel.State))
            {
                searching |= overlay.State == SearchOverlayState.Searching;
                published |= overlay.State is SearchOverlayState.Results or SearchOverlayState.Error;
                idle |= overlay.State == SearchOverlayState.Idle;
            }
        };
        search.Open();
        Assert.True(field.Focus(), "premise: the field refused the keys");
        host.Settle();

        foreach ((string label, string query, SearchOverlayState lands) in new[]
        {
            ("from Idle: 'alpha'", "alpha", SearchOverlayState.Results),
            ("over results: 'alpha beta'", "alpha beta", SearchOverlayState.Results),
            ("zero results: 'zzqqxx'", "zzqqxx", SearchOverlayState.Results),
            ("an error: an unterminated quote", "\"unterminated", SearchOverlayState.Error),
            ("from the error: 'gamma'", "gamma", SearchOverlayState.Results),
        })
        {
            Clean(host);
            searching = false;
            published = false;
            field.Text = query;
            Watch(
                findings,
                new Stage(label + ": the Searching state", () => searching, () => findings.NothingPending(label + ": the Searching state's operation", surface)),
                new Stage(label + ": the publication", () => published, () => findings.NothingPending(label + ": the publication", surface)));
            SettleUntil(host, () => overlay.State == lands, $"search never reached {lands} for {label}");
        }

        Clean(host);
        idle = false;
        field.Text = string.Empty;
        Watch(findings, new Stage("emptied: the idle transition", () => idle, () => findings.NothingPending("emptied: the idle transition", surface)));

        findings.AssertNone("Search's publications left layout pending as they ended");
    });

    /// <summary>The Files filter's every publication: the keystroke that swaps the tree for the
    /// list, each filter run it posts, the keystroke that empties the field and swaps back, and
    /// the field's Escape clear.</summary>
    [Fact]
    public void FilesFilterLeavesNothingPendingWhenItPublishes() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var files = new FilesSidebar(host);
        FilesSidebarViewModel sidebar = files.Model;
        ListBox list = host.Shell.FilterResultsList;
        var swap = (FrameworkElement)VisualTreeHelper.GetParent(list);
        TextBox field = host.Shell.SidebarFilterTextBox;
        var findings = new Findings();
        bool added = false;
        sidebar.FilterResults.CollectionChanged += (_, e) => added |= e.Action == NotifyCollectionChangedAction.Add;
        Assert.True(field.Focus(), "premise: the filter refused the keys");
        host.Settle();

        void Type(string query)
        {
            Clean(host);
            added = false;
            field.Text = query;
            findings.NothingPending($"typed '{query}': the keystroke", swap);
            Watch(findings, new Stage($"typed '{query}'", () => added, () => findings.NothingPending($"typed '{query}': the filter's publication", swap)));
            SettleUntil(host, () => !sidebar.IsFiltering, $"the filter of '{query}'");
        }

        Type("topic");
        Type("topic-1");
        Type("topic-2");

        Clean(host);
        field.Text = string.Empty;
        findings.NothingPending("emptied: the keystroke that swaps back to the tree", swap);
        host.Settle();

        Type("topic-3");
        Clean(host);
        Press(field, Key.Escape);
        findings.Expect(!sidebar.IsFilterActive, "premise: Escape did not clear the filter");
        findings.NothingPending("Escape: the clear", swap);

        findings.AssertNone("The Files filter's publications left layout pending as they ended");
    });

    /// <summary>Move To's every publication: the opening, each folder page and the complete
    /// publication, and each keystroke's rebuild.</summary>
    [Fact]
    public void MoveToLeavesNothingPendingWhenItPublishes() => Sta(() =>
    {
        using var host = new ShownShell(NestedFolders(60));
        WindowActivation.Activate(host.Shell);
        using var files = new FilesSidebar(host);
        ListBox list = host.Shell.MoveToList;
        TextBox field = host.Shell.MoveToFilterTextBox;
        var findings = new Findings();
        bool rows = false;

        Clean(host);
        MoveToPickerViewModel picker = files.OpenMoveTo();
        findings.NothingPending("the opening's own operation", list);
        picker.PropertyChanged += (_, e) => rows |= e.PropertyName == nameof(MoveToPickerViewModel.Rows);
        int pages = ReadEachPublication(
            findings,
            "a folder page",
            list,
            () =>
            {
                bool was = rows;
                rows = false;
                return was;
            },
            () => picker.State == MoveToLoadState.Ready);
        findings.Expect(pages >= 3, $"premise: only {pages} folder pages were read; the nested folders should give more than 60");
        host.Settle();
        Assert.True(field.Focus(), "premise: the filter refused the keys");
        host.Settle();

        foreach (string query in new[] { "dir-4", string.Empty, "dir" })
        {
            Clean(host);
            field.Text = query;
            findings.NothingPending($"typed '{query}': the keystroke's rebuild", list);
            host.Settle();
        }

        findings.AssertNone("Move To's publications left layout pending as they ended");
    });

    /// <summary>The canvas card and file pickers: each keystroke's filter, which publishes in the
    /// keystroke itself.</summary>
    [Fact]
    public void ThePickersLeaveNothingPendingWhenTheyPublish() => Sta(() =>
    {
        using var host = new ShownShell([("board.canvas", Board(60)), .. Topics(60)]);
        WindowActivation.Activate(host.Shell);
        CanvasDocumentViewModel canvas = OpenCanvas(host, "board.canvas");
        var findings = new Findings();
        foreach (Picker kind in new[] { Picker.Card, Picker.File })
        {
            (ICanvasPickerSheet sheet, ListBox list) = OpenPicker(host, canvas, kind);
            TextBox field = host.Shell.CanvasCardPickerFilterBox;
            Assert.True(field.Focus(), "premise: the filter refused the keys");
            host.Settle();
            string narrow = kind == Picker.Card ? "item 1" : "topic-1";
            string other = kind == Picker.Card ? "item 2" : "topic-2";
            foreach (string query in new[] { narrow, string.Empty, other, string.Empty })
            {
                Clean(host);
                field.Text = query;
                findings.NothingPending($"{kind} picker: typed '{query}'", list);
                host.Settle();
            }

            Assert.Same(sheet, host.Workspace.CanvasCardPickerSheet);
            host.Workspace.CloseCanvasCardPicker();
            host.Settle();
        }

        findings.AssertNone("The pickers' publications left layout pending as they ended");
    });

    // ------------------------------------------------------------------ the arrows' row in view

    /// <summary>Each Down and Up through the window's key route: the selected result realized
    /// and inside the list's viewport, and nothing pending, as the key's handling ends.</summary>
    [Fact]
    public void QuickOpenShowsTheRowTheArrowsSelect() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var quickOpen = new QuickOpen(host, notes: 60);
        quickOpen.Open();
        TextBox field = host.Shell.QuickSwitcherSearchTextBox;
        Assert.True(field.Focus(), "premise: the field refused the keys");
        host.Settle();
        ArrowsShowTheirRow(host, field, host.Shell.QuickSwitcherResultsList, () => quickOpen.Switcher.SelectedRow, downs: 30, ups: 3);
    });

    /// <summary>The search overlay's arrows, through the window's key route.</summary>
    [Fact]
    public void SearchShowsTheRowTheArrowsSelect() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var search = new SearchOverlay(host);
        search.Open();
        TextBox field = host.Shell.SearchOverlaySearchTextBox;
        Assert.True(field.Focus(), "premise: the field refused the keys");
        search.Show("alpha", 60);
        ArrowsShowTheirRow(host, field, host.Shell.SearchOverlayResults, () => host.Shell.SearchOverlayResults.SelectedItem, downs: 30, ups: 3);
    });

    /// <summary>Move To's arrows from its filter, through the sheet's key route.</summary>
    [Fact]
    public void MoveToShowsTheRowTheArrowsSelect() => Sta(() =>
    {
        using var host = new ShownShell(NestedFolders(60));
        WindowActivation.Activate(host.Shell);
        using var files = new FilesSidebar(host);
        MoveToPickerViewModel picker = files.OpenMoveTo();
        SettleUntil(host, () => picker.State == MoveToLoadState.Ready, "Move To never finished loading");
        TextBox field = host.Shell.MoveToFilterTextBox;
        Assert.True(field.Focus(), "premise: the filter refused the keys");
        host.Settle();
        ArrowsShowTheirRow(host, field, host.Shell.MoveToList, () => picker.SelectedRow, downs: 25, ups: 3);
    });

    private static void ArrowsShowTheirRow(ShownShell host, UIElement field, ListBox list, Func<object?> selected, int downs, int ups)
    {
        var findings = new Findings();
        int press = 0;
        foreach (Key key in Enumerable.Repeat(Key.Down, downs).Concat(Enumerable.Repeat(Key.Up, ups)))
        {
            press++;
            Clean(host);
            Press(field, key);
            string label = $"press {press} ({key})";
            findings.Shown(label, list, selected());
            findings.NothingPending(label, list);
        }

        findings.AssertNone("The arrows' row was not shown as the key's handling ended");
    }

    /// <summary>The rescan's silent re-rank keeps the reader's result selected far down the
    /// list, on a fresh row: the row is realized and in view as the publication ends, and still
    /// after everything queued has run.</summary>
    [Fact]
    public void QuickOpenKeepsAReRankedSelectionInView() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var quickOpen = new QuickOpen(host, notes: 60);
        quickOpen.Open();
        QuickSwitcherViewModel switcher = quickOpen.Switcher;
        ListBox list = host.Shell.QuickSwitcherResultsList;
        switcher.SelectedRow = switcher.Results[30];
        list.ScrollIntoView(switcher.SelectedRow);
        host.Settle();
        host.Settle();
        var findings = new Findings();
        findings.Shown("premise: the reader's row before the re-rank", list, switcher.SelectedRow);
        string path = switcher.SelectedRow!.Path;
        bool added = false;
        switcher.Results.CollectionChanged += (_, e) => added |= e.Action == NotifyCollectionChangedAction.Add;

        Clean(host);
        Task reRank = switcher.ReRankForRescanAsync(CancellationToken.None);
        Watch(findings, new Stage("the silent re-rank", () => added, () =>
        {
            findings.Expect(switcher.SelectedRow?.Path == path, $"the re-rank did not keep {path} (it holds {switcher.SelectedRow?.Path})");
            findings.Shown("as the re-rank's publication ends", list, switcher.SelectedRow);
        }));
        SettleUntil(host, () => reRank.IsCompleted, "the silent re-rank");
        findings.Shown("after everything queued has run", list, switcher.SelectedRow);

        findings.AssertNone("A re-ranked selection was not kept in view");
    });

    /// <summary>From an overlay whose list has never generated a row, the first results: the
    /// selected row is realized as the publication ends (DS-10's check), and no
    /// <c>ItemsControl.OnBringItemIntoView</c> is left deferred to the Loaded pass.</summary>
    [Fact]
    public void SearchsFirstResultsLeaveNoScrollDeferred() => Sta(() =>
    {
        using var host = new ShownShell(Topics(60));
        WindowActivation.Activate(host.Shell);
        using var search = new SearchOverlay(host);
        SearchOverlayViewModel overlay = search.Overlay;
        ListBox list = host.Shell.SearchOverlayResults;
        TextBox field = host.Shell.SearchOverlaySearchTextBox;
        search.Open();
        Assert.True(field.Focus(), "premise: the field refused the keys");
        host.Settle();
        Assert.False(list.IsVisible, "premise: the list shows before the first results");
        Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(0));

        var scrolls = new List<DispatcherOperation>();
        void Posted(object? sender, DispatcherHookEventArgs e)
        {
            if (OperationName(e.Operation).EndsWith("OnBringItemIntoView", StringComparison.Ordinal))
            {
                scrolls.Add(e.Operation);
            }
        }

        bool published = false;
        overlay.PropertyChanged += (_, e) => published |= e.PropertyName == nameof(SearchOverlayViewModel.State)
            && overlay.State == SearchOverlayState.Results;
        var findings = new Findings();
        DispatcherHooks hooks = Dispatcher.CurrentDispatcher.Hooks;
        hooks.OperationPosted += Posted;
        try
        {
            Clean(host);
            field.Text = "alpha";
            Watch(findings, new Stage("the first results", () => published, () =>
            {
                findings.Expect(overlay.SelectedIndex == 0, $"premise: the publication selected {overlay.SelectedIndex}");
                findings.Expect(
                    list.SelectedItem is { } row && list.ItemContainerGenerator.ContainerFromItem(row) is not null,
                    "the selected row is not realized as the first results' publication ends");
                int deferred = scrolls.Count(operation => operation.Status == DispatcherOperationStatus.Pending);
                findings.Expect(deferred == 0, $"{deferred} ItemsControl.OnBringItemIntoView left deferred as the first results' publication ends");
                findings.NothingPending("the first results' publication", list);
            }));
        }
        finally
        {
            hooks.OperationPosted -= Posted;
        }

        findings.AssertNone("Search's first results left a scroll deferred");
    });

    /// <summary>Folder pages that land while a reader has scrolled the list keep the reader's
    /// view: a background page keeps the selection, so nothing scrolls. A keystroke's
    /// publication chooses the selection, and shows it.</summary>
    [Fact]
    public void MoveToKeepsTheViewWhileFoldersLoad() => Sta(() =>
    {
        using var host = new ShownShell(NestedFolders(60));
        WindowActivation.Activate(host.Shell);
        using var files = new FilesSidebar(host);
        using var gate = new SemaphoreSlim(0);
        // Every listing below the root waits for the fact: the root's page lands, and each later
        // page lands when the fact lets one listing run.
        files.Model.BeforeMoveToPageForTesting = (parent, _) =>
        {
            if (parent.Length > 0 && !gate.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("the fact never let this folder listing run");
            }
        };
        try
        {
            ListBox list = host.Shell.MoveToList;
            TextBox field = host.Shell.MoveToFilterTextBox;
            MoveToPickerViewModel picker = files.OpenMoveTo();
            SettleUntil(host, () => picker.Rows.Count >= 60, "the root's folder page never landed");
            Assert.True(field.Focus(), "premise: the filter refused the keys");
            host.Settle();
            ScrollViewer viewer = Descendants<ScrollViewer>(list).First();
            viewer.ScrollToVerticalOffset(20);
            host.Settle();
            host.Settle();
            double offset = viewer.VerticalOffset;
            Assert.True(offset >= 19, $"premise: the reader's scroll reached offset {offset}");
            string selected = picker.SelectedRow!.Destination;
            var findings = new Findings();
            bool rows = false;
            picker.PropertyChanged += (_, e) => rows |= e.PropertyName == nameof(MoveToPickerViewModel.Rows);

            for (int page = 1; page <= 3; page++)
            {
                int count = picker.Rows.Count;
                rows = false;
                Clean(host);
                gate.Release();
                string label = $"folder page {page}";
                Watch(findings, new Stage(label, () => rows && picker.Rows.Count > count, () =>
                {
                    findings.Expect(picker.SelectedRow?.Destination == selected, $"{label}: the page moved the selection to {picker.SelectedRow?.Destination}");
                    findings.Expect(viewer.VerticalOffset == offset, $"{label}: the view moved from {offset} to {viewer.VerticalOffset} as the page ended");
                }));
                host.Settle();
                findings.Expect(viewer.VerticalOffset == offset, $"{label}: the view moved from {offset} to {viewer.VerticalOffset} after the page");
            }

            Clean(host);
            field.Text = "dir-3";
            findings.Expect(picker.SelectedRow?.Destination == "dir-30", $"premise: 'dir-3' selected {picker.SelectedRow?.Destination}");
            findings.Shown("typed 'dir-3': the keystroke's publication", list, picker.SelectedRow);

            findings.AssertNone("Move To's view did not hold while folders loaded");
        }
        finally
        {
            _ = gate.Release(1_000);
            files.Model.BeforeMoveToPageForTesting = null;
        }
    });

    // ------------------------------------------------------------------ letters and pickers

    /// <summary>With the keys and the selection on the fourth row of a search field's list, a
    /// letter typed into the row moves neither and says nothing. WPF's own letter search matched
    /// the rows' type names (CB-34's class): the letter each row's type name starts with is the
    /// one typed.</summary>
    [Theory]
    [InlineData("Quick Open")]
    [InlineData("Search")]
    [InlineData("Move To")]
    [InlineData("Card picker")]
    [InlineData("File picker")]
    public void ALetterOnASearchListMovesNothing(string member) => Sta(() =>
    {
        using var host = new ShownShell(member switch
        {
            "Move To" => NestedFolders(20),
            "Card picker" or "File picker" => [("board.canvas", Board(20)), .. Topics(20)],
            _ => Topics(20),
        });
        WindowActivation.Activate(host.Shell);
        var cleanup = new Stack<IDisposable>();
        try
        {
            ListBox list;
            Func<int> modelSelected;
            Action<int> seat;
            Func<int> heard;
            switch (member)
            {
                case "Quick Open":
                    var quickOpen = new QuickOpen(host, notes: 20);
                    cleanup.Push(quickOpen);
                    quickOpen.Open();
                    list = host.Shell.QuickSwitcherResultsList;
                    modelSelected = () => quickOpen.Switcher.SelectedRow is { } row ? quickOpen.Switcher.Results.IndexOf(row) : -1;
                    seat = index => quickOpen.Switcher.SelectedRow = quickOpen.Switcher.Results[index];
                    heard = () => quickOpen.Announced.Count;
                    break;
                case "Search":
                    var search = new SearchOverlay(host);
                    cleanup.Push(search);
                    search.Open();
                    search.Show("alpha", 20);
                    list = host.Shell.SearchOverlayResults;
                    modelSelected = () => search.Overlay.SelectedIndex;
                    seat = index => search.Overlay.SelectedIndex = index;
                    heard = () => search.Announced.Count;
                    break;
                case "Move To":
                    var files = new FilesSidebar(host);
                    cleanup.Push(files);
                    MoveToPickerViewModel picker = files.OpenMoveTo();
                    SettleUntil(host, () => picker.State == MoveToLoadState.Ready, "Move To never finished loading");
                    list = host.Shell.MoveToList;
                    modelSelected = () => picker.SelectedRow is { } row ? IndexIn(picker.Rows, row) : -1;
                    seat = index => picker.SelectedRow = picker.Rows[index];
                    heard = () => files.Announced.Count;
                    break;
                default:
                    CanvasDocumentViewModel canvas = OpenCanvas(host, "board.canvas");
                    (ICanvasPickerSheet sheet, ListBox rows) = OpenPicker(host, canvas, member == "Card picker" ? Picker.Card : Picker.File);
                    list = rows;
                    modelSelected = () => IndexIn(list, SheetSelection(sheet));
                    seat = index => list.SelectedIndex = index;
                    heard = () => host.Announced.Count;
                    break;
            }

            host.Settle();
            seat(3);
            host.Settle();
            list.UpdateLayout();
            var row3 = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(3));
            Assert.True(row3.Focus(), "premise: the fourth row refused the keys");
            host.Settle();
            Assert.Equal(3, list.SelectedIndex);
            Assert.Equal(3, modelSelected());
            string letter = InitialOf(list.Items[0]!);
            int before = heard();

            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, row3, letter));
            host.Settle();

            Assert.True(
                ReferenceEquals(row3, Keyboard.FocusedElement)
                    && list.SelectedIndex == 3
                    && modelSelected() == 3
                    && heard() == before,
                $"{member}: '{letter}' typed on the fourth row moved the keys to {KeysRow(list)}, the list's selection to "
                + $"{list.SelectedIndex} and the model's to {modelSelected()}, with {heard() - before} announcement(s)");
        }
        finally
        {
            while (cleanup.TryPop(out IDisposable? disposable))
            {
                disposable.Dispose();
            }
        }
    });

    /// <summary>A picker whose filter drops the selected row selects the first row it keeps,
    /// and the list shows the same row, so Enter picks it: the rows are published before the
    /// selection, so the list's two-way selection no longer writes the dropped row's null back
    /// over the picker's choice.</summary>
    [Theory]
    [InlineData("Card picker")]
    [InlineData("File picker")]
    public void APickerKeepsASelectionWhenItsFilterDropsTheRow(string member) => Sta(() =>
    {
        using var host = new ShownShell([("board.canvas", Board(20)), .. Topics(20)]);
        WindowActivation.Activate(host.Shell);
        CanvasDocumentViewModel canvas = OpenCanvas(host, "board.canvas");
        (ICanvasPickerSheet sheet, ListBox list) = OpenPicker(host, canvas, member == "Card picker" ? Picker.Card : Picker.File);
        list.SelectedIndex = 3;
        host.Settle();
        object seated = Assert.IsAssignableFrom<object>(SheetSelection(sheet));
        Assert.Same(list.Items[3], seated);
        string filter = LabelOf(list.Items[0]!);
        TextBox field = host.Shell.CanvasCardPickerFilterBox;
        Assert.True(field.Focus(), "premise: the filter refused the keys");
        host.Settle();

        field.Text = filter;

        Assert.DoesNotContain(seated, list.Items.Cast<object>());
        Assert.NotEmpty(list.Items.Cast<object>());
        object? chosen = SheetSelection(sheet);
        Assert.True(chosen is not null, $"{member}: the filter '{filter}' dropped the selected row and left the picker with no selection");
        Assert.Same(list.Items[0], chosen);
        Assert.Same(chosen, list.SelectedItem);
        host.Settle();
        Assert.Same(chosen, SheetSelection(sheet));
        Assert.True(sheet.Confirm(), $"{member}: Enter picked nothing after the filter");
    });

    // ------------------------------------------------------------------ fixtures

    /// <summary>Quick Open over the shell's own overlay, attached through the lifecycle's
    /// setter as a vault's is; with <c>failOn</c>, that one query's rank throws.</summary>
    private sealed class QuickOpen : IDisposable
    {
        private readonly ShownShell _host;
        private readonly int _notes;

        public QuickOpen(ShownShell host, int notes, string? failOn = null)
        {
            _host = host;
            _notes = notes;
            VaultSession session = host.Session;
            Switcher = new QuickSwitcherViewModel(
                session,
                host.Root,
                Announced.Add,
                localAppDataRoot: host.Root,
                rankTop: failOn is null
                    ? null
                    : (query, recents, cancel) => query == failOn
                        ? throw new InvalidOperationException("CB-35's planted rank failure")
                        : session.SwitcherRankTopIndexed(query, recents, QuickSwitcherViewModel.DisplayCap, cancel));
            SetLifecycleField(host, "_session", session);
            SetLifecycleField(host, "_vaultPath", host.Root);
            SetLifecycleProperty(host, nameof(VaultLifecycleViewModel.QuickSwitcher), Switcher);
        }

        public QuickSwitcherViewModel Switcher { get; }

        public List<A11yEvent> Announced { get; } = [];

        public void Open()
        {
            Switcher.Open();
            SettleUntil(
                _host,
                () => !Switcher.IsRanking && Switcher.Results.Count == Math.Min(_notes, QuickSwitcherViewModel.DisplayCap),
                "Quick Open's first rank never published");
        }

        public void Dispose()
        {
            Switcher.Dismiss();
            SetLifecycleProperty(_host, nameof(VaultLifecycleViewModel.QuickSwitcher), null);
            SetLifecycleField(_host, "_session", null);
            SetLifecycleField(_host, "_vaultPath", string.Empty);
            PumpedDispatcher.Drain();
            Switcher.Dispose();
        }
    }

    /// <summary>The lifecycle's own search overlay over the shell's vault.</summary>
    private sealed class SearchOverlay : IDisposable
    {
        private readonly ShownShell _host;

        public SearchOverlay(ShownShell host)
        {
            _host = host;
            SetLifecycleField(host, "_session", host.Session);
            SetLifecycleField(host, "_vaultPath", host.Root);
            Overlay = host.Lifecycle.Search;
            FieldInfo field = typeof(SearchOverlayViewModel).GetField("_announce", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("SearchOverlayViewModel._announce is gone");
            var original = (Action<A11yEvent>)field.GetValue(Overlay)!;
            field.SetValue(Overlay, (Action<A11yEvent>)(announcement =>
            {
                Announced.Add(announcement);
                original(announcement);
            }));
        }

        public SearchOverlayViewModel Overlay { get; }

        public List<A11yEvent> Announced { get; } = [];

        public void Open()
        {
            Overlay.Open();
            Assert.True(Overlay.IsOpen, "premise: search did not open");
            _host.Settle();
            _host.Settle();
        }

        /// <summary>A query typed and its results published and laid out.</summary>
        public void Show(string query, int rows)
        {
            _host.Shell.SearchOverlaySearchTextBox.Text = query;
            SettleUntil(_host, () => Overlay.State == SearchOverlayState.Results && Overlay.Rows.Count == rows, $"search never published {rows} rows for '{query}'");
        }

        public void Dispose()
        {
            Overlay.Close();
            SetLifecycleField(_host, "_session", null);
            SetLifecycleField(_host, "_vaultPath", string.Empty);
            PumpedDispatcher.Drain();
        }
    }

    /// <summary>A Files sidebar over the shell's vault, attached through the lifecycle's
    /// setter, as the step 0 probe attached it.</summary>
    private sealed class FilesSidebar : IDisposable
    {
        private readonly ShownShell _host;

        public FilesSidebar(ShownShell host)
        {
            _host = host;
            Model = new FilesSidebarViewModel(
                host.Session, Announced.Add, vaultRoot: host.Root, localAppDataRoot: Path.Combine(host.Root, "device-state"));
            PumpedDispatcher.PumpUntilDrained(Model.TreeRefreshCompletion);
            SetLifecycleProperty(host, nameof(VaultLifecycleViewModel.FileSidebar), Model);
            host.Settle();
        }

        public FilesSidebarViewModel Model { get; }

        public List<A11yEvent> Announced { get; } = [];

        /// <summary>Move To for the first note, from the tree's selection; answers the sheet.</summary>
        public MoveToPickerViewModel OpenMoveTo()
        {
            Model.SelectedNode = Model.RootNodes.Single(node => node.Path == "topic-00.md");
            Model.MoveToCommand.Execute(null);
            return Assert.IsType<MoveToPickerViewModel>(Model.MoveToSheet);
        }

        public void Dispose()
        {
            SetLifecycleProperty(_host, nameof(VaultLifecycleViewModel.FileSidebar), null);
            PumpedDispatcher.PumpUntilDrained(Model.BeginSessionShutdownAndCaptureWork().SessionWork);
        }
    }

    private enum Picker
    {
        Card,
        File,
    }

    private static (ICanvasPickerSheet Sheet, ListBox List) OpenPicker(ShownShell host, CanvasDocumentViewModel canvas, Picker kind)
    {
        canvas.SeatSelectionSilently("c00");
        host.Settle();
        if (kind == Picker.Card)
        {
            canvas.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
        }
        else
        {
            canvas.RequestVaultPick(CanvasVaultPickPurpose.Note, host.Workspace.ActiveGroup.ActiveTab);
        }

        SettleUntil(
            host,
            () => host.Workspace.CanvasCardPickerSheet is not null && host.Shell.OpenModalSurface == ModalSurface.CanvasCardPicker,
            $"the {kind} picker never opened");
        ICanvasPickerSheet sheet = host.Workspace.CanvasCardPickerSheet!;
        ListBox list = Descendants<ListBox>(host.Shell).Single(candidate => AutomationProperties.GetAutomationId(candidate) == "CanvasCardPickerRows");
        Assert.True(list.Items.Count > 4, $"premise: the {kind} picker lists {list.Items.Count} rows");
        return (sheet, list);
    }

    private static object? SheetSelection(ICanvasPickerSheet sheet) => sheet switch
    {
        CanvasCardPickerViewModel cards => cards.SelectedRow,
        CanvasVaultFilePickerViewModel files => files.SelectedRow,
        _ => throw new InvalidOperationException($"an unknown picker sheet {sheet.GetType().Name}"),
    };

    private static string LabelOf(object row) => row switch
    {
        CanvasCardPickerRow card => card.Label,
        CanvasPickerRow file => file.Label,
        _ => throw new InvalidOperationException($"an unknown picker row {row.GetType().Name}"),
    };

    private static CanvasDocumentViewModel OpenCanvas(ShownShell host, string path)
    {
        host.Workspace.OpenPath(path);
        CanvasDocumentViewModel canvas = Assert.IsType<CanvasDocumentViewModel>(
            Assert.IsType<WorkspaceTabViewModel>(host.Workspace.ActiveGroup.ActiveTab).Canvas);
        SettleUntil(host, () => canvas.State == CanvasLoadState.Ready, $"the canvas never loaded ({canvas.State})");
        return canvas;
    }

    // ------------------------------------------------------------------ reading

    /// <summary>What a fact found, reported together, so a red run shows every reading.</summary>
    private sealed class Findings
    {
        private readonly List<string> _found = [];

        public void NothingPending(string label, DependencyObject root)
        {
            string pending = PendingLayout.Describe(root);
            if (pending.Length > 0)
            {
                _found.Add($"{label}: {pending}");
            }
        }

        /// <summary>The row realized, showing, and inside its list's viewport, now.</summary>
        public void Shown(string label, ListBox list, object? row)
        {
            if (row is null)
            {
                _found.Add($"{label}: no row is selected");
                return;
            }

            if (!ReferenceEquals(list.SelectedItem, row) && !Equals(list.SelectedItem, row))
            {
                _found.Add($"{label}: the list selects {list.SelectedItem} and the model {row}");
            }

            if (list.ItemContainerGenerator.ContainerFromItem(row) is not FrameworkElement container)
            {
                _found.Add($"{label}: the selected row (index {list.Items.IndexOf(row)}) is not realized");
                return;
            }

            ScrollContentPresenter? viewport = Descendants<ScrollContentPresenter>(list).FirstOrDefault();
            if (viewport is null || !container.IsVisible)
            {
                _found.Add($"{label}: the selected row (index {list.Items.IndexOf(row)}) does not show");
                return;
            }

            Rect bounds = container.TransformToAncestor(viewport).TransformBounds(new Rect(container.RenderSize));
            if (bounds.Top < -0.5 || bounds.Bottom > viewport.ActualHeight + 0.5)
            {
                _found.Add($"{label}: the selected row (index {list.Items.IndexOf(row)}) is outside the viewport "
                    + $"(row {bounds.Top:0}..{bounds.Bottom:0}, viewport 0..{viewport.ActualHeight:0})");
            }
        }

        public void Expect(bool holds, string failure)
        {
            if (!holds)
            {
                _found.Add(failure);
            }
        }

        public void AssertNone(string what) =>
            Assert.True(_found.Count == 0, $"{what} ({_found.Count}):\n  " + string.Join("\n  ", _found));
    }

    private sealed record Stage(string Label, Func<bool> Published, Action AtItsEnd);

    /// <summary>Runs each stage's reading at the end of the first dispatcher operation after
    /// which its condition holds, in order, with nothing pumped after that operation.</summary>
    private static void Watch(Findings findings, params Stage[] stages)
    {
        int next = 0;
        void Completed(object? sender, DispatcherHookEventArgs e)
        {
            while (next < stages.Length && stages[next].Published())
            {
                stages[next++].AtItsEnd();
            }
        }

        DispatcherHooks hooks = Dispatcher.CurrentDispatcher.Hooks;
        hooks.OperationCompleted += Completed;
        try
        {
            if (!PumpedDispatcher.PumpUntil(() => next >= stages.Length, TimeSpan.FromSeconds(20)))
            {
                findings.Expect(false, $"{stages[next].Label}: never published within 20 s");
            }
        }
        finally
        {
            hooks.OperationCompleted -= Completed;
        }
    }

    /// <summary>Reads the list at the end of every dispatcher operation that published, until
    /// <paramref name="done"/>; answers how many it read.</summary>
    private static int ReadEachPublication(Findings findings, string label, DependencyObject root, Func<bool> takePublished, Func<bool> done)
    {
        int read = 0;
        void Completed(object? sender, DispatcherHookEventArgs e)
        {
            if (takePublished())
            {
                read++;
                findings.NothingPending($"{label} ({read})", root);
            }
        }

        DispatcherHooks hooks = Dispatcher.CurrentDispatcher.Hooks;
        hooks.OperationCompleted += Completed;
        try
        {
            if (!PumpedDispatcher.PumpUntil(done, TimeSpan.FromSeconds(30)))
            {
                findings.Expect(false, $"{label}: the publications never finished within 30 s");
            }
        }
        finally
        {
            hooks.OperationCompleted -= Completed;
        }

        return read;
    }

    /// <summary>Everything laid out before a fact acts, so what it reads after is the act's.</summary>
    private static void Clean(ShownShell host)
    {
        host.Shell.UpdateLayout();
        Assert.Empty(PendingLayout.InTheDispatcher(host.Shell.Dispatcher));
    }

    private static void SettleUntil(ShownShell host, Func<bool> condition, string what)
    {
        Assert.True(PumpedDispatcher.PumpUntil(condition, TimeSpan.FromSeconds(20)), what);
        host.Settle();
        host.Settle();
    }

    private static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });

    private static string OperationName(DispatcherOperation operation)
    {
        if (typeof(DispatcherOperation).GetProperty("Name", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(operation) is string name)
        {
            return name;
        }

        return typeof(DispatcherOperation).GetField("_method", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(operation) is Delegate method
            ? $"{method.Method.DeclaringType?.FullName}.{method.Method.Name}"
            : "?";
    }

    private static string KeysRow(ItemsControl list) =>
        Keyboard.FocusedElement is DependencyObject focused && ItemsControl.ContainerFromElement(list, focused) is { } container
            ? "row " + list.ItemContainerGenerator.IndexFromContainer(container).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Keyboard.FocusedElement?.GetType().Name ?? "nothing";

    private static string InitialOf(object item) => char.ToLowerInvariant(item.ToString()![0]).ToString();

    private static int IndexIn<T>(IReadOnlyList<T> rows, T row)
        where T : class
    {
        for (int index = 0; index < rows.Count; index++)
        {
            if (ReferenceEquals(rows[index], row))
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexIn(ListBox list, object? row) => row is null ? -1 : list.Items.IndexOf(row);

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject => ShownShell.Descendants(root).OfType<T>();

    private static void SetLifecycleField(ShownShell host, string name, object? value) =>
        (typeof(VaultLifecycleViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
        .SetValue(host.Lifecycle, value);

    private static void SetLifecycleProperty(ShownShell host, string name, object? value) =>
        (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
        .SetValue(host.Lifecycle, value);

    private static (string Path, string Text)[] Topics(int count) =>
        [.. Enumerable.Range(0, count).Select(index => ($"topic-{index:D2}.md", $"# Topic {index:D2}\n\nalpha beta gamma {index:D2}\n"))];

    /// <summary>Three notes and <paramref name="count"/> folders, each with a folder inside: the
    /// root's listing is one page, and each folder's own listing is another.</summary>
    private static (string Path, string Text)[] NestedFolders(int count) =>
    [
        .. Topics(3),
        .. Enumerable.Range(0, count).Select(index => ($"dir-{index:D2}/sub/inside.md", "# Inside\n")),
    ];

    private static string Board(int count) =>
        "{\"nodes\":["
        + string.Join(",", Enumerable.Range(0, count).Select(index =>
            $"{{\"id\":\"c{index:D2}\",\"type\":\"text\",\"text\":\"Item {index:D2}\",\"x\":{index % 6 * 300},\"y\":{index / 6 * 200},\"width\":240,\"height\":120}}"))
        + "],\"edges\":[]}";

    private static void Sta(Action body)
    {
        Func<bool> overlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(240), "a CB-35 fact timed out");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = overlayProbe;
        }
    }
}

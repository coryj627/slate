// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Xml.Linq;
using SlateWindows.Tests.Censuses;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, R-4: a duplicate sibling is told apart; codex PR 3
/// round 2): the flat lists whose rows can share a name, hosted from their
/// authored XAML over real view models and read as UIA reads them — every
/// row a distinct name. The filter results and the shortcuts flatten the
/// vault, so one file name in two folders is two rows that read alike
/// unless each also speaks its path.
/// </summary>
public sealed class SharedNameTests
{
    /// <summary>Two notes called note.md in two folders, and a third whose
    /// name nothing shares: the filter results and the shortcuts each read
    /// the two apart by path, and leave the third bare. Removing one of
    /// the pair re-reads the other bare.</summary>
    [Fact]
    public void FlatFileListsTellASharedFileNameApartByPath() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "shared-file-names");
        foreach (string path in new[] { "A/note.md", "B/note.md", "notes-extra.md" })
        {
            string full = Path.Combine(fixture.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "plain body\n");
        }
        Directory.CreateDirectory(Path.Combine(fixture.Root, ".slate"));
        File.WriteAllText(
            Path.Combine(fixture.Root, ".slate", "sidebar.json"),
            """
            {
              "version": 1,
              "shortcuts": [
                { "kind": "file", "path": "A/note.md" },
                { "kind": "file", "path": "B/note.md" },
                { "kind": "file", "path": "notes-extra.md" }
              ]
            }
            """);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var inline = new InlineContext();
        var sidebar = new FilesSidebarViewModel(
            session,
            _ => { },
            vaultRoot: fixture.Root,
            localAppDataRoot: Path.Combine(fixture.Root, "device-state"),
            filterUiContext: inline,
            treeUiContext: inline,
            treeWorker: (work, _) => { work(); return Task.CompletedTask; },
            filterWorker: (work, _) => { work(); return Task.CompletedTask; },
            filterDelay: _ => Task.CompletedTask);
        sidebar.FilterText = "note";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => sidebar.FilterResults.Count == 3, TimeSpan.FromSeconds(10)),
            $"the filter published {sidebar.FilterResults.Count} results");

        HostedNames("SidebarFilterResults", sidebar, names => Assert.Equal(
            [
                "note.md, file, A/note.md",
                "note.md, file, B/note.md",
                "notes-extra.md, file",
            ],
            names.Order(StringComparer.Ordinal)));
        HostedNames("SidebarShortcuts", sidebar, names => Assert.Equal(
            [
                "note.md, file shortcut, A/note.md",
                "note.md, file shortcut, B/note.md",
                "notes-extra.md, file shortcut",
            ],
            names.Order(StringComparer.Ordinal)));

        sidebar.Shortcuts.RemoveAt(1);
        HostedNames("SidebarShortcuts", sidebar, names => Assert.Equal(
            ["note.md, file shortcut", "notes-extra.md, file shortcut"],
            names.Order(StringComparer.Ordinal)));
    });

    /// <summary>The spec review, rounds 21-23: Quick Open speaks a row by
    /// core's DISPLAY name — the extension stripped — so note.md and
    /// note.markdown in two folders both read "note". Namesakes are found
    /// over that final label, never the raw file name, and each adds the
    /// path its row shows; a label no sibling shares reads bare.</summary>
    [Fact]
    public void QuickOpenRowsSharingADisplayNameReadTheirVisiblePath() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "quick-open-namesakes");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using QuickSwitcherViewModel quick = OpenQuickSwitcher(
            session,
            fixture.Root,
            new SwitcherFile("A/note.md", "note.md"),
            new SwitcherFile("B/note.markdown", "note.markdown"),
            new SwitcherFile("C/other.md", "other.md"));
        // The premise: two raw names, ONE spoken label.
        Assert.Equal(
            ["note", "note", "other"],
            quick.Results.Select(row => row.DisplayName).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["note.markdown", "note.md", "other.md"],
            quick.Results.Select(row => row.Name).Order(StringComparer.Ordinal));
        HostedNames("QuickSwitcherResults", quick, names => Assert.Equal(
            ["note, A/note.md", "note, B/note.markdown", "other"],
            names.Order(StringComparer.Ordinal)));
    });

    /// <summary>...and rows whose labels differ read bare, whatever their
    /// folders: the path tells namesakes apart, it is not a
    /// decoration.</summary>
    [Fact]
    public void QuickOpenRowsWithLabelsOfTheirOwnReadBare() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "quick-open-distinct");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using QuickSwitcherViewModel quick = OpenQuickSwitcher(
            session,
            fixture.Root,
            new SwitcherFile("A/alpha.md", "alpha.md"),
            new SwitcherFile("A/deep/beta.md", "beta.md"));
        HostedNames("QuickSwitcherResults", quick, names => Assert.Equal(
            ["alpha", "beta"],
            names.Order(StringComparer.Ordinal)));
    });

    private static QuickSwitcherViewModel OpenQuickSwitcher(
        VaultSession session, string root, params SwitcherFile[] files)
    {
        var quick = new QuickSwitcherViewModel(
            session,
            root,
            _ => { },
            files,
            Path.Combine(root, "device-state"),
            debounceRanking: false);
        quick.Open();
        Assert.Equal(files.Length, quick.Results.Count);
        return quick;
    }

    /// <summary>The spec review, round 21: two tabs of one title read their
    /// folders; two tabs of one FILE (Duplicate Tab) share the folder too,
    /// so they read their places — and every state a tab can be in
    /// (unsaved, missing from disk, both) is spoken over the tab's own
    /// told-apart name.</summary>
    [Fact]
    public void WorkspaceTabsSharingATitleReadTheirFolderElseTheirPlaceInEveryState() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "shared-tab-titles");
        foreach (string path in new[] { "A/note.md", "B/note.md" })
        {
            string full = Path.Combine(fixture.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "plain body\n");
        }
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        workspace.OpenPath("A/note.md");
        workspace.OpenPath("B/note.md", WorkspaceOpenTarget.NewTab);
        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel[] tabs = [.. workspace.ActiveGroup.Tabs];
        Assert.Equal(["A/note.md", "B/note.md", "B/note.md"], tabs.Select(tab => tab.Path));
        // A tab is titled by its display name, the extension stripped.
        Assert.All(tabs, tab => Assert.Equal("note", tab.Title));

        (ItemsControl host, _) = ItemContainerNameBindingTests.AuthoredHost("WorkspaceTabs");
        host.ItemsSource = workspace.ActiveGroup.Tabs;
        string[] told = ["note, A", "note, tab 2", "note, tab 3"];
        static string Stated(string name, WorkspaceTabViewModel tab) => (tab.IsDirty, tab.IsMissingFromDisk) switch
        {
            (false, false) => name,
            (true, false) => $"{name}, unsaved changes",
            (false, true) => $"{name}, missing from disk",
            (true, true) => $"{name}, missing from disk, unsaved changes",
        };
        ItemContainerNameBindingTests.Hosted(host, () =>
        {
            void Expect(string state)
            {
                string[] expected = [.. tabs.Select((tab, index) => Stated(told[index], tab))];
                string[] read = ItemContainerNameBindingTests.ItemNames(host);
                Assert.True(
                    expected.SequenceEqual(read),
                    $"{state}: expected [{string.Join(" | ", expected)}], read [{string.Join(" | ", read)}]");
            }

            Assert.All(tabs, tab => Assert.False(tab.IsDirty || tab.IsMissingFromDisk));
            Expect("at rest");
            tabs[0].Text = "edited body\n";
            Assert.True(tabs[0].IsDirty);
            Expect("the first unsaved");
            tabs[1].InvalidatePath();
            Assert.True(tabs[1].IsMissingFromDisk);
            Expect("the second missing");
            tabs[0].InvalidatePath();
            Assert.True(tabs[0].IsDirty && tabs[0].IsMissingFromDisk);
            Expect("the first unsaved and missing");
        });
    });

    /// <summary>Add section takes one saved query twice: each section reads
    /// its place, and follows it through a move and a removal.</summary>
    [Fact]
    public void ASavedQueryAddedTwiceReadsAsTwoSections()
    {
        var editor = new Bases.DashboardEditorViewModel(null, "Board");
        editor.Sections.Add(new Bases.DashboardEditorSection("q1", "Journey query"));
        editor.Sections.Add(new Bases.DashboardEditorSection("q1", "Journey query"));
        Bases.DashboardEditorSection first = editor.Sections[0];
        Bases.DashboardEditorSection second = editor.Sections[1];
        Assert.Equal("Journey query (section 1)", first.AutomationName);
        Assert.Equal("Journey query (section 2)", second.AutomationName);

        var changed = new List<string?>();
        first.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        editor.Sections.Move(0, 1);
        Assert.Equal("Journey query (section 2)", first.AutomationName);
        Assert.Equal("Journey query (section 1)", second.AutomationName);
        Assert.Contains(nameof(Bases.DashboardEditorSection.AutomationName), changed);

        Assert.True(editor.Sections.Remove(second));
        Assert.Equal("Journey query (section 1)", first.AutomationName);
    }

    /// <summary>The same, hosted: two sections of one query, and every stop
    /// in the list — each section row and each control in it — reads
    /// apart.</summary>
    [Fact]
    public void DashboardSectionsOfOneQueryReadApartDownToTheirControls() => RunSta(() =>
    {
        var editor = new Bases.DashboardEditorViewModel(null, "Board");
        editor.Sections.Add(new Bases.DashboardEditorSection("q1", "Journey query"));
        editor.Sections.Add(new Bases.DashboardEditorSection("q1", "Journey query"));
        static string[] Section(int position)
        {
            string name = $"Journey query (section {position})";
            return
            [
                name,
                $"Move {name} up",
                $"Move {name} down",
                $"Remove {name}",
                $"Heading override for {name}",
                $"View override for {name}",
            ];
        }
        HostedStops("DashboardEditorSections", editor, stops => Assert.Equal(
            [.. Section(1), .. Section(2)],
            stops));
    });

    /// <summary>Loads the authored host <paramref name="label"/> over
    /// <paramref name="dataContext"/> and hands <paramref name="check"/>
    /// every stop in it — each item and each control an item holds
    /// (<see cref="WrappedStopTests.Stops"/>) — after asserting no two
    /// read alike.</summary>
    internal static void HostedStops(string label, object dataContext, Action<string[]> check) =>
        Hosted(label, dataContext, host =>
        {
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
            peer.ResetChildrenCache();
            string[] stops = [.. (peer.GetChildren() ?? [])
                .OfType<ItemAutomationPeer>()
                .SelectMany(item => WrappedStopTests.Stops(item))
                .Select(stop => stop.GetName())];
            AssertDistinct(label, stops);
            check(stops);
        });

    /// <summary>Loads the authored host <paramref name="label"/> over
    /// <paramref name="dataContext"/> and hands <paramref name="check"/>
    /// every item peer's name, after asserting no two are alike.</summary>
    internal static void HostedNames(string label, object dataContext, Action<string[]> check) =>
        Hosted(label, dataContext, host =>
        {
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
            peer.ResetChildrenCache();
            string[] names = [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>().Select(item => item.GetName())];
            AssertDistinct(label, names);
            check(names);
        });

    private static void AssertDistinct(string label, string[] names)
    {
        Assert.NotEmpty(names);
        Assert.True(
            names.Distinct(StringComparer.CurrentCultureIgnoreCase).Count() == names.Length,
            $"{label}: stops read alike: {string.Join(" | ", names)}");
    }

    private static void Hosted(string label, object dataContext, Action<ItemsControl> read)
    {
        (string file, XElement element) = ItemContainerNameCensus.XamlHost(label);
        (Grid root, ItemsControl host) = ShellXamlFragments.LoadHost(element, file);
        var window = new Window
        {
            DataContext = dataContext,
            Content = root,
            Width = 720,
            Height = 480,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            window.UpdateLayout();
            read(host);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Runs every post at once, on the posting thread.</summary>
    private sealed class InlineContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

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
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

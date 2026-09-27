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
        FilterWhenSettled(sidebar, "note", 3);

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

    /// <summary>The spec review, rounds 21-23 and 26: Quick Open speaks a
    /// row by core's DISPLAY name — the extension stripped — so note.md and
    /// note.markdown both read "note", in two folders or in one. Namesakes
    /// are found over that final label, never the raw file name, and each
    /// adds the path its row shows, extension and all; a label no sibling
    /// shares reads bare.</summary>
    [Fact]
    public void QuickOpenRowsSharingADisplayNameReadTheirVisiblePath() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "quick-open-namesakes");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using QuickSwitcherViewModel quick = OpenQuickSwitcher(
            session,
            fixture.Root,
            new SwitcherFile("A/note.md", "note.md"),
            new SwitcherFile("A/note.markdown", "note.markdown"),
            new SwitcherFile("B/note.markdown", "note.markdown"),
            new SwitcherFile("C/other.md", "other.md"));
        // The premise: raw names that differ, ONE spoken label — within one
        // folder (spec round 26) and across two.
        Assert.Equal(
            ["note", "note", "note", "other"],
            quick.Results.Select(row => row.DisplayName).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["note.markdown", "note.markdown", "note.md", "other.md"],
            quick.Results.Select(row => row.Name).Order(StringComparer.Ordinal));
        HostedNames("QuickSwitcherResults", quick, names => Assert.Equal(
            ["note, A/note.markdown", "note, A/note.md", "note, B/note.markdown", "other"],
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

    /// <summary>Codex PR 3 round 6, owner decision OD-8 — occurrence
    /// identity, found by the round's scope probe: Assign Shortcut puts one
    /// note in two slots, and two value-equal shortcut records were ONE item
    /// to UIA (WPF keys item peers by equality) — and Remove Shortcut took the
    /// first of the two, not the selected one. Each shortcut is its own
    /// occurrence: two slots are two items, read apart, and removing the
    /// second removes the second.</summary>
    [Fact]
    public void OneNoteInTwoShortcutSlotsIsTwoItems() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "shortcut-occurrences");
        File.WriteAllText(Path.Combine(fixture.Root, "note.md"), "plain body\n");
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
        Assert.True(
            PumpedDispatcher.PumpUntil(() => sidebar.TreeRefreshCompletion.IsCompleted, TimeSpan.FromSeconds(10)),
            "the sidebar's first tree load did not land");
        sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == "note.md");
        sidebar.AssignShortcut(1);
        sidebar.AssignShortcut(2);
        Assert.Equal(2, sidebar.Shortcuts.Count);
        SidebarShortcutViewModel first = sidebar.Shortcuts[0];
        SidebarShortcutViewModel second = sidebar.Shortcuts[1];
        Assert.NotSame(first, second);

        HostedNames("SidebarShortcuts", sidebar, names => Assert.Equal(
            ["note.md, file shortcut, note.md, shortcut 1", "note.md, file shortcut, note.md, shortcut 2"],
            names));

        sidebar.SelectedShortcut = second;
        sidebar.RemoveShortcutCommand.Execute(null);
        Assert.Same(first, Assert.Single(sidebar.Shortcuts));
    });

    /// <summary>Filters <paramref name="sidebar"/> once its first tree load
    /// has landed. That load (the constructor's Refresh) yields to the
    /// thread pool, and with these facts' inline contexts it publishes there
    /// and re-runs the filter from that thread: typed before it lands, the
    /// filter's rows could be replaced under the fact.</summary>
    private static void FilterWhenSettled(FilesSidebarViewModel sidebar, string query, int expected)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => sidebar.TreeRefreshCompletion.IsCompleted, TimeSpan.FromSeconds(10)),
            "the sidebar's first tree load did not land");
        sidebar.FilterText = query;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => sidebar.FilterCompletion.IsCompleted && sidebar.FilterResults.Count == expected,
                TimeSpan.FromSeconds(10)),
            $"the filter published {sidebar.FilterResults.Count} results");
    }

    private static QuickSwitcherViewModel OpenQuickSwitcher(
        VaultSession session, string root, params SwitcherFile[] files) =>
        OpenQuickSwitcher(session, root, _ => { }, files);

    private static QuickSwitcherViewModel OpenQuickSwitcher(
        VaultSession session, string root, Action<A11yEvent> announce, params SwitcherFile[] files)
    {
        var quick = new QuickSwitcherViewModel(
            session,
            root,
            announce,
            files,
            Path.Combine(root, "device-state"),
            debounceRanking: false);
        quick.Open();
        Assert.Equal(files.Length, quick.Results.Count);
        return quick;
    }

    /// <summary>The spec review, rounds 21 and 26: a tab is titled by its
    /// display name, the extension stripped, so tabs of different files can
    /// share a title — each then reads its full visible path (folder, name
    /// and extension); two tabs of ONE file (Duplicate Tab) share even that,
    /// so they read their path AND their places (codex PR 3 round 4: the
    /// place never replaces the path). Every state a tab can be in — unsaved,
    /// missing from disk, both — is spoken over the tab's own told-apart
    /// name.</summary>
    [Fact]
    public void WorkspaceTabsSharingATitleReadTheirPathElseTheirPlaceInEveryState() => RunSta(() =>
        TabsReadApartInEveryState(
            "shared-tab-titles",
            ["A/note.md", "B/note.md"],
            duplicateLast: true,
            ["note, A/note.md", "note, B/note.md, tab 2", "note, B/note.md, tab 3"]));

    /// <summary>Spec round 26: two files of one folder whose names differ
    /// only by extension share title AND folder — a folder alone would leave
    /// them alike; the full path tells them apart.</summary>
    [Fact]
    public void WorkspaceTabsOfOneFolderReadTheirFileNamesInEveryState() => RunSta(() =>
        TabsReadApartInEveryState(
            "shared-tab-folder",
            ["A/note.md", "A/note.markdown"],
            duplicateLast: false,
            ["note, A/note.md", "note, A/note.markdown"]));

    /// <summary>Spec round 26: a note and a canvas of one name, side by
    /// side — two kinds, one title — read their full paths.</summary>
    [Fact]
    public void WorkspaceTabsOfTwoKindsReadTheirFileNamesInEveryState() => RunSta(() =>
        TabsReadApartInEveryState(
            "shared-tab-kinds",
            ["note.md", "note.canvas"],
            duplicateLast: false,
            ["note, note.md", "note, note.canvas"]));

    /// <summary>Opens <paramref name="paths"/> in one pane (and duplicates
    /// the last when <paramref name="duplicateLast"/>), hosts the authored
    /// tab strip over the real tabs, and walks them through every state:
    /// at rest, the first markdown tab unsaved, the last tab missing from
    /// disk, the first unsaved AND missing. Each reads
    /// <paramref name="told"/> under its own state's format.</summary>
    private static void TabsReadApartInEveryState(
        string name, string[] paths, bool duplicateLast, string[] told)
    {
        using FixtureVault fixture = FixtureVault.Create(0, name);
        foreach (string path in paths)
        {
            string full = Path.Combine(fixture.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, path.EndsWith(".canvas", StringComparison.Ordinal)
                ? "{\"nodes\":[],\"edges\":[]}\n"
                : "plain body\n");
        }
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        workspace.OpenPath(paths[0]);
        foreach (string path in paths.Skip(1))
        {
            workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
        }
        if (duplicateLast)
        {
            ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        }
        WorkspaceTabViewModel[] tabs = [.. workspace.ActiveGroup.Tabs];
        string[] opened = duplicateLast ? [.. paths, paths[^1]] : paths;
        Assert.Equal(opened, tabs.Select(tab => tab.Path));
        // The premise: one title for all of them, the extension stripped.
        Assert.All(tabs, tab => Assert.Equal("note", tab.Title));

        (ItemsControl host, _) = ItemContainerNameBindingTests.AuthoredHost("WorkspaceTabs");
        host.ItemsSource = workspace.ActiveGroup.Tabs;
        static string Stated(string spoken, WorkspaceTabViewModel tab) => (tab.IsDirty, tab.IsMissingFromDisk) switch
        {
            (false, false) => spoken,
            (true, false) => $"{spoken}, unsaved changes",
            (false, true) => $"{spoken}, missing from disk",
            (true, true) => $"{spoken}, missing from disk, unsaved changes",
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
            WorkspaceTabViewModel edited = tabs.First(tab => tab.IsMarkdown);
            edited.Text = "edited body\n";
            Assert.True(edited.IsDirty);
            Expect("one unsaved");
            tabs[^1].InvalidatePath();
            Assert.True(tabs[^1].IsMissingFromDisk);
            Expect("one missing");
            edited.InvalidatePath();
            Assert.True(edited.IsDirty && edited.IsMissingFromDisk);
            Expect("one unsaved and missing");
        });
    }

    /// <summary>Codex PR 3 round 5: a tab's state is the sibling rule's
    /// input, never text appended after it. Beside draft.md in each state
    /// sits a tab whose file's own title is exactly what draft.md's tab reads
    /// in that state — "draft, unsaved changes.md" beside draft.md unsaved —
    /// and the two read apart by their paths, draft.md's still ending in its
    /// state. Appended after the rule (the trigger formats this replaced),
    /// both read "draft, unsaved changes".</summary>
    [Theory]
    [InlineData("unsaved changes")]
    [InlineData("missing from disk")]
    [InlineData("missing from disk, unsaved changes")]
    public void ATabsStateNeverMakesItReadLikeAnotherTabsTitle(string state) => RunSta(() =>
    {
        string natural = $"draft, {state}";
        string[] paths = ["draft.md", $"{natural}.md"];
        using FixtureVault fixture = FixtureVault.Create(0, "stated-tab-titles");
        foreach (string path in paths)
        {
            File.WriteAllText(Path.Combine(fixture.Root, path), "plain body\n");
        }
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        workspace.OpenPath(paths[0]);
        workspace.OpenPath(paths[1], WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel[] tabs = [.. workspace.ActiveGroup.Tabs];
        Assert.Equal(paths, tabs.Select(tab => tab.Path));
        // The premise: the second tab's own title is the first's name in
        // that state.
        Assert.Equal(["draft", natural], tabs.Select(tab => tab.Title));

        (ItemsControl host, _) = ItemContainerNameBindingTests.AuthoredHost("WorkspaceTabs");
        host.ItemsSource = workspace.ActiveGroup.Tabs;
        ItemContainerNameBindingTests.Hosted(host, () =>
        {
            Assert.Equal(["draft", natural], ItemContainerNameBindingTests.ItemNames(host));
            WorkspaceTabViewModel draft = tabs[0];
            if (state.Contains("unsaved", StringComparison.Ordinal))
            {
                draft.Text = "edited body\n";
            }
            if (state.Contains("missing", StringComparison.Ordinal))
            {
                draft.InvalidatePath();
            }
            Assert.Equal(state, draft.SpokenState);
            string[] read = ItemContainerNameBindingTests.ItemNames(host);
            AssertDistinct("WorkspaceTabs", read);
            Assert.Equal([$"draft, draft.md, {state}", $"{natural}, {paths[1]}"], read);
        });
    });

    /// <summary>Codex PR 3 round 5 (the post-rule sweep): speech that names
    /// a row outside UIA — the announcement as the arrow reaches it — names
    /// it as its list does. Quick Open's and the Files filter's namesakes
    /// were announced by their bare labels, all alike; they are announced
    /// under the one rule, path and all, and a row whose label no sibling
    /// shares stays bare.</summary>
    [Fact]
    public void ASelectionAnnouncesItsRowAsTheListNamesIt() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        string LastSelected() => announced.OfType<A11yEvent.RowSelected>().Last().Name;

        using FixtureVault fixture = FixtureVault.Create(0, "selection-namesakes");
        foreach (string path in new[] { "A/note.md", "B/note.md", "notes-extra.md" })
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

        using QuickSwitcherViewModel quick = OpenQuickSwitcher(
            session,
            fixture.Root,
            announced.Add,
            new SwitcherFile("A/note.md", "note.md"),
            new SwitcherFile("A/note.markdown", "note.markdown"),
            new SwitcherFile("C/other.md", "other.md"));
        void Select(string path)
        {
            // Opening selects the first row silently; start from none so
            // every selection speaks.
            quick.SelectedRow = null;
            quick.SelectedRow = quick.Results.Single(row => row.Path == path);
        }
        Select("A/note.markdown");
        Assert.Equal("note, A/note.markdown", LastSelected());
        Select("A/note.md");
        Assert.Equal("note, A/note.md", LastSelected());
        Select("C/other.md");
        Assert.Equal("other", LastSelected());

        var inline = new InlineContext();
        var sidebar = new FilesSidebarViewModel(
            session,
            announced.Add,
            vaultRoot: fixture.Root,
            localAppDataRoot: Path.Combine(fixture.Root, "device-state"),
            filterUiContext: inline,
            treeUiContext: inline,
            treeWorker: (work, _) => { work(); return Task.CompletedTask; },
            filterWorker: (work, _) => { work(); return Task.CompletedTask; },
            filterDelay: _ => Task.CompletedTask);
        FilterWhenSettled(sidebar, "note", 3);
        sidebar.SelectedNode = sidebar.FilterResults.Single(row => row.Path == "B/note.md");
        Assert.Equal("note.md, B/note.md", LastSelected());
        sidebar.SelectedNode = sidebar.FilterResults.Single(row => row.Path == "notes-extra.md");
        Assert.Equal("notes-extra.md", LastSelected());
    });

    /// <summary>A note that links to target.md and embeds it lists two
    /// outgoing rows that both read "Link to target.md" (mac's labels, the
    /// role on the badge): the embed adds its badge, so the two read apart
    /// without a number, and two plain links to one note read their places.
    /// (The right-pane journey's fixture is exactly this note.)</summary>
    [Fact]
    public void OutgoingLinksToOneNoteReadTheirBadgeElseTheirPlace() => RunSta(() =>
    {
        static Panels.OutgoingLinkRowViewModel Link(bool embed, uint ordinal) => new(new OutgoingLink(
            "target.md", "target", null, embed ? "embed" : "wikilink", embed, false, false, string.Empty,
            ordinal, 0, 0, null));
        var rows = new System.Collections.ObjectModel.ObservableCollection<Panels.OutgoingLinkRowViewModel>
        {
            Link(embed: false, 0),
            Link(embed: true, 1),
        };
        var context = new { Panels = new { OutgoingLinks = rows } };
        HostedNames("PanelOutgoingLinksList", context, names => Assert.Equal(
            ["Link to target.md", "Link to target.md, Embed"], names));
        rows.Add(Link(embed: false, 2));
        HostedNames("PanelOutgoingLinksList", context, names => Assert.Equal(
            ["Link to target.md, link 1", "Link to target.md, Embed", "Link to target.md, link 3"], names));
    });

    /// <summary>Codex PR 3 round 4: a top-level folder named "Vault root"
    /// reads exactly like the pinned root destination. The picker's rows are
    /// on the sibling rule — the folder adds its place, the root stays the
    /// root — and moving the selection speaks the same composed name the
    /// list item reads, so the reader always knows where Enter moves the
    /// files.</summary>
    [Fact]
    public void AFolderNamedVaultRootReadsApartFromTheRoot() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        var picker = new FileManagement.MoveToPickerViewModel(
            ["Vault root", "alpha", "X/Vault root"],
            rootIsLegal: true,
            itemNoun: "a.md",
            confirmed: _ => { },
            createAndMove: _ => { },
            cancelled: () => { },
            newFolderPathAllowed: _ => true,
            announce: announced.Add);
        string[] composed = ["Vault root", "Vault root, folder Vault root", "alpha", "Vault root. X/Vault root."];
        Assert.Equal(composed, picker.Rows.Select(row => row.SpokenName));
        HostedNames("MoveToList", picker, names => Assert.Equal(composed, names));

        announced.Clear();
        picker.SelectedRow = picker.Rows[1];
        Assert.Equal(
            "Vault root, folder Vault root",
            Assert.Single(announced.OfType<A11yEvent.RowSelected>()).Name);
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
            names.Distinct(SiblingNames.ReadAlike).Count() == names.Length,
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

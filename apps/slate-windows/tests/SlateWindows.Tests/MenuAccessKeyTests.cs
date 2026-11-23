// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SlateWindows.Bases;
using SlateWindows.Graph;
using SlateWindows.Tests.Censuses;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-22b: no two items of one menu level share an access key, so each item's key is
/// its own. Typed into the open level, it runs nothing while its item is off, where on main a
/// twin that shared the key ran at once (step 0 rows 2, 5, 6, 7, 9, 11, 12, 13, 20, 23, 37);
/// and it runs its item at once while the item is on, where on main a shared key only moved
/// the keys to it.
/// </summary>
/// <remarks>
/// The shipped window over a real Files sidebar, attached through the lifecycle's own setter,
/// each state built through the view models. The level opens by the window's own Alt route,
/// <c>MainWindow.OpenAppMenu</c>, which Alt and the menu's letter run: the keys land on its
/// first enabled, visible row. Each key is read from the item's live <see cref="AccessText"/>
/// with the menu open (else from its header, as the census reads it), so no fact names a
/// letter, and it is typed as the text WPF's access keys listen on. A desktop whose session
/// has no foreground window opens no menu popup (#1680); there a fact skips.
/// </remarks>
public sealed class MenuAccessKeyTests
{
    private const string Alpha = "---\nstatus: active\n---\n# Alpha\n\nAlpha links to [[beta]].\n";
    private const string Beta = "---\nstatus: draft\n---\n# Beta\n\nBeta links to [[alpha]] and [[gamma]].\n";
    private const string Gamma = "---\nstatus: done\n---\n# Gamma\n\nGamma body.\n";

    private const string NotesBase =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n      - note.status\n";

    /// <summary>The states the rows stand in, each built through the view models.</summary>
    public enum MenuState
    {
        /// <summary>beta.md open as Quick Open opens it, one pane: nothing selected in the
        /// Files tree, no recents.</summary>
        Usual,

        /// <summary>The usual state with an import running, held at its worker.</summary>
        ImportRunning,

        /// <summary>The usual state with the Files sort changed from the vault's default.</summary>
        NothingSelectedSortChanged,

        /// <summary>The usual state with a folder that has no folder note selected in the tree.</summary>
        AFolderRow,

        /// <summary>beta.md opened from the tree (one recent), then an import running.</summary>
        ImportWithARecent,

        /// <summary>A folder added to the shortcuts and its shortcut selected, no recents, then
        /// an import running.</summary>
        ImportWithAShortcutSelected,

        /// <summary>Every tab closed.</summary>
        NoTab,

        /// <summary>beta.md, then gamma.md selected in the tree, then Back.</summary>
        ForwardHistory,

        /// <summary>Notes.base open and loaded.</summary>
        BaseTab,

        /// <summary>A graph tab showing a live diagram.</summary>
        DiagramGraph,
    }

    /// <summary>An item's own key, typed into the open level while the item is off, runs
    /// nothing: no item of the level clicks, nothing is announced, and the menu keeps the
    /// keys. The premise: the item off and its former twin on, both read with the menu open.
    /// An item is named by its automation id, or by its command's binding path where it has
    /// none.</summary>
    [Theory]
    [InlineData("FileMenu", "NewCanvasMenuItem", "CloseVaultMenuItem", MenuState.ImportRunning)]
    [InlineData("FilesSidebarMenu", "FilesSidebarOpenInSplitMenuItem", "FilesSidebarUseVaultDefaultSortMenuItem", MenuState.NothingSelectedSortChanged)]
    [InlineData("FilesSidebarMenu", "FilesSidebarAddToShortcutsMenuItem", "FilesSidebarUseVaultDefaultSortMenuItem", MenuState.NothingSelectedSortChanged)]
    [InlineData("FilesSidebarMenu", "FilesSidebarOpenInSplitMenuItem", "FilesSidebarAddToShortcutsMenuItem", MenuState.AFolderRow)]
    [InlineData("FilesSidebarMenu", "FilesSidebarUseVaultDefaultSortMenuItem", "FilesSidebarAddToShortcutsMenuItem", MenuState.AFolderRow)]
    [InlineData("FilesSidebarMenu", "FilesSidebarCancelImportMenuItem", "FilesSidebarCollapseAllMenuItem", MenuState.Usual)]
    [InlineData("FilesSidebarMenu", "FilesSidebarRemoveShortcutMenuItem", "FilesSidebarRefreshMenuItem", MenuState.Usual)]
    [InlineData("FilesSidebarMenu", "FilesSidebarClearRecentsMenuItem", "FilesSidebarRefreshMenuItem", MenuState.Usual)]
    [InlineData("FilesSidebarMenu", "FilesSidebarForwardMenuItem", "FilesSidebarFocusFilterMenuItem", MenuState.Usual)]
    [InlineData("FilesSidebarMenu", "FilesSidebarRefreshMenuItem", "FilesSidebarClearRecentsMenuItem", MenuState.ImportWithARecent)]
    [InlineData("FilesSidebarMenu", "FilesSidebarRemoveShortcutMenuItem", "FilesSidebarClearRecentsMenuItem", MenuState.ImportWithARecent)]
    [InlineData("FilesSidebarMenu", "FilesSidebarRefreshMenuItem", "FilesSidebarRemoveShortcutMenuItem", MenuState.ImportWithAShortcutSelected)]
    [InlineData("FilesSidebarMenu", "FilesSidebarClearRecentsMenuItem", "FilesSidebarRemoveShortcutMenuItem", MenuState.ImportWithAShortcutSelected)]
    [InlineData("WorkspaceMenu", "Workspace.CloseActiveTabCommand", "HistoryChangesSinceOpenMenuItem", MenuState.NoTab)]
    [InlineData("WorkspaceMenu", "ClosePaneMenuItem", "Workspace.ToggleRightPaneCommand", MenuState.Usual)]
    [InlineData("GraphMenu", "GraphZoomOutMenuItem", "GraphOpenTabMenuItem", MenuState.Usual)]
    public void AnItemsOwnKeyRunsNothingElseWhileItIsOff(string level, string item, string twin, MenuState state) => RunSta(() =>
    {
        using var shell = new MenuShell(HoldsAnImport(state));
        shell.Build(state);
        MenuItem menu = shell.Level(level);
        MenuItem off = MenuShell.Item(menu, item);
        MenuItem on = MenuShell.Item(menu, twin);
        ClickLog clicks = ClickLog.Of(menu);
        shell.Open(level, off, on);
        Assert.False(off.IsEnabled, $"premise: {Name(off)} is on with the menu open in the state {state}");
        Assert.True(on.IsEnabled, $"premise: {Name(on)} is off with the menu open in the state {state}");
        char key = KeyOf(off);
        shell.ClearSaid();

        shell.Type(key);

        Assert.True(
            clicks.Ran.Count == 0,
            $"{Name(off)}'s own key {key}, typed in the open menu while it is off, ran {string.Join(" and ", clicks.Ran.Select(Name))}");
        string[] said = shell.Said();
        Assert.True(
            said.Length == 0,
            $"{Name(off)}'s own key {key}, typed while it is off, was followed by: {string.Join(" | ", said)}");
        Assert.True(
            shell.KeysIn(menu),
            $"{Name(off)}'s own key {key}, typed while it is off, closed the menu or took the keys out of it: they are on {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>An item whose key changed runs at once on its own key while it is on, and no
    /// other item does. Export View as CSV's picker and scope question are answered with
    /// cancel, so no dialog opens.</summary>
    [Theory]
    [InlineData("FileMenu", "NewCanvasMenuItem", MenuState.Usual)]
    [InlineData("FilesSidebarMenu", "FilesSidebarForwardMenuItem", MenuState.ForwardHistory)]
    [InlineData("WorkspaceMenu", "SwitchThemeMenuItem", MenuState.Usual)]
    [InlineData("WorkspaceMenu", "ShowTasksReviewMenuItem", MenuState.Usual)]
    [InlineData("BaseMenu", "BasesExportCsvMenuItem", MenuState.BaseTab)]
    [InlineData("GraphMenu", "GraphZoomOutMenuItem", MenuState.DiagramGraph)]
    public void AChangedItemRunsOnItsOwnKey(string level, string item, MenuState state) => RunSta(() =>
    {
        using var shell = new MenuShell(HoldsAnImport(state));
        shell.Build(state);
        MenuItem menu = shell.Level(level);
        MenuItem target = MenuShell.Item(menu, item);
        ClickLog clicks = ClickLog.Of(menu);
        shell.Open(level, target);
        Assert.True(target.IsEnabled, $"premise: {Name(target)} is off with the menu open in the state {state}");
        char key = KeyOf(target);

        shell.Type(key);

        int own = clicks.Ran.Count(ran => ReferenceEquals(ran, target));
        string others = string.Join(" and ", clicks.Ran.Where(ran => !ReferenceEquals(ran, target)).Select(Name));
        Assert.True(
            own == 1 && others.Length == 0,
            $"{Name(target)}'s own key {key}, typed in the open menu while it is on, ran it {own} time(s)"
            + (others.Length > 0 ? $" and ran {others}" : string.Empty)
            + $"; the keys are on {Describe(Keyboard.FocusedElement)} and the menu is {(menu.IsSubmenuOpen ? "open" : "closed")}");
    });

    // ---- Support ------------------------------------------------------------------

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "the menu access key fact timed out.");

    private static bool HoldsAnImport(MenuState state) =>
        state is MenuState.ImportRunning or MenuState.ImportWithARecent or MenuState.ImportWithAShortcutSelected;

    /// <summary>The item's access key: its live AccessText's, read with the menu open, else
    /// the one its header gives as the census reads it.</summary>
    private static char KeyOf(MenuItem item)
    {
        char live = ShownShell.Descendants(item).OfType<AccessText>()
            .Select(text => text.AccessKey)
            .FirstOrDefault(key => key != '\0');
        char? key = live != '\0'
            ? live
            : MenuAccessKeyCensus.AccessKeyOf(item.Header as string ?? string.Empty);
        Assert.True(key is not null, $"{Name(item)} carries no access key");
        return char.ToUpperInvariant(key.Value);
    }

    private static string Name(MenuItem item)
    {
        string header = item.Header as string ?? item.Header?.ToString() ?? string.Empty;
        int index = header.IndexOf('_', StringComparison.Ordinal);
        return "\"" + (index < 0 ? header : header.Remove(index, 1)) + "\"";
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        MenuItem item => $"the menu item {Name(item)}",
        FrameworkElement framework => $"{framework.GetType().Name} '{AutomationProperties.GetAutomationId(framework)}'",
        _ => element.GetType().Name,
    };

    /// <summary>Every Click of a level's items, each counted on the item that was
    /// clicked.</summary>
    private sealed class ClickLog
    {
        public List<MenuItem> Ran { get; } = [];

        public static ClickLog Of(MenuItem level)
        {
            var log = new ClickLog();
            foreach (MenuItem item in level.Items.OfType<MenuItem>())
            {
                item.Click += (_, e) =>
                {
                    if (ReferenceEquals(e.OriginalSource, item))
                    {
                        log.Ran.Add(item);
                    }
                };
            }

            return log;
        }
    }

    /// <summary>An import whose sources come back at once and whose worker waits for the
    /// fact's release, so the sidebar's import stays running until then. A
    /// <see cref="TaskCompletionSource"/>, so there is nothing to dispose while the worker
    /// may still be leaving it.</summary>
    private sealed class RunningImport
    {
        private readonly string _source = Path.Combine(Path.GetTempPath(), $"slate-menu-keys-import-{Guid.NewGuid():N}.md");
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RunningImport() => File.WriteAllText(_source, "# Imported\n");

        /// <summary>The worker's run, once the import has reached it.</summary>
        public Task Work { get; private set; } = Task.CompletedTask;

        public Task<IReadOnlyList<string>> PickSources() => Task.FromResult<IReadOnlyList<string>>([_source]);

        public Task Run(Action work, CancellationToken cancellation)
        {
            Work = Task.Run(() =>
            {
                _ = _released.Task.Wait(TimeSpan.FromSeconds(60));
                work();
            });
            return Work;
        }

        public void Release() => _ = _released.TrySetResult();

        public void DeleteSource()
        {
            try
            {
                File.Delete(_source);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The shipped window (<see cref="ShownShell"/>) with a Files sidebar attached
    /// through the lifecycle's own setter and the lifecycle's relay of its opens, every
    /// announcement heard (the workspace's, the sidebar's and the window announcer's), and
    /// the Base export's picker and scope question answered with cancel.</summary>
    private sealed class MenuShell : IDisposable
    {
        private static readonly MethodInfo OpenAppMenu = typeof(MainWindow).GetMethod(
                "OpenAppMenu", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("MainWindow.OpenAppMenu is gone; the facts open a menu by the window's Alt route");

        private readonly string _deviceState = Path.Combine(Path.GetTempPath(), $"slate-menu-keys-device-{Guid.NewGuid():N}");
        private readonly RunningImport? _import;
        private readonly List<A11yEvent> _sidebarSaid = [];
        private readonly List<RenderedAnnouncement> _windowSaid = [];
        private readonly MenuItem _app;

        public MenuShell(bool holdsAnImport)
        {
            Host = new ShownShell(
                ("Projects/alpha.md", Alpha),
                ("beta.md", Beta),
                ("gamma.md", Gamma),
                ("Notes.base", NotesBase));
            Directory.CreateDirectory(_deviceState);
            _import = holdsAnImport ? new RunningImport() : null;
            Sidebar = new FilesSidebarViewModel(
                Host.Session,
                _sidebarSaid.Add,
                vaultRoot: Host.Root,
                pickImportSources: _import is null ? null : _import.PickSources,
                localAppDataRoot: _deviceState,
                importWorker: _import is null ? null : _import.Run);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            Sidebar.OpenTargetRequested += OpenAsTheLifecycleDoes;
            SetFileSidebar(Sidebar);
            Host.RelayFocusBoundaries();
            var announcer = (AccessibilityNotificationDispatcher)(typeof(MainWindow)
                .GetField("_announcer", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(Host.Shell) ?? throw new InvalidOperationException("MainWindow._announcer is gone"));
            announcer.PostedForTests = _windowSaid.Add;
            Host.Workspace.PickBasesExportDestination = (_, _) => null;
            Host.Workspace.BasesExportScopePrompt = _ => WorkspaceViewModel.BasesExportScope.Cancel;
            Host.Settle();
            _app = Host.Shell.MainMenu.Items.OfType<MenuItem>()
                .Single(menu => AutomationProperties.GetAutomationId(menu) == "AppMenu");
        }

        public ShownShell Host { get; }

        public FilesSidebarViewModel Sidebar { get; }

        /// <summary>A level under the logo's menu, by its automation id; the Files Sidebar
        /// submenu is the File menu's.</summary>
        public MenuItem Level(string id) => id == "FilesSidebarMenu"
            ? ById(Level("FileMenu"), id)
            : ById(_app, id);

        /// <summary>An item of <paramref name="level"/> by its automation id, or by its
        /// command's binding path (a name with a dot) where it has none.</summary>
        public static MenuItem Item(MenuItem level, string name) => level.Items.OfType<MenuItem>()
            .Single(item => name.Contains('.', StringComparison.Ordinal)
                ? BindingOperations.GetBindingExpression(item, MenuItem.CommandProperty)?.ParentBinding.Path.Path == name
                : AutomationProperties.GetAutomationId(item) == name);

        private static MenuItem ById(MenuItem parent, string id) => parent.Items.OfType<MenuItem>()
            .Single(item => AutomationProperties.GetAutomationId(item) == id);

        public void Settle()
        {
            for (int turn = 0; turn < 3; turn++)
            {
                Host.Settle();
            }
        }

        // ---- The states ------------------------------------------------------------

        public void Build(MenuState state)
        {
            switch (state)
            {
                case MenuState.Usual:
                    OpenNote("beta.md");
                    break;
                case MenuState.ImportRunning:
                    OpenNote("beta.md");
                    StartImport();
                    break;
                case MenuState.NothingSelectedSortChanged:
                    OpenNote("beta.md");
                    Sidebar.SortMode = SidebarSortMode.NameDescending;
                    Settle();
                    Assert.True(Sidebar.UseVaultDefaultSortCommand.CanExecute(null), "premise: the changed sort did not turn Use Vault Default Sort on");
                    break;
                case MenuState.AFolderRow:
                    OpenNote("beta.md");
                    Select("Projects");
                    break;
                case MenuState.ImportWithARecent:
                    OpenFromTheTree("beta.md");
                    StartImport();
                    break;
                case MenuState.ImportWithAShortcutSelected:
                    OpenNote("beta.md");
                    Select("Projects");
                    Sidebar.AddShortcutCommand.Execute(null);
                    Settle();
                    Sidebar.SelectedShortcut = Assert.Single(Sidebar.Shortcuts);
                    Settle();
                    StartImport();
                    break;
                case MenuState.NoTab:
                    OpenNote("beta.md");
                    for (int tab = 0; tab < 5 && Host.Workspace.ActiveGroup.ActiveTab is not null; tab++)
                    {
                        Host.Workspace.CloseActiveTabCommand.Execute(null);
                        Settle();
                    }

                    Assert.True(Host.Workspace.ActiveGroup.ActiveTab is null, "premise: a tab stayed open");
                    break;
                case MenuState.ForwardHistory:
                    Select("beta.md");
                    Select("gamma.md");
                    Sidebar.HistoryBackCommand.Execute(null);
                    Settle();
                    Assert.True(Sidebar.HistoryForwardCommand.CanExecute(null), "premise: Back left no forward history");
                    LandOnTheEditor();
                    break;
                case MenuState.BaseTab:
                    OpenBase();
                    break;
                case MenuState.DiagramGraph:
                    OpenNote("beta.md");
                    OpenADiagram();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "no such state");
            }
        }

        private void OpenNote(string path)
        {
            Host.Workspace.OpenPath(path);
            Settle();
            LandOnTheEditor();
        }

        private void OpenFromTheTree(string path)
        {
            FileTreeNodeViewModel row = Select(path);
            Assert.True(Sidebar.OpenNode(row, WorkspaceOpenTarget.CurrentTab), $"premise: Enter on {path} opened nothing");
            Settle();
            LandOnTheEditor();
        }

        private FileTreeNodeViewModel Select(string path)
        {
            FileTreeNodeViewModel row = Flatten(Sidebar.RootNodes).Single(node => node.Path == path);
            Sidebar.SelectedNode = row;
            Settle();
            return row;
        }

        private void LandOnTheEditor()
        {
            Assert.True(Host.Land(ShellRegionKind.Editor), "premise: the editor region did not take the keys");
            Settle();
        }

        private void StartImport()
        {
            Sidebar.ImportCommand.Execute(null);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Sidebar.IsImportRunning, TimeSpan.FromSeconds(15)),
                "premise: the import never reached its worker");
            Settle();
        }

        private void OpenBase()
        {
            Host.Workspace.OpenPath("Notes.base");
            BaseDocumentViewModel document = Host.Workspace.ActiveGroup.ActiveTab?.Base
                ?? throw new Xunit.Sdk.XunitException("premise: the base's tab has no document");
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(30)),
                "premise: the base never loaded");
            Settle();
            LandOnTheEditor();
        }

        private void OpenADiagram()
        {
            Host.Workspace.OpenGraph();
            GraphDocumentViewModel document = Host.Workspace.GraphDocument
                ?? throw new Xunit.Sdk.XunitException("premise: Open Graph made no graph document");
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained(), TimeSpan.FromSeconds(30));
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram), "premise: the graph did not switch to Diagram");
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram || document.DiagramError is not null, TimeSpan.FromSeconds(30)),
                "premise: the diagram never built");
            Assert.Null(document.DiagramError);
            Settle();
            LandOnTheEditor();
            Assert.True(document.IsDiagramEffective, "premise: the diagram is not in effect");
        }

        private static IEnumerable<FileTreeNodeViewModel> Flatten(IEnumerable<FileTreeNodeViewModel> nodes)
        {
            foreach (FileTreeNodeViewModel node in nodes)
            {
                yield return node;
                foreach (FileTreeNodeViewModel child in Flatten(node.Children))
                {
                    yield return child;
                }
            }
        }

        /// <summary>The lifecycle's own relay of a sidebar open
        /// (<c>VaultLifecycleViewModel.FileSidebar_OpenTargetRequested</c>).</summary>
        private void OpenAsTheLifecycleDoes(object? sender, (string Path, WorkspaceOpenTarget Target, bool FocusEditor) request) =>
            Host.Workspace.OpenPath(request.Path, request.Target, fromSelection: !request.FocusEditor);

        private void SetFileSidebar(FilesSidebarViewModel? sidebar) =>
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.FileSidebar), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{nameof(VaultLifecycleViewModel.FileSidebar)} is gone"))
            .SetValue(Host.Lifecycle, sidebar);

        // ---- The menu ----------------------------------------------------------------

        /// <summary>The level open by the window's Alt route, the keys on a row of it, and
        /// <paramref name="onScreen"/> on screen; else, where the desktop refuses the menu,
        /// the fact skips. The Files Sidebar submenu opens by its own key typed in the open
        /// File menu.</summary>
        public void Open(string level, params MenuItem[] onScreen)
        {
            Host.Shell.Activate();
            Settle();
            string top = level == "FilesSidebarMenu" ? "FileMenu" : level;
            MenuItem topMenu = Level(top);
            _ = OpenAppMenu.Invoke(Host.Shell, [top]);
            if (!PumpedDispatcher.PumpUntil(() => KeysIn(topMenu), TimeSpan.FromSeconds(5)))
            {
                SkipWhereTheDesktopRefusesTheMenu($"the keys never reached a row of the {top} menu (they are on {Describe(Keyboard.FocusedElement)})");
            }

            MenuItem menu = Level(level);
            if (!ReferenceEquals(menu, topMenu))
            {
                Settle();
                Type(KeyOf(menu));
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => KeysIn(menu), TimeSpan.FromSeconds(5)),
                    $"premise: {Name(menu)}'s own key did not open it with the keys on a row (they are on {Describe(Keyboard.FocusedElement)})");
            }

            Settle();
            foreach (MenuItem item in onScreen)
            {
                if (!item.IsVisible)
                {
                    SkipWhereTheDesktopRefusesTheMenu($"{Name(item)} is not on screen");
                }
            }
        }

        public bool KeysIn(MenuItem level) =>
            level.IsSubmenuOpen
            && Keyboard.FocusedElement is MenuItem row
            && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(row), level);

        /// <summary>A key typed in the open menu, as the text the keyboard would deliver:
        /// the route WPF's access keys listen on.</summary>
        public void Type(char key)
        {
            _ = TextCompositionManager.StartComposition(
                new TextComposition(InputManager.Current, Keyboard.FocusedElement, char.ToLowerInvariant(key).ToString()));
            Settle();
        }

        private void CloseMenus()
        {
            foreach (MenuItem menu in _app.Items.OfType<MenuItem>())
            {
                foreach (MenuItem inner in menu.Items.OfType<MenuItem>().Where(item => item.IsSubmenuOpen))
                {
                    inner.IsSubmenuOpen = false;
                }

                menu.IsSubmenuOpen = false;
            }

            _app.IsSubmenuOpen = false;
            Settle();
            if (Host.Lifecycle.IsVaultOpen && Host.Lifecycle.Palette.IsOpen)
            {
                Host.Lifecycle.Palette.Dismiss();
                Settle();
            }
        }

        [DoesNotReturn]
        private static void SkipWhereTheDesktopRefusesTheMenu(string what)
        {
            Assert.Skip($"this desktop refused the menu (#1680): {what}, so the access key was not measured on it");
            throw new System.Diagnostics.UnreachableException("Assert.Skip returned instead of ending the fact");
        }

        // ---- What was said -----------------------------------------------------------

        public void ClearSaid()
        {
            Host.Announced.Clear();
            _sidebarSaid.Clear();
            _windowSaid.Clear();
        }

        public string[] Said() =>
        [
            .. Host.Announced.Concat(_sidebarSaid).Select(item => SlateUniffiMethods.A11yRender(item).Text),
            .. _windowSaid.Select(rendered => rendered.Text),
        ];

        public void Dispose()
        {
            CloseMenus();
            if (_import is not null)
            {
                _import.Release();
                PumpedDispatcher.PumpUntilDrained(_import.Work, TimeSpan.FromSeconds(30));
                _ = PumpedDispatcher.PumpUntil(() => !Sidebar.IsImporting, TimeSpan.FromSeconds(15));
            }

            SetFileSidebar(null);
            PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
            Host.Dispose();
            _import?.DeleteSource();
            try
            {
                Directory.Delete(_deviceState, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}

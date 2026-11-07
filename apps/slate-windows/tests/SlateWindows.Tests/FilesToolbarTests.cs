// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// DS-17 (DSD9, DSD12): the Files pane sits under one toolbar with the vault's name as
/// its level 1 heading. The sort, Group by date, the tag tree and the dual pane moved
/// from a stack of controls into Sort and group's menu, each with its name, and the
/// button's name says the order, which the combo box said on focus.
/// </summary>
public sealed class FilesToolbarTests
{
    private readonly ITestOutputHelper _output;

    public FilesToolbarTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheSortMenuListsEveryModeAndChecksTheCurrentOne() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        FilesSidebarViewModel sidebar = attached.Sidebar;
        sidebar.SortMode = SidebarSortMode.ModifiedOldest;
        host.Settle();

        ContextMenu menu = OpenSort(host);
        MenuItem[] modes = [.. menu.Items.OfType<MenuItem>().Where(row => AutomationProperties.GetAutomationId(row).StartsWith(MainWindow.SidebarSortItemIdPrefix, StringComparison.Ordinal))];
        Assert.Equal(
            ["Name (A to Z)", "Name (Z to A)", "Modified (newest)", "Modified (oldest)", "Created (newest)", "Created (oldest)"],
            modes.Select(row => (string)row.Header));
        Assert.Equal(["Modified (oldest)"], modes.Where(row => row.IsChecked).Select(row => (string)row.Header));

        Toggle(modes.Single(row => (string)row.Header == "Created (newest)"));
        host.Settle();
        Assert.Equal(SidebarSortMode.CreatedNewest, sidebar.SortMode);
        Assert.False(menu.IsOpen, "a pick didn't close Sort and group");
    });

    /// <summary>The button's name says the order, and as the keys come back from a pick
    /// it already says the new one, so a reader hears it once.</summary>
    [Fact]
    public void SortAndGroupNamesItsOrderWhenTheKeysComeBack() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        FilesSidebarViewModel sidebar = attached.Sidebar;
        sidebar.GroupByDate = false;
        sidebar.SortMode = SidebarSortMode.NameAscending;
        host.Settle();
        Button button = host.Shell.SidebarSortButton;
        Assert.Equal(SlateUniffiMethods.SidebarSortButtonName(SidebarSortOrder.NameAscending, false), AutomationProperties.GetName(button));

        ContextMenu menu = OpenSort(host);
        bool keys = MenuHasTheKeys(menu);
        var names = new List<string>();
        button.GotKeyboardFocus += (_, _) => names.Add(AutomationProperties.GetName(button));
        Toggle(menu.Items.OfType<MenuItem>().Single(row => AutomationProperties.GetAutomationId(row) == MainWindow.SidebarSortItemIdPrefix + SidebarSortMode.NameDescending));
        host.Settle();

        string expected = SlateUniffiMethods.SidebarSortButtonName(SidebarSortOrder.NameDescending, false);
        Assert.Equal(expected, AutomationProperties.GetName(button));
        string[] returned = keys ? [expected] : [];
        Assert.Equal(returned, names);
        Assert.Same(button, Keyboard.FocusedElement);

        sidebar.GroupByDate = true;
        host.Settle();
        Assert.Equal(SlateUniffiMethods.SidebarSortButtonName(FilesSidebarViewModel.ToCore(sidebar.SortMode), true), AutomationProperties.GetName(button));
    });

    [Fact]
    public void GroupByDateShowTagsAndDualPaneAreCheckableRows() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        FilesSidebarViewModel sidebar = attached.Sidebar;
        sidebar.GroupByDate = false;
        sidebar.ShowTags = false;
        sidebar.IsDualPaneEnabled = false;
        host.Settle();

        ContextMenu menu = OpenSort(host);
        MenuItem Row(string id) => menu.Items.OfType<MenuItem>().Single(row => AutomationProperties.GetAutomationId(row) == id);
        Assert.Equal("Group by date", Row("SidebarGroupDates").Header);
        Assert.Equal("Show tag tree", Row("SidebarShowTags").Header);
        Assert.Equal("Enable dual-pane files sidebar", Row("SidebarDualPaneToggle").Header);
        Assert.All(new[] { "SidebarGroupDates", "SidebarShowTags", "SidebarDualPaneToggle" }, id => Assert.True(Row(id).IsCheckable));

        // The model to the rows, while the menu is open.
        sidebar.ShowTags = true;
        host.Settle();
        Assert.True(Row("SidebarShowTags").IsChecked);

        // The rows to the model.
        Toggle(Row("SidebarDualPaneToggle"));
        host.Settle();
        Assert.True(sidebar.IsDualPaneEnabled);
        menu = OpenSort(host);
        Toggle(Row("SidebarGroupDates"));
        host.Settle();
        Assert.True(sidebar.GroupByDate);
        menu = OpenSort(host);
        Toggle(Row("SidebarShowTags"));
        host.Settle();
        Assert.False(sidebar.ShowTags);
    });

    [Fact]
    public void TheVaultsNameIsTheLevelOneHeadingInTheToolbar() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        var toolbar = (PanelToolbarView)host.Shell.FindName("FilesToolbar");

        Assert.Equal(host.Lifecycle.VaultDisplayName, toolbar.TitleBlock.Text);
        Assert.False(string.IsNullOrEmpty(toolbar.TitleBlock.Text));
        Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(toolbar.TitleBlock));
        Assert.Equal(host.Lifecycle.VaultPath, toolbar.TitleBlock.ToolTip);
        Assert.Single(
            ShownShell.Descendants(host.Shell).OfType<TextBlock>(),
            text => text.IsVisible && AutomationProperties.GetHeadingLevel(text) == AutomationHeadingLevel.Level1);
    });

    /// <summary>No band above the panes: nothing else shows the vault's name or path.</summary>
    [Fact]
    public void TheBandIsGone() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        var toolbar = (PanelToolbarView)host.Shell.FindName("FilesToolbar");

        Assert.DoesNotContain(
            ShownShell.Descendants(host.Shell).OfType<TextBlock>(),
            text => text.IsVisible && !ReferenceEquals(text, toolbar.TitleBlock)
                && (text.Text == host.Lifecycle.VaultDisplayName || text.Text == host.Lifecycle.VaultPath));
        var columns = (FrameworkElement)host.Shell.FindName("WorkspaceColumns");
        var root = (FrameworkElement)host.Shell.FindName("WorkspaceRoot");
        Assert.Equal(0, columns.TranslatePoint(default, root).Y, 1);
    });

    [Fact]
    public void ThePanelToolbarIsFortyAndItsButtonsThirtyTwo() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        var toolbar = (PanelToolbarView)host.Shell.FindName("FilesToolbar");

        Assert.True(toolbar.ActualHeight >= 40, $"the toolbar is {toolbar.ActualHeight:F0} tall.");
        Assert.Equal(
            ["SidebarNewNote", "SidebarSortAndGroup", "SidebarCollapseAll", "SidebarExpandLoaded", "SidebarRefresh"],
            toolbar.Buttons.Select(button => AutomationProperties.GetAutomationId(button)));
        Assert.All(toolbar.Buttons.OfType<Button>(), button =>
        {
            Assert.Equal(32, button.ActualWidth, 1);
            Assert.Equal(32, button.ActualHeight, 1);
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(button)));
        });
    });

    /// <summary>The pane at its narrowest default, 260: the toolbar's buttons all show,
    /// whole, inside the pane.</summary>
    [Fact]
    public void NothingClipsAtTheDefaultWidth() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        Assert.Equal(260, host.Workspace.FilesSidebarWidth);
        using Attached attached = Attach(host);
        var pane = (FrameworkElement)host.Shell.FindName("FilesPaneBorder");
        var toolbar = (PanelToolbarView)host.Shell.FindName("FilesToolbar");

        foreach (Button button in toolbar.Buttons.OfType<Button>())
        {
            Point left = button.TranslatePoint(default, pane);
            Assert.True(left.X >= 0 && left.X + button.ActualWidth <= pane.ActualWidth + 0.5, $"{AutomationProperties.GetName(button)} is at {left.X:F0} in a {pane.ActualWidth:F0} pane.");
        }
    });

    [Fact]
    public void F6StillLandsOnTheTree() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        var toolbar = (PanelToolbarView)host.Shell.FindName("FilesToolbar");
        Assert.True(host.Land(ShellRegionKind.Files));
        Assert.True(host.Shell.FilesTree.IsKeyboardFocusWithin, $"F6 landed on {Keyboard.FocusedElement?.GetType().Name}.");
        Assert.False(toolbar.IsKeyboardFocusWithin, "F6 landed on the toolbar.");
    });

    [Fact]
    public void EscapeReturnsTheKeysToTheButton() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using Attached attached = Attach(host);
        FilesSidebarViewModel sidebar = attached.Sidebar;
        SidebarSortMode before = sidebar.SortMode;
        // From elsewhere: New note (the hosted Files tree is empty, with no row to take the keys).
        var toolbar = (PanelToolbarView)host.Shell.FindName("FilesToolbar");
        Assert.True(toolbar.Buttons[0].Focus(), "premise: New note refused the keys");
        ContextMenu menu = OpenSort(host);

        if (MenuHasTheKeys(menu))
        {
            Press(host, Key.Escape);
        }
        else
        {
            menu.IsOpen = false;
        }

        host.Settle();
        Assert.False(menu.IsOpen);
        Assert.Equal(before, sidebar.SortMode);
        Assert.Same(host.Shell.SidebarSortButton, Keyboard.FocusedElement);
    });

    /// <summary>A Files sidebar on the shown shell, and a vault name and path for the
    /// toolbar to show; the hosted shell has neither of its own.</summary>
    private static Attached Attach(ShownShell host)
    {
        string local = Path.Combine(Path.GetTempPath(), "slate-ds17-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(local);
        var sidebar = new FilesSidebarViewModel(host.Session, _ => { }, localAppDataRoot: local);
        PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
        SetProperty(host, nameof(VaultLifecycleViewModel.VaultDisplayName), "Demo Vault");
        SetProperty(host, nameof(VaultLifecycleViewModel.VaultPath), @"C:\Vaults\Demo Vault");
        SetProperty(host, nameof(VaultLifecycleViewModel.FileSidebar), sidebar);
        host.Settle();
        host.Settle();
        return new Attached(host, sidebar);
    }

    /// <summary>The attached sidebar, taken off the shell and shut down as the palette
    /// harness does, so none of its work outlives the fact's thread.</summary>
    private sealed class Attached(ShownShell host, FilesSidebarViewModel sidebar) : IDisposable
    {
        public FilesSidebarViewModel Sidebar => sidebar;

        public void Dispose()
        {
            SetProperty(host, nameof(VaultLifecycleViewModel.FileSidebar), null);
            PumpedDispatcher.PumpUntilDrained(sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
        }
    }

    private static void SetProperty(ShownShell host, string name, object? value) =>
        (typeof(VaultLifecycleViewModel).GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
        .SetValue(host.Lifecycle, value);

    private static FilesSidebarViewModel Sidebar(ShownShell host) => Assert.IsType<FilesSidebarViewModel>(host.Shell.SidebarSortButton.DataContext);

    private static ContextMenu OpenSort(ShownShell host)
    {
        host.Shell.OpenSidebarSortMenu();
        host.Settle();
        ContextMenu menu = Assert.IsType<ContextMenu>(host.Shell.SidebarSortMenu);
        Assert.True(menu.IsOpen, "Sort and group didn't open");
        return menu;
    }

    /// <summary>Whether the open menu took the keys; a hosted runner's desktop refuses a
    /// popup them (#1680), and the keys stay on the button.</summary>
    private bool MenuHasTheKeys(ContextMenu menu)
    {
        if (menu.IsKeyboardFocusWithin)
        {
            return true;
        }

        _output.WriteLine("DESKTOP REFUSED THE MENU THE KEYS: the keys stayed on Sort and group (#1680).");
        Assert.Same(menu.PlacementTarget, Keyboard.FocusedElement);
        return false;
    }

    /// <summary>A checkable row, as a reader's toggle reaches it.</summary>
    private static void Toggle(MenuItem item)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(item);
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
        PumpedDispatcher.Drain();
    }

    private static void Press(ShownShell host, Key key)
    {
        var target = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        PresentationSource source = PresentationSource.FromVisual(host.Shell)!;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }

        PumpedDispatcher.Drain();
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}

// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>DS-14 (#1815): the window can be 500 wide, and below the room both panes need
/// the right pane steps aside, then Files. A pane that steps aside is suspended, not
/// hidden: what the reader asked for comes back when there's room, and a reader's own show
/// or hide is still the only thing that changes what they asked for.</summary>
public sealed class NarrowPaneTests : IDisposable
{
    private readonly FixtureVault _fixture = FixtureVault.Create(1, "narrow-pane");
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public NarrowPaneTests()
    {
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
    public void WideningRestoresWhatTheUserHadOpen() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        Assert.True(workspace.IsFilesSidebarVisible && workspace.IsRightPaneVisible);
        _announced.Clear();

        workspace.WorkspaceRowWidth = 500;
        Assert.True(workspace.IsFilesSidebarVisible);
        Assert.False(workspace.IsRightPaneVisible);

        workspace.WorkspaceRowWidth = 440;
        Assert.False(workspace.IsFilesSidebarVisible);
        Assert.False(workspace.IsRightPaneVisible);

        workspace.WorkspaceRowWidth = 1200;
        Assert.True(workspace.IsFilesSidebarVisible && workspace.IsRightPaneVisible);
        Assert.Empty(_announced);
    });

    [Fact]
    public void AUserHiddenPaneStaysHiddenWhenWidened() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        workspace.IsRightPaneVisible = false;

        workspace.WorkspaceRowWidth = 500;
        workspace.WorkspaceRowWidth = 1200;

        Assert.False(workspace.IsRightPaneVisible);
        Assert.True(workspace.IsFilesSidebarVisible);

        workspace.IsFilesSidebarVisible = false;
        workspace.IsRightPaneVisible = true;
        workspace.WorkspaceRowWidth = 500;
        workspace.WorkspaceRowWidth = 1200;

        Assert.False(workspace.IsFilesSidebarVisible);
        Assert.True(workspace.IsRightPaneVisible);
    });

    /// <summary>A pane that stepped aside comes back when the reader asks for it, and the
    /// other steps aside to make the room. The ask speaks as it always did; the pane that
    /// made room, with no keys in it, says nothing.</summary>
    [Fact]
    public void AskingForACollapsedPaneShowsIt() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        workspace.WorkspaceRowWidth = 500;
        Assert.False(workspace.RightPaneToggle);
        _announced.Clear();

        workspace.RightPaneToggle = true;

        Assert.True(workspace.IsRightPaneVisible);
        Assert.False(workspace.IsFilesSidebarVisible);
        Assert.IsType<A11yEvent.RightPaneShown>(Assert.Single(_announced));
        _announced.Clear();

        workspace.WorkspaceRowWidth = 520;
        Assert.True(workspace.IsRightPaneVisible);
        Assert.False(workspace.IsFilesSidebarVisible);

        workspace.FilesSidebarToggle = true;

        Assert.True(workspace.IsFilesSidebarVisible);
        Assert.False(workspace.IsRightPaneVisible);
        Assert.IsType<A11yEvent.FilesSidebarShown>(Assert.Single(_announced));
    });

    [Fact]
    public void AHistoryRouteRevealsThePaneInANarrowWindow() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        workspace.WorkspaceRowWidth = 500;

        workspace.ShowHistoryPanel();

        Assert.True(workspace.IsRightPaneVisible);
        Assert.Equal("history", workspace.ActiveLeaf.Id);
        Assert.False(workspace.IsFilesSidebarVisible);
    });

    /// <summary>The toggles read what shows: a suspended pane's button isn't pressed, and
    /// it hears the change. Its two resize steps requery too, since a menu item caches its
    /// command's state and asks again only when told.</summary>
    [Fact]
    public void TheToggleReadsWhatIsShowing() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        var changed = new List<string?>();
        ((INotifyPropertyChanged)workspace).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        int rightRequeried = 0;
        int filesRequeried = 0;
        workspace.WidenRightPaneCommand.CanExecuteChanged += (_, _) => rightRequeried++;
        workspace.WidenFilesSidebarCommand.CanExecuteChanged += (_, _) => filesRequeried++;

        workspace.WorkspaceRowWidth = 500;

        Assert.False(workspace.RightPaneToggle);
        Assert.True(workspace.FilesSidebarToggle);
        Assert.Contains(nameof(WorkspaceViewModel.RightPaneToggle), changed);
        Assert.Contains(nameof(WorkspaceViewModel.IsRightPaneVisible), changed);
        Assert.Contains(nameof(WorkspaceViewModel.RightPaneColumnWidth), changed);
        Assert.False(workspace.WidenRightPaneCommand.CanExecute(null));
        Assert.True(rightRequeried > 0, "the right pane's steps weren't requeried");

        changed.Clear();
        workspace.WorkspaceRowWidth = 440;

        Assert.False(workspace.FilesSidebarToggle);
        Assert.Contains(nameof(WorkspaceViewModel.FilesSidebarToggle), changed);
        Assert.Contains(nameof(WorkspaceViewModel.FilesColumnWidth), changed);
        Assert.True(filesRequeried > 0, "the Files sidebar's steps weren't requeried");
    });

    [Fact]
    public void ACollapseWithFocusElsewhereIsSilent() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.KeysAreInPane = _ => false;
        workspace.WorkspaceRowWidth = 1200;
        _announced.Clear();

        workspace.WorkspaceRowWidth = 500;
        workspace.WorkspaceRowWidth = 440;

        Assert.False(workspace.IsFilesSidebarVisible || workspace.IsRightPaneVisible);
        Assert.Empty(_announced);
    });

    /// <summary>A reader whose pane just went needs to hear it: the pane's own line, once,
    /// and only for the pane the keys were in.</summary>
    [Fact]
    public void ACollapseWithFocusInsideSpeaksThePanesLine() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        workspace.KeysAreInPane = pane => pane == ShellSidebar.RightPane;
        _announced.Clear();

        workspace.WorkspaceRowWidth = 500;
        Assert.IsType<A11yEvent.RightPaneHidden>(Assert.Single(_announced));
        _announced.Clear();

        workspace.KeysAreInPane = pane => pane == ShellSidebar.Files;
        workspace.WorkspaceRowWidth = 440;
        Assert.IsType<A11yEvent.FilesSidebarHidden>(Assert.Single(_announced));
    });

    /// <summary>The editor keeps its 270 against a pane that stepped in by itself, and gives
    /// way only to one the reader asked for, which doesn't fit beside 270 alone.</summary>
    [Fact]
    public void TheEditorsFloorGivesWayOnlyToAnAskedPane() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1200;
        Assert.Equal(WorkspaceViewModel.MinimumEditorWidth, workspace.EditorColumnMinimumWidth);

        workspace.WorkspaceRowWidth = 500;
        Assert.Equal(WorkspaceViewModel.MinimumEditorWidth, workspace.EditorColumnMinimumWidth);

        workspace.RightPaneToggle = true;
        Assert.Equal(WorkspaceViewModel.MinimumRightPaneWidth, workspace.RightPaneWidth);
        Assert.Equal(
            500 - WorkspaceViewModel.MinimumRightPaneWidth - WorkspaceViewModel.SplitterWidth,
            workspace.EditorColumnMinimumWidth);
    });

    [Fact]
    public void TheWindowCanBeFiveHundredWide() => RunSta(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));

        Assert.Equal(500, host.Shell.MinWidth);
        host.Shell.Width = 500;
        host.Settle();

        Assert.True(host.Workspace.IsFilesSidebarVisible);
        Assert.False(host.Workspace.IsRightPaneVisible);
        FrameworkElement status = host.Body("StatusBar");
        double right = status.TranslatePoint(new Point(status.ActualWidth, 0), host.Shell).X;
        Assert.True(right <= host.Shell.ActualWidth + 0.5, $"the status bar ends at {right:F0}px in a {host.Shell.ActualWidth:F0}px window.");
    });

    /// <summary>The keys in a pane that steps aside go to the editor, as a hide's do, and the
    /// reader hears the pane go.</summary>
    [Fact]
    public void ACollapseWithFocusInsideMovesItAndSpeaks() => RunSta(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.RightPaneRail));
        host.Announced.Clear();

        host.Shell.Width = 500;
        host.Settle();

        Assert.False(host.Workspace.IsRightPaneVisible);
        Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)host.Shell).FocusedRegion());
        Assert.Single(host.Announced.OfType<A11yEvent.RightPaneHidden>());
        Assert.Empty(host.Announced.OfType<A11yEvent.RightPaneShown>());
    });

    [Fact]
    public void TheWelcomeViewFitsAtFiveHundred() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        host.Shell.Width = 500;
        host.Settle();

        foreach (string name in new[] { "OpenVaultButton", "WelcomeRoot" })
        {
            FrameworkElement shown = host.Body(name);
            double right = shown.TranslatePoint(new Point(shown.ActualWidth, 0), host.Shell).X;
            Assert.True(right <= host.Shell.ActualWidth + 0.5, $"{name} ends at {right:F0}px in a {host.Shell.ActualWidth:F0}px window.");
        }

        FrameworkElement title = ShownShell.Descendants(host.Body("WelcomeRoot")).OfType<System.Windows.Controls.TextBlock>()
            .First(text => text.Text == "Welcome to Slate");
        double end = title.TranslatePoint(new Point(title.ActualWidth, 0), host.Shell).X;
        Assert.True(end <= host.Shell.ActualWidth, $"the title ends at {end:F0}px in a {host.Shell.ActualWidth:F0}px window.");
    });

    /// <summary>In a narrow window Settings' categories show their glyphs alone, as WinUI's
    /// compact navigation does, each still named and with its name as a tooltip; at the
    /// window's usual size they show their names. The tooltip is the mouse's: the sheet
    /// opens with the keys on the list, where a tooltip would cover the page's description,
    /// and the page's heading already names the category.</summary>
    [Fact]
    public void TheSettingsCategoriesShowTheirGlyphsAloneInANarrowWindow() => RunSta(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        var first = (System.Windows.Controls.ListBoxItem)host.Shell.SettingsCategoriesList.ItemContainerGenerator.ContainerFromIndex(0);
        string name = System.Windows.Automation.AutomationProperties.GetName(first);
        Assert.False(SettingsLayoutGrid.GetIsCompact(first));
        Assert.Null(first.ToolTip);
        Assert.Contains(ShownText(first), text => text == name);

        host.Shell.Width = 500;
        host.Settle();

        Assert.True(SettingsLayoutGrid.GetIsCompact(first));
        Assert.Equal(name, System.Windows.Automation.AutomationProperties.GetName(first));
        Assert.Equal(name, first.ToolTip);
        Assert.False(System.Windows.Controls.ToolTipService.GetShowsToolTipOnKeyboardFocus(first));
        Assert.DoesNotContain(ShownText(first), text => text == name);
        Assert.True(host.Shell.SettingsCategoriesList.ActualWidth <= SettingsLayoutGrid.CompactListWidth);
    });

    /// <summary>A setting's control goes under its label once the row is too narrow for
    /// both, and comes back beside it when there's room.</summary>
    [Fact]
    public void ASettingRowStacksInANarrowWindow() => RunSta(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        SettingRowGrid row = ShownShell.Descendants(host.Body("SettingsSheet")).OfType<SettingRowGrid>().First(grid => grid.IsVisible);
        Assert.False(row.IsStacked);

        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Shell.Width = 500;
        host.Settle();
        Assert.True(row.IsStacked);
        Assert.Equal(1, System.Windows.Controls.Grid.GetRow(row.Children[1]));

        TypeRamp.Apply(RampOf(host.Shell), 1);
        host.Shell.Width = 1120;
        host.Settle();
        Assert.False(row.IsStacked);
        Assert.Equal(1, System.Windows.Controls.Grid.GetColumn(row.Children[1]));
    });

    private static string[] ShownText(DependencyObject root) =>
        [.. ShownShell.Descendants(root).OfType<System.Windows.Controls.TextBlock>().Where(text => text.IsVisible).Select(text => text.Text)];

    private static ResourceDictionary RampOf(FrameworkElement owner) =>
        owner.Resources.MergedDictionaries.First(layer => layer.Contains(TypeRamp.Body.SizeKey));

    private WorkspaceViewModel NewWorkspace() =>
        new(
            _session,
            _fixture.Root,
            () => [],
            _announced.Add,
            startInteractionBackgroundWork: false,
            preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}

// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4b (#1247; the owner's decision on AR-38): the keyboard's route
/// to the two sidebar widths. The splitters left the Tab order (R-5: they
/// are in no region, and Left/Right on a focused one resized the layout),
/// so Widen/Narrow Files Sidebar and Widen/Narrow Right Pane step each
/// width — from the palette or the Workspace menu — and core says the new
/// width (<see cref="A11yEvent.SidebarResized"/>, rendered in
/// <c>a11y.rs</c>, never host copy). A drag on a splitter moves the same
/// width: the columns bind to these two properties both ways.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>One step of a resize command, in device-independent
    /// pixels.</summary>
    internal const double SidebarWidthStep = 40;

    /// <summary>The narrowest a sidebar goes — the columns' own
    /// MinWidth.</summary>
    internal const double MinimumSidebarWidth = 200;

    /// <summary>#1795: the narrowest the right pane goes: its rail, and room beside it
    /// for a panel's buttons at 14 point (DS-2).</summary>
    internal const double MinimumRightPaneWidth = 280;

    /// <summary>The widest a sidebar goes, whatever room the window has.</summary>
    internal const double MaximumSidebarWidth = 640;

    /// <summary>The editor keeps at least this much between the sidebars —
    /// its column's own MinWidth. DS-2 moved 30 of it to the right pane, because
    /// the window's minimum can't grow past half of a common screen.</summary>
    internal const double MinimumEditorWidth = 270;

    /// <summary>The two splitters' own width.</summary>
    internal const double SplitterWidth = 5;

    private double _filesSidebarWidth = 270;
    private double _rightPaneWidth = 320;

    // #1795: the widths a reader chose, which a narrow window holds down for as long
    // as it's narrow.
    private double _filesSidebarChosenWidth = 270;
    private double _rightPaneChosenWidth = 320;

    /// <summary>The Files sidebar's width, bound both ways to its column,
    /// and held inside its range whoever writes it (codex PR 4b r1 F7): a
    /// splitter drag or arrow is bounded only by the column's minimum.</summary>
    public double FilesSidebarWidth
    {
        get => _filesSidebarWidth;
        set
        {
            HoldWidth(ref _filesSidebarWidth, value, ShellSidebar.Files);
            _filesSidebarChosenWidth = _filesSidebarWidth;
            OnPropertyChanged(nameof(FilesColumnWidth));
        }
    }

    /// <summary>The right pane's width, bound both ways to its column, and
    /// held inside its range whoever writes it.</summary>
    public double RightPaneWidth
    {
        get => _rightPaneWidth;
        set
        {
            HoldWidth(ref _rightPaneWidth, value, ShellSidebar.RightPane);
            _rightPaneChosenWidth = _rightPaneWidth;
            OnPropertyChanged(nameof(RightPaneColumnWidth));
        }
    }

    /// <summary>#1790: the right pane's column, bound both ways: the pane's width while
    /// it shows, and nothing while it's hidden, so the editor can take the room. A
    /// splitter drag writes the width through here; a hidden pane's width is kept for
    /// when it comes back.</summary>
    public double RightPaneColumnWidth
    {
        get => IsRightPaneVisible ? _rightPaneWidth : 0;
        set
        {
            if (IsRightPaneVisible)
            {
                RightPaneWidth = value;
            }
        }
    }

    /// <summary>The column's floor. A fixed floor would hold a hidden pane's column
    /// open.</summary>
    public double RightPaneColumnMinimumWidth => IsRightPaneVisible ? MinimumRightPaneWidth : 0;

    /// <summary>The splitter's column, which goes with the pane.</summary>
    public double RightPaneSplitterWidth => IsRightPaneVisible ? SplitterWidth : 0;

    /// <summary>DS-7: whether the Files sidebar shows. Not persisted, like the right
    /// pane. Core says which, and the keys in it, when it goes, land in the editor through
    /// the focus guard (MainWindow's WorkspaceColumns landing).</summary>
    public bool IsFilesSidebarVisible
    {
        get => _isFilesSidebarVisible;
        set
        {
            if (SetField(ref _isFilesSidebarVisible, value))
            {
                _announce(value ? new A11yEvent.FilesSidebarShown() : new A11yEvent.FilesSidebarHidden());
                OnPropertyChanged(nameof(FilesSidebarToggle));
                RaiseFilesSidebarResizeStates();
                RaiseFilesColumnStates();
            }
        }
    }

    private bool _isFilesSidebarVisible = true;

    /// <summary>DS-7: the status bar's button, bound both ways, because UIA's Toggle
    /// doesn't run a button's command. The setter is the route, as the command's is.</summary>
    public bool FilesSidebarToggle
    {
        get => IsFilesSidebarVisible;
        set
        {
            if (value != IsFilesSidebarVisible)
            {
                ToggleFilesSidebar();
            }
        }
    }

    public ICommand ToggleFilesSidebarCommand => _toggleFilesSidebarCommand ??=
        new RelayCommand(_ => ToggleFilesSidebar(), _ => true);

    private ICommand? _toggleFilesSidebarCommand;

    private void ToggleFilesSidebar() => IsFilesSidebarVisible = !IsFilesSidebarVisible;

    /// <summary>DS-7: the Files sidebar's column, as <see cref="RightPaneColumnWidth"/>
    /// is the right pane's.</summary>
    public double FilesColumnWidth
    {
        get => IsFilesSidebarVisible ? _filesSidebarWidth : 0;
        set
        {
            if (IsFilesSidebarVisible)
            {
                FilesSidebarWidth = value;
            }
        }
    }

    /// <summary>The column's floor. A fixed floor would hold a hidden sidebar's column
    /// open.</summary>
    public double FilesColumnMinimumWidth => IsFilesSidebarVisible ? MinimumSidebarWidth : 0;

    /// <summary>The splitter's column, which goes with the sidebar.</summary>
    public double FilesSplitterWidth => IsFilesSidebarVisible ? SplitterWidth : 0;

    /// <summary>The width of the row the two sidebars and the editor share,
    /// as the window lays it out; zero until it has been laid out, when
    /// only <see cref="MaximumSidebarWidth"/> bounds a width. A narrower row
    /// brings both widths back into its room (a window shrunk or snapped
    /// under a wide sidebar), so every step is one step and says the
    /// truth.</summary>
    internal double WorkspaceRowWidth
    {
        get => _workspaceRowWidth;
        set
        {
            _workspaceRowWidth = value;
            HoldBothWidths();
        }
    }

    /// <summary>Files first, so a sidebar that spread into a hidden pane's room gives
    /// it back. A hidden pane's own width waits: it takes no room to hold it to. Each
    /// holds what the reader chose, so more room gives a narrowed width back.</summary>
    private void HoldBothWidths()
    {
        HoldFilesSidebarWidth();
        HoldRightPaneWidth();
    }

    private void HoldFilesSidebarWidth()
    {
        if (IsFilesSidebarVisible)
        {
            HoldWidth(ref _filesSidebarWidth, _filesSidebarChosenWidth, ShellSidebar.Files, nameof(FilesSidebarWidth));
            OnPropertyChanged(nameof(FilesColumnWidth));
        }
    }

    private void HoldRightPaneWidth()
    {
        if (IsRightPaneVisible)
        {
            HoldWidth(ref _rightPaneWidth, _rightPaneChosenWidth, ShellSidebar.RightPane, nameof(RightPaneWidth));
            OnPropertyChanged(nameof(RightPaneColumnWidth));
        }
    }

    /// <summary>#1790: the pane's column and splitter follow it, and a Files sidebar
    /// that spread into its room makes way when it comes back.</summary>
    private void RaiseRightPaneColumnStates()
    {
        OnPropertyChanged(nameof(RightPaneColumnWidth));
        OnPropertyChanged(nameof(RightPaneColumnMinimumWidth));
        OnPropertyChanged(nameof(RightPaneSplitterWidth));
        HoldBothWidths();
    }

    /// <summary>DS-7: the twin, so the right pane goes first: it's the one that spread
    /// into the room the sidebar is coming back to.</summary>
    private void RaiseFilesColumnStates()
    {
        OnPropertyChanged(nameof(FilesColumnWidth));
        OnPropertyChanged(nameof(FilesColumnMinimumWidth));
        OnPropertyChanged(nameof(FilesSplitterWidth));
        HoldRightPaneWidth();
        HoldFilesSidebarWidth();
    }

    private double _workspaceRowWidth;

    /// <summary>Stores <paramref name="value"/> bounded to
    /// <paramref name="sidebar"/>'s range — at least the columns' minimum, at
    /// most the ceiling and the room beside the other sidebar and the
    /// editor's minimum. A column the drag wrote follows the held width: its
    /// two-way binding converts, and WPF reads a converted source back after
    /// the write (SidebarResizeTests.ADraggedWidthIsHeldInsideTheRoom).</summary>
    private void HoldWidth(ref double field, double value, ShellSidebar sidebar, [CallerMemberName] string? name = null) =>
        SetField(ref field, Math.Clamp(value, NarrowestWidth(sidebar), WidestSidebarWidth(sidebar)), name);

    private static double NarrowestWidth(ShellSidebar sidebar) =>
        sidebar == ShellSidebar.Files ? MinimumSidebarWidth : MinimumRightPaneWidth;

    public ICommand WidenFilesSidebarCommand => _widenFilesSidebarCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.Files, +1), _ => IsFilesSidebarVisible);

    public ICommand NarrowFilesSidebarCommand => _narrowFilesSidebarCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.Files, -1), _ => IsFilesSidebarVisible);

    public ICommand WidenRightPaneCommand => _widenRightPaneCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.RightPane, +1), _ => IsRightPaneVisible);

    public ICommand NarrowRightPaneCommand => _narrowRightPaneCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.RightPane, -1), _ => IsRightPaneVisible);

    private ICommand? _widenFilesSidebarCommand;
    private ICommand? _narrowFilesSidebarCommand;
    private ICommand? _widenRightPaneCommand;
    private ICommand? _narrowRightPaneCommand;

    /// <summary>A show or hide of the right pane changes whether its two
    /// steps can run: both requery, and the registered catalog with them. A
    /// menu item caches its command's state and asks again only when told
    /// (SidebarResizeTests.TheRightPaneCommandsNeedTheRightPaneShown).</summary>
    private void RaiseRightPaneResizeStates()
    {
        ((RelayCommand)WidenRightPaneCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NarrowRightPaneCommand).RaiseCanExecuteChanged();
        RegisteredCommandStatesChanged?.Invoke();
    }

    /// <summary>DS-7: the same for the Files sidebar's two steps.</summary>
    private void RaiseFilesSidebarResizeStates()
    {
        ((RelayCommand)WidenFilesSidebarCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NarrowFilesSidebarCommand).RaiseCanExecuteChanged();
        RegisteredCommandStatesChanged?.Invoke();
    }

    /// <summary>The widest <paramref name="sidebar"/> may be now: the fixed
    /// ceiling, and the room left beside the other sidebar, the editor's
    /// minimum and the splitters — never below the floor.</summary>
    internal double WidestSidebarWidth(ShellSidebar sidebar)
    {
        if (WorkspaceRowWidth <= 0)
        {
            return MaximumSidebarWidth;
        }

        double other = sidebar == ShellSidebar.Files ? RightPaneColumnWidth : FilesColumnWidth;
        double room = WorkspaceRowWidth - other - MinimumEditorWidth - FilesSplitterWidth - RightPaneSplitterWidth;
        return Math.Max(NarrowestWidth(sidebar), Math.Min(MaximumSidebarWidth, room));
    }

    /// <summary>One step wider (+1) or narrower (−1), bounded; a step
    /// refused at a limit says the sidebar is already there.</summary>
    internal void StepSidebar(ShellSidebar sidebar, int direction)
    {
        double current = sidebar == ShellSidebar.Files ? FilesSidebarWidth : RightPaneWidth;
        double widest = WidestSidebarWidth(sidebar);
        double narrowest = NarrowestWidth(sidebar);
        bool atLimit = direction > 0 ? current >= widest : current <= narrowest;
        if (atLimit)
        {
            _announce(new A11yEvent.SidebarResized(
                sidebar,
                (uint)Math.Round(current),
                direction > 0 ? SidebarWidthLimit.Widest : SidebarWidthLimit.Narrowest));
            return;
        }

        double next = Math.Clamp(current + (direction * SidebarWidthStep), narrowest, widest);
        if (sidebar == ShellSidebar.Files)
        {
            FilesSidebarWidth = next;
        }
        else
        {
            RightPaneWidth = next;
        }

        _announce(new A11yEvent.SidebarResized(sidebar, (uint)Math.Round(next), null));
    }
}

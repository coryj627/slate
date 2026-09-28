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

    /// <summary>The widest a sidebar goes, whatever room the window has.</summary>
    internal const double MaximumSidebarWidth = 640;

    /// <summary>The editor keeps at least this much between the sidebars —
    /// its column's own MinWidth.</summary>
    internal const double MinimumEditorWidth = 300;

    /// <summary>The two splitters' own width.</summary>
    internal const double SplitterWidth = 5;

    private double _filesSidebarWidth = 270;
    private double _rightPaneWidth = 280;

    /// <summary>The Files sidebar's width, bound both ways to its column,
    /// and held inside its range whoever writes it (codex PR 4b r1 F7): a
    /// splitter drag or arrow is bounded only by the column's minimum.</summary>
    public double FilesSidebarWidth
    {
        get => _filesSidebarWidth;
        set => HoldWidth(ref _filesSidebarWidth, value, ShellSidebar.Files);
    }

    /// <summary>The right pane's width, bound both ways to its column, and
    /// held inside its range whoever writes it.</summary>
    public double RightPaneWidth
    {
        get => _rightPaneWidth;
        set => HoldWidth(ref _rightPaneWidth, value, ShellSidebar.RightPane);
    }

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
            FilesSidebarWidth = _filesSidebarWidth;
            RightPaneWidth = _rightPaneWidth;
        }
    }

    private double _workspaceRowWidth;

    /// <summary>Stores <paramref name="value"/> bounded to
    /// <paramref name="sidebar"/>'s range — at least the columns' minimum, at
    /// most the ceiling and the room beside the other sidebar and the
    /// editor's minimum. A write the bound changed is always announced, even
    /// when the held width is the one already stored: the column the drag
    /// wrote then follows the held width, not the dragged one.</summary>
    private void HoldWidth(ref double field, double value, ShellSidebar sidebar, [CallerMemberName] string? name = null)
    {
        double held = Math.Clamp(value, MinimumSidebarWidth, WidestSidebarWidth(sidebar));
        if (!SetField(ref field, held, name) && held != value)
        {
            OnPropertyChanged(name);
        }
    }

    public ICommand WidenFilesSidebarCommand => _widenFilesSidebarCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.Files, +1), _ => true);

    public ICommand NarrowFilesSidebarCommand => _narrowFilesSidebarCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.Files, -1), _ => true);

    public ICommand WidenRightPaneCommand => _widenRightPaneCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.RightPane, +1), _ => IsRightPaneVisible);

    public ICommand NarrowRightPaneCommand => _narrowRightPaneCommand ??=
        new RelayCommand(_ => StepSidebar(ShellSidebar.RightPane, -1), _ => IsRightPaneVisible);

    private ICommand? _widenFilesSidebarCommand;
    private ICommand? _narrowFilesSidebarCommand;
    private ICommand? _widenRightPaneCommand;
    private ICommand? _narrowRightPaneCommand;

    /// <summary>The widest <paramref name="sidebar"/> may be now: the fixed
    /// ceiling, and the room left beside the other sidebar, the editor's
    /// minimum and the splitters — never below the floor.</summary>
    internal double WidestSidebarWidth(ShellSidebar sidebar)
    {
        if (WorkspaceRowWidth <= 0)
        {
            return MaximumSidebarWidth;
        }

        double other = sidebar == ShellSidebar.Files ? RightPaneWidth : FilesSidebarWidth;
        double room = WorkspaceRowWidth - other - MinimumEditorWidth - (2 * SplitterWidth);
        return Math.Max(MinimumSidebarWidth, Math.Min(MaximumSidebarWidth, room));
    }

    /// <summary>One step wider (+1) or narrower (−1), bounded; a step
    /// refused at a limit says the sidebar is already there.</summary>
    internal void StepSidebar(ShellSidebar sidebar, int direction)
    {
        double current = sidebar == ShellSidebar.Files ? FilesSidebarWidth : RightPaneWidth;
        double widest = WidestSidebarWidth(sidebar);
        bool atLimit = direction > 0 ? current >= widest : current <= MinimumSidebarWidth;
        if (atLimit)
        {
            _announce(new A11yEvent.SidebarResized(
                sidebar,
                (uint)Math.Round(current),
                direction > 0 ? SidebarWidthLimit.Widest : SidebarWidthLimit.Narrowest));
            return;
        }

        double next = Math.Clamp(current + (direction * SidebarWidthStep), MinimumSidebarWidth, widest);
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

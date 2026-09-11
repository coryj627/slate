// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;

namespace SlateWindows;

/// <summary>
/// W6-2 PR C (#746), contracts C-9 and C-12: the Graph menu's Verbosity
/// submenu, rebuilt from core's vector for every observed workspace and
/// unwired from the old one (IGP-15) — the window's one graph wiring.
/// </summary>
public partial class MainWindow
{
    private void WireWorkspaceGraph(WorkspaceViewModel workspace) =>
        GraphVerbosityMenu.Populate(GraphVerbosityMenuItem, workspace.GraphPreferences);

    private void UnwireWorkspaceGraph(WorkspaceViewModel workspace)
    {
        _ = workspace;
        GraphVerbosityMenu.Clear(GraphVerbosityMenuItem);
    }
}

// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 8 (R-10, OD-12's one entry): the shell's landing entry
/// (<c>MainWindow.FocusEditorPane</c>) reduced to its raise, for the facts
/// that host a canvas or graph surface WITHOUT the shell. Since OD-12 the
/// workspace's funnel only asks — the shell creates every editor landing
/// request, under its slot — so a view-model fixture that exercises a
/// surface's delivery of the funnel's addressed request (contract A14, Term
/// F6) stands the shell's raise in here: the active tab's canvas or graph
/// document is asked for its landing, addressed to that tab, and a canvas
/// jump's named landing is raised while its tab is still the active one.
/// The shell's own facts (<c>ReadingFocusTests</c>) never attach it.
/// </summary>
internal static class ShellLandingStandIn
{
    internal static WorkspaceViewModel WithShellLandings(this WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.EditorPaneFocusRequested += (_, group) =>
        {
            if (group.ActiveTab is { } tab)
            {
                tab.Canvas?.RequestFocusLanding(tab);
                tab.Graph?.RequestFocusLanding(tab);
            }
        };
        workspace.CanvasNodeLandingRequested += (_, intent) =>
        {
            if (ReferenceEquals(workspace.ActiveGroup.ActiveTab, intent.Tab)
                && ReferenceEquals(intent.Tab.Canvas, intent.Document))
            {
                intent.Document.RequestFocusLanding(intent.Tab, intent.NodeId);
            }
        };
        return workspace;
    }
}

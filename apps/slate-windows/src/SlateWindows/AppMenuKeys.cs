// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// DS-13 (#1814): which menu under the logo Alt and a letter opens
/// (<c>design/windows/readme.md:120</c>). The menus sit inside the logo's popup, where their
/// headers' access keys aren't live until it's open, so the window opens it at the menu.
/// </summary>
internal static class AppMenuKeys
{
    /// <summary>The <c>AutomationId</c> of the menu to open for <paramref name="systemKey"/>,
    /// or null to leave the key alone: under a modal surface, when a live control holds the
    /// letter (the find bar's Alt+N and Alt+W), and for any letter but the six. H waits for
    /// Help (#756).</summary>
    internal static string? Decide(Key systemKey, bool modalOpen, bool liveAccessKey) =>
        modalOpen || liveAccessKey
            ? null
            : systemKey switch
            {
                Key.F => "FileMenu",
                Key.W => "WorkspaceMenu",
                Key.B => "BaseMenu",
                Key.E => "EditorMenu",
                Key.N => "CanvasMenu",
                Key.G => "GraphMenu",
                _ => null,
            };
}

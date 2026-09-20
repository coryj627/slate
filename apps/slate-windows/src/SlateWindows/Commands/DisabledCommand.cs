// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;

namespace SlateWindows.Commands;

/// <summary>
/// The command a menu item or key binding falls back to when its bound
/// path has no target. W7-5 (#1239): items bound through
/// <c>Workspace.ActiveGroup.ActiveTab…</c> resolved to a NULL command
/// with no tab open, and a WPF <c>MenuItem</c> with no command is
/// enabled — so "Activate at Cursor" and "Preview Embed" took keyboard
/// focus and invoked nothing while the items that COULD run were skipped.
/// A binding's <c>FallbackValue</c> of this instance keeps the item
/// disabled until the path resolves.
/// </summary>
internal sealed class DisabledCommand : ICommand
{
    public static DisabledCommand Instance { get; } = new();

    private DisabledCommand()
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => false;

    public void Execute(object? parameter)
    {
    }
}

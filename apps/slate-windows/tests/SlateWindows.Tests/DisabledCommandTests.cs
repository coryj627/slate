// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Commands;

namespace SlateWindows.Tests;

public sealed class DisabledCommandTests
{
    /// <summary>W7-5 (#1239): the fallback an <c>ActiveTab</c>-bound menu
    /// item lands on with no tab open never executes and never raises,
    /// so the item reads and stays disabled instead of taking focus as a
    /// silent no-op.</summary>
    [Fact]
    public void NeverExecutesAndNeverChanges()
    {
        var command = DisabledCommand.Instance;
        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        Assert.False(command.CanExecute(null));
        Assert.False(command.CanExecute("anything"));
        command.Execute(null);
        Assert.Equal(0, raised);
        Assert.Same(command, DisabledCommand.Instance);
    }
}
